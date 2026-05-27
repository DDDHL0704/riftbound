# Stage 4D-17LU Recovery Snapshot Player Object List Scalar Payload Shape Audit

Date: 2026-05-27 23:28 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for recovered player-view snapshot player object list scalar payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSnapshotShape` now distinguishes present malformed recovered snapshot player object list scalar payloads from list-shaped payloads.
- Present non-list `Players[*]["objects"][objectId]["tags"]` and `untilEndOfTurnEffects` now produce explicit player object list payload-shape diagnostics.
- Missing/null optional list scalars keep existing optional compatibility.
- Valid list-shaped payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSnapshotPlayerObjectListScalarPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates recovered snapshot player object `tags` and `untilEndOfTurnEffects` to non-list payloads and asserts explicit list payload-shape diagnostics for both fields.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `352/352`.
- Adjacent recovery/opening/store-smoke filter: `933/933`.
- Backend full suite: `6298/6298`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
