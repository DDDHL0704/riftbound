# Stage 4D-223R GameHub SubmitDeck Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`SubmitDeckDuplicateClientIntentReorderedRawPayloadReplaysButChangedRawConflictsWithoutMutation` now proves the idempotent reordered raw-payload `SUBMIT_DECK` replay emits:

- a replayed group `EVENTS` envelope with `MessageType.EVENTS` and default protocol/schema versions;
- replayed group `SNAPSHOT` fanout messages with `MessageType.SNAPSHOT` and default protocol/schema versions;
- replayed group `PROMPT` fanout messages with `MessageType.PROMPT` and default protocol/schema versions.

The existing assertions remain intact: the replay preserves event kinds, server tick, snapshot/prompt player fanout, prompt actions and journal count, while a later changed raw payload with the same client intent id still returns `CLIENT_INTENT_CONFLICT` without broadcasting or mutating the journal.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local core rule text before documenting this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Core rules 115-119: opening setup places main decks and rune decks, determines turn order, draws opening hands, performs hand adjustment and starts the game.
- Core rule 108.7: hands are player-owned private information and hand size is public information.
- Core rule 129.3: cards in private or hidden states, including main-deck cards and hand cards, are represented by backs to hide face information.

This was a pure GameHub protocol-envelope coverage change, not a runtime opening-rule change. The test asserts envelope types and protocol defaults only; it does not expose hidden card identities beyond the already submitted command payload, private deck order, random seeds or other private state.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~SubmitDeckDuplicateClientIntentReorderedRawPayloadReplaysButChangedRawConflictsWithoutMutation` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~GameHubJoinTests` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitDeck|FullyQualifiedName~Opening|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2752/2752`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8262/8262`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
