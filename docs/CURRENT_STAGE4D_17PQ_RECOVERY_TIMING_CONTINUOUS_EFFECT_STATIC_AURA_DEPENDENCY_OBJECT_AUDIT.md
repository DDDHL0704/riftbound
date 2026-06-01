# Stage 4D-17PQ Recovery Timing Continuous Effect Static Aura Dependency Object Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks that current `STATIC_AURA` dependency lists reference their own source and target objects. If a static-aura payload has a readable `sourceObjectId` and readable non-empty `sourceDependencyObjectIds`, that list must include the source object id. If it has a readable `targetObjectId` and readable non-empty `targetDependencyObjectIds`, that list must include the target object id. Missing, null, empty or malformed dependency lists keep the existing dedicated diagnostics from earlier slices. The spectator coverage includes a continuous-effect count mismatch case so same-payload static-aura dependency-object diagnostics still run before authoritative parity is skipped.

## Validation

- Focused static-aura dependency-object consistency tests: `2/2`
- Focused recovery tests: `474/474`
- Adjacent recovery/opening/store-smoke tests: `1055/1055`
- Backend full: `6420/6420`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
