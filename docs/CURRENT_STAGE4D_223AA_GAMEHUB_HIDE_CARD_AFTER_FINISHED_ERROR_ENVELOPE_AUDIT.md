# Stage 4D-223AA GameHub HideCard After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`HideCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `HIDE_CARD` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no `intentId`, `HIDE_CARD`, `SubmitIntent`, `MatchFinished` or `MatchFinished` code leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 107.3, 108.7, 128 and 129.3 anchor standby zones, hand privacy, private/hidden information and card-back hiding. Latest core rules 355.9.a.3, 355.10.a.1, 421 and 811 anchor face-down / standby card treatment, public-zone boundaries and the rule that placing a card into standby is not playing it. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change standby placement legality, hidden/private information redaction, card visibility, stack placement, reveal/play-from-standby behavior, win/score/surrender/removal behavior, or redaction text.

## Validation

- Fast-forwarded local `main` to incoming frontend commit `76f4265a` before final validation and code commit.
- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/HideCard/Standby/Raw/ClientIntent filter: `626/626`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch and pre-code checks.

Project remains **NOT READY**.
