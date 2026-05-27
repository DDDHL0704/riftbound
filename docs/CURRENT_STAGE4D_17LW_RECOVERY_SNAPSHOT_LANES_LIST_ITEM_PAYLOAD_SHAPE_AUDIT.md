# Stage 4D-17LW Recovery Snapshot Lanes List Item Payload Shape Audit

Date: 2026-05-27 23:45 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot lanes list item payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now distinguishes non-object entries inside list-shaped recovered snapshot lane payloads from object entries.
- Non-object entries inside `Lanes["battlefieldObjectIds"]` and `Lanes["battlefields"]` now produce explicit recovered lanes list item payload-shape diagnostics.
- Missing/null/non-list lane lists keep existing compatibility and 17LV list payload-shape diagnostics.
- Object entries continue through existing property-name validation and future value consumers.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotLanesListItemPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot `battlefieldObjectIds[]` and `battlefields[]` to non-object entries and asserts explicit list item payload-shape diagnostics for both fields.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `354/354`.
- Adjacent recovery/opening/store-smoke filter: `935/935`.
- Backend full suite: `6300/6300`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
