2026-05-24 Stage 4D-17Y recovery snapshot players validation accepted.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change:
- `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered snapshots whose `Players` payload is null.
- `MatchRecoveryValidator.ExtractSeats` now returns an empty seat map for malformed null player collections instead of throwing during agreement or authoritative-state comparison.
- This is recovery frame validation only; command resolution, protocol shape, prompts, frontend and matrix JSON are unchanged.

Coverage added:
- `RecoveryValidatorRejectsMissingSnapshotPlayers`

Behavior proved:
- Corrupted snapshot player payload metadata is rejected before player-view agreement or authoritative-state comparison can extract seats from malformed snapshot data.

Validation:
- Focused: `51/51`.
- Adjacent recovery/opening: `632/632`.
- Backend full: `5997/5997`.

This narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
