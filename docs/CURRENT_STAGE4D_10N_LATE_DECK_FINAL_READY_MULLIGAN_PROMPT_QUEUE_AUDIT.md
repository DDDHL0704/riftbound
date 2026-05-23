# Stage 4D-10N Late-Deck Final Ready Mulligan Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official late-deck final-ready / mulligan prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `OfficialFinalReadyAfterLateDeckSubmissionStartsMulliganWithoutRoomPromptResidue` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing official room setup path already allowed the final `READY` after a late deck submission to start official opening / mulligan. This slice binds that bridge transition to opening zones, mulligan prompts, snapshot queues, ready replay determinism, and absence of stale room actions.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialFinalReadyAfterLateDeckSubmissionStartsMulliganWithoutRoomPromptResidue`.
- Starts from P1-submitted / P1-ready / P2-late-submitted room setup state and reuses `AssertOfficialSingleReadyBothDecksPromptQueueAudit(...)`.
- Proves P2 final `READY` accepts, records `["P1", "P2"]` ready players, enters `IN_PROGRESS`, `MULLIGAN` / `MULLIGAN`, emits one `PLAYER_READY`, and emits official opening / match start events.
- Proves opening active / second-player identities are distinct and the mulligan prompt queue satisfies `AssertOfficialReadyMulliganPromptQueueAudit(...)`.
- Proves opening prompts and candidates contain no stale `READY` or `SUBMIT_DECK` room actions.
- Proves P2 `READY` replay accepts without events and preserves exact state hash, tick, RNG cursor, ready players, hands, main decks, mulligan completion list, second-action player id, snapshots and idle pending-task queue.

## Non-Closure

This narrows official late-deck final-ready transition, mulligan prompt-queue and stale room-action cleanup drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
