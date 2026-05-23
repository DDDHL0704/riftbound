# Stage 4D-17AE Recovery Spectator Replay Turn-State Validation Audit

2026-05-24 Stage 4D-17AE recovery spectator replay turn-state validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `TurnState` payload is blank.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshotTurnState`

Behavior proved:

- Corrupted spectator replay snapshot turn-state metadata is rejected before malformed spectator turn-state data can pass recovery validation.

Validation:

- Focused: `57/57`.
- Adjacent recovery/opening: `638/638`.
- Backend full: `6003/6003`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
