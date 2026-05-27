# Stage 4D-17LJ Recovery Spectator Timing Pending Payment List Payload Shape Audit

Date: 2026-05-27 21:44 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing pending-payment list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed pending-payment list payloads from list-shaped payloads.
- Present non-list `Timing["pendingPayment"]["paymentChoices"]` now produces `spectator replay frame timing pending payment payment choice list payload is required`.
- Present non-list `Timing["pendingPayment"]["paymentResourceActions"]` now produces `spectator replay frame timing pending payment payment resource action list payload is required`.
- Missing/null optional payment lists keep existing compatibility/mismatch behavior.
- Valid list-shaped payment payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame pending-payment `paymentChoices` and `paymentResourceActions` payloads to non-list values and asserts the explicit list payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `341/341`.
- Adjacent recovery/opening/store-smoke filter: `922/922`.
- Backend full suite: `6287/6287`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
