# Stage 4D-217J Recovery Spectator Continuous Effect Source Object Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` source-object consistency validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectSourceObjectGlobalRuleTextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative continuous-effect state.
- The authoritative continuous-effect count remains unchanged.
- The spectator continuous effect keeps the existing effect item, mutates it into a global rule-text effect, and leaves `sourceObjectId` present.
- Recovery validation must emit the `global rule text source object id must be absent` source-object consistency diagnostic.
- The test also proves the diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- The existing multi-item source-object consistency drift test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectSourceObjectGlobalRuleTextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1833/1833`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1838/1838`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8121/8121`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `cf2f0a92` (`test: cover continuous effect source object drift without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect global rule-text source-object consistency validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
