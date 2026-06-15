# Stage 4D-223A GameHub RecycleRune Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `RecycleRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `RECYCLE_RUNE` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` / `" P2 "` -> canonical active player id);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- keeps the accepted `RUNE_RECYCLED` and `POWER_GAINED` event payload checks stable;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout after the rune leaves base and the rune pool gains mana plus power;
- keeps the existing raw-command conflict rejection, raw-journal payload and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` section 160.2.b: recycled runes return to the rune deck rather than the main deck.
- Core rules 161-167: runes produce mana/rune resources, mana pays numeric costs, rune resources pay trait costs, rune pools collect resources before they are spent, rune pools clear at draw-step end and turn end, and gain expressions add resources to the rune pool.
- Core rules 163.2.b and 163.2.b.1: a basic rune's recycle skill is `回收此牌：[反应] — [获得][C]`, and the gained rune resource has the recycled rune's trait.
- Core rules 416.1-416.3: recycle moves selected cards to the bottom of the corresponding deck, recycled runes return to the rune deck, and recycle-as-cost must be completable before the cost can be paid.

This slice only validates protocol-envelope replay behavior for an already implemented official recycle-rune scenario; it does not change rune legality, rune deck/order, rune-pool accounting, payment consumption, draw/end-turn pool clearing, hidden/private deck order or runtime behavior. The replay assertions are limited to public envelope metadata, normalized routing, accepted event kinds/payloads and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecycleRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~RecycleRune|FullyQualifiedName~Rune|FullyQualifiedName~Opening|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2870/2870`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
