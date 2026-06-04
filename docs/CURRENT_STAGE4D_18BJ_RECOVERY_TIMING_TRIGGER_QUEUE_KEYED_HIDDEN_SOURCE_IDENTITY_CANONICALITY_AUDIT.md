# Stage 4D-18BJ Recovery Timing Trigger Queue Keyed Hidden Source Identity Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceIdentityCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden-source public identity canonicality drift when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality validation and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with `sourceObjectId`, `sourceVisibility` and `effectKind` redacted to `HIDDEN`, preserving the hidden-source redaction contract.
- The same-key payload keeps the authoritative `triggerId`, changes `controllerId` to `" bob "` and `triggeredByEventKind` to `" OBJECT_DESTROYED "`, and adds a second payload with `triggerId = "trigger-extra"` to keep trigger-count mismatch active.
- Assertions require surrounding-whitespace diagnostics, same-key controller and triggered-event-kind authoritative mismatches, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source identity canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `385/385`.
- Focused recovery filter: `1090/1090`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1671/1671`.
- Backend full: `7036/7036`.
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
