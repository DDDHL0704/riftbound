2026-05-24 Stage 4D-17C recovery command tick-bound validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects recovered commands with `StartedTick < 0`.
- `MatchRecoveryValidator.ValidateCommands` now rejects recovered commands with `CompletedTick < StartedTick`.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsCommandsWithInvalidTickBounds`

Behavior proved:
- A recovered command with negative started tick reports an explicit validation error.
- A recovered command whose completed tick is before started tick reports an explicit validation error.
- The validation happens at base recovery-frame validation time, before action-log replay restoration requires authoritative state.

Validation:
- Focused: `27/27`.
- Adjacent recovery/opening: `608/608`.
- Backend full: `5973/5973`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
