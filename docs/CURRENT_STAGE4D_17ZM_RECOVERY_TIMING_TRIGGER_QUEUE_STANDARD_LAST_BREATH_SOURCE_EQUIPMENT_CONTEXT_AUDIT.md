# Stage 4D-17ZM Recovery Timing Trigger Queue Standard Last Breath Source Equipment Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Standard last-breath trigger items are queued from destroyed source units that reached graveyard.

Runtime `Resolve*LastBreath*PlayerId` helpers require `FieldRemovalResult.WasDestroyed`, `FieldRemovalResult.WasUnit` and `DestinationZone = "GRAVEYARD"` before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with `UNIT_DESTROYED`. Equipment-only removals are not `WasUnit` for this trigger family, so a retained source object exposed as `CardObjectTags.EquipmentCard` without `CardObjectTags.UnitCard` is unreachable standard last-breath trigger context.

## Validator Change

`MatchRecoveryValidator` now validates standard last-breath source equipment-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to the standard last-breath family handled by `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext`. When the applicable recovered snapshot or authoritative object-tag registry exposes a source object as `CardObjectTags.EquipmentCard` and does not expose `CardObjectTags.UnitCard`, validation rejects the trigger item.

Legacy or partial payloads that do not expose object-tag data remain compatible. This slice intentionally avoids a generic source UnitCard or face-down/standby requirement because Watchful Sentinel's direct last-breath resolver is constrained by `WasUnit` and card number, while other standard last-breath resolvers carry additional visibility checks.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceEquipmentCardContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceEquipmentCardContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceEquipmentCardContextDrift`.

Each test uses a legal Watchful Sentinel last-breath trigger id, keeps the source object card number, controller, graveyard location, graveyard player and graveyard membership aligned with alice, and exposes the source object as equipment-only. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject source equipment-card drift without relying on trigger id, stack-context, source-card, source-controller, source-location, graveyard-membership or graveyard-player diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new standard last-breath source equipment-card context tests: `3/3`.
- Focused `TriggerQueue` filter: `347/347`.
- Focused recovery filter: `1031/1031`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1612/1612`.
- Backend full: `6977/6977`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
