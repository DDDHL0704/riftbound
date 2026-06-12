# Stage 4D-211E Recovery Continuous Effect Trigger Queue Property Name Count Audit

Date: 2026-06-12 14:14 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `d3e45e0a`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingPayloadPropertyNameDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing `continuousEffects[]` and `triggerQueue[]` item property-name validation when both spectator lists also have count mismatches against authoritative state.

- Authoritative state contains one PowerModifier continuous effect and one trigger queue item.
- The first spectator continuous-effect item is replaced with raw JSON containing a duplicate `effectId`, a surrounding-whitespace `scope` property, and an empty property name.
- The first spectator trigger queue item is replaced with raw JSON containing a duplicate `triggerId`, a surrounding-whitespace `controllerId` property, and an empty property name.
- The spectator frame appends one additional raw JSON item to each list with the same property-name drift, so both spectator counts are `2` while authoritative state has `1`.

The validator now has explicit regression coverage proving it reports continuous-effect and trigger queue item property-name diagnostics together with both list count mismatch diagnostics in the same spectator replay frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingPayloadPropertyNameDriftWithCountMismatch"` -> `1/1`
- Focused continuous-effect: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ContinuousEffect"` -> `293/293`
- Focused trigger queue: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerQueue"` -> `701/701`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1789/1789`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1794/1794`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8064/8064`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing continuous-effect and trigger-queue item property-name validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
