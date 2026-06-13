# Stage 4D-218Q Recovery Spectator Trigger Queue Source Visibility Shape Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic source-visibility payload-shape validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceVisibilityShapeWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload mutates only `sourceVisibility` to a non-string array, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the source-visibility required diagnostic, the authoritative keyed source-visibility mismatch diagnostic and the aggregate source-visibility disagreement diagnostic.
- Recovery validation must avoid controller-id, source-object-id, effect-kind, triggered-event-kind disagreement and spectator replay timing trigger queue count mismatch.
- Existing keyed visible-source source-visibility shape, null, empty, canonicality, redaction and count-mismatch tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceVisibilityShapeWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1861/1861`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1866/1866`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test --no-restore` passed `8149/8149`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `ae3e4435` (`test: cover trigger queue source visibility shape without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue source-visibility shape validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
