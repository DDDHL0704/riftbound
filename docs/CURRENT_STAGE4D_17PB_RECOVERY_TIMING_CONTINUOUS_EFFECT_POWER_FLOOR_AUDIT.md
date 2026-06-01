# Stage 4D-17PB Recovery Timing Continuous Effect Power Floor Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now enforces LayerEngine power-floor scalar consistency. Optional `minimumPower` must be non-negative when present, and optional `resultingPower` must not be lower than `minimumPower` when both values are present. The spectator coverage includes a continuous-effect count mismatch case so same-payload power-floor diagnostics still run before authoritative parity is skipped.

## Validation

- Focused power-floor tests: `2/2`
- Focused recovery tests: `444/444`
- Adjacent recovery/opening/store-smoke tests: `1025/1025`
- Backend full: `6390/6390`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
