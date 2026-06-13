# Stage 4D-221E Recovery Spectator Trigger Queue Source Object Id Missing Field Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: recovery spectator replay timing `triggerQueue[]` source object id missing-field diagnostics with a trigger-queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceObjectIdMissingFieldWithCountMismatch`.
- The new test builds a natural authoritative trigger queue item, mutates the spectator replay payload by removing `sourceObjectId`, then appends an otherwise valid `trigger-extra` spectator trigger.
- Recovery validation must emit the required source object id diagnostic and source object id mismatch diagnostic while also reporting the unknown extra trigger id and the `triggerQueue[]` count mismatch.
- Existing without-count source object id missing-field coverage, source object id shape/canonicality/null/empty coverage, triggered-event-kind count-mismatch coverage, triggerQueue controllerId count-mismatch coverage, keyed triggerQueue field/value count-mismatch coverage and broader recovery timing triggerQueue diagnostics remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests.RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceObjectIdMissingFieldWithCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1923/1923`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1928/1928`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8215/8215`.
- Mechanical checks passed before docs sync: `git diff --check`; anchored conflict-marker scan over `docs`, `src` and `tests`.

## Commits

- Code: `f1caed77` (`test: cover trigger queue source object missing count mismatch`)
- Docs: this checkpoint.

## Remaining

- This narrows recovery spectator timing `triggerQueue[]` source-object-id missing-field/count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
