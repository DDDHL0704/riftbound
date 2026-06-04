# Stage 4D-18DS Recovery Timing Temporary Payment Resource Remaining Power Negative Value Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedRemainingPowerNegativeValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps the emitted spectator payload keyed to `temp-payment-resource-1`, changes the emitted `remainingPower` from authoritative `1` to negative value `-1`, then appends `temp-payment-resource-extra` to force resource-count mismatch.

It proves validation still emits:

- remaining-power negative-value diagnostics for `temp-payment-resource-1`;
- keyed authoritative remaining-power value mismatch diagnostics for `temp-payment-resource-1`;
- unknown extra-resource diagnostics;
- resource-count mismatch diagnostics.

This closes the focused remaining-power negative-value variant, keeping keyed authoritative remaining-power validation independently locked for spectator timing temporary-payment-resource payloads when the spectator value is readable but invalid because it is negative.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `118/118`.
- Focused recovery filter: `1151/1151`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1732/1732`.
- Backend full: `7097/7097`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
