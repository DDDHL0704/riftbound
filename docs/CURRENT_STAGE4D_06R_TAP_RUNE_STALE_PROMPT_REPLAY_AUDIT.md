# Stage 4D-06R Tap Rune Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server rune-resource prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for `TAP_RUNE` after P1 taps a basic red rune, gains mana, exhausts the rune, and remains in the main-action window with that rune no longer offered as a tap candidate.

Allowed runtime surface was observation-only. No runtime change was required because `MatchSession.SubmitAsync(...)` already applies `TryRejectStalePrompt(...)` before core command resolution while the match is in progress.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `TapRuneStalePromptReplayAfterRuneExhaustsRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the current prompt-scoped `TAP_RUNE` accepts once, emits exactly `RUNE_TAPPED` and `MANA_GAINED`, increments P1 mana to `1`, exhausts the rune, and keeps the source in `BASE`.
- Proves replaying the same `TapRuneCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate mana gain, no duplicate rune exhaustion, no object-location drift, and no stale `TAP_RUNE` source candidate exposure after the rune is exhausted.

## Non-Closure

This narrows tap-rune prompt replay / resource-entry duplication risk only. It does not close full PaymentEngine breadth, full rune-resource breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
