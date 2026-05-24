# Stage 4D-17AU Recovery Spectator Replay Snapshot Stack Item-Id Validation Audit

2026-05-24 Stage 4D-17AU recovery spectator replay snapshot stack item-id validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` item ids disagree with authoritative final state `StackItems` ids.
- `ExtractStackItemIds` reads stack ids from dictionary or JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemIdsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack identity metadata is rejected before spectator snapshot / authoritative stack drift can pass recovery validation.

Validation:
- Focused: `73/73`.
- Adjacent recovery/opening: `654/654`.
- Backend full: `6019/6019`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
