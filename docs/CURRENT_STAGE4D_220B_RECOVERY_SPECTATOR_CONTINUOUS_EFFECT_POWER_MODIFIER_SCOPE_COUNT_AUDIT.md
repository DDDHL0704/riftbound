# Stage 4D-220B Recovery Spectator Continuous Effect Power Modifier Scope Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` POWER_MODIFIER scope semantics diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectLayerScopePowerModifierDriftWithCountMismatch`.
- The new test builds a natural authoritative power-modifier continuous effect, mutates the spectator replay payload `scope` to invalid `GLOBAL` for a POWER_MODIFIER effect, then appends an `effect-extra` spectator effect.
- Recovery validation must emit the POWER_MODIFIER scope semantic diagnostic while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count power-modifier scope drift, broader known-value/canonicality, scalar/list payload and continuous-effect count mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectLayerScopePowerModifierDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1894/1894`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1899/1899`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8186/8186`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `966b479a` (`test: cover power modifier scope count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` POWER_MODIFIER scope/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
