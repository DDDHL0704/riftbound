# Stage 4D-211O GameHub Boolean Command Type Journaling Audit

Date: 2026-06-12 15:37 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub boolean command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentBooleanCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast` now covers a submitted object command whose `cmdType` value is JSON boolean `true` and whose raw payload contains sentinel top-level and nested audit values.

The test proves:

- the command reaches the mapper fallback and records rejected journal command type `UNKNOWN`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType` or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the boolean command type intent;
- the rejected journal entry preserves the original raw object payload including the boolean `cmdType` and nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentBooleanCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `187/187`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `187/187`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8074/8074`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `5e34820e test: cover boolean command type hub journaling`

## Remaining Open Gates

This closes only the boolean command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
