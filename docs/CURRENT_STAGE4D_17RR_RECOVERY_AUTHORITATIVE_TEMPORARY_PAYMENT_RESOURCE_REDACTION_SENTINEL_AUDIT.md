# Stage 4D-17RR Recovery Authoritative Temporary Payment Resource Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RR tightens authoritative-state recovery validation for temporary payment resources. Authoritative `TemporaryPaymentResourceState` values now reject the view-redaction sentinel `HIDDEN` in internal machine-readable fields:

- `resourceId`
- `ownerPlayerId`
- `sourceObjectId`
- `abilityId`
- `paymentWindow`
- `allowedPaymentKinds[]`

This keeps the authoritative temporary payment resource payload on the same redaction boundary as the recent stack, trigger queue, pending payment and pending hand choice slices. `HIDDEN` remains a view redaction marker, not a legal authoritative recovery value.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when an authoritative temporary payment resource id, owner player, source object, ability id, payment window or allowed payment kind equals `HIDDEN`.
- Existing blank, whitespace, duplicate, player-seat and value-shape diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateTemporaryPaymentResourceRedactionSentinelDrift`.
- The test constructs an authoritative temporary payment resource with `HIDDEN` in all newly protected fields and asserts all six explicit redaction diagnostics.

## Validation

- Focused temporary-payment-resource redaction sentinel test: `1/1`.
- Focused temporary/payment resource tests: `88/88`.
- Focused recovery tests: `570/570`.
- Adjacent recovery/opening/store-smoke filter: `1151/1151`.
- Backend full: `6516/6516`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RR authoritative temporary payment resource redaction-sentinel slice.
