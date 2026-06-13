# Stage 4D-218T Recovery Spectator Trigger Queue Effect Kind Canonicality Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` generic effect-kind whitespace canonicality validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueEffectKindCanonicalityWithoutCountMismatch`.
- The test builds a natural authoritative visible-source trigger queue item with one spectator trigger queue item.
- The spectator payload mutates only `effectKind` to a surrounding-whitespace value, keeping the spectator trigger queue length equal to the authoritative trigger queue length of one.
- Recovery validation must emit the effect-kind surrounding-whitespace diagnostic, the authoritative keyed effect-kind mismatch diagnostic and the aggregate effect-kind disagreement diagnostic.
- Recovery validation must avoid controller-id, source-object-id, source-visibility, triggered-event-kind disagreement and spectator replay timing trigger queue count mismatch.
- Existing keyed visible-source effect-kind canonicality, generic effect-kind shape/missing-field/redaction and value-drift tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueEffectKindCanonicalityWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1864/1864`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1869/1869`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test --no-restore` passed `8152/8152`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `c99b155f` (`test: cover trigger queue effect kind canonicality without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue effect-kind canonicality validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
