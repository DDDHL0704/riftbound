# Stage 4D-17UN Recovery Spectator Object Id Unreadable Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player object identity payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the object-id analogue after 17UK through 17UM: readable object-id drift already emitted authoritative/object-key mismatch diagnostics. Present but unreadable `objectId` values could still stop at required/shape diagnostics for visible objects and extra objects.

## Runtime Change

`MatchRecoveryValidator` now records whether a player object payload `objectId` is present and non-null before parsing it. When a visible object payload carries an unreadable-present `objectId`, validation now emits the authoritative object-id mismatch diagnostic alongside the required/shape diagnostic.

The extra-object path now applies the same present-but-unreadable check against the payload key. When an extra object payload carries an unreadable-present `objectId`, validation emits the payload-object-id/key mismatch diagnostic alongside the required/shape diagnostic and any existing visibility mismatch.

Readable value drift keeps the existing diagnostic wording. Missing/null `objectId` behavior is unchanged.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectUnreadableObjectIdParity`.

The test mutates a visible spectator snapshot player object so its `objectId` is present but unreadable, then asserts both the required object-id diagnostic and the authoritative object-id mismatch diagnostic are emitted.

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectUnreadableObjectIdParityWithVisibilityMismatch`.

The test forges an extra hidden hand object into the spectator snapshot with unreadable-present `objectId` and visible `isFaceDown=false`, then asserts the required object-id diagnostic, payload-object-id/key mismatch diagnostic and extra-object visibility mismatch all remain present.

## Validation

- Focused object-id unreadable parity tests: `2/2`.
- Focused spectator player object / extra-object filter: `24/24`.
- Focused `MatchRecoveryTests` filter: `668/668`.
- Adjacent recovery/opening/store-smoke filter: `1249/1249`.
- Backend full: `6614/6614`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
