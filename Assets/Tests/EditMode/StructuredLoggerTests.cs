using NUnit.Framework;
using RustPlus.Core.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace RustPlus.Tests.EditMode
{
    public sealed class StructuredLoggerTests
    {
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
    }
}