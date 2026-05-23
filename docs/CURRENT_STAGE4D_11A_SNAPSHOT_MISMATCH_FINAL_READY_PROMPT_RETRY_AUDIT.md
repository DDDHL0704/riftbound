# Stage 4D-11A Snapshot-Mismatch Final READY Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official snapshot-mismatch prompt-scoped final READY retry / mulligan prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `SnapshotMismatchPromptScopedFinalReadyKeepsRoomPromptReusable` to `OfficialOpeningTests`, reusing the 10Z optional `snapshotTick` override on `PromptScopedReadyRawCommand(...)`.

Runtime behavior was not changed. The existing prompt validation path already compares prompt id first and snapshot tick second, so when the final room player carries the current `READY` prompt id but a stale snapshot tick, the command is rejected before official opening starts. This slice binds that final READY snapshot-mismatch contract to the exact snapshot-expired diagnostics, no-mutation room state, final READY prompt id / snapshot tick stability and successful reuse of the original prompt to enter mulligan.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `SnapshotMismatchPromptScopedFinalReadyKeepsRoomPromptReusable`.
- Starts from room setup after P1 has submitted/readied, P2 submits late, and P2 remains on actionable final `READY`.
- Proves P2 submitting a prompt-scoped final `READY` with the current prompt id but stale snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese snapshot-expired message.
- Proves the snapshot-mismatch final READY rejection emits no events and preserves exact state hash, tick, ready players, both submitted decklists, snapshots and idle pending-task queue.
- Proves P2's original final READY prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves P2 can then use the same prompt id / current snapshot tick to READY successfully, enter `IN_PROGRESS` / `MULLIGAN`, emit `PLAYER_READY`, `OFFICIAL_OPENING_STARTED` and `MATCH_STARTED`, expose mulligan prompts, and clear stale room `READY` / `SUBMIT_DECK` actions and candidates.

## Non-Closure

This narrows official snapshot-mismatch prompt-scoped final READY rejection, room prompt reuse and room-to-mulligan prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
