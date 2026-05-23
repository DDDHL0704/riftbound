# Stage 4D-17AB Recovery Spectator Replay Players Validation Audit

2026-05-24 Stage 4D-17AB recovery spectator replay players validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Players` payload is null.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshotPlayers`

Behavior proved:

- Corrupted spectator replay snapshot player metadata is rejected before malformed spectator-state data can pass recovery validation.

Validation:

- Focused: `54/54`.
- Adjacent recovery/opening: `635/635`.
- Backend full: `6000/6000`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
