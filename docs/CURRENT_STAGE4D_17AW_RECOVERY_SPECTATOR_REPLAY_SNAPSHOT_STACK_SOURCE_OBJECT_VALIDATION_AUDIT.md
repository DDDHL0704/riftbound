# Stage 4D-17AW Recovery Spectator Replay Snapshot Stack Source-Object Validation Audit

2026-05-24 Stage 4D-17AW recovery spectator replay snapshot stack source-object validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` source object ids disagree with authoritative final state `StackItems` source object ids.
- The existing stack string extraction helper is reused for dictionary and JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackSourceObjectIdsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack source metadata is rejected before spectator snapshot / authoritative stack source-object drift can pass recovery validation.

Validation:
- Focused: `75/75`.
- Adjacent recovery/opening: `656/656`.
- Backend full: `6021/6021`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
