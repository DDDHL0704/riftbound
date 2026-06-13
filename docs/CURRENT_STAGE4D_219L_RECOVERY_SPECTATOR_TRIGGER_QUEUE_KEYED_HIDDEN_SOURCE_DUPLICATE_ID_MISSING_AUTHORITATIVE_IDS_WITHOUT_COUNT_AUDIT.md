# Stage 4D-219L Recovery Spectator Trigger Queue Keyed Hidden Source Duplicate Id Missing Authoritative Ids Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed hidden-source duplicate trigger-id plus unknown/missing-authoritative key-set validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch`.
- The test builds three natural authoritative hidden-source trigger queue items and three matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to duplicate the first item, changes its controller and triggered-event kind, and mutates the third item `triggerId` to `trigger-extra`.
- Recovery validation must emit duplicate trigger-id, unknown trigger-id, required authoritative trigger-id diagnostics for both missing hidden-source trigger ids, keyed controller/triggered-event mismatches for the duplicated id, and aggregate trigger-id/controller/triggered-event disagreement.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch and the hidden-source trigger-id redaction diagnostic.
- Existing generic key-set no-count coverage, hidden-source duplicate-id no-count coverage, hidden-source duplicate-id with missing-authoritative count-mismatch coverage and keyed hidden-source value drift tests remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1882/1882`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecovery"` passed `1887/1887`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx` passed `8170/8170`, refreshing the full backend gate after the Stage 4D-219J/219K/219L routine server-test shards following Stage 4D-219I.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src tests`.

## Commits

- Code: `21a25bf1` (`test: cover hidden trigger queue keyset duplicate without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed hidden-source duplicate trigger-id plus unknown/missing-authoritative key-set validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
