# Stage 4D-17LL Recovery Spectator Timing Resolution History Item List Payload Shape Audit

Date: 2026-05-27 22:03 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing resolution-history item list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed resolution-history item list payloads from list-shaped payloads.
- Present non-list `Timing["battlefieldResolutions"][]["participantObjectIds"]` and `relatedEventKinds` now produce explicit list payload-shape diagnostics.
- Present non-list `Timing["battleResolutions"][]["attackerObjectIds"]`, `defenderObjectIds`, `survivingAttackerObjectIds`, `survivingDefenderObjectIds`, `destroyedObjectIds` and `relatedEventKinds` now produce explicit list payload-shape diagnostics.
- Missing/null item lists keep existing optional/parity behavior.
- Valid list-shaped item payloads continue through existing string-list value validation.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryItemListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame battlefield/battle resolution item list fields to non-list values and asserts the explicit list payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `343/343`.
- Adjacent recovery/opening/store-smoke filter: `924/924`.
- Backend full suite: `6289/6289`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
