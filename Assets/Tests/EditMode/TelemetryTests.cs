using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RustPlus.Core.Diagnostics;
using RustPlus.Core.Simulation;

namespace RustPlus.Tests.EditMode
{
    public sealed class TelemetryTests
    {
        private sealed class ListSink : ITelemetrySink
        {
            public readonly List<TelemetryEvent> Events = new List<TelemetryEvent>();
            public int FlushCount;

            public void Write(TelemetryEvent telemetryEvent) => Events.Add(telemetryEvent);
            public void Flush() => FlushCount++;
        }

        [TearDown]
        public void TearDown()
        {
            Telemetry.Uninstall();
            SimulationClock.Active = null;
        }

        [Test]
        public void JsonLineContainsHeaderAndTypedFields()
        {
            var telemetryEvent = new TelemetryEvent(
                "save.written",
                "abc123",
                new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc),
                77,
                new (string, object)[] { ("file", "player-save.json"), ("bytes", 512), ("ok", true), ("ratio", 0.5f), ("missing", null) });

            string line = telemetryEvent.ToJsonLine();

            Assert.That(line, Is.EqualTo(
                "{\"ts\":\"2026-09-27T12:00:00.0000000Z\",\"tick\":77,\"session\":\"abc123\",\"event\":\"save.written\"," +
                "\"file\":\"player-save.json\",\"bytes\":512,\"ok\":true,\"ratio\":0.5,\"missing\":null}"));
        }

        [Test]
        public void JsonLineEscapesStrings()
        {
            var telemetryEvent = new TelemetryEvent("quote", "s", DateTime.UtcNow, -1, new (string, object)[] { ("text", "a\"b\\c\nd\u0001") });

            string line = telemetryEvent.ToJsonLine();

            Assert.That(line, Does.Contain("\"text\":\"a\\\"b\\\\c\\nd\\u0001\""));
            Assert.That(line, Does.Not.Contain("\n"));
        }

        [Test]
        public void RecordIsANoOpWithoutASink()
        {
            Assert.That(Telemetry.IsEnabled, Is.False);
            Assert.DoesNotThrow(() => Telemetry.Record("nothing.listens", ("value", 1)));
        }

        [Test]
        public void RecordStampsSessionAndTick()
        {
            var sink = new ListSink();
            Telemetry.Install(sink, "session-1");
            var clock = new SimulationClock(9);
            SimulationClock.Active = clock;

            Telemetry.Record("boot.completed", ("save_outcome", "Loaded"));
            Telemetry.Flush();

            Assert.That(sink.Events.Count, Is.EqualTo(1));
            Assert.That(sink.Events[0].SessionId, Is.EqualTo("session-1"));
            Assert.That(sink.Events[0].Tick, Is.EqualTo(9));
            Assert.That(sink.FlushCount, Is.EqualTo(1));
        }

        [Test]
        public void JsonLinesSinkAppendsOneLinePerEvent()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"rustplus-telemetry-{Guid.NewGuid():N}");
            string path = Path.Combine(directory, "session.jsonl");
            try
            {
                using (var sink = new JsonLinesTelemetrySink(path))
                {
                    Telemetry.Install(sink, "file-session");
                    Telemetry.Record("first");
                    Telemetry.Record("second", ("n", 2));
                    Telemetry.Uninstall();
                }

                string[] lines = File.ReadAllLines(path);
                Assert.That(lines.Length, Is.EqualTo(2));
                Assert.That(lines[0], Does.Contain("\"event\":\"first\""));
                Assert.That(lines[1], Does.Contain("\"n\":2"));
                Assert.That(lines[1], Does.Contain("\"session\":\"file-session\""));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
