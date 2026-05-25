# Stage 4D-17IM Recovery Spectator Timing Trigger Queue Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame spectator replay validation slice for `Timing["triggerQueue"][]` value shape.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates present spectator replay-frame `Timing["triggerQueue"][]` scalar values before trigger-queue comparisons consume those payloads.
- `triggerId`, `controllerId`, `sourceObjectId`, `effectKind` and `triggeredByEventKind` reject missing, blank and surrounding-whitespace values.
- `sourceVisibility` rejects missing, blank and surrounding-whitespace values, and must be one of `VISIBLE` or `HIDDEN`.
- Duplicate normalized spectator `triggerId` values now produce explicit diagnostics.

Test change:

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueValueDrift`, covering whitespace, blank, non-string source-object, invalid visibility and duplicate trigger-id drift.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `266/266`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `847/847`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6212/6212`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
