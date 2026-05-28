# Stage 4D-17OK Recovery Snapshot Timing Pending Task Queue Metadata List Payload Shape Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing pending-task-queue metadata validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: object-shaped `Timing["pendingTaskQueue"]["metadata"]` payloads now emit an explicit nested string-list payload-shape diagnostic for malformed non-list `stateBasedTaskKinds` before downstream value validation and recovered snapshot comparison logic consume that field.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `420/420`
- Adjacent recovery/opening/store-smoke tests: `1001/1001`
- Backend full: `6366/6366`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
