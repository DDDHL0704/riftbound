# Stage 4D-17ZL Recovery Timing Trigger Queue Standard Last Breath Source Graveyard Player Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Standard last-breath trigger items are queued from destroyed source units that reached graveyard, and the queued trigger controller is the player resolved from that destroyed source.

Runtime `Resolve*LastBreath*PlayerId` helpers require `FieldRemovalResult.WasDestroyed`, `FieldRemovalResult.WasUnit` and `DestinationZone = "GRAVEYARD"`. They return the destroyed source controller, owner or removal owner as the trigger controller before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with `UNIT_DESTROYED`. A retained source object that is exposed in a graveyard location for a different player is therefore drift.

## Validator Change

`MatchRecoveryValidator` now validates standard last-breath source graveyard player context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to the standard last-breath family handled by `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext`. When a retained source object has an exposed `GRAVEYARD` object location and a non-empty object-location `PlayerId`, that player id must match the trigger controller id.

Legacy or partial payloads that do not expose object-location data, location zones, location player ids or trigger controllers remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceGraveyardPlayerContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceGraveyardPlayerContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardPlayerContextDrift`.

Each test uses a legal Watchful Sentinel last-breath trigger id, keeps the source object card number and controller aligned with alice, exposes the source object location as `GRAVEYARD` for bob, and keeps `source-1` present in bob's graveyard zone list. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject source graveyard-player drift without relying on trigger id, stack-context, source-card, source-controller, location-zone or graveyard-membership diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new standard last-breath source graveyard-player context tests: `3/3`.
- Focused `TriggerQueue` filter: `344/344`.
- Focused recovery filter: `1028/1028`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1609/1609`.
- Backend full: `6974/6974`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
