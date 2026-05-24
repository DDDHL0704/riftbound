# Stage 4D-17AT Recovery Spectator Replay Snapshot Stack-Count Validation Audit

2026-05-24 Stage 4D-17AT recovery spectator replay snapshot stack-count validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` item count disagrees with the authoritative final state `StackItems` count.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackCountMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack-count metadata is rejected before spectator snapshot / authoritative stack drift can pass recovery validation.

Validation:
- Focused: `72/72`.
- Adjacent recovery/opening: `653/653`.
- Backend full: `6018/6018`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
