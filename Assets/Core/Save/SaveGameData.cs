using System;
using UnityEngine;

namespace RustPlus.Core.Save
{
    /// <summary>
    /// Root of the persistent save payload. Schema version 2 splits the payload into three
    /// boundaries: metadata about the save itself, player-owned state, and world-owned state.
    /// Any change to these types requires a schema version bump and a migration in <see cref="SaveMigrations"/>.
    /// </summary>
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public SaveMetadata Meta = new SaveMetadata();
        public PlayerSaveData Player = new PlayerSaveData();
        public WorldSaveData World = new WorldSaveData();

        /// <summary>
        /// Returns a deep copy so callers can hand data to the save service without it being mutated.
        /// </summary>
        public SaveGameData Clone()
        {
            SaveGameData copy = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(this));
            copy.EnsureSections();
            return copy;
        }

        internal void EnsureSections()
        {
            Meta ??= new SaveMetadata();
            Player ??= new PlayerSaveData();
            World ??= new WorldSaveData();
            Player.Inventory ??= Array.Empty<string>();
            Player.Name ??= string.Empty;
        }
    }

    /// <summary>Facts about the save file itself, not about the game state.</summary>
    [Serializable]
    public sealed class SaveMetadata
    {
        /// <summary>Simulation tick at the moment of saving (fixed 50 Hz steps since boot of the session that wrote it).</summary>
        public long SaveTick;

        /// <summary>UTC time of the write, ISO 8601 round-trip format. Set by <see cref="LocalSaveService"/>.</summary>
        public string SavedAtUtc = string.Empty;

        /// <summary><c>Application.version</c> of the build that wrote the file.</summary>
        public string GameVersion = string.Empty;
    }

    /// <summary>State owned by the local player.</summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        public string Name = string.Empty;
        public int Health;
        public int Hunger;
        public int Thirst;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public string[] Inventory = Array.Empty<string>();
    }

    /// <summary>State owned by the world rather than any player.</summary>
    [Serializable]
    public sealed class WorldSaveData
    {
        /// <summary>World generation seed; matches the <c>ulong</c> seed of <c>DeterministicRandom</c>.</summary>
        public ulong Seed;

        /// <summary>Chunk the local player was last in, used to decide what to stream in first on load.</summary>
        public int LastChunkX;
        public int LastChunkZ;
    }
}
