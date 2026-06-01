# Stage 4D-17RV Recovery Authoritative Resolution History Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RV tightens authoritative-state recovery validation for battlefield and battle resolution history payloads. Authoritative resolution history now rejects the view-redaction sentinel `HIDDEN` in internal machine-readable resolution fields:

- resolution ids
- resolution kind and reason
- battlefield object ids
- resolution player ids
- previous/controller/attacking/defending/winner player ids
- source object ids
- participant, attacker, defender, surviving attacker, surviving defender and destroyed object id lists
- related event kind lists

This follows the Stage 4D-17RP through 17RU authoritative redaction-boundary hardening and closes the adjacent resolution-history sentinel gap noted in the current closure docs.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when authoritative battlefield or battle resolution metadata uses `HIDDEN`.
- Existing required, blank, whitespace, duplicate, tick-range, player-seat and object-registry diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateResolutionHistoryRedactionSentinelDrift`.
- The test constructs authoritative battlefield and battle resolution history entries with `HIDDEN` in all newly protected metadata fields and asserts the explicit redaction diagnostics.

## Validation

- Focused resolution-history redaction sentinel test: `1/1`.
- Focused ResolutionHistory/Resolution tests: `77/77`.
- Focused recovery tests: `574/574`.
- Adjacent recovery/opening/store-smoke filter: `1155/1155`.
- Backend full: `6520/6520`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RV authoritative resolution-history redaction-sentinel slice.
