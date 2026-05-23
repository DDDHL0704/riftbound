2026-05-24 Stage 4D-17O recovery command diagnostic presence validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now rejects accepted recovered commands with any stored error message.
- `MatchRecoveryValidator.ValidateCommands` now rejects rejected recovered commands without a non-blank error message.
- This aligns recovered command diagnostics with normal runtime journal behavior before idempotency-cache restoration and replay audit.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsCommandDiagnosticPresenceMismatch`

Behavior proved:
- Corrupted accepted/error-message and rejected/missing-error command records are rejected before restore or replay.

Validation:
- Focused: `41/41`.
- Adjacent recovery/opening: `622/622`.
- Backend full: `5987/5987`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
