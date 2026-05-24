# Stage 4D-17BL Recovery Spectator Replay Timing Battle Damage-Assignment Legal-Target Validation Audit

2026-05-24 Stage 4D-17BL recovery spectator replay timing battle damage-assignment legal-target validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["legalTargets"]` disagrees with authoritative `ResolutionResult.BattleDamageLegalTargetsFor(battle)`.
- The legal-target check only applies after `isPending`, `phase`, identity fields and `damagePool` match the authoritative pending battle damage-assignment window.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Existing damage, lethal thresholds and required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentLegalTargetsMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment legal-target metadata is rejected before spectator snapshot / authoritative battle legal-target drift can pass recovery validation.

Validation:
- Focused: `90/90`.
- Adjacent recovery/opening: `671/671`.
- Backend full: `6036/6036`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
