# Stage 4D-09W Reveal Card Reaction Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 reveal-card reaction accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing standby reaction reveal-card accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `P4RevealCardCommandRejectsAcceptedReactionReplayWithoutMutation` and `RevealCardReactionStalePromptReplayAfterCardMovesToStackRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale reaction `REVEAL_CARD` commands without mutation; this slice binds both accepted results and both rejected replay results to the stack-priority prompt, snapshot and idle queue contract after the standby reaction flips face-up and moves from base to stack above the pending P2 stack item.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertRevealCardReactionStackPriorityPromptQueueAudit(...)`.
- Reused the helper for the accepted reaction reveal result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped reaction reveal result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_CLOSED`, with P1 priority, the revealed standby card face-up on stack, no pending tasks, no battlefield tasks and exactly two stack items: the original P2 pending probe and the P1 Teemo standby reaction above it.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_CLOSED`, both stack items in order and idle pending-task queue.
- It proves P1 stays on the authoritative actionable `STACK_PRIORITY` prompt with `PASS_PRIORITY`, and any `REVEAL_CARD` candidate for the old source is disabled or absent after accepted and rejected replay results.
- It proves P2 remains non-actionable without `REVEAL_CARD` / `PASS_PRIORITY`.

## Non-Closure

This narrows reaction reveal-card replay / stale prompt final prompt-queue and stack visibility drift risk only. It does not close base reveal-card breadth, full standby breadth, full hidden-info random-zone breadth, full payment-resource breadth, full priority/stack lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
