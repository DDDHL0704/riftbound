# Stage 4D-17ZO Recovery Timing Trigger Queue Standard Last Breath Source Unit-Card Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Non-Watchful standard last-breath trigger items are queued only from source objects that are unit cards.

Runtime last-breath resolvers for Scouting Warhawk, Sad Poro, Loyal Poro, Honest Broker, Mechanical Trickster, Undercover Agent, Unsung Hero, Ironclad Vanguard and Muddy Dredger all require `CardObjectTags.UnitCard` on the destroyed source object before `BuildLastBreathTriggerQueueItem` writes `TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}` with `UNIT_DESTROYED`.

Watchful Sentinel remains intentionally excluded from this tag guard because its direct resolver is constrained by `FieldRemovalResult.WasUnit`, graveyard destination and card number. Equipment-only Watchful drift remains covered by the existing equipment-card guard.

## Validator Change

`MatchRecoveryValidator` now validates non-Watchful standard last-breath source unit-card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

For standard last-breath effects other than `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`, readable source objects now reject when the applicable recovered snapshot or authoritative object registry exposes source tags without `CardObjectTags.UnitCard`.

Legacy or partial payloads that do not expose object tag data remain compatible.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceUnitCardContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceUnitCardContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceUnitCardContextDrift`.

Each test uses a legal Scouting Warhawk last-breath trigger id, keeps the source object card number, controller, graveyard location, graveyard player and graveyard membership aligned with alice, and exposes the source object without `CardObjectTags.UnitCard`. This proves recovered snapshot, authoritative state and spectator replay-frame validation reject source unit-card drift without relying on trigger id, stack-context, source-card, source-controller, source-location, graveyard-membership, graveyard-player, equipment-card, face-down or standby diagnostics.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new standard last-breath source unit-card context tests: `3/3`.
- Focused `TriggerQueue` filter: `353/353`.
- Focused recovery filter: `1037/1037`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1618/1618`.
- Backend full: `6983/6983`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for non-Watchful standard last-breath timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
