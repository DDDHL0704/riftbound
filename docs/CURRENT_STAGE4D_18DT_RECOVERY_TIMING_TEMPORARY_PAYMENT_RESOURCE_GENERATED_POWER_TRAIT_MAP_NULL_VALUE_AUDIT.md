# Stage 4D-18DT Recovery Timing Temporary Payment Resource Generated Power Trait Map Null Value Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedGeneratedPowerTraitMapNullValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps the emitted spectator payload keyed to `temp-payment-resource-1`, changes the emitted `generatedPowerByTrait` map to `null`, then appends `temp-payment-resource-extra` to force resource-count mismatch.

It proves validation still emits:

- generated-power trait-map required diagnostics for `temp-payment-resource-1`;
- keyed authoritative generated-power trait mismatch diagnostics for `temp-payment-resource-1`;
- unknown extra-resource diagnostics;
- resource-count mismatch diagnostics.

This closes the focused generated-power trait-map null-value variant, keeping keyed authoritative generated-power trait validation independently locked for spectator timing temporary-payment-resource payloads when the spectator trait map is present but null.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `119/119`.
- Focused recovery filter: `1152/1152`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1733/1733`.
- Backend full: `7098/7098`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
