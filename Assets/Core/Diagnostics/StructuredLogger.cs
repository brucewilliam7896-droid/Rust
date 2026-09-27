using System;
using UnityEngine;

namespace RustPlus.Core.Diagnostics
{
    public enum LogSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class StructuredLogEntry
    {
        public LogSeverity Severity { get; }
        public string Category { get; }
        public string Message { get; }
        public string Context { get; }

        public StructuredLogEntry(LogSeverity severity, string category, string message, string context)
        {
            Severity = severity;
            Category = category;
            Message = message;
            Context = context;
        }

        public override string ToString()
        {
            string contextSuffix = string.IsNullOrWhiteSpace(Context) ? string.Empty : $" | Context={Context}";
            return $"[{Severity.ToString().ToUpperInvariant()}][{Category}] {Message}{contextSuffix}";
        }
    }

    public sealed class StructuredLogger
    {
        private readonly Action<StructuredLogEntry> _sink;

        /// <summary>
        /// Static event raised on every log call. Consumers can subscribe to receive all structured log entries.
        /// </summary>
        public static event Action<LogSeverity, string, string, string> LogReceived;

        public StructuredLogger() : this(WriteToUnityConsole)
        {
        }

        public StructuredLogger(Action<StructuredLogEntry> sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Log(LogSeverity severity, string category, string message, string context = null)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("A log category is required.", nameof(category));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Raise static event for consumers
            LogReceived?.Invoke(severity, category, message, context);

            _sink(new StructuredLogEntry(severity, category, message, context));
        }

        private static void WriteToUnityConsole(StructuredLogEntry entry)
        {
            string formattedEntry = entry.ToString();
            switch (entry.Severity)
            {
                case LogSeverity.Info:
                    Debug.Log(formattedEntry);
                    break;
                case LogSeverity.Warning:
                    Debug.LogWarning(formattedEntry);
                    break;
                case LogSeverity.Error:
                    Debug.LogError(formattedEntry);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.Severity, "Unsupported log severity.");
            }
        }
    }
}