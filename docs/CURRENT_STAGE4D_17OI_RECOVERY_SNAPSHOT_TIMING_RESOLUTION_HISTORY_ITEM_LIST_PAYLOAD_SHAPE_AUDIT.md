# Stage 4D-17OI Recovery Snapshot Timing Resolution History Item List Payload Shape Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing resolution-history validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: object-shaped `Timing["battlefieldResolutions"][]` entries now emit explicit nested string-list payload-shape diagnostics for malformed non-list `participantObjectIds` and `relatedEventKinds` fields, and object-shaped `Timing["battleResolutions"][]` entries now emit the same shape diagnostics for malformed non-list `attackerObjectIds`, `defenderObjectIds`, `survivingAttackerObjectIds`, `survivingDefenderObjectIds`, `destroyedObjectIds` and `relatedEventKinds` fields before downstream value validation and recovered snapshot comparison logic consume those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `418/418`
- Adjacent recovery/opening/store-smoke tests: `999/999`
- Backend full: `6364/6364`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
