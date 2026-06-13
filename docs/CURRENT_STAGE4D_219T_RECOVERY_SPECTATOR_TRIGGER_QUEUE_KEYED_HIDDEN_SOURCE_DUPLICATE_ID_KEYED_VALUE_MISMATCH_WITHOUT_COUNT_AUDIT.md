# Stage 4D-219T Recovery Spectator Trigger Queue Keyed Hidden Source Duplicate Id Keyed Value Mismatch Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed hidden-source duplicate trigger-id plus keyed value mismatch validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithKeyedValueMismatchWithoutCountMismatch`.
- The test builds two natural authoritative hidden-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to duplicate `trigger-hidden-a`, drifts its controller and triggered-event kind, and keeps the spectator trigger queue length equal to two.
- Recovery validation must emit duplicate trigger-id, required authoritative trigger-id, aggregate trigger-id/controller/triggered-event disagreement and keyed controller/triggered-event mismatches for the duplicate id.
- Recovery validation must avoid trigger queue count mismatch, false unknown-trigger diagnostics for the duplicate id and trigger-id redaction diagnostics.
- Existing hidden-source canonicalized duplicate keyed-value no-count coverage, hidden-source duplicate count coverage and visible-source duplicate keyed-value no-count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithKeyedValueMismatchWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1890/1890`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1895/1895`.
- Backend full was not rerun for this second routine server-test shard after Stage 4D-219R; latest backend full remains Stage 4D-219R `8176/8176`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `4958f60b` (`test: cover hidden trigger queue duplicate value drift`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed hidden-source duplicate trigger-id plus keyed value mismatch validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
