# Stage 4D-217D Recovery Spectator Continuous Effect List Value Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` nested list value validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListValueDriftWithoutCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The authoritative continuous-effect count remains unchanged.
- The spectator continuous effect keeps the existing effect item while forging participant, source dependency, target dependency, participant dependency, and deferred LayerEngine residual lists with blank, whitespace-mutated, and duplicate-normalized values.
- Recovery validation must emit the list-value diagnostics for required values, surrounding whitespace, and duplicate normalized entries.
- The test also proves the diagnostics are emitted without any spectator replay timing continuous-effect count mismatch.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListValueDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1830/1830`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1835/1835`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8118/8118`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`.

## Commits

- Code: `1c310bad` (`test: cover continuous effect list value drift without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect nested list value drift validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
