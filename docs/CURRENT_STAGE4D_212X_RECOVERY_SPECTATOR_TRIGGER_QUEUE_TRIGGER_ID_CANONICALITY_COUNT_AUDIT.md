# Stage 4D-212X Recovery Spectator Trigger Queue Trigger Id Canonicality Count Audit

Timestamp: 2026-06-12 21:42 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` trigger-id canonicality validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdCanonicalityWithCountMismatch` builds a spectator replay frame from authoritative visible trigger queue state.
- The test changes the spectator trigger's `triggerId` from `trigger-visible` to ` trigger-visible `, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the trigger-id surrounding-whitespace diagnostic, reports unexpected trigger id `trigger-extra`, and emits the trigger queue count mismatch.
- The test proves canonical key-set matching is preserved under count drift by asserting no missing/not-present `trigger-visible` diagnostics are emitted.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1821/1821`.
- Adjacent recovery filter `MatchRecovery`: `1826/1826`.
- Backend full: `8109/8109`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `7531ae37` (`test: cover spectator trigger queue trigger id canonicality with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-id canonicality validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
