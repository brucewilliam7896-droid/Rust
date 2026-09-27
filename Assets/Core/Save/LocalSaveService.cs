using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RustPlus.Core.Diagnostics;
using UnityEngine;

namespace RustPlus.Core.Save
{
    public enum SaveLoadOutcome
    {
        /// <summary>The primary file was read as-is.</summary>
        Loaded,

        /// <summary>The primary file used an older schema; it was upgraded and rewritten (the original is kept as <c>.bak</c>).</summary>
        Migrated,

        /// <summary>The primary file was missing or unreadable; the backup was used and copied back to the primary path.</summary>
        RestoredFromBackup,

        /// <summary>No save existed; a new game was created.</summary>
        CreatedNew,

        /// <summary>Every save file was unreadable; they were quarantined and a new game was created.</summary>
        ReplacedUnreadable
    }

    public sealed class SaveLoadResult
    {
        public SaveGameData Data { get; }
        public SaveLoadOutcome Outcome { get; }

        /// <summary>Paths that unreadable files were moved to. Empty unless something was quarantined.</summary>
        public IReadOnlyList<string> QuarantinedFiles { get; }

        public SaveLoadResult(SaveGameData data, SaveLoadOutcome outcome, IReadOnlyList<string> quarantinedFiles = null)
        {
            Data = data;
            Outcome = outcome;
            QuarantinedFiles = quarantinedFiles ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Schema-versioned JSON persistence for one save slot.
    /// Writes go to <c>.tmp</c>, are flushed to disk, then atomically replace the primary while the previous
    /// primary becomes <c>.bak</c>. Nothing readable is ever deleted: unreadable files are moved to
    /// <c>.quarantine-*</c> files instead of being overwritten.
    /// </summary>
    public sealed class LocalSaveService
    {
        private const string Category = "Save";

        private readonly string _savePath;
        private readonly string _backupPath;
        private readonly string _tempPath;
        private readonly StructuredLogger _logger;

        public LocalSaveService(string savePath, StructuredLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("A save path is required.", nameof(savePath));
            }

            _savePath = savePath;
            _backupPath = savePath + ".bak";
            _tempPath = savePath + ".tmp";
            _logger = logger ?? new StructuredLogger();
        }

        public string SavePath => _savePath;
        public string BackupPath => _backupPath;

        /// <summary>
        /// Writes <paramref name="data"/> and returns the copy that was written (with metadata filled in).
        /// The caller's instance is not modified.
        /// </summary>
        public SaveGameData Save(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            SaveGameData payload = data.Clone();
            payload.SchemaVersion = SaveGameData.CurrentSchemaVersion;
            payload.Meta.SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            payload.Meta.GameVersion = Application.version;

            string directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteDurably(_tempPath, JsonUtility.ToJson(payload, true));

            if (File.Exists(_savePath))
            {
                File.Replace(_tempPath, _savePath, _backupPath, true);
            }
            else
            {
                File.Move(_tempPath, _savePath);
            }

            _logger.Log(LogSeverity.Info, Category, "Save written", $"file={Path.GetFileName(_savePath)} tick={payload.Meta.SaveTick}");
            Telemetry.Record("save.written", ("file", Path.GetFileName(_savePath)), ("tick", payload.Meta.SaveTick));
            return payload;
        }

        /// <summary>
        /// Loads the save, falling back to the backup when needed.
        /// Throws <see cref="SaveNotFoundException"/>, <see cref="SaveVersionException"/> or <see cref="SaveCorruptException"/>.
        /// </summary>
        public SaveGameData Load()
        {
            return LoadWithReport().Data;
        }

        /// <inheritdoc cref="Load"/>
        public SaveLoadResult LoadWithReport()
        {
            DeleteStaleTempFile();

            ReadResult primary = TryRead(_savePath);
            if (primary.Status == ReadStatus.Ok)
            {
                if (primary.Migrated)
                {
                    SaveGameData migrated = Save(primary.Data);
                    _logger.Log(LogSeverity.Warning, Category, "Save migrated", $"from=v{primary.Version} to=v{SaveGameData.CurrentSchemaVersion}");
                    Telemetry.Record("save.migrated", ("from", primary.Version), ("to", SaveGameData.CurrentSchemaVersion));
                    return new SaveLoadResult(migrated, SaveLoadOutcome.Migrated);
                }

                return new SaveLoadResult(primary.Data, SaveLoadOutcome.Loaded);
            }

            ReadResult backup = TryRead(_backupPath);
            if (backup.Status == ReadStatus.Ok)
            {
                var quarantined = new List<string>();
                if (primary.Status != ReadStatus.Missing)
                {
                    quarantined.Add(Quarantine(_savePath));
                }

                // Copy rather than move so the backup stays in place until the next successful save rotates it.
                WriteDurably(_tempPath, File.ReadAllText(_backupPath));
                File.Move(_tempPath, _savePath);
                SaveGameData restored = backup.Migrated ? Save(backup.Data) : backup.Data;

                _logger.Log(LogSeverity.Warning, Category, "Primary save unusable; restored from backup", $"primary={primary.Status} detail={primary.Detail}");
                Telemetry.Record("save.restored_from_backup", ("primary", primary.Status.ToString()));
                return new SaveLoadResult(restored, SaveLoadOutcome.RestoredFromBackup, quarantined);
            }

            if (primary.Status == ReadStatus.Missing && backup.Status == ReadStatus.Missing)
            {
                throw new SaveNotFoundException(_savePath);
            }

            ReadResult blocking = primary.Status != ReadStatus.Missing ? primary : backup;
            if (blocking.Status == ReadStatus.UnsupportedVersion)
            {
                throw new SaveVersionException(blocking.Path, blocking.Version);
            }

            throw new SaveCorruptException(blocking.Path, blocking.Detail);
        }

        /// <summary>
        /// Loads the save or, when none is usable, creates one with <paramref name="createDefault"/>.
        /// Unreadable files are quarantined first, so this never destroys data.
        /// </summary>
        public SaveLoadResult LoadOrCreate(Func<SaveGameData> createDefault)
        {
            if (createDefault == null)
            {
                throw new ArgumentNullException(nameof(createDefault));
            }

            try
            {
                return LoadWithReport();
            }
            catch (SaveNotFoundException)
            {
                SaveGameData created = Save(createDefault());
                Telemetry.Record("save.created");
                return new SaveLoadResult(created, SaveLoadOutcome.CreatedNew);
            }
            catch (Exception ex) when (ex is SaveCorruptException || ex is SaveVersionException)
            {
                var quarantined = new List<string>();
                if (File.Exists(_savePath))
                {
                    quarantined.Add(Quarantine(_savePath));
                }

                if (File.Exists(_backupPath))
                {
                    quarantined.Add(Quarantine(_backupPath));
                }

                SaveGameData created = Save(createDefault());
                _logger.Log(LogSeverity.Warning, Category, "No readable save; started a new game", $"reason={ex.Message} quarantined={string.Join(";", quarantined)}");
                Telemetry.Record("save.replaced_unreadable", ("reason", ex.GetType().Name), ("quarantined", quarantined.Count));
                return new SaveLoadResult(created, SaveLoadOutcome.ReplacedUnreadable, quarantined);
            }
        }

        private enum ReadStatus
        {
            Missing,
            Ok,
            Corrupt,
            UnsupportedVersion
        }

        private readonly struct ReadResult
        {
            public readonly string Path;
            public readonly ReadStatus Status;
            public readonly SaveGameData Data;
            public readonly bool Migrated;
            public readonly int Version;
            public readonly string Detail;

            public ReadResult(string path, ReadStatus status, SaveGameData data = null, bool migrated = false, int version = 0, string detail = null)
            {
                Path = path;
                Status = status;
                Data = data;
                Migrated = migrated;
                Version = version;
                Detail = detail ?? string.Empty;
            }
        }

        private static ReadResult TryRead(string path)
        {
            if (!File.Exists(path))
            {
                return new ReadResult(path, ReadStatus.Missing);
            }

            string raw = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new ReadResult(path, ReadStatus.Corrupt, detail: "file is empty");
            }

            int version;
            try
            {
                version = SaveMigrations.ReadSchemaVersion(raw);
            }
            catch (ArgumentException ex)
            {
                return new ReadResult(path, ReadStatus.Corrupt, detail: ex.Message);
            }

            if (version <= 0)
            {
                return new ReadResult(path, ReadStatus.Corrupt, version: version, detail: "missing schema version");
            }

            if (version > SaveGameData.CurrentSchemaVersion ||
                (version < SaveGameData.CurrentSchemaVersion && !SaveMigrations.CanMigrate(version)))
            {
                return new ReadResult(path, ReadStatus.UnsupportedVersion, version: version, detail: $"schema v{version}");
            }

            try
            {
                if (version < SaveGameData.CurrentSchemaVersion)
                {
                    SaveGameData migrated = SaveMigrations.MigrateToCurrent(raw, version);
                    return new ReadResult(path, ReadStatus.Ok, migrated, migrated: true, version: version);
                }

                SaveGameData data = JsonUtility.FromJson<SaveGameData>(raw);
                if (data == null)
                {
                    return new ReadResult(path, ReadStatus.Corrupt, version: version, detail: "payload was null");
                }

                data.EnsureSections();
                return new ReadResult(path, ReadStatus.Ok, data, version: version);
            }
            catch (ArgumentException ex)
            {
                return new ReadResult(path, ReadStatus.Corrupt, version: version, detail: ex.Message);
            }
        }

        private static void WriteDurably(string path, string contents)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private string Quarantine(string path)
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture);
            string target = $"{path}.quarantine-{stamp}";
            for (int suffix = 1; File.Exists(target); suffix++)
            {
                target = $"{path}.quarantine-{stamp}-{suffix}";
            }

            File.Move(path, target);
            _logger.Log(LogSeverity.Warning, Category, "Unreadable save quarantined", $"from={Path.GetFileName(path)} to={Path.GetFileName(target)}");
            return target;
        }

        private void DeleteStaleTempFile()
        {
            if (!File.Exists(_tempPath))
            {
                return;
            }

            // A leftover .tmp means a write was interrupted before the atomic replace; the primary and backup are authoritative.
            File.Delete(_tempPath);
            _logger.Log(LogSeverity.Warning, Category, "Removed stale temporary save", $"file={Path.GetFileName(_tempPath)}");
        }
    }
}
