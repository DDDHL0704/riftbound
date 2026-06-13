# Stage 4D-220O Recovery Spectator Continuous Effect Static Aura Battlefield Dependency List Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` battlefield STATIC_AURA source/target dependency-list required diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldDependencyListDriftWithCountMismatch`.
- The new test builds a natural authoritative battlefield static-aura continuous effect, mutates the spectator replay payload by removing both `sourceDependencyObjectIds` and `targetDependencyObjectIds`, then appends an otherwise valid `effect-extra` spectator effect.
- Recovery validation must emit both missing battlefield dependency-list diagnostics while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count battlefield dependency-list drift, battlefield dependency-object membership drift, battlefield participant object/dependency count-mismatch coverage and broader static-aura count-mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldDependencyListDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1907/1907`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1912/1912`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8199/8199`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `32dcf2c3` (`test: cover battlefield static aura dependency list count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` battlefield STATIC_AURA dependency-list/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
