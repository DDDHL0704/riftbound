# Stage 4D-09U Hide Card Replay Prompt Queue Audit

Date: 2026-05-23

Owner: `A_MAIN`

Status: accepted test-only server P0-005 hide-card accepted replay / stale prompt replay prompt-queue audit slice. Project remains **NOT READY**.

## Scope

This slice strengthens the existing hide-card accepted replay and stale prompt replay tests in `ConformanceFixtureRunnerTests`: `P4HideCardCommandRejectsAcceptedReplayWithoutMutation` and `HideCardStalePromptReplayAfterCardMovesToBaseRejectsWithoutMutation`.

Runtime behavior was not changed. The existing accepted replay and prompt-expiry paths already rejected stale `HIDE_CARD` commands without mutation; this slice binds both accepted results and both rejected replay results to the ordinary main-window prompt, snapshot, idle queue and hidden-info redaction contract after the standby card moves from hand to base face-down.

Locked surfaces remained unchanged: runtime, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `AssertHideCardOrdinaryMainPromptQueueAudit(...)`.
- Reused the helper for the accepted hide-card result and accepted-command replay rejection result.
- Reused the helper for the accepted prompt-scoped hide-card result and rejected stale prompt replay result.
- The audit proves the final state remains P1 active / turn player in `MAIN` / `NEUTRAL_OPEN`, with the standby card removed from hand, placed in base face-down, mana/power spent, no pending tasks, no stack items, no battlefield tasks and no priority / focus player.
- It proves snapshots expose P1 active / turn player, `MAIN` / `NEUTRAL_OPEN`, empty stack and idle pending-task queue.
- It proves P2 snapshot redacts the face-down hidden card's `cardNo`, `power`, `tags` and `manaCost`.
- It proves P1 stays on an authoritative actionable `MAIN_ACTION` prompt with `END_TURN`. If `HIDE_CARD` remains listed as an action after stale prompt replay, the helper requires the `HIDE_CARD` candidate to be disabled and the old source absent, so the stale command is not executable.
- It proves P2 remains non-actionable without `HIDE_CARD`.

## Non-Closure

This narrows hide-card replay / stale prompt final prompt-queue and hidden-info redaction drift risk only. It does not close full standby breadth, reveal-card breadth, full hidden-info random-zone breadth, full payment-resource breadth, full turn lifecycle breadth, full replay/recovery determinism breadth, full prompt/reconnect breadth, full PaymentEngine breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, card matrix readiness, P0/P1 closure, frontend build, Chrome smoke, formal 18-step final rerun, READY or goal completion.
