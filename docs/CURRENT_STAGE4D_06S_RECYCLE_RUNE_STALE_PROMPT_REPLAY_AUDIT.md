# Stage 4D-06S Recycle Rune Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server rune-resource prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for `RECYCLE_RUNE` after P1 recycles a basic red rune, moves it from `BASE` to `RUNE_DECK`, gains 1 red power, and remains in the main-action window with that rune no longer offered as a recycle candidate.

Allowed runtime surface was observation-only. No runtime change was required because `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before core command resolution while the match is in progress.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `RecycleRuneStalePromptReplayAfterRuneMovesToRuneDeckRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the current prompt-scoped `RECYCLE_RUNE` accepts once, emits exactly `RUNE_RECYCLED` and `POWER_GAINED`, removes the source from `BASE`, appends it to `RUNE_DECK`, grants 1 red power, clears source exhaustion, and records the source location as `RUNE_DECK`.
- Proves replaying the same `RecycleRuneCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate power gain, no duplicate rune-deck append, no exhausted-state drift, no object-location drift, and no stale `RECYCLE_RUNE` source candidate exposure after the rune leaves base.

## Non-Closure

This narrows recycle-rune prompt replay / resource-entry duplication risk only. It does not close full PaymentEngine breadth, full rune-resource breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
