# Stage 4D-222G GameHub Pass Replay Protocol Envelope Audit

Timestamp: 2026-06-15 09:41 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `Pass` wrapper idempotent replay protocol-envelope versioning.

## Change

- Added `PassReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins Alice and Bob, readies both players, submits the `Pass` wrapper with `pass-replay-protocol-envelope`, then resubmits the same wrapper command through whitespace-normalized player identity with the same client intent id.
- The new assertions prove the idempotent Pass replay path emits replayed group `EVENTS`, `SNAPSHOT` and `PROMPT` envelopes with `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `alice` routing on the replayed events envelope, keeps replayed event kinds stable with the accepted message and keeps the replayed events server tick stable with the accepted message.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PassReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts" --no-restore` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `209/209`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~Pass|FullyQualifiedName~Replay|FullyQualifiedName~ClientIntent" --no-restore` -> `1850/1850`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8243/8243`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `c7888018` (`test: cover pass replay protocol envelope`).
- Worktree note: A_MAIN used temporary isolation worktree `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` on branch `codex/stage4d-222g-pass-replay-protocol` because the primary `/Users/dinghaolin/IdeaProjects/riftbound` worktree remained externally checked out to `codex/rule-audit-local2p-20260615` with an unpushed local-2p merge. No subagent was created.
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-15 09:41 CST.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the Pass wrapper idempotent replay protocol-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
