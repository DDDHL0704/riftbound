# Stage 4D-17OX Recovery Timing Continuous Effect Known Value Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects unknown known-value scalars for `scope`, `layer` and optional `layerEngineStatus`. Accepted scopes are `GLOBAL`, `OBJECT` and `BATTLEFIELD`; accepted layers are `POWER_MODIFIER`, `RULE_TEXT` and `STATIC_AURA`; accepted layer-engine status is `FOUNDATION_ONLY`. The spectator coverage includes a continuous-effect count mismatch case so same-payload known-value diagnostics still run before authoritative parity is skipped.

## Validation

- Focused known-value tests: `2/2`
- Focused recovery tests: `436/436`
- Adjacent recovery/opening/store-smoke tests: `1017/1017`
- Backend full: `6382/6382`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
