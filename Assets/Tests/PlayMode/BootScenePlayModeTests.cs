using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RustPlus.Core.Bootstrap;
using RustPlus.Core.Diagnostics;
using RustPlus.Core.Save;
using RustPlus.Core.Simulation;
using RustPlus.UI.Debugging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RustPlus.Tests.PlayMode
{
    public sealed class BootScenePlayModeTests
    {
        private const string BootSceneName = "Boot";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"rustplus-boot-{Guid.NewGuid():N}");
            SavePaths.RootOverride = _root;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameBootstrapper.Instance != null)
            {
                UnityEngine.Object.Destroy(GameBootstrapper.Instance.gameObject);
                yield return null;
            }

            SavePaths.RootOverride = null;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [UnityTest]
        public IEnumerator BootSceneStartsSessionCreatesSaveAndRunsTheClock()
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(BootSceneName), Is.True, "Boot scene must be in Build Settings");

            yield return SceneManager.LoadSceneAsync(BootSceneName, LoadSceneMode.Single);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            GameBootstrapper boot = GameBootstrapper.Instance;
            Assert.That(boot, Is.Not.Null);
            Assert.That(boot.BootSave.Outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(File.Exists(SavePaths.DefaultSavePath), Is.True);
            Assert.That(SimulationClock.CurrentTick, Is.GreaterThan(0));
            Assert.That(Telemetry.IsEnabled, Is.True);

            RuntimePlayerState player = UnityEngine.Object.FindAnyObjectByType<RuntimePlayerState>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.SavePath, Is.EqualTo(SavePaths.DefaultSavePath), "player and bootstrapper share one save");
            Assert.That(UnityEngine.Object.FindAnyObjectByType<DebugOverlay>(), Is.Not.Null);

            Telemetry.Flush();
            string[] telemetryFiles = Directory.GetFiles(SavePaths.TelemetryDirectory, "*.jsonl");
            Assert.That(telemetryFiles.Length, Is.EqualTo(1));
        }
    }
}
