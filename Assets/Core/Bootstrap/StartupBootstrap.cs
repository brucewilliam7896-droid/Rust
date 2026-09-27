using System;
using System.IO;
using RustPlus.Core.Save;
using UnityEngine;

namespace RustPlus.Core.Bootstrap
{
    public sealed class StartupBootstrap
    {
        private readonly string _rootPath;
        private readonly string _saveFileName;
        private readonly LocalSaveService _saveService;

        public StartupBootstrap(string rootPath, string saveFileName = "player-save.json")
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("A valid root path is required for startup bootstrap.", nameof(rootPath));
            }

            if (string.IsNullOrWhiteSpace(saveFileName))
            {
                throw new ArgumentException("A save file name is required for startup bootstrap.", nameof(saveFileName));
            }

            _rootPath = rootPath;
            _saveFileName = saveFileName;
            Directory.CreateDirectory(_rootPath);

            string savePath = Path.Combine(_rootPath, _saveFileName);
            _saveService = new LocalSaveService(savePath);
        }

        public SaveGameData EnsureSaveState()
        {
            try
            {
                return _saveService.Load();
            }
            catch (FileNotFoundException)
            {
                SaveGameData defaultState = CreateDefaultState();
                _saveService.Save(defaultState);
                return defaultState;
            }
            catch (InvalidOperationException)
            {
                SaveGameData defaultState = CreateDefaultState();
                _saveService.Save(defaultState);
                return defaultState;
            }
        }

        public SaveGameData CreateDefaultState()
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
                ChunkX = 0,
                ChunkZ = 0,
                WorldSeed = 0,
                SaveTick = 0,
                Inventory = new[] { "Stone", "Wood" }
            };
        }
    }

    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private string saveFileName = "player-save.json";

        private StartupBootstrap _startupBootstrap;

        private void Awake()
        {
            string rootPath = Path.Combine(Application.persistentDataPath, "bootstrap");
            _startupBootstrap = new StartupBootstrap(rootPath, saveFileName);
            _startupBootstrap.EnsureSaveState();
        }
    }
}
