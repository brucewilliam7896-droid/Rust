using System;
using System.IO;

namespace RustPlus.Core.Save
{
    /// <summary>Neither the primary nor the backup save file exists.</summary>
    public sealed class SaveNotFoundException : FileNotFoundException
    {
        public SaveNotFoundException(string path)
            : base("No save data exists at this path.", path)
        {
        }
    }

    /// <summary>Save files exist but none of them can be parsed or validated.</summary>
    public sealed class SaveCorruptException : InvalidOperationException
    {
        public string SavePath { get; }

        public SaveCorruptException(string path, string detail)
            : base($"Save data at '{path}' is corrupt: {detail}")
        {
            SavePath = path;
        }
    }

    /// <summary>The save was written by a schema this build cannot read (usually a newer build).</summary>
    public sealed class SaveVersionException : InvalidOperationException
    {
        public string SavePath { get; }
        public int FoundVersion { get; }

        public SaveVersionException(string path, int foundVersion)
            : base($"Save schema version mismatch at '{path}'. Expected {SaveGameData.CurrentSchemaVersion} but found {foundVersion}.")
        {
            SavePath = path;
            FoundVersion = foundVersion;
        }
    }
}
