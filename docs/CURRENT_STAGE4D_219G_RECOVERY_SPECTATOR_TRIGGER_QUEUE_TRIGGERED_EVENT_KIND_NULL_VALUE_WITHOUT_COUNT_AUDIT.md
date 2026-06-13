# Stage 4D-219G Recovery Spectator Trigger Queue Triggered Event Kind Null Value Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic triggered-event-kind null-value validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindNullValueWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload mutates only `triggeredByEventKind` to `null`, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the triggered-event-kind required diagnostic, the authoritative keyed triggered-event-kind mismatch diagnostic and the aggregate triggered-event-kind disagreement diagnostic.
- Recovery validation must avoid controller-id, source-object-id, source-visibility, effect-kind disagreement and spectator replay timing trigger queue count mismatch.
- Existing keyed visible/hidden-source triggered-event-kind empty/null/shape/canonicality tests, generic triggered-event-kind missing-field/shape/canonicality tests and aggregate value-drift tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindNullValueWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1877/1877`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1882/1882`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test --no-restore` passed `8165/8165`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `5057bad3` (`test: cover trigger queue triggered event kind null value without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue triggered-event-kind null-value validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
