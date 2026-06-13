# Stage 4D-221V GameHub Request Snapshot Protocol Envelope Audit

Date: 2026-06-14

Status: accepted as a narrow A_MAIN server-test slice. Project remains **NOT READY**.

## Scope

- Covered successful GameHub request-snapshot caller envelopes for default protocol/schema versioning.
- Added `RequestSnapshotMessagesCarryProtocolVersionsOnSnapshotAndPrompt` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins a player, requests a snapshot with a trimmed player id, and asserts the `SNAPSHOT` and `PROMPT` messages carry `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`.

## Files Touched

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_221V_GAMEHUB_REQUEST_SNAPSHOT_PROTOCOL_ENVELOPE_AUDIT.md`

## Validation

- Focused: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests.RequestSnapshotMessagesCarryProtocolVersionsOnSnapshotAndPrompt"` -> `1/1`.
- Changed class: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` -> `198/198`.
- Adjacent Hub/protocol/reconnect/request-snapshot filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~Reconnect|FullyQualifiedName~RequestSnapshot"` -> `211/211`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` -> `8232/8232`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests` returned no matches.

## Notes

- Runtime changed: no, server test coverage only.
- Coordination: no subagent or new worktree was created.
- Main code commit: `598bad16`.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`, last observed 2026-06-14 01:02 CST; no DOC_MATRIX action is requested.
