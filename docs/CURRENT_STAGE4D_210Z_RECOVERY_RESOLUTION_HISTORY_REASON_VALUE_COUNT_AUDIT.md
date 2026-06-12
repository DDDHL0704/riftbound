# Stage 4D-210Z Recovery Resolution History Reason Value Count Audit

Date: 2026-06-12 13:26 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `0c7505e7`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryReasonValueDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing retained resolution-history validation when retained resolution `reason` values are outside the known value set and the spectator retained resolution lists also drift in count.

- Authoritative retained `battlefieldResolutions[]` contains one entry, `battlefield-resolution-0`.
- Authoritative retained `battleResolutions[]` contains one entry, `battle-resolution-0`.
- The spectator battlefield resolution payload starts from the authoritative redacted payload, rewrites `reason` to `UNKNOWN_BATTLEFIELD_REASON`, and appends `battlefield-resolution-1`.
- The spectator battle resolution payload starts from the authoritative redacted payload, rewrites `reason` to `UNKNOWN_BATTLE_REASON`, and appends `battle-resolution-1`.
- Both spectator retained resolution lists contain two entries while authoritative state contains one entry.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing battlefield resolution count 2 does not match authoritative state battlefield resolution count 1`
- `spectator replay frame timing battlefield resolution item resolution id battlefield-resolution-1 is not present in authoritative state battlefield resolutions`
- `spectator replay frame timing battlefield resolution item reason UNKNOWN_BATTLEFIELD_REASON is invalid`
- `spectator replay frame timing battle resolution count 2 does not match authoritative state battle resolution count 1`
- `spectator replay frame timing battle resolution item resolution id battle-resolution-1 is not present in authoritative state battle resolutions`
- `spectator replay frame timing battle resolution item reason UNKNOWN_BATTLE_REASON is invalid`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryReasonValueDriftWithCountMismatch"` -> `1/1`
- Focused resolution-history: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ResolutionHistory"` -> `111/111`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1784/1784`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1789/1789`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8059/8059`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing resolution-history known-reason validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
