# Stage 4D-223Y GameHub ChooseHandCards After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`ChooseHandCardsAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `CHOOSE_HAND_CARDS` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no `choiceId`, `choiceWindow`, `chosenObjectIds`, `handChoiceId`, `handChoiceWindow`, `selectedObjectIds`, `chosenCardIds`, `handChoices`, `choosingPlayerId`, `requiredCount`, `maxCount`, `reason`, `effectKind`, `HAND_CHOICE` or `command` leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 115-119 anchor opening setup, turn order, opening draw and hand adjustment. Latest core rules 108.7, 128 and 129.3 anchor hand privacy, public hand size and card-back hiding for private hand/deck information. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change opening setup, hand adjustment, mulligan choice legality, private hand/deck visibility, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/ChooseHandCards/Mulligan/Opening/Hand filter: `988/988`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch, pre-code and docs-checkpoint checks.

Project remains **NOT READY**.
