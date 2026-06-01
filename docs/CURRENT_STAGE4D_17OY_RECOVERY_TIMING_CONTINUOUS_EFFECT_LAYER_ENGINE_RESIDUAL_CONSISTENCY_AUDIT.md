# Stage 4D-17OY Recovery Timing Continuous Effect LayerEngine Residual Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks the local LayerEngine residual invariant emitted by the snapshot builder. `layerEngineStatus=FOUNDATION_ONLY` requires a non-empty `deferredLayerEngineResiduals` list, and present residuals require the foundation-only layer-engine status. The spectator coverage includes a continuous-effect count mismatch case so same-payload residual/status diagnostics still run before authoritative parity is skipped.

## Validation

- Focused LayerEngine residual consistency tests: `2/2`
- Focused recovery tests: `438/438`
- Adjacent recovery/opening/store-smoke tests: `1019/1019`
- Backend full: `6384/6384`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
