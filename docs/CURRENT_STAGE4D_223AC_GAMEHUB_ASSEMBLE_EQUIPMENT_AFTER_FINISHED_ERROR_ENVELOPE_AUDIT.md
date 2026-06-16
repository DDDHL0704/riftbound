# Stage 4D-223AC GameHub AssembleEquipment After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`AssembleEquipmentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `ASSEMBLE_EQUIPMENT` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no `intentId`, `ASSEMBLE_EQUIPMENT`, `SubmitIntent`, `MatchFinished` or `MatchFinished` code leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 148-151 anchor equipment identity, public information and location. Latest core rules 403-404 anchor cost payment command-surface context. Latest core rules 434 and 452 anchor attach semantics and unattached equipment recall. Latest core rule 818 anchors `Assemble` as an active-skill keyword that pays a cost, targets a unit and attaches the equipment to that unit. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change equipment legality, assemble costs, attach semantics, target legality, hidden/private visibility, stack behavior, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/AssembleEquipment/Assemble/Equipment/Attach/Pay/Raw/ClientIntent filter: `2413/2413`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch and pre-code checks.

Project remains **NOT READY**.
