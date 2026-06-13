# Stage 4D-218E Recovery Spectator Trigger Queue Mismatch Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing trigger queue aggregate field-drift validation without relying on a trigger queue count mismatch.

## Coverage

- Renamed the existing equal-length spectator timing trigger-queue mismatch test to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatchWithoutCountMismatch`.
- The authoritative trigger queue count remains unchanged at two, and the spectator replay frame keeps the same two trigger queue entries.
- The test mutates existing visible and hidden spectator trigger items across `triggerId`, `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind`.
- Recovery validation must emit all six aggregate disagreement diagnostics for ids, controller ids, source object ids, source visibilities, effect kinds and triggered event kinds.
- The test now also proves these diagnostics are emitted without any spectator replay timing trigger queue count mismatch.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMismatchWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1851/1851`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1856/1856`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8139/8139`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `885c9315` (`test: mark trigger queue mismatch without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue aggregate field-drift validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
