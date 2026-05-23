# Stage 4D-06T Hide Card Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server standby / hidden-info prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for `HIDE_CARD` after P1 hides Teemo from hand as a standby card, pays the standby cost, moves the source from `HAND` to `BASE`, and keeps the hidden card face down.

Allowed runtime surface was observation-only. No runtime change was required. The current session-layer behavior correctly rejects the old prompt stamp with `PROMPT_EXPIRED`; the public rejected prompt may retain a disabled `HIDE_CARD` candidate for explanatory UI metadata, but it must not be enabled or expose the old source.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `HideCardStalePromptReplayAfterCardMovesToBaseRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the current prompt-scoped `HIDE_CARD` accepts once, emits exactly `COST_PAID` and `CARD_HIDDEN`, spends P1's 1 mana, removes the source from hand, places it in `BASE`, and keeps hidden card details redacted from the public hidden event.
- Proves replaying the same `HideCardCommand` with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate cost payment, no duplicate hide, no hand/base/source-state drift, no stack drift, and no enabled stale `HIDE_CARD` candidate or old-source candidate exposure after the source leaves hand.

## Non-Closure

This narrows standby-hide prompt replay / hidden-card duplication risk only. It does not close full PaymentEngine breadth, full standby/reveal breadth, hidden-info random-zone breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
