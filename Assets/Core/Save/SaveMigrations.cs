using System;
using UnityEngine;

namespace RustPlus.Core.Save
{
    /// <summary>
    /// Upgrades older save payloads to <see cref="SaveGameData.CurrentSchemaVersion"/>.
    /// Each historical schema keeps a frozen DTO here so old files stay readable forever.
    /// </summary>
    public static class SaveMigrations
    {
        [Serializable]
        private sealed class SchemaProbe
        {
            public int SchemaVersion;
        }

        /// <summary>Schema version 1: one flat player payload (2026-09 Phase 0 prototype).</summary>
        [Serializable]
        internal sealed class SaveGameDataV1
        {
            public int SchemaVersion;
            public string PlayerName;
            public int Health;
            public int Hunger;
            public int Thirst;
            public float PositionX;
            public float PositionY;
            public float PositionZ;
            public string[] Inventory;
            public int ChunkX;
            public int ChunkZ;
            public int WorldSeed;
            public int SaveTick;
        }

        /// <summary>Reads only the schema version. Throws <see cref="ArgumentException"/> on malformed JSON.</summary>
        public static int ReadSchemaVersion(string json)
        {
            SchemaProbe probe = JsonUtility.FromJson<SchemaProbe>(json);
            return probe?.SchemaVersion ?? 0;
        }

        public static bool CanMigrate(int fromVersion)
        {
            return fromVersion == 1;
        }

        /// <summary>Parses a payload of an older supported version and returns it as the current schema.</summary>
        public static SaveGameData MigrateToCurrent(string json, int fromVersion)
        {
            switch (fromVersion)
            {
                case 1:
                    return FromV1(JsonUtility.FromJson<SaveGameDataV1>(json));
                default:
                    throw new ArgumentOutOfRangeException(nameof(fromVersion), fromVersion, "No migration exists for this schema version.");
            }
        }

        private static SaveGameData FromV1(SaveGameDataV1 v1)
        {
            if (v1 == null)
            {
                throw new ArgumentException("Version 1 payload was empty.");
            }

            var data = new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                Meta = new SaveMetadata
                {
                    SaveTick = v1.SaveTick
                },
                Player = new PlayerSaveData
                {
                    Name = v1.PlayerName ?? string.Empty,
                    Health = v1.Health,
                    Hunger = v1.Hunger,
                    Thirst = v1.Thirst,
                    PositionX = v1.PositionX,
                    PositionY = v1.PositionY,
                    PositionZ = v1.PositionZ,
                    Inventory = v1.Inventory ?? Array.Empty<string>()
                },
                World = new WorldSaveData
                {
                    // v1 stored the seed as int; keep the same bit pattern so regenerated worlds match.
                    Seed = unchecked((ulong)(uint)v1.WorldSeed),
                    LastChunkX = v1.ChunkX,
                    LastChunkZ = v1.ChunkZ
                }
            };

            data.EnsureSections();
            return data;
        }
    }
}
