using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RustPlus.Core.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace RustPlus.Tests.PlayMode
{
    public sealed class RuntimePlayerStatePlayModeTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"rustplus-playmode-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [UnityTest]
        public IEnumerator SaveAndReloadRestoresTransformAcrossRepeatedCycles()
        {
            string savePath = Path.Combine(_directory, "runtime-player-state.json");
            var trackedObject = new GameObject("RuntimePlayerStateTest");
            trackedObject.SetActive(false);

            try
            {
                RuntimePlayerState state = trackedObject.AddComponent<RuntimePlayerState>();
                state.Configure(savePath);

                trackedObject.SetActive(true);
                yield return null;

                Assert.That(state.LastLoadOutcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
                Assert.That(state.SavePath, Is.EqualTo(savePath));

                Vector3 firstPosition = new Vector3(4.25f, 1.5f, -8f);
                trackedObject.transform.position = firstPosition;
                state.Current.Player.Health = 73;
                state.Save();

                trackedObject.transform.position = Vector3.zero;
                SaveGameData firstLoad = state.Load();
                Assert.That(Vector3.Distance(trackedObject.transform.position, firstPosition), Is.LessThan(0.0001f));
                Assert.That(firstLoad.Player.Health, Is.EqualTo(73));

                Vector3 secondPosition = new Vector3(-12f, 3.75f, 21.5f);
                trackedObject.transform.position = secondPosition;
                state.Current.Player.Health = 58;
                state.Save();

                trackedObject.transform.position = Vector3.one;
                SaveGameData secondLoad = state.Load();
                Assert.That(Vector3.Distance(trackedObject.transform.position, secondPosition), Is.LessThan(0.0001f));
                Assert.That(secondLoad.Player.Health, Is.EqualTo(58));
                Assert.That(File.Exists(savePath + ".bak"), Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(trackedObject);
            }
        }
    }
}
