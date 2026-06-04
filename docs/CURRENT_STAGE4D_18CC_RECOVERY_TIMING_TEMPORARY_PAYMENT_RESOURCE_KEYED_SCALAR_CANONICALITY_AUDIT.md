# Stage 4D-18CC Recovery Timing Temporary Payment Resource Keyed Scalar Canonicality Audit

Date: 2026-06-04
Owner: A_MAIN
Status: accepted

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]`.

Runtime changed: no. The implementation change is one targeted `MatchRecoveryTests` regression that locks existing `MatchRecoveryValidator` behavior.

## Coverage Added

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedScalarCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative temporary payment resource from real `MatchState` source-object state, keeps the spectator `resourceId` trim-normalizable to the authoritative key, pads scalar values with surrounding whitespace, and adds `temp-payment-resource-extra` to force resource-count mismatch. It proves validation still emits:

- scalar surrounding-whitespace diagnostics for `resourceId`, `ownerPlayerId`, `sourceObjectId`, `abilityId`, `paymentWindow` and `resourceRestriction`;
- same-key authoritative owner/source/ability/payment-window/restriction mismatch diagnostics for `temp-payment-resource-1`;
- resource-count mismatch diagnostics.

## Validation

- Focused new test: `1/1`.
- Focused `TemporaryPaymentResource` filter: `76/76`.
- Focused recovery filter: `1109/1109`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1690/1690`.
- Backend full: `7055/7055`.
- Touched-file scoped whitespace format: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no hits.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Dotted test-path typo scan: no hits.

Backend full was rerun because this batch touched the MatchRecovery test surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remained locked.

Project remains **NOT READY**. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
