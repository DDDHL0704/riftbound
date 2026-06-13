# Stage 4D-220K Recovery Spectator Continuous Effect Static Aura Participant Dependency Object Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` STATIC_AURA participant dependency-object membership diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyObjectDriftWithCountMismatch`.
- The new test builds a natural authoritative static-aura continuous effect, mutates the spectator replay payload so `participantDependencyObjectIds` no longer includes the participant object id, then appends an otherwise valid `effect-extra` spectator effect.
- Recovery validation must emit the missing participant dependency-object membership diagnostic while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count participant dependency-object drift, participant dependency-list drift, participant object-list drift, dependency object/list drift, broader known-value/canonicality and continuous-effect count mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyObjectDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1903/1903`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1908/1908`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8195/8195`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `a968f041` (`test: cover static aura participant dependency object count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` STATIC_AURA participant dependency-object/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
