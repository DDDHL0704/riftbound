# Stage 4D-17AK Recovery Spectator Replay Timing-State Validation Audit

2026-05-24 Stage 4D-17AK recovery spectator replay timing-state validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `timingState` disagrees with the authoritative final state `TimingState`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingStateMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing-state metadata is rejected before spectator timing/authoritative timing-state drift can pass recovery validation.

Validation:
- Focused: `63/63`.
- Adjacent recovery/opening: `644/644`.
- Backend full: `6009/6009`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
