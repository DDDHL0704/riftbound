# Stage 4D-17LT Recovery Snapshot Player Zones List Payload Shape Audit

Date: 2026-05-27 23:19 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot player zones list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now distinguishes present malformed recovered snapshot player zone list payloads from list-shaped payloads.
- Present non-list `Players[*]["zones"]["hand"]`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone` and `championZone` now produce explicit player zone list payload-shape diagnostics.
- Missing/null zone lists keep existing required diagnostics.
- Valid list-shaped payloads continue through existing string-list value validation.
- The existing spectator list payload-shape helper now delegates to the shared snapshot string-list payload-shape helper.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotPlayerZonesListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot player zone lists to non-list payloads and asserts explicit list payload-shape diagnostics across all seven zone lists.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `351/351`.
- Adjacent recovery/opening/store-smoke filter: `932/932`.
- Backend full suite: `6297/6297`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
