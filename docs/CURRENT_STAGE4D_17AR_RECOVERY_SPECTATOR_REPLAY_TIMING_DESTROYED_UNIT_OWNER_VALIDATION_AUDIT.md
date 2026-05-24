# Stage 4D-17AR Recovery Spectator Replay Timing Destroyed-Unit-Owner Validation Audit

2026-05-24 Stage 4D-17AR recovery spectator replay timing destroyed-unit-owner validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `destroyedUnitOwnerIdsThisTurn` list disagrees with the authoritative final state `DestroyedUnitOwnerIdsThisTurn`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingDestroyedUnitOwnersMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing destroyed-unit-owner metadata is rejected before spectator timing/authoritative destroyed-unit-owner drift can pass recovery validation.

Validation:
- Focused: `70/70`.
- Adjacent recovery/opening: `651/651`.
- Backend full: `6016/6016`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
