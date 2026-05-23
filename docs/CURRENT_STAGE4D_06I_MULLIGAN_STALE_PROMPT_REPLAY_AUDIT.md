# Stage 4D-06I Mulligan Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted server test / opening mulligan stale prompt replay closure slice. Project remains **NOT READY**.

## Scope

This slice covers an official opening mulligan replay edge at the `MatchSession` prompt boundary: the active opening player submits the current prompt-scoped `MULLIGAN`, completes their mulligan, and the authoritative mulligan window advances to the second-action player.

The accepted coverage proves that replaying the old prompt-scoped `MULLIGAN` raw command after the window advances is rejected by stale prompt protection before it can mutate hand / main-deck / RNG / prompt state. No runtime behavior change was required because `TryRejectStalePrompt(...)` already compares submitted `promptId` / `snapshotTick` to the current prompt.

Locked surfaces remained unchanged: runtime behavior, matrix JSON, `PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `OfficialMulliganStalePromptReplayAfterSecondPlayerWindowStartsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`.
- The test starts from the official opening state after both players submit valid decks and both players ready the match into mulligan timing.
- It captures the active opening player's current `MULLIGAN` prompt-scoped raw command with `promptId` and `snapshotTick`.
- It accepts the first submit, proving one `MULLIGAN_COMPLETED`, selected cards returned to main deck, the active player recorded as completed, and the second-action player becoming the only actionable mulligan prompt.
- It replays the old prompt-scoped command with a new intent and proves `PROMPT_EXPIRED`, no events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate mulligan side effects, no hand / main-deck / RNG drift, and no active-player mulligan prompt fork.

## Non-Closure

This narrows opening mulligan stale prompt replay / second-player window fork risk only. It does not close full opening lifecycle breadth, deck submission / READY / mulligan breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
