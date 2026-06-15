# Stage 4D-222P GameHub MoveUnit Replay Protocol Envelope Audit

Timestamp: 2026-06-15 11:26 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `MOVE_UNIT` raw-command idempotent replay protocol-envelope versioning.

## Change

- Extended `MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test already joins two players, seeds the development `battlefield-static-roam` scenario, accepts a `MOVE_UNIT` raw command, replays the same raw command with the same client intent id, then verifies changed raw payloads conflict without mutation.
- The new assertions make the replay submit through whitespace-normalized player identity, prove the replayed `EVENTS` envelope carries the normalized player id plus `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, and prove replayed `SNAPSHOT` and `PROMPT` envelopes carry the same protocol/schema defaults while preserving stable event kinds, server tick and fanout.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MoveUnitDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation" --no-restore` -> `2/2`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `217/217`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~MoveUnit|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development" --no-restore` -> `2062/2062`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8259/8259`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs`, `src` and `tests` had no matches.
