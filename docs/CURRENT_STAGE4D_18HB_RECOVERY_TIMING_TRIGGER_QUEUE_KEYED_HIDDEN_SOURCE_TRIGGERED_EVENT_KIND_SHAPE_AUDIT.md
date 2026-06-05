# Stage 4D-18HB Recovery Timing Trigger Queue Keyed Hidden Source Triggered Event Kind Shape Audit

Date: 2026-06-05 16:44 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, preserves `triggeredByEventKind = "BATTLEFIELD_HELD"`, keeps the payload keyed to authoritative `trigger-hidden`, changes only `triggeredByEventKind` to an unreadable array payload, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- triggered-event-kind required diagnostic for an unreadable array payload
- keyed authoritative triggered-event-kind mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

The test intentionally does not assert a triggered-event-kind invalid diagnostic; the existing required-scalar validation is the stable diagnostic for this shape path.

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source triggered-event-kind shape test: `1/1`
- Focused `TriggerQueue` filter: `412/412`
- Focused `MatchRecoveryTests` filter: `1239/1239`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1820/1820`
- Backend full via tracked `Riftbound.slnx`: `7185/7185`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18HB stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface. The current runner reported filtered/full totals two higher than the prior 18HA coordination text while this batch added exactly one `[Fact]`; A_MAIN recorded the actual runner counts above and kept the code scope to the single 18HB regression.

## Subagent Review

- Read-only gap review confirmed shape is the missing standalone hidden-source triggered-event-kind slice after canonicality, empty-value and null-value coverage.
- Read-only diagnostics review confirmed the required diagnostic, keyed authoritative mismatch wording, extra-trigger diagnostic and count mismatch diagnostic; it also confirmed no invalid diagnostic should be asserted.
- Parallel worker branches were opened for later hidden-source redaction empty-value candidates; those commits are not part of this 18HB main checkpoint.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source triggered-event-kind shape parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
