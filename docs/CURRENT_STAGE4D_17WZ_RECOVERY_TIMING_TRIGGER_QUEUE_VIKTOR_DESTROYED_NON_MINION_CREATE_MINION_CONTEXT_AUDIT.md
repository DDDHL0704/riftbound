# Stage 4D-17WZ Recovery Timing Trigger Queue Viktor Destroyed Non-Minion Create-Minion Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Viktor destroyed non-minion create-minion trigger queue context.

Runtime `CoreRuleEngine.BuildViktorDestroyedNonMinionTriggerQueueItems` constructs this trigger shape as `TRIGGER-{stackItemId}-{sourceObjectId}-{destroyedObjectId}-VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`, with controller set to the destroyed unit controller, effect kind `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION` and triggered event kind `UNIT_DESTROYED`.

The construction is used when a non-minion unit is destroyed while a visible Viktor destroyed-non-minion source on field and controlled by the destroyed unit controller creates a minion-token trigger before the trigger is resolved through the trigger queue / stack flow.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now includes `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION` in the trigger queue context validator. The guard applies to:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects:

- malformed Viktor trigger ids when the effect kind identifies the trigger;
- non-visible snapshot/spectator source visibility;
- readable effect kind drift away from `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`;
- triggered event kind drift away from `UNIT_DESTROYED`.

Source object membership remains covered by the existing trigger queue source-object membership validation. Direct stack/source/destroyed id parsing is intentionally not added here because the id segments can contain hyphens and the current recovery validator's standard suffix check avoids ambiguous parsing.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueViktorDestroyedNonMinionCreateMinionContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueViktorDestroyedNonMinionCreateMinionContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionCreateMinionContextDrift`

Validation passed:

- focused new Viktor destroyed non-minion create-minion context tests: `3/3`
- focused `TriggerQueue` filter: `152/152`
- focused recovery filter: `837/837`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1417/1417`
- backend full conformance: `6782/6782`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Viktor trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
