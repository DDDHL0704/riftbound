# Stage 4D-17AF Recovery Spectator Replay Snapshot Tick Validation Audit

2026-05-24 Stage 4D-17AF recovery spectator replay snapshot tick validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot payload `Tick` disagrees with the containing replay frame `Tick`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotTickMismatch`

Behavior proved:

- Corrupted spectator replay snapshot tick metadata is rejected before payload/frame tick drift can pass recovery validation.

Validation:

- Focused: `58/58`.
- Adjacent recovery/opening: `639/639`.
- Backend full: `6004/6004`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
