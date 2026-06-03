# Stage 4D-17ZP Recovery Timing Trigger Queue Standard Last Breath Source Power Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Unsung Hero powerful-draw last-breath trigger items are queued only when the destroyed source unit is powerful enough.

Runtime `ResolveUnsungHeroLastBreathDrawPlayerId` requires the destroyed source object's power to be at least `5` before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2` with `UNIT_DESTROYED`.

This slice is intentionally Unsung-Hero-specific. Other standard last-breath source requirements remain covered by the existing stack, source-card, source-controller, graveyard location, graveyard membership, graveyard-player, equipment-card, unit-card and visibility-state guards.

## Validator Change

`MatchRecoveryValidator` now validates Unsung Hero standard last-breath source power context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

Readable Unsung Hero source objects now reject when the applicable recovered snapshot object payload or authoritative object registry exposes source power below `5`.

Legacy or partial payloads that do not expose object power data remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourcePowerContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourcePowerContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourcePowerContextDrift`.

Each test uses a legal Unsung Hero last-breath trigger id, keeps the source card number, controller, unit-card tag, face-up state, graveyard location, graveyard player and graveyard membership aligned with alice, and exposes source power `4`. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject source power drift without relying on trigger id, source-card, source-controller, source-location, graveyard-membership, graveyard-player, unit-card, equipment-card, face-down or standby diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new standard last-breath source power context tests: `3/3`.
- Focused `TriggerQueue` filter: `356/356`.
- Focused recovery filter: `1040/1040`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1621/1621`.
- Backend full: `6986/6986`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Unsung Hero standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
