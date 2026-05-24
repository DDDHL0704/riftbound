# Stage 4D-17BO Recovery Spectator Replay Timing Battle Damage-Assignment Required-Assignments Validation Audit

2026-05-24 Stage 4D-17BO recovery spectator replay timing battle damage-assignment required-assignments validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["requiredAssignments"]` disagrees with authoritative `ResolutionResult.BattleRequiredAssignmentsFor(state, battle)`.
- The required-assignments check only applies after `isPending`, `phase`, identity fields, `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` match the authoritative pending battle damage-assignment window.
- The validator now parses required-assignment arrays from in-memory snapshot objects and JSON replay shapes before comparing source object id, damage and legal target ids in order.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentsMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment required-assignment metadata is rejected before spectator snapshot / authoritative battle required-assignment drift can pass recovery validation.

Validation:
- Focused: `93/93`.
- Adjacent recovery/opening: `674/674`.
- Backend full: `6039/6039`.

This narrows P1-004 replay/recovery determinism and covers the nested battle damage-assignment snapshot fields. Project remains **NOT READY**.
