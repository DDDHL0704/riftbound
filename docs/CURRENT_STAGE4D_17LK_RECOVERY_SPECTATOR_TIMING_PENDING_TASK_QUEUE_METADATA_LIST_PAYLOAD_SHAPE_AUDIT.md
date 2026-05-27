# Stage 4D-17LK Recovery Spectator Timing Pending Task Queue Metadata List Payload Shape Audit

Date: 2026-05-27 21:54 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing pending task queue metadata list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed pending task queue metadata list payloads from list-shaped payloads.
- Present non-list `Timing["pendingTaskQueue"]["metadata"]["stateBasedTaskKinds"]` now produces `spectator replay frame timing pending task queue metadata state-based task kind list payload is required`.
- Missing/null metadata lists keep existing required diagnostics.
- Valid list-shaped metadata payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueMetadataListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame pending task queue metadata `stateBasedTaskKinds` to a non-list value and asserts the explicit list payload-shape diagnostic.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `342/342`.
- Adjacent recovery/opening/store-smoke filter: `923/923`.
- Backend full suite: `6288/6288`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
