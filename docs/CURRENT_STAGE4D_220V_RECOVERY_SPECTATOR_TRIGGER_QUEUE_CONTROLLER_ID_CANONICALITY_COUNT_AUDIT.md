# Stage 4D-220V Recovery Spectator Trigger Queue Controller Id Canonicality Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `triggerQueue[]` controllerId canonicality diagnostics with a trigger-queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdCanonicalityWithCountMismatch`.
- The new test builds a natural authoritative trigger queue item, mutates the spectator replay payload by surrounding `controllerId` with whitespace, then appends an otherwise valid `trigger-extra` spectator trigger.
- Recovery validation must emit the controllerId surrounding-whitespace diagnostic and controllerId mismatch diagnostic while also reporting the unknown extra trigger id and the `triggerQueue[]` count mismatch.
- Existing without-count controllerId canonicality coverage, controllerId shape count-mismatch coverage, controllerId missing-field count-mismatch coverage, keyed triggerQueue field/value count-mismatch coverage and broader recovery timing triggerQueue diagnostics remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueControllerIdCanonicalityWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1914/1914`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1919/1919`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8206/8206`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `a13b7e5c` (`test: cover trigger queue controller id canonicality count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `triggerQueue[]` controllerId canonicality/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
