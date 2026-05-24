# Stage 4D-17BJ Recovery Spectator Replay Timing Battle Damage-Assignment Identity Validation Audit

2026-05-24 Stage 4D-17BJ recovery spectator replay timing battle damage-assignment identity validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects pending spectator replay frames whose nested spectator snapshot `Timing["battle"]["damageAssignment"]` battle id, battlefield id or assigning player id disagrees with authoritative battle damage-assignment identity.
- The identity checks only apply after `isPending` and `phase` match the authoritative pending battle damage-assignment window.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Damage pool, legal targets, existing damage, lethal thresholds and required assignments remain outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentIdentityMismatch`

Behavior proved:
- Corrupted pending spectator replay timing battle damage-assignment identity metadata is rejected before spectator snapshot / authoritative battle damage-assignment drift can pass recovery validation.

Validation:
- Focused: `88/88`.
- Adjacent recovery/opening: `669/669`.
- Backend full: `6034/6034`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
