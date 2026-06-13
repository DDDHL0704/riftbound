# Stage 4D-220N Recovery Spectator Continuous Effect Static Aura Battlefield Dependency Object Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` battlefield STATIC_AURA source/target dependency-object membership diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldDependencyObjectDriftWithCountMismatch`.
- The new test builds a natural authoritative battlefield static-aura continuous effect, mutates the spectator replay payload so `sourceDependencyObjectIds` no longer includes the source battlefield object id and `targetDependencyObjectIds` no longer includes the target participant object id, then appends an otherwise valid `effect-extra` spectator effect.
- Recovery validation must emit both missing battlefield dependency-object membership diagnostics while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count battlefield dependency-object drift, battlefield dependency-list drift, battlefield participant object/dependency count-mismatch coverage and broader static-aura count-mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldDependencyObjectDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1906/1906`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1911/1911`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8198/8198`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `129748e8` (`test: cover battlefield static aura dependency object count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` battlefield STATIC_AURA dependency-object/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
