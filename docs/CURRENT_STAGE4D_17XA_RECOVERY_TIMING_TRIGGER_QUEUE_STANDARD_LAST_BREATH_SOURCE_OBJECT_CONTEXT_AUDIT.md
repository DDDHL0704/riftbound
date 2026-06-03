# Stage 4D-17XA Recovery Timing Trigger Queue Standard Last-Breath Source-Object Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted

Project status: **NOT READY**

## Scope

This slice covers recovery validation for standard last-breath trigger queue source-object context.

Runtime `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs standard last-breath trigger ids as `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}`, with the last-breath effect kind as the suffix and `UNIT_DESTROYED` as the triggered event kind.

The recovery validator already recognized the standard last-breath family for source visibility, effect kind and triggered event kind. This slice adds a source-object consistency guard: when `sourceObjectId` is readable and not `HIDDEN`, it must appear immediately before the expected effect-kind suffix in the trigger id.

## Runtime Parity

`src/Riftbound.Engine/MatchRecovery.cs` now validates standard last-breath source-object context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative state `TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The validator rejects source-object id drift for the standard last-breath effects covered by the current recovery trigger-queue context family:

- Watchful Sentinel last-breath draw
- Unsung Hero last-breath powerful draw
- Scouting Warhawk last-breath call-rune
- Sad Poro last-breath draw
- Loyal Poro last-breath draw
- Honest Broker last-breath create-gold
- Mechanical Trickster last-breath create-minions
- Undercover Agent last-breath
- Ironclad Vanguard last-breath create-robots
- Muddy Dredger last-breath create-warhawk

The guard does not parse arbitrary stack/source id segments. It uses the exact runtime suffix shape `-{sourceObjectId}-{expectedEffectKind}` so source ids containing hyphens remain safe. It also intentionally excludes friendly-destroyed and Viktor destroyed-non-minion trigger ids because those runtime ids include an additional destroyed-object id segment before the effect kind.

Existing trigger queue source-object membership validation continues to cover whether the readable source object exists in the recovered snapshot, authoritative state or spectator authoritative registry.

## Tests

New `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueStandardLastBreathSourceObjectContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueStandardLastBreathSourceObjectContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceObjectContextDrift`

Validation passed:

- focused new standard last-breath source-object context tests: `3/3`
- focused `TriggerQueue` filter: `155/155`
- focused recovery filter: `840/840`
- adjacent recovery/official-opening/Postgres recovery-store filter: `1420/1420`
- backend full conformance: `6785/6785`
- touched-file scoped whitespace format
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- matrix JSON parse

## Remaining Open

This slice narrows P1-004 replay/recovery determinism and standard last-breath trigger-queue context validation only.

Still open: broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status.
