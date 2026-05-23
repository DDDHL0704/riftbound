# Stage 4D-06C Spell Duel Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / spell-duel stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a consecutive spell-duel prompt replay edge: P1 closes the active BF-A spell duel, cleanup removes the BF-A defender, and the pending task queue immediately advances to a new BF-B spell duel where P1 is again the focus player.

The accepted coverage proves that replaying the old BF-A prompt-scoped `PASS_FOCUS` raw command after BF-B starts is rejected by `MatchSession` stale prompt protection before it can mutate BF-B. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `SpellDuelFocusStalePromptReplayAfterNextContestStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`.
- The test starts from the existing two-contest spell-duel representative state with BF-A already waiting for P1's closing `PASS_FOCUS`.
- It captures P1's BF-A prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving `FOCUS_PASSED`, `SPELL_DUEL_CLOSED`, `UNIT_DESTROYED`, `BATTLEFIELD_CONTESTED` and `SPELL_DUEL_STARTED` happen once and BF-B becomes the active spell-duel prompt for P1.
- It replays the old BF-A prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate spell-duel close / cleanup / next-contest advancement, and no BF-B focus drift.

## Non-Closure

This narrows spell-duel prompt replay / consecutive-task fork risk only. It does not close full spell-duel lifecycle breadth, full battle lifecycle breadth, full action-window determinism, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
