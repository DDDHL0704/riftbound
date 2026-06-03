# Stage 4D-17ZC Recovery Timing Trigger Queue Friendly-Destroyed Source Object Id Context Audit

Date: 2026-06-03

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger families for Ghostly Centaur, Resonant Soul, Savage Jawfish and Viktor destroyed-non-minion are constructed by runtime as:

```text
TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-{effectKind}
```

with `triggeredByEventKind = UNIT_DESTROYED`. Runtime also excludes the destroyed object from the source-object enumeration before queueing these triggers.

The recovered readable timing payload already carries `sourceObjectId`, so retained recovery and replay payloads must not allow that scalar to drift from the source segment embedded in the trigger id. Once a friendly-destroyed trigger family is identified and the source object is readable, the trigger id must contain the payload source object id before the destroyed object id segment, and the destroyed object id segment must be non-empty and distinct from the source object id.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed source-object id trigger-id context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The new helper covers:

- `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`;
- `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`;
- `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`;
- `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`.

It deliberately uses the readable payload `sourceObjectId` as the anchor because source and stack ids can contain hyphens. Hidden or missing source ids remain handled by the existing redaction/source-membership validators.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedSourceObjectIdContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedSourceObjectIdContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceObjectIdContextDrift`.

Each test keeps the object registry, object location and player-zone field membership legal for `source-1`, then forges the trigger id to encode `source-2`, proving the validator rejects source-id drift instead of relying on broader source object validity.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed source-object-id context tests: `3/3`.
- Focused `TriggerQueue` filter: `317/317`.
- Focused recovery filter: `1002/1002`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1582/1582`.
- Backend full: `6947/6947`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
