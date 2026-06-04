# Stage 4D-18BE Recovery Timing Trigger Queue Keyed Hidden Source Full Reveal Source Membership Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceFullRevealSourceMembershipWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden-source full-reveal source-object membership when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing visible source-object membership checks and hidden-source keyed authoritative redaction validation.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with source/effect redacted to `HIDDEN`, keeps the authoritative `triggerId`, then forges the hidden trigger as `sourceVisibility = "VISIBLE"` with `sourceObjectId = "missing-hidden-source"` and `effectKind = "AMBUSH_REVEALED"`.
- A second trigger with `triggerId = "trigger-extra"` keeps trigger-count mismatch active.
- Assertions require visible source-object membership diagnostics, same-key source-object/source-visibility/effect-kind authoritative redaction mismatches, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source full-reveal source membership test: `1/1`.
- Focused `TriggerQueue` filter: `380/380`.
- Focused recovery filter: `1085/1085`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1666/1666`.
- Backend full was not rerun for this second post-18BC test-only micro-slice; latest backend full remains Stage 4D-18BC at `7029/7029`.
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
