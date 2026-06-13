# Stage 4D-218C Recovery Spectator Trigger Queue Visible Source Object Payload Identity Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` visible source object payload identity validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectPayloadIdentityDriftWithoutCountMismatch`.
- The authoritative trigger queue count remains unchanged at one naturally emitted trigger item.
- The fixture intentionally makes the authoritative object registry inconsistent: `cardObjects` and `objectLocations` use map key `actual-source`, while the card object payload identity is `source-1`.
- The authoritative trigger item naturally references source object id `source-1`, so the spectator frame emits a single visible `triggerQueue[]` item rather than a synthetically added trigger.
- Recovery validation must emit the card object map-key identity diagnostic, the authoritative trigger queue missing source-object diagnostic and the spectator visible source-object missing-from-registry diagnostic.
- The test also proves these diagnostics are emitted without any spectator replay timing trigger queue count mismatch.
- The existing synthetic visible source-object payload identity drift test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectPayloadIdentityDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1851/1851`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1856/1856`.
- Backend full was not rerun for this routine single-test coverage shard; latest backend full remains Stage 4D-218B at `8138/8138`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `fdd328f5` (`test: cover trigger queue payload identity without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue visible source-object payload identity validation without count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
