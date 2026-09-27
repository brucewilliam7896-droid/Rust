# UI

Presentation only (assembly `RustPlus.UI`). UI reads simulation state and sends player intent; it never owns game state.

- `Debug/DebugOverlay`: development overlay toggled with the backquote key. Shows FPS, simulation tick, session and save status, recent structured log lines, and save/reload buttons.
