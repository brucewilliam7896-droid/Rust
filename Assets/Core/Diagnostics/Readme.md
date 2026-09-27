# Diagnostics foundation

## Structured logging
`StructuredLogger` writes categorized, severity-tagged entries. Each entry carries a UTC timestamp and the simulation tick (`-1` when no clock runs).

```csharp
var logger = new StructuredLogger();
logger.Log(LogSeverity.Info, "Save", "Save completed", "slot=autosave");
```

- The default sink writes to the Unity Console (`Info`, `Warning`, `Error` map to the matching Unity methods). Pass an `Action<StructuredLogEntry>` to capture entries in tests.
- `StructuredLogger.MinimumSeverity` drops lower-severity entries globally.
- `StructuredLogger.LogReceived` fires after the sink with the full entry. A throwing subscriber is reported as a warning and cannot suppress the sink or other subscribers. Subscribers run on the logging thread.

## Telemetry
Local-only event recording; nothing leaves the machine.

```csharp
Telemetry.Record("save.written", ("file", "player-save.json"), ("tick", 1200));
```

- With no sink installed `Record` is a no-op, so plain C# code can call it freely.
- `GameBootstrapper` installs a `JsonLinesTelemetrySink` per session: `persistentDataPath/telemetry/session-<utc>-<id>.jsonl`, one JSON object per line with `ts`, `tick`, `session`, `event` and the fields.
- Current events: `session.started`, `boot.completed`, `save.written`, `save.created`, `save.migrated`, `save.restored_from_backup`, `save.replaced_unreadable`, `session.ended`.

## Debug tools
`RustPlus.UI.Debugging.DebugOverlay` (in `Assets/UI`) shows FPS, tick, session, save status, recent log lines and save/reload buttons. Toggle with the backquote key.
