# Stage 4D-06P Activate Ability Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server activated-ability / stack-priority prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for `ACTIVATE_ABILITY` after P1 activates Fluft Poro's Warhawk ability, exhausts the source as cost, and advances from the main-action prompt into stack priority.

Allowed runtime surface was observation-only. No runtime change was required because `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before core command resolution while the match is in progress.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `FluftPoroActivationStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/FluftPoroActivatedAbilityTests.cs`.
- Proves the current prompt-scoped `ACTIVATE_ABILITY` accepts once, emits exactly `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID` and `STACK_ITEM_ADDED`, exhausts Fluft Poro, keeps the source on battlefield, places a no-target ability item on the stack, opens neutral-closed priority for P1, and creates no Warhawk tokens before stack resolution.
- Proves replaying the same `ActivateAbilityCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate exhaustion, no duplicate cost payment, no duplicate stack item, no stack-item drift, no priority-window drift, no pre-resolution token creation, and no stale `ACTIVATE_ABILITY` action exposure while stack priority is active.

## Non-Closure

This narrows activated-ability prompt replay / stack-priority transition fork risk only. It does not close full PaymentEngine breadth, full activated-ability official breadth, full stack lifecycle breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
