# Stage 4D-223F GameHub MoveUnit Replay Message Type Correction Audit

Date: 2026-06-15

Status: accepted as a narrow A_MAIN server-test correction slice on local `main`.

Runtime changed: no. Test coverage changed: yes.

## Scope

This slice corrects the Stage 4D-223E code/doc mismatch for `MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.

The prior Stage 4D-223E documentation described MoveUnit replay `SNAPSHOT` / `PROMPT` message-type assertions, but the pushed code commit `1a5504be` had landed those assertions in the DeclareBattle replay loop. This correction commit `ffa69160` adds the intended MoveUnit assertions:

- replayed MoveUnit snapshot fanout messages are explicitly `MessageType.SNAPSHOT`;
- replayed MoveUnit prompt fanout messages are explicitly `MessageType.PROMPT`;
- existing protocol/schema default checks remain in place for each replayed snapshot and prompt;
- existing movement payload, Roam keyword, raw-command conflict rejection, raw-journal payload and no-mutation checks remain intact.

## Rule Source

Per the core-rule PDF gate in `docs/CURRENT_CORE_RULE_PDF_READING_NOTES.md`, A_MAIN re-checked the extracted local rule text before this correction:

- `/tmp/riftbound_rules_pdf_text/core_rules_260330.txt`
- `《符文战场》核心规则_260330.pdf` rule 144: standard movement and standard movement destinations.
- Core rule 420: movement as moving a game object between field positions, including standard movement as a self-determined action.
- Core rule 810: Roam expands standard movement from one battlefield to another battlefield.

This slice only validates replay envelope message typing for an already implemented `MOVE_UNIT` scenario; it does not change movement legality, Roam permission, battlefield occupancy, battle triggering, priority windows, hidden information or runtime behavior.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation"` passed `2/2`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` passed `217/217`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Hub|FullyQualifiedName~protocol|FullyQualifiedName~MoveUnit|FullyQualifiedName~Move|FullyQualifiedName~Roam|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Official"` passed `2987/2987`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `8260/8260`.
- Mechanical: `git diff --check` passed and anchored conflict-marker scan over `docs src tests` found no matches before the code commit.
- Standing merge source: `rule-audit-remaining-20260615` had no committed changes ahead of `main` during the pre-batch, pre-code-commit and pre-docs-checkpoint checks for this correction.

Note: validation used the project `.NET 10.0.100` runtime from `/Users/dinghaolin/.dotnet` with the same `DOTNET_ROOT`/PATH values as `scripts/dev-env.sh`.

Project remains **NOT READY**.
