using System;
using System.IO;
using NUnit.Framework;
using RustPlus.Core.Determinism;
using RustPlus.Core.Save;
using UnityEngine;

namespace RustPlus.Tests.EditMode
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void SaveRoundTripPreservesVersionedState()
        {
            string path = Path.Combine(Path.GetTempPath(), $"rustplus-save-{Guid.NewGuid():N}.json");
            try
            {
                var service = new LocalSaveService(path);
                var original = new SaveGameData
                {
                    SchemaVersion = SaveGameData.CurrentSchemaVersion,
                    Player = new PlayerData
                    {
                        PlayerName = "TestRunner",
                        Health = 92,
                        Hunger = 64,
                        Thirst = 61,
                        PositionX = 12.5f,
                        PositionY = 18.0f,
                        PositionZ = -4.25f,
                        Inventory = new[] { "Stone", "Wood", "Fiber" }
                    },
                    World = new WorldMetadata()
                };

                service.Save(original);
                SaveGameData loaded = service.Load();

                Assert.That(loaded.Player.PlayerName, Is.EqualTo(original.Player.PlayerName));
                Assert.That(loaded.Player.Health, Is.EqualTo(original.Player.Health));
                Assert.That(loaded.Player.PositionX, Is.EqualTo(original.Player.PositionX));
                Assert.That(loaded.Player.Inventory, Is.EqualTo(original.Player.Inventory));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string backup = path + ".bak";
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                }
            }
        }

        [Test]
        public void CorruptPrimarySaveFallsBackToBackup()
        {
            string path = Path.Combine(Path.GetTempPath(), $"rustplus-save-{Guid.NewGuid():N}.json");
            string backupPath = path + ".bak";
            try
            {
                File.WriteAllText(path, "{not valid json");
                var expected = new SaveGameData
                {
                    SchemaVersion = SaveGameData.CurrentSchemaVersion,
                    Player = new PlayerData
                    {
                        PlayerName = "Recovered",
                        Health = 77,
                        Hunger = 50,
                        Thirst = 40,
                        PositionX = 1f,
                        PositionY = 2f,
                        PositionZ = 3f,
                        Inventory = new[] { "Metal", "Scrap" }
                    },
                    World = new WorldMetadata()
                };

                File.WriteAllText(backupPath, JsonUtility.ToJson(expected));

                var service = new LocalSaveService(path);
                SaveGameData loaded = service.Load();

                Assert.That(loaded.Player.PlayerName, Is.EqualTo("Recovered"));
                Assert.That(loaded.Player.Health, Is.EqualTo(77));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
        }

        [Test]
        public void SaveVersionMismatchThrowsClearError()
        {
            string path = Path.Combine(Path.GetTempPath(), $"rustplus-save-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, "{\"SchemaVersion\":999,\"PlayerName\":\"Old\"}");

                var service = new LocalSaveService(path);

                Assert.Throws<InvalidOperationException>(() => service.Load());
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void EqualSeedsProduceEqualSequences()
        {
            var first = new DeterministicRandom(123456789UL);
            var second = new DeterministicRandom(123456789UL);

            for (int index = 0; index < 32; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        [Test]
        public void SeededSequenceRemainsStable()
        {
            var random = new DeterministicRandom(123456789UL);
            uint[] expected =
            {
                2225433366U,
                773505313U,
                836987698U,
                782696607U,
                1973458049U
            };

            foreach (uint value in expected)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(value));
            }
        }

        [Test]
        public void CapturedStateRestoresTheSequence()
        {
            var random = new DeterministicRandom(987654321UL);
            random.NextUInt();
            DeterministicRandomState state = random.CaptureState();
            uint expectedNextValue = random.NextUInt();

            var restored = new DeterministicRandom(1UL, 2UL);
            restored.RestoreState(state);

            Assert.That(restored.NextUInt(), Is.EqualTo(expectedNextValue));
        }

        [Test]
        public void NextIntStaysWithinHalfOpenBounds()
        {
            var random = new DeterministicRandom(42UL);

            for (int index = 0; index < 1000; index++)
            {
                int value = random.NextInt(-7, 13);
                Assert.That(value, Is.GreaterThanOrEqualTo(-7));
                Assert.That(value, Is.LessThan(13));
            }
        }

        [Test]
        public void NextIntRejectsAnEmptyRange()
        {
            var random = new DeterministicRandom(42UL);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
        }

        [Test]
        public void NextFloatIsInTheUnitInterval()
        {
            var random = new DeterministicRandom(42UL);

            for (int index = 0; index < 1000; index++)
            {
                float value = random.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f));
                Assert.That(value, Is.LessThan(1f));
            }
        }
    }
}