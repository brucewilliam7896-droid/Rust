using System;
using RustPlus.Core.Simulation;
using UnityEngine;

namespace RustPlus.Core.Save
{
    /// <summary>
    /// Binds the save file to the player object: restores the tracked Transform on load and
    /// captures it on save. Saves on application pause and quit.
    /// Uses <see cref="SavePaths.DefaultSavePath"/> unless <see cref="Configure"/> is called before it wakes.
    /// </summary>
    public sealed class RuntimePlayerState : MonoBehaviour
    {
        [SerializeField] private Transform trackedTransform;

        private LocalSaveService _saveService;
        private string _savePathOverride;

        public SaveGameData Current { get; private set; } = SaveDefaults.CreateNewGame();

        public SaveLoadOutcome LastLoadOutcome { get; private set; }

        public string SavePath => _saveService?.SavePath ?? _savePathOverride ?? SavePaths.DefaultSavePath;

        /// <summary>
        /// Points this component at a specific save file. Call before the component wakes
        /// (for example on an inactive GameObject), such as in tests.
        /// </summary>
        public void Configure(string savePath)
        {
            if (_saveService != null)
            {
                throw new InvalidOperationException("RuntimePlayerState is already initialized; call Configure before Awake.");
            }

            _savePathOverride = savePath;
        }

        private void Awake()
        {
            if (trackedTransform == null)
            {
                trackedTransform = transform;
            }

            _saveService = new LocalSaveService(_savePathOverride ?? SavePaths.DefaultSavePath);

            SaveLoadResult result = _saveService.LoadOrCreate(() => SaveDefaults.CreateNewGame());
            LastLoadOutcome = result.Outcome;
            ApplyState(result.Data);
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
            if (_saveService == null)
            {
                return;
            }

            Current = _saveService.Save(CaptureState());
        }

        public SaveGameData Load()
        {
            SaveGameData loaded = _saveService.Load();
            ApplyState(loaded);
            return Current;
        }

        public SaveGameData CaptureState()
        {
            SaveGameData snapshot = Current.Clone();
            Transform target = trackedTransform != null ? trackedTransform : transform;
            Vector3 position = target.position;

            snapshot.Player.PositionX = position.x;
            snapshot.Player.PositionY = position.y;
            snapshot.Player.PositionZ = position.z;
            if (string.IsNullOrWhiteSpace(snapshot.Player.Name))
            {
                snapshot.Player.Name = SaveDefaults.DefaultPlayerName;
            }

            if (SimulationClock.CurrentTick >= 0)
            {
                snapshot.Meta.SaveTick = SimulationClock.CurrentTick;
            }

            return snapshot;
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
    }
}
