# Stage 4D-17KZ Recovery Spectator Timing Window/Duel/Battle Payload Shape Audit

Date: 2026-05-27 20:18 CST

Scope: A_MAIN server P1-004 replay/recovery determinism slice for spectator replay-frame timing top-level object payload shape validation.

Runtime change:
- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now distinguishes missing/null spectator timing `turnWindow`, `spellDuel` and `battle` objects from present malformed non-object payloads.
- Present non-null non-object `Timing["turnWindow"]`, `Timing["spellDuel"]` and `Timing["battle"]` payloads now produce explicit payload-shape diagnostics before property-name, value-shape and parity consumers consume those payloads.
- Missing/null timing objects retain the existing authoritative mismatch diagnostics.

Test coverage:
- Added `RecoveryValidatorRejectsSpectatorReplayTimingWindowBattlePayloadShapeDrift` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- The regression mutates spectator replay-frame `Timing["turnWindow"]`, `Timing["spellDuel"]` and `Timing["battle"]` to non-object payloads and asserts explicit payload-shape diagnostics for each object.

Validation:
- Focused single test: `1/1`.
- Focused recovery suite: `331/331`.
- Adjacent recovery/opening/store-smoke filter: `912/912`.
- Backend full suite: `6277/6277`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

Locks:
- Touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, this dedicated audit doc and the shared coordination board.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Status: This narrows replay/recovery determinism only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
