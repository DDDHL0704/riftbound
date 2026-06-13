# Stage 4D-220Z Recovery Spectator Trigger Queue Triggered Event Kind Empty Value Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `triggerQueue[]` triggered event kind empty-value diagnostics with a trigger-queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindEmptyValueWithCountMismatch`.
- The new test builds a natural authoritative trigger queue item, mutates the spectator replay payload by setting `triggeredByEventKind` to an empty string, then appends an otherwise valid `trigger-extra` spectator trigger.
- Recovery validation must emit the missing/required triggered event kind diagnostic and triggered event kind mismatch diagnostic while also reporting the unknown extra trigger id and the `triggerQueue[]` count mismatch.
- Existing without-count triggered event kind empty-value coverage, triggered event kind null-value count-mismatch coverage, triggerQueue controllerId count-mismatch coverage, keyed triggerQueue field/value count-mismatch coverage and broader recovery timing triggerQueue diagnostics remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindEmptyValueWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1918/1918`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1923/1923`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8210/8210`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `744767b1` (`test: cover trigger queue event kind empty count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `triggerQueue[]` triggered event kind empty-value/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
