# Stage 4D-17IF Recovery Snapshot Timing Temporary Payment Resource List-Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame player-view snapshot validation slice for `Timing["temporaryPaymentResources"][]` item list-valued field `allowedPaymentKinds`.

Runtime change:

- `MatchRecoveryValidator.ValidateSnapshotShape` now rejects blank values, surrounding-whitespace values and duplicate normalized values in recovered player-view snapshot `Timing["temporaryPaymentResources"][]` item `allowedPaymentKinds` lists before temporary-payment-resource field consumers can read that list.

Test change:

- Added `RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourceAllowedPaymentKindListValueDrift`, covering whitespace-mutated `RUNE_COST`, duplicate normalized `RUNE_COST` and blank allowed-payment-kind values.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingTemporaryPaymentResourceAllowedPaymentKindListValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `259/259`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `840/840`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6205/6205`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
