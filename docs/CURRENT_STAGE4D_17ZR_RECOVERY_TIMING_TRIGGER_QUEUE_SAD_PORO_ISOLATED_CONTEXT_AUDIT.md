# Stage 4D-17ZR Recovery Timing Trigger Queue Sad Poro Isolated Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Sad Poro last-breath draw triggers are queued only when the destroyed Sad Poro was isolated at its base position.

Runtime `ResolveSadPoroLastBreathDrawPlayerId` requires a destroyed `SFD·036/221` or `UNL-221/219` unit card to be face up, non-standby and controlled by the trigger player, then rejects the trigger when `HasOtherFriendlyUnitAtSamePosition(...)` finds another friendly face-up, non-standby unit before `BuildLastBreathTriggerQueueItem` can write `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-SAD_PORO_LAST_BREATH_DRAW_1` with `UNIT_DESTROYED`.

This slice is intentionally Sad-Poro-specific and complements Stage 4D-17ZQ's Loyal Poro inverse guard. Other standard last-breath source requirements remain covered by the existing stack, source-card, source-controller, source-location, graveyard-membership, graveyard-player, equipment-card, unit-card, visibility-state and Unsung Hero source-power guards.

## Validator Change

`MatchRecoveryValidator` now validates the Sad Poro isolated-base reachability context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

Readable Sad Poro trigger payloads now reject when the applicable player-zone and object-registry context shows another friendly face-up unit in the trigger controller's base. Legacy or partial payloads that do not expose the needed base/object-tag context remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueSadPoroLastBreathNonIsolatedSourceContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueSadPoroLastBreathNonIsolatedSourceContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSadPoroLastBreathNonIsolatedSourceContextDrift`.

Each test uses a legal Sad Poro last-breath trigger id, keeps the source card number, controller, unit-card tag, face-up state, graveyard location, graveyard player and graveyard membership aligned with alice, and leaves another friendly face-up unit in alice's base. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject non-isolated Sad Poro trigger drift without relying on trigger id, source-card, source-controller, source-location, graveyard-membership, graveyard-player, unit-card, equipment-card, face-down, standby or source-power diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new Sad Poro non-isolated source context tests: `3/3`.
- Focused `TriggerQueue` filter: `362/362`.
- Focused recovery filter: `1046/1046`.
- Backend full: `6992/6992`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Sad Poro standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
