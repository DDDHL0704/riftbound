# Stage 4D-17RZ Recovery Authoritative Player Zone Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RZ tightens authoritative-state recovery validation for player-zone object ids. Authoritative player-zone state now rejects the view-redaction sentinel `HIDDEN` in internal machine-readable object-id lists:

- main deck
- rune deck
- hand
- base
- battlefield
- graveyard
- banished
- legend zone
- champion zone

This follows Stage 4D-17RY decklist card-number redaction-boundary hardening and closes the adjacent player-zone object-id sentinel gap.

## Runtime Changes

- `MatchRecoveryValidator` now rejects `HIDDEN` while building the authoritative player-zone object index for value diagnostics.
- Existing required-list, required-object, whitespace and duplicate-location diagnostics are preserved.
- The object-location comparison pass still reuses the same index without emitting duplicate redaction diagnostics.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStatePlayerZoneObjectRedactionSentinelDrift`.
- The test constructs an authoritative state with `HIDDEN` in all nine player-zone object-id lists and asserts the explicit redaction diagnostics.

## Validation

- Focused player-zone redaction sentinel test: `1/1`.
- Focused PlayerZone/ObjectLocation tests: `31/31`.
- Focused recovery tests: `578/578`.
- Adjacent recovery/opening/store-smoke filter: `1159/1159`.
- Backend full: `6524/6524`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RZ authoritative player-zone redaction-sentinel slice.
