# Stage 4D-17AL Recovery Spectator Replay Timing Turn-Player Validation Audit

2026-05-24 Stage 4D-17AL recovery spectator replay timing turn-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `turnPlayerId` disagrees with the authoritative final state `TurnPlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingTurnPlayerMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing turn-player metadata is rejected before spectator timing/authoritative turn-player drift can pass recovery validation.

Validation:
- Focused: `64/64`.
- Adjacent recovery/opening: `645/645`.
- Backend full: `6010/6010`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
