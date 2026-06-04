# Stage 4D-18EG Recovery Timing Temporary Payment Resource Payment-Only Missing Field Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `temporaryPaymentResources[]` same-key `paymentOnly` missing-field drift under resource-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedPaymentOnlyMissingFieldWithCountMismatch` coverage builds an authoritative temporary payment resource from real `MatchState` source-object state with `paymentOnly: true`, `allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind]`, `generatedPowerByTrait: { blue: 2 }` and `remainingPowerByTrait: { blue: 1 }`. It keeps the emitted spectator payload keyed to `temp-payment-resource-1`, removes `paymentOnly`, and appends `temp-payment-resource-extra` to force count mismatch.

This slice locks both the same-payload required validation and keyed authoritative payment-only mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key payment-only diagnostics:

- payment-only flag is required
- keyed authoritative payment-only flag mismatch for `temp-payment-resource-1`
- unknown extra temporary payment resource id `temp-payment-resource-extra`
- resource-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new payment-only missing-field test `1/1`
- focused `TemporaryPaymentResource` filter `132/132`
- focused recovery filter `1165/1165`
- adjacent recovery/official-opening/Postgres recovery-store filter `1746/1746`
- backend full `7111/7111`
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
