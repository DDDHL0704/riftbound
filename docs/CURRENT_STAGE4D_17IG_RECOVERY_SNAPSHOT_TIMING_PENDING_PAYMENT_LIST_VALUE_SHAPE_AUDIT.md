# Stage 4D-17IG Recovery Snapshot Timing Pending Payment List-Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame player-view snapshot validation slice for `Timing["pendingPayment"]` list-valued fields `paymentChoices` and `paymentResourceActions`.

Runtime change:

- `MatchRecoveryValidator.ValidateSnapshotShape` now rejects blank values, surrounding-whitespace values and duplicate normalized values in recovered player-view snapshot `Timing["pendingPayment"]["paymentChoices"]` and `Timing["pendingPayment"]["paymentResourceActions"]` before pending-payment field consumers can read those lists.

Test change:

- Added `RecoveryValidatorRejectsSnapshotTimingPendingPaymentListValueDrift`, covering whitespace-mutated, duplicate normalized and blank payment-choice / payment-resource-action values.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingPendingPaymentListValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `260/260`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `841/841`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6206/6206`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
