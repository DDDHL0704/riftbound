# Stage 4D-17AN Recovery Spectator Replay Timing Focus-Player Validation Audit

2026-05-24 Stage 4D-17AN recovery spectator replay timing focus-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `focusPlayerId` disagrees with the authoritative final state `FocusPlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingFocusPlayerMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing focus-player metadata is rejected before spectator timing/authoritative focus-player drift can pass recovery validation.

Validation:
- Focused: `66/66`.
- Adjacent recovery/opening: `647/647`.
- Backend full: `6012/6012`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
