# Stage 4D-210P Recovery Trigger Queue Kogmaw Source Visibility State Count Audit

Date: 2026-06-12 12:13 CST

Project status: **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for spectator replay recovery timing trigger queue validation. Runtime changed: no, server test coverage only.

This slice covers Kogmaw last breath source visibility authoritative object-state validation while the spectator trigger queue count also mismatches authoritative state.

## Coverage Added

- File: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- Test: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceVisibilityStateContextDriftWithCountMismatch`
- Main code commit: `0c2b8d28`

The test starts from authoritative trigger id `TRIGGER-stack-1-source-1-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1`, keeps `source-1` as a visible Kogmaw unit card controlled by `alice`, keeps `wrong-source` as a Kogmaw unit card that is face down and tagged standby, keeps `battlefield-1` as a battlefield card in `alice`'s `Battlefields`, rewrites the spectator trigger id to forged `TRIGGER-stack-1-wrong-source-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1`, rewrites the spectator source object id to `wrong-source`, rewrites spectator source visibility to `HIDDEN`, then appends `trigger-extra` to force trigger queue count and key-set drift.

Recovery validation must reject the replay frame with all of:

- Kogmaw last breath source visibility must be `VISIBLE`.
- Kogmaw last breath source object id `wrong-source` must not be face down.
- Kogmaw last breath source object id `wrong-source` must not be a standby card.
- Spectator trigger id `trigger-extra` is not present in authoritative state trigger queue.
- Spectator trigger queue count `2` does not match authoritative state trigger queue count `1`.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceVisibilityStateContextDriftWithCountMismatch"` -> `1/1`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1774/1774`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1779/1779`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8049/8049`
- Mechanical: `git diff --check` passed.
- Conflict scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` had no matches.

## Coordination

- No subagent or new worktree was created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.
- Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth and final readiness remain open.
