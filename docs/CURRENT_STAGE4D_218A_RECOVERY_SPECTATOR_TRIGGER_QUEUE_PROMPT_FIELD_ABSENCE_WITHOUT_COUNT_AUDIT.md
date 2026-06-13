# Stage 4D-218A Recovery Spectator Trigger Queue Prompt Field Absence Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` prompt-only field absence validation without relying on a trigger queue count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePromptFieldAbsenceWithoutCountMismatch` builds a spectator replay frame from authoritative trigger queue state.
- The authoritative trigger queue count remains unchanged at one naturally emitted trigger item.
- The fixture registers source object id `source-1` in `alice`'s base zone, object registry and object locations so registry and ordered parity diagnostics stay out of scope.
- The spectator trigger item is not synthetically added and is not count-shifted; it naturally emits `trigger-1`, controller `alice`, source object id `source-1`, `sourceVisibility` `VISIBLE`, effect kind `LAST_BREATH` and triggered event kind `OBJECT_DESTROYED`.
- The test injects prompt-only fields `summary` and `visibleText` into that existing trigger item.
- Recovery validation must emit both prompt field absence diagnostics.
- The test also proves these diagnostics are emitted without a keyed source-object mismatch and without any spectator replay timing trigger queue count mismatch.
- The existing synthetic prompt-field absence test with count mismatch remains intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePromptFieldAbsenceWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1850/1850`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1855/1855`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8138/8138`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `5a488bed` (`test: cover trigger queue prompt fields without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue prompt-field absence validation without count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
