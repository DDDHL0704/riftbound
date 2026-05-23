# Stage 4D-17AD Recovery Spectator Replay Stack Validation Audit

2026-05-24 Stage 4D-17AD recovery spectator replay stack validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Stack` payload is null.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshotStack`

Behavior proved:

- Corrupted spectator replay snapshot stack metadata is rejected before malformed spectator stack-state data can pass recovery validation.

Validation:

- Focused: `56/56`.
- Adjacent recovery/opening: `637/637`.
- Backend full: `6002/6002`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
