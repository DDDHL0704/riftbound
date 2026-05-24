# Stage 4D-17AY Recovery Spectator Replay Snapshot Stack Card-Number Validation Audit

2026-05-24 Stage 4D-17AY recovery spectator replay snapshot stack card-number validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` card numbers disagree with authoritative final state `StackItems` card numbers.
- The existing stack string extraction helper is reused for dictionary and JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackCardNumbersMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack card metadata is rejected before spectator snapshot / authoritative stack card-number drift can pass recovery validation.

Validation:
- Focused: `77/77`.
- Adjacent recovery/opening: `658/658`.
- Backend full: `6023/6023`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
