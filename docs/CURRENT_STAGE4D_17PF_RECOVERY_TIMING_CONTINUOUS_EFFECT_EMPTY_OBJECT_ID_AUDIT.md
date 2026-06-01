# Stage 4D-17PF Recovery Timing Continuous Effect Empty Object Id Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects present empty-string `targetObjectId` and `sourceObjectId` values. Present null remains valid because those fields are nullable. This matches the snapshot builder, which emits null or a concrete object id but never an empty object id string. The spectator coverage includes a continuous-effect count mismatch case so same-payload empty object-id diagnostics still run before authoritative parity is skipped.

## Validation

- Focused empty nullable object-id tests: `2/2`
- Focused recovery tests: `452/452`
- Adjacent recovery/opening/store-smoke tests: `1033/1033`
- Backend full: `6398/6398`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
