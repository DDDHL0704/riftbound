# Stage 4D-219D Recovery Spectator Trigger Queue Source Visibility Empty Value Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic source-visibility empty-value validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceVisibilityEmptyValueWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload mutates only `sourceVisibility` to an empty string, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the source-visibility required diagnostic, the authoritative keyed source-visibility mismatch diagnostic and the aggregate source-visibility disagreement diagnostic.
- Recovery validation must avoid controller-id, source-object-id, effect-kind, triggered-event-kind disagreement and spectator replay timing trigger queue count mismatch.
- Existing keyed visible-source source-visibility empty/null/shape/canonicality/invalid tests, generic source-visibility missing-field/null/shape/canonicality tests and aggregate value-drift tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSourceVisibilityEmptyValueWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1874/1874`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1879/1879`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test --no-restore` passed `8162/8162`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `7f8552e5` (`test: cover trigger queue source visibility empty value without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue source-visibility empty-value validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
