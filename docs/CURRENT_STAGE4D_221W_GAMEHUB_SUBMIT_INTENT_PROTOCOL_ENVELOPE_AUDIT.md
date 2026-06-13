# Stage 4D-221W GameHub SubmitIntent Protocol Envelope Audit

Date: 2026-06-14

Status: accepted as a narrow A_MAIN server-test slice. Project remains **NOT READY**.

## Scope

- Covered accepted GameHub `SubmitIntent` group envelopes for default protocol/schema versioning.
- Added `SubmitIntentMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins both players, starts the match, submits `PASS_PRIORITY` through the JSON mapper path with a trimmed player id, and asserts the resulting `EVENTS`, `SNAPSHOT` and `PROMPT` messages carry `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`.

## Files Touched

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_221W_GAMEHUB_SUBMIT_INTENT_PROTOCOL_ENVELOPE_AUDIT.md`

## Validation

- Focused: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts"` -> `1/1`.
- Changed class: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` -> `199/199`.
- Adjacent Hub/protocol/SubmitIntent/raw/mapper filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitIntent|FullyQualifiedName~Raw|FullyQualifiedName~Mapper"` -> `490/490`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` -> `8233/8233`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests` returned no matches.

## Notes

- Runtime changed: no, server test coverage only.
- Coordination: no subagent or new worktree was created.
- Main code commit: `f293ec35`.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`, last observed 2026-06-14 01:08 CST; no DOC_MATRIX action is requested.
