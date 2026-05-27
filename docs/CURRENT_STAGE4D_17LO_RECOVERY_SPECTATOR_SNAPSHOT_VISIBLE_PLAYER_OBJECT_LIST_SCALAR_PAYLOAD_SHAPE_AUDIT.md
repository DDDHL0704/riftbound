# Stage 4D-17LO Recovery Spectator Snapshot Visible Player Object List Scalar Payload Shape Audit

Date: 2026-05-27 22:32 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame snapshot visible player object list scalar payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes present malformed visible player object list scalar payloads from list-shaped payloads.
- Present non-list visible `Players[*]["objects"][objectId]["tags"]` and `untilEndOfTurnEffects` now produce explicit list payload-shape diagnostics.
- Missing/null optional list scalars keep existing authoritative mismatch behavior.
- Valid list-shaped payloads continue through existing string-list value validation and authoritative comparisons.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplaySnapshotVisiblePlayerObjectListScalarPayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame visible player object list scalar fields to non-list values and asserts the explicit list payload-shape diagnostics.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `346/346`.
- Adjacent recovery/opening/store-smoke filter: `927/927`.
- Backend full suite: `6292/6292`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
