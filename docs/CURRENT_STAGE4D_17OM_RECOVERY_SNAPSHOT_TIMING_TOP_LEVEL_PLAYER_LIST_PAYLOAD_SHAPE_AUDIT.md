# Stage 4D-17OM Recovery Snapshot Timing Top-Level Player List Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: recovered snapshot timing now emits explicit string-list payload-shape diagnostics for malformed non-list top-level `readyPlayerIds`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` and `destroyedUnitOwnerIdsThisTurn` payloads before downstream membership, value and recovered snapshot comparison logic consume those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `422/422`
- Adjacent recovery/opening/store-smoke tests: `1003/1003`
- Backend full: `6368/6368`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
