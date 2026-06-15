# Stage 4D-223S GameHub SeedScenario Duplicate Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

`SeedScenarioDuplicateClientIntentRawPayloadReplaysButChangedScenarioConflictsWithoutMutation` now proves the idempotent development `SeedScenario` replay path emits:

- a replayed group `EVENTS` envelope with `MessageType.EVENTS` and default protocol/schema versions;
- replayed group `SNAPSHOT` fanout messages with `MessageType.SNAPSHOT` and default protocol/schema versions;
- replayed group `PROMPT` fanout messages with `MessageType.PROMPT` and default protocol/schema versions.

The existing assertions remain intact: the accepted development seed writes one journal entry, the replay keeps the journal count stable, the replay preserves the accepted server tick, event kinds, event descriptions and snapshot/prompt player fanout, and a later request with the same client intent id but a changed scenario still returns `CLIENT_INTENT_CONFLICT` without broadcasting or mutating the journal.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local core rule text before documenting this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- Core rules 115-119: opening setup places main decks and rune decks, determines turn order, draws opening hands, performs hand adjustment and starts the game.
- Core rule 108.7: hands are player-owned private information and hand size is public information.
- Core rule 129.3: cards in private or hidden states, including main-deck cards and hand cards, are represented by backs to hide face information.

`SeedScenario` is a development-only test harness entry point, not a gameplay action defined by the PDF rules. This was a pure GameHub protocol-envelope coverage change around replay/idempotency and conflict handling; it does not add private-state assertions, reveal hidden card identities, expose private deck order or change the seeded gameplay state.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~SeedScenarioDuplicateClientIntentRawPayloadReplaysButChangedScenarioConflictsWithoutMutation` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~GameHubJoinTests` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SeedScenario|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `1994/1994`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8262/8262`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
