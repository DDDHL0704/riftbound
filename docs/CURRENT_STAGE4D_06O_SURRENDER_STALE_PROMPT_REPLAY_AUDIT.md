# Stage 4D-06O Surrender Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server runtime / terminal-state `SURRENDER` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers a terminal-state replay edge at the `MatchSession` prompt boundary: P1 submits the current prompt-scoped `SURRENDER`, the accepted command finishes the match and moves both players to terminal `WAIT` prompts, and P1's old main-action prompt is no longer current.

Runtime behavior changed narrowly in `MatchSession.SubmitAsync(...)`: when the match is already finished, a prompt-stamped raw command now checks `TryRejectStalePrompt(...)` before throwing `MATCH_FINISHED`. Unstamped finished-state submits keep the existing `MATCH_FINISHED` behavior.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Updated `src/Riftbound.Engine/MatchSession.cs` so finished-state `SubmitAsync(...)` can return `PROMPT_EXPIRED` for old prompt-scoped raw commands while preserving existing unstamped `MATCH_FINISHED` behavior.
- Added `SurrenderStalePromptReplayAfterMatchFinishedRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- The test starts from a P1 open main prompt, captures P1's current prompt-scoped `SURRENDER` raw command, and accepts it once to emit `MATCH_WON`, finish the match, record P2 as winner, and expose terminal `WAIT` prompts.
- It replays the old prompt-scoped `SURRENDER` with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `MATCH_WON`, no winner/status drift, no terminal prompt fork, and no stale `SURRENDER` action exposure.

## Non-Closure

This narrows terminal-state `SURRENDER` stale prompt replay / finished-state prompt-guard bypass risk only. It does not close full win/loss breadth, full terminal recovery breadth, full turn/battle lifecycle breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
