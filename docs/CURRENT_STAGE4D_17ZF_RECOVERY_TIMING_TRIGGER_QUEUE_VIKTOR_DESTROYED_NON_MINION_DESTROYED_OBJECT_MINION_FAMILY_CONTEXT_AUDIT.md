# Stage 4D-17ZF Recovery Timing Trigger Queue Viktor Destroyed Non-Minion Destroyed Object Minion-Family Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Viktor destroyed-non-minion trigger items are queued only when `CoreRuleEngine.IsViktorDestroyedNonMinionTriggerTarget` accepts the destroyed target. That requires a real destroyed unit-card target and rejects objects tagged `CardObjectTags.MinionTokenFamily`.

Runtime then writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{destroyedObjectId}-VIKTOR_DESTROYED_NON_MINION_CREATE_MINION` into the timing trigger queue. A retained recovery or spectator replay trigger id therefore cannot legitimately point its destroyed-object segment at a minion-token-family object.

## Validator Change

`MatchRecoveryValidator` now validates Viktor destroyed-object minion-family context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies only to `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`. It reuses the Stage 4D-17ZD destroyed-object parser and runs after the Stage 4D-17ZE equipment-only destroyed-object card check. When object tags are available for the parsed destroyed object, the validator rejects `CardObjectTags.MinionTokenFamily` and preserves legacy/partial registries that do not expose tags.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectMinionFamilyContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueViktorDestroyedNonMinionDestroyedObjectMinionFamilyContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectMinionFamilyContextDrift`.

Each test keeps the Viktor source object legal (`ARC-006/006`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present in the applicable object registry, then marks the destroyed object as `UnitCard` plus `MinionTokenFamily` to prove recovered snapshot, authoritative state and spectator replay-frame validation reject impossible Viktor non-minion target context.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new Viktor destroyed-object minion-family context tests: `3/3`.
- Focused `TriggerQueue` filter: `326/326`.
- Focused recovery filter: `1011/1011`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1591/1591`.
- Backend full: `6956/6956`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Viktor destroyed-non-minion timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
