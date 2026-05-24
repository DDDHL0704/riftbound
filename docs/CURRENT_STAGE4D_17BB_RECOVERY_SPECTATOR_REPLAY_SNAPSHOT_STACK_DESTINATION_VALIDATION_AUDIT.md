# Stage 4D-17BB Recovery Spectator Replay Snapshot Stack Destination Validation Audit

2026-05-24 Stage 4D-17BB recovery spectator replay snapshot stack destination validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` destinations disagree with authoritative final state `StackItems` destinations.
- Missing optional destination keys are treated as empty destinations, matching spectator snapshot emission.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackDestinationsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack destination metadata is rejected before spectator snapshot / authoritative stack destination drift can pass recovery validation.

Validation:
- Focused: `80/80`.
- Adjacent recovery/opening: `661/661`.
- Backend full: `6026/6026`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
