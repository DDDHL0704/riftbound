# Stage 4D-222D GameHub SubmitIntent Conflict Protocol Envelope Audit

Timestamp: 2026-06-15 09:02 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `SubmitIntent` duplicate client-intent conflict protocol-envelope versioning.

## Change

- Added `SubmitIntentDuplicateConflictMessagesCarryProtocolVersionsOnError` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins Alice and Bob, starts the match, submits `PASS_PRIORITY` with `intent-conflict-protocol-envelope`, then resubmits the same client intent id through whitespace-normalized player identity with a changed `END_TURN` command.
- The new assertions prove the duplicate-client-intent conflict path emits a caller `ERROR` message with `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `room-a` / `alice` routing, reports `ErrorCodes.ClientIntentConflict`, and does not emit group `EVENTS`, `SNAPSHOT` or `PROMPT` messages.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SubmitIntentDuplicateConflictMessagesCarryProtocolVersionsOnError" --no-restore` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `206/206`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitIntent|FullyQualifiedName~Error|FullyQualifiedName~Raw|FullyQualifiedName~Mapper|FullyQualifiedName~ClientIntent|FullyQualifiedName~Conflict" --no-restore` -> `536/536`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8240/8240`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `6d7da93d` (`test: cover submit intent conflict protocol envelope`).
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-15 09:02 CST.
- No subagent or new worktree was created.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the SubmitIntent duplicate-client-intent conflict error-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
