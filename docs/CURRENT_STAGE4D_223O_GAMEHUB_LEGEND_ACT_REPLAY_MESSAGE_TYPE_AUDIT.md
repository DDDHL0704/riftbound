# Stage 4D-223O GameHub LegendAct Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `LegendActDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `LEGEND_ACT` raw-command idempotency path preserved normalized player identity, replayed `LEGEND_ABILITY_ACTIVATED` / `EXPERIENCE_SPENT` / `LEGEND_EXHAUSTED` / `CARD_DRAWN` event kinds and accepted payload fields, accepted server tick, experience and hand/deck state, legend exhausted state, raw-journal payload, snapshot/prompt fanout, prompt actions and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed LegendAct snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed LegendAct prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing raw-command conflict rejection, raw-journal payload, experience/legend state and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` rules 107.4 and 173-175: legend zone, legend object identity, immobility and legend skill permissions.
- Core rules 376-381 and 402-405: active-skill activation, choices, total cost determination, cost payment and legality checks.
- Core rule 414: exhausted/rested state and exhaust as a cost for skills.
- Core rules 728-733: experience as a public resource that can be gained and spent.
- `裁判FAQ_251023.pdf` active-skill / trigger-cost timing clarifications were re-checked as adjacent stack-cost guidance.

This slice only validates replay envelope message typing for an already implemented `LEGEND_ACT` scenario; it does not change legend ability legality, experience spending, exhaustion costs, draw behavior, stack timing or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~LegendActDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~protocol|FullyQualifiedName~LegendAct|FullyQualifiedName~Legend|FullyQualifiedName~Experience|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2272/2272`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch, pre-code-commit and pre-docs-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
