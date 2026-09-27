using System;
using System.IO;
using RustPlus.Core.Save;

namespace RustPlus.Core.Bootstrap
{
    /// <summary>
    /// Plain C# boot step: makes sure a usable save exists before any gameplay object loads it.
    /// Unreadable saves are quarantined, never overwritten (see <see cref="LocalSaveService.LoadOrCreate"/>).
    /// </summary>
    public sealed class StartupBootstrap
    {
        private readonly LocalSaveService _saveService;

        public StartupBootstrap(string saveDirectory, string saveFileName = SavePaths.DefaultSaveFileName)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                throw new ArgumentException("A valid save directory is required for startup bootstrap.", nameof(saveDirectory));
            }

            if (string.IsNullOrWhiteSpace(saveFileName))
            {
                throw new ArgumentException("A save file name is required for startup bootstrap.", nameof(saveFileName));
            }

            Directory.CreateDirectory(saveDirectory);
            SavePath = Path.Combine(saveDirectory, saveFileName);
            _saveService = new LocalSaveService(SavePath);
        }

        public string SavePath { get; }

        /// <summary>How the most recent <see cref="EnsureSaveState"/> call obtained its data.</summary>
        public SaveLoadResult LastResult { get; private set; }

        public SaveGameData EnsureSaveState()
        {
            LastResult = _saveService.LoadOrCreate(CreateDefaultState);
            return LastResult.Data;
        }

        public SaveGameData CreateDefaultState()
        {
            return SaveDefaults.CreateNewGame();
        }
    }
}
