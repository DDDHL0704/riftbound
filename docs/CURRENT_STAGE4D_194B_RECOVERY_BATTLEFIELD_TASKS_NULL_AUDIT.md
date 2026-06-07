# Stage 4D-194B Recovery Battlefield Tasks Null Audit

Date: 2026-06-07 22:06 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 194B added spectator replay timing `battlefieldTasks` null-payload validation coverage in `MatchRecoveryTests`.
- The new regression sets `battlefieldTasks` itself to null in a redacted spectator replay frame and proves recovery validation emits the stable required battlefield tasks diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 194B main `294870ed`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1344/1344`.
- Adjacent recovery filter: `1349/1349`.
- Backend full conformance project: `7619/7619`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `battlefieldTasks` null-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
