# Stage 4D-10X Snapshot-Mismatch Mulligan Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official snapshot-mismatch prompt-scoped mulligan retry / active prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `SnapshotMismatchPromptScopedMulliganKeepsActivePromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt validation path already compares prompt id first and snapshot tick second, so when the active player carries the current initial `MULLIGAN` prompt id but a stale snapshot tick, the command is rejected before mulligan execution. This slice binds that initial-mulligan snapshot-mismatch contract to the exact snapshot-expired diagnostics, no-mutation opening state, active prompt id / snapshot tick stability and successful active-player reuse of the original prompt.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `SnapshotMismatchPromptScopedMulliganKeepsActivePromptReusable`.
- Starts from official opening after both players submitted legal decks and readied.
- Captures the active player's actionable initial `MULLIGAN` prompt.
- Proves the active player submitting a prompt-scoped `MULLIGAN` with the current prompt id but stale snapshot tick rejects with `ErrorCodes.PromptExpired` and the exact Chinese snapshot-expired message.
- Proves the snapshot-mismatch prompt-scoped rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, mulligan completion list, opening second-action player, both players' hands / main decks, snapshots and idle pending-task queue.
- Proves the active player's original prompt remains reusable by preserving the same prompt id and snapshot tick after rejection.
- Proves the active player can then use the same prompt id / current snapshot tick to submit a legal `MULLIGAN`, emit one `MULLIGAN_COMPLETED`, avoid premature `MULLIGAN_PHASE_COMPLETED`, mark the active player complete and expose the second player's current mulligan prompt through `AssertOfficialMulliganSecondPlayerPromptQueueAudit(...)`.

## Non-Closure

This narrows official snapshot-mismatch prompt-scoped mulligan rejection, active prompt reuse and mulligan prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, full mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
