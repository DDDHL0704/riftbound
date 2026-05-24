# Stage 4D-17BI Recovery Spectator Replay Timing Battle Damage-Assignment Phase Validation Audit

2026-05-24 Stage 4D-17BI recovery spectator replay timing battle damage-assignment phase validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["phase"]` is not `DAMAGE_ASSIGNMENT`.
- The phase check only applies after `isPending` matches authoritative `ResolutionResult.HasOpenBattleDamageAssignmentWindow(state)`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Battle damage-assignment identity fields, damage pool, legal targets, existing damage, lethal thresholds and required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPhaseMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment phase metadata is rejected before spectator snapshot / authoritative battle damage-assignment drift can pass recovery validation.

Validation:
- Focused: `87/87`.
- Adjacent recovery/opening: `668/668`.
- Backend full: `6033/6033`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
