# Stage 4D-18AP Recovery Timing Trigger Queue Keyed Identity Redaction Sentinel Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedIdentityRedactionSentinelWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key identity redaction sentinel validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing identity redaction sentinel and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps `triggerId = "trigger-visible"` so it remains keyed to the authoritative visible trigger.
- The same-key payload changes `controllerId` and `triggeredByEventKind` to `HIDDEN`, then adds `trigger-extra` to force count mismatch.
- Assertions require identity redaction sentinel diagnostics, keyed authoritative controller/event-kind mismatch diagnostics for `trigger-visible`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed identity redaction-sentinel test: `1/1`.
- Focused `TriggerQueue` filter: `369/369`.
- Focused recovery filter: `1070/1070`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1651/1651`.
- Backend full was not rerun for this second post-18AN test-only micro-slice; latest backend full remains Stage 4D-18AN at `7014/7014`.
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
