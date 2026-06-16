# Stage 4D-223W GameHub AssignCombatDamage After-Finished Error Envelope Audit

Date: 2026-06-16
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`AssignCombatDamageAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `ASSIGN_COMBAT_DAMAGE` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no `assignments`, `battleId`, `battlefieldId`, `sourceObjectId`, `targetObjectId` or `command` leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 417.1.a, 417.3.a, 417.6.c and 460.2.c-d anchor combat damage assignment as distinct from damage dealing and the simultaneous damage sequence after assignment. Latest core rules 815 and 826 anchor Barrier and Back Row assignment constraints. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change combat damage assignment legality, simultaneous damage, Barrier / Back Row behavior, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused test: `1/1`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/AssignCombatDamage filter: `290/290`.
- Backend full `Riftbound.slnx`: `8262/8262`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `main` during pre-batch, pre-code and docs-checkpoint checks.

Project remains **NOT READY**.
