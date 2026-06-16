# Stage 4D-223AK GameHub Mulligan/Surrender/SubmitDeck After-Finished Error Envelope Audit

Date: 2026-06-16 12:50 CST

Status: accepted on local `main` as code commit `3cb8db28`; docs checkpoint follows this audit. Project remains **NOT READY**.

## Scope

- Server validation slice: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- Runtime changed: no, server test coverage only.
- Frontend changed: no.

## Coverage

`GameHubJoinTests` now proves the remaining after-finished command surfaces in this group emit normalized error envelopes:

- `MulliganAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`
- `SurrenderAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`
- `SubmitDeckAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`
- Existing P7-9 battlefield-held-seven-units after-finished SubmitDeck check

The caller error now explicitly carries `MessageType.ERROR`, normalized `roomId`, `P1` player routing and default protocol/schema versions. The tests also assert no group error broadcast for the newly covered paths. Existing `MatchFinished` code/message, sentinel/clientIntent/raw/secret/internal/debug/command redaction, no caller/group event/snapshot/prompt broadcast, no runtime mutation and SubmitDeck rejected-journal assertions remain intact.

## Rule Source

Rule source checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`:

- Latest core rules 103 and 115-119 for deck construction, opening setup and mulligan/hand-adjustment context.
- Latest core rules 302, 323.1 and 467 for game-ending/win-state context.
- Latest core rules 649-652, especially 651.3, for surrender, player removal and removed-player inability to choose or otherwise affect the game.

## Validation

- Focused after-finished filter: `3/3` passed.
- P7-9 battlefield-held-seven-units after-finished SubmitDeck scenario: `1/1` passed.
- Changed class: `GameHubJoinTests` `218/218` passed.
- Adjacent Hub / after-finished / protocol / error / Mulligan / Surrender / SubmitDeck / Opening / Raw / ClientIntent filter: `1109/1109` passed.
- Backend full: `8269/8269` passed.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: passed.

## Coordination

- A_MAIN continued directly on `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` branch `main` per user request.
- `rule-audit-remaining-20260615` cadence check after fetch found no commits ahead of `origin/main`.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`.
- No subagent was created.
- Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, official catalog, Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Open

This narrows Mulligan/Surrender/SubmitDeck after-finished error-envelope coverage only. It does not change mulligan semantics, deck legality, surrender/removal runtime behavior, SubmitDeck rejected-journal semantics, match-finished enforcement broadly, priority/focus handling, turn advancement semantics, recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, L2P-RG-005 or final readiness. Project remains **NOT READY**.
