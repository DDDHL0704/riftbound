# Stage 4D-218J Recovery Spectator Trigger Queue Controller Id Missing Field Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic controller-id missing-field validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdMissingFieldWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload removes only `controllerId`, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the controller-id required diagnostic, the authoritative keyed controller-id mismatch diagnostic and the aggregate controller-id disagreement diagnostic.
- Recovery validation must avoid any spectator replay timing trigger queue count mismatch.
- Existing combined required-field absence, null, empty, shape, redaction-sentinel, membership and count-mismatch controller-id tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdMissingFieldWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1854/1854`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1859/1859`.
- Backend full was not rerun for this second routine small server-test shard after Stage 4D-218H; latest backend full remains Stage 4D-218H `8140/8140`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `6e2dbebe` (`test: cover trigger queue missing controller id without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue controller-id missing-field validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
