# Stage 4D-217Q Recovery Spectator Continuous Effect Static Aura Participant Dependency Object Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` static-aura participant dependency object-list membership validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyObjectDriftWithoutCountMismatch` builds a spectator replay frame from authoritative static-aura continuous-effect state.
- The authoritative continuous-effect count remains unchanged at one naturally generated Ornn friendly-equipment static aura.
- The fixture marks the equipment object with `EquipmentCard` so the redactor naturally emits `participantObjectIds` and `participantDependencyObjectIds`.
- The spectator continuous effect keeps the existing effect item and count, but replaces `participantDependencyObjectIds` with another valid object id while omitting the generated participant object id.
- Recovery validation must emit the static-aura participant dependency object-list membership diagnostic.
- The test also proves that diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- The existing multi-item static-aura participant dependency-object consistency test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyObjectDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1840/1840`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1845/1845`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8128/8128`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `456c39fd` (`test: cover continuous effect static aura participant dependency membership without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect static-aura participant dependency object-list membership validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
