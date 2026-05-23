# Stage 4D-06U Reveal Card Stale Prompt Replay Audit

Date: 2026-05-22

Owner: `A_MAIN`

Status: accepted test-only server standby reveal / reaction prompt-expiry closure slice. Project remains **NOT READY**.

## Scope

This slice covers prompt-stamped stale replay for `REVEAL_CARD` after P1 uses an old prompt to reveal a face-down Teemo standby card through both implemented paths:

- base reveal: `STANDBY_REVEAL` keeps the source in `BASE` and flips it face up.
- reaction reveal: `STANDBY_REACTION` removes the source from `BASE`, moves it to `STACK`, increments cards-played once and appends one standby reaction stack item.

Allowed runtime surface was observation-only. No runtime change was required. The current session-layer behavior correctly rejects the old prompt stamp with `PROMPT_EXPIRED`; the public rejected prompt may omit `REVEAL_CARD` entirely or retain only disabled explanatory metadata, but it must not be enabled or expose the old source.

Locked surfaces remained unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol core fields, official catalog, browser / Chrome / formal E2E scripts, `fullOfficial`, READY and `riftbound-dotnet.sln`.

## Evidence

- Added `RevealCardBaseStalePromptReplayAfterCardFlipsFaceUpRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Added `RevealCardReactionStalePromptReplayAfterCardMovesToStackRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`.
- Proves the current prompt-scoped base reveal accepts once, emits exactly `CARD_REVEALED`, keeps the source in `BASE`, flips it face up, keeps stack empty, and preserves the opposing battlefield unit.
- Proves the current prompt-scoped reaction reveal accepts once, emits exactly `CARD_REVEALED`, `CARD_PLAYED`, `COST_PAID` and `STACK_ITEM_ADDED`, removes the source from `BASE`, moves it to `STACK`, increments P1 cards played once and preserves the pending opposing stack item.
- Proves replaying the same `RevealCardCommand` instances with the old `promptId` / `snapshotTick` rejects with `PROMPT_EXPIRED`.
- Proves no replay events, exact `MatchStateHasher.Hash(...)` preservation, no duplicate reveal / play / cost / stack side effects, no cards-played drift, no face-down / face-up drift, no base / object-location / stack drift, and no enabled stale `REVEAL_CARD` candidate or old-source candidate exposure.

## Non-Closure

This narrows standby reveal / standby reaction prompt replay, duplicate reveal, duplicate stack-entry and hidden-card transition risk only. It does not close full PaymentEngine breadth, full standby/hide/reveal breadth, full reaction timing breadth, full stack lifecycle breadth, hidden-info random-zone breadth, replay/recovery determinism breadth, P0/P1, card matrix readiness, frontend build, Chrome smoke, formal E2E, `fullOfficial` or READY.
