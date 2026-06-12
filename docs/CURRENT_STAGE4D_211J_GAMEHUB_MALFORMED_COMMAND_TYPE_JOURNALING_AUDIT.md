# Stage 4D-211J GameHub Malformed Command Type Journaling Audit

Date: 2026-06-12 14:58 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for GameHub malformed command type handling at the mapper/hub boundary. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

## Coverage Added

`SubmitIntentMalformedCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast` now covers a submitted command whose `cmdType` is a non-string array containing `FLIP_TABLE`, plus sentinel top-level and nested payload fields.

The test proves:

- the command reaches the mapper fallback and records rejected journal command type `UNKNOWN`;
- the caller receives only stable `UNSUPPORTED_COMMAND` with Chinese message `当前命令不受服务端支持。`;
- serialized error output does not echo `FLIP_TABLE` or the sentinel payload;
- no caller/group events, snapshots or prompts are emitted for the rejected command;
- exactly one new rejected journal entry is recorded for the malformed command intent;
- the rejected journal entry preserves the original raw JSON, including the array `cmdType`, `clientNote`, and nested audit sentinel;
- the rejected journal entry has no emitted events.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests.SubmitIntentMalformedCommandTypePreservesUnknownRawPayloadInJournalWithoutBroadcast"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHubJoinTests"` -> passed `182/182`.
- Adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GameHub|FullyQualifiedName~UnsupportedCommand"` -> passed `182/182`.
- Backend full: `dotnet test Riftbound.slnx` -> passed `8069/8069`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `22242b1e test: cover malformed command type hub journaling`

## Remaining Open Gates

This closes only the malformed command type raw-command journaling/no-broadcast test shard. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
