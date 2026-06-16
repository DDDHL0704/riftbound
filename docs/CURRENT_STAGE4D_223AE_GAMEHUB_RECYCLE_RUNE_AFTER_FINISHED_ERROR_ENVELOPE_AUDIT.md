# Stage 4D-223AE GameHub RecycleRune After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`RecycleRuneAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `RECYCLE_RUNE` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/nested-sentinel/clientIntent/raw/secret/internal/debug leakage
- no `intentId`, `RECYCLE_RUNE`, `SubmitIntent`, `MatchFinished` or `MatchFinished` code leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 160-166 anchor rune identity, rune-deck return behavior and rune-pool resource storage/spending. Latest core rule 163.2.b anchors the basic rune recycle-for-rune-power skill. Latest core rule 416 anchors recycle as returning cards to the correct deck bottom, including runes returning to the rune deck. Latest core rule 429 anchors resource gain into the rune pool. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change recycle legality, rune-deck bottom ordering, resource generation, rune-pool spending, hidden rune-deck order, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/RecycleRune/TapRune/Rune/Resource/Pay/Raw/ClientIntent filter: `2500/2500`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch and pre-code checks.

Project remains **NOT READY**.
