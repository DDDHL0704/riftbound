# Stage 4D-17XC Recovery Timing Trigger Queue Kogmaw Last-Breath Source-Object Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for Kogmaw last-breath trigger queue source-object context.

Runtime `CoreRuleEngine.BuildKogmawLastBreathTriggerQueueItem` constructs the trigger id as `TRIGGER-{stackItemId}-{sourceObjectId}-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::{battlefieldObjectId}`, with effect kind `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT` and triggered event kind `UNIT_DESTROYED`.

The recovery validator already recognized this trigger family for battlefield marker, effect kind and triggered event kind. This slice adds a source-object consistency guard: when `sourceObjectId` is readable and not `HIDDEN`, it must appear immediately before the Kogmaw effect-kind and battlefield marker in the trigger id.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates Kogmaw last-breath source-object context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The guard does not parse arbitrary stack/source id segments. It anchors on the stable runtime marker `OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::` and requires the trigger-id prefix before that marker to end with `-{sourceObjectId}-`, so ids containing hyphens remain safe.

Existing trigger queue source-object membership validation continues to cover whether the readable source object exists in the recovered snapshot, authoritative state or spectator authoritative registry. Existing nested Kogmaw stack-prefix acceptance remains covered.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueKogmawLastBreathSourceObjectContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueKogmawLastBreathSourceObjectContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceObjectContextDrift`

Validation passed:

- focused new Kogmaw last-breath source-object context tests: `3/3`
- focused `TriggerQueue` filter: `161/161`
- focused recovery filter: `846/846`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1426/1426`
- backend full conformance: `6791/6791`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and Kogmaw trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
