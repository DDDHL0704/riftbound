# Stage 4D-17OS Recovery Timing Pending Task Queue Task List Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in shared pending-task-queue timing validation.

The runtime change is limited to `MatchRecoveryValidator`: recovered player-view snapshot timing and spectator replay-frame timing `pendingTaskQueue.tasks` payloads now emit an explicit task-list payload-shape diagnostic for malformed non-list values before downstream task item-shape validation, task value validation, count checks and recovered/spectator parity logic consume or skip that field.

## Validation

- Focused tests: `2/2`
- Focused recovery tests: `429/429`
- Adjacent recovery/opening/store-smoke tests: `1010/1010`
- Backend full: `6375/6375`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
