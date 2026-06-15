# Stage 4D-222R GameHub LegendAct Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `LegendActDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `LEGEND_ACT` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` -> `P1`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves the accepted server tick and event-kind sequence;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout and prompt actions;
- keeps the existing raw-command conflict rejection and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` sections around rules 173-175: legends are game objects, start in the legend zone, are not played in normal gameplay, cannot be moved/destroyed, may be targeted, and may have active skills.
- Core targeting section around rule 355: the legend zone is public and legend references can be valid targets when chosen by a spell/skill text.
- Core keyword sections around rules 806 and 813: Swift and Reaction can grant play/activation permissions to legend skills but do not alter the underlying effect function.

This slice only validates protocol-envelope replay behavior for an already implemented development legend-action scenario; it does not change legend-action legality, costs, target handling, timing permissions, hidden-information visibility or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~LegendActDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~LegendAct|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2064/2064`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8259/8259`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`; the script itself probes `psql`/`redis-cli`, which were not on this shell PATH.

Project remains **NOT READY**.
