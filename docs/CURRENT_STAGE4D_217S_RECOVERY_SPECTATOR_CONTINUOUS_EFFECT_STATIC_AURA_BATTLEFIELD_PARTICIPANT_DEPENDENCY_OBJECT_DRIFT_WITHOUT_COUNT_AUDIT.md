# Stage 4D-217S Recovery Spectator Continuous Effect Static Aura Battlefield Participant Dependency Object Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` battlefield static-aura participant dependency object-list membership validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantDependencyObjectDriftWithoutCountMismatch` builds a spectator replay frame from authoritative battlefield static-aura state.
- The authoritative continuous-effect count remains unchanged at one naturally generated `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` static aura.
- The fixture uses battlefield source object `battlefield-1` with card number `OGN·294/298` and participant unit `participant-1` at the same battlefield, causing recovery redaction to emit a battlefield static-aura effect for the participant.
- The spectator continuous effect keeps the existing effect item, count, `targetObjectId`, and `participantObjectIds`, but replaces `participantDependencyObjectIds` with the battlefield source object id so the dependency list omits the generated participant object id.
- Recovery validation must emit the static-aura participant dependency object-list membership diagnostic for the generated participant object id.
- The test also proves that diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- Existing multi-item static-aura participant dependency-object consistency tests with count mismatch remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantDependencyObjectDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1842/1842`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1847/1847`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8130/8130`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `951d7d32` (`test: cover battlefield static aura participant dependency membership without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect battlefield static-aura participant dependency object-list membership validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
