# Stage 4D-06G Order Triggers Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / order-triggers stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a trigger-ordering replay edge at the `MatchSession` prompt boundary: P1 orders APNAP battle-initial triggers, the trigger queue moves to stack, and stack priority immediately opens for P2.

The accepted coverage proves that replaying the old prompt-scoped `ORDER_TRIGGERS` raw command after stack priority starts is rejected by `MatchSession` stale prompt protection before it can mutate the new stack-priority task. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OrderTriggersStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`.
- The test starts from the APNAP battle-initial trigger queue representative state.
- It captures P1's current order-triggers prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `TRIGGERS_ORDERED` and `TRIGGERS_MOVED_TO_STACK` happen once, the trigger queue is cleared, deterministic ordered stack items are created, and P2 receives stack priority.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate trigger-order / stack-move side effects, no trigger queue recreation, no duplicate stack items, no priority drift, and no order-triggers prompt fork.

## Non-Closure

This narrows trigger-ordering stale prompt replay / stack-priority fork risk only. It does not close full APNAP / replacement ordering breadth, full priority / stack lifecycle breadth, full battle lifecycle breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
