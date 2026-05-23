# Stage 4D-06B Order Triggers Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / order-triggers replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers the `ORDER_TRIGGERS` command surface after a legal APNAP trigger ordering has already been accepted and the trigger queue has moved onto the stack.

The accepted coverage proves that replaying the same `ORDER_TRIGGERS` command after the order window closes rejects without mutation. No runtime behavior change was required because `CoreRuleEngine.ResolveOrderTriggersRuntime(...)` already rejects when there is no open trigger-ordering window.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OrderTriggersRejectsAcceptedCommandReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`.
- The test starts from the existing battle-initial APNAP trigger queue representative state.
- It accepts the prompt-provided legal order `TRIGGER-BATTLE-DEFENDER`, then `TRIGGER-BATTLE-ATTACKER`.
- It proves the first submit emits exactly `TRIGGERS_ORDERED` and `TRIGGERS_MOVED_TO_STACK`, clears `TriggerQueue`, appends deterministic stack items, sets P2 as priority player, and removes `ORDER_TRIGGERS` from prompts.
- It replays the exact same command with a new intent and proves rejection with no events, exact `MatchStateHasher.Hash(...)` preservation, no trigger queue recreation, no duplicate stack items, no priority drift, and no `ORDER_TRIGGERS` prompt fork.

## Non-Closure

This narrows order-trigger accepted-command replay and trigger-queue duplicate-stack risk only. It does not close full trigger ordering breadth, full APNAP / replacement ordering breadth, battle / spell-duel lifecycle breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
