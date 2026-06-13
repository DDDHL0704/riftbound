# Stage 4D-219M Recovery Spectator Trigger Queue Keyed Hidden Source Canonicalized Duplicate Id Missing Authoritative Ids Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed hidden-source canonicalized duplicate trigger-id plus unknown/missing-authoritative key-set validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch`.
- The test builds three natural authoritative hidden-source trigger queue items and three matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to `" trigger-hidden-a "`, changes its controller and triggered-event kind, and mutates the third item `triggerId` to `trigger-extra`.
- Recovery validation must emit surrounding-whitespace canonicality, duplicate trigger-id, unknown trigger-id, required authoritative trigger-id diagnostics for both missing hidden-source trigger ids, keyed controller/triggered-event mismatches for the duplicated normalized id, and aggregate trigger-id/controller/triggered-event disagreement.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch and the hidden-source trigger-id redaction diagnostic.
- Existing hidden-source canonicalized duplicate-id no-count coverage, hidden-source canonicalized duplicate-id keyed-value no-count coverage, hidden-source canonicalized duplicate-id with missing-authoritative count-mismatch coverage, and Stage 4D-219L non-canonical duplicate missing-authoritative no-count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithMissingAuthoritativeIdsWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1883/1883`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecovery"` passed `1888/1888`.
- Backend full was not rerun for this first routine server-test shard after Stage 4D-219L; latest backend full remains Stage 4D-219L `8170/8170`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src tests`.

## Commits

- Code: `7c1a30a8` (`test: cover hidden trigger queue canonical duplicate keyset`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed hidden-source canonicalized duplicate trigger-id plus unknown/missing-authoritative key-set validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
