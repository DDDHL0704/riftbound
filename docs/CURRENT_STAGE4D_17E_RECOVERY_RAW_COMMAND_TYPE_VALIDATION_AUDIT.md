2026-05-24 Stage 4D-17E recovery raw command type consistency validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now reads `rawCommand.cmdType` when present.
- A recovered command whose raw `cmdType` differs from persisted `CommandType` now reports a recovery validation error.
- Existing dev-seed scenario recovery remains accepted: persisted `DEV_SEED_SCENARIO:<id>` maps to raw `DEV_SEED_SCENARIO`.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsRawCommandTypeMismatch`
- `RecoveryValidatorAcceptsDevSeedScenarioRawCommandType`

Behavior proved:
- Ordinary raw command metadata drift is rejected before action-log replay restoration.
- Dev-seed scenario recovery compatibility is preserved.

Validation:
- Focused: `30/30`.
- Adjacent recovery/opening: `611/611`.
- Backend full: `5976/5976`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
