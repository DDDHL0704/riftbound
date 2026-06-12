# Stage 4D-211Q GameHub Object Command Type Journaling Audit

Date: 2026-06-12 17:17 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub object command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentObjectCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast` now covers a submitted object command whose `cmdType` value is a JSON object containing `name: "FLIP_TABLE"` plus a nested sentinel, while the top-level raw payload also contains sentinel `clientNote` and nested audit values.

The test proves:

- the command reaches the mapper fallback and records rejected journal command type `UNKNOWN`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType`, `FLIP_TABLE`, or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the object command type intent;
- the rejected journal entry preserves the original raw object payload including the object `cmdType`, its nested sentinel, and the top-level nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentObjectCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `189/189`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `189/189`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8076/8076`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `cd373186 test: cover object command type hub journaling`

## Remaining Open Gates

This closes only the object command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
