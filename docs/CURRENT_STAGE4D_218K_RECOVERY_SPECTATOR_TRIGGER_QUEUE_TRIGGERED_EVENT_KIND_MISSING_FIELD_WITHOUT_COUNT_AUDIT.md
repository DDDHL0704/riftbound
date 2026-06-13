# Stage 4D-218K Recovery Spectator Trigger Queue Triggered Event Kind Missing Field Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic triggered-event-kind missing-field validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindMissingFieldWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload removes only `triggeredByEventKind`, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the triggered-event-kind required diagnostic, the authoritative keyed triggered-event-kind mismatch diagnostic and the aggregate triggered-event-kind disagreement diagnostic.
- Recovery validation must avoid any spectator replay timing trigger queue count mismatch.
- Existing combined required-field absence, null, empty, shape, redaction-sentinel, invalid-value and count-mismatch triggered-event-kind tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindMissingFieldWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1855/1855`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1860/1860`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore` passed `8143/8143`, refreshing the full backend gate after the Stage 4D-218I/218J/218K trigger-queue missing-field bundle.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `6251707a` (`test: cover trigger queue missing triggered event kind without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue triggered-event-kind missing-field validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
