# Stage 4D-18EC Recovery Timing Temporary Payment Resource Allowed Payment Kind List Missing Field Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `temporaryPaymentResources[]` same-key `allowedPaymentKinds` missing-field drift under resource-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListMissingFieldWithCountMismatch` coverage builds an authoritative temporary payment resource from real `MatchState` source-object state with `allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind]`, `generatedPowerByTrait: { blue: 2 }` and `remainingPowerByTrait: { blue: 1 }`. It keeps the emitted spectator payload keyed to `temp-payment-resource-1`, removes `allowedPaymentKinds`, and appends `temp-payment-resource-extra` to force count mismatch.

This slice intentionally does not claim same-payload required or payload-shape diagnostics for the missing list payload; it locks the keyed authoritative collection mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key allowed-payment-kind collection diagnostics:

- keyed authoritative allowed-payment-kind collection mismatch for `temp-payment-resource-1`
- unknown extra temporary payment resource id `temp-payment-resource-extra`
- resource-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new allowed-payment-kind list missing-field test `1/1`
- focused `TemporaryPaymentResource` filter `128/128`
- focused recovery filter `1161/1161`
- adjacent recovery/official-opening/Postgres recovery-store filter `1742/1742`
- backend full `7107/7107`
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
