# Stage 4D-223C GameHub EndTurn Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `EndTurnDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `END_TURN` wrapper-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" alice "` -> `alice`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- keeps the accepted `TURN_ENDED` and `TURN_BEGAN` event sequence stable;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout after active player advances from `alice` to `bob`;
- keeps the existing raw-command conflict rejection, raw-journal payload and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` rule 143.3.b.1: damage is removed from units at each player's turn end.
- Core rules 166-166.1: each player's draw-step end and each turn end clear all players' rune pools.
- Core rules 315-317: turn start, main phase and turn-end phase/steps define the handoff between turns.
- Core rules 318-324 and 334-335: cleanup/special-cleanup and outstanding tasks run through the HOT/FEPR model before game flow advances.

This slice only validates protocol-envelope replay behavior for an already implemented placeholder-rule EndTurn scenario; it does not change turn sequencing, cleanup, rune-pool clearing, damage removal, active-player advancement, task processing or runtime behavior. The replay assertions are limited to public envelope metadata, normalized routing, accepted event kinds and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~EndTurnDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~EndTurn|FullyQualifiedName~Turn|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2875/2875`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice. Before the docs checkpoint it produced source commit `7c763c24`, which A_MAIN merged into `main` as `553edb7a`, adding `docs/CURRENT_RULE_AUDIT_RESIDUAL_PASS_2026-06-15.md`.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
