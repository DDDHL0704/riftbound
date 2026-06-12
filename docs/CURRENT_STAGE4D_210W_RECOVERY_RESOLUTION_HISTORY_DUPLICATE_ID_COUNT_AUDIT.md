# Stage 4D-210W Recovery Resolution History Duplicate Id Count Audit

Date: 2026-06-12 13:07 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `a5229473`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryDuplicateIdsWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing retained resolution-history validation when duplicate resolution ids and count drift appear in the same redacted frame.

- Authoritative retained `battlefieldResolutions[]` contains two entries.
- Authoritative retained `battleResolutions[]` contains two entries.
- The spectator battlefield resolution payload duplicates the first payload so `battlefield-resolution-0` appears twice.
- The spectator battle resolution payload duplicates the first payload so `battle-resolution-0` appears twice.
- One extra retained battlefield resolution payload is appended, making spectator battlefield resolution count `3` while authoritative count remains `2`.
- One extra retained battle resolution payload is appended, making spectator battle resolution count `3` while authoritative count remains `2`.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing battlefield resolution count 3 does not match authoritative state battlefield resolution count 2`
- `spectator replay frame timing battlefield resolution item resolution id battlefield-resolution-0 is duplicated`
- `spectator replay frame timing battle resolution count 3 does not match authoritative state battle resolution count 2`
- `spectator replay frame timing battle resolution item resolution id battle-resolution-0 is duplicated`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryDuplicateIdsWithCountMismatch"` -> `1/1`
- Focused resolution-history: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ResolutionHistory"` -> `108/108`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1781/1781`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1786/1786`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8056/8056`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing resolution-history duplicate-id validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
