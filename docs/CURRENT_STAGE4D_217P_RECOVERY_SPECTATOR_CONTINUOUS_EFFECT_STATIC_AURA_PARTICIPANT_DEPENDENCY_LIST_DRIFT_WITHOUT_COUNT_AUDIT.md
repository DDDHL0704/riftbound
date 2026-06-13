# Stage 4D-217P Recovery Spectator Continuous Effect Static Aura Participant Dependency List Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` static-aura participant dependency object-list required validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyListDriftWithoutCountMismatch` builds a spectator replay frame from authoritative static-aura continuous-effect state.
- The authoritative continuous-effect count remains unchanged at one naturally generated Ornn friendly-equipment static aura.
- The fixture marks the equipment object with `EquipmentCard` so the redactor naturally emits `participantObjectIds` and `participantDependencyObjectIds`.
- The spectator continuous effect keeps the existing effect item and count, but removes `participantDependencyObjectIds` while leaving the generated `participantObjectIds` in place.
- Recovery validation must emit the static-aura participant dependency object-list required diagnostic.
- The test also proves that diagnostic is emitted without any spectator replay timing continuous-effect count mismatch.
- The existing multi-item static-aura participant dependency-list consistency test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraParticipantDependencyListDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1839/1839`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1844/1844`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8127/8127`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `541b0597` (`test: cover continuous effect static aura participant dependencies without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect static-aura participant dependency object-list required validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
