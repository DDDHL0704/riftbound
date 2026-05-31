# Stage 4D-17OT Recovery Snapshot Timing Pending Payment Cost Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view pending-payment timing validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: recovered player-view snapshot `Timing["pendingPayment"]["cost"]` now distinguishes missing/null cost from malformed non-object cost payloads. Missing/null cost keeps the existing required diagnostic; malformed non-object cost now emits an explicit cost payload-shape diagnostic before downstream cost scalar validation, power-trait map validation and recovered pending-payment comparison logic consume or skip that field.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `430/430`
- Adjacent recovery/opening/store-smoke tests: `1011/1011`
- Backend full: `6376/6376`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
