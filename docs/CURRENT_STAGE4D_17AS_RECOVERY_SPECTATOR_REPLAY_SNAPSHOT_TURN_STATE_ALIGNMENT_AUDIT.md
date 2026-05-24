# Stage 4D-17AS Recovery Spectator Replay Snapshot Turn-State Alignment Audit

2026-05-24 Stage 4D-17AS recovery spectator replay snapshot turn-state alignment accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects spectator replay frames whose spectator snapshot top-level `TurnState` disagrees with the authoritative final state `TimingState`.
- This is recovery frame validation only; command resolution, protocol shape, player snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsSpectatorReplaySnapshotTurnStateMismatch`

Behavior proved:
- Corrupted spectator replay snapshot turn-state metadata is rejected before spectator snapshot/authoritative timing-state drift can pass recovery validation.

Validation:
- Focused: `71/71`.
- Adjacent recovery/opening: `652/652`.
- Backend full: `6017/6017`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
