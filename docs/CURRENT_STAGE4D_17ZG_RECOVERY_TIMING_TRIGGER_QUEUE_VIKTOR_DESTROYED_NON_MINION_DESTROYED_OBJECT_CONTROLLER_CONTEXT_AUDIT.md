# Stage 4D-17ZG Recovery Timing Trigger Queue Viktor Destroyed Non-Minion Destroyed Object Controller Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Viktor destroyed-non-minion trigger items are queued with the destroyed target's pre-removal effective controller as `TriggerQueueItemState.ControllerId`.

Runtime computes that value before removal through `EffectiveFieldControllerId(playerZones, destroyedObjectId, destroyedState)`, then passes it into `BuildViktorDestroyedNonMinionTriggerQueueItems`. A retained recovery or spectator replay trigger id therefore cannot legitimately point its destroyed-object segment at a readable object whose controller id differs from the trigger controller.

## Validator Change

`MatchRecoveryValidator` now validates Viktor destroyed-object controller context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies only to `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`. It reuses the Stage 4D-17ZD destroyed-object parser and runs after the Stage 4D-17ZF minion-family destroyed-object check. When object controllers are available for the parsed destroyed object and the trigger controller is readable, the validator rejects mismatches while preserving legacy/partial registries that do not expose the destroyed object's controller.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectControllerContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueViktorDestroyedNonMinionDestroyedObjectControllerContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectControllerContextDrift`.

Each test keeps the Viktor source object legal (`ARC-006/006`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present and non-minion, then marks the destroyed object as controlled by bob while the trigger controller remains alice to prove recovered snapshot, authoritative state and spectator replay-frame validation reject impossible Viktor destroyed-object controller context.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new Viktor destroyed-object controller context tests: `3/3`.
- Focused `TriggerQueue` filter: `329/329`.
- Focused recovery filter: `1014/1014`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1594/1594`.
- Backend full: `6959/6959`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Viktor destroyed-non-minion timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
