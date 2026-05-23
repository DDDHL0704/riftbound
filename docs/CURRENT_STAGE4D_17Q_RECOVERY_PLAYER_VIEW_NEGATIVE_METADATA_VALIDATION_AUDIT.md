2026-05-24 Stage 4D-17Q recovery player-view negative metadata validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects negative snapshot row tick.
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects negative snapshot event sequence.
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects negative prompt row tick.
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects negative prompt event sequence.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNegativePlayerViewRowMetadata`

Behavior proved:
- Corrupted player-view row metadata is rejected before replay-point calculation or prompt restoration.

Validation:
- Focused: `43/43`.
- Adjacent recovery/opening: `624/624`.
- Backend full: `5989/5989`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
