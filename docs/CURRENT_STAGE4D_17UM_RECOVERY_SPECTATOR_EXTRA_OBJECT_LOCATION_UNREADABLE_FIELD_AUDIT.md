# Stage 4D-17UM Recovery Spectator Extra Object Location Unreadable Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player extra-object `location` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the extra-object analogue of 17UL: when an object is not visible in the authoritative spectator view but still appears in a spectator snapshot, readable location drift already emitted authoritative object-location mismatch diagnostics. Present but unreadable `location.playerId`, `location.zone` and authoritative-present `location.battlefieldObjectId` could still stop at required/shape diagnostics in the extra-object path.

## Runtime Change

`MatchRecoveryValidator` now records whether extra-object location fields are present and non-null before parsing them. When an authoritative object location exists, unreadable-present `playerId` and `zone` now still emit the corresponding authoritative object-location mismatch diagnostics. Unreadable-present `battlefieldObjectId` also emits the authoritative battlefield-object-id mismatch diagnostic when the authoritative location carries a non-empty battlefield object id.

Readable value drift keeps the existing diagnostic wording. Missing/null field behavior is unchanged. Optional battlefield object id parity stays conservative for authoritative-empty locations.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectLocationUnreadableParityWithVisibilityMismatch`.

The test forges an extra object into the spectator snapshot while the authoritative spectator view does not expose that object, then gives the forged object's `location` unreadable-present `playerId`, `zone` and `battlefieldObjectId` values against an authoritative object location with a non-empty battlefield object id.

Expected diagnostics include:

- location player id authoritative mismatch;
- location zone authoritative mismatch;
- location battlefield object id authoritative mismatch;
- the existing extra-object visibility mismatch.

## Validation

- Focused extra-object unreadable location parity test: `1/1`.
- Focused extra-object location filter: `5/5`.
- Focused `MatchRecoveryTests` filter: `666/666`.
- Adjacent recovery/opening/store-smoke filter: `1247/1247`.
- Backend full: `6612/6612`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
