# Stage 4D-222Y GameHub Mulligan Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `MulliganDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `MULLIGAN` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` / `" P2 "` -> canonical player id);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout after the active player's mulligan resolves and the next player remains actionable;
- keeps the existing raw-command conflict rejection, raw-journal payload and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` sections 115-119: players shuffle/place decks, fair random turn order is established, each player draws four cards, players perform hand adjustment in turn order, each player may set aside up to two hand cards, draws the same number, then recycles the set-aside cards.
- Core rules 108.7, 128 and 129: each player has their own hand, hand cards are private information, hand size is public, and private/hidden cards are represented by card backs.

This slice only validates protocol-envelope replay behavior for an already implemented official mulligan scenario; it does not change opening setup, shuffle/random order, hand selection limits, draw/recycle handling, hand privacy, card-back redaction or runtime behavior. The replay assertions are limited to public envelope metadata, normalized routing, accepted event kinds and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MulliganDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~Mulligan|FullyQualifiedName~Opening|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2754/2754`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
