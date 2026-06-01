# Stage 4D-17PH Recovery Timing Continuous Effect Empty Deferred LayerEngine Residual Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects a present empty `deferredLayerEngineResiduals` list with the explicit `deferred LayerEngine residual list is required` diagnostic, regardless of whether `layerEngineStatus` is already `FOUNDATION_ONLY`. Present residual lists still require `layerEngineStatus=FOUNDATION_ONLY`, so empty residual lists with absent or non-foundation status now report both the direct empty-list diagnostic and the status-consistency diagnostic. This matches the snapshot builder, which emits `deferredLayerEngineResiduals` only when residual entries are non-empty. The spectator coverage includes a continuous-effect count mismatch case so same-payload empty residual-list diagnostics still run before authoritative parity is skipped.

## Validation

- Focused empty deferred LayerEngine residual tests: `2/2`
- Focused recovery tests: `456/456`
- Adjacent recovery/opening/store-smoke tests: `1037/1037`
- Backend full: `6402/6402`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
