# Stage 4D-192W Recovery Pending Payment Cost Mana Audit

Date: 2026-06-07 18:30 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 192W added spectator replay timing `pendingPayment.cost.mana` missing-payload validation coverage in `MatchRecoveryTests`.
- The new regression removes the nested `mana` scalar from a redacted spectator replay frame's `pendingPayment.cost` payload and proves recovery validation emits the stable required-scalar diagnostic.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 192W main `f72d839d`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1313/1313`.
- Adjacent recovery filter: `1318/1318`.
- Backend full conformance project: `7588/7588`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the code commit.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `pendingPayment.cost.mana` missing-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
