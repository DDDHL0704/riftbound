# Stage 4D-211R GameHub Duplicate Command Type Journaling Audit

Date: 2026-06-12 17:26 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub duplicate command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentDuplicateCommandTypeUsesLastMalformedValueAndPreservesRawPayloadInJournalWithoutBroadcast` now covers a submitted object command with duplicate `cmdType` properties where the first value is known `PASS_PRIORITY` and the final value is malformed `["FLIP_TABLE"]`, plus sentinel top-level and nested audit values.

The test proves:

- the final malformed `cmdType` value controls mapper behavior and records rejected journal command type `UNKNOWN`;
- the smuggled known first `cmdType` value does not execute as `PASS_PRIORITY`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType`, `PASS_PRIORITY`, `FLIP_TABLE`, or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the duplicate command type intent;
- the rejected journal entry preserves the original raw object payload including both `cmdType` properties and the nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentDuplicateCommandTypeUsesLastMalformedValueAndPreservesRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `190/190`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `190/190`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8077/8077`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `8618e756 test: cover duplicate command type hub journaling`

## Remaining Open Gates

This closes only the duplicate command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
