# Diagnostics foundation

`StructuredLogger` writes categorized, severity-tagged entries to the Unity Console. The default sink maps `Info`, `Warning`, and `Error` to the matching Unity log methods. An optional context string is included when present.

```csharp
var logger = new StructuredLogger();
logger.Log(LogSeverity.Info, "Save", "Save completed", "slot=autosave");
```

Pass an `Action<StructuredLogEntry>` to the constructor to capture or forward entries through a test sink. This foundation is local only; it does not send telemetry or provide debug UI.