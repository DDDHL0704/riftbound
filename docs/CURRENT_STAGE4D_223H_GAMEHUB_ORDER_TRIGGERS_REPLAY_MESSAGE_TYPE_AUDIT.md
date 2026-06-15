# Stage 4D-223H GameHub OrderTriggers Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `ORDER_TRIGGERS` raw-command idempotency path preserved normalized player identity, replayed `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK` event kinds, ordered trigger ids, snapshot/prompt fanout, server tick and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed OrderTriggers snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed OrderTriggers prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing trigger ordering, stack handoff, raw-command conflict rejection, raw-journal payload and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` rules 327 and 333: stack zone / stack creation for cards and skills.
- Core rules 342 and 376: spell-duel and active-skill stack timing anchors.
- `裁判FAQ_251023.pdf` questions 2.2 and 2.3: simultaneous triggered-skill ordering and battle initial-stack ordering.

This slice only validates replay envelope message typing for an already implemented `ORDER_TRIGGERS` scenario; it does not change trigger ordering, skill confirmation, stack priority, battle initial trigger timing, hidden information or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~OrderTriggersDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~OrderTriggers|FullyQualifiedName~Trigger|FullyQualifiedName~Stack|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2810/2810`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch, pre-code-commit and pre-docs-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
