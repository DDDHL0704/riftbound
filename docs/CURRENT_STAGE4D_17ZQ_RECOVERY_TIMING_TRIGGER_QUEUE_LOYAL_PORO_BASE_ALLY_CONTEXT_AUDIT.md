# Stage 4D-17ZQ Recovery Timing Trigger Queue Loyal Poro Base Ally Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Loyal Poro last-breath draw triggers are queued only when the destroyed Loyal Poro was not isolated at its base position.

Runtime `ResolveLoyalPoroLastBreathDrawPlayerId` requires a destroyed `UNL-156/219` unit card to be face up, non-standby and controlled by the trigger player, then requires `HasOtherFriendlyUnitAtSamePosition(...)` before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-LOYAL_PORO_LAST_BREATH_DRAW_1` with `UNIT_DESTROYED`. State-based cleanup additionally rejects the trigger when the only other friendly base unit is also being removed in the same cleanup pass.

This slice is intentionally Loyal-Poro-specific. Other standard last-breath source requirements remain covered by the existing stack, source-card, source-controller, source-location, graveyard-membership, graveyard-player, equipment-card, unit-card, visibility-state and Unsung Hero source-power guards.

## Validator Change

`MatchRecoveryValidator` now validates the Loyal Poro base-ally reachability context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

Readable Loyal Poro trigger payloads now reject when the applicable player-zone and object-registry context shows no other friendly face-up unit in the trigger controller's base. Legacy or partial payloads that do not expose the needed base/object-tag context remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueLoyalPoroLastBreathIsolatedSourceContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueLoyalPoroLastBreathIsolatedSourceContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueLoyalPoroLastBreathIsolatedSourceContextDrift`.

Each test uses a legal Loyal Poro last-breath trigger id, keeps the source card number, controller, unit-card tag, face-up state, graveyard location, graveyard player and graveyard membership aligned with alice, and leaves alice's base without another friendly face-up unit. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject isolated Loyal Poro trigger drift without relying on trigger id, source-card, source-controller, source-location, graveyard-membership, graveyard-player, unit-card, equipment-card, face-down, standby or source-power diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new Loyal Poro isolated source context tests: `3/3`.
- Focused `TriggerQueue` filter: `359/359`.
- Focused recovery filter: `1043/1043`.
- Backend full: `6989/6989`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Loyal Poro standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
