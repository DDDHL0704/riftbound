# Stage 4D-17AI Recovery Spectator Replay Seat Validation Audit

2026-05-24 Stage 4D-17AI recovery spectator replay seat validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot player seat map disagrees with the authoritative final state seats.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotSeatMismatch`

Behavior proved:
- Corrupted spectator replay snapshot seat metadata is rejected before payload/authoritative seat drift can pass recovery validation.

Validation:
- Focused: `61/61`.
- Adjacent recovery/opening: `642/642`.
- Backend full: `6007/6007`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
