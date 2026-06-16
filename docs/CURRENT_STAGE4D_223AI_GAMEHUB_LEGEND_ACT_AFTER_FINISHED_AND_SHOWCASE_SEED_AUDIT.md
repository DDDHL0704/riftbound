# Stage 4D-223AI GameHub LegendAct After-Finished And Showcase Seed Audit

Date: 2026-06-16 12:20 CST
Status: accepted as a narrow A_MAIN `main`-branch slice with concurrent Dev seed/frontend import.
Runtime changed: yes, Dev seed/configuration surface only.
Frontend changed: yes, battlefield card image orientation only.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` and accepts concurrent local changes in `src/Riftbound.Api/Hubs/GameHub.cs`, `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.DevUi/src/styles/globals.css` and `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`.

`LegendActAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `LEGEND_ACT` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing `LEGEND_ACT` after-finished assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/clientIntentId/intentId/raw/secret/internal/debug leakage
- no `LEGEND_ACT`, `SubmitIntent`, `MatchFinished` or error-code text leakage in the user message
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

The concurrent Dev seed import adds a `midgame-showcase` scenario, exposes a `Riftbound:AllowDevSeedScenarios` configuration gate for explicitly allowing `SeedScenario` outside Development, completes showcase object locations for base/graveyard/banished objects, and rotates battlefield card images to `270deg`. New tests prove the showcase seed exposes intended scores, cards-played counters, base/battlefield/graveyard/banished zones, object card details and locations, hides the opposing face-down standby object from the non-owner view, and allows production-hosted seed only when the explicit configuration flag is true.

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 401-404 anchor active / triggered skill stack placement, choices, cost determination and payment for the `LEGEND_ACT` command-surface context. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

For the showcase seed visibility checks, latest core rules 108.7, 128 and 129.3 anchor hand privacy, privacy levels and card-back hiding; latest core rules 160-166 anchor rune identity, rune resources and rune-pool context.

## Validation

- Focused tests: `3/3`.
- Changed classes `GameHubJoinTests|ConformanceFixtureShapeTests`: `363/363`.
- Adjacent Hub/after-finished/protocol/error/LegendAct/ActivateAbility/PayCost/Trigger/Stack/Raw/ClientIntent/SeedScenario/Showcase filter: `2134/2134`.
- Backend full `Riftbound.slnx`: `8269/8269`.
- DevUi build passed.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `origin/main`; DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

This narrows `LEGEND_ACT` after-finished error-envelope coverage and accepts a Dev seed/frontend showcase import only. It does not change active-skill legality, target selection, cost payment, stack placement, priority/focus handling, match-finished enforcement, win/score/surrender/removal behavior, redaction text, journal semantics, broader error envelopes, production gameplay rules, official catalog, recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final status.

Project remains **NOT READY**.
