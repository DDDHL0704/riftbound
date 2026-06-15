# Stage 4D-222W GameHub AssembleEquipment Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `ASSEMBLE_EQUIPMENT` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` -> `P1`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- preserves the accepted payment, rune recycle, cost-paid and equipment-attached payloads;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout and prompt actions;
- keeps the existing raw-command conflict rejection and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/soulforged_official_faq_260114.txt`
- `《符文战场》核心规则_260330.pdf` sections 148-151: equipment as public field objects, equipment entering active, equipment base/battlefield location and special movement when attached to a unit.
- Core rule 434: attach connects field cards, makes one or more cards attached, gives the top card attached text/stat modifications, moves the attached card to the top card's location and detaches from a previous top card when reattached.
- Core rule 818: Assemble is an active-skill keyword; paying the assemble cost attaches the equipment to a chosen controlled unit, the chosen unit is the target and becomes the top card, and the completed attach creates an assembled-equipment event.
- `铸魂淬炼系列_官方FAQ_260114.pdf` Night's Edge assemble clarification: the existing assemble behavior is preserved while standby/equipment wording is clarified for target lookup.

This slice only validates protocol-envelope replay behavior for an already implemented development assemble-equipment/payment scenario; it does not change equipment legality, attach/detach, payment planning, target selection, standby equipment behavior, cleanup recall or runtime behavior. The replay assertions are limited to public envelope metadata, accepted payment/attach payloads and already accepted snapshot/prompt fanout.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~AssembleEquipmentDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~Assemble|FullyQualifiedName~Equipment|FullyQualifiedName~Attach|FullyQualifiedName~Pay|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `3541/3541`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
