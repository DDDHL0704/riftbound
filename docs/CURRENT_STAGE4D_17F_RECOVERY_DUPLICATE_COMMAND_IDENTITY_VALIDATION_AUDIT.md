2026-05-24 Stage 4D-17F recovery duplicate command identity validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidateCommands` now tracks recovered command identity per `(PlayerId, ClientIntentId)`.
- A recovery frame containing the same player/client-intent pair more than once now reports a recovery validation error.
- The same client intent id remains valid for different players, matching the live session idempotency cache boundary.
- This is recovery frame validation only; command resolution, protocol shape, snapshots/prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsDuplicateRecoveredCommandIntentForSamePlayer`
- `RecoveryValidatorAllowsSameRecoveredCommandIntentForDifferentPlayers`

Behavior proved:
- Corrupted duplicate recovered command identity for one player is rejected before action-log replay restoration.
- Cross-player client intent id reuse is not treated as duplicate identity.

Validation:
- Focused: `32/32`.
- Adjacent recovery/opening: `613/613`.
- Backend full: `5978/5978`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
