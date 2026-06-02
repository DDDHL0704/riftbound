# Stage 4D-17UE Recovery Spectator Continuous Effect Keyed Required Field Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice narrows P1-004 recovery/replay determinism for spectator replay-frame timing `continuousEffects[]` payloads. It only touches recovery validation and recovery conformance tests.

The gap closed here was that keyed authoritative continuous-effect diagnostics only ran for required scalar fields when the same-key spectator field was readable. If the spectator continuous-effects list had a count mismatch, broad ordered parity was skipped; if a same-effect payload then omitted or made required scalar fields unreadable, the validator emitted generic shape diagnostics but not the keyed authoritative mismatch diagnostics that identify the authoritative same-effect field drift.

## Runtime Change

`MatchRecoveryValidator` now emits keyed authoritative mismatch diagnostics for same-effect spectator replay-frame timing `continuousEffects[]` required scalar fields even when a field is missing or unreadable.

The helper coverage includes:

- `scope`;
- `layer`;
- `duration`;
- `powerDelta`;
- `basePower`;
- `effectivePower`;
- `sequence`.

Readable value drift keeps the existing diagnostic wording. Missing/unreadable required scalar fields now also emit the same keyed authoritative mismatch wording for the matching `effectId`.

No protocol shape, frontend, official catalog, matrix JSON, command execution, randomness or solution-file changes were made.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedRequiredFieldAbsenceWithCountMismatch`.

The test mutates a spectator replay-frame timing continuous-effects payload with:

- one authoritative effect `effect-1`;
- `scope`, `duration`, `powerDelta` and `sequence` removed from the same-key spectator payload;
- `layer`, `basePower` and `effectivePower` changed to unreadable payload shapes;
- an extra continuous effect added so `continuousEffects` count mismatch keeps broad ordered parity skipped.

Expected diagnostics are:

- generic required/invalid shape diagnostics for the seven required scalar fields;
- keyed authoritative mismatch diagnostics for the same seven required scalar fields;
- continuous-effect count mismatch.

## Validation

- Focused keyed required-field absence test: `1/1`.
- Focused `ContinuousEffect` filter: `132/132`.
- Focused `MatchRecoveryTests` filter: `658/658`.
- Adjacent recovery/opening/store-smoke filter: `1239/1239`.
- Backend full: `6604/6604`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `src` and `tests`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- `dotnet format Riftbound.slnx --verify-no-changes --no-restore`: still blocked by unrelated pre-existing whitespace diagnostics outside this slice; no unrelated formatting was applied.

## Remaining Work

This slice does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness status.
