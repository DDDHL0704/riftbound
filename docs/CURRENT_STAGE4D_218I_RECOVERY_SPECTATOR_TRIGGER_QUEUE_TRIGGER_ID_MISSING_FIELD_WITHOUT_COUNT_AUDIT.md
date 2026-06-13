# Stage 4D-218I Recovery Spectator Trigger Queue Trigger Id Missing Field Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic trigger-id missing-field validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdMissingFieldWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload removes only `triggerId`, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the trigger-id required diagnostic, the authoritative key-set required-trigger diagnostic and the ordered trigger-id disagreement diagnostic.
- Recovery validation must avoid any spectator replay timing trigger queue count mismatch.
- Existing null, empty, shape, redaction-sentinel and count-mismatch trigger-id tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdMissingFieldWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1853/1853`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1858/1858`.
- Backend full was not rerun for this first routine small server-test shard after Stage 4D-218H; latest backend full remains Stage 4D-218H `8140/8140`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `81e05d4a` (`test: cover trigger queue missing trigger id without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue trigger-id missing-field validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
