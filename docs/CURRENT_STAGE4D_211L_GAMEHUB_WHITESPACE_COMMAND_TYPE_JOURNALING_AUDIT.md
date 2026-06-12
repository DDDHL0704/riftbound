# Stage 4D-211L GameHub Whitespace Command Type Journaling Audit

Date: 2026-06-12 15:14 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub whitespace-only command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentWhitespaceCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast` now covers a submitted object command whose `cmdType` value is whitespace-only and whose raw payload also contains sentinel top-level and nested audit values.

The test proves:

- the command reaches the mapper fallback and records rejected journal command type `UNKNOWN`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType` or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the whitespace command type intent;
- the rejected journal entry preserves the original raw object payload, including the untrimmed whitespace `cmdType` and nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentWhitespaceCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `184/184`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `184/184`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8071/8071`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `831a4a93 test: cover whitespace command type hub journaling`

## Remaining Open Gates

This closes only the whitespace command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
