# Stage 4D-17UL Recovery Spectator Object Location Unreadable Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player object `location` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that visible player object location parity already detected readable `playerId`, `zone` and `battlefieldObjectId` drift, but fields that were present and unreadable could stop at required/shape diagnostics without the authoritative object-location mismatch diagnostics.

## Runtime Change

`MatchRecoveryValidator` now records whether spectator replay-frame snapshot player object location fields are present and non-null before parsing them. When `playerId` or `zone` is present but unreadable, validation now still emits the corresponding authoritative object-location mismatch diagnostic. When `battlefieldObjectId` is present but unreadable and the authoritative object location has a non-empty battlefield object id, validation now emits the authoritative battlefield-object-id mismatch diagnostic.

Readable value drift keeps the existing diagnostic wording. Missing/null field behavior is unchanged. Optional battlefield object id parity stays conservative for authoritative-empty locations.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectLocationUnreadableParity`.

The test mutates one spectator-visible battlefield object location with unreadable-present `playerId`, `zone` and `battlefieldObjectId` values while authoritative `ObjectLocationState` remains populated.

Expected diagnostics are authoritative object-location mismatch diagnostics for:

- location player id;
- location zone;
- location battlefield object id.

## Validation

- Focused unreadable location parity test: `1/1`.
- Focused spectator player object location filter: `3/3`.
- Focused `MatchRecoveryTests` filter: `665/665`.
- Adjacent recovery/opening/store-smoke filter: `1246/1246`.
- Backend full: `6611/6611`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
