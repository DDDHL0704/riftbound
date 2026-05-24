# Stage 4D-17AO Recovery Spectator Replay Timing Winner-Player Validation Audit

2026-05-24 Stage 4D-17AO recovery spectator replay timing winner-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `winnerPlayerId` disagrees with the authoritative final state `WinnerPlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingWinnerPlayerMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing winner-player metadata is rejected before spectator timing/authoritative winner-player drift can pass recovery validation.

Validation:
- Focused: `67/67`.
- Adjacent recovery/opening: `648/648`.
- Backend full: `6013/6013`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
