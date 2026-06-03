# Stage 4D-17ZI Recovery Timing Trigger Queue Friendly Destroyed Destroyed Object Graveyard Location Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger items are queued only from destroyed unit removals whose destroyed object moved to graveyard.

Runtime `CoreRuleEngine.TryDestroyTarget` returns `FieldRemovalResult.WasDestroyed` only for non-banish, non-recall removals. In that branch it removes the target from field zones, appends the target object id to the removal owner's graveyard, and returns `DestinationZone = "GRAVEYARD"`. Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger builders all consume this destroyed-object id only after `WasDestroyed` and `WasUnit` hold, then queue `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}` with `UNIT_DESTROYED`.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed destroyed-object graveyard-location context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger families. It reuses the Stage 4D-17ZD destroyed-object parser and remains compatible with legacy/partial payloads that do not expose object-location data for the parsed destroyed object. When object-location data is available, any zone other than `GRAVEYARD` is rejected.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardLocationContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardLocationContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardLocationContextDrift`.

Each test uses a legal Ghostly Centaur source object (`UNL-068/219`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present and unit-card tagged, then exposes the destroyed object's location as `BASE` while the trigger still claims a friendly-destroyed `UNIT_DESTROYED` trigger. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject destroyed-object location drift without relying on destroyed-object registry, equipment, unit-card, minion-family or controller diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed destroyed-object graveyard-location context tests: `3/3`.
- Focused `TriggerQueue` filter: `335/335`.
- Focused recovery filter: `1019/1019`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1600/1600`.
- Backend full: `6965/6965`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
