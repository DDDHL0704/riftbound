# Stage 4D-217R Recovery Spectator Continuous Effect Static Aura Battlefield Participant Object Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` battlefield static-aura participant object-list membership validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantObjectDriftWithoutCountMismatch` builds a spectator replay frame from authoritative battlefield static-aura state.
- The authoritative continuous-effect count remains unchanged at one naturally generated `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` static aura.
- The fixture uses battlefield source object `battlefield-1` with card number `OGN·294/298` and participant unit `participant-1` at the same battlefield, causing recovery redaction to emit a battlefield static-aura effect for the participant.
- The spectator continuous effect keeps the existing effect item and count, but replaces `participantObjectIds` and `participantDependencyObjectIds` with the battlefield source object id so the participant dependency membership remains internally coherent while the participant list omits the target object id.
- Recovery validation must emit the battlefield static-aura participant object-list membership diagnostic for the generated target object id.
- The test also proves that diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- Existing multi-item battlefield static-aura consistency tests with count mismatch remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantObjectDriftWithoutCountMismatch"` passed `1/1` after an initial analyzer-order fix.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1841/1841`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1846/1846`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8129/8129`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `d6dac6bf` (`test: cover battlefield static aura participant membership without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect battlefield static-aura participant object-list membership validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
