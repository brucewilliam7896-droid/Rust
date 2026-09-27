# Save and state foundation

Offline persistence for one save slot. Schema version 2.

## Pieces
- `SaveGameData` (schema v2): `Meta` (save tick, UTC time, game version), `Player` (name, vitals, position, inventory), `World` (seed as `ulong`, last chunk). These are the player / world / metadata data boundaries.
- `SaveMigrations`: frozen DTOs for old schemas and the upgrade path. v1 (flat player payload) migrates automatically.
- `LocalSaveService`: versioned JSON persistence with durable writes, backup rotation, fallback, and quarantine.
- `SaveDefaults`: the only definition of a new game's starting state.
- `SavePaths`: the only place paths are decided. Saves live in `persistentDataPath/saves/player-save.json`, telemetry in `persistentDataPath/telemetry/`. Tests redirect the root with `SavePaths.RootOverride`.
- `RuntimePlayerState`: binds the save to the player Transform; saves on pause and quit. `Configure(path)` before it wakes points it elsewhere.

## File behavior
- Writes go to `.tmp`, are flushed to disk (`Flush(true)`), then `File.Replace` swaps them in and keeps the previous primary as `.bak`.
- Load reads the primary. If it is missing, corrupt, has no version (`{}`), or has an unknown version, the backup is used and copied back to the primary. An unreadable primary is moved to `<file>.quarantine-<utc>` first.
- Older schemas are migrated and rewritten; the original file becomes the `.bak`.
- `Load` throws `SaveNotFoundException`, `SaveVersionException` or `SaveCorruptException` and never moves files it cannot read.
- `LoadOrCreate` (used at boot) creates a new game when nothing is usable, quarantining every unreadable file first. **No code path deletes or overwrites a save without keeping a copy.**
- A leftover `.tmp` from an interrupted write is deleted on load; the primary and backup are authoritative.
- `Save` never mutates the caller's object and returns the copy that was written.

## Tests
`Assets/Tests/EditMode/LocalSaveServiceTests.cs` covers round trip, no mutation, backup rotation, corrupt/missing/newer primary fallback, version-error and corrupt errors, v1 migration (including seed bit pattern), quarantine, stale `.tmp` cleanup and deep clone. `StartupBootstrapTests` and the PlayMode suite cover boot and runtime use.

## Not yet covered
Fault injection at each filesystem step (power loss mid-replace), multiple save slots, and world/structure payloads beyond seed and chunk.
