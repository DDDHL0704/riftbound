# Stage 4D-17LB Recovery Spectator Timing Pending Nested Payload Shape Audit

Date: 2026-05-27 20:37 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing pending queue/payment nested payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes missing/null spectator timing pending queue metadata and pending payment cost semantics from present malformed non-object nested payloads.
- Present non-object `Timing["pendingTaskQueue"]["metadata"]` payloads now produce `spectator replay frame timing pending task queue metadata payload is required`.
- Present non-object `Timing["pendingPayment"]["cost"]` payloads now produce `spectator replay frame timing pending payment cost payload is required`.
- Non-object pending task queue task entries now continue validation across the expected task list, allowing multiple `spectator replay frame timing pending task queue task payload is required` diagnostics in one pass.
- Missing/null metadata and cost payloads keep their existing required diagnostics.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingNestedPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame pending task entries, pending queue metadata and pending payment cost to non-object payloads and asserts explicit nested payload-shape diagnostics, including two malformed task item diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `333/333`.
- Adjacent recovery/opening/store-smoke filter: `914/914`.
- Backend full suite: `6279/6279`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
