# Stage 4D-17SC Recovery Authoritative Power Modifier Source Registry Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17SC tightens authoritative-state recovery validation for card-object power modifier source object references. Authoritative `PowerModifierLedgerEntry.SourceObjectId` values are now checked against the canonical object registry when present and not the redaction sentinel.

This follows Stage 4D-17RT power modifier value/redaction validation and closes the adjacent object-registry membership gap for nested power modifier ledger sources. The slice is limited to recovery authoritative-state validation.

## Runtime Changes

- `MatchRecoveryValidator` now walks `CardObjectState.UntilEndOfTurnPowerModifiers` during authoritative object-reference validation.
- Nonblank, non-`HIDDEN` power modifier source object ids must exist in the canonical object registry built from `CardObjects` and `ObjectLocations`.
- Existing value-shape, target-object consistency, redaction-sentinel, null-ledger and whitespace diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateCardObjectPowerModifierSourceObjectOutsideRegistry`.
- The test constructs an authoritative card object with a tracked power modifier whose `sourceObjectId` is absent from the object registry and asserts the explicit missing-registry diagnostic.

## Validation

- Focused power modifier source registry test: `1/1`.
- Focused card-object power-modifier/object-reference tests: `4/4`.
- Focused recovery tests: `581/581`.
- Adjacent recovery/opening/store-smoke filter: `1162/1162`.
- Backend full: `6527/6527`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17SC authoritative power modifier source-object registry slice.
