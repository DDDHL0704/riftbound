# Stage 4D-17LY Recovery Snapshot Lanes Battlefield-Object-Id Item Value Audit

Date: 2026-05-28 00:03 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot lanes battlefield-object-id item value validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now validates required values inside object entries of list-shaped recovered snapshot `Lanes["battlefieldObjectIds"]`.
- Each battlefield-object-id entry now requires well-formed `playerId` and `objectId` string values.
- Blank, non-string and surrounding-whitespace values now produce explicit recovered lanes battlefield-object-id item value diagnostics.
- Missing/null/non-list lane lists keep existing compatibility and 17LV/17LW/17LX shape/property-name diagnostics.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotLanesBattlefieldObjectIdItemValueShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot `battlefieldObjectIds[]` object entries with whitespace, blank and non-string `playerId` / `objectId` values, then asserts explicit value-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `356/356`.
- Adjacent recovery/opening/store-smoke filter: `937/937`.
- Backend full suite: `6302/6302`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
