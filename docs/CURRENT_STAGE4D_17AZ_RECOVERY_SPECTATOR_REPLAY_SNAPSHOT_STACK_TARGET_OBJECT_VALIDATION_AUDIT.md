# Stage 4D-17AZ Recovery Spectator Replay Snapshot Stack Target-Object Validation Audit

2026-05-24 Stage 4D-17AZ recovery spectator replay snapshot stack target-object validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` target object id lists disagree with authoritative final state `StackItems` target object id lists.
- New stack string-list extraction support reads dictionary and JSON object snapshot entries while preserving each stack item's nested target-list ordering.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackTargetObjectIdsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack target metadata is rejected before spectator snapshot / authoritative stack target-object drift can pass recovery validation.

Validation:
- Focused: `78/78`.
- Adjacent recovery/opening: `659/659`.
- Backend full: `6024/6024`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
