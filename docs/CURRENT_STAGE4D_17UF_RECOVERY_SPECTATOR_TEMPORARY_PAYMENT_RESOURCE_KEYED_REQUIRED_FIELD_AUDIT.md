# Stage 4D-17UF Recovery Spectator Temporary Payment Resource Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that keyed authoritative temporary-payment-resource diagnostics only ran when the same-resource spectator field was readable. If the spectator temporary-payment-resource list had a count mismatch, broad ordered parity was skipped; if a same-resource payload then omitted or made required fields unreadable, the validator emitted generic shape diagnostics but not the keyed authoritative mismatch diagnostics that identify the authoritative same-resource field drift.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-resource spectator replay-frame timing `temporaryPaymentResources[]` required and authoritative-present fields even when a field is missing or unreadable.

The helper coverage includes:

- `ownerPlayerId`;
- `sourceObjectId`;
- `abilityId`;
- `paymentWindow`;
- `generatedPower`;
- `remainingPower`;
- `generatedPowerByTrait`;
- `remainingPowerByTrait`;
- `allowedPaymentKinds`;
- `paymentOnly`;
- `resourceRestriction`;
- `createdTick`.

Readable value drift keeps the existing diagnostic wording. Missing/unreadable required or authoritative-present fields now also emit keyed authoritative mismatch wording for the matching `resourceId`.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates a spectator replay-frame timing temporary-payment-resource payload with:

- one authoritative resource `temp-payment-resource-1`;
- `ownerPlayerId`, `sourceObjectId`, `paymentWindow`, `remainingPower`, `generatedPowerByTrait`, `paymentOnly` and `resourceRestriction` removed from the same-key spectator payload;
- `abilityId`, `generatedPower`, `remainingPowerByTrait`, `allowedPaymentKinds` and `createdTick` changed to unreadable payload shapes;
- an extra temporary payment resource added so `temporaryPaymentResources` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the malformed same-resource payload;
- keyed authoritative mismatch diagnostics for the same-resource fields listed above;
- temporary-payment-resource count mismatch.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `69/69`.
- Focused `MatchRecoveryTests` filter: `659/659`.
- Adjacent recovery/opening/store-smoke filter: `1240/1240`.
- Backend full: `6605/6605`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs`, `MatchSession.cs`, `ConformanceFixtureRunnerTests.cs`, `GameHubJoinTests.cs`, `PaymentEngineCoverageAuditTests.cs` and `TriggerPaymentTests.cs`; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
