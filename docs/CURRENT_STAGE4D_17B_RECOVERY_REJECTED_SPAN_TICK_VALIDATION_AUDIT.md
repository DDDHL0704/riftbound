2026-05-24 Stage 4D-17B recovery rejected-command span/tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now treats rejected recovered commands as invalid if their completed event sequence is greater than their started event sequence.
- `MatchRecoveryValidator.ValidateCommands` now treats rejected recovered commands as invalid if their completed tick differs from their started tick.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsRejectedCommandsThatAdvanceTickOrRecordEvents`

Behavior proved:
- A rejected unsupported command with a positive recovered event span reports the event-span invariant error.
- A rejected unsupported command with completed tick different from started tick reports the tick-advance invariant error.
- The validation happens at base recovery-frame validation time, before action-log replay restoration requires authoritative state.

Validation:
- Focused: `26/26`.
- Adjacent recovery/opening: `607/607`.
- Backend full: `5972/5972`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
