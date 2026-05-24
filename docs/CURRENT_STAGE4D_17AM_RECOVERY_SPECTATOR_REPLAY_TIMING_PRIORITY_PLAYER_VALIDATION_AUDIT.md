# Stage 4D-17AM Recovery Spectator Replay Timing Priority-Player Validation Audit

2026-05-24 Stage 4D-17AM recovery spectator replay timing priority-player validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot timing `priorityPlayerId` disagrees with the authoritative final state `PriorityPlayerId`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingPriorityPlayerMismatch`

Behavior proved:
- Corrupted spectator replay snapshot timing priority-player metadata is rejected before spectator timing/authoritative priority-player drift can pass recovery validation.

Validation:
- Focused: `65/65`.
- Adjacent recovery/opening: `646/646`.
- Backend full: `6011/6011`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
