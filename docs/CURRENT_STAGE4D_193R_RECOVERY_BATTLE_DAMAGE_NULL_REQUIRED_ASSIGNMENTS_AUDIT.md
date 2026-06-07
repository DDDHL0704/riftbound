# Stage 4D-193R Recovery Battle Damage Null Required Assignments Audit

Date: 2026-06-07 20:55 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 193R added spectator replay timing `battle.damageAssignment.requiredAssignments` null required-list validation coverage in `MatchRecoveryTests`.
- The new regression sets `requiredAssignments` to null in a redacted spectator replay frame's `battle.damageAssignment` payload and proves recovery validation emits the stable required assignment list diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 193R main `d62b25b3`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1334/1334`.
- Adjacent recovery filter: `1339/1339`.
- Backend full conformance project: `7609/7609`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `battle.damageAssignment.requiredAssignments` null required-list audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
