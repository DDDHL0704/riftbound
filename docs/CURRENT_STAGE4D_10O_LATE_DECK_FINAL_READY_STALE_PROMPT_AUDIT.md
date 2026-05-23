# Stage 4D-10O Late-Deck Final Ready Stale Prompt Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official late-deck final-ready stale prompt / mulligan prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialFinalReadyLateDeckPromptReplayRejectsWithoutRoomPromptResidue` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing prompt-expiry path already rejected a prompt-scoped final `READY` replay after the same late-deck final-ready prompt had advanced the match into official opening / mulligan. This slice binds that stale-prompt rejection to opening-zone determinism, mulligan prompt state, snapshot queues, exact no-mutation assertions, and absence of stale room actions.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialFinalReadyLateDeckPromptReplayRejectsWithoutRoomPromptResidue`.
- Starts from P1-submitted / P1-ready / P2-late-submitted room setup state and reuses `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.
- Captures P2's prompt-scoped `READY` command from the actionable room prompt after late deck submission.
- Proves P2 final prompt-scoped `READY` accepts, records `["P1", "P2"]` ready players, enters `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, emits one `PLAYER_READY`, and emits official opening / match start events.
- Proves the accepted transition exposes only mulligan prompts and no stale `READY` or `SUBMIT_DECK` room actions or candidates.
- Proves replaying the same prompt-scoped P2 `READY` rejects with `ErrorCodes.PromptExpired` and `行动窗口已过期，请按最新提示重新提交。`.
- Proves stale-prompt rejection emits no events and preserves exact state hash, tick, RNG cursor, ready players, hands, main decks, mulligan completion list, second-action player id, snapshots and idle pending-task queue.
- Proves stale-prompt rejection keeps the same mulligan prompt queue and does not reintroduce stale room actions or candidates.

## Non-Closure

This narrows official late-deck final-ready stale prompt rejection, mulligan prompt-queue and stale room-action cleanup drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
