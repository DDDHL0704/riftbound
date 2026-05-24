# Stage 4D-17AP Recovery Spectator Replay Timing Passed-Priority Validation Audit

2026-05-24 Stage 4D-17AP recovery spectator replay timing passed-priority validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `passedPriorityPlayerIds` list disagrees with the authoritative final state `PassedPriorityPlayerIds`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingPassedPriorityPlayersMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing passed-priority metadata is rejected before spectator timing/authoritative passed-priority drift can pass recovery validation.

Validation:
- Focused: `68/68`.
- Adjacent recovery/opening: `649/649`.
- Backend full: `6014/6014`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
