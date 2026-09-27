using System;
using RustPlus.Core.Simulation;
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
        public DateTime TimestampUtc { get; }

        /// <summary>Simulation tick when the entry was written, or -1 when no clock is running.</summary>
        public long Tick { get; }

        public StructuredLogEntry(LogSeverity severity, string category, string message, string context)
            : this(severity, category, message, context, DateTime.UtcNow, -1)
        {
        }

        public StructuredLogEntry(LogSeverity severity, string category, string message, string context, DateTime timestampUtc, long tick)
        {
            Severity = severity;
            Category = category;
            Message = message;
            Context = context;
            TimestampUtc = timestampUtc;
            Tick = tick;
        }

        /// <summary>Console format. The Unity Console adds its own timestamp, so this one omits it.</summary>
        public override string ToString()
        {
            string contextSuffix = string.IsNullOrWhiteSpace(Context) ? string.Empty : $" | Context={Context}";
            return $"[{Severity.ToString().ToUpperInvariant()}][{Category}] {Message}{contextSuffix}";
        }
    }

    /// <summary>
    /// Categorized, severity-tagged logging. Entries go to the instance sink first, then to
    /// <see cref="LogReceived"/> subscribers (debug overlay, telemetry). A throwing subscriber is
    /// isolated and cannot suppress the sink or other subscribers. Safe to call from any thread;
    /// subscribers run on the calling thread.
    /// </summary>
    public sealed class StructuredLogger
    {
        private readonly Action<StructuredLogEntry> _sink;

        /// <summary>Raised after the sink for every entry at or above <see cref="MinimumSeverity"/>.</summary>
        public static event Action<StructuredLogEntry> LogReceived;

        /// <summary>Entries below this severity are dropped globally.</summary>
        public static LogSeverity MinimumSeverity { get; set; } = LogSeverity.Info;

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

            if (severity < MinimumSeverity)
            {
                return;
            }

            var entry = new StructuredLogEntry(severity, category, message, context, DateTime.UtcNow, SimulationClock.CurrentTick);

            _sink(entry);

            Action<StructuredLogEntry> subscribers = LogReceived;
            if (subscribers == null)
            {
                return;
            }

            foreach (Delegate subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    ((Action<StructuredLogEntry>)subscriber)(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WARNING][Diagnostics] Log subscriber {subscriber.Method.DeclaringType?.Name}.{subscriber.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
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
