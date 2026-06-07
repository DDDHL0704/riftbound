# Stage 4D-193G Recovery Pending Hand Choice Max Count Audit

Date: 2026-06-07 19:41 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 193G added spectator replay timing `pendingHandChoice.maxCount` missing-payload validation coverage in `MatchRecoveryTests`.
- The new regression removes the top-level `maxCount` scalar from a redacted spectator replay frame's `pendingHandChoice` payload and proves recovery validation emits the stable required-scalar diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 193G main `b388753a`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1323/1323`.
- Adjacent recovery filter: `1328/1328`.
- Backend full conformance project: `7598/7598`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `pendingHandChoice.maxCount` missing-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
