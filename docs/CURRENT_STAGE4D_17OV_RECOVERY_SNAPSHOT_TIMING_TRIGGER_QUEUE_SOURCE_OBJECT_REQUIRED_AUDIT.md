# Stage 4D-17OV Recovery Snapshot Timing Trigger Queue Source Object Required Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view trigger-queue timing validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: recovered player-view snapshot `Timing["triggerQueue"][]` items now require `sourceObjectId`, matching the snapshot builder and spectator replay-frame trigger-queue validation. Missing/null source-object identities now emit an explicit required diagnostic before downstream trigger queue value and duplicate-id checks consume those entries.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `432/432`
- Adjacent recovery/opening/store-smoke tests: `1013/1013`
- Backend full: `6378/6378`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
