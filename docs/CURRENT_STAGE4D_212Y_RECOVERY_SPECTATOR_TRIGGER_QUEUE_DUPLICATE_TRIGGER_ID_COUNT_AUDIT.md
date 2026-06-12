# Stage 4D-212Y Recovery Spectator Trigger Queue Duplicate Trigger Id Count Audit

Timestamp: 2026-06-12 21:49 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` duplicate trigger-id validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueDuplicateTriggerIdWithCountMismatch` builds a spectator replay frame from authoritative trigger queue state with `trigger-visible-a` and `trigger-visible-b`.
- The test changes the second spectator trigger id to duplicate `trigger-visible-a`, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `2` to `3`.
- Recovery validation emits the duplicate `trigger-visible-a` diagnostic, reports unexpected trigger id `trigger-extra`, reports missing authoritative trigger id `trigger-visible-b`, and emits the trigger queue count mismatch.
- The test preserves the existing duplicate-id canonical key behavior by asserting `trigger-visible-a` is not reported as absent from authoritative state.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1822/1822`.
- Adjacent recovery filter `MatchRecovery`: `1827/1827`.
- Backend full: `8110/8110`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `52900681` (`test: cover spectator trigger queue duplicate trigger id with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing duplicate trigger-id validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
