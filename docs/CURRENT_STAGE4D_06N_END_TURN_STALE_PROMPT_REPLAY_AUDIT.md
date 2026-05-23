# Stage 4D-06N End Turn Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server turn/window `END_TURN` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers an action-window replay edge at the `MatchSession` prompt boundary: P1 submits the current prompt-scoped `END_TURN`, the accepted command advances to P2's turn-start / main-window sequence, and P1's old main-action prompt is no longer current.

Runtime behavior was not changed in this slice. `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before handing core commands to `CoreRuleEngine`; the new coverage proves that global prompt guard protects this `END_TURN` turn-advance transition.

Locked surfaces remained unchanged: runtime, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `EndTurnStalePromptReplayAfterNextPlayerStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Added a local `PromptScopedRawCommand(...)` helper for prompt-stamped raw commands in the same test file.
- The test starts from a P1 open main prompt, captures P1's current prompt-scoped `END_TURN` raw command, and accepts it once to run turn-end / next-player turn-start resolution and expose P2's current `END_TURN` prompt.
- It replays the old prompt-scoped `END_TURN` with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate turn advancement / turn-start side effects, no prompt fork, and no stale P1 `END_TURN` action exposure.

## Non-Closure

This narrows `END_TURN` stale prompt replay / next-player turn transition fork risk only. It does not close full turn lifecycle breadth, cleanup breadth, battle / spell-duel lifecycle breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
