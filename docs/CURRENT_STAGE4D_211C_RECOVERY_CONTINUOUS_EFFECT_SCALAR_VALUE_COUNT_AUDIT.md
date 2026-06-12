# Stage 4D-211C Recovery Continuous Effect Scalar Value Count Audit

Date: 2026-06-12 13:50 CST

## Scope

- Owner: A_MAIN
- Worktree: `/Users/dinghaolin/IdeaProjects/riftbound`
- Branch: `main`
- Code commit: `d16b3f24`
- Runtime changed: no, server test coverage only
- Primary test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- New test: `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectScalarValueDriftWithCountMismatch`

## Coverage Added

This slice covers spectator replay timing `continuousEffects[]` scalar value and scalar payload-shape validation when the spectator list also has a count mismatch against authoritative state.

- Authoritative state contains one PowerModifier continuous effect, `effect-1`.
- The first spectator effect starts from the redacted authoritative effect.
- The spectator effect rewrites scalar fields across effect id, scope, layer, duration, target/source object ids, numeric power fields, sequence, effect kind, source card number, source path, LayerEngine status, source order, condition, and lifecycle.
- The spectator list appends `effect-extra`, so the spectator `continuousEffects[]` count is `2` while authoritative state has `1`.

The validator now has explicit regression coverage proving it reports scalar value/shape diagnostics together with unexpected effect-id and count mismatch diagnostics in the same spectator replay frame.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectScalarValueDriftWithCountMismatch"` -> `1/1`
- Focused continuous-effect: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~ContinuousEffect"` -> `292/292`
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1787/1787`
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1792/1792`
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `8062/8062`
- Mechanical: `git diff --check` passed.
- Mechanical: conflict-marker scan over `docs`, `tests`, and `src` passed.

## Status

This narrows recovery spectator replay timing continuous-effect scalar value/shape validation with count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.

Project remains **NOT READY**.
