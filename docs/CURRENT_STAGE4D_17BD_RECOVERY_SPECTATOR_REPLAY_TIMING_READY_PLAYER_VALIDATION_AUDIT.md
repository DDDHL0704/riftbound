# Stage 4D-17BD Recovery Spectator Replay Timing Ready-Player Validation Audit

2026-05-24 Stage 4D-17BD recovery spectator replay timing ready-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Timing["readyPlayerIds"]` disagrees with authoritative final state `ReadyPlayerIds`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingReadyPlayersMismatch`

Behavior proved:
- Corrupted spectator replay timing ready-player metadata is rejected before spectator snapshot / authoritative ready-player drift can pass recovery validation.

Validation:
- Focused: `82/82`.
- Adjacent recovery/opening: `663/663`.
- Backend full: `6028/6028`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
