# Stage 4D-17BE Recovery Spectator Replay Timing Turn-Window Validation Audit

2026-05-24 Stage 4D-17BE recovery spectator replay timing turn-window validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Timing["turnWindow"]` composite view disagrees with authoritative final state `TurnWindow`.
- The matched fields are `state`, `isSpellDuel`, `isClosed`, `hasStack` and `actingPlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingTurnWindowMismatch`

Behavior proved:
- Corrupted spectator replay timing turn-window metadata is rejected before spectator snapshot / authoritative turn-window drift can pass recovery validation.

Validation:
- Focused: `83/83`.
- Adjacent recovery/opening: `664/664`.
- Backend full: `6029/6029`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
