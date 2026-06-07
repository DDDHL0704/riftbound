# Stage 4D-193W Recovery Battle Damage Assigning Player Id Null Audit

Date: 2026-06-07 21:32 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 193W added spectator replay timing `battle.damageAssignment.assigningPlayerId` null-payload validation coverage in `MatchRecoveryTests`.
- The new regression sets `assigningPlayerId` to null in a redacted spectator replay frame's `battle.damageAssignment` payload and proves recovery validation emits the stable required assigning player id diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 193W main `cf75ba8d`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1339/1339`.
- Adjacent recovery filter: `1344/1344`.
- Backend full conformance project: `7614/7614`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `battle.damageAssignment.assigningPlayerId` null-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
