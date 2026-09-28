---
name: run-rustplus
description: Build, run, and drive the Rust+ Unity project (RustPlus). Use when asked to start the Unity editor for this project, run its EditMode/PlayMode tests, check the console for compile errors, take a screenshot of the running scene, or otherwise interact with the live Unity Editor.
---

Rust+ is a Unity 6 (`6000.6.0f1`) survival-game project. It has no
custom binary or dev server to launch - the "running app" is the Unity
**Editor** with the project open, and it's driven programmatically via
the **MCP for Unity** bridge (`com.coplaydev.unity-mcp`, already a
dependency in `Packages/manifest.json`), which exposes an HTTP JSON-RPC
endpoint at `http://127.0.0.1:8080/mcp`. Drive it with
`.claude/skills/run-rustplus/driver.py` - a zero-dependency Python
script that speaks that protocol directly, so it works whether or not
Claude Code's own MCP client has this server loaded.

All paths below are relative to the repo root (`D:\Development\GitHub\Rust`).

## Prerequisites

- Unity Editor `6000.6.0f1` installed (this project pins that exact
  version in `ProjectSettings/ProjectVersion.txt`). Verified install
  path on this machine: `D:\Game-Projects\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- Python 3 on PATH (stdlib only - no `pip install` needed for the driver).
- The MCP for Unity bridge must be reachable, which means **the Rust+
  project must already be open in the Unity Editor** with the bridge
  started. It auto-starts once configured; if `driver.py` can't connect,
  see Troubleshooting.

## Run (agent path)

With the project open in the Editor, drive it directly:

```bash
python .claude/skills/run-rustplus/driver.py state
```

```json
{
  "success": true,
  "data": {
    "unity": {"instance_id": "Rust@ddcc20b79ead64a8", "unity_version": "6000.6.0f1", "platform": "WindowsEditor"},
    "editor": {"is_focused": false, "play_mode": {"is_playing": false}, "active_scene": {"path": "Assets/Scenes/Boot.unity"}},
    "compilation": {"is_compiling": false},
    "advice": {"ready_for_tools": true}
  }
}
```

Check `data.advice.ready_for_tools` before doing anything else - if
`false` with `blocking_reasons: ["stale_status"]` and `data.staleness.is_stale:
true`, that's just the background snapshot heartbeat going quiet (e.g.
the Editor window lost focus); it does **not** mean tool calls will
fail - `console`/`test`/`screenshot` all still worked when this was
observed. Only trust a `false` as truly blocking when
`data.compilation.is_compiling` or `is_domain_reload_pending` is `true`.

| command | what it does |
|---|---|
| `driver.py state` | Editor readiness snapshot (compiling? focused? active scene?). Check this first. |
| `driver.py instances` | Lists connected Unity instances (`name@hash`) - useful if more than one editor is open. |
| `driver.py console [count] [error\|warning\|log\|all]` | Reads the Unity Console. Default: 20 entries, all types. |
| `driver.py screenshot [name] [--resolution N]` | Captures the Game view to `Assets/Screenshots/<name>`. Prints the full path on disk. |
| `driver.py test [EditMode\|PlayMode] [--timeout SECONDS]` | Runs the test suite and polls until it finishes, printing the pass/fail summary. Default mode: EditMode, timeout: 120s. |
| `driver.py resource <uri>` | Raw MCP resource read (e.g. `mcpforunity://scene/cameras`). |
| `driver.py tool <name> ['<json-args>']` | Raw MCP tool call for anything not wrapped above (`manage_scene`, `manage_gameobject`, `find_gameobjects`, `read_console` with custom filters, etc). See `mcpforunity://` resources and the tool list via a raw `initialize` for the full catalog - there are 40+ tools (scene, GameObject, script, asset, build, graphics, camera, physics, UI Toolkit, ProBuilder...). |

Example - reading compile errors:

```bash
python .claude/skills/run-rustplus/driver.py console 10 error
```

Example - running tests and reading the summary from stderr:

```bash
python .claude/skills/run-rustplus/driver.py test EditMode
# -> EditMode: 16 passed, 0 failed, 0 skipped (0.05s)
```

Example - screenshot:

```bash
python .claude/skills/run-rustplus/driver.py screenshot check.png --resolution 512
# -> Saved to: D:/Development/GitHub/Rust/Assets/Screenshots/check.png
```

Screenshot capture is **asynchronous** in Unity - the driver sleeps 2s
after requesting one before reporting the path; if you poll the file
yourself instead, don't check for its existence immediately.

## Run (human path)

Open Unity Hub, select the Rust+ project, and let it load. That's the
whole "run" - there's no separate launch step, the Editor *is* the app.

If the project is **not** already open, launch it from the command line:

```bash
"D:\Game-Projects\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -projectPath "D:\Development\GitHub\Rust"
```

(Not executed in this form during authoring, since it would have opened
a second window against the same project that was already open - see
below. The exe path and `-projectPath` argument were verified via the
`-batchmode` probe instead.) This opens the normal windowed Editor -
useless in a truly headless environment, but this is a Windows desktop
project, not a container build.

If another instance already has the project open, Unity refuses to
start a second one - verified by actually running this:

```bash
"D:\Game-Projects\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "D:\Development\GitHub\Rust" -logFile -
# -> Aborting batchmode due to fatal error:
#    It looks like another Unity instance is running with this project open.
```

Use the driver against the existing instance instead of trying to
launch a new one.

## Test

```bash
python .claude/skills/run-rustplus/driver.py test EditMode
python .claude/skills/run-rustplus/driver.py test PlayMode
```

Verified result at time of writing: **16/16 EditMode tests pass**
(`RustPlus.Tests.EditMode`, covering deterministic RNG, save/load,
structured logging, and startup bootstrap). PlayMode has one test
(`RuntimePlayerStatePlayModeTests`) that requires the Editor to enter
Play mode, which is slower - give it a longer `--timeout`.

---

## Gotchas

- **MCP tools registered mid-session are invisible to that Claude Code
  session.** `claude mcp list` will show `UnityMCP` as "Connected" the
  moment the Unity Editor's bridge starts, but a Claude Code session
  that was already running when the server was added won't have its
  tools in `ToolSearch` until restarted. `driver.py` sidesteps this
  entirely by talking raw JSON-RPC over HTTP - no dependency on Claude
  Code's own MCP client state.
- **The HTTP response is SSE, not plain JSON**, even for one-shot
  request/response calls: the body is `event: message\ndata: {...}\n\n`.
  Parse out the `data:` line, don't `json.loads()` the raw body.
- **A session handshake is mandatory.** POST `initialize`, capture the
  `Mcp-Session-Id` response header, then POST `notifications/initialized`
  before any `tools/call` or `resources/read` - skipping the notification
  still works for the very first call in practice, but the server logs
  it as a warning; sending it is the two extra lines and avoids relying
  on undocumented leniency.
- **Only one Unity Editor instance per project.** A second `Unity.exe
  -projectPath <same path>` aborts immediately with "another Unity
  instance is running with this project open" - verified above. Don't
  try to launch a second instance to "make sure it's running"; check
  `driver.py instances` / `driver.py state` first.
- **Per-project MCP registration.** `UnityMCP` is added to Claude
  Code's **local** (per-project) config, not global - `claude mcp get
  UnityMCP` shows `Scope: Local config (private to you in this
  project)`. It won't show up in an unrelated project's session.

## Troubleshooting

- **`Could not reach Unity MCP bridge at http://127.0.0.1:8080/mcp`**:
  the Rust+ project isn't open in the Editor, or its MCP bridge hasn't
  started. Open the project in Unity Hub and wait for it to finish
  loading; re-run `driver.py state`.
- **`error CS0246: The type or namespace name 'LitMaterialExport' could
  not be found`** (seen via `driver.py console`, from
  `com.unity.cloud.gltfast`'s `MaterialExport.cs`): this package's URP
  export path needs `com.unity.render-pipelines.universal`, which is
  **not currently installed** in this project's `manifest.json`. This
  is a real, currently-unresolved compile error in this repo, not a
  driver problem - it blocks that one package assembly, but did not
  block the `RustPlus.Tests.EditMode` assembly (16/16 still passed).
  Flagged here so a future run isn't mistaken for something the driver
  broke; fixing it means either installing URP or removing gltFast's
  export feature.
- **`Unable to parse file Assets/Scenes/Boot.unity: [Parser Failure at
  line 404: Expected closing '}']`** (seen via `driver.py console`):
  Unity's own console reported this scene as failing to parse on a
  refresh, even though it's the currently active scene. Also
  pre-existing, not caused by the driver. Worth a look in the Editor
  (open Boot.unity, check for a corrupted `GUIStyle` field around line
  404 - `DebugOverlay._lineStyle` - and re-save) before trusting that
  scene's on-disk state.
- **Screenshot file not found immediately after the tool call
  returns**: capture is async (`"isAsync": true` in the response).
  Wait ~2s (the driver does this for you) before reading the file.
