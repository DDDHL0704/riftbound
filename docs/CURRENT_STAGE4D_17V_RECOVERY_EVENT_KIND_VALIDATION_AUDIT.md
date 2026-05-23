2026-05-24 Stage 4D-17V recovery event kind validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateEvents` now rejects recovered events whose `GameEvent.Kind` is blank.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsBlankRecoveredEventKind`

Behavior proved:
- Corrupted recovered event kind metadata is rejected before event ordering, gap validation, spectator redaction, state-hash audit, or action-log replay restoration can consume the event payload.

Validation:
- Focused: `48/48`.
- Adjacent recovery/opening: `629/629`.
- Backend full: `5994/5994`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
