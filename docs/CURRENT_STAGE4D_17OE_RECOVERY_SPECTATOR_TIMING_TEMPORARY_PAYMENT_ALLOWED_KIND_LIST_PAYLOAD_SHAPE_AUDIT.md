# Stage 4D-17OE Recovery Spectator Timing Temporary Payment Allowed Kind List Payload Shape Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in spectator replay-frame timing temporary-payment-resource validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSpectatorReplayFrame`: object-shaped `Timing["temporaryPaymentResources"][]` entries now emit an explicit nested string-list payload-shape diagnostic for malformed non-list `allowedPaymentKinds` fields before downstream value validation and authoritative parity extraction consume that field.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `414/414`
- Adjacent recovery/opening/store-smoke tests: `995/995`
- Backend full: `6360/6360`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
