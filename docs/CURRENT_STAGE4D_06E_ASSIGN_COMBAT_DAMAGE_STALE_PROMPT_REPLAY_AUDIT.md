# Stage 4D-06E Assign Combat Damage Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / assign-combat-damage stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a consecutive battle-task prompt replay edge: P1 assigns combat damage for the current battle, the battle closes, and the task queue immediately advances to the next contested battlefield's spell-duel prompt.

The accepted coverage proves that replaying the old prompt-scoped `ASSIGN_COMBAT_DAMAGE` raw command after the next contest starts is rejected by `MatchSession` stale prompt protection before it can mutate the next task. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `NaturalAssignCombatDamageStalePromptReplayAfterNextContestStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`.
- The test starts from a natural assignment-ordering battle with a hidden standby object and a queued next contested battlefield.
- It captures P1's current assign-combat-damage prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `BATTLE_DAMAGE_STEP_STARTED`, `COMBAT_DAMAGE_ASSIGNED`, `BATTLE_CLOSED`, `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED` happen once, the old battle task closes, and the next contested battlefield opens a spell-duel focus prompt.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate combat-damage / battle-close / next-contest side effects, no task-queue drift, no zone drift, no stack drift, and no assign-damage prompt fork.

## Non-Closure

This narrows assign-combat-damage stale prompt replay / next-contest task fork risk only. It does not close full battle lifecycle breadth, full spell-duel lifecycle breadth, full priority / stack lifecycle breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
