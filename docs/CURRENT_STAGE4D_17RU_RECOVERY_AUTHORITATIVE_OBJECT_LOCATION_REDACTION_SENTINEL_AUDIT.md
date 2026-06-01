# Stage 4D-17RU Recovery Authoritative Object Location Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RU tightens authoritative-state recovery validation for object location payloads. Authoritative `ObjectLocationState` values now reject the view-redaction sentinel `HIDDEN` in internal machine-readable object-location fields:

- object location map key
- `playerId`
- `zone`
- `battlefieldObjectId`

This follows Stage 4D-17RS and 17RT card-object redaction-boundary hardening and closes the adjacent object-location sentinel gap noted in the current closure docs.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when an authoritative object location map key, player id, zone or battlefield object id equals `HIDDEN`.
- Existing blank, whitespace, duplicate, supported-zone, player-seat, player-zone and object-reference diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateObjectLocationRedactionSentinelDrift`.
- The test constructs an authoritative object location with `HIDDEN` in all newly protected fields and asserts all four explicit redaction diagnostics.

## Validation

- Focused object-location redaction sentinel test: `1/1`.
- Focused ObjectLocation/CardObject tests: `21/21`.
- Focused recovery tests: `573/573`.
- Adjacent recovery/opening/store-smoke filter: `1154/1154`.
- Backend full: `6519/6519`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RU authoritative object-location redaction-sentinel slice.
