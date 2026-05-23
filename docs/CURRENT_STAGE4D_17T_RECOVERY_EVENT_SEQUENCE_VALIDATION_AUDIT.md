2026-05-24 Stage 4D-17T recovery event sequence validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateEvents` now rejects recovered events whose `Sequence` is zero or negative.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNonPositiveRecoveredEventSequence`

Behavior proved:
- Corrupted recovered event sequence metadata is rejected before event ordering, gap checks, or action-log replay restoration can consume the invalid sequence.

Validation:
- Focused: `46/46`.
- Adjacent recovery/opening: `627/627`.
- Backend full: `5992/5992`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
