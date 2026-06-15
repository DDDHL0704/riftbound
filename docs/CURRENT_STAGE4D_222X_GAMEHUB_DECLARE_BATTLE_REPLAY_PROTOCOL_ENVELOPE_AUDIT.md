# Stage 4D-222X GameHub DeclareBattle Replay Protocol Envelope Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice extends `DeclareBattleDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The test now proves the replayed `DECLARE_BATTLE` raw-command idempotency path:

- accepts the replay through whitespace-normalized player identity (`" P1 "` -> `P1`);
- emits a replayed `EVENTS` message with default protocol/schema versions;
- preserves normalized player routing, the accepted server tick and event-kind sequence;
- preserves the accepted battle-declared, damage-applied and unit-destroyed payloads already asserted by the existing test;
- emits replayed `SNAPSHOT` and `PROMPT` messages with default protocol/schema versions;
- preserves accepted snapshot/prompt player fanout and prompt actions;
- keeps the existing raw-command conflict rejection and no-mutation checks intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this slice:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `/tmp/riftbound_rules_pdf_text/judge_faq_251023.txt`
- `《符文战场》核心规则_260330.pdf` sections 454-461: battle identity, pending battle state, battles occurring between exactly two players' units, battle spell-duel timing, battle damage, cleanup and battle result.
- Core rule 460.2.c / 460.2.c.1-a / 460.2.d: attacker assignment first, defender assignment after that, assignment is not damage, and after all damage is assigned it is dealt simultaneously.
- `裁判FAQ_251023.pdf` questions 6.1-6.4: combat damage assignment is distinct from damage, overassignment restrictions apply, same-priority choices are selected by the assigning player, and conflicting requirements are chosen by that player.

This slice only validates protocol-envelope replay behavior for an already implemented development declare-battle scenario; it does not change battle declaration legality, attacker/defender identity, combat damage assignment, simultaneous damage, cleanup, scoring or runtime behavior. L2P-RG-005 remains open for a later state-model slice covering independent attacker/defender assignment choices.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~DeclareBattleDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Battle|FullyQualifiedName~Combat|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development"` passed `2713/2713`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch and pre-checkpoint checks for this slice.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
