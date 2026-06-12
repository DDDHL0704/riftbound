# Stage 4D-211B Recovery Continuous Effect List Payload Shape Count Audit

Date: 2026-06-12 13:42 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `d3e32edb`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListPayloadShapeDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing `continuousEffects[]` nested list-payload shape validation when the spectator list also has a count mismatch against authoritative state.

- Authoritative state contains one PowerModifier continuous effect, `effect-1`.
- The first spectator effect starts from the redacted authoritative effect.
- The spectator effect rewrites `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds`, and `deferredLayerEngineResiduals` to non-list payloads.
- The spectator list appends `effect-extra` with non-list nested payloads, so the spectator `continuousEffects[]` count is `2` while authoritative state has `1`.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing continuous effect item participant object id list payload is required`
- `spectator replay frame timing continuous effect item source dependency object id list payload is required`
- `spectator replay frame timing continuous effect item target dependency object id list payload is required`
- `spectator replay frame timing continuous effect item participant dependency object id list payload is required`
- `spectator replay frame timing continuous effect item deferred LayerEngine residual list payload is required`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListPayloadShapeDriftWithCountMismatch"` -> `1/1`
- Focused continuous-effect: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ContinuousEffect"` -> `291/291`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1786/1786`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1791/1791`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8061/8061`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing continuous-effect nested list-payload shape validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
