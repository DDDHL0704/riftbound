# Stage 4D-18AY Recovery Timing Trigger Queue Keyed Hidden Source Redaction Required Field Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceRedactionRequiredFieldShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden source redaction required-field shape drift when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing required-field shape and keyed authoritative validation.

## Evidence

- The test builds an authoritative hidden-source trigger queue item from real `MatchState` object state.
- The hidden source object is face-down, unit tagged and standby tagged in a battlefield location, so the spectator replay frame must redact `sourceObjectId`, `sourceVisibility` and `effectKind` to `HIDDEN`.
- The same-key spectator payload keeps a readable `triggerId = "trigger-hidden"` but mutates `sourceObjectId`, `sourceVisibility` and `effectKind` to unreadable array shapes, then adds `trigger-extra` to force count mismatch.
- Assertions require required-field diagnostics, keyed authoritative source-object/source-visibility/effect-kind mismatch diagnostics for `trigger-hidden`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source redaction required-field shape test: `1/1`.
- Focused `TriggerQueue` filter: `378/378`.
- Focused recovery filter: `1079/1079`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1660/1660`.
- Backend full was not rerun for this second post-18AW test-only micro-slice; latest backend full remains Stage 4D-18AW at `7023/7023`.
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Path typo scan for `tests\.Riftbound` over `docs`, `tests` and `src` passed.

## Open

- Broader command/recovery/random determinism remains open.
- Remaining recovered/spectator/authoritative nested payload shape/value breadth remains open.
- Full LayerEngine breadth remains open.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
