# Stage 4D-17PU Recovery Timing Continuous Effect Static Aura Modifier Scalar Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects `STATIC_AURA` effects carrying readable non-null integer `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower` or `resultingPower` values. This matches current builder output because those modifier scalars are emitted for tracked temporary `POWER_MODIFIER` ledger entries, while current static-aura builders never set them. Absent/null static-aura modifier scalars keep existing optional-field compatibility, and malformed values keep the existing optional-int diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura modifier-scalar diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura modifier-scalar consistency tests: `2/2`
- Focused recovery tests: `482/482`
- Adjacent recovery/opening/store-smoke tests: `1063/1063`
- Backend full: `6428/6428`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
