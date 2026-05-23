# Stage 4D-06J Ready Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server runtime / opening READY stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers an official opening READY replay edge at the `MatchSession` prompt boundary: P2 submits the current prompt-scoped `READY`, the match starts official opening mulligan timing, and P2's old READY prompt is no longer current.

Runtime now checks stale prompt stamps in `ReadyAsync(...)` before the existing `InProgress` READY no-op compatibility path. Unstamped READY replay remains compatible as a no-op after match start, but a raw command carrying an old `promptId` / `snapshotTick` now rejects with `PROMPT_EXPIRED` before it can bypass the prompt guard.

Locked surfaces remained unchanged: matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Updated `src/Riftbound.Engine/MatchSession.cs` so `ReadyAsync(...)` calls `TryRejectStalePrompt(...)` after finished-match rejection and before the `InProgress` no-op path.
- Added `OfficialReadyStalePromptReplayAfterMulliganStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- The test starts from official deck submission and P1 ready, captures P2's current prompt-scoped READY raw command, and accepts it once to start official opening mulligan.
- It replays the old prompt-scoped READY with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate `PLAYER_READY` / `OFFICIAL_OPENING_STARTED` / `MATCH_STARTED` side effects, no hand / main-deck / RNG drift, no ready-player drift, and no READY prompt fork.

## Non-Closure

This narrows opening READY stale prompt replay / prompt-guard bypass risk only. It does not close full opening lifecycle breadth, deck submission / READY / mulligan breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
