# Stage 4D-18AR Recovery Timing Trigger Queue Keyed Visible Source Membership Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectMembershipWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key visible source-object membership validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing visible source-object membership and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps `triggerId = "trigger-visible"` and `sourceVisibility = "VISIBLE"` so it remains keyed to the authoritative visible trigger.
- The same-key payload changes `sourceObjectId` to `missing-source`, an object id absent from the authoritative object registry, then adds `trigger-extra` to force count mismatch.
- Assertions require the visible source-object membership diagnostic, keyed authoritative source-object mismatch diagnostic for `trigger-visible`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed visible source membership test: `1/1`.
- Focused `TriggerQueue` filter: `371/371`.
- Focused recovery filter: `1072/1072`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1653/1653`.
- Backend full was not rerun for this first post-18AQ test-only micro-slice; latest backend full remains Stage 4D-18AQ at `7017/7017`.
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
