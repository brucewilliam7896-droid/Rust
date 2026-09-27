using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RustPlus.Core.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace RustPlus.Tests.EditMode
{
    public sealed class LocalSaveServiceTests
    {
        private const string V1Payload =
            "{\"SchemaVersion\":1,\"PlayerName\":\"Veteran\",\"Health\":81,\"Hunger\":40,\"Thirst\":35," +
            "\"PositionX\":3.5,\"PositionY\":1.0,\"PositionZ\":-7.25,\"Inventory\":[\"Stone\",\"Hatchet\"]," +
            "\"ChunkX\":4,\"ChunkZ\":-2,\"WorldSeed\":1234,\"SaveTick\":900}";

        private string _directory;
        private string _path;
        private string _backupPath;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"rustplus-save-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "slot.json");
            _backupPath = _path + ".bak";
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private static SaveGameData Sample(string name = "TestRunner", int health = 92)
        {
            SaveGameData data = SaveDefaults.CreateNewGame(0xFEDCBA9876543210UL);
            data.Player.Name = name;
            data.Player.Health = health;
            data.Player.PositionX = 12.5f;
            data.Player.PositionY = 18f;
            data.Player.PositionZ = -4.25f;
            data.Player.Inventory = new[] { "Stone", "Wood", "Fiber" };
            data.World.LastChunkX = 3;
            data.World.LastChunkZ = -9;
            data.Meta.SaveTick = 5_000_000_000L;
            return data;
        }

        private string[] QuarantinedFiles()
        {
            return Directory.GetFiles(_directory, "*.quarantine-*");
        }

        [Test]
        public void SaveRoundTripPreservesAllSections()
        {
            var service = new LocalSaveService(_path);
            SaveGameData original = Sample();

            service.Save(original);
            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.Loaded));
            SaveGameData loaded = result.Data;
            Assert.That(loaded.SchemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
            Assert.That(loaded.Player.Name, Is.EqualTo("TestRunner"));
            Assert.That(loaded.Player.Health, Is.EqualTo(92));
            Assert.That(loaded.Player.PositionX, Is.EqualTo(12.5f));
            Assert.That(loaded.Player.Inventory, Is.EqualTo(original.Player.Inventory));
            Assert.That(loaded.World.Seed, Is.EqualTo(0xFEDCBA9876543210UL), "ulong seeds above 2^53 must survive JSON");
            Assert.That(loaded.World.LastChunkX, Is.EqualTo(3));
            Assert.That(loaded.World.LastChunkZ, Is.EqualTo(-9));
            Assert.That(loaded.Meta.SaveTick, Is.EqualTo(5_000_000_000L));
            Assert.That(loaded.Meta.SavedAtUtc, Is.Not.Empty);
        }

        [Test]
        public void SaveDoesNotMutateTheCallersData()
        {
            var service = new LocalSaveService(_path);
            SaveGameData original = Sample();
            original.SchemaVersion = 42;

            SaveGameData written = service.Save(original);

            Assert.That(original.SchemaVersion, Is.EqualTo(42));
            Assert.That(original.Meta.SavedAtUtc, Is.Empty);
            Assert.That(written, Is.Not.SameAs(original));
            Assert.That(written.SchemaVersion, Is.EqualTo(SaveGameData.CurrentSchemaVersion));
        }

        [Test]
        public void SecondSaveKeepsThePreviousPrimaryAsBackup()
        {
            var service = new LocalSaveService(_path);
            service.Save(Sample("First", 10));
            Assert.That(File.Exists(_backupPath), Is.False);

            service.Save(Sample("Second", 20));

            Assert.That(File.Exists(_backupPath), Is.True);
            SaveGameData backup = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(_backupPath));
            Assert.That(backup.Player.Name, Is.EqualTo("First"));
            Assert.That(service.Load().Player.Name, Is.EqualTo("Second"));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackupAndRestoresPrimary()
        {
            File.WriteAllText(_path, "{not valid json");
            File.WriteAllText(_backupPath, JsonUtility.ToJson(Sample("Recovered", 77)));
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.RestoredFromBackup));
            Assert.That(result.Data.Player.Name, Is.EqualTo("Recovered"));
            Assert.That(result.QuarantinedFiles.Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(result.QuarantinedFiles[0]), Is.EqualTo("{not valid json"), "the bad primary is kept, not deleted");

            SaveGameData primary = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(_path));
            Assert.That(primary.Player.Name, Is.EqualTo("Recovered"), "primary is restored from the backup");
            Assert.That(service.LoadWithReport().Outcome, Is.EqualTo(SaveLoadOutcome.Loaded));
        }

        [Test]
        public void MissingPrimaryFallsBackToBackup()
        {
            File.WriteAllText(_backupPath, JsonUtility.ToJson(Sample("OnlyBackup", 55)));
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.RestoredFromBackup));
            Assert.That(result.QuarantinedFiles, Is.Empty);
            Assert.That(File.Exists(_path), Is.True);
        }

        [Test]
        public void NewerSchemaInPrimaryFallsBackToBackup()
        {
            File.WriteAllText(_path, "{\"SchemaVersion\":999}");
            File.WriteAllText(_backupPath, JsonUtility.ToJson(Sample("Older", 60)));
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.RestoredFromBackup));
            Assert.That(result.Data.Player.Name, Is.EqualTo("Older"));
            Assert.That(File.ReadAllText(result.QuarantinedFiles.Single()), Does.Contain("999"));
        }

        [Test]
        public void NewerSchemaWithoutBackupThrowsVersionError()
        {
            File.WriteAllText(_path, "{\"SchemaVersion\":999,\"PlayerName\":\"Future\"}");
            var service = new LocalSaveService(_path);

            var error = Assert.Throws<SaveVersionException>(() => service.Load());

            Assert.That(error.FoundVersion, Is.EqualTo(999));
            Assert.That(File.Exists(_path), Is.True, "Load alone never moves or deletes files it cannot read");
        }

        [Test]
        public void EmptyObjectIsTreatedAsCorruptNotAsVersionZero()
        {
            File.WriteAllText(_path, "{}");
            var service = new LocalSaveService(_path);

            Assert.Throws<SaveCorruptException>(() => service.Load());
        }

        [Test]
        public void MissingFilesThrowNotFound()
        {
            var service = new LocalSaveService(_path);

            Assert.Throws<SaveNotFoundException>(() => service.Load());
        }

        [Test]
        public void VersionOneSaveIsMigratedAndRewritten()
        {
            File.WriteAllText(_path, V1Payload);
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.Migrated));
            SaveGameData data = result.Data;
            Assert.That(data.SchemaVersion, Is.EqualTo(2));
            Assert.That(data.Player.Name, Is.EqualTo("Veteran"));
            Assert.That(data.Player.Health, Is.EqualTo(81));
            Assert.That(data.Player.PositionZ, Is.EqualTo(-7.25f));
            Assert.That(data.Player.Inventory, Is.EqualTo(new[] { "Stone", "Hatchet" }));
            Assert.That(data.World.Seed, Is.EqualTo(1234UL));
            Assert.That(data.World.LastChunkX, Is.EqualTo(4));
            Assert.That(data.World.LastChunkZ, Is.EqualTo(-2));
            Assert.That(data.Meta.SaveTick, Is.EqualTo(900L));

            Assert.That(SaveMigrationsProbe(_path), Is.EqualTo(2), "primary is rewritten in the current schema");
            Assert.That(File.ReadAllText(_backupPath), Is.EqualTo(V1Payload), "the original v1 file is kept as the backup");
        }

        [Test]
        public void NegativeVersionOneSeedKeepsItsBitPattern()
        {
            File.WriteAllText(_path, "{\"SchemaVersion\":1,\"PlayerName\":\"Neg\",\"WorldSeed\":-1}");

            SaveGameData data = new LocalSaveService(_path).Load();

            Assert.That(data.World.Seed, Is.EqualTo((ulong)uint.MaxValue));
        }

        [Test]
        public void LoadOrCreateCreatesNewGameWhenNothingExists()
        {
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadOrCreate(() => SaveDefaults.CreateNewGame());

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(result.Data.Player.Name, Is.EqualTo(SaveDefaults.DefaultPlayerName));
            Assert.That(File.Exists(_path), Is.True);
        }

        [Test]
        public void LoadOrCreateQuarantinesUnreadableFilesInsteadOfOverwritingThem()
        {
            File.WriteAllText(_path, "{\"SchemaVersion\":999,\"PlayerName\":\"Future\"}");
            File.WriteAllText(_backupPath, "garbage");
            var service = new LocalSaveService(_path);

            SaveLoadResult result = service.LoadOrCreate(() => SaveDefaults.CreateNewGame());

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.ReplacedUnreadable));
            Assert.That(result.QuarantinedFiles.Count, Is.EqualTo(2));
            string[] contents = QuarantinedFiles().Select(File.ReadAllText).ToArray();
            Assert.That(contents, Has.Some.Contains("Future"));
            Assert.That(contents, Has.Some.EqualTo("garbage"));
            Assert.That(service.Load().Player.Name, Is.EqualTo(SaveDefaults.DefaultPlayerName));
        }

        [Test]
        public void StaleTemporaryFileIsRemovedOnLoad()
        {
            var service = new LocalSaveService(_path);
            service.Save(Sample());
            File.WriteAllText(_path + ".tmp", "{\"SchemaVersion\":2");

            SaveLoadResult result = service.LoadWithReport();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.Loaded));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void CloneIsDeep()
        {
            SaveGameData original = Sample();
            SaveGameData copy = original.Clone();

            copy.Player.Name = "Changed";
            copy.Player.Inventory[0] = "Changed";
            copy.World.Seed = 1UL;

            Assert.That(original.Player.Name, Is.EqualTo("TestRunner"));
            Assert.That(original.Player.Inventory[0], Is.EqualTo("Stone"));
            Assert.That(original.World.Seed, Is.EqualTo(0xFEDCBA9876543210UL));
        }

        private static int SaveMigrationsProbe(string path)
        {
            return SaveMigrations.ReadSchemaVersion(File.ReadAllText(path));
        }
    }
}
