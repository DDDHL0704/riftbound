# Stage 4D-17AJ Recovery Spectator Replay Timing Phase Validation Audit

2026-05-24 Stage 4D-17AJ recovery spectator replay timing phase validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `phase` disagrees with the authoritative final state `Phase`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingPhaseMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing phase metadata is rejected before spectator timing/authoritative phase drift can pass recovery validation.

Validation:
- Focused: `62/62`.
- Adjacent recovery/opening: `643/643`.
- Backend full: `6008/6008`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
