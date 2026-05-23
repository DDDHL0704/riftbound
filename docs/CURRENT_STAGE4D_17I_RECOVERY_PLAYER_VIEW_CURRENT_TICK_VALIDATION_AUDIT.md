2026-05-24 Stage 4D-17I recovery player-view current tick bound validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now receives the recovery frame `currentTick`.
- Recovered snapshot row ticks cannot be greater than the recovery frame `currentTick`.
- Recovered prompt row ticks cannot be greater than the recovery frame `currentTick`.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsPlayerViewTicksAfterRecoveryTick`

Behavior proved:
- Corrupted player-view snapshot/prompt metadata from a future tick is rejected before player-view restoration.

Validation:
- Focused: `35/35`.
- Adjacent recovery/opening: `616/616`.
- Backend full: `5981/5981`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
