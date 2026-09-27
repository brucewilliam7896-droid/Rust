# World

Everything owned by the world rather than a player: terrain and chunk streaming, resource nodes, procedural generation.
World state persists through `WorldSaveData` (seed, chunks) and must stay deterministic from the seed via `DeterministicRandom`.
Starts in Phase 1 (resource nodes) and grows in Phase 4 (procedural generation).
