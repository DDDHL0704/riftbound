# Stage 4D-17AV Recovery Spectator Replay Snapshot Stack Controller-Id Validation Audit

2026-05-24 Stage 4D-17AV recovery spectator replay snapshot stack controller-id validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` controller ids disagree with authoritative final state `StackItems` controller ids.
- The existing stack string extraction helper is reused for dictionary and JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackControllerIdsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack controller metadata is rejected before spectator snapshot / authoritative stack controller drift can pass recovery validation.

Validation:
- Focused: `74/74`.
- Adjacent recovery/opening: `655/655`.
- Backend full: `6020/6020`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
