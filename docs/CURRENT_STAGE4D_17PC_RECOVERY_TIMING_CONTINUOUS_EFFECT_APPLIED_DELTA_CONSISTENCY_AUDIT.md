# Stage 4D-17PC Recovery Timing Continuous Effect Applied Delta Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now enforces the snapshot builder's applied-delta scalar consistency. Optional `appliedPowerDelta` must match the required `powerDelta` when present. The spectator coverage includes a continuous-effect count mismatch case so same-payload applied-delta diagnostics still run before authoritative parity is skipped.

## Validation

- Focused applied-delta tests: `2/2`
- Focused recovery tests: `446/446`
- Adjacent recovery/opening/store-smoke tests: `1027/1027`
- Backend full: `6392/6392`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
