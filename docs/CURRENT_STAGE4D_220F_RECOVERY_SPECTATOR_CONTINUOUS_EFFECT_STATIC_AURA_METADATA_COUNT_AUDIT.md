# Stage 4D-220F Recovery Spectator Continuous Effect Static Aura Metadata Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` STATIC_AURA metadata required-field diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraMetadataConsistencyDriftWithCountMismatch`.
- The new test builds a natural authoritative static-aura continuous effect, mutates the spectator replay payload by removing `effectKind`, `sourceCardNo`, `sourcePath`, `condition` and `lifecycle`, then appends an otherwise valid `effect-extra` spectator effect.
- Recovery validation must emit the five missing static-aura metadata diagnostics while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count static-aura metadata consistency drift, dependency registry/list/object, object-reference outside registry, broader known-value/canonicality and continuous-effect count mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraMetadataConsistencyDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1898/1898`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1903/1903`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8190/8190`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `b2e117e5` (`test: cover static aura metadata count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` STATIC_AURA metadata/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
