# Stage 4D-10D Final Mulligan Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 official final mulligan replay / stale prompt no-mutation audit slice. Project remains **NOT READY**.

## Scope

This slice adds final-mulligan replay coverage to `OfficialOpeningTests`: `OfficialFinalMulliganReplaysAfterFirstTurnStartsRejectWithoutMutation`.

Runtime behavior was not changed. The existing final mulligan completion, prompt-snapshot expiry and post-mulligan phase guards already rejected stale or replayed final `MULLIGAN` commands after the first turn began. This slice binds those rejections to state-hash, event, zone / object-location, prompt / snapshot and idle pending-queue contracts.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialFinalMulliganReplaysAfterFirstTurnStartsRejectWithoutMutation`.
- Reused `AssertOfficialFinalMulliganFirstTurnPromptQueueAudit(...)` for both accepted and replay-rejection first-turn states, with event assertion disabled for no-event rejection results.
- The audit proves a prompt-scoped final second-player `MULLIGAN` accepts once, completes both players' mulligans and enters the active player's first `MAIN_ACTION` prompt.
- It proves replaying that old prompt-scoped final `MULLIGAN` after first turn starts rejects with `PROMPT_EXPIRED`, emits no events and preserves the exact state hash, tick, RNG cursor, first-turn zones, prompts, snapshots and idle pending-task queue.
- It proves replaying an unstamped final `MULLIGAN` after first turn starts rejects with `PHASE_NOT_ALLOWED`, emits no events and preserves the same first-turn state hash and prompt / queue contract.
- It proves both rejection forms keep stale `MULLIGAN` absent from the active `MAIN_ACTION` prompt and keep the second player non-actionable on `WAIT` / `SURRENDER` without enabled `MULLIGAN`.

## Non-Closure

This narrows official final-mulligan replay / stale prompt no-mutation and first-turn prompt-queue drift risk only. It does not close submit-deck breadth, ready breadth, remaining mulligan edge breadth, full opening / first-turn breadth, hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
