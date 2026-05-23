2026-05-24 Stage 4D-17D recovery accepted-command event ownership validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now tracks loaded event sequences covered by accepted recovered command spans.
- A loaded event sequence covered by more than one accepted command now reports a recovery validation error.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsAcceptedCommandsThatOverlapEventOwnership`

Behavior proved:
- Overlapping accepted `END_TURN` / `PASS` command spans are rejected even though each command can individually satisfy event-count coverage.
- Duplicate event ownership is detected at base recovery-frame validation time, before action-log replay restoration requires authoritative state.

Validation:
- Focused: `28/28`.
- Adjacent recovery/opening: `609/609`.
- Backend full: `5974/5974`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
