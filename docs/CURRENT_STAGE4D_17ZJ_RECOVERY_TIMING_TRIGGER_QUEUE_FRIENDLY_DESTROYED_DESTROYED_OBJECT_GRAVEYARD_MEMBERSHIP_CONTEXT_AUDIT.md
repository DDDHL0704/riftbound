# Stage 4D-17ZJ Recovery Timing Trigger Queue Friendly Destroyed Destroyed Object Graveyard Membership Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger items are queued only from destroyed unit removals whose destroyed object is inserted into the removal owner's graveyard.

Runtime `CoreRuleEngine.TryDestroyTarget` returns `FieldRemovalResult.WasDestroyed` only for non-banish, non-recall removals. In that branch it removes the target from field zones, appends the target object id to the removal owner's graveyard, records `DestinationZone = "GRAVEYARD"`, and then downstream friendly-destroyed trigger builders queue `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}` with `UNIT_DESTROYED`.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed destroyed-object graveyard membership context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion trigger families. It reuses the Stage 4D-17ZD destroyed-object parser and Stage 4D-17ZI destroyed-object graveyard-location context. When a parsed destroyed object has an exposed `GRAVEYARD` object-location player and that player's graveyard zone list is available, the destroyed object id must be present in that list.

Legacy or partial payloads that do not expose object-location data, location player ids or player graveyard zone lists remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardMembershipContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardMembershipContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectGraveyardMembershipContextDrift`.

Each test uses a legal Ghostly Centaur source object (`UNL-068/219`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present and unit-card tagged, exposes the destroyed object's location as `GRAVEYARD` for alice, and removes `destroyed-1` from alice's graveyard zone list. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject destroyed-object graveyard membership drift without relying on destroyed-object registry, equipment, unit-card, minion-family, controller or location-zone diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed destroyed-object graveyard-membership context tests: `3/3`.
- Focused `TriggerQueue` filter: `338/338`.
- Focused recovery filter: `1022/1022`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1603/1603`.
- Backend full: `6968/6968`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
