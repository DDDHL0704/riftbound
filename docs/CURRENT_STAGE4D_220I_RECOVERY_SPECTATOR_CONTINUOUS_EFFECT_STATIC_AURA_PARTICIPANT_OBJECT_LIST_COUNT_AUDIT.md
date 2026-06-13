# Stage 4D-220I Recovery Spectator Continuous Effect Static Aura Participant Object List Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `continuousEffects[]` STATIC_AURA participant object-list required diagnostics with a continuous-effect count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantObjectListDriftWithCountMismatch`.
- The new test builds a natural authoritative static-aura continuous effect, mutates the spectator replay payload by removing `participantObjectIds` while retaining `participantDependencyObjectIds`, then appends an otherwise valid `effect-extra` spectator effect.
- Recovery validation must emit the missing static-aura participant object-list diagnostic while also reporting the unknown extra effect id and the `continuousEffects[]` count mismatch.
- Existing without-count static-aura participant object-list drift, participant dependency list/object drift, dependency-object/list drift, metadata, dependency registry, broader known-value/canonicality and continuous-effect count mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantObjectListDriftWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1901/1901`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1906/1906`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8193/8193`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `422ecb60` (`test: cover static aura participant object list count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `continuousEffects[]` STATIC_AURA participant object-list/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
