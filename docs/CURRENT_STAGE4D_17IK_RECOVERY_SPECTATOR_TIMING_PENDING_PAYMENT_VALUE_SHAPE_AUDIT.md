# Stage 4D-17IK Recovery Spectator Timing Pending Payment Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame spectator replay validation slice for `Timing["pendingPayment"]` value shape.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates present spectator replay-frame `Timing["pendingPayment"]` scalar/list/cost/trait values before pending-payment comparisons consume those payloads.
- `paymentId`, `paymentWindow` and `playerId` reject blank values and surrounding-whitespace values.
- `paymentChoices` and `paymentResourceActions` reject blank, whitespace-mutated and duplicate normalized values.
- Present `cost` payloads validate non-negative integer `mana` and `power` values.
- Present `cost.powerByTrait` must be an object map whose trait values are positive integers.

Test change:

- Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentValueDrift`, covering whitespace, blank, negative, non-integer, non-positive trait-cost and duplicate/blank list drift.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingPendingPaymentValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `264/264`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `845/845`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6210/6210`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
