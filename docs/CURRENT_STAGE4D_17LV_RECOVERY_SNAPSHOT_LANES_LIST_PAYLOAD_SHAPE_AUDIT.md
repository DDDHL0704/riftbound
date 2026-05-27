# Stage 4D-17LV Recovery Snapshot Lanes List Payload Shape Audit

Date: 2026-05-27 23:37 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot lanes list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now distinguishes present malformed recovered snapshot lane object-list payloads from list-shaped payloads.
- Present non-list `Lanes["battlefieldObjectIds"]` and `Lanes["battlefields"]` now produce explicit recovered lanes list payload-shape diagnostics.
- Missing/null lane lists keep existing recovered snapshot compatibility.
- Valid list-shaped payloads continue through existing property-name validation and future value consumers.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotLanesListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot `battlefieldObjectIds` and `battlefields` to non-list payloads and asserts explicit list payload-shape diagnostics for both fields.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `353/353`.
- Adjacent recovery/opening/store-smoke filter: `934/934`.
- Backend full suite: `6299/6299`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
