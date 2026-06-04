# Stage 4D-18CH Recovery Timing Temporary Payment Resource Keyed Property Name Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedPropertyNameWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps a RawJson spectator payload keyed to `temp-payment-resource-1`, mutates that same-key payload with duplicate `resourceId`, whitespace-padded `ownerPlayerId` property name and a blank property name, then appends `temp-payment-resource-extra` to force resource-count mismatch while preserving same-key authoritative lookup.

It proves validation still emits:

- temporary-payment-resource item property duplicate, surrounding-whitespace and required-name diagnostics;
- same-key authoritative owner-player mismatch diagnostics from the malformed keyed payload;
- unknown extra-resource and resource-count mismatch diagnostics.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `81/81`.
- Focused recovery filter: `1114/1114`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1695/1695`.
- Backend full: `7060/7060`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
