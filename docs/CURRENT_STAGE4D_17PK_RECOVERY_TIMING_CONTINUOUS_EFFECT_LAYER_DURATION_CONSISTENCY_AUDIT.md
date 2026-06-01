# Stage 4D-17PK Recovery Timing Continuous Effect Layer Duration Consistency Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects otherwise-known `duration` values when they are invalid for the known `layer`. Current non-static continuous effects (`POWER_MODIFIER` and `RULE_TEXT`) require `UNTIL_END_OF_TURN`. Current `STATIC_AURA` effects require `WHILE_SOURCE_ON_PUBLIC_FIELD` or `WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD`, with existing foundation-only LayerEngine metadata/residual validation still enforced separately. The spectator coverage includes a continuous-effect count mismatch case so same-payload layer-duration diagnostics still run before authoritative parity is skipped.

## Validation

- Focused layer-duration consistency tests: `2/2`
- Focused recovery tests: `462/462`
- Adjacent recovery/opening/store-smoke tests: `1043/1043`
- Backend full: `6408/6408`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
