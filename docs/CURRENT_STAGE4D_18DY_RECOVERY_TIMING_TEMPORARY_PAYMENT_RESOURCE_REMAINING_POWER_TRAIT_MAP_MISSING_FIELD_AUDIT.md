# Stage 4D-18DY Recovery Timing Temporary Payment Resource Remaining Power Trait Map Missing-Field Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `temporaryPaymentResources[]` same-key missing `remainingPowerByTrait` drift under resource-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerTraitMapMissingFieldWithCountMismatch` coverage builds an authoritative temporary payment resource from real `MatchState` source-object state with `generatedPowerByTrait: { blue: 2 }` and `remainingPowerByTrait: { blue: 1 }`. It keeps the emitted spectator payload keyed to `temp-payment-resource-1`, removes only `remainingPowerByTrait`, and appends `temp-payment-resource-extra` to force count mismatch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key nested trait-map diagnostics:

- `remaining power trait map is required`
- keyed authoritative remaining-power traits mismatch for `temp-payment-resource-1`
- unknown extra temporary payment resource id `temp-payment-resource-extra`
- resource-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new remaining-power trait-map missing-field test `1/1`
- focused `TemporaryPaymentResource` filter `124/124`
- focused recovery filter `1157/1157`
- adjacent recovery/official-opening/Postgres recovery-store filter `1738/1738`
- backend full `7103/7103`
- touched-file scoped whitespace format

Mechanical validation passed:

- `git diff --check`
- anchored conflict-marker scan over `docs`, `tests`, and `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- path typo scan

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Project remains **NOT READY**.
