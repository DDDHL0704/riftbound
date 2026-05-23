# Stage 4D-06L Move Unit Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server board-task / `MOVE_UNIT` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a board action-window replay edge at the `MatchSession` prompt boundary: P2 submits the current prompt-scoped `MOVE_UNIT` from base into an occupied enemy battlefield, that submit starts spell-duel focus timing, and P2's old action-window `MOVE_UNIT` prompt is no longer current.

Runtime behavior was not changed in this slice. `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before handing core commands to `CoreRuleEngine`; the new coverage proves that global prompt guard protects this `MOVE_UNIT` transition.

Locked surfaces remained unchanged: runtime, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `MoveUnitStalePromptReplayAfterSpellDuelStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.
- Added a local `PromptScopedMoveUnitRawCommand(...)` helper for prompt-stamped `MOVE_UNIT` raw commands.
- The test starts from a base-to-occupied-battlefield move window, captures P2's current prompt-scoped `MOVE_UNIT` raw command, and accepts it once to move the unit, mark the battlefield contested, start spell duel, and expose `PASS_FOCUS`.
- It replays the old prompt-scoped `MOVE_UNIT` with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate move / contest / spell-duel side effects, no task-queue drift, no object-location drift, and no `MOVE_UNIT` prompt fork.

## Non-Closure

This narrows board action-window `MOVE_UNIT` stale prompt replay / spell-duel transition fork risk only. It does not close full move-unit legality breadth, full battle / spell-duel lifecycle breadth, full replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
