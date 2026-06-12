# Stage 4D-211I GameHub Unsupported Raw Command Journaling Audit

Date: 2026-06-12 14:50 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `3954e831`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
- New test: `SubmitIntentUnsupportedCommandPreservesRawPayloadInJournalWithoutBroadcast`

## Coverage Added

This slice covers GameHub unsupported raw-command handling after both players are joined and ready.

- The test submits unsupported `FLIP_TABLE` JSON with sentinel `clientNote` and nested audit payload fields.
- The client receives only the stable `UNSUPPORTED_COMMAND` error message.
- The serialized client error response must not echo the sentinel raw payload.
- Caller and group clients must receive no events, snapshots, or prompts for the rejected unsupported command.
- The match journal records exactly one rejected command entry for the unsupported intent.
- The rejected journal entry preserves the original raw command JSON, including the sentinel top-level and nested payload fields, for recovery audit.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentUnsupportedCommandPreservesRawPayloadInJournalWithoutBroadcast"` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> `181/181`
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> `181/181`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8068/8068`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows GameHub unsupported raw-command journaling and no-broadcast isolation only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
