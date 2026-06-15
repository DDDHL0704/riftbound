# Stage 4D-223N GameHub RevealCard Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `RevealCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `REVEAL_CARD` raw-command idempotency path preserved normalized player identity, replayed `CARD_REVEALED` / `CARD_PLAYED` / `STACK_ITEM_ADDED` event kinds and accepted payload fields, accepted server tick, standby reveal prompt data, raw-journal payload, stack signature, snapshot/prompt fanout, prompt actions and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed RevealCard snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed RevealCard prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing raw-command conflict rejection, raw-journal payload, stack signature and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `/tmp/riftbound_rules_pdf_text/soulforged_official_faq_260114.txt`
- `《符文战场》核心规则_260330.pdf` rules 108.7, 128 and 129: hand/private information, public hand count and card-back / face-down representation.
- Core rules 107.3, 421 and 811: standby zone and Standby keyword anchors for hidden/face-down standby play.
- Core rules 355.9.a.3, 355.10.a and 355.10.a.1: face-down cards, non-public information and public-zone information boundaries.
- `裁判FAQ_251023.pdf` standby contested-battlefield timing answer: a standby card stays in place until the spell duel closes and remains playable if timing allows.
- `铸魂淬炼系列_官方FAQ_260114.pdf` standby clarifications: standby cards are face-down and can be played as a Reaction from face-down when applicable.

This slice only validates replay envelope message typing for an already implemented `REVEAL_CARD` scenario; it does not change standby legality, hidden information redaction, reaction timing, stack placement or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RevealCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~protocol|FullyQualifiedName~RevealCard|FullyQualifiedName~Standby|FullyQualifiedName~Hidden|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2161/2161`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch, pre-code-commit and pre-docs-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
