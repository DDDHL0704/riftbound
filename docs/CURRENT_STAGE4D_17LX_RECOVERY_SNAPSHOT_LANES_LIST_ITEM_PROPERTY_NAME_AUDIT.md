# Stage 4D-17LX Recovery Snapshot Lanes List Item Property-Name Audit

Date: 2026-05-27 23:54 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot lanes list item property-name validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now validates property names inside object entries of list-shaped recovered snapshot lane payloads.
- Blank property names, surrounding-whitespace property names and duplicate normalized property names inside `Lanes["battlefieldObjectIds"][]` and `Lanes["battlefields"][]` now produce explicit recovered lanes list item property-name diagnostics.
- Missing/null/non-list lane lists keep existing compatibility and 17LV/17LW shape diagnostics.
- Valid object entries continue through future value consumers unchanged.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotLanesListItemPropertyNameDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot `battlefieldObjectIds[]` and `battlefields[]` object entries to include duplicate normalized keys, surrounding-whitespace keys and blank keys, then asserts explicit property-name diagnostics for both lists.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `355/355`.
- Adjacent recovery/opening/store-smoke filter: `936/936`.
- Backend full suite: `6301/6301`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
