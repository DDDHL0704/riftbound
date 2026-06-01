# Stage 4D-17RW Recovery Authoritative Player Identity Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RW tightens authoritative-state recovery validation for top-level player identity and seat payloads. Authoritative player identity state now rejects the view-redaction sentinel `HIDDEN` in internal machine-readable player fields:

- seat player ids and seat values
- required active and turn player pointers
- optional priority, focus, winner, opening-second-action and extra-turn player pointers
- ready, passed-priority, passed-focus, mulligan-completed and destroyed-unit-owner player lists
- rune pool, player zone, score, experience, cards-played-this-turn and decklist player map keys

This follows Stage 4D-17RP through 17RV authoritative redaction-boundary hardening and closes the adjacent top-level player identity sentinel gap noted in the current closure docs.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when authoritative top-level player identity fields use `HIDDEN`.
- Existing required, blank, whitespace, duplicate, seat-membership and seat-validity diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStatePlayerIdentityRedactionSentinelDrift`.
- The test constructs an authoritative state with `HIDDEN` in all newly protected seat, player pointer, player list and player map-key fields and asserts the explicit redaction diagnostics.

## Validation

- Focused player-identity redaction sentinel test: `1/1`.
- Focused AuthoritativeStatePlayer/AuthoritativeStateSeat tests: `6/6`.
- Focused recovery tests: `575/575`.
- Adjacent recovery/opening/store-smoke filter: `1156/1156`.
- Backend full: `6521/6521`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RW authoritative player-identity redaction-sentinel slice.
