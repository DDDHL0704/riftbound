# Stage 4D-17UD Recovery Spectator Trigger Queue Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `triggerQueue[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that keyed authoritative triggerQueue diagnostics only ran when same-key required fields were readable strings. If the spectator triggerQueue had a count mismatch, broad ordered parity was skipped; if a same-trigger payload then omitted or made required fields unreadable, the validator emitted generic shape diagnostics but not the keyed authoritative mismatch diagnostics that identify the expected controller, source object, visibility, effect kind or event kind.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-trigger spectator replay-frame timing `triggerQueue[]` required string fields even when a field is missing or unreadable.

The helper covers:

- `controllerId`;
- `sourceObjectId`;
- `sourceVisibility`;
- `effectKind`;
- `triggeredByEventKind`.

Readable value drift keeps the existing diagnostic shape. Missing/unreadable fields now emit the same authoritative expected-value context without an actual-value segment.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates a spectator replay-frame timing triggerQueue with:

- one authoritative visible trigger with source object `visible-source-1`;
- `sourceObjectId` removed from the same-key spectator payload;
- `sourceVisibility` removed from the same-key spectator payload;
- `effectKind` changed to an unreadable integer payload;
- `triggeredByEventKind` changed to an unreadable array payload;
- an extra trigger added so `triggerQueue` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required-field shape diagnostics for source object id, source visibility, effect kind and triggered event kind;
- keyed authoritative mismatch diagnostics for the same four required fields;
- triggerQueue count mismatch.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `TriggerQueue` filter: `89/89`.
- Focused `MatchRecoveryTests` filter: `657/657`.
- Adjacent recovery/opening/store-smoke filter: `1238/1238`.
- Backend full: `6603/6603`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
