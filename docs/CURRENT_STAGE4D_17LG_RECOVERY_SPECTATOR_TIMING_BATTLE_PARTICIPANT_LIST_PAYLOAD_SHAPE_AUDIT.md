# Stage 4D-17LG Recovery Spectator Timing Battle Participant List Payload Shape Audit

Date: 2026-05-27 21:23 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing battle attacker/defender participant-list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed battle participant-list payloads from list-shaped payloads.
- Present non-list `Timing["battle"]["attackerObjectIds"]` now produces `spectator replay frame timing battle attacker object id list payload is required`.
- Present non-list `Timing["battle"]["defenderObjectIds"]` now produces `spectator replay frame timing battle defender object id list payload is required`.
- Missing/null participant lists keep existing required diagnostics.
- Valid list-shaped participant payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleParticipantListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame battle `attackerObjectIds` and `defenderObjectIds` payloads to non-list values and asserts the explicit list payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `338/338`.
- Adjacent recovery/opening/store-smoke filter: `919/919`.
- Backend full suite: `6284/6284`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
