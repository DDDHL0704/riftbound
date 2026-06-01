# Stage 4D-17PM Recovery Timing Continuous Effect Target Object Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks the required target-object shape for known valid layer/scope combinations. Current global `RULE_TEXT` effects must not carry a `targetObjectId`; current object-scoped `POWER_MODIFIER` and `RULE_TEXT` effects require one; current `STATIC_AURA` effects with valid `OBJECT` or `BATTLEFIELD` scope require one. Invalid layer/scope combinations continue to use the existing layer-scope diagnostic and do not cascade into target-object diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload target-object diagnostics still run before authoritative parity is skipped.

## Validation

- Focused target-object consistency tests: `2/2`
- Focused recovery tests: `466/466`
- Adjacent recovery/opening/store-smoke tests: `1047/1047`
- Backend full: `6412/6412`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
