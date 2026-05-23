2026-05-24 Stage 4D-17K recovery event tick/order validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateEvents` now receives recovery frame `currentTick`.
- Recovered events reject negative `Tick`.
- Recovered events reject negative `Order`.
- Recovered events reject `Tick` after recovery `currentTick` when available.
- Recovered event ticks must not move backwards in frame sequence order.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsInvalidRecoveredEventTickAndOrder`

Behavior proved:
- Corrupted event tick/order metadata is rejected before replay restoration.

Validation:
- Focused: `37/37`.
- Adjacent recovery/opening: `618/618`.
- Backend full: `5983/5983`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
