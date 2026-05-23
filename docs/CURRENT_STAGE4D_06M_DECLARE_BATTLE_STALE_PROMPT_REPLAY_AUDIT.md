# Stage 4D-06M Declare Battle Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server battle-task / `DECLARE_BATTLE` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a battle-declaration replay edge at the `MatchSession` prompt boundary: P1 submits the current prompt-scoped `DECLARE_BATTLE` for an active start-battle task, that declaration closes the current battle and advances to the next contested battlefield's spell-duel focus prompt, and P1's old battle-declaration prompt is no longer current.

Runtime behavior was not changed in this slice. `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before handing core commands to `CoreRuleEngine`; the new coverage proves that global prompt guard protects this `DECLARE_BATTLE` to spell-duel transition.

Locked surfaces remained unchanged: runtime, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `DeclareBattleStalePromptReplayAfterNextSpellDuelStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`.
- Added a local `PromptScopedDeclareBattleRawCommand(...)` helper for prompt-stamped `DECLARE_BATTLE` raw commands.
- The test starts from an active start-battle task, captures P1's current prompt-scoped `DECLARE_BATTLE` raw command, and accepts it once to close BF-1 battle, resolve control, destroy the defender, and advance to BF-2 spell-duel focus.
- It replays the old prompt-scoped `DECLARE_BATTLE` with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate battle declaration / close / control / destroy / next-spell-duel side effects, no task-queue drift, and no `DECLARE_BATTLE` prompt fork.

## Non-Closure

This narrows battle-declaration stale prompt replay / next-spell-duel transition fork risk only. It does not close full declare-battle legality breadth, full battle / spell-duel lifecycle breadth, full replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
