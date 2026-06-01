# Stage 4D-17PL Recovery Timing Continuous Effect Layer Scope Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects otherwise-known `scope` values when they are invalid for the known `layer`. Current power-modifier continuous effects require `OBJECT` scope. Current rule-text continuous effects require `GLOBAL` or `OBJECT` scope. Current static-aura continuous effects require `OBJECT` or `BATTLEFIELD` scope, with existing duration and foundation-only LayerEngine metadata/residual validation still enforced separately. The spectator coverage includes a continuous-effect count mismatch case so same-payload layer-scope diagnostics still run before authoritative parity is skipped.

## Validation

- Focused layer-scope consistency tests: `2/2`
- Focused recovery tests: `464/464`
- Adjacent recovery/opening/store-smoke tests: `1045/1045`
- Backend full: `6410/6410`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
