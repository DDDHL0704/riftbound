# Stage 4D-17TJ Recovery Timing Temporary Payment Resource Key Set Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TJ narrows P1-004 recovery/replay determinism for spectator replay-frame timing `temporaryPaymentResources[]` payloads. The slice targets the same count-mismatch gap recently closed for trigger queue and continuous effects: broad index-based authoritative parity is skipped when spectator and authoritative list counts differ, so missing or forged temporary-resource identities needed explicit key-set validation before that early return.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now builds an authoritative temporary payment resource index keyed by `resourceId` from `MatchState.TemporaryPaymentResources`.

Before the count-mismatch early return, spectator replay-frame `temporaryPaymentResources[]` payloads now report:

- resource ids that are not present in authoritative temporary payment resources
- authoritative temporary payment resource ids that are missing from the spectator payload

This check runs alongside the existing same-payload shape/value validation, duplicate resource-id validation, and count mismatch diagnostic. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTemporaryPaymentResourceKeySetWithCountMismatch`.

The test builds a spectator replay frame from an authoritative temporary payment resource list containing `temp-payment-resource-1`, replaces the visible spectator resource id with `temp-payment-resource-extra-a`, adds `temp-payment-resource-extra-b`, and keeps the spectator count-mismatch path active. Validation now reports both forged resource ids and the missing authoritative resource id before the count-mismatch return.

## Validation

- Focused new test: `1/1`
- Focused TemporaryPaymentResource filter: `67/67`
- Focused recovery filter: `638/638`
- Adjacent recovery/opening/store-smoke filter: `1218/1218`
- Backend full: `6583/6583`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
