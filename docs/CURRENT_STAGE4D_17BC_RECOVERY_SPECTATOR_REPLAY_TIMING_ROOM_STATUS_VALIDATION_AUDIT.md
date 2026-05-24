# Stage 4D-17BC Recovery Spectator Replay Timing Room-Status Validation Audit

2026-05-24 Stage 4D-17BC recovery spectator replay timing room-status validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Timing["roomStatus"]` disagrees with authoritative final state `Status`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingRoomStatusMismatch`

Behavior proved:
- Corrupted spectator replay timing room-status metadata is rejected before spectator snapshot / authoritative status drift can pass recovery validation.

Validation:
- Focused: `81/81`.
- Adjacent recovery/opening: `662/662`.
- Backend full: `6027/6027`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
