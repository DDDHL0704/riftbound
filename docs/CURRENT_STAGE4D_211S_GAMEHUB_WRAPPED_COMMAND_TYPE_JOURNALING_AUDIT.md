# Stage 4D-211S GameHub Wrapped Command Type Journaling Audit

Date: 2026-06-12 17:35 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub whitespace-wrapped known command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentWhitespaceWrappedKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast` now covers a submitted object command whose `cmdType` is `" PASS_PRIORITY "`, plus sentinel top-level and nested audit values.

The test proves:

- the mapper does not trim the whitespace-wrapped known command type into executable `PASS_PRIORITY`;
- the rejected journal entry records command type `" PASS_PRIORITY "` rather than `UNKNOWN` or trimmed `PASS_PRIORITY`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `cmdType`, `PASS_PRIORITY`, or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the wrapped command type intent;
- the rejected journal entry preserves the original raw object payload including the untrimmed `cmdType` and nested sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentWhitespaceWrappedKnownCommandTypeDoesNotExecuteAndPreservesRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `191/191`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `191/191`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8078/8078`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `82f843d2 test: cover wrapped command type hub journaling`

## Remaining Open Gates

This closes only the whitespace-wrapped known command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
