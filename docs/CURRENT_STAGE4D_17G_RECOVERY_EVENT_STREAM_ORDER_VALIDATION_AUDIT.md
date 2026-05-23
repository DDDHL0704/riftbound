2026-05-24 Stage 4D-17G recovery event stream order validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateEvents` now checks the original recovered event list order.
- A recovery frame whose event sequence decreases or repeats in frame order now reports a recovery validation error.
- Existing sorted gap/duplicate/end-sequence validation remains in place for canonical stream completeness.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsOutOfOrderRecoveredEvents`

Behavior proved:
- Corrupted out-of-order recovered event lists are rejected before replay tail construction can preserve the bad order.

Validation:
- Focused: `33/33`.
- Adjacent recovery/opening: `614/614`.
- Backend full: `5979/5979`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
