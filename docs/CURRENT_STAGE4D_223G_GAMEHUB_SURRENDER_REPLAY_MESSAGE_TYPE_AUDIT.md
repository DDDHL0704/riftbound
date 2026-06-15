# Stage 4D-223G GameHub Surrender Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `SurrenderDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `SURRENDER` raw-command idempotency path preserved normalized player identity, replayed `MATCH_WON` payload fields, snapshot/prompt fanout, server tick and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed Surrender snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed Surrender prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing match-winner, surrendered-player, reason, raw-command conflict rejection, raw-journal payload and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` rules 649-650: surrender and the player's ability to surrender at any time.
- Core rule 651: surrender removes that player from the current game and a 1v1 remaining player wins.
- Core rule 652: player-removal cleanup after surrender.

This slice only validates replay envelope message typing for an already implemented `SURRENDER` scenario; it does not change surrender legality, player removal, team handling, game continuation, winner determination, hidden information or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~SurrenderDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~Surrender|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2738/2738`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch, pre-code-commit and pre-docs-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
