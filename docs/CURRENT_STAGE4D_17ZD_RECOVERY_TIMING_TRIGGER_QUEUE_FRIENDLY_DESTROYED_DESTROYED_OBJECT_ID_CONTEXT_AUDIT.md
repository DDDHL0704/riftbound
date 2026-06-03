# Stage 4D-17ZD Recovery Timing Trigger Queue Friendly-Destroyed Destroyed Object Id Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger families for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion are constructed by runtime as:

```text
TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}
```

with `triggeredByEventKind = UNIT_DESTROYED`. Runtime passes `destroyedObjectId` from the unit/non-minion object that was just destroyed before queueing these triggers, and source enumeration excludes that same object.

Stage 4D-17ZC already anchored readable `sourceObjectId` against the trigger id and required a non-empty, distinct destroyed-object segment. This slice adds the next retained-payload invariant: once that destroyed-object segment can be parsed, it must refer to an object known to the applicable recovered snapshot or authoritative object registry.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed destroyed-object id registry membership for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The helper covers:

- `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`;
- `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`;
- `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`;
- `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`.

The parser intentionally uses the readable payload `sourceObjectId` as the marker before the destroyed-object segment because stack and source ids can contain hyphens. Hidden, missing or malformed source ids remain handled by existing source/redaction validators, and malformed destroyed-object segments remain handled by the 17ZC source-object-id context guard.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedDestroyedObjectIdContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedDestroyedObjectIdContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectIdContextDrift`.

Each test keeps `source-1` legal for the Ghostly Centaur source context while forging the trigger id to carry missing `destroyed-404`, proving recovered snapshot, authoritative state and spectator replay-frame validation reject destroyed-object registry drift without relying on broader source-object validity.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed destroyed-object-id context tests: `3/3`.
- Focused `TriggerQueue` filter: `320/320`.
- Focused recovery filter: `1005/1005`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1585/1585`.
- Backend full: `6950/6950`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
