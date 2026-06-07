# Stage 4D-192J Raw Replay Audit

Date: 2026-06-07 17:06 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 192J added reordered stale raw `PLAY_CARD` rejected-cache replay coverage for Rek'Sai attack-reveal unit play.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 192J main `782c693f`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `10/10`.
- Adjacent raw/replay/client-intent filter: `1171/1171`.
- Backend full conformance project: not rerun for this first low-risk test-only small slice after the 192G/192H/192I bundle; the latest backend full remains 192I's `7577/7577`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- Standard raw property-order assertions remain strict by default; order comparison is relaxed only for the explicit reordered raw assertion.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the code commit.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow reordered stale raw `PLAY_CARD` replay audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
