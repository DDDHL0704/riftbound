# Stage 4D-17AC Recovery Spectator Replay Lanes Validation Audit

2026-05-24 Stage 4D-17AC recovery spectator replay lanes validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Lanes` payload is null.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshotLanes`

Behavior proved:

- Corrupted spectator replay snapshot lane metadata is rejected before malformed spectator battlefield-state data can pass recovery validation.

Validation:

- Focused: `55/55`.
- Adjacent recovery/opening: `636/636`.
- Backend full: `6001/6001`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
