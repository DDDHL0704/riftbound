# Stage 4D-17RX Recovery Authoritative Trait Power Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RX tightens authoritative-state recovery validation for trait-keyed power maps. Authoritative trait power state now rejects the view-redaction sentinel `HIDDEN` in internal machine-readable trait fields:

- rune pool `PowerByTrait` keys
- pending payment `PowerCostByTrait` keys
- temporary payment resource `GeneratedPowerByTrait` keys
- temporary payment resource `RemainingPowerByTrait` keys

This follows Stage 4D-17RP through 17RW authoritative redaction-boundary hardening and closes the adjacent trait-power sentinel gap in resource and payment state.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when authoritative trait power map keys use `HIDDEN`.
- The validator checks the trimmed original trait key before `RuneTrait.Normalize` can lower-case unknown keys, preserving the exact view-redaction sentinel boundary.
- Existing required, whitespace, positive-value and duplicate trait diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateTraitPowerRedactionSentinelDrift`.
- The test constructs an authoritative state with `HIDDEN` in rune pool, pending payment and temporary payment trait power maps and asserts the explicit redaction diagnostics.

## Validation

- Focused trait-power redaction sentinel test: `1/1`.
- Focused TraitPower/ResourceValue/TemporaryPaymentResource/PendingPayment tests: `85/85`.
- Focused recovery tests: `576/576`.
- Adjacent recovery/opening/store-smoke filter: `1157/1157`.
- Backend full: `6522/6522`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RX authoritative trait-power redaction-sentinel slice.
