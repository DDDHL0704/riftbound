# Stage 4D-212V Recovery Spectator Trigger Queue Trigger Id Null Count Audit

Timestamp: 2026-06-12 21:28 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` trigger-id null validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdNullWithCountMismatch` builds a spectator replay frame from authoritative visible trigger queue state.
- The test changes the spectator trigger's `triggerId` from `trigger-visible` to `null`, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the trigger-id required diagnostic for the null value, reports unexpected trigger id `trigger-extra`, reports missing authoritative trigger id `trigger-visible`, and emits the trigger queue count mismatch.
- The test exercises payload/key-set/count validation under count drift and does not rely on positional trigger-id parity diagnostics.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1819/1819`.
- Adjacent recovery filter `MatchRecovery`: `1824/1824`.
- Backend full: `8107/8107`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `6d7750cb` (`test: cover spectator trigger queue trigger id null with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-id null validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
