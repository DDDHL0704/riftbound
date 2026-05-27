# Stage 4D-17LC Recovery Spectator Timing Battle Damage-Assignment Payload Shape Audit

Date: 2026-05-27 20:46 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing battle damage-assignment nested payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes missing/null battle damage-assignment semantics from present malformed non-object payloads.
- Missing/null `Timing["battle"]["damageAssignment"]` now keeps a required diagnostic.
- Present non-object `Timing["battle"]["damageAssignment"]` payloads now produce `spectator replay frame timing battle damage assignment payload is required`.
- Present non-list `Timing["battle"]["damageAssignment"]["requiredAssignments"]` payloads now produce `spectator replay frame timing battle damage assignment required assignment list payload is required`.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame battle damage-assignment and required-assignment-list payloads to non-object/non-list values and asserts explicit payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `334/334`.
- Adjacent recovery/opening/store-smoke filter: `915/915`.
- Backend full suite: `6280/6280`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
