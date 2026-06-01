# Stage 4D-17SD Recovery Authoritative Empty Registry Workflow Reference Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17SD tightens authoritative-state recovery validation when the canonical object registry is empty. Workflow and resolution object references now still run through object-registry membership validation instead of being skipped after the stack/trigger empty-registry checks.

This follows Stage 4D-17SC power modifier source registry validation and closes the adjacent empty-registry branch for:

- pending hand choice source object and legal object ids
- temporary payment resource source object ids
- battlefield resolution battlefield/source/participant object ids
- battle resolution battlefield, attacker, defender, survivor and destroyed object ids

## Runtime Changes

- `MatchRecoveryValidator` no longer returns early from authoritative object-reference validation when the canonical object registry is empty.
- Stack and trigger queue empty-registry diagnostics are preserved.
- Pending hand choice, temporary payment resource and resolution-history object references now emit the same explicit missing-registry diagnostics under an empty object registry as they already did when another object existed in the registry.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateWorkflowObjectReferencesWithEmptyObjectRegistry`.
- The test constructs authoritative pending hand choice, temporary payment resource, battlefield resolution and battle resolution object references without any card-object or object-location registry entries and asserts explicit missing-registry diagnostics.

## Validation

- Focused workflow empty-registry test: `1/1`.
- Focused empty-registry/object-reference tests: `7/7`.
- Focused recovery tests: `582/582`.
- Adjacent recovery/opening/store-smoke filter: `1163/1163`.
- Backend full: `6528/6528`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17SD authoritative empty-registry workflow/resolution object-reference slice.
