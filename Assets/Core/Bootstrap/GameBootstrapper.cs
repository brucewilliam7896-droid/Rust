using System;
using System.Globalization;
using System.IO;
using RustPlus.Core.Diagnostics;
using RustPlus.Core.Save;
using RustPlus.Core.Simulation;
using UnityEngine;

namespace RustPlus.Core.Bootstrap
{
    /// <summary>
    /// First object to run in the Boot scene. Owns the session: simulation clock, telemetry sink and
    /// save validation. Survives scene loads and keeps exactly one instance alive.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private const string Category = "Boot";

        [SerializeField] private bool writeTelemetry = true;

        private readonly StructuredLogger _logger = new StructuredLogger();
        private JsonLinesTelemetrySink _telemetrySink;

        public static GameBootstrapper Instance { get; private set; }

        public SimulationClock Clock { get; private set; }

        public SaveLoadResult BootSave { get; private set; }

        public string SessionId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            SessionId = Guid.NewGuid().ToString("N");
            Clock = new SimulationClock();
            SimulationClock.Active = Clock;

            if (writeTelemetry)
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
                string path = Path.Combine(SavePaths.TelemetryDirectory, $"session-{stamp}-{SessionId.Substring(0, 8)}.jsonl");
                _telemetrySink = new JsonLinesTelemetrySink(path);
                Telemetry.Install(_telemetrySink, SessionId);
            }

            Telemetry.Record("session.started", ("version", Application.version), ("platform", Application.platform.ToString()));

            var bootstrap = new StartupBootstrap(SavePaths.SaveDirectory);
            bootstrap.EnsureSaveState();
            BootSave = bootstrap.LastResult;

            _logger.Log(LogSeverity.Info, Category, "Boot complete", $"session={SessionId} save={BootSave.Outcome}");
            Telemetry.Record("boot.completed", ("save_outcome", BootSave.Outcome.ToString()));
        }

        private void FixedUpdate()
        {
            Clock?.Advance();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Telemetry.Flush();
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Telemetry.Record("session.ended", ("ticks", Clock?.Tick ?? 0));
            ITelemetrySink sink = Telemetry.Uninstall();
            sink?.Flush();
            _telemetrySink?.Dispose();
            _telemetrySink = null;

            if (SimulationClock.Active == Clock)
            {
                SimulationClock.Active = null;
            }

            Instance = null;
        }
    }
}
