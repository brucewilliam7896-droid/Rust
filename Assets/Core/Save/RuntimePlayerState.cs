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
                PlayerName = string.IsNullOrWhiteSpace(Current.PlayerName) ? "Survivor" : Current.PlayerName,
                Health = Current.Health,
                Hunger = Current.Hunger,
                Thirst = Current.Thirst,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                ChunkX = Current.ChunkX,
                ChunkZ = Current.ChunkZ,
                WorldSeed = Current.WorldSeed,
                SaveTick = Current.SaveTick,
                Inventory = Current.Inventory ?? Array.Empty<string>()
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
                trackedTransform.position = new Vector3(state.PositionX, state.PositionY, state.PositionZ);
            }
        }

        private static SaveGameData CreateDefaultState()
        {
            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                PlayerName = "Survivor",
                Health = 100,
                Hunger = 100,
                Thirst = 100,
                PositionX = 0f,
                PositionY = 0.5f,
                PositionZ = 0f,
                Inventory = new[] { "Stone", "Wood" }
            };
        }
    }
}
