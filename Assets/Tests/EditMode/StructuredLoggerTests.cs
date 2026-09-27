using System;
using System.Collections.Generic;
using NUnit.Framework;
using RustPlus.Core.Diagnostics;
using RustPlus.Core.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RustPlus.Tests.EditMode
{
    public sealed class StructuredLoggerTests
    {
        private LogSeverity _previousMinimum;

        [SetUp]
        public void SetUp()
        {
            _previousMinimum = StructuredLogger.MinimumSeverity;
            StructuredLogger.MinimumSeverity = LogSeverity.Info;
        }

        [TearDown]
        public void TearDown()
        {
            StructuredLogger.MinimumSeverity = _previousMinimum;
            SimulationClock.Active = null;
        }

        [Test]
        public void Log_PreservesStructuredFieldsForInjectedSink()
        {
            StructuredLogEntry capturedEntry = null;
            var logger = new StructuredLogger(entry => capturedEntry = entry);

            logger.Log(LogSeverity.Warning, "Save", "Backup restored", "slot=autosave");

            Assert.That(capturedEntry, Is.Not.Null);
            Assert.That(capturedEntry.Severity, Is.EqualTo(LogSeverity.Warning));
            Assert.That(capturedEntry.Category, Is.EqualTo("Save"));
            Assert.That(capturedEntry.Message, Is.EqualTo("Backup restored"));
            Assert.That(capturedEntry.Context, Is.EqualTo("slot=autosave"));
            Assert.That((DateTime.UtcNow - capturedEntry.TimestampUtc).TotalSeconds, Is.LessThan(5));
            Assert.That(capturedEntry.Tick, Is.EqualTo(-1), "no clock is running in edit mode");
        }

        [TestCase(LogSeverity.Info, LogType.Log, "[INFO][Save] Save complete | Context=primary")]
        [TestCase(LogSeverity.Warning, LogType.Warning, "[WARNING][Save] Save complete | Context=primary")]
        [TestCase(LogSeverity.Error, LogType.Error, "[ERROR][Save] Save complete | Context=primary")]
        public void Log_WritesFormattedEntryToUnityConsole(
            LogSeverity severity,
            LogType expectedType,
            string expectedMessage)
        {
            LogAssert.Expect(expectedType, expectedMessage);

            new StructuredLogger().Log(severity, "Save", "Save complete", "primary");
        }

        [Test]
        public void Log_OmitsContextSuffixWhenContextIsAbsent()
        {
            string formattedEntry = null;
            var logger = new StructuredLogger(entry => formattedEntry = entry.ToString());

            logger.Log(LogSeverity.Info, "Save", "Save complete");

            Assert.That(formattedEntry, Is.EqualTo("[INFO][Save] Save complete"));
        }

        [Test]
        public void Log_StampsTheActiveSimulationTick()
        {
            var clock = new SimulationClock(41);
            clock.Advance();
            SimulationClock.Active = clock;
            StructuredLogEntry captured = null;

            new StructuredLogger(entry => captured = entry).Log(LogSeverity.Info, "Sim", "Tick");

            Assert.That(captured.Tick, Is.EqualTo(42));
        }

        [Test]
        public void Log_DropsEntriesBelowTheMinimumSeverity()
        {
            StructuredLogger.MinimumSeverity = LogSeverity.Warning;
            var captured = new List<StructuredLogEntry>();
            var logger = new StructuredLogger(captured.Add);

            logger.Log(LogSeverity.Info, "Save", "Dropped");
            logger.Log(LogSeverity.Warning, "Save", "Kept");

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0].Message, Is.EqualTo("Kept"));
        }

        [Test]
        public void Log_WritesToSinkBeforeRaisingTheEvent()
        {
            var order = new List<string>();
            var logger = new StructuredLogger(_ => order.Add("sink"));
            Action<StructuredLogEntry> subscriber = _ => order.Add("event");
            StructuredLogger.LogReceived += subscriber;
            try
            {
                logger.Log(LogSeverity.Info, "Order", "Check");
            }
            finally
            {
                StructuredLogger.LogReceived -= subscriber;
            }

            Assert.That(order, Is.EqualTo(new[] { "sink", "event" }));
        }

        [Test]
        public void Log_IsolatesAThrowingSubscriber()
        {
            var sinkEntries = new List<StructuredLogEntry>();
            StructuredLogEntry secondSubscriberEntry = null;
            var logger = new StructuredLogger(sinkEntries.Add);
            Action<StructuredLogEntry> throwing = _ => throw new InvalidOperationException("boom");
            Action<StructuredLogEntry> healthy = entry => secondSubscriberEntry = entry;
            StructuredLogger.LogReceived += throwing;
            StructuredLogger.LogReceived += healthy;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("threw InvalidOperationException: boom"));
            try
            {
                Assert.DoesNotThrow(() => logger.Log(LogSeverity.Info, "Isolation", "Still delivered"));
            }
            finally
            {
                StructuredLogger.LogReceived -= throwing;
                StructuredLogger.LogReceived -= healthy;
            }

            Assert.That(sinkEntries.Count, Is.EqualTo(1));
            Assert.That(secondSubscriberEntry, Is.Not.Null);
            Assert.That(secondSubscriberEntry.Message, Is.EqualTo("Still delivered"));
        }
    }
}
