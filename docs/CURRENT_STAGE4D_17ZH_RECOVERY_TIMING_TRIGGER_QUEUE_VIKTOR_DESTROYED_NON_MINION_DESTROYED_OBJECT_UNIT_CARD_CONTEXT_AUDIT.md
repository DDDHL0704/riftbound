# Stage 4D-17ZH Recovery Timing Trigger Queue Viktor Destroyed Non-Minion Destroyed Object Unit Card Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Viktor destroyed-non-minion trigger items are queued only when the destroyed target is a unit-card object and is not a minion-family object.

Runtime enforces this through `CoreRuleEngine.IsViktorDestroyedNonMinionTriggerTarget`: the removal result must be destroyed, must have been a unit, the destroyed object must carry `CardObjectTags.UnitCard`, and the destroyed object must not carry `CardObjectTags.MinionTokenFamily`. A retained recovery or spectator replay trigger id therefore cannot legitimately point its destroyed-object segment at a readable non-unit object such as a spell-card object.

## Validator Change

`MatchRecoveryValidator` now validates Viktor destroyed-object unit-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies only to `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`. It reuses the Stage 4D-17ZD destroyed-object parser and runs after the shared destroyed-object equipment-card check, preserving the earlier equipment-only diagnostic for equipment-card non-units. When object tags are available for the parsed destroyed object, the validator rejects non-equipment objects that are not tagged `CardObjectTags.UnitCard`.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectUnitCardContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueViktorDestroyedNonMinionDestroyedObjectUnitCardContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectUnitCardContextDrift`.

Each test keeps the Viktor source object legal (`ARC-006/006`, unit-card tagged, controlled by alice and present in base), keeps the destroyed object present, non-equipment, non-minion and controlled by alice, then tags the destroyed object as `CardObjectTags.SpellCard` to prove recovered snapshot, authoritative state and spectator replay-frame validation reject impossible Viktor destroyed-object unit-card context without relying on the existing equipment-only, minion-family or controller diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new Viktor destroyed-object unit-card context tests: `3/3`.
- Focused `TriggerQueue` filter: `332/332`.
- Focused recovery filter: `1016/1016`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1597/1597`.
- Backend full: `6962/6962`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for Viktor destroyed-non-minion timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
