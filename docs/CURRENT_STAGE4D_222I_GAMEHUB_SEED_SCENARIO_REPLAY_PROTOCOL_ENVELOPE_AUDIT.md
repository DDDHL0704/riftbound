# Stage 4D-222I GameHub SeedScenario Replay Protocol Envelope Audit

Timestamp: 2026-06-15 10:00 CST

Owner: A_MAIN

Scope: direct single-agent server-test shard for GameHub development `SeedScenario` idempotent replay protocol-envelope versioning.

## Change

- Added `SeedScenarioReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts` in `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`.
- The test joins P1 and P2 with a development host environment, accepts `SeedScenario` under `seed-replay-protocol-envelope`, then resubmits the same scenario through whitespace-normalized player identity with the same client intent id.
- The new assertions prove the idempotent SeedScenario replay path emits replayed group `EVENTS`, `SNAPSHOT` and `PROMPT` envelopes with `ProtocolDefaults.ProtocolVersion` and `ProtocolDefaults.SchemaVersion`, preserves normalized `P1` routing on the replayed events envelope, keeps replayed event kinds stable with the accepted message, keeps replayed snapshot/prompt player fanout stable and keeps the replayed events server tick stable with the accepted message.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SeedScenarioReplayMessagesCarryProtocolVersionsOnEventsSnapshotsAndPrompts" --no-restore` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests" --no-restore` -> `211/211`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Protocol|FullyQualifiedName~SeedScenario|FullyQualifiedName~Replay|FullyQualifiedName~ClientIntent|FullyQualifiedName~Development" --no-restore` -> `1796/1796`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` -> `8245/8245`.
- Mechanical: `git diff --check` passed; anchored conflict-marker scan over `docs src tests` had no matches.

## Coordination

- Main code commit: `193fdd8d` (`test: cover seed scenario replay protocol envelope`).
- Worktree note: A_MAIN used temporary isolation worktree `/Users/dinghaolin/MyProjects/riftbound-stage4d-222e-protocol-envelope` on branch `codex/stage4d-222i-seed-scenario-replay-protocol` because the primary `/Users/dinghaolin/IdeaProjects/riftbound` worktree remained externally checked out to `codex/rule-audit-local2p-20260615` with an unpushed local-2p merge. No subagent was created.
- DOC_MATRIX_CURRENT actual worktree `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current` was clean at `17bde0c3`, observed 2026-06-15 10:00 CST.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

## Remaining Risk

This closes only the development SeedScenario idempotent replay protocol-envelope versioning slice. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
