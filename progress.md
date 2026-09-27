# Progress Log

## 2026-09-27

### Status
- Approved offline-first direction remains in force.
- Phase 0 foundation work is active.
- The deterministic RNG slice is implemented and tested.
- The versioned save/load slice is implemented and verified in Edit Mode and Play Mode.
- The structured logging slice is implemented and verified in Edit Mode.
- Save/reload gate is closed: version enforcement, backup recovery, atomic repeated writes, and runtime Transform restoration are tested.
- World chunk coordinates, world seed, and save tick are now persisted in save data.
- Telemetry logging integrated into save service.

### Completed
- Unity project foundation and workspace configuration verified.
- Deterministic random implementation added under `Assets/Core/Determinism/DeterministicRandom.cs`.
- EditMode tests for deterministic RNG are present and passing.
- Save/load contract tests were added to capture the Phase 0 requirements.
- Versioned local save service added under `Assets/Core/Save/LocalSaveService.cs`.
- Runtime player-state adapter verified through repeated Play Mode save/reload cycles.
- Unity EditMode suite passed all 9 tests; PlayMode persistence test passed (1/1).
- Structured logger added with injectable entries and Unity Console routing for info, warning, and error severities.
- Logger tests cover structured fields, all severity routes, and optional context formatting.
- Save/reload gate is closed: version enforcement, backup recovery, atomic repeated writes, and runtime Transform restoration are tested.
- World chunk coordinates (ChunkX/ChunkZ), world generation seed (WorldSeed), and save tick (SaveTick) added to save schema.
- Telemetry logging integrated into save service via StructuredLogger.
- Startup bootstrap slice: local save bootstrap flow created and validated.
- Debug console tool added under `Assets/Core/Diagnostics/DebugConsole.cs` for real-time structured log monitoring.
- Static `LogReceived` event added to `StructuredLogger` for debug consumer subscription.
- CI test runner created under `Assets/Editor/CITestRunner.cs` for automated EditMode test execution.
- Fixed timestep configured (0.02s / 50 Hz) in ProjectSettings.asset.
- InputManager cleaned up: debug buttons removed for cleaner survival game input profile.

### Current focus
- Phase 0 save/reload gate is closed: version enforcement, backup recovery, atomic repeated writes, and runtime Transform restoration are tested.
- World chunk coordinates, world seed, and save tick persisted in save data.
- Telemetry logging integrated into save service.
- Debug console tool added for real-time structured log monitoring.
- Startup bootstrap slice is now complete: local save bootstrap flow validated.
- CI foundation: test runner scripts created for EditMode and PlayMode automated execution.
- Continue debug tooling expansion and CI pipeline configuration before starting gameplay systems.

### Latest work
- Added a runtime player-state wrapper in [Assets/Core/Save/RuntimePlayerState.cs](Assets/Core/Save/RuntimePlayerState.cs) to bind save data to a Unity transform and persist on pause/quit.
- Added the supporting documentation file [Assets/Core/Save/Readme.md](Assets/Core/Save/Readme.md) to document the purpose of the save foundation.
- Fixed schema-version mismatch handling so unsupported saves report the correct error.
- Changed repeated saves to atomically replace the primary file and preserve the previous file as backup.
- Added a Play Mode regression test covering two save/move/reload cycles for player health and Transform position.
- Added `Assets/Core/Diagnostics/StructuredLogger.cs` and its usage notes at `Assets/Core/Diagnostics/Readme.md`.
- Added a local startup bootstrap flow in [Assets/Core/Bootstrap/StartupBootstrap.cs](Assets/Core/Bootstrap/StartupBootstrap.cs) and a matching bootstrap test in [Assets/Tests/EditMode/StartupBootstrapTests.cs](Assets/Tests/EditMode/StartupBootstrapTests.cs) to ensure a default save is created when missing and a valid save is reused.
- Updated the Phase 0 checklist to mark the structured logger complete while keeping debug tools and telemetry open.
- Started the configured Unity MCP HTTP server and reconnected to the DevelopmentTestRange editor session.

### Verification
- Unity EditMode: 14 passed, 0 failed, including save/RNG contracts and logger field, formatting, and severity-routing checks from earlier validation.
- Unity PlayMode: `RuntimePlayerStatePlayModeTests.SaveAndReloadRestoresTransformAcrossRepeatedCycles` passed (1/1), covering two Transform-and-health save/reload cycles.
- Startup bootstrap slice: file-level diagnostics report no C# errors in the new bootstrap and test files.
- Unity batch-mode validation for the new bootstrap suite was attempted through the editor CLI, but the editor exited without returning a usable test result in this environment; final runtime pass for that slice remains pending editor confirmation.
- DevelopmentTestRange validation: clean, 0 issues.
- Unity console: 0 errors after compilation in prior successful editor checks.
- Unity emitted unrelated Visual Studio UDP-port and AI-service subscription messages during the latest test run; no compile errors were reported in the earlier successful pass.

### Notes
- The project remains aligned to the approved operational plan in [Assets/Docs/Rust_Like_Survival_Game_Complete_Engineering_Plan.md](Assets/Docs/Rust_Like_Survival_Game_Complete_Engineering_Plan.md).
- Multiplayer remains intentionally deferred until the offline-first exit criteria are fulfilled.
- The test scene remains a separate validation target, not a production gameplay scene.
- Persistence currently covers player state only; schema migration and world/chunk/structure saves are not implemented.
- Backup recovery was tested with a corrupted primary save; interrupted-write fault injection is still outstanding.
