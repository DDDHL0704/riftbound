# Stage 4D-217E Recovery Spectator Continuous Effect Scalar Value Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` scalar value validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectScalarValueDriftWithoutCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The authoritative continuous-effect count remains unchanged.
- The spectator continuous effect keeps the existing effect item while forging scalar, object-reference, numeric, optional metadata, source-order, condition, and lifecycle payloads into malformed or whitespace-mutated values.
- Recovery validation must emit the scalar-value diagnostics for required strings, surrounding whitespace, invalid object references, invalid numeric payloads, optional metadata shape, and invalid lifecycle/source-order values.
- The test also proves the diagnostics are emitted without any spectator replay timing continuous-effect count mismatch.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectScalarValueDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1830/1830`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1835/1835`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8118/8118`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`.

## Commits

- Code: `7eb3f5b4` (`test: cover continuous effect scalar drift without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect scalar value drift validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
