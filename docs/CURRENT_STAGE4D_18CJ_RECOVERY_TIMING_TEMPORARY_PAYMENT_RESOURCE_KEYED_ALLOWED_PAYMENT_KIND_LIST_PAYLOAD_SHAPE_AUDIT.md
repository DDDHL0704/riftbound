# Stage 4D-18CJ Recovery Timing Temporary Payment Resource Keyed Allowed Payment Kind List Payload Shape Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedAllowedPaymentKindListPayloadShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps a same-key spectator payload keyed to `temp-payment-resource-1`, mutates that payload so `allowedPaymentKinds` is an object payload instead of a string list, then appends `temp-payment-resource-extra` to force resource-count mismatch while preserving same-key authoritative lookup.

It proves validation still emits:

- allowed-payment-kind list payload-shape and invalid-list diagnostics;
- same-key authoritative allowed-payment-kind collection mismatch diagnostics from the malformed keyed payload;
- unknown extra-resource and resource-count mismatch diagnostics.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `83/83`.
- Focused recovery filter: `1116/1116`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1697/1697`.
- Backend full: `7062/7062`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
