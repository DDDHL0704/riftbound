2026-05-24 Stage 4D-17L recovery command current-tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now receives recovery frame `currentTick`.
- Recovered commands reject `StartedTick` after recovery `currentTick` when available.
- Recovered commands reject `CompletedTick` after recovery `currentTick` when available.
- This aligns recovered command tick metadata with the existing recovery tick bounds for events, snapshots and prompts.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsRecoveredCommandTicksAfterRecoveryTick`

Behavior proved:
- Corrupted future command tick metadata is rejected before replay restoration.

Validation:
- Focused: `38/38`.
- Adjacent recovery/opening: `619/619`.
- Backend full: `5984/5984`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
