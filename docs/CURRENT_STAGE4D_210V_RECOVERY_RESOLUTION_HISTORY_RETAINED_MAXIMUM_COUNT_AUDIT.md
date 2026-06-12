# Stage 4D-210V Recovery Resolution History Retained Maximum Count Audit

Date: 2026-06-12 12:59 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `27174617`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedListMaximumWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing retained resolution-history validation when maximum-list overflow and count drift appear in the same redacted frame.

- Authoritative retained `battlefieldResolutions[]` contains the maximum allowed twelve entries.
- Authoritative retained `battleResolutions[]` contains the maximum allowed twelve entries.
- The spectator battlefield resolution payload appends one extra retained payload, making spectator battlefield resolution count `13` while authoritative count remains `12`.
- The spectator battle resolution payload appends one extra retained payload, making spectator battle resolution count `13` while authoritative count remains `12`.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing battlefield resolutions list has 13 items, maximum is 12`
- `spectator replay frame timing battlefield resolution count 13 does not match authoritative state battlefield resolution count 12`
- `spectator replay frame timing battle resolutions list has 13 items, maximum is 12`
- `spectator replay frame timing battle resolution count 13 does not match authoritative state battle resolution count 12`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryRetainedListMaximumWithCountMismatch"` -> `1/1`
- Focused resolution-history: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ResolutionHistory"` -> `107/107`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1780/1780`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1785/1785`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8055/8055`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing resolution-history retained maximum-list validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
