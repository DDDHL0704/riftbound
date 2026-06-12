# Stage 4D-210X Recovery Resolution History Id Mismatch Count Audit

Date: 2026-06-12 13:14 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `c869f16b`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryIdMismatchWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing retained resolution-history validation when resolution ids disagree and the spectator retained resolution lists also drift in count.

- Authoritative retained `battlefieldResolutions[]` contains one entry, `battlefield-resolution-0`.
- Authoritative retained `battleResolutions[]` contains one entry, `battle-resolution-0`.
- The spectator battlefield resolution payload starts from the authoritative redacted payload, rewrites the first resolution id to `other-battlefield-resolution`, and appends `battlefield-resolution-1`.
- The spectator battle resolution payload starts from the authoritative redacted payload, rewrites the first resolution id to `other-battle-resolution`, and appends `battle-resolution-1`.
- Both spectator retained resolution lists contain two entries while authoritative state contains one entry.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing battlefield resolution count 2 does not match authoritative state battlefield resolution count 1`
- `spectator replay frame timing battlefield resolution item resolution id other-battlefield-resolution is not present in authoritative state battlefield resolutions`
- `spectator replay frame timing battlefield resolution item resolution id battlefield-resolution-1 is not present in authoritative state battlefield resolutions`
- `spectator replay frame timing battlefield resolution item resolution id battlefield-resolution-0 is required by authoritative state battlefield resolutions`
- `spectator replay frame timing battle resolution count 2 does not match authoritative state battle resolution count 1`
- `spectator replay frame timing battle resolution item resolution id other-battle-resolution is not present in authoritative state battle resolutions`
- `spectator replay frame timing battle resolution item resolution id battle-resolution-1 is not present in authoritative state battle resolutions`
- `spectator replay frame timing battle resolution item resolution id battle-resolution-0 is required by authoritative state battle resolutions`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryIdMismatchWithCountMismatch"` -> `1/1`
- Focused resolution-history: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ResolutionHistory"` -> `109/109`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1782/1782`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1787/1787`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8057/8057`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing resolution-history id-mismatch validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
