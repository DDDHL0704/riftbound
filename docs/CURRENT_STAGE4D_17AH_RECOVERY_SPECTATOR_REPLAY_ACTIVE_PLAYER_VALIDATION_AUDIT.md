# Stage 4D-17AH Recovery Spectator Replay Active-Player Validation Audit

2026-05-24 Stage 4D-17AH recovery spectator replay active-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot payload `ActivePlayerId` disagrees with the authoritative final state `ActivePlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotActivePlayerMismatch`

Behavior proved:
- Corrupted spectator replay snapshot active-player metadata is rejected before payload/authoritative active-player drift can pass recovery validation.

Validation:
- Focused: `60/60`.
- Adjacent recovery/opening: `641/641`.
- Backend full: `6006/6006`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
