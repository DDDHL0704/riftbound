# Stage 4D-17ZN Recovery Timing Trigger Queue Standard Last Breath Source Visibility-State Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Most standard last-breath trigger items are queued only from visible, non-standby source units that reached graveyard.

Runtime last-breath resolvers for Scouting Warhawk, Sad Poro, Loyal Poro, Honest Broker, Mechanical Trickster, Undercover Agent, Unsung Hero, Ironclad Vanguard and Muddy Dredger require the destroyed source to be a unit card, not face down and not tagged `CardObjectTags.Standby` before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with `UNIT_DESTROYED`.

Watchful Sentinel remains intentionally excluded from this visibility-state guard because its direct resolver is constrained by `FieldRemovalResult.WasUnit`, graveyard destination and card number, while visible cleanup uses a separate wrapper.

## Validator Change

`MatchRecoveryValidator` now validates non-Watchful standard last-breath source visibility-state context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard applies to the standard last-breath family handled by `ValidateTriggerQueueStandardLastBreathSourceObjectIdContext`. For standard last-breath effects other than `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, readable source objects now reject when the applicable recovered snapshot or authoritative object registry exposes the source as face down or standby-tagged.

Legacy or partial payloads that do not expose object face-down or tag data remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceVisibilityStateContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceVisibilityStateContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceVisibilityStateContextDrift`.

Each test uses a legal Scouting Warhawk last-breath trigger id, keeps the source object card number, controller, graveyard location, graveyard player and graveyard membership aligned with alice, and exposes the source object as both face down and standby-tagged. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject source visibility-state drift without relying on trigger id, stack-context, source-card, source-controller, source-location, graveyard-membership, graveyard-player or equipment-card diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new standard last-breath source visibility-state context tests: `3/3`.
- Focused `TriggerQueue` filter: `350/350`.
- Focused recovery filter: `1034/1034`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1615/1615`.
- Backend full: `6980/6980`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for non-Watchful standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
