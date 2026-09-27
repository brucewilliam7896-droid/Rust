using System;
using NUnit.Framework;
using RustPlus.Core.Determinism;

namespace RustPlus.Tests.EditMode
{
    public sealed class DeterministicRandomTests
    {
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