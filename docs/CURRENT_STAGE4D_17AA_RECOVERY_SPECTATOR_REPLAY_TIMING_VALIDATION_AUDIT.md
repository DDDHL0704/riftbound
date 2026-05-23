# Stage 4D-17AA Recovery Spectator Replay Timing Validation Audit

2026-05-24 Stage 4D-17AA recovery spectator replay timing validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Timing` payload is null.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:

- `RecoveryValidatorRejectsMissingSpectatorReplaySnapshotTiming`

Behavior proved:

- Corrupted spectator replay snapshot timing metadata is rejected before random-state redaction checks dereference malformed replay-frame data.

Validation:

- Focused: `53/53`.
- Adjacent recovery/opening: `634/634`.
- Backend full: `5999/5999`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
