# Stage 4D-194Z Recovery Temporary Payment Restriction Null Audit

Date: 2026-06-08 01:07 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 194Z added spectator replay timing `temporaryPaymentResources[0].resourceRestriction` null-payload validation coverage in `MatchRecoveryTests`.
- The new regression sets the single redacted spectator replay temporary payment resource restriction to null while the authoritative state has one temporary payment resource with the Malzahar payment-only temporary ledger restriction.
- The test proves recovery validation emits the stable resource restriction required diagnostic, the keyed authoritative resource restriction mismatch diagnostic, and the aggregate restrictions disagree diagnostic without a temporary resource count mismatch.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 194Z main `1da5bd1d`

## Validation

- Focused test: `1/1`.
- Changed-class filter: `1368/1368`.
- Adjacent recovery filter: `1373/1373`.
- Backend full conformance project: `7643/7643`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `temporaryPaymentResources[0].resourceRestriction` null-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
