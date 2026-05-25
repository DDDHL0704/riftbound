# Stage 4D-17IL Recovery Spectator Timing Temporary Payment Resource Value Shape Audit

Date: 2026-05-25

Project status: **NOT READY**.

## Scope

A_MAIN accepted a small server P1-004 recovery-frame spectator replay validation slice for `Timing["temporaryPaymentResources"][]` value shape.

Runtime change:

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates present spectator replay-frame `Timing["temporaryPaymentResources"][]` scalar/base/trait/list values before temporary-payment-resource comparisons consume those payloads.
- `resourceId`, `ownerPlayerId` and `resourceRestriction` reject missing, blank and surrounding-whitespace values.
- Optional-empty `sourceObjectId`, `abilityId` and `paymentWindow` remain compatible, but present non-string or surrounding-whitespace values fail explicitly.
- `generatedPower`, `remainingPower` and `createdTick` must be integer non-negative values.
- `generatedPowerByTrait` and `remainingPowerByTrait` must be object maps whose trait values are positive integers.
- `allowedPaymentKinds` rejects blank, whitespace-mutated and duplicate normalized values.
- `paymentOnly` must be present and true.

Test change:

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceValueDrift`, covering whitespace, blank, non-string, negative, non-integer, non-positive trait-map, invalid list, false-boolean and negative-tick drift.

## Validation

Passed:

- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceValueDrift"`: `1/1`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`: `265/265`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests|FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~PostgresMatchRecoveryStoreSmokeTests"`: `846/846`.
- `DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" dotnet test Riftbound.slnx --no-restore`: `6211/6211`.
- `git diff --check`.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs src tests --glob "!src/Riftbound.DevUi/node_modules/**"`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Locks

Not changed: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, Chrome/browser/formal E2E scripts, `fullOfficial`, final readiness status and `riftbound-dotnet.sln`.

Remaining open: P0/P1 overall closure, broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E and `fullOfficial`.
