# Stage 4D-17ZK Recovery Timing Trigger Queue Friendly Destroyed Destroyed Object Graveyard Player Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger items are queued from destroyed unit removals whose destroyed object is moved to the graveyard owned by the same runtime player retained as trigger controller.

Runtime `CoreRuleEngine.TryDestroyTarget` returns `FieldRemovalResult.WasDestroyed` only for non-banish, non-recall removals. In that branch it removes the target from the owning field-zone player, appends the target object id to that player's graveyard, records `DestinationZone = "GRAVEYARD"`, and returns that player as the removal owner. Ghostly Centaur, Resonant Soul and Savage Jawfish trigger builders use that removal owner as `TriggerQueueItemState.ControllerId`; Viktor destroyed-non-minion uses the destroyed target's effective field controller, which in the current field-zone model is the same player whose graveyard receives the destroyed object.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed destroyed-object graveyard player context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger families. It reuses the Stage 4D-17ZD destroyed-object parser and runs after Stage 4D-17ZI destroyed-object graveyard-location validation. When a parsed destroyed object has an exposed `GRAVEYARD` object location and a non-empty object-location `PlayerId`, that player id must match the trigger controller id.

Legacy or partial payloads that do not expose object-location data, location zones or location player ids remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardPlayerContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardPlayerContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardPlayerContextDrift`.

Each test uses a legal Ghostly Centaur source object (`UNL-068/219`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present and unit-card tagged, exposes the destroyed object's location as `GRAVEYARD` for bob, and keeps `destroyed-1` in bob's graveyard zone list while the trigger controller is alice. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject destroyed-object graveyard-player drift without relying on destroyed-object registry, equipment, unit-card, minion-family, controller, location-zone or graveyard-membership diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed destroyed-object graveyard-player context tests: `3/3`.
- Focused `TriggerQueue` filter: `341/341`.
- Focused recovery filter: `1025/1025`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1606/1606`.
- Backend full: `6971/6971`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
