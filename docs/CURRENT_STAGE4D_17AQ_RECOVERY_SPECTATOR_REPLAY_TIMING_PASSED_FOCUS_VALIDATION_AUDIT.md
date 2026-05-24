# Stage 4D-17AQ Recovery Spectator Replay Timing Passed-Focus Validation Audit

2026-05-24 Stage 4D-17AQ recovery spectator replay timing passed-focus validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `passedFocusPlayerIds` list disagrees with the authoritative final state `PassedFocusPlayerIds`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingPassedFocusPlayersMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing passed-focus metadata is rejected before spectator timing/authoritative passed-focus drift can pass recovery validation.

Validation:
- Focused: `69/69`.
- Adjacent recovery/opening: `650/650`.
- Backend full: `6015/6015`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
