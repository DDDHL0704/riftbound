# Stage 4D-195K Recovery Temporary Payment Remaining Power Shape Audit

Date: 2026-06-08 10:30 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 195K added spectator replay timing `temporaryPaymentResources[0].remainingPower` non-number/list-array payload-shape validation coverage in `MatchRecoveryTests`.
- The new regression sets the redacted spectator replay timing temporary payment resource remaining power to an array while the authoritative state has one temporary payment resource with remaining power 1.
- The test proves recovery validation emits the stable remaining power invalid diagnostic, the keyed authoritative remaining power mismatch diagnostic and the aggregate remaining powers disagree diagnostic.
- The test explicitly proves the rejection is not relying on a temporary payment resource count mismatch.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 195K main `2ffc009f`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1379/1379`.
- Adjacent recovery filter: `1384/1384`.
- Backend full conformance project: `7654/7654`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit succeeded via SSH.

## Remaining Open

This closes only a narrow spectator replay timing `temporaryPaymentResources[0].remainingPower` payload-shape audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
