# Stage 4D-17RY Recovery Authoritative Decklist Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RY tightens authoritative-state recovery validation for player decklist contents. Authoritative decklist state now rejects the view-redaction sentinel `HIDDEN` in internal machine-readable decklist fields:

- legend card number
- champion card number
- main deck card numbers
- rune deck card numbers
- battlefield card numbers

This follows Stage 4D-17RW/17RX authoritative redaction-boundary hardening: the decklist player map key was already covered, and this slice closes the nested decklist card-number sentinel gap.

## Runtime Changes

- `MatchRecoveryValidator` now validates authoritative `PlayerDecklists` values after decklist player map-key validation.
- Decklist card-number scalars and card-number lists now emit explicit `must not be redacted` diagnostics for `HIDDEN`.
- Existing decklist player-seat membership, card-list duplicate legality and protocol shape are preserved; deck duplicates remain allowed.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateDecklistRedactionSentinelDrift`.
- The test constructs an authoritative state with `HIDDEN` in legend, champion, main deck, rune deck and battlefield decklist fields and asserts the explicit redaction diagnostics.

## Validation

- Focused decklist redaction sentinel test: `1/1`.
- Focused Decklist/AuthoritativeStatePlayer tests: `6/6`.
- Focused recovery tests: `577/577`.
- Adjacent recovery/opening/store-smoke filter: `1158/1158`.
- Backend full: `6523/6523`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RY authoritative decklist redaction-sentinel slice.
