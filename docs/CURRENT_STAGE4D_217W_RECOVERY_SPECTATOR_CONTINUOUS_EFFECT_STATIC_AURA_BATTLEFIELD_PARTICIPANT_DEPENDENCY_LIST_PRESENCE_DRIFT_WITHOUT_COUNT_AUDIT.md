# Stage 4D-217W Recovery Spectator Continuous Effect Static Aura Battlefield Participant Dependency List Presence Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` battlefield static-aura required participant dependency object-list validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantDependencyListPresenceDriftWithoutCountMismatch` builds a spectator replay frame from authoritative battlefield static-aura state.
- The authoritative continuous-effect count remains unchanged at one naturally generated `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` static aura.
- The fixture uses battlefield source object `battlefield-1` with card number `OGN·294/298` and participant unit `participant-1` at the same battlefield, causing recovery redaction to emit a battlefield static-aura effect for the participant.
- The spectator continuous effect keeps the existing effect item, count, scope, duration, target object id, and `participantObjectIds`, but removes `participantDependencyObjectIds`.
- Recovery validation must emit the static-aura participant dependency object-list required diagnostic.
- The test also proves the diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- Existing multi-item battlefield static-aura participant dependency-list consistency tests with count mismatch remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldParticipantDependencyListPresenceDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1846/1846`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1851/1851`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8134/8134`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `4559ca38` (`test: cover battlefield static aura participant dependency list presence without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect battlefield static-aura required participant dependency object-list validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
