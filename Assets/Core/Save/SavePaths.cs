using System.IO;
using UnityEngine;

namespace RustPlus.Core.Save
{
    /// <summary>
    /// The single source of truth for where local data lives. Every runtime component resolves
    /// its paths here so the game never ends up with two diverging saves.
    /// </summary>
    public static class SavePaths
    {
        public const string DefaultSaveFileName = "player-save.json";

        /// <summary>
        /// Replaces <c>Application.persistentDataPath</c> as the root. Tests set this to a temporary
        /// directory and reset it to <c>null</c> afterwards.
        /// </summary>
        public static string RootOverride { get; set; }

        public static string Root => string.IsNullOrEmpty(RootOverride) ? Application.persistentDataPath : RootOverride;

        public static string SaveDirectory => Path.Combine(Root, "saves");

        public static string DefaultSavePath => Path.Combine(SaveDirectory, DefaultSaveFileName);

        public static string TelemetryDirectory => Path.Combine(Root, "telemetry");
    }
}
