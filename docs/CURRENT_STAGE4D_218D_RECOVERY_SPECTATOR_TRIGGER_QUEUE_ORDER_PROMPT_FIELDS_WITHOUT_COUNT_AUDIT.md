# Stage 4D-218D Recovery Spectator Trigger Queue Order Prompt Fields Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing order-trigger prompt-only field absence validation without relying on a trigger queue count mismatch.

## Coverage

- Renamed the existing equal-length spectator timing-map prompt-field test to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDriftWithoutCountMismatch`.
- The authoritative trigger queue count remains unchanged at zero and the spectator trigger queue is not count-shifted.
- The test injects prompt-only order-trigger fields into the spectator timing map: `orderingPlayerId`, `orderedTriggerIds`, `triggerIds`, `triggers`, `triggerChoices`, `legalOrderingConstraints`, `triggeredByEventKind` and `orderingState`.
- Recovery validation must emit the eight order-trigger prompt field absence diagnostics.
- The test now also proves these diagnostics are emitted without any spectator replay timing trigger queue count mismatch.
- The existing `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDriftWithCountMismatch` remains intact for the count-mismatch companion path.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOrderPromptFieldAbsenceDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1851/1851`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1856/1856`.
- Backend full was not rerun for this routine test naming/assertion shard; latest backend full remains Stage 4D-218B at `8138/8138`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `a90ac045` (`test: mark order prompt fields without trigger count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing order-trigger prompt field absence validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
