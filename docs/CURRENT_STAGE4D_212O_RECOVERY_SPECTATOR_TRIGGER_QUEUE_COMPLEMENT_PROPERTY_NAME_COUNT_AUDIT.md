# Stage 4D-212O Recovery Spectator Trigger Queue Complement Property Name Count Audit

Timestamp: 2026-06-12 20:34 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing keyed complementary item property-name validation for `triggerQueue[]` when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedComplementPropertyNameWithCountMismatch` builds a spectator replay frame from authoritative trigger queue state.
- The test replaces the first spectator trigger-queue item with raw JSON carrying whitespace-wrapped `triggerId`, duplicate `controllerId`, whitespace-wrapped `sourceObjectId`, duplicate `sourceVisibility`, whitespace-wrapped `effectKind`, duplicate `triggeredByEventKind`, and an empty property name.
- The test appends `trigger-extra`, forcing spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits all complementary spectator trigger-queue property-name diagnostics, reports `trigger-extra`, and emits the trigger queue count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1812/1812`.
- Adjacent recovery filter `MatchRecovery`: `1817/1817`.
- Backend full: `8100/8100`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `99b08d14` (`test: cover spectator trigger queue complement property names with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue complement property-name validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
