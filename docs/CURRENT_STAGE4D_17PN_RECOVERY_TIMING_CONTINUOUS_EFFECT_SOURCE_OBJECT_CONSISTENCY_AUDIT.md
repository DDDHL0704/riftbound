# Stage 4D-17PN Recovery Timing Continuous Effect Source Object Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now checks current source-object shape for known valid layer/scope combinations. Current global `RULE_TEXT` effects must not carry a `sourceObjectId`, while current `STATIC_AURA` effects with valid `OBJECT` or `BATTLEFIELD` scope require one. Other valid continuous-effect combinations keep existing nullable source-object compatibility because current builder output still includes both tracked-source and legacy/null-source forms. Invalid layer/scope combinations continue to use the existing layer-scope diagnostic and do not cascade into source-object diagnostics. The spectator coverage includes a continuous-effect count mismatch case so same-payload source-object diagnostics still run before authoritative parity is skipped.

## Validation

- Focused source-object consistency tests: `2/2`
- Focused recovery tests: `468/468`
- Adjacent recovery/opening/store-smoke tests: `1049/1049`
- Backend full: `6414/6414`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
