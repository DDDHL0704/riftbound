# Stage 4D-17PG Recovery Timing Continuous Effect Empty Optional String Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects present empty-string optional fields for `effectKind`, `sourceCardNo`, `sourcePath`, `layerEngineStatus`, `condition` and `lifecycle`. Absent and null optional compatibility remains unchanged. This matches the snapshot builder, which emits those optional string fields only when the source value is non-empty. The spectator coverage includes a continuous-effect count mismatch case so same-payload empty optional-string diagnostics still run before authoritative parity is skipped.

## Validation

- Focused empty optional-string tests: `2/2`
- Focused recovery tests: `454/454`
- Adjacent recovery/opening/store-smoke tests: `1035/1035`
- Backend full: `6400/6400`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
