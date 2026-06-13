# Stage 4D-219O Recovery Spectator Trigger Queue Keyed Visible Source Canonicalized Duplicate Id Missing Authoritative Ids Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source canonicalized duplicate trigger-id plus unknown/missing-authoritative key-set validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch`.
- The test builds three natural authoritative visible-source trigger queue items and three matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to `" trigger-visible-a "`, changes its controller, effect kind and triggered-event kind while keeping its own source object, and mutates the third item `triggerId` to `trigger-extra`.
- Recovery validation must emit surrounding-whitespace canonicality, duplicate trigger-id, unknown trigger-id, required authoritative trigger-id diagnostics for both missing visible-source trigger ids, keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the duplicated normalized id, and aggregate trigger-id/controller/effect-kind/triggered-event-kind disagreement.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch and trigger-id redaction diagnostics.
- Existing visible-source non-canonical duplicate-id missing-authoritative no-count coverage, visible-source duplicate-id keyed-value no-count coverage, hidden-source canonicalized duplicate-id missing-authoritative no-count coverage, and generic key-set no-count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1885/1885`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1890/1890`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed `8173/8173`, refreshing the full backend gate after the Stage 4D-219M/219N/219O routine server-test shards following Stage 4D-219L.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `7b5d5bc8` (`test: cover visible trigger queue canonical duplicate keyset`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source canonicalized duplicate trigger-id plus unknown/missing-authoritative key-set validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
