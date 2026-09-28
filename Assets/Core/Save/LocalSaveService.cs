using System;
using System.IO;
using UnityEngine;
using RustPlus.Core.Diagnostics;

namespace RustPlus.Core.Save
{
    /// <summary>
    /// Per-player vitals, position, and inventory. Owned by the player domain;
    /// world/chunk concerns must not be added here.
    /// </summary>
    [Serializable]
    public sealed class PlayerData
    {
        public string PlayerName;
        public int Health;
        public int Hunger;
        public int Thirst;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string[] Inventory;

        public PlayerData()
        {
            PlayerName = string.Empty;
            Inventory = Array.Empty<string>();
        }
    }

    /// <summary>
    /// World-scoped save metadata: the chunk the player was last in, the world
    /// generation seed, and the tick the save was taken at. Owned by the world
    /// domain; player vitals must not be added here.
    /// </summary>
    [Serializable]
    public sealed class WorldMetadata
    {
        public int ChunkX;
        public int ChunkZ;
        public int WorldSeed;
        public int SaveTick;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion;
        public PlayerData Player;
        public WorldMetadata World;

        public SaveGameData()
        {
            SchemaVersion = CurrentSchemaVersion;
            Player = new PlayerData();
            World = new WorldMetadata();
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
