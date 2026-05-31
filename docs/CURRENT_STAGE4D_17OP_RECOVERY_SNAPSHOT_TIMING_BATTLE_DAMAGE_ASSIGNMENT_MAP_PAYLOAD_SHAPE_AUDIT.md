# Stage 4D-17OP Recovery Snapshot Timing Battle Damage Assignment Map Payload Shape Audit

Date: 2026-05-31

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in recovered player-view snapshot timing battle damage-assignment validation.

The runtime change is limited to `MatchRecoveryValidator.ValidateSnapshotPlayerPayloads`: object-shaped `Timing["battle"]["damageAssignment"]` payloads now emit explicit map payload-shape diagnostics for malformed non-map `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` fields before downstream map value validation and recovered snapshot comparison logic consume those fields.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `425/425`
- Adjacent recovery/opening/store-smoke tests: `1006/1006`
- Backend full: `6371/6371`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
