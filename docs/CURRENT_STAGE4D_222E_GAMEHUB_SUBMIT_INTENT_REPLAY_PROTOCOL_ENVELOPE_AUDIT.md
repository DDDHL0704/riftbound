# Stage 4D-222E GameHub SubmitIntent Replay Protocol Envelope Audit

Timestamp: 2026-06-15 09:20 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub `SubmitIntent` idempotent replay protocol-envelope versioning.

## Change

- Added `SubmitIntentReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins Alice and Bob, starts the match, submits `PASS_PRIORITY` with `intent-replay-protocol-envelope`, then resubmits the same raw command through whitespace-normalized player identity with the same client intent id.
- The new assertions prove the idempotent replay path emits replayed group `EVENTS`, `SNAPSHOT` and `PROMPT` envelopes with `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `alice` routing on the events envelope, and keeps the replayed `EVENTS` server tick stable with the accepted message.

## Validation

- Restore setup for isolated worktree: `/Users/dinghaolin/.dotnet/dotnet restore Riftbound.slnx` passed.
- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SubmitIntentReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts" --no-restore` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `207/207`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SubmitIntent|FullyQualifiedName~Replay|FullyQualifiedName~Raw|FullyQualifiedName~Mapper|FullyQualifiedName~ClientIntent" --no-restore` -> `2011/2011`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8241/8241`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `3518e584` (`test: cover submit intent replay protocol envelope`).
- Worktree note: A_MAIN used temporary isolation worktree `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` on branch `codex/stage4d-222e-protocol-envelope` because the primary `/Users/dinghaolin/IdeaProjects/riftbound` worktree was externally checked out to `codex/rule-audit-local2p-20260615` with an unpushed local-2p merge. No subagent was created.
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-15 09:20 CST.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the SubmitIntent idempotent replay protocol-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
