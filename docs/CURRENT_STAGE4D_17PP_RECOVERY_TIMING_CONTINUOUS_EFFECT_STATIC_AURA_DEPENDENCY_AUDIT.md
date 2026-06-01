# Stage 4D-17PP Recovery Timing Continuous Effect Static Aura Dependency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks the current static-aura dependency-list shape. Current `STATIC_AURA` effects must carry `sourceDependencyObjectIds` and `targetDependencyObjectIds` lists, matching the current friendly-equipment and battlefield static-aura builder paths that derive source and target dependencies from public-field objects. Participant and participant-dependency lists keep their existing optional compatibility because some current static-aura paths can legitimately emit them only when non-empty. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura dependency diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura dependency-list consistency tests: `2/2`
- Focused recovery tests: `472/472`
- Adjacent recovery/opening/store-smoke tests: `1053/1053`
- Backend full: `6418/6418`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
