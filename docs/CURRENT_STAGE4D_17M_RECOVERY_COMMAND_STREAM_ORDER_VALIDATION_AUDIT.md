2026-05-24 Stage 4D-17M recovery command stream order validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now checks the original recovered command list order.
- A recovery frame whose command event span moves backwards by `(StartedEventSequence, CompletedEventSequence)` now reports a recovery validation error.
- Equal spans remain allowed so parallel rejected/no-event commands are not rejected solely by ordering.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsOutOfOrderRecoveredCommands`

Behavior proved:
- Corrupted out-of-order recovered command lists are rejected before replay sorting can hide the bad order.

Validation:
- Focused: `39/39`.
- Adjacent recovery/opening: `620/620`.
- Backend full: `5985/5985`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
