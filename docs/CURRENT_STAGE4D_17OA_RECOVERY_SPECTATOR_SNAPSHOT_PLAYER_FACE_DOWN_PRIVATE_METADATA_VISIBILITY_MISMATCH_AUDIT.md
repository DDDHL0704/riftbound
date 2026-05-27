# Stage 4D-17OA Recovery Spectator Snapshot Player Face-Down Private Metadata Visibility Mismatch Audit

Date: 2026-05-28

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN closed one server P1-004 replay/recovery determinism slice in spectator replay-frame snapshot player object redaction.

The runtime change is limited to `MatchRecoveryValidator.ValidateSpectatorReplayFrame`: face-down spectator object payloads now treat every normal visible object scalar field as private metadata exposure. This covers `damage`, power scalars, exhausted/combat booleans, tags/effects, mana cost, attachment, card identity and owner/controller identity, instead of only `cardNo`, `tags` and `power`.

The helper is shared by expected visible face-down object validation and object-shaped extra `objects{}` validation under visibility drift.

## Validation

- Focused single test: `1/1`
- Focused recovery tests: `410/410`
- Adjacent recovery/opening/store-smoke tests: `991/991`
- Backend full: `6356/6356`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Locks

Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This narrows replay/recovery determinism only. P0/P1 and final readiness remain open.
