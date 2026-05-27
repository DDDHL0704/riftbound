# Stage 4D-17LM Recovery Spectator Snapshot Lane Battlefield String List Payload Shape Audit

Date: 2026-05-27 22:14 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame snapshot lane battlefield string-list payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed lane battlefield string-list payloads from list-shaped payloads.
- Present non-list `Lanes["battlefields"][]["occupantObjectIds"]`, `occupantControllerIds`, `standbyObjectIds`, `scoredThisTurnPlayerIds` and `pendingTaskKinds` now produce explicit list payload-shape diagnostics.
- Missing/null lane battlefield lists keep existing required diagnostics.
- Valid list-shaped payloads continue through existing string-list value validation and authoritative comparisons.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplaySnapshotBattlefieldStringListPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame lane battlefield string-list fields to non-list values and asserts the explicit list payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `344/344`.
- Adjacent recovery/opening/store-smoke filter: `925/925`.
- Backend full suite: `6290/6290`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
