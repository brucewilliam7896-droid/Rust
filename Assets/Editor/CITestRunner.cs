using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RustPlus.EditorTools
{
    /// <summary>
    /// Runs the project's tests through the Unity Test Runner API from menu items or the command line.
    /// Batch mode: Unity -batchmode -projectPath &lt;path&gt; -executeMethod RustPlus.EditorTools.CITestRunner.RunEditModeTestsBatch
    /// The process exits with code 0 when all tests pass and 1 otherwise. For PlayMode in CI, prefer
    /// Unity -batchmode -projectPath &lt;path&gt; -runTests -testPlatform PlayMode -testResults results.xml
    /// </summary>
    public static class CITestRunner
    {
        public const string EditModeAssembly = "RustPlus.Tests.EditMode";
        public const string PlayModeAssembly = "RustPlus.Tests.PlayMode";

        [MenuItem("Tools/CI/Run EditMode Tests")]
        public static void RunEditModeTests()
        {
            Run(TestMode.EditMode, EditModeAssembly, null);
        }

        [MenuItem("Tools/CI/Run PlayMode Tests")]
        public static void RunPlayModeTests()
        {
            Run(TestMode.PlayMode, PlayModeAssembly, null);
        }

        // Entry point for -executeMethod; the run is asynchronous and exits the editor from RunFinished.
        public static void RunEditModeTestsBatch()
        {
            RunEditModeTests();
        }

        /// <summary>
        /// Executes tests for one assembly, optionally narrowed to full test or fixture names.
        /// </summary>
        public static void Run(TestMode mode, string assemblyName, string[] testNames)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName },
                testNames = testNames
            };

            // PlayMode triggers a domain reload that discards these callbacks; the Test Runner window shows those results.
            if (mode == TestMode.EditMode)
            {
                api.RegisterCallbacks(new ResultLogger(api));
            }

            api.Execute(new ExecutionSettings(filter));
            Debug.Log($"Requested {mode} tests for {assemblyName}.");
        }

        private sealed class ResultLogger : ICallbacks
        {
            private readonly TestRunnerApi _api;

            public ResultLogger(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"FAILED {result.FullName}: {result.Message}");
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _api.UnregisterCallbacks(this);

                bool success = result.FailCount == 0 && result.InconclusiveCount == 0;
                string summary = $"Test run finished: {result.PassCount} passed, {result.FailCount} failed, " +
                                 $"{result.SkipCount} skipped, {result.InconclusiveCount} inconclusive.";
                if (success)
                {
                    Debug.Log(summary);
                }
                else
                {
                    Debug.LogError(summary);
                }

                UnityEngine.Object.DestroyImmediate(_api);

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(success ? 0 : 1);
                }
            }
        }
    }
}
