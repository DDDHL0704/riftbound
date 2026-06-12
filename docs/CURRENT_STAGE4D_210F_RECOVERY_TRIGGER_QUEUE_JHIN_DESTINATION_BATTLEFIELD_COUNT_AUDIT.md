# Stage 4D-210F Recovery Trigger Queue Jhin Destination Battlefield Count Audit

Date: 2026-06-12 11:11 CST

Project status: **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for spectator replay recovery timing trigger queue validation. Runtime changed: no, server test coverage only.

This slice covers Jhin movement resource destination `BATTLEFIELD:<battlefieldObjectId>` object-location validation while the spectator trigger queue count also mismatches authoritative state.

## Coverage Added

- File: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceDestinationLocationContextDriftWithCountMismatch`
- Main code commit: `e3481cd4`

The test starts from authoritative trigger id `JHIN_MOVE_RESOURCE::3::source-1::BASE::BATTLEFIELD:battlefield-1`, keeps `source-1` as a Jhin unit card controlled by `alice`, keeps authoritative object location `BATTLEFIELD:battlefield-1`, keeps `battlefield-1` and `battlefield-2` as battlefield card objects in `alice`'s `Battlefields`, rewrites the spectator trigger item to forged trigger id `JHIN_MOVE_RESOURCE::3::source-1::BASE::BATTLEFIELD:battlefield-2`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation must reject the replay frame with all of:

- Jhin movement resource destination `BATTLEFIELD:battlefield-2` does not match source object id `source-1` battlefield object id `battlefield-1` in authoritative state object locations.
- Spectator trigger id `trigger-extra` is not present in authoritative state trigger queue.
- Spectator trigger queue count `2` does not match authoritative state trigger queue count `1`.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceDestinationLocationContextDriftWithCountMismatch` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~MatchRecoveryTests` -> `1764/1764`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --filter FullyQualifiedName~Recovery` -> `1769/1769`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8039/8039`
- Mechanical: `git diff --check` passed.
- Conflict scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
- Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth and final readiness remain open.
