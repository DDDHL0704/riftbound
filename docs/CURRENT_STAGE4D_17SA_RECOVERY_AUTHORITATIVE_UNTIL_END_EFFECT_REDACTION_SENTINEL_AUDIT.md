# Stage 4D-17SA Recovery Authoritative Until-End Effect Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17SA tightens authoritative-state recovery validation for the top-level until-end-of-turn effect id list. Authoritative internal effect ids now reject the view-redaction sentinel `HIDDEN`.

This follows Stage 4D-17RZ player-zone object-id redaction-boundary hardening and closes the adjacent top-level effect-list sentinel gap. Card-object nested until-end effect ids were already covered by the card-object redaction slice; this slice covers `MatchState.UntilEndOfTurnEffects`.

## Runtime Changes

- `MatchRecoveryValidator` now enables redaction-sentinel rejection for authoritative top-level `until end of turn effect` list values.
- Existing required-list, required-item, whitespace and duplicate diagnostics are preserved.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateUntilEndOfTurnEffectRedactionSentinelDrift`.
- The test constructs an authoritative state with top-level `UntilEndOfTurnEffects = ["HIDDEN"]` and asserts the explicit `must not be redacted` diagnostic.

## Validation

- Focused until-end effect redaction sentinel test: `1/1`.
- Focused UntilEndOfTurnEffect/CardObject tests: `6/6`.
- Focused recovery tests: `579/579`.
- Adjacent recovery/opening/store-smoke filter: `1160/1160`.
- Backend full: `6525/6525`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17SA authoritative until-end effect redaction-sentinel slice.
