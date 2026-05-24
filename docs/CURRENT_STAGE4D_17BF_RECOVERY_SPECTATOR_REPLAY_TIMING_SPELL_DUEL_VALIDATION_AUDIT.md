# Stage 4D-17BF Recovery Spectator Replay Timing Spell-Duel Validation Audit

2026-05-24 Stage 4D-17BF recovery spectator replay timing spell-duel validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot `Timing["spellDuel"]` composite view disagrees with authoritative final state `SpellDuelState`.
- The matched fields are `isActive`, `isClosed`, `spellDuelId`, `battlefieldObjectId`, `focusPlayerId`, `passedFocusPlayerIds`, `stackItemIds` and `stackControllerIds`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplayTimingSpellDuelMismatch`

Behavior proved:
- Corrupted spectator replay timing spell-duel metadata is rejected before spectator snapshot / authoritative spell-duel drift can pass recovery validation.

Validation:
- Focused: `84/84`.
- Adjacent recovery/opening: `665/665`.
- Backend full: `6030/6030`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
