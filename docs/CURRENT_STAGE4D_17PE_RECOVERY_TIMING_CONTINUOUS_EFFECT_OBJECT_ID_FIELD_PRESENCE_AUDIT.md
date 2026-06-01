# Stage 4D-17PE Recovery Timing Continuous Effect Object Id Field Presence Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now requires the nullable `targetObjectId` and `sourceObjectId` fields to be present. Their values may still be null or valid strings. This matches the snapshot builder, which always emits both fields for continuous-effect snapshot views even when the authoritative state has no target or source object id. The spectator coverage includes a continuous-effect count mismatch case so same-payload field-presence diagnostics still run before authoritative parity is skipped.

## Validation

- Focused nullable object-id field tests: `2/2`
- Focused recovery tests: `450/450`
- Adjacent recovery/opening/store-smoke tests: `1031/1031`
- Backend full: `6396/6396`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
