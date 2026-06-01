# Stage 4D-17RS Recovery Authoritative Card Object Redaction Sentinel Audit

Date: 2026-06-01
Owner: A_MAIN
Status: accepted

## Scope

Stage 4D-17RS tightens authoritative-state recovery validation for base card object payloads. Authoritative `CardObjectState` values now reject the view-redaction sentinel `HIDDEN` in internal machine-readable card-object fields:

- card object map key
- `objectId`
- `ownerId`
- `controllerId`
- `cardNo`
- `attachedToObjectId`
- `untilEndOfTurnEffects[]`
- `tags[]`

This continues the recent authoritative redaction-boundary hardening from stack, trigger queue, pending workflow and temporary payment resource state into card object state. `HIDDEN` remains a view redaction marker, not a legal authoritative recovery value.

## Runtime Changes

- `MatchRecoveryValidator` now emits explicit `must not be redacted` diagnostics when an authoritative card object map key, self object id, owner player, controller player, card number, attached object id, until-end effect id or tag equals `HIDDEN`.
- Existing blank, whitespace, duplicate, object-id mismatch, player-seat and numeric diagnostics are preserved.
- This slice intentionally leaves card-object power modifier ledger and object-location redaction sentinels for later smaller slices.
- No protocol shape, frontend, matrix JSON, official catalog or `fullOfficial` scope changed.

## Test Coverage

- Added `RecoveryValidatorRejectsAuthoritativeStateCardObjectRedactionSentinelDrift`.
- The test constructs an authoritative card object with `HIDDEN` in all newly protected base fields and asserts all eight explicit redaction diagnostics.

## Validation

- Focused card-object redaction sentinel test: `1/1`.
- Focused CardObject tests: `3/3`.
- Focused recovery tests: `571/571`.
- Adjacent recovery/opening/store-smoke filter: `1152/1152`.
- Backend full: `6517/6517`.
- Mechanical checks passed:
  - `git diff --check`
  - anchored conflict-marker scan over `docs`, `src`, `tests`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Project remains **NOT READY**. This closes only the Stage 4D-17RS authoritative card-object base-field redaction-sentinel slice.
