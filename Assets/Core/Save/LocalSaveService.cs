using System;
using System.IO;
using UnityEngine;
using RustPlus.Core.Diagnostics;

namespace RustPlus.Core.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion;
        public string PlayerName;
        public int Health;
        public int Hunger;
        public int Thirst;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string[] Inventory;

        /// <summary>
        /// World chunk coordinates this player was last in.
        /// Used for world persistence and chunk reloading.
        /// </summary>
        public int ChunkX;
        public int ChunkZ;

        /// <summary>
        /// World generation seed for this save.
        /// Required for world regeneration during wipe/season cycles.
        /// </summary>
        public int WorldSeed;

        /// <summary>
        /// Current game tick when this save was taken.
        /// Used for deterministic replay and validation.
        /// </summary>
        public int SaveTick;

        public SaveGameData()
        {
            SchemaVersion = CurrentSchemaVersion;
            PlayerName = string.Empty;
            Inventory = Array.Empty<string>();
            ChunkX = 0;
            ChunkZ = 0;
            WorldSeed = 0;
            SaveTick = 0;
        }
    }

    public sealed class LocalSaveService
    {
        private readonly string _savePath;
        private readonly string _backupPath;

        private static readonly StructuredLogger _logger = new StructuredLogger();

        public LocalSaveService(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("A save path is required.", nameof(savePath));
            }

            _savePath = savePath;
            _backupPath = savePath + ".bak";
        }

        public void Save(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.SchemaVersion = SaveGameData.CurrentSchemaVersion;

            // Log save operation
            _logger.Log(LogSeverity.Info, "Save", "Saving player state", "slot=autosave");

            string directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = _savePath + ".tmp";
            string payload = JsonUtility.ToJson(data);
            File.WriteAllText(tempPath, payload);

            if (File.Exists(_savePath))
            {
                File.Replace(tempPath, _savePath, _backupPath, true);
            }
            else
            {
                File.Move(tempPath, _savePath);
            }
        }

        public SaveGameData Load()
        {
            string candidatePath = _savePath;
            if (File.Exists(candidatePath))
            {
                SaveGameData loaded = TryLoadFrom(candidatePath);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            if (File.Exists(_backupPath))
            {
                SaveGameData backup = TryLoadFrom(_backupPath);
                if (backup != null)
                {
                    File.Copy(_backupPath, _savePath, true);
                    return backup;
                }
            }

            throw new FileNotFoundException("No valid save data was found for the current game state.", _savePath);
        }

        private SaveGameData TryLoadFrom(string path)
        {
            try
            {
                string raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                SaveGameData data = JsonUtility.FromJson<SaveGameData>(raw);
                if (data == null)
                {
                    return null;
                }

                if (data.SchemaVersion != SaveGameData.CurrentSchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Save schema version mismatch. Expected {SaveGameData.CurrentSchemaVersion} but found {data.SchemaVersion}.");
                }

                return data;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                if (string.Equals(path, _savePath, StringComparison.OrdinalIgnoreCase) && File.Exists(_backupPath))
                {
                    return TryLoadFrom(_backupPath);
                }

                return null;
            }
        }
    }
}
