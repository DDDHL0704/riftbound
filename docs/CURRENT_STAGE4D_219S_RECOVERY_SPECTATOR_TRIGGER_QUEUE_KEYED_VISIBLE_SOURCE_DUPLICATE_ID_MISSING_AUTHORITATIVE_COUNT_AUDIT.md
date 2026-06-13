# Stage 4D-219S Recovery Spectator Trigger Queue Keyed Visible Source Duplicate Id Missing Authoritative Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source duplicate trigger-id plus missing-authoritative/unknown key-set validation with the trigger queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithMissingAuthoritativeAndCountMismatch`.
- The test builds two natural authoritative visible-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates the second item `triggerId` to duplicate `trigger-visible-a`, drifts its controller, effect kind and triggered-event kind, and appends a cloned first item with `triggerId` changed to `trigger-extra`.
- Recovery validation must emit duplicate trigger-id, unknown extra trigger-id, required authoritative trigger-id, keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the duplicate id and the trigger queue count mismatch.
- Recovery validation must avoid false unknown-trigger diagnostics for the duplicate id, aggregate trigger-id disagreement and trigger-id redaction diagnostics.
- Existing visible-source duplicate missing-authoritative no-count coverage, visible-source canonicalized duplicate missing-authoritative count coverage and hidden-source duplicate missing-authoritative count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithMissingAuthoritativeAndCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1889/1889`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1894/1894`.
- Backend full was not rerun for this first routine server-test shard after Stage 4D-219R; latest backend full remains Stage 4D-219R `8176/8176`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `0cddd9d5` (`test: cover visible trigger queue duplicate missing count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source duplicate trigger-id plus missing-authoritative count-mismatch validation only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
