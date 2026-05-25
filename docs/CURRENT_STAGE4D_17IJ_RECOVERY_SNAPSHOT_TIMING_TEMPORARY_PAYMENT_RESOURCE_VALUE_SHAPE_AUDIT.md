# Stage 4D-17IJ Recovery Snapshot Timing Temporary Payment Resource Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame player-view snapshot validation slice for `Timing["temporaryPaymentResources"][]` item value shape.

Runtime change:

- `MatchRecoveryValidator.ValidateSnapshotShape` now validates recovered player-view snapshot temporary-payment-resource scalar/base fields before consumers read those payloads.
- `resourceId`, `ownerPlayerId`, `generatedPower`, `remainingPower`, `generatedPowerByTrait`, `remainingPowerByTrait`, `paymentOnly`, `resourceRestriction` and `createdTick` are required when a temporary-payment-resource item is present.
- Optional-empty `sourceObjectId`, `abilityId` and `paymentWindow` remain compatible, but present non-string or whitespace-mutated values fail explicitly.
- Trait maps must be object maps whose values are positive integers, generated/remaining power and created tick must be non-negative integer values, and `paymentOnly` must be true.

Test change:

- Added `RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourceValueDrift`, covering whitespace, missing, non-string, negative, non-integer, false-boolean and missing-map drift.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourceValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `263/263`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `844/844`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6209/6209`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
