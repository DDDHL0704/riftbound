# Stage 4D-211H GameHub Invalid Reconnect Isolation Audit

Date: 2026-06-12 14:42 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `107389ca`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- New test: `ReconnectWithInvalidTokenDoesNotJoinGroupsOrLeakSessionData`

## Coverage Added

This slice covers GameHub reconnect failure isolation when a client presents an invalid reconnect token.

- The test first joins `alice` and captures the issued reconnect token.
- It then calls `Reconnect("room-a", " alice ", "wrong-token")` to exercise player-id normalization and invalid-token handling together.
- The failed reconnect path must emit only an `INVALID_RECONNECT_TOKEN` error for normalized `alice`.
- The connection must not be added to room/player SignalR groups.
- The hub must not emit `Joined`, `Snapshot`, or `Prompt` messages to caller or group clients.
- The serialized error response must not contain the prior reconnect token.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.ReconnectWithInvalidTokenDoesNotJoinGroupsOrLeakSessionData"` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> `180/180`
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Reconnect"` -> `190/190`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8067/8067`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows GameHub invalid reconnect failure isolation only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
