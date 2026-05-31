# Stage 4D-17OQ Recovery Snapshot Timing Battle Damage Assignment Required Target List Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing battle damage-assignment validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: object-shaped `Timing["battle"]["damageAssignment"]["requiredAssignments"][]` payloads now emit an explicit list payload-shape diagnostic for malformed non-list `legalTargetObjectIds` fields before downstream list value validation and recovered snapshot comparison logic consume those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `426/426`
- Adjacent recovery/opening/store-smoke tests: `1007/1007`
- Backend full: `6372/6372`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
