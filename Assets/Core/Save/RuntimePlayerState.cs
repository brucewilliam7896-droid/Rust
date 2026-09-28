using System;
using System.IO;
using UnityEngine;

namespace RustPlus.Core.Save
{
    public sealed class RuntimePlayerState : MonoBehaviour
    {
        [SerializeField] private string saveFileName = "player-save.json";
        [SerializeField] private Transform trackedTransform;

        private LocalSaveService _saveService;

        public SaveGameData Current { get; private set; } = new SaveGameData();

        private void Awake()
        {
            if (trackedTransform == null)
            {
                trackedTransform = transform;
            }

            string path = Path.Combine(Application.persistentDataPath, saveFileName);
            _saveService = new LocalSaveService(path);

            try
            {
                Current = _saveService.Load();
                ApplyState(Current);
            }
            catch (FileNotFoundException)
            {
                Current = CreateDefaultState();
                Save();
            }
            catch (InvalidOperationException)
            {
                Current = CreateDefaultState();
                Save();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Save()
        {
            Current = CaptureState();
            _saveService.Save(Current);
        }

        public SaveGameData Load()
        {
            Current = _saveService.Load();
            ApplyState(Current);
            return Current;
        }

        public SaveGameData CaptureState()
        {
            Transform target = trackedTransform != null ? trackedTransform : transform;
            Vector3 position = target.position;

            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                Player = new PlayerData
                {
                    PlayerName = string.IsNullOrWhiteSpace(Current.Player.PlayerName) ? "Survivor" : Current.Player.PlayerName,
                    Health = Current.Player.Health,
                    Hunger = Current.Player.Hunger,
                    Thirst = Current.Player.Thirst,
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    Inventory = Current.Player.Inventory ?? Array.Empty<string>()
                },
                World = new WorldMetadata
                {
                    ChunkX = Current.World.ChunkX,
                    ChunkZ = Current.World.ChunkZ,
                    WorldSeed = Current.World.WorldSeed,
                    SaveTick = Current.World.SaveTick
                }
            };
        }

        public void ApplyState(SaveGameData state)
        {
            if (state == null)
            {
                return;
            }

            Current = state;

            if (trackedTransform != null)
            {
                trackedTransform.position = new Vector3(state.Player.PositionX, state.Player.PositionY, state.Player.PositionZ);
            }
        }

        private static SaveGameData CreateDefaultState()
        {
            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                Player = new PlayerData
                {
                    PlayerName = "Survivor",
                    Health = 100,
                    Hunger = 100,
                    Thirst = 100,
                    PositionX = 0f,
                    PositionY = 0.5f,
                    PositionZ = 0f,
                    Inventory = new[] { "Stone", "Wood" }
                },
                World = new WorldMetadata()
            };
        }
    }
}
