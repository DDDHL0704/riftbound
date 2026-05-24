# Stage 4D-17BK Recovery Spectator Replay Timing Battle Damage-Assignment Damage-Pool Validation Audit

2026-05-24 Stage 4D-17BK recovery spectator replay timing battle damage-assignment damage-pool validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["damagePool"]` disagrees with authoritative `ResolutionResult.BattleDamagePoolFor(state, battle)`.
- The damage-pool check only applies after `isPending`, `phase` and identity fields match the authoritative pending battle damage-assignment window.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Legal targets, existing damage, lethal thresholds and required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentDamagePoolMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment damage-pool metadata is rejected before spectator snapshot / authoritative battle damage-pool drift can pass recovery validation.

Validation:
- Focused: `89/89`.
- Adjacent recovery/opening: `670/670`.
- Backend full: `6035/6035`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
