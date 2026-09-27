using System;
using System.Globalization;
using System.IO;
using System.Text;
using RustPlus.Core.Simulation;

namespace RustPlus.Core.Diagnostics
{
    /// <summary>A named gameplay or system event with flat key/value fields.</summary>
    public sealed class TelemetryEvent
    {
        public string Name { get; }
        public string SessionId { get; }
        public DateTime TimestampUtc { get; }
        public long Tick { get; }
        public (string Key, object Value)[] Fields { get; }

        public TelemetryEvent(string name, string sessionId, DateTime timestampUtc, long tick, (string Key, object Value)[] fields)
        {
            Name = name;
            SessionId = sessionId;
            TimestampUtc = timestampUtc;
            Tick = tick;
            Fields = fields ?? Array.Empty<(string, object)>();
        }

        /// <summary>One JSON object on one line (JSON Lines). Numbers and booleans stay unquoted.</summary>
        public string ToJsonLine()
        {
            var builder = new StringBuilder(128);
            builder.Append("{\"ts\":");
            AppendString(builder, TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
            builder.Append(",\"tick\":").Append(Tick.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"session\":");
            AppendString(builder, SessionId ?? string.Empty);
            builder.Append(",\"event\":");
            AppendString(builder, Name);

            foreach ((string key, object value) in Fields)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                builder.Append(',');
                AppendString(builder, key);
                builder.Append(':');
                AppendValue(builder, value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                case int _:
                case long _:
                case uint _:
                case ulong _:
                case short _:
                case ushort _:
                case byte _:
                case sbyte _:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case float single when !float.IsNaN(single) && !float.IsInfinity(single):
                    builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double real when !double.IsNaN(real) && !double.IsInfinity(real):
                    builder.Append(real.ToString("R", CultureInfo.InvariantCulture));
                    break;
                default:
                    AppendString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }

    public interface ITelemetrySink
    {
        void Write(TelemetryEvent telemetryEvent);
        void Flush();
    }

    /// <summary>
    /// Local-only telemetry: events are appended to a JSON Lines file for this session.
    /// Nothing leaves the machine. Thread-safe.
    /// </summary>
    public sealed class JsonLinesTelemetrySink : ITelemetrySink, IDisposable
    {
        private readonly object _gate = new object();
        private StreamWriter _writer;

        public string FilePath { get; }

        public JsonLinesTelemetrySink(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A telemetry file path is required.", nameof(filePath));
            }

            FilePath = filePath;
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        }

        public void Write(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                return;
            }

            string line = telemetryEvent.ToJsonLine();
            lock (_gate)
            {
                _writer?.WriteLine(line);
            }
        }

        public void Flush()
        {
            lock (_gate)
            {
                _writer?.Flush();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }

    /// <summary>
    /// Global entry point for recording events. With no sink installed, recording is a no-op,
    /// so plain C# code and tests can call it freely.
    /// </summary>
    public static class Telemetry
    {
        private static ITelemetrySink _sink;

        public static string SessionId { get; private set; } = string.Empty;

        public static bool IsEnabled => _sink != null;

        public static void Install(ITelemetrySink sink, string sessionId)
        {
            _sink = sink;
            SessionId = sessionId ?? string.Empty;
        }

        /// <summary>Removes the sink and returns it so the caller can flush or dispose it.</summary>
        public static ITelemetrySink Uninstall()
        {
            ITelemetrySink previous = _sink;
            _sink = null;
            SessionId = string.Empty;
            return previous;
        }

        public static void Record(string eventName, params (string Key, object Value)[] fields)
        {
            ITelemetrySink sink = _sink;
            if (sink == null || string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            long tick = SimulationClock.CurrentTick;
            sink.Write(new TelemetryEvent(eventName, SessionId, DateTime.UtcNow, tick, fields));
        }

        public static void Flush()
        {
            _sink?.Flush();
        }
    }
}
