2026-05-24 Stage 4D-17X recovery player-view snapshot validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player views with missing snapshot payloads.
- `ValidatePlayerViewAgreement` and `ValidateAuthoritativeState` skip malformed null snapshots after the explicit recovery error is recorded, avoiding recovery validation crashes.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsMissingPlayerViewSnapshot`

Behavior proved:
- Corrupted player-view snapshot payload metadata is rejected before replay-point restoration, player-view agreement, or authoritative-state comparison can dereference malformed data.

Validation:
- Focused: `50/50`.
- Adjacent recovery/opening: `631/631`.
- Backend full: `5996/5996`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
