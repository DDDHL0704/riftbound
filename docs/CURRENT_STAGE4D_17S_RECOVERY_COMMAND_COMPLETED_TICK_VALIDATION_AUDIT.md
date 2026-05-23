2026-05-24 Stage 4D-17S recovery command completed tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects recovered commands whose `CompletedTick` is negative.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNegativeCommandCompletedTick`

Behavior proved:
- Corrupted command completed tick metadata is rejected before command tick-bound comparison, current-tick bounds, or action-log replay restoration can consume the invalid tick.

Validation:
- Focused: `45/45`.
- Adjacent recovery/opening: `626/626`.
- Backend full: `5991/5991`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
