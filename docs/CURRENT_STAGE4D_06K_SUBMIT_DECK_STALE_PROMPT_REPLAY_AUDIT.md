# Stage 4D-06K Submit Deck Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server runtime / opening `SUBMIT_DECK` stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers an official opening deck-submission replay edge at the `MatchSession` prompt boundary: P2 submits the current prompt-scoped `SUBMIT_DECK`, both players advance to `READY` prompts, and P2's old `SUBMIT_DECK` prompt is no longer current.

Runtime now checks stale prompt stamps in `SubmitDeckAsync(...)` before the in-progress / ready / validation / duplicate-deck branches. Unstamped duplicate-deck behavior remains governed by the existing duplicate-deck guard, while a raw command carrying an old `promptId` / `snapshotTick` now rejects with `PROMPT_EXPIRED` before it can bypass the prompt guard.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Updated `src/Riftbound.Engine/MatchSession.cs` so `SubmitDeckAsync(...)` calls `TryRejectStalePrompt(...)` after finished-match rejection and before the in-progress / ready / validation / duplicate-deck paths.
- Added `SubmitDeckStalePromptReplayAfterReadyPromptStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- The test starts from P1's accepted deck submission, captures P2's current prompt-scoped `SUBMIT_DECK` raw command, and accepts it once to submit P2's deck and expose `READY` prompts for both players.
- It replays the old prompt-scoped `SUBMIT_DECK` with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `DECK_SUBMITTED`, no tick drift, no P1 / P2 decklist drift, and no `READY` prompt fork.

## Non-Closure

This narrows opening deck-submission stale prompt replay / prompt-guard bypass risk only. It does not close full opening lifecycle breadth, deck replacement breadth, invalid deck matrix breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
