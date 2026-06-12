# Stage 4D-210U Recovery Resolution History Retained Tick Order Count Audit

Date: 2026-06-12 12:50 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `78d0fefe`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedTickOrderWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing retained resolution-history validation when ordering and count drift appear in the same redacted frame.

- Authoritative retained `battlefieldResolutions[]` contains two newest-first entries: tick `2` before tick `1`.
- Authoritative retained `battleResolutions[]` contains two newest-first entries: tick `2` before tick `1`.
- The spectator battlefield resolution payload is reversed so tick `2` appears after earlier tick `1`.
- The spectator battle resolution payload is reversed so tick `2` appears after earlier tick `1`.
- One extra retained battlefield resolution payload is appended, making spectator battlefield resolution count `3` while authoritative count remains `2`.
- One extra retained battle resolution payload is appended, making spectator battle resolution count `3` while authoritative count remains `2`.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing battlefield resolutions ticks must be retained newest-first: spectator replay frame timing battlefield resolution item resolution id battlefield-resolution-1 tick 2 appears after earlier tick 1`
- `spectator replay frame timing battlefield resolution count 3 does not match authoritative state battlefield resolution count 2`
- `spectator replay frame timing battle resolutions ticks must be retained newest-first: spectator replay frame timing battle resolution item resolution id battle-resolution-1 tick 2 appears after earlier tick 1`
- `spectator replay frame timing battle resolution count 3 does not match authoritative state battle resolution count 2`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedTickOrderWithCountMismatch"` -> `1/1`
- Focused resolution-history: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ResolutionHistory"` -> `106/106`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1779/1779`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1784/1784`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8054/8054`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing resolution-history retained ordering with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
