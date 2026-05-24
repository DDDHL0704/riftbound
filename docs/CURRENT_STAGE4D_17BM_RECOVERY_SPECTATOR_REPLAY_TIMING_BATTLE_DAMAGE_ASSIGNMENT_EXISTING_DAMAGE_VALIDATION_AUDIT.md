# Stage 4D-17BM Recovery Spectator Replay Timing Battle Damage-Assignment Existing-Damage Validation Audit

2026-05-24 Stage 4D-17BM recovery spectator replay timing battle damage-assignment existing-damage validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["existingDamage"]` disagrees with authoritative `ResolutionResult.BattleExistingDamageFor(state, battle)`.
- The existing-damage check only applies after `isPending`, `phase`, identity fields, `damagePool` and `legalTargets` match the authoritative pending battle damage-assignment window.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Lethal thresholds and required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentExistingDamageMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment existing-damage metadata is rejected before spectator snapshot / authoritative battle existing-damage drift can pass recovery validation.

Validation:
- Focused: `91/91`.
- Adjacent recovery/opening: `672/672`.
- Backend full: `6037/6037`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
