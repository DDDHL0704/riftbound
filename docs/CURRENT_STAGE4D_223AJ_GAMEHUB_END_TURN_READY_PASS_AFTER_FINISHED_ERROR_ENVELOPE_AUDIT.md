# Stage 4D-223AJ GameHub EndTurn/Ready/Pass After-Finished Error Envelope Audit

Date: 2026-06-16 12:41 CST

Status: accepted on local `main` as server-test commit `3766ecb0` plus accepted DevUi layout import commit `983ca311`; docs checkpoint follows this audit. Project remains **NOT READY**.

## Scope

- Server validation slice: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Accepted concurrent frontend import: `src/Riftbound.DevUi/src/components/match/PlayerBoard.tsx` and `src/Riftbound.DevUi/src/styles/globals.css`.
- Coordination/doc surface: completion audit, P0/P1 closure plan, dispatch/write-lock notes and shared coordination board.

## Server Coverage

`GameHubJoinTests` now proves four after-finished caller errors are normalized protocol envelopes:

- `EndTurnAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`
- `EndTurnWrapperAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`
- `ReadyAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`
- `PassAfterFinishedRedactsClientIntentAndDoesNotBroadcastOrMutate`

Each assertion now checks `MessageType.ERROR`, normalized `roomId`, `P1` player routing and default protocol/schema versions through `AssertProtocolDefaults(error)`. The EndTurn SubmitIntent path also explicitly proves no group error is broadcast after the match is finished. Existing `MatchFinished` code/message, sentinel/clientIntent/raw/secret/internal/debug/command redaction, no caller/group event/snapshot/prompt broadcast, no journal growth and no runtime mutation assertions remain intact.

## Frontend Import Review

A_MAIN inspected and accepted the concurrent DevUi match-board layout changes. The import wraps the rune meter and rune-card row in `.rune-slot-body`, tightens match-frame primary-zone grid sizing, centers signature/rune/battlefield card presentation, removes overlapping hand cards in the match frame and constrains battlefield occupant rows with fixed compact-card tracks. This is a layout-only frontend change.

## Rule Source

Rule source checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`:

- Latest core rules 302, 323.1 and 467 for game-ending/win-state context.
- Latest core rules 315-317 and 318-323.1 for turn structure, main/end phase flow and cleanup/win checks.
- Latest core rules 334-335 for end-of-turn declaration and turn advancement context.
- Latest core rules 649-652, especially 651.3, for removed-player inability to choose or otherwise affect the game.

## Validation

- Focused server filter: `4/4` passed.
- Changed class: `GameHubJoinTests` `218/218` passed.
- Adjacent Hub / after-finished / protocol / error / EndTurn / Ready / Pass / Turn / Raw / ClientIntent filter: `1602/1602` passed.
- Backend full before commit: `8269/8269` passed.
- DevUi build: passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed.

Browser note: the current DevUi shell was loaded during frontend review, and API 5088 health/catalog/negotiate checks were healthy for the tested origin. A full dynamic match connection was not claimed in this slice because the in-app browser had stale local settings pointing at port 5094, and the Browser text-input path could not update that setting in the session.

## Coordination

- A_MAIN continued directly on `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` branch `main` per user request.
- `rule-audit-remaining-20260615` cadence check after fetch found no commits ahead of `origin/main`.
- No subagent was created.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, official catalog, Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Open

This narrows GameHub EndTurn/Ready/Pass after-finished error-envelope coverage and accepts one layout-only DevUi import. It does not close match-finished enforcement broadly, mulligan/surrender/submit-deck remaining error envelopes, end-turn runtime legality, priority/focus handling, turn advancement semantics, recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness. Project remains **NOT READY**.
