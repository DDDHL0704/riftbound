# Stage 4D-17RT Recovery Authoritative Card Object Power Modifier Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RT tightens authoritative-state recovery validation for card object power modifier ledger payloads. Authoritative `PowerModifierLedgerEntry` values now reject the view-redaction sentinel `HIDDEN` in internal machine-readable ledger fields:

- `effectId`
- `effectKind`
- `duration`
- `targetObjectId`
- `sourcePath`
- `sourceObjectId`
- `sourceCardNo`

This follows Stage 4D-17RS card object base-field redaction-boundary hardening and keeps power modifier ledger state from accepting view-redacted values as authoritative recovery data.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when an authoritative card object power modifier id, effect kind, duration, target object, source path, source object or source card number equals `HIDDEN`.
- Existing blank, whitespace, duplicate, target-object consistency, numeric and applied-order diagnostics are preserved.
- Object-location redaction sentinels remain a separate follow-up slice.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateCardObjectPowerModifierRedactionSentinelDrift`.
- The test constructs an authoritative card object power modifier ledger entry with `HIDDEN` in all newly protected string fields and asserts all seven explicit redaction diagnostics.

## Validation

- Focused card-object power modifier redaction sentinel test: `1/1`.
- Focused CardObject tests: `4/4`.
- Focused recovery tests: `572/572`.
- Adjacent recovery/opening/store-smoke filter: `1153/1153`.
- Backend full: `6518/6518`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RT authoritative card-object power-modifier redaction-sentinel slice.
