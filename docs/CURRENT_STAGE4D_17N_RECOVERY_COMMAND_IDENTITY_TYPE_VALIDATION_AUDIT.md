2026-05-24 Stage 4D-17N recovery command identity/type validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects blank recovered command `PlayerId`.
- `MatchRecoveryValidator.ValidateCommands` now rejects blank recovered command `ClientIntentId`.
- `MatchRecoveryValidator.ValidateCommands` now rejects blank recovered command `CommandType`.
- Raw command type comparison is still performed when recovered command type is present; missing recovered type is reported directly instead of flowing into replay mapping.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsBlankRecoveredCommandIdentityAndType`

Behavior proved:
- Corrupted command identity/type metadata is rejected before idempotency-cache restoration or replay mapping.

Validation:
- Focused: `40/40`.
- Adjacent recovery/opening: `621/621`.
- Backend full: `5986/5986`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
