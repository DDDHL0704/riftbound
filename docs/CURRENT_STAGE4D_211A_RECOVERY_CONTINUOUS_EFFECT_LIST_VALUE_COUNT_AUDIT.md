# Stage 4D-211A Recovery Continuous Effect List Value Count Audit

Date: 2026-06-12 13:35 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `0aa455c6`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListValueDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing `continuousEffects[]` list-value canonicality validation when the spectator list also has a count mismatch against authoritative state.

- Authoritative state contains one PowerModifier continuous effect, `effect-1`.
- The first spectator effect starts from the redacted authoritative effect.
- The spectator effect rewrites `participantObjectIds[]`, `sourceDependencyObjectIds[]`, `targetDependencyObjectIds[]`, `participantDependencyObjectIds[]`, and `deferredLayerEngineResiduals[]` with a canonical value, a surrounding-whitespace duplicate, and a blank value.
- The spectator list appends `effect-extra`, so the spectator `continuousEffects[]` count is `2` while authoritative state has `1`.

The validator now has explicit regression coverage proving it reports all of these diagnostics together:

- `spectator replay frame timing continuous effect item participant object id participant-1 has surrounding whitespace`
- `spectator replay frame timing continuous effect item participant object id participant-1 is duplicated`
- `spectator replay frame timing continuous effect item participant object id is required`
- `spectator replay frame timing continuous effect item source dependency object id source-1 has surrounding whitespace`
- `spectator replay frame timing continuous effect item source dependency object id source-1 is duplicated`
- `spectator replay frame timing continuous effect item target dependency object id target-1 has surrounding whitespace`
- `spectator replay frame timing continuous effect item target dependency object id target-1 is duplicated`
- `spectator replay frame timing continuous effect item participant dependency object id dependency-1 has surrounding whitespace`
- `spectator replay frame timing continuous effect item participant dependency object id dependency-1 is duplicated`
- `spectator replay frame timing continuous effect item deferred LayerEngine residual residual-1 has surrounding whitespace`
- `spectator replay frame timing continuous effect item deferred LayerEngine residual residual-1 is duplicated`
- `spectator replay frame timing continuous effect item deferred LayerEngine residual is required`
- `spectator replay frame timing continuous effect item effect id effect-extra is not present in authoritative state continuous effects`
- `spectator replay frame timing continuous effect count 2 does not match authoritative state continuous effect count 1`

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListValueDriftWithCountMismatch"` -> `1/1`
- Focused continuous-effect: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ContinuousEffect"` -> `290/290`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1785/1785`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1790/1790`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8060/8060`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing continuous-effect list-value canonicality validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
