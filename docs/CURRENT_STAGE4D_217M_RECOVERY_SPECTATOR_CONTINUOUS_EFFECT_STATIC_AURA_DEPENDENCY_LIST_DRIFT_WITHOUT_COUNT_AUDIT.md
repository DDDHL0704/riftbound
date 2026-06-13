# Stage 4D-217M Recovery Spectator Continuous Effect Static Aura Dependency List Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` static-aura required dependency-list validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraDependencyListDriftWithoutCountMismatch` builds a spectator replay frame from authoritative static-aura continuous-effect state.
- The authoritative continuous-effect count remains unchanged at one naturally generated Ornn friendly-equipment static aura.
- The spectator continuous effect keeps the existing effect item and count, but removes `sourceDependencyObjectIds` and `targetDependencyObjectIds`.
- Recovery validation must emit the static-aura source and target dependency-list required diagnostics.
- The test also proves those diagnostics are emitted without any spectator replay timing continuous-effect count mismatch.
- The existing multi-item static-aura dependency-list consistency test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraDependencyListDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1836/1836`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1841/1841`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8124/8124`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `402e5261` (`test: cover continuous effect static aura dependency lists without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect static-aura required dependency-list validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
