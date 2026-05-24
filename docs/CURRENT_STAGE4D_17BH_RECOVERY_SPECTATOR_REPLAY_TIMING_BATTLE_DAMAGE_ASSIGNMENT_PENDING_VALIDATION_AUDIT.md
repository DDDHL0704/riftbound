# Stage 4D-17BH Recovery Spectator Replay Timing Battle Damage-Assignment Pending Validation Audit

2026-05-24 Stage 4D-17BH recovery spectator replay timing battle damage-assignment pending validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]["isPending"]` disagrees with authoritative `ResolutionResult.HasOpenBattleDamageAssignmentWindow(state)`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Deeper damage-assignment fields remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPendingMismatch`

Behavior proved:
- Corrupted spectator replay timing battle damage-assignment pending metadata is rejected before spectator snapshot / authoritative battle damage-assignment window drift can pass recovery validation.

Validation:
- Focused: `86/86`.
- Adjacent recovery/opening: `667/667`.
- Backend full: `6032/6032`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
