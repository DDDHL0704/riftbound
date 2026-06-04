# Stage 4D-18BK Recovery Timing Trigger Queue Keyed Hidden Source Trigger Id Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden-source trigger-id canonicality when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing trigger-id scalar canonicality validation and trimmed-key authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with `sourceObjectId`, `sourceVisibility` and `effectKind` redacted to `HIDDEN`, preserving the hidden-source redaction contract.
- The same-key payload changes `triggerId` to `" trigger-hidden "` while keeping source/effect redaction intact, then drifts `controllerId` to `bob` and `triggeredByEventKind` to `OBJECT_DESTROYED`.
- A second payload with `triggerId = "trigger-extra"` keeps trigger-count mismatch active.
- Assertions require the trigger-id surrounding-whitespace diagnostic, trimmed-key controller and triggered-event-kind authoritative mismatches, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source trigger-id canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `386/386`.
- Focused recovery filter: `1091/1091`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1672/1672`.
- Backend full was not rerun for this first post-18BJ test-only micro-slice; latest full remains Stage 4D-18BJ at `7036/7036`.
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
