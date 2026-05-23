# Stage 4D-10R Invalid Submit-Deck Prompt Retry Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official invalid prompt-scoped submit-deck retry / room prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice adds `InvalidPromptScopedSubmitDeckKeepsRoomPromptReusable` to `OfficialOpeningTests`.

Runtime behavior was not changed. The existing invalid official-deck rejection path already rejected a prompt-scoped `SUBMIT_DECK` without mutating room state or consuming the actionable prompt. This slice binds that retry contract to prompt-scoped raw commands, decklist preservation, room prompt-queue determinism and a successful same-window valid resubmission.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `InvalidPromptScopedSubmitDeckKeepsRoomPromptReusable`.
- Starts from P2-submitted / P1-not-submitted room setup state and reuses `AssertOfficialSubmitDeckReadyAndSubmitPromptQueueAudit(...)`.
- Captures P1's actionable `SUBMIT_DECK` prompt after P2 has submitted a legal deck.
- Proves P1 prompt-scoped invalid `SUBMIT_DECK` rejects with `ErrorCodes.InvalidDeck` and Chinese official-deck diagnostics.
- Proves invalid prompt-scoped rejection emits no events and preserves exact state hash, tick, ready players, P1 no-deck state, P2 decklist, snapshots and idle pending-task queue.
- Proves the same room prompt remains usable: P1 submits a corrected legal deck with the same prompt id / snapshot tick and the command accepts.
- Proves the corrected submission emits one `DECK_SUBMITTED`, does not start official opening or match start, and preserves both-decks room setup prompts through `AssertOfficialSubmitDeckBothReadyPromptQueueAudit(...)`.

## Non-Closure

This narrows official invalid prompt-scoped submit-deck retry and room prompt-queue drift risk only. It does not close full submit-deck breadth, full ready breadth, mulligan breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
