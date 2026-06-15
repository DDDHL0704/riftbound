# Stage 4D-223L GameHub AssembleEquipment Replay Message Type Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test already proved the replayed `ASSEMBLE_EQUIPMENT` raw-command idempotency path preserved normalized player identity, replayed `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` / `EQUIPMENT_ATTACHED` event kinds and accepted payload fields, snapshot/prompt fanout, prompt actions, server tick and protocol/schema defaults. This slice additionally locks the replayed group fanout message types:

- replayed AssembleEquipment snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed AssembleEquipment prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing raw-command conflict rejection, raw-journal payload, payment-resource, attach payload and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/soulforged_official_faq_260114.txt`
- `《符文战场》核心规则_260330.pdf` rules 148-151: equipment identity, public information and equipment location.
- Core rule 434: attach action semantics and attach not being movement.
- Core rule 818: Assemble as an active-skill keyword, assemble cost payment and target unit attachment.
- `铸魂淬炼系列_官方FAQ_260114.pdf` Night's Edge clarification: assemble/auto-attach wording remains functionally unchanged.
- `铸魂淬炼系列_官方FAQ_260114.pdf` Ezreal clarification: assemble costs are activation costs, not optional extra costs.

This slice only validates replay envelope message typing for an already implemented `ASSEMBLE_EQUIPMENT` scenario; it does not change equipment legality, assemble cost payment, attach semantics, hidden information or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~protocol|FullyQualifiedName~Assemble|FullyQualifiedName~Equipment|FullyQualifiedName~Attach|FullyQualifiedName~Pay|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `3541/3541`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8261/8261`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-code-commit checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
