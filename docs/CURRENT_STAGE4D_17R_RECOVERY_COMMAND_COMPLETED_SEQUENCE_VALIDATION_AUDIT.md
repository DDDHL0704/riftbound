2026-05-24 Stage 4D-17R recovery command completed event sequence validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects recovered commands whose `CompletedEventSequence` is negative.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsNegativeCommandCompletedEventSequence`

Behavior proved:
- Corrupted command completed event sequence metadata is rejected before event-span coverage, accepted-event ownership, or action-log replay restoration can consume the invalid range.

Validation:
- Focused: `44/44`.
- Adjacent recovery/opening: `625/625`.
- Backend full: `5990/5990`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
