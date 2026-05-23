2026-05-24 Stage 4D-17P recovery raw command shape validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects present `RawCommand` payloads that are not JSON objects.
- Present `RawCommand` payloads must include `cmdType`.
- Present `RawCommand.cmdType` must be a non-blank string.
- `RawCommand=null` remains allowed for the existing synthesized replay command path.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsInvalidRawCommandShape`

Behavior proved:
- Corrupted raw command payload shape is rejected before replay command mapping.

Validation:
- Focused: `42/42`.
- Adjacent recovery/opening: `623/623`.
- Backend full: `5988/5988`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
