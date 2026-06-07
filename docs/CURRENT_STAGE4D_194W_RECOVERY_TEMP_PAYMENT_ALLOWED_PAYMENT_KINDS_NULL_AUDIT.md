# Stage 4D-194W Recovery Temporary Payment Allowed Payment Kinds Null Audit

Date: 2026-06-08 00:46 CST

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- 194W added spectator replay timing `temporaryPaymentResources[0].allowedPaymentKinds` null-payload validation coverage in `MatchRecoveryTests`.
- The new regression sets the single redacted spectator replay temporary payment resource allowed payment kind list to null while the authoritative state has one temporary payment resource with allowed payment kind `RUNE_COST`.
- The test proves recovery validation emits the stable keyed authoritative allowed payment kinds mismatch diagnostic plus the aggregate allowed payment kinds disagree diagnostic without a temporary resource count mismatch.

Runtime changed: no. Server test coverage only. This was implemented directly by A_MAIN in single-agent mode.

## Commits

- 194W main `64f1ff5e`

## Validation

- Focused test: `1/1` after aligning the assertion to the current validator diagnostic contract.
- Changed-class filter: `1365/1365`.
- Adjacent recovery filter: `1370/1370`.
- Backend full conformance project: `7640/7640`.
- `git diff --check`: passed.
- Conflict-marker scan over `docs`, `tests` and `src`: passed before docs sync.

## Coordination Notes

- A_MAIN did not create a subagent or new worktree for this slice.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked before the docs checkpoint.
- Push after the code commit failed because GitHub HTTPS credentials were unavailable in this environment.

## Remaining Open

This closes only a narrow spectator replay timing `temporaryPaymentResources[0].allowedPaymentKinds` null-payload audit slice. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
