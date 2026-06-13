# Stage 4D-219P Recovery Spectator Trigger Queue Keyed Visible Source Canonicalized Duplicate Id Keyed Value Mismatch Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source canonicalized duplicate trigger-id plus keyed value mismatch validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithKeyedValueMismatchWithoutCountMismatch`.
- The test builds two natural authoritative visible-source trigger queue items and two matching spectator trigger queue items.
- The spectator payload mutates only the second item `triggerId` to `" trigger-visible-a "` while preserving that item's controller, source object id, source visibility, effect kind and triggered-event kind.
- Recovery validation must emit surrounding-whitespace canonicality, duplicate trigger-id, missing authoritative trigger-id, aggregate trigger-id disagreement and keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the duplicated normalized id.
- Recovery validation must avoid spectator replay timing trigger queue count mismatch, false unknown-trigger diagnostics for the canonical id and trigger-id redaction diagnostics.
- Existing visible-source non-canonical duplicate keyed-value no-count coverage, visible-source canonicalized duplicate missing-authoritative no-count coverage and hidden-source canonicalized duplicate keyed-value no-count coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithKeyedValueMismatchWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1886/1886`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1891/1891`.
- Backend full was not rerun for this first routine server-test shard after Stage 4D-219O; latest backend full remains Stage 4D-219O `8173/8173`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `3fa2e656` (`test: cover visible trigger queue canonical value duplicate`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source canonicalized duplicate trigger-id plus keyed value mismatch validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
