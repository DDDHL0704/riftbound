# Stage 4D-17LF Recovery Spectator Timing Battle Damage-Assignment Required Target List Payload Shape Audit

Date: 2026-05-27 21:13 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing battle damage-assignment required-assignment target-list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed required-assignment target-list payloads from list-shaped payloads.
- Present non-list `Timing["battle"]["damageAssignment"]["requiredAssignments"][]["legalTargetObjectIds"]` now produces `spectator replay frame timing battle damage assignment required assignment item legal target object id list payload is required`.
- Missing/null required target lists keep existing required diagnostics.
- Valid list-shaped target payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentRequiredAssignmentItemPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates a spectator replay-frame damage-assignment required-assignment `legalTargetObjectIds` payload to a non-list value and asserts the explicit list payload-shape diagnostic.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `337/337`.
- Adjacent recovery/opening/store-smoke filter: `918/918`.
- Backend full suite: `6283/6283`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
