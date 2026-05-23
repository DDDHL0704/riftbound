# Stage 4D-17AG Recovery Spectator Replay Snapshot Turn-Number Validation Audit

2026-05-24 Stage 4D-17AG recovery spectator replay snapshot turn-number validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot payload `TurnNumber` disagrees with the authoritative final state `TurnNumber`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotTurnNumberMismatch`

Behavior proved:

- Corrupted spectator replay snapshot turn-number metadata is rejected before payload/authoritative turn drift can pass recovery validation.

Validation:

- Focused: `59/59`.
- Adjacent recovery/opening: `640/640`.
- Backend full: `6005/6005`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
