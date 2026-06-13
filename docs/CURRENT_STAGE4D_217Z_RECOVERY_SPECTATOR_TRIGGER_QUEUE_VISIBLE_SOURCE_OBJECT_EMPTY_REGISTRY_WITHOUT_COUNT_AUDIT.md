# Stage 4D-217Z Recovery Spectator Trigger Queue Visible Source Object Empty Registry Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` visible source object registry validation without relying on a trigger queue count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistryWithoutCountMismatch` builds a spectator replay frame from authoritative trigger queue state.
- The authoritative trigger queue count remains unchanged at one naturally emitted trigger item.
- The fixture intentionally leaves the object registry empty while the authoritative trigger references source object id `source-1`.
- The spectator trigger item is not synthetically added and is not count-shifted; it naturally emits `trigger-1`, controller `alice`, source object id `source-1`, `sourceVisibility` `VISIBLE`, effect kind `LAST_BREATH` and triggered event kind `OBJECT_DESTROYED`.
- Recovery validation must emit the authoritative trigger queue source-object missing-from-registry diagnostic and the spectator trigger queue visible source-object missing-from-registry diagnostic.
- The test also proves these diagnostics are emitted without a keyed source-object mismatch, without ordered source-object-id parity drift, and without any spectator replay timing trigger queue count mismatch.
- Existing trigger queue visible-source object membership tests with count mismatch remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceObjectWithEmptyObjectRegistryWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1849/1849`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1854/1854`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8137/8137`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `5add92b2` (`test: cover trigger queue empty source registry without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue visible source object registry validation without count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
