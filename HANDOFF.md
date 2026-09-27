# Rust+ — Project Handoff (goal, context, state)

Last updated: 2026-09-27. Use this to brief a fresh Claude session. Verify anything time-sensitive against the code before acting.

## 1. Goal

Build a Rust-style persistent survival game in Unity, starting **offline-first**. Multiplayer (dedicated authoritative servers, client prediction) is deliberately deferred until the offline exit criteria are met. The full plan is `Assets/Docs/Rust_Like_Survival_Game_Complete_Engineering_Plan.md` (10 phases; a PDF copy exists). `PLAN.md` holds the Phase 0 completion plan that was just executed.

Working principle from the plan: AI accelerates implementation, tests and docs; the human owns architecture and correctness-critical decisions.

Architecture rules (plan §3): server owns truth; simulation separate from presentation; every persistent schema versioned; every gameplay event attributable; deterministic systems use controlled seeded randomness; expensive work has a budget.

Engine decision (made): Unity with modular MonoBehaviours around plain C# domain logic. Consider ECS only if profiling demands it.

## 2. Environment

- Unity **6000.6.0f1** (installed at `D:\Game-Projects\Unity\Hub\Editor\6000.6.0f1`), URP 17.6.0, Input System 1.20.0 (Input System only), Test Framework 1.8.0. Fixed timestep 0.02 s (50 Hz).
- Project root: `D:\Development\GitHub\Rust`. Source control is **Git** (`github.com/brucewilliam7896-droid/Rust`). The old Plastic workspace is gone.
- Unity MCP: the server runs locally, but the Unity-side package (`com.coplaydev.unity-mcp`) is not in the manifest. Add it from the Package Manager if MCP control of the editor is wanted.
- Build scenes: `Boot` (first), `SampleScene` (URP template), `DevelopmentTestRange` (placeholder fixtures).

## 3. Code layout

| Path | Contents |
|---|---|
| `Assets/Core/` (asmdef `RustPlus.Core`) | `Bootstrap/` (`StartupBootstrap`, `GameBootstrapper`), `Save/` (schema v2 `SaveGameData`, `SaveMigrations`, `LocalSaveService`, `SavePaths`, `SaveDefaults`, `RuntimePlayerState`), `Diagnostics/` (`StructuredLogger`, `Telemetry`), `Determinism/` (PCG32), `Simulation/` (`SimulationClock`) |
| `Assets/UI/` (asmdef `RustPlus.UI`) | `Debug/DebugOverlay` (IMGUI, backquote toggle) |
| `Assets/Gameplay`, `World`, `Systems`, `Data` | Empty except READMEs stating what belongs there |
| `Assets/Editor/` (asmdef `RustPlus.Editor`) | `CITestRunner`, `UnityBootstrapTestRunner`, `BootSceneBuilder` (`RustPlus/Create Boot Scene`) |
| `Assets/Tests/EditMode/` | 39 tests: RNG, save service, bootstrap, logger, telemetry |
| `Assets/Tests/PlayMode/` | 2 tests: player save/reload cycles, Boot scene smoke test |
| `.github/workflows/tests.yml` | game-ci EditMode + PlayMode |

## 4. Current state

Phase 0 is complete (see plan checklist). Verified 2026-09-27 in batch mode on a clean copy: **EditMode 39/39, PlayMode 2/2, no compile errors.** CI has never run; it needs the Unity license secrets.

## 5. Known gaps

- CI requires repository secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`. The game-ci image for 6000.6.0f1 must exist; if not, pin `unityVersion` to the closest available image or use a self-hosted runner.
- No fault-injection tests for power loss between the flush and the replace.
- `RuntimePlayerState` restores position by setting the Transform; a future CharacterController or Rigidbody needs its own restore path.
- Template leftovers remain (`TutorialInfo/`, `Readme.asset`, `SampleScene`). Unused engine modules are still in the manifest.
- `Core` still mixes MonoBehaviours (`GameBootstrapper`, `RuntimePlayerState`) with plain C#; split into a runtime assembly when Gameplay code starts.

## 6. Next steps (Phase 1: core survival loop)

1. Player controller on the Input System in the Boot scene.
2. Vitals decay driven by `SimulationClock`, persisted in `PlayerSaveData`.
3. Resource nodes, gathering, inventory as data (`Assets/Data`), basic crafting.
4. Each system in plain C# with EditMode tests, thin MonoBehaviour adapters.

## 7. How to verify work

- With Unity closed for the project: `Unity.exe -batchmode -projectPath <path> -runTests -testPlatform EditMode -testResults results.xml` (and `PlayMode`).
- While the editor is open on the project: copy `Assets`, `Packages`, `ProjectSettings` to a **short** path (for example `D:\rptest`; long paths break package import) and run batch mode there.
- In the editor: Test Runner window, or `Tools/CI/Run EditMode Tests`.

## 8. Collaboration notes

- When something can't be inspected directly, the owner prefers one targeted diagnostic script that prints the ground-truth facts over several guessed theories.
- Ask before editing code or config, pushing, merging, or opening a PR, unless the owner has said to proceed. Work happens on feature branches, not `main`.
