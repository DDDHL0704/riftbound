# Stage 4D-17TK Recovery Timing Temporary Payment Resource Keyed Value Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TK narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]` payloads. The slice targets the gap left after 17TJ: count mismatch now names missing and extra `resourceId` keys, but same-key authoritative value drift still relied on broad index-based parity that is skipped when counts differ.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now keys authoritative `MatchState.TemporaryPaymentResources` by `resourceId` and validates matching spectator replay-frame `temporaryPaymentResources[]` payloads before the count-mismatch early return.

The keyed value validation covers:

- `ownerPlayerId`, `sourceObjectId`, `abilityId` and `paymentWindow`
- `generatedPower` and `remainingPower`
- `generatedPowerByTrait` and `remainingPowerByTrait`
- `allowedPaymentKinds`
- `paymentOnly`
- `resourceRestriction`
- `createdTick`

This check runs alongside the 17TJ key-set validation, same-payload shape/value validation, duplicate resource-id validation and count mismatch diagnostic. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeyedValuesWithCountMismatch`.

The test builds a spectator replay frame from an authoritative temporary payment resource list containing `temp-payment-resource-1`, keeps that `resourceId` stable, mutates same-key fields, and adds a forged `temp-payment-resource-extra` entry to keep the spectator count-mismatch path active. Validation now reports same-key owner, source, ability, payment window, power, trait-map, allowed-kind, payment-only, restriction and created-tick diagnostics before the count-mismatch return.

## Validation

- Focused new test: `1/1`
- Focused TemporaryPaymentResource filter: `68/68`
- Focused recovery filter: `639/639`
- Adjacent recovery/opening/store-smoke filter: `1219/1219`
- Backend full: `6584/6584`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
