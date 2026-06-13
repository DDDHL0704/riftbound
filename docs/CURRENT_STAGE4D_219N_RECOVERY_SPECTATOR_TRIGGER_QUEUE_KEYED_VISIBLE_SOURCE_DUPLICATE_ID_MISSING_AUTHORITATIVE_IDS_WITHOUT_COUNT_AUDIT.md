# Stage 4D-219N Recovery Spectator Trigger Queue Keyed Visible Source Duplicate Id Missing Authoritative Ids Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source duplicate trigger-id plus unknown/missing-authoritative key-set validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch`.
- The test builds three natural authoritative visible-source trigger queue items and three matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to duplicate `trigger-visible-a`, changes its controller, effect kind and triggered-event kind while keeping its own source object, and mutates the third item `triggerId` to `trigger-extra`.
- Recovery validation must emit duplicate trigger-id, unknown trigger-id, required authoritative trigger-id diagnostics for both missing visible-source trigger ids, keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the duplicated id, and aggregate trigger-id/controller/effect-kind/triggered-event-kind disagreement.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch and trigger-id redaction diagnostics.
- Existing visible-source duplicate-id keyed-value no-count coverage, generic duplicate-id no-count coverage, generic key-set no-count coverage, and Stage 4D-219L/219M hidden-source duplicate missing-authoritative no-count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1884/1884`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1889/1889`.
- Backend full was not rerun for this second routine server-test shard after Stage 4D-219L; latest backend full remains Stage 4D-219L `8170/8170`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `af70e140` (`test: cover visible trigger queue duplicate keyset`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source duplicate trigger-id plus unknown/missing-authoritative key-set validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
