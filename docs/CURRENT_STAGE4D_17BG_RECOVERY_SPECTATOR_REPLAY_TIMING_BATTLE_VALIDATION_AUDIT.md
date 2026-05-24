# Stage 4D-17BG Recovery Spectator Replay Timing Battle Validation Audit

2026-05-24 Stage 4D-17BG recovery spectator replay timing battle validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose top-level spectator snapshot `Timing["battle"]` view disagrees with authoritative final state `BattleState`.
- The matched fields are `isActive`, `battleId`, `battlefieldObjectId`, `attackerObjectIds`, `defenderObjectIds` and `participantControllerIds`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.
- Deeper `battle.damageAssignment` validation remains outside this slice.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingBattleMismatch`

Behavior proved:
- Corrupted spectator replay timing battle metadata is rejected before spectator snapshot / authoritative battle-state drift can pass recovery validation.

Validation:
- Focused: `85/85`.
- Adjacent recovery/opening: `666/666`.
- Backend full: `6031/6031`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
