# Stage 4D-213S Recovery Spectator Trigger Queue OGS Lux High Cost Spell Context Drift Without Count Audit

Timestamp: 2026-06-13 00:14 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` OGS Lux high-cost-spell effect/event context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative OGS Lux high-cost-spell trigger queue state.
- The test mutates the spectator trigger `effectKind` to `WRONG_EFFECT` and `triggeredByEventKind` to `UNIT_DESTROYED` while preserving the authoritative trigger queue count.
- Recovery validation emits the OGS Lux high-cost-spell effect-kind diagnostic requiring `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`, the triggered-event-kind diagnostic requiring `CARD_PLAYED`, and no trigger queue count mismatch.
- This complements the existing OGS Lux high-cost-spell context drift with count mismatch that also proves the unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1829/1829`.
- Adjacent recovery filter `MatchRecovery`: `1834/1834`.
- Backend full: `8117/8117`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `1c0b9b5f` (`test: cover spectator ogs lux context drift without count`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue OGS Lux high-cost-spell effect/event context drift validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
