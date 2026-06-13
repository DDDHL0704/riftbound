# Stage 4D-218X Recovery Spectator Trigger Queue Controller Id Canonicality Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic controller-id whitespace canonicality validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdCanonicalityWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload mutates only `controllerId` to a surrounding-whitespace value, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the controller-id surrounding-whitespace diagnostic, the authoritative keyed controller-id mismatch diagnostic and the aggregate controller-id disagreement diagnostic.
- Recovery validation must avoid source-object-id, source-visibility, effect-kind, triggered-event-kind disagreement and spectator replay timing trigger queue count mismatch.
- Existing keyed visible-source controller-id canonicality, generic controller-id shape/missing-field and aggregate value-drift tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdCanonicalityWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1868/1868`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1873/1873`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test --no-restore` passed `8156/8156`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `da8f2bd5` (`test: cover trigger queue controller id canonicality without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue controller-id canonicality validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
