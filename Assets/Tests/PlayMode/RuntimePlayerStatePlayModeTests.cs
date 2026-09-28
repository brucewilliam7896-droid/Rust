using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RustPlus.Core.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace RustPlus.Tests.PlayMode
{
    public sealed class RuntimePlayerStatePlayModeTests
    {
        [UnityTest]
        public IEnumerator SaveAndReloadRestoresTransformAcrossRepeatedCycles()
        {
            string saveFileName = $"runtime-player-state-{Guid.NewGuid():N}.json";
            string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
            var trackedObject = new GameObject("RuntimePlayerStateTest");
            trackedObject.SetActive(false);

            try
            {
                RuntimePlayerState state = trackedObject.AddComponent<RuntimePlayerState>();
                FieldInfo saveFileNameField = typeof(RuntimePlayerState).GetField(
                    "saveFileName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(saveFileNameField, Is.Not.Null);
                saveFileNameField.SetValue(state, saveFileName);

                trackedObject.SetActive(true);
                yield return null;

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
            }
            finally
            {
                if (trackedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(trackedObject);
                }

                DeleteIfExists(savePath);
                DeleteIfExists(savePath + ".bak");
                DeleteIfExists(savePath + ".tmp");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}