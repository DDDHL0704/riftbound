# Stage 4D-223AH GameHub SubmitIntent And ActivateAbility After-Finished Error Envelope Audit

Date: 2026-06-16 11:52 CST
Status: accepted as a narrow A_MAIN server-test slice on local `main`.
Runtime changed: no.
Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`SubmitIntentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the generic after-finished `SubmitIntent` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

`ActivateAbilityAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate` now proves the after-finished `ACTIVATE_ABILITY` caller error is a `MessageType.ERROR` envelope, preserves normalized `roomId` / `P1` routing, and carries default protocol/schema versions.

Existing assertions remain intact:

- stable `MatchFinished` code/message
- no sentinel/clientIntent/raw/secret/internal/debug leakage
- no command or `SubmitIntent` / `MatchFinished` internal text leakage
- no caller or group events/snapshots/prompts/errors beyond the caller error
- no journal growth
- no runtime mutation for either player snapshot

## Rule Source

PDF gate checked through `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md` and `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`.

Latest core rules 302, 323.1 and 467 anchor the game-ending and win-state context. Latest core rules 401-404 anchor active / triggered skill stack placement, choices, cost determination and payment for active-skill command-surface context. Latest core rules 649-652, especially 651.3, anchor player removal and the rule that a removed player cannot choose or otherwise affect the game after removal.

This is protocol-error-envelope coverage only. It does not change active-skill legality, target selection, cost payment, stack placement, priority/focus handling, win/score/surrender/removal behavior, or redaction text.

## Validation

- Focused tests: `2/2`.
- Changed class `GameHubJoinTests`: `217/217`.
- Adjacent Hub/after-finished/protocol/error/ActivateAbility/SubmitIntent/PayCost/Trigger/Stack/Raw/ClientIntent filter: `2066/2066`.
- Backend full `Riftbound.slnx`: `8267/8267`.
- `git diff --check` passed before code commit.
- Anchored conflict-marker scan over `docs`, `src` and `tests` passed before code commit.
- Standing `rule-audit-remaining-20260615` cadence check found no commits ahead of `origin/main`; DOC_MATRIX_CURRENT remained clean at `17bde0c3`.

Project remains **NOT READY**.
