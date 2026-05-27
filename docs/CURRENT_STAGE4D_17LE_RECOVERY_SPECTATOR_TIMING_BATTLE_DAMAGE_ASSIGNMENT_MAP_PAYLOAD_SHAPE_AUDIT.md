# Stage 4D-17LE Recovery Spectator Timing Battle Damage-Assignment Map Payload Shape Audit

Date: 2026-05-27 21:04 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing battle damage-assignment map payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed battle damage-assignment map payloads from object-shaped map payloads.
- Present non-object `Timing["battle"]["damageAssignment"]["damagePool"]` now produces `spectator replay frame timing battle damage assignment damage pool map payload is required`.
- Present non-object `Timing["battle"]["damageAssignment"]["legalTargets"]` now produces `spectator replay frame timing battle damage assignment legal targets map payload is required`.
- Present non-object `Timing["battle"]["damageAssignment"]["existingDamage"]` now produces `spectator replay frame timing battle damage assignment existing damage map payload is required`.
- Present non-object `Timing["battle"]["damageAssignment"]["lethalDamageThreshold"]` now produces `spectator replay frame timing battle damage assignment lethal damage threshold map payload is required`.
- Valid object-shaped maps continue through existing value validation, and missing/null required pending maps keep existing diagnostics.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentMapPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame damage-assignment `damagePool`, `legalTargets`, `existingDamage` and `lethalDamageThreshold` payloads to non-object values and asserts the explicit map payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `336/336`.
- Adjacent recovery/opening/store-smoke filter: `917/917`.
- Backend full suite: `6282/6282`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
