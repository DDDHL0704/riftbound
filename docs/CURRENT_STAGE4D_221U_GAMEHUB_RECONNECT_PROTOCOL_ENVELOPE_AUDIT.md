# Stage 4D-221U GameHub Reconnect Protocol Envelope Audit

Date: 2026-06-14

Status: accepted as a narrow A_MAIN server-test slice. Project remains **NOT READY**.

## Scope

- Covered successful GameHub reconnect caller envelopes for default protocol/schema versioning.
- Added `ReconnectMessagesCarryProtocolVersionsOnReconnectSnapshotAndPrompt` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins a player, captures the generated reconnect token, reconnects with a trimmed player id, and asserts the `RECONNECT`, `SNAPSHOT` and `PROMPT` messages all carry `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`.

## Files Touched

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_SHARED_COORDINATION_BOARD.md`
- `docs/CURRENT_STAGE4D_221U_GAMEHUB_RECONNECT_PROTOCOL_ENVELOPE_AUDIT.md`

## Validation

- Focused: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests.ReconnectMessagesCarryProtocolVersionsOnReconnectSnapshotAndPrompt"` -> `1/1`.
- Changed class: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` -> `197/197`.
- Adjacent Hub/protocol/reconnect filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~Reconnect"` -> `210/210`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` -> `8231/8231`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests` returned no matches.

## Notes

- Runtime changed: no, server test coverage only.
- Coordination: no subagent or new worktree was created.
- Main code commit: `a122618b`.
- DOC_MATRIX_CURRENT remained clean at `17bde0c3`, last observed 2026-06-14 00:54 CST; no DOC_MATRIX action is requested.
