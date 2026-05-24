# Stage 4D-17BN Recovery Spectator Replay Timing Battle Damage-Assignment Lethal-Threshold Validation Audit

2026-05-24 Stage 4D-17BN recovery spectator replay timing battle damage-assignment lethal-threshold validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["lethalDamageThreshold"]` disagrees with authoritative `ResolutionResult.BattleLethalDamageThresholdFor(state, battle)`.
- The lethal-threshold check only applies after `isPending`, `phase`, identity fields, `damagePool`, `legalTargets` and `existingDamage` match the authoritative pending battle damage-assignment window.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLethalThresholdMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment lethal-threshold metadata is rejected before spectator snapshot / authoritative battle lethal-threshold drift can pass recovery validation.

Validation:
- Focused: `92/92`.
- Adjacent recovery/opening: `673/673`.
- Backend full: `6038/6038`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
