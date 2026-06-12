# Stage 4D-211T GameHub Lowercase Command Type Journaling Audit

Date: 2026-06-12 17:44 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub lowercase known-looking command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentLowercaseKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast` now covers a submitted object command whose `cmdType` is `"pass_priority"`, plus sentinel top-level and nested audit values.

The test proves:

- the mapper does not normalize lowercase `pass_priority` into executable `PASS_PRIORITY`;
- the rejected journal entry records command type `"pass_priority"` rather than `UNKNOWN` or uppercase `PASS_PRIORITY`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType`, `pass_priority`, `PASS_PRIORITY`, or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the lowercase command type intent;
- the rejected journal entry preserves the original raw object payload including the lowercase `cmdType` and nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentLowercaseKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `192/192`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `192/192`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8079/8079`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `fb90cc24 test: cover lowercase command type hub journaling`

## Remaining Open Gates

This closes only the lowercase known command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
