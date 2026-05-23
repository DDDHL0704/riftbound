# Stage 4D-06X Pending Pay Cost Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / pending `PAY_COST` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers ordinary pending `PAY_COST` windows and temporary payment-resource pending `PAY_COST` windows at the `MatchSession` prompt boundary. It extends the earlier trigger-payment `PAY_COST` replay coverage to representative synthetic pending-payment states that quote ordinary mana, generic power, typed power and payment-only temporary resources.

The accepted coverage proves that a prompt-scoped pending `PAY_COST` command can accept once, close the payment window, clear the pay-cost prompt and consume the quoted resource exactly once. Replaying the old prompt-scoped raw command with a new intent after the window closes is rejected with `PROMPT_EXPIRED` before it can duplicate `COST_PAID`, `PAYMENT_WINDOW_CLOSED`, temporary-resource spend/clear events or any resource ledger mutation.

No runtime behavior change was required because `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before `CoreRuleEngine.ResolveAsync(...)`.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `PendingPayCostPromptScopedOrdinaryReplayAfterWindowClosesRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- Added `PendingPayCostPromptScopedTemporaryResourceReplayAfterWindowClosesRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`.
- Ordinary coverage runs mana, generic-power and typed-power pending-payment shapes through `MatchSession`, captures the current pay-cost prompt stamp, accepts the first submit, then replays the old raw command.
- Temporary-resource coverage does the same for a generated payment-only resource, proving it spends and clears once before the old prompt-stamped replay rejects.
- Each replay asserts `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no reopened pending payment, no pay-cost prompt fork and no stack drift.

## Non-Closure

This narrows PaymentEngine pending `PAY_COST` stale prompt replay, duplicate window-close and temporary-resource duplicate-spend risk only. It does not close full PaymentEngine / PAY_COST breadth, all duplicate-command combinations, keyword payment branches, remaining payment windows, full official matrix, card matrix readiness, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
