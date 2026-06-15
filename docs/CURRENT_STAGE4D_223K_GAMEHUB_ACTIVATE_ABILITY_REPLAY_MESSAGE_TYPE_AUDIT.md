# Stage 4D-223K GameHub ActivateAbility Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `ActivateAbilityDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `ACTIVATE_ABILITY` raw-command idempotency path preserved normalized player identity, replayed `ABILITY_ACTIVATED` / `BATTLEFIELD_TRIGGER_RESOLVED` / `EXPERIENCE_GAINED` event kinds and accepted payload fields, snapshot/prompt fanout, prompt actions, server tick and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed ActivateAbility snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed ActivateAbility prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing raw-command conflict rejection, raw-journal payload, replayed event payload and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` rules 347 and 376: skill activation permission in spell-duel/open opportunity windows and active-skill process anchors.
- Core rules 403-404: active/triggered skill total-cost determination and payment.
- `裁判FAQ_251023.pdf` question 3.1: resource-gain skill confirmation does not pass priority or focus.

This slice only validates replay envelope message typing for an already implemented `ACTIVATE_ABILITY` scenario; it does not change active-skill legality, cost payment, priority/focus passing, hidden information or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ActivateAbilityDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~protocol|FullyQualifiedName~ActivateAbility|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2076/2076`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
