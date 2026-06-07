# Stage 4D-193D Recovery Pending Hand Choice Window Audit

Date: 2026-06-07 19:13 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 193D added spectator replay timing `pendingHandChoice.choiceWindow` missing-payload validation coverage in `MatchRecoveryTests`.
- The new regression removes the top-level `choiceWindow` scalar from a redacted spectator replay frame's `pendingHandChoice` payload and proves recovery validation emits the stable required-scalar diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 193D main `4ed5285a`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1320/1320`.
- Adjacent recovery filter: `1325/1325`.
- Backend full conformance project: `7595/7595`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `pendingHandChoice.choiceWindow` missing-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
