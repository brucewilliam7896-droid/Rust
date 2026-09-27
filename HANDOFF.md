# Rust+ — Project Handoff (goal, context, state)

Last updated: 2026-09-27. Use this to brief a fresh Claude session. Verify anything time-sensitive against the code before acting.

## 1. Goal

Build a Rust-style persistent survival game in Unity, starting **offline-first**. Multiplayer (dedicated authoritative servers, client prediction) is deliberately deferred until the offline exit criteria are met. The full plan is `Assets/Docs/Rust_Like_Survival_Game_Complete_Engineering_Plan.md` (10 phases, ~37 sections; a PDF copy exists).

Working principle from the plan: AI accelerates implementation, tests and docs; the human owns architecture and correctness-critical decisions.

Architecture rules (plan §3): server owns truth; simulation separate from presentation; every persistent schema versioned; every gameplay event attributable; deterministic systems use controlled seeded randomness; expensive work has a budget.

Engine decision (made): Unity with modular MonoBehaviours around plain C# domain logic. Consider ECS only if profiling demands it.

## 2. Environment

- Unity **6000.6.0f1**, URP 17.6, Input System 1.20 (Input System only), Test Framework 1.8.0. Fixed timestep 0.02 s (50 Hz).
- Project root: `D:\Development\GitHub\Rust+` (Windows 11). Source control is Unity Version Control (Plastic; `ignore.conf`, `.plastic`), **not Git**. The plan assumes Git + CI; neither is set up.
- Unity MCP (Coplay `com.coplaydev.unity-mcp` v10.2.0, pinned via git URL). Server: `http://127.0.0.1:8080/mcp`, instance name `Rust+@<hash>`. It gives console reads, refresh/compile, test runs (`run_tests`, `get_test_job`), menu items, scene/GameObject tools. A cloud session cannot reach this; only a local Claude session can.
- Build settings scenes: `SampleScene` (URP template), `DevelopmentTestRange` (placeholder fixtures: RNG samples, physics stress, stability, combat targets). No boot scene exists.

## 3. Code layout (about 14 hand-written C# files)

| Path | Contents |
|---|---|
| `Assets/Core/` (asmdef `RustPlus.Core`) | `Determinism/DeterministicRandom.cs` (PCG32, unbiased `NextInt`, capture/restore state); `Save/LocalSaveService.cs` (versioned JSON, `.tmp` + `File.Replace` + `.bak`, backup fallback); `Save/RuntimePlayerState.cs` (MonoBehaviour, saves on pause/quit); `Diagnostics/StructuredLogger.cs` (injectable sink, static `LogReceived` event); `Diagnostics/DebugConsole.cs` (TextMesh log overlay); `Bootstrap/StartupBootstrap.cs` (`StartupBootstrap` + `GameBootstrapper`) |
| `Assets/Tests/EditMode/` | 16 tests: RNG (6), save (3, misfiled in `DeterministicRandomTests.cs`), logger (5), bootstrap (2) |
| `Assets/Tests/PlayMode/` | 1 test: save/reload round trip of Transform + health |
| `Assets/Editor/` (asmdef `RustPlus.Editor`, namespace `RustPlus.EditorTools`) | `CITestRunner` (menu + `-executeMethod RustPlus.EditorTools.CITestRunner.RunEditModeTestsBatch`, exits 0/1 in batch mode), `UnityBootstrapTestRunner` |
| Other | `Docs/` engineering plan, `progress.md` (project root, running log), template leftovers (`TutorialInfo/`, `Readme.asset`, `InputSystem_Actions.inputactions`, `Settings/`) |

## 4. Current state (Phase 0 — Foundations)

Done (per plan checklist and verified): engine decision, structured logger, versioned save with backup handling, deterministic RNG, local test scene.
Open in the plan: project folder structure (only Core exists), debug tools, data boundaries (player/world/metadata), telemetry, CI, boot scene. Exit criteria still open: "logs and telemetry visible and structured", "stable enough to begin gameplay".

Verified live on 2026-09-27 through Unity MCP: **EditMode 16/16 pass, PlayMode 1/1 pass, no compile errors.**

## 5. What was done this session

- Full read-only analysis of the codebase.
- Fixed the Editor compile failure. The runner scripts sat in the predefined Editor assembly with no reference to the test-runner assemblies, and they used APIs that don't exist (`TestRunnerFilter`, `TestResult`, `GetResults`). Added `Assets/Editor/RustPlus.Editor.asmdef`, rewrote both runners against `Filter`, `ExecutionSettings` and `ICallbacks`. The menu path was verified ("16 passed, 0 failed"); the batch-mode `-executeMethod` path and its exit code were **not** verified.
- No other files were changed.

## 6. Known issues still open (most serious first)

1. **Bootstrap is unattachable and unused.** `GameBootstrapper` (MonoBehaviour) lives in `StartupBootstrap.cs`, so its file name doesn't match its class name and Unity can't add it as a component. No scene references any project script (`RuntimePlayerState`, `DebugConsole`, `GameBootstrapper`). `GameBootstrapper` saves under `persistentDataPath/bootstrap/`, `RuntimePlayerState` under `persistentDataPath/`, so they would create two separate saves.
2. **Schema mismatch can destroy the save.** `Load` throws `InvalidOperationException`; `RuntimePlayerState.Awake` and `StartupBootstrap.EnsureSaveState` then write a default save. The old file becomes `.bak` and is overwritten by the next save. No migration path exists.
3. **`LocalSaveService` gaps.**
   - No flush or fsync before replace.
   - A version mismatch in the primary never falls back to the backup, and `{}` parses as version 0.
   - When the primary is bad and the backup is good, the recursive load path returns the backup without restoring it to the primary.
   - If both files are bad, the backup is read twice.
   - `Save` mutates its input and hardcodes `slot=autosave` in the log.
   - Stale `.tmp` files are never cleaned.
   - `WorldSeed` and `SaveTick` are `int` while the RNG seed is `ulong`.
   - The default player state is duplicated in two places and has drifted (chunk fields).
4. **`DebugConsole` bugs.**
   - `AppendLine` writes `\r\n` but trimming splits on `\n`.
   - O(n) string work on every log.
   - Redundant `_lineHistory`.
   - `#pragma` never restored.
   - Global namespace.
   - Legacy `TextMesh`.
5. **`StructuredLogger` design.**
   - `LogReceived` fires before the sink, so a throwing subscriber suppresses the log write.
   - No timestamps or tick numbers.
   - No level filtering.
   - Not thread-safe.
   - "Telemetry" is only this logger.
6. **Architecture drift.**
   - `Core` mixes MonoBehaviours with logic and depends on `JsonUtility` and `Debug`.
   - The planned Gameplay, World, Systems, UI and Data folders don't exist.
   - The save is one flat player payload containing world seed and chunk data.
   - Vitals fields are not driven by any system.
7. **Test gaps.**
   - `.bak` creation after the second save.
   - Schema mismatch falling back to the backup.
   - `RuntimePlayerState` `Awake` recovery paths.
   - `GameBootstrapper`.
   - The PlayMode test reflects on a private field name.
   - Test asmdefs use the legacy `optionalUnityReferences`.
8. **Stale docs.** `progress.md` says 14 EditMode tests and the Save README says 9; the real count is 16. Both also claim "0 compile errors" from before the fix above. `progress.md` repeats several lines across sections.
9. **Package bloat.** Unused modules (XR, vehicles, wind, video, cloth, vectorgraphics, tetgen), Visual Scripting, Timeline, and `com.unity.pipeline 0.6.0-exp.1`. The MCP package is a git-URL dependency.

## 7. Suggested next steps

1. Move `GameBootstrapper` into `GameBootstrapper.cs`, create a Boot scene, use one save path, and wire `RuntimePlayerState` and `DebugConsole` into a scene.
2. Make schema mismatch non-destructive (copy the old file to a timestamped name) and add fallback and restore logic with tests.
3. Harden `LocalSaveService` (items in §6.3) and add the missing tests.
4. Fix `DebugConsole`; decide the `LogReceived` contract (carry a `StructuredLogEntry`, fire after the sink).
5. Update `progress.md` and the READMEs from a real test run; put the project under Git and add CI running EditMode and PlayMode (`-runTests -testResults`).
6. Then close the remaining Phase 0 items (folder structure, data boundaries, debug tools, telemetry) before starting Phase 1 (core survival loop).

## 8. How to verify work

- Local Claude with Unity MCP: clear console, `refresh_unity` (force, compile), read console errors, `run_tests` for EditMode and PlayMode, poll `get_test_job`.
- Without MCP: Unity Test Runner window, or `Unity -batchmode -projectPath <path> -runTests -testPlatform EditMode -testResults results.xml` (Unity must be closed for the project).

## 9. Collaboration notes

- When something can't be inspected directly (a remote machine, CI, production), the owner prefers one targeted diagnostic script that prints the ground-truth facts, over several guessed theories one at a time.
- Don't commit or push unless asked. The repo is not Git, so there is no commit flow yet.
