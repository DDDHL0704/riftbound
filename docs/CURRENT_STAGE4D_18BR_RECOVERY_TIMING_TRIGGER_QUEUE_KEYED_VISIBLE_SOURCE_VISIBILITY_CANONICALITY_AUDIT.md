# Stage 4D-18BR Recovery Timing Trigger Queue Keyed Visible Source Visibility Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceVisibilityCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key visible-source source-visibility canonicality when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload starts with `sourceVisibility = "VISIBLE"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates that `sourceVisibility` to `" VISIBLE "` while keeping the authoritative `triggerId` readable for keyed lookup.
- The test appends `trigger-extra` to force trigger-count mismatch.
- Assertions require the source-visibility surrounding-whitespace diagnostic, keyed authoritative source-visibility mismatch diagnostic, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed visible-source source-visibility canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `389/389`.
- Focused recovery filter: `1098/1098`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1679/1679`.
- Backend full was not rerun for this second post-18BP test-only micro-slice; latest backend full remains Stage 4D-18BP at `7042/7042`.
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
