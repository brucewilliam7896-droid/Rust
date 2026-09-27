using System.Collections.Generic;
using System.Linq;
using RustPlus.Core.Bootstrap;
using RustPlus.Core.Save;
using RustPlus.UI.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RustPlus.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Boot.unity</c> from code so the boot scene is reproducible and reviewable,
    /// and makes it the first scene in Build Settings.
    /// Batch mode: Unity -batchmode -projectPath &lt;path&gt; -executeMethod RustPlus.EditorTools.BootSceneBuilder.CreateBootSceneBatch -quit
    /// </summary>
    public static class BootSceneBuilder
    {
        public const string BootScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("RustPlus/Create Boot Scene")]
        public static void CreateBootScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrap = new GameObject("GameBootstrapper");
            bootstrap.AddComponent<GameBootstrapper>();
            bootstrap.AddComponent<DebugOverlay>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.AddComponent<RuntimePlayerState>();

            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 6f, -10f), Quaternion.Euler(25f, 0f, 0f));
            }

            EditorSceneManager.SaveScene(scene, BootScenePath);
            SetBootSceneFirst();
            Debug.Log($"Created {BootScenePath} and set it as the first build scene.");
        }

        public static void CreateBootSceneBatch()
        {
            CreateBootScene();
        }

        private static void SetBootSceneFirst()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(existing => existing.path != BootScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
