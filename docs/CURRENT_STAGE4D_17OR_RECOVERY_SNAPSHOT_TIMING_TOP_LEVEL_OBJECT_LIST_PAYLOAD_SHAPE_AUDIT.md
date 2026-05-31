# Stage 4D-17OR Recovery Snapshot Timing Top-Level Object List Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: top-level object-list `Timing` payloads now emit explicit list payload-shape diagnostics for malformed non-list `temporaryPaymentResources`, `continuousEffects`, `triggerQueue`, `battlefieldTasks`, `battlefieldResolutions` and `battleResolutions` fields before downstream item-shape validation, value validation and recovered snapshot comparison logic consume or skip those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `427/427`
- Adjacent recovery/opening/store-smoke tests: `1008/1008`
- Backend full: `6373/6373`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
