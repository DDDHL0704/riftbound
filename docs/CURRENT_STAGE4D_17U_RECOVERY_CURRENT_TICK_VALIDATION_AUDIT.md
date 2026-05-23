2026-05-24 Stage 4D-17U recovery current tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.Validate` now rejects negative recovery `currentTick` values.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNegativeCurrentTick`

Behavior proved:
- Corrupted recovery frame current-tick metadata is rejected before event, command, player-view, authoritative-state, or spectator replay tick comparisons can consume the invalid tick.

Validation:
- Focused: `47/47`.
- Adjacent recovery/opening: `628/628`.
- Backend full: `5993/5993`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
