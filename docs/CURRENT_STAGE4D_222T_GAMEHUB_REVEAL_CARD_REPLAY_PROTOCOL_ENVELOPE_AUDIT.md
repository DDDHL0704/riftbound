# Stage 4D-222T GameHub RevealCard Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `RevealCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `REVEAL_CARD` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` -> `P1`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- preserves the revealed-card event player/source/card payload;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout, prompt actions and stack signature;
- keeps the existing raw-command conflict rejection and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` sections around rules 128-129: private/hidden cards and face-down card backs hide card faces from players who are not allowed to view them.
- Core rules around rules 421 and 811: standby places a card face down, playing from face-down standby opens the stack, and standby play permissions preserve the card's normal skills and instructions.
- Core rules around rule 813: Reaction is a permission keyword and does not alter the underlying card/effect function.

This slice only validates protocol-envelope replay behavior for an already implemented development standby-reaction reveal scenario; it does not change RevealCard legality, standby timing, stack resolution, Reaction permissions, hidden-information visibility, face-down reveal handling or runtime behavior. The new replay assertions do not require clients or spectators to see private card faces beyond the already accepted reveal/play/stack event payloads.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RevealCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~RevealCard|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2046/2046`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no commits ahead of `main` during the pre-batch and pre-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`; the script itself probes `psql`/`redis-cli`, which were not on this shell PATH.

Project remains **NOT READY**.
