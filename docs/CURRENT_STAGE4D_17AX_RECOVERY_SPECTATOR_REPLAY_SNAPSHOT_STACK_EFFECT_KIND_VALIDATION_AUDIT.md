# Stage 4D-17AX Recovery Spectator Replay Snapshot Stack Effect-Kind Validation Audit

2026-05-24 Stage 4D-17AX recovery spectator replay snapshot stack effect-kind validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `Stack` effect kinds disagree with authoritative final state `StackItems` effect kinds.
- The existing stack string extraction helper is reused for dictionary and JSON object snapshot entries so persisted recovery frames get the same validation.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackEffectKindsMismatch`

Behavior proved:
- Corrupted spectator replay snapshot stack effect metadata is rejected before spectator snapshot / authoritative stack effect-kind drift can pass recovery validation.

Validation:
- Focused: `76/76`.
- Adjacent recovery/opening: `657/657`.
- Backend full: `6022/6022`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
