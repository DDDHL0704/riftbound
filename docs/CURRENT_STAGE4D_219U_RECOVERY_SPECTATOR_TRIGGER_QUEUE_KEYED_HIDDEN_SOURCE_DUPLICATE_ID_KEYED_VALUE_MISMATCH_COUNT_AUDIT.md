# Stage 4D-219U Recovery Spectator Trigger Queue Keyed Hidden Source Duplicate Id Keyed Value Mismatch Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed hidden-source duplicate trigger-id plus keyed value mismatch validation with a trigger queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithKeyedValueMismatchAndCountMismatch`.
- The test builds two natural authoritative hidden-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to duplicate `trigger-hidden-a`, drifts its controller and triggered-event kind, appends a cloned first spectator item as `trigger-extra`, and therefore makes the spectator trigger queue length three while the authoritative queue length remains two.
- Recovery validation must emit duplicate trigger-id, unknown extra trigger-id, required authoritative trigger-id, keyed controller/triggered-event mismatches for the duplicate id and the trigger queue count mismatch.
- Recovery validation must avoid false unknown-trigger diagnostics for the duplicate id, trigger-id redaction diagnostics and aggregate trigger-id disagreement when the count mismatch is present.
- Existing hidden-source duplicate keyed-value no-count coverage, hidden-source canonicalized duplicate keyed-value count coverage and visible-source duplicate keyed-value coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithKeyedValueMismatchAndCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1891/1891`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1896/1896`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8179/8179`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `0c3a800d` (`test: cover hidden trigger queue duplicate value count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed hidden-source duplicate trigger-id plus keyed value mismatch validation with trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
