# Stage 4D-18DJ Recovery Timing Temporary Payment Resource Generated Power Null Value Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedGeneratedPowerNullValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps the emitted spectator payload keyed to `temp-payment-resource-1`, changes the emitted `generatedPower` to `null`, then appends `temp-payment-resource-extra` to force resource-count mismatch.

It proves validation still emits:

- generated-power required numeric diagnostics;
- keyed authoritative generated-power mismatch diagnostics for `temp-payment-resource-1`;
- unknown extra-resource diagnostics;
- resource-count mismatch diagnostics.

This closes the focused generated-power null-value variant outside the broader required-field tests, keeping required numeric payload validation and keyed authoritative validation independently locked for the required `generatedPower` field.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `109/109`.
- Focused recovery filter: `1142/1142`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1723/1723`.
- Backend full: `7088/7088`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
