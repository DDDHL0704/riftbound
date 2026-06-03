# Stage 4D-18AM Recovery Timing Trigger Queue Keyed Required Field Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedRequiredFieldShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key required-field shape validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar shape and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps a readable `triggerId = "trigger-visible"` so it remains keyed to the authoritative trigger.
- The same-key payload changes `controllerId`, `sourceObjectId`, `sourceVisibility`, `effectKind` and `triggeredByEventKind` to unreadable array shapes, then adds `trigger-extra` to force count mismatch.
- Assertions require required-field shape diagnostics, keyed authoritative mismatch diagnostics for `trigger-visible`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed required-field shape test: `1/1`.
- Focused `TriggerQueue` filter: `366/366`.
- Focused recovery filter: `1067/1067`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1648/1648`.
- Backend full was not rerun for this second post-18AK test-only micro-slice; latest backend full remains Stage 4D-18AK at `7011/7011`.
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
