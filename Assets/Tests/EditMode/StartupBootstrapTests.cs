using System;
using System.IO;
using NUnit.Framework;
using RustPlus.Core.Bootstrap;
using RustPlus.Core.Save;
using UnityEngine;

namespace RustPlus.Tests.EditMode
{
    public sealed class StartupBootstrapTests
    {
        [Test]
        public void BootstrapCreatesDefaultSaveWhenMissing()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"rustplus-bootstrap-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                var bootstrap = new StartupBootstrap(tempRoot, "bootstrap-save.json");
                SaveGameData state = bootstrap.EnsureSaveState();

                Assert.That(state.Player.PlayerName, Is.EqualTo("Survivor"));
                Assert.That(state.Player.Health, Is.EqualTo(100));
                Assert.That(File.Exists(Path.Combine(tempRoot, "bootstrap-save.json")), Is.True);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void BootstrapKeepsExistingValidSave()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"rustplus-bootstrap-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                string savePath = Path.Combine(tempRoot, "bootstrap-save.json");
                SaveGameData expected = new SaveGameData
                {
                    SchemaVersion = SaveGameData.CurrentSchemaVersion,
                    Player = new PlayerData
                    {
                        PlayerName = "Alice",
                        Health = 75,
                        Hunger = 60,
                        Thirst = 82,
                        PositionX = 12.5f,
                        PositionY = 6f,
                        PositionZ = -3.25f,
                        Inventory = new[] { "Stone", "Wood" }
                    },
                    World = new WorldMetadata()
                };

                File.WriteAllText(savePath, JsonUtility.ToJson(expected));

                var bootstrap = new StartupBootstrap(tempRoot, "bootstrap-save.json");
                SaveGameData loaded = bootstrap.EnsureSaveState();

                Assert.That(loaded.Player.PlayerName, Is.EqualTo("Alice"));
                Assert.That(loaded.Player.Health, Is.EqualTo(75));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
