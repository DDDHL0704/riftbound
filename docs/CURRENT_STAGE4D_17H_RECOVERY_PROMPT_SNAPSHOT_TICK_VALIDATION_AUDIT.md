2026-05-24 Stage 4D-17H recovery prompt snapshot tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now checks recovered prompt metadata consistency.
- If an embedded `ActionPromptDto.SnapshotTick` is present, it must match the recovery row `PromptTick`.
- A mismatch now reports a recovery validation error.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsPromptSnapshotTickMismatch`

Behavior proved:
- Corrupted prompt payload snapshot tick metadata is rejected before player-view restoration.

Validation:
- Focused: `34/34`.
- Adjacent recovery/opening: `615/615`.
- Backend full: `5980/5980`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
