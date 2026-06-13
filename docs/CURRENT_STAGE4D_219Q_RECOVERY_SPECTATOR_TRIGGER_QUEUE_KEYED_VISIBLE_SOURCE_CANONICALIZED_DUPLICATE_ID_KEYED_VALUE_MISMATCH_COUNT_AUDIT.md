# Stage 4D-219Q Recovery Spectator Trigger Queue Keyed Visible Source Canonicalized Duplicate Id Keyed Value Mismatch Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` keyed visible-source canonicalized duplicate trigger-id plus keyed value mismatch validation with the trigger queue count mismatch present.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithKeyedValueMismatchAndCountMismatch`.
- The test builds one natural authoritative visible-source trigger queue item and one matching spectator trigger queue item.
- The spectator payload clones that item, mutates the cloned `triggerId` to `" trigger-visible "`, and drifts the cloned controller, source object id, effect kind and triggered-event kind.
- Recovery validation must emit surrounding-whitespace canonicality, duplicate trigger-id, keyed controller/source-object/effect-kind/triggered-event-kind mismatches for the normalized id and the trigger queue count mismatch.
- Recovery validation must avoid false unknown-trigger diagnostics for the canonical id, required-authoritative diagnostics for the same canonical id, aggregate trigger-id disagreement and trigger-id redaction diagnostics.
- Existing visible-source non-canonical duplicate count-mismatch coverage, visible-source canonicalized duplicate keyed-value no-count coverage and hidden-source canonicalized duplicate keyed-value count-mismatch coverage remain intact.

## Validation

- Focused test: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceCanonicalizedDuplicateIdWithKeyedValueMismatchAndCountMismatch"` passed `1/1`.
- Changed-class filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1887/1887`.
- Adjacent recovery filter: `DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` passed `1892/1892`.
- Backend full was not rerun for this second routine server-test shard after Stage 4D-219O; latest backend full remains Stage 4D-219O `8173/8173`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<{7}|={7}|>{7})( |$)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs`.

## Commits

- Code: `e1255aa5` (`test: cover visible trigger queue canonical duplicate count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue keyed visible-source canonicalized duplicate trigger-id plus keyed value mismatch validation with trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
