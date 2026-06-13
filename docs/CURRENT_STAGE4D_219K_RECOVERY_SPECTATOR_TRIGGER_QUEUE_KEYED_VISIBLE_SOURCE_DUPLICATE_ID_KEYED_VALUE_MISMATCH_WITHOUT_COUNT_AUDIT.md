# Stage 4D-219K Recovery Spectator Trigger Queue Keyed Visible Source Duplicate Id Keyed Value Mismatch Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source duplicate trigger-id plus keyed value mismatch validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithKeyedValueMismatchWithoutCountMismatch`.
- The test builds two natural authoritative visible-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates only the second item `triggerId` to duplicate the first item while preserving the second item controller, source object, effect kind and triggered-event kind values.
- Recovery validation must emit the duplicate trigger-id diagnostic, the missing authoritative trigger-id diagnostic, aggregate trigger-id disagreement, and keyed controller/source-object/effect-kind/triggered-event-kind mismatch diagnostics for the duplicated key.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch.
- Existing generic duplicate-id no-count coverage, key-set no-count coverage, visible-source duplicate-id with-count coverage and keyed visible/hidden-source value drift tests remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithKeyedValueMismatchWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Riftbound.ConformanceTests.MatchRecoveryTests"` passed `1881/1881`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1886/1886`.
- Backend full was not rerun for this second routine server-test shard after Stage 4D-219I; latest backend full remains Stage 4D-219I `8167/8167`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" .`.

## Commits

- Code: `58726cbb` (`test: cover trigger queue duplicate keyed mismatch without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source duplicate trigger-id plus keyed value mismatch validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
