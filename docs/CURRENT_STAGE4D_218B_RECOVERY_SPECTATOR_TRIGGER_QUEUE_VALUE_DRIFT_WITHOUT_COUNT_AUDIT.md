# Stage 4D-218B Recovery Spectator Trigger Queue Value Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` scalar value drift validation without relying on a trigger queue count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueValueDriftWithoutCountMismatch` is the existing equal-length spectator trigger queue scalar value drift test, now named and asserted as the no-count companion.
- The authoritative trigger queue count remains unchanged at two naturally emitted trigger items.
- The fixture keeps registered visible source object ids `source-1` and `source-2` in base zones, object registry and object locations so registry diagnostics stay out of scope.
- The spectator trigger queue is not count-shifted; it mutates the existing two trigger items with raw JSON value drift across `triggerId`, `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind`.
- Recovery validation must emit surrounding-whitespace, duplicate-trigger-id, required-value and invalid-value diagnostics for those scalar fields.
- The test now also proves these diagnostics are emitted without any spectator replay timing trigger queue count mismatch.
- The existing `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueValueDriftWithCountMismatch` remains intact for the unexpected-trigger and count-mismatch path.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueValueDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1850/1850`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1855/1855`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8138/8138`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `f5eea55f` (`test: mark trigger queue value drift without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue scalar value drift validation without count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
