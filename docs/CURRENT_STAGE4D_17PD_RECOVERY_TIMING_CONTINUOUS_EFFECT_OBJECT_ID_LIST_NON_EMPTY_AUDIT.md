# Stage 4D-17PD Recovery Timing Continuous Effect Object Id List Non-Empty Audit

Date: 2026-06-01

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in continuous-effect timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `continuousEffects[]` item validation now rejects present empty optional participant/dependency object-id lists. This matches the snapshot builder, which emits `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds` and `participantDependencyObjectIds` only when the source list has at least one value. The spectator coverage includes a continuous-effect count mismatch case so same-payload empty-list diagnostics still run before authoritative parity is skipped.

## Validation

- Focused empty object-id list tests: `2/2`
- Focused recovery tests: `448/448`
- Adjacent recovery/opening/store-smoke tests: `1029/1029`
- Backend full: `6394/6394`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
