# Stage 4D-17SB Recovery Authoritative Scalar Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17SB tightens authoritative-state recovery validation for top-level scalar text fields. Authoritative internal scalar state now rejects the view-redaction sentinel `HIDDEN` in:

- room id
- status
- phase
- timing state

This follows Stage 4D-17SA top-level until-end effect hardening and closes the adjacent top-level scalar sentinel gap. Player pointers and seat/map identity values were already covered by Stage 4D-17RW.

## Runtime Changes

- `MatchRecoveryValidator` now rejects `HIDDEN` for authoritative room id after required/whitespace normalization.
- The known-text scalar validator can reject redaction sentinels and now applies that check to status, phase and timing state.
- Existing required, whitespace and known-value diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateScalarRedactionSentinelDrift`.
- The test constructs an authoritative state with `HIDDEN` in room id, status, phase and timing state and asserts the explicit redaction diagnostics.

## Validation

- Focused scalar redaction sentinel test: `1/1`.
- Focused AuthoritativeStateScalar tests: `2/2`.
- Focused recovery tests: `580/580`.
- Adjacent recovery/opening/store-smoke filter: `1161/1161`.
- Backend full: `6526/6526`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17SB authoritative scalar redaction-sentinel slice.
