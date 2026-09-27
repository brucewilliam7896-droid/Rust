# Save and state foundation

This folder contains the Phase 0 offline persistence foundation.

## Included pieces
- `LocalSaveService`: schema-versioned JSON persistence with temporary writes, atomic replacement, and backup recovery.
- `RuntimePlayerState`: captures and restores player vitals, inventory identifiers, and a tracked Transform; saves on application pause and quit.
- `SaveGameData`: schema version 1 payload containing player name, health, hunger, thirst, position, and inventory identifiers.

## File behavior
The default runtime file is `player-save.json` under `Application.persistentDataPath`; the filename can be overridden on `RuntimePlayerState`.

Each write serializes to a `.tmp` file. The first save moves that file into place. Later saves use `File.Replace` to atomically replace the primary file while keeping the prior primary as `.bak`. Loading prefers the primary file and falls back to the backup when the primary payload is malformed; a recovered backup is copied back to the primary path.

`LocalSaveService.Load` rejects schema versions other than the current version. The runtime adapter currently responds to an unsupported version by creating and saving a default player state; schema migration is not implemented.

## Verification and limits
The Unity Edit Mode suite passes 9 tests, including save round-trip, corrupt-primary backup recovery, and schema mismatch handling. `RuntimePlayerStatePlayModeTests.SaveAndReloadRestoresTransformAcrossRepeatedCycles` passes in Play Mode and verifies two position-and-health save/reload cycles using a temporary, unique save filename.

The current payload is player-only; world, chunk, and structure state are not persisted. Backup recovery is tested with a corrupted primary file, but interruption at each filesystem-write boundary has not been fault-injected. The development test scene remains unchanged; the Play Mode test creates and removes its own temporary object.
