# Stage 4D-17UQ Recovery Spectator Object ID Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame snapshot player object identity payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was the missing/null required-field analogue after 17UN: unreadable-present visible-object and extra-object `objectId` values already emitted authoritative object-id or payload-key mismatch diagnostics, but missing or null `objectId` could still stop at the required-field diagnostic.

## Runtime Change

`MatchRecoveryValidator` now treats missing or null visible-object `objectId` values as authoritative object-id mismatches, because visible object entries are keyed by the authoritative spectator-visible object id.

For extra objects, missing or null `objectId` values now emit the same payload-key mismatch diagnostic as unreadable-present object ids, because the extra object payload must identify the same object key that introduced it.

Readable value drift and unreadable-present drift keep the existing diagnostic wording.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerObjectRequiredObjectIdParity`.

The visible-object test mutates two spectator-visible battlefield object payloads: one omits `objectId`, and one carries `objectId` as null. It asserts both objects emit required-field diagnostics and authoritative object-id mismatch diagnostics.

Added `RecoveryValidatorRejectsSpectatorReplaySnapshotPlayerExtraObjectRequiredObjectIdParityWithVisibilityMismatch`.

The extra-object test forges two hidden hand objects into the spectator snapshot: one omits `objectId`, and one carries `objectId` as null. It asserts both objects emit required-field diagnostics, payload-key mismatch diagnostics and extra-object visibility mismatches.

## Validation

- Focused object-id required parity tests: `2/2`.
- Focused spectator player object / extra-object filter: `28/28`.
- Focused `MatchRecoveryTests` filter: `672/672`.
- Adjacent recovery/opening/store-smoke filter: `1253/1253`.
- Backend full: `6618/6618`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Touched-file scoped `dotnet format --verify-no-changes --no-restore --include src/Riftbound.Engine/MatchRecovery.cs tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`: passed.
- Full `dotnet format --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice in `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
