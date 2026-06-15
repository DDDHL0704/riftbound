# Stage 4D-222Z GameHub TapRune Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `TapRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `TAP_RUNE` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` / `" P2 "` -> canonical active player id);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- keeps the accepted `RUNE_TAPPED` and `MANA_GAINED` event sequence stable;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout after the rune is exhausted and mana is added;
- keeps the existing raw-command conflict rejection, raw-journal payload and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` sections 161-167: runes produce mana/rune resources, mana pays numeric costs, rune resources pay trait costs, basic runes have an exhaust/Reaction gain-1 skill, rune pools collect resources before they are spent, rune pools clear at draw-step end and turn end, and gain expressions add resources to the rune pool.
- Core rules 414-415: exhaust/ready are object states, exhaust represents a field object spent for an action or cost, exhaust can be used as a cost only when it can be completed, and ready restores field objects during the ready step or by effect.

This slice only validates protocol-envelope replay behavior for an already implemented official tap-rune scenario; it does not change rune legality, exhaust/ready state, rune-pool accounting, payment consumption, draw/end-turn pool clearing or runtime behavior. The replay assertions are limited to public envelope metadata, normalized routing, accepted event kinds and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TapRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~TapRune|FullyQualifiedName~Rune|FullyQualifiedName~Opening|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2870/2870`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
