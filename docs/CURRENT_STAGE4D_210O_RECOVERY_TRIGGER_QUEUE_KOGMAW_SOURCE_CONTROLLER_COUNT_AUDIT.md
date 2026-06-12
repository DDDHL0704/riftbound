# Stage 4D-210O Recovery Trigger Queue Kogmaw Source Controller Count Audit

Date: 2026-06-12 12:07 CST

Project status: **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for spectator replay recovery timing trigger queue validation. Runtime changed: no, server test coverage only.

This slice covers Kogmaw last breath source object controller validation while the spectator trigger queue count also mismatches authoritative state.

## Coverage Added

- File: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceControllerContextDriftWithCountMismatch`
- Main code commit: `2ca4e00b`

The test starts from authoritative trigger id `TRIGGER-stack-1-source-1-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1`, keeps `source-1` as a Kogmaw unit card controlled by `alice`, keeps `wrong-source` as a Kogmaw unit card controlled by `bob`, keeps `battlefield-1` as a battlefield card in `alice`'s `Battlefields`, rewrites the spectator trigger id to forged `TRIGGER-stack-1-wrong-source-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1`, rewrites the spectator source object id to `wrong-source`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation must reject the replay frame with all of:

- Kogmaw last breath source object id `wrong-source` controller id `bob` must match trigger controller id `alice`.
- Spectator trigger id `trigger-extra` is not present in authoritative state trigger queue.
- Spectator trigger queue count `2` does not match authoritative state trigger queue count `1`.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceControllerContextDriftWithCountMismatch"` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1773/1773`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1778/1778`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8048/8048`
- Mechanical: `git diff --check` passed.
- Conflict scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
- Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth and final readiness remain open.
