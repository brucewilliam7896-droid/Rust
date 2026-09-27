# Phase 0 Completion Plan

Written 2026-09-27. Goal: close every open Phase 0 item in
`Assets/Docs/Rust_Like_Survival_Game_Complete_Engineering_Plan.md` so Phase 1
(core survival loop) can start on a stable base. Work happens on the Git branch
`claude/project-thread-4gouxt`, one commit per step, verified by running the
EditMode and PlayMode suites in batch mode.

## Starting point (verified)

- Unity 6000.6.0f1. The committed `Packages/manifest.json` has lost URP, the
  Input System and the direct Test Framework reference, while
  `QualitySettings` still points at URP assets and `activeInputHandler` is
  Input System only. The scenes reference URP components.
- The Plastic workspace `Rust+` no longer exists locally; this Git repo is the
  only copy and is the source of truth from now on.
- About 14 C# files: RNG, versioned save, logger, debug console, bootstrap.
  16 EditMode tests, 1 PlayMode test. No gameplay code.

## Steps

### 1. Restore packages and clean the repo
- Add `com.unity.render-pipelines.universal` 17.6.0, `com.unity.inputsystem`
  1.20.0 and `com.unity.test-framework` 1.8.0 (the versions this editor bundles).
- Stop tracking `.dotnet-home/` and `.plastic/`; add them to `.gitignore`.
- Out of scope: the Unity MCP package (dev tooling only; re-add from the
  Package Manager when needed) and pruning unused engine modules.

### 2. Bootstrap, boot scene and one save path
- Move `GameBootstrapper` into `GameBootstrapper.cs` so Unity can attach it.
- One save location: `SavePaths.DefaultSavePath` (`persistentDataPath/saves/player-save.json`),
  used by both the bootstrapper and `RuntimePlayerState`.
- One source of default state: `SaveDefaults.CreateNewGame()`.
- `Boot` scene created by an editor command (`RustPlus/Create Boot Scene`),
  first in Build Settings, containing the bootstrapper, a player-state object
  and the debug overlay.

### 3. Saves never destroy data
- Schema versions older than current are migrated in code (v1 to v2).
- Unknown or newer versions, or files that are corrupt in both primary and
  backup, are quarantined (copied to a timestamped `.quarantine` file) before
  a fresh game is created. Nothing is overwritten without a copy.
- `LocalSaveService` fixes: flush to disk before replace; version or parse
  failure in the primary falls back to the backup and restores the primary
  from it; `{}` (version 0) is invalid; `Save` no longer mutates its input;
  stale `.tmp` files are cleaned; typed exceptions (`SaveNotFoundException`,
  `SaveCorruptException`, `SaveVersionException`).
- Tests for each path.

### 4. Player, world and metadata boundaries (schema v2)
- `SaveGameData` v2 = `Meta` (save tick as `long`, UTC time, game version) +
  `Player` (name, vitals, position, inventory) + `World` (seed as `ulong`,
  last chunk). v1 files migrate automatically.

### 5. Logging, telemetry and debug tools
- `StructuredLogger`: UTC timestamp and optional tick on every entry, minimum
  level filter, sink runs first, `LogReceived` carries the entry and a
  throwing subscriber can no longer break logging. Thread-safe dispatch.
- Telemetry: `TelemetryRecorder` writes session-scoped JSON Lines events to
  `persistentDataPath/telemetry/`, flushed on quit. Bootstrap and save emit
  events.
- Debug tools: `DebugOverlay` (IMGUI, toggle with backquote via the Input
  System) showing FPS, fixed tick, recent log lines and quick save/load.
  The broken `TextMesh` `DebugConsole` is replaced.

### 6. Folder structure and test hygiene
- Create `Gameplay`, `World`, `Systems`, `UI`, `Data` folders with a short
  README stating what belongs there. Existing `Core` (incl. Save) and `Tests`
  stay.
- Split save tests out of `DeterministicRandomTests.cs`; modern test asmdefs
  (`UNITY_INCLUDE_TESTS`, `nunit.framework.dll`); PlayMode test no longer
  reflects on a private field.

### 7. CI
- `.github/workflows/tests.yml` running EditMode and PlayMode via
  `game-ci/unity-test-runner`. Needs the `UNITY_LICENSE`, `UNITY_EMAIL` and
  `UNITY_PASSWORD` repository secrets, which only the owner can add.

### 8. Verify and hand off
- Run both suites in batch mode locally; fix until green.
- Update `progress.md`, `HANDOFF.md`, READMEs and the Phase 0 checklist from
  the real results. Commit and push the branch. No PR unless asked.

## Exit check (Phase 0)
Boots from `Boot` scene; save/reload repeatable; versioning enforced with
migration and quarantine; logs and telemetry structured and visible; tests
green locally; CI defined.
