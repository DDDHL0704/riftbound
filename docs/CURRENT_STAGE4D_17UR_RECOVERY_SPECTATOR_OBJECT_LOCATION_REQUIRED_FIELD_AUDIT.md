# Stage 4D-17UR Recovery Spectator Object Location Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player object location payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the missing/null required-field analogue after 17UL and 17UM: unreadable-present visible-object and extra-object `location.playerId` / `location.zone` values already emitted authoritative object-location mismatch diagnostics, but missing or null nested location fields could still stop at the required-field diagnostic.

## Runtime Change

`MatchRecoveryValidator` now treats missing or null visible-object `location.playerId` and `location.zone` values as authoritative object-location mismatches when an expected spectator object location exists.

For extra objects, missing or null `location.playerId` and `location.zone` values now emit the same authoritative object-location mismatch diagnostics as unreadable-present nested fields when an authoritative location exists.

Readable value drift and unreadable-present drift keep the existing diagnostic wording. Whole `location` absence keeps the existing required-field diagnostic and does not introduce a new whole-location mismatch string.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationRequiredFieldParity`.

The visible-object test mutates a spectator-visible battlefield object location so `playerId` is missing and `zone` is null. It asserts the required-field diagnostics and authoritative object-location mismatch diagnostics are both emitted.

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationRequiredFieldParityWithVisibilityMismatch`.

The extra-object test forges a hidden hand object location so `playerId` is null and `zone` is missing. It asserts required-field diagnostics, authoritative object-location mismatch diagnostics and the extra-object visibility mismatch.

## Validation

- Focused location required parity tests: `2/2`.
- Focused spectator player object / extra-object location filter: `10/10`.
- Focused spectator player object / extra-object filter: `30/30`.
- Focused `MatchRecoveryTests` filter: `674/674`.
- Adjacent recovery/opening/store-smoke filter: `1255/1255`.
- Backend full: `6620/6620`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
