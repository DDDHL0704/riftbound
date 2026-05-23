# Stage 4D-09V Reveal Card Base Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 reveal-card base accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing base reveal-card accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `P4RevealCardCommandRejectsAcceptedBaseReplayWithoutMutation` and `RevealCardBaseStalePromptReplayAfterCardFlipsFaceUpRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale base `REVEAL_CARD` commands without mutation; this slice binds both accepted results and both rejected replay results to the ordinary main-window prompt, snapshot, idle queue and face-up visibility contract after the standby card flips face-up in base.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertRevealCardBaseOrdinaryMainPromptQueueAudit(...)`.
- Reused the helper for the accepted base reveal result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped base reveal result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_OPEN`, with the standby card face-up in base, P2 battlefield unit undamaged, no pending tasks, no stack items, no battlefield tasks and no priority / focus player.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_OPEN`, empty stack and idle pending-task queue.
- It proves P2 snapshot exposes the now face-up card's `cardNo`, `power`, `tags` and `manaCost`.
- It proves P1 stays on an authoritative actionable `MAIN_ACTION` prompt with `END_TURN`, and any `REVEAL_CARD` candidate for the old source is disabled or absent after accepted and rejected replay results.
- It proves P2 remains non-actionable without `REVEAL_CARD`.

## Non-Closure

This narrows base reveal-card replay / stale prompt final prompt-queue and visibility drift risk only. It does not close reaction reveal-card breadth, full standby breadth, full hidden-info random-zone breadth, full payment-resource breadth, full turn lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
