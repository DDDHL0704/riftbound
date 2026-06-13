# Stage 4D-217L Recovery Spectator Continuous Effect Static Aura Metadata Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` static-aura required metadata validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraMetadataConsistencyDriftWithoutCountMismatch` builds a spectator replay frame from authoritative static-aura continuous-effect state.
- The authoritative continuous-effect count remains unchanged at one naturally generated Ornn friendly-equipment static aura.
- The spectator continuous effect keeps the existing effect item and count, but removes required static-aura metadata fields: `effectKind`, `sourceCardNo`, `sourcePath`, `condition`, and `lifecycle`.
- Recovery validation must emit the static-aura required metadata diagnostics.
- The test also proves those diagnostics are emitted without any spectator replay timing continuous-effect count mismatch.
- The existing multi-item static-aura metadata consistency test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraMetadataConsistencyDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1835/1835`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1840/1840`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8123/8123`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `3ef975f2` (`test: cover continuous effect static aura metadata drift without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect static-aura required metadata validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
