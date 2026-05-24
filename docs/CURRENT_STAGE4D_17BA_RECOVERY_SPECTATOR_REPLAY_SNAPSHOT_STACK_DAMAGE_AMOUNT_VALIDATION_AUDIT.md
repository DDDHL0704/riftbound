# Stage 4D-17BA Recovery Spectator Replay Snapshot Stack Damage-Amount Validation Audit

2026-05-24 Stage 4D-17BA recovery spectator replay snapshot stack damage-amount validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` damage amounts disagree with authoritative final state `StackItems` damage amounts.
- New stack integer extraction support reads dictionary and JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackDamageAmountsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack damage metadata is rejected before spectator snapshot / authoritative stack damage-amount drift can pass recovery validation.

Validation:
- Focused: `79/79`.
- Adjacent recovery/opening: `660/660`.
- Backend full: `6025/6025`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
