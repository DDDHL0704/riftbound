# Stage 4D-17PJ Recovery Timing Continuous Effect Static Aura Foundation Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects `STATIC_AURA` payloads that are missing the snapshot builder's foundation-only LayerEngine metadata. Static-aura continuous effects now require `layerEngineStatus=FOUNDATION_ONLY` and a non-empty `deferredLayerEngineResiduals` list before downstream continuous-effect consumers and parity-only checks run. This matches the current builder paths for friendly equipment static auras and battlefield all-units static auras. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura foundation diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura foundation tests: `2/2`
- Focused recovery tests: `460/460`
- Adjacent recovery/opening/store-smoke tests: `1041/1041`
- Backend full: `6406/6406`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
