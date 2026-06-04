# Stage 4D-18EQ Recovery Timing Temporary Payment Resource Created-Tick Shape Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `temporaryPaymentResources[]` same-key `createdTick` payload-shape drift under resource-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedCreatedTickShapeWithCountMismatch` coverage builds an authoritative temporary payment resource from real `MatchState` source-object state with `paymentOnly: true`, `allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind]`, `generatedPowerByTrait: { blue: 2 }` and `remainingPowerByTrait: { blue: 1 }`. It keeps the emitted spectator payload keyed to `temp-payment-resource-1`, changes `createdTick` to an unreadable array shape, and appends `temp-payment-resource-extra` to force count mismatch.

This slice locks the same-payload created-tick shape diagnostic and keyed authoritative created-tick mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key created-tick shape diagnostics:

- created-tick invalid diagnostic for the unreadable array payload
- keyed authoritative created-tick mismatch for `temp-payment-resource-1`
- unknown extra temporary payment resource id `temp-payment-resource-extra`
- resource-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new created-tick shape test `1/1`
- focused `TemporaryPaymentResource` filter `142/142`
- focused recovery filter `1175/1175`
- adjacent recovery/official-opening/Postgres recovery-store filter `1756/1756`
- backend full `7121/7121`
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
