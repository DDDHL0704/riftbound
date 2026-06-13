# Stage 4D-219R Recovery Spectator Trigger Queue Keyed Visible Source Canonicalized Duplicate Id Missing Authoritative Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source canonicalized duplicate trigger-id plus missing-authoritative/unknown key-set validation with the trigger queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithMissingAuthoritativeAndCountMismatch`.
- The test builds two natural authoritative visible-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to `" trigger-visible-a "`, drifts its controller, effect kind and triggered-event kind, and appends a cloned first item with `triggerId` changed to `trigger-extra`.
- Recovery validation must emit surrounding-whitespace canonicality, duplicate trigger-id, unknown extra trigger-id, required authoritative trigger-id, keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the normalized duplicate id and the trigger queue count mismatch.
- Recovery validation must avoid false unknown-trigger diagnostics for the canonical id, aggregate trigger-id disagreement and trigger-id redaction diagnostics.
- Existing visible-source canonicalized duplicate missing-authoritative no-count coverage, visible-source canonicalized duplicate keyed-value count coverage and hidden-source canonicalized duplicate missing-authoritative count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithMissingAuthoritativeAndCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1888/1888`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1893/1893`.
- Backend full: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test Riftbound.slnx --no-restore` passed `8176/8176`, refreshing the full backend gate after the Stage 4D-219P/219Q/219R routine server-test shards following Stage 4D-219O.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `127a7e9a` (`test: cover visible trigger queue canonical missing count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source canonicalized duplicate trigger-id plus missing-authoritative count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
