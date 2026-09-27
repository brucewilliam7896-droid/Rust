using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace RustPlus.EditorTools
{
    public static class UnityBootstrapTestRunner
    {
        [MenuItem("RustPlus/Run Bootstrap Tests")]
        public static void RunBootstrapTests()
        {
            CITestRunner.Run(
                TestMode.EditMode,
                CITestRunner.EditModeAssembly,
                new[] { "RustPlus.Tests.EditMode.StartupBootstrapTests" });
        }
    }
}
