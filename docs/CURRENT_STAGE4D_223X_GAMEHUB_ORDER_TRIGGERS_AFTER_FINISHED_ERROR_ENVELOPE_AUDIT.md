# Stage 4D-223X GameHub OrderTriggers After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`OrderTriggersAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `ORDER_TRIGGERS` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no `orderedTriggerIds`, `triggerIds`, `intentId` or `command` leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 303.2.a and 304 anchor turn-player order for simultaneous actions / effects / triggers. Latest core rules 382-383, especially 383.3.d and 383.3.d.1, anchor triggered-skill ordering onto the stack. Latest core rules 401-404 anchor active / triggered skill confirmation, choices and costs around stack placement. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change trigger ordering, stack insertion, optional trigger costs, triggered-skill choice legality, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/OrderTriggers/Trigger filter: `1252/1252`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch, pre-code and docs-checkpoint checks.

Project remains **NOT READY**.
