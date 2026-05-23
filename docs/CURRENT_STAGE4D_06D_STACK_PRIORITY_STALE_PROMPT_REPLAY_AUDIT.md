# Stage 4D-06D Stack Priority Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / stack-priority stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a consecutive stack-priority prompt replay edge: P1 passes priority on the current top stack item, the item resolves, and the stack immediately exposes a new top item with P1 still as the priority player.

The accepted coverage proves that replaying the old prompt-scoped `PASS_PRIORITY` raw command after the next stack item becomes current is rejected by `MatchSession` stale prompt protection before it can mutate or resolve the next stack item. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `StackPriorityStalePromptReplayAfterNextStackItemStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.
- The test starts from a two-item stack where `STACK-BATTLE-OR-FLIGHT` is the current top item and `STACK-FOLLOWUP-NOOP` is queued behind it.
- It captures P1's current stack-priority prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED` and `UNIT_MOVED_TO_BASE` happen once, the target defender returns to base, `STACK-FOLLOWUP-NOOP` becomes the only stack item, and P1 receives the next stack-priority prompt.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate priority pass / stack resolution / movement side effects, no accidental resolution of the next stack item, and no priority prompt drift.

## Non-Closure

This narrows stack-priority stale prompt replay / next-stack-item fork risk only. It does not close full priority / stack lifecycle breadth, full battle lifecycle breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
