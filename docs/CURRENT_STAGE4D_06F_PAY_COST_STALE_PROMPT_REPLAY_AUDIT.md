# Stage 4D-06F Pay Cost Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / pay-cost stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a trigger-payment replay edge at the `MatchSession` prompt boundary: P1 pays a battlefield-conquered Gold trigger payment, the payment window closes, and the task queue immediately advances to the next contested battlefield's spell-duel prompt.

The accepted coverage proves that replaying the old prompt-scoped `PAY_COST` raw command after the next contest starts is rejected by `MatchSession` stale prompt protection before it can mutate the new task. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `BattlefieldConquerGoldTriggerPaymentStalePromptReplayAfterNextContestStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`.
- The test starts from a battlefield-conquered Gold trigger payment with a queued next contested battlefield.
- It captures P1's current pay-cost prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `COST_PAID`, `BATTLEFIELD_TRIGGER_RESOLVED` and `PAYMENT_WINDOW_CLOSED` happen once, the Gold token is created once, and the next contested battlefield opens a spell-duel focus prompt.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate payment / trigger / token / window-close side effects, no next-contest task drift, no Gold token duplication, and no pay-cost prompt fork.

## Non-Closure

This narrows PaymentEngine / `PAY_COST` stale prompt replay / next-contest task fork risk only. It does not close full PaymentEngine / PAY_COST breadth, full battle lifecycle breadth, full spell-duel lifecycle breadth, full priority / stack lifecycle breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
