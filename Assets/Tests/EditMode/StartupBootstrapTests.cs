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
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"rustplus-bootstrap-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void BootstrapCreatesDefaultSaveWhenMissing()
        {
            var bootstrap = new StartupBootstrap(_root, "bootstrap-save.json");
            SaveGameData state = bootstrap.EnsureSaveState();

            Assert.That(state.Player.Name, Is.EqualTo(SaveDefaults.DefaultPlayerName));
            Assert.That(state.Player.Health, Is.EqualTo(SaveDefaults.MaxVital));
            Assert.That(bootstrap.LastResult.Outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(File.Exists(Path.Combine(_root, "bootstrap-save.json")), Is.True);
        }

        [Test]
        public void BootstrapKeepsExistingValidSave()
        {
            Directory.CreateDirectory(_root);
            SaveGameData expected = SaveDefaults.CreateNewGame();
            expected.Player.Name = "Alice";
            expected.Player.Health = 75;
            File.WriteAllText(Path.Combine(_root, "bootstrap-save.json"), JsonUtility.ToJson(expected));

            var bootstrap = new StartupBootstrap(_root, "bootstrap-save.json");
            SaveGameData loaded = bootstrap.EnsureSaveState();

            Assert.That(loaded.Player.Name, Is.EqualTo("Alice"));
            Assert.That(loaded.Player.Health, Is.EqualTo(75));
            Assert.That(bootstrap.LastResult.Outcome, Is.EqualTo(SaveLoadOutcome.Loaded));
        }

        [Test]
        public void BootstrapNeverOverwritesAnUnreadableSave()
        {
            Directory.CreateDirectory(_root);
            string savePath = Path.Combine(_root, "bootstrap-save.json");
            const string futureSave = "{\"SchemaVersion\":999,\"PlayerName\":\"FromTheFuture\"}";
            File.WriteAllText(savePath, futureSave);

            var bootstrap = new StartupBootstrap(_root, "bootstrap-save.json");
            SaveGameData state = bootstrap.EnsureSaveState();

            Assert.That(state.Player.Name, Is.EqualTo(SaveDefaults.DefaultPlayerName));
            Assert.That(bootstrap.LastResult.Outcome, Is.EqualTo(SaveLoadOutcome.ReplacedUnreadable));
            Assert.That(File.ReadAllText(bootstrap.LastResult.QuarantinedFiles[0]), Is.EqualTo(futureSave));
        }

        [Test]
        public void DefaultStateMatchesTheSharedDefinition()
        {
            var bootstrap = new StartupBootstrap(_root);

            Assert.That(JsonUtility.ToJson(bootstrap.CreateDefaultState()), Is.EqualTo(JsonUtility.ToJson(SaveDefaults.CreateNewGame())));
            Assert.That(bootstrap.SavePath, Is.EqualTo(Path.Combine(_root, SavePaths.DefaultSaveFileName)));
        }
    }
}
