# Stage 4D-17ZE Recovery Timing Trigger Queue Friendly-Destroyed Destroyed Object Card Context Audit

Date: 2026-06-04

Status: accepted. Project remains **NOT READY**.

## Runtime Invariant

Friendly-destroyed trigger families for Ghostly Centaur, Resonant Soul and Savage Jawfish are queued only from runtime `UNIT_DESTROYED` events, and Viktor destroyed-non-minion additionally requires a destroyed unit-card target that is not a minion-token family card.

Runtime equipment-only removals produce `EQUIPMENT_DESTROYED`, not `UNIT_DESTROYED`, and Viktor's target guard also rejects equipment-only destroyed objects. A retained friendly-destroyed trigger id therefore cannot legitimately point its destroyed-object segment at an object that is exposed as an equipment card without also being a unit card.

## Validator Change

`MatchRecoveryValidator` now validates friendly-destroyed destroyed-object card context for:

- recovered player snapshot timing `triggerQueue[]`;
- authoritative `MatchState.TriggerQueue`;
- spectator replay-frame timing `triggerQueue[]`.

The helper covers:

- `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2`;
- `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1`;
- `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1`;
- `VIKTOR_DESTROYED_NON_MINION_CREATE_MINION`.

It reuses the Stage 4D-17ZD destroyed-object parser. When object tags are available for the parsed destroyed object, the validator rejects equipment-only destroyed objects while preserving legacy/partial registries that do not expose tags and preserving the existing runtime tolerance for non-equipment objects.

## Tests

Added coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueFriendlyDestroyedDestroyedObjectCardContextDrift`;
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueFriendlyDestroyedDestroyedObjectCardContextDrift`;
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectCardContextDrift`.

Each test keeps `source-1` legal for Ghostly Centaur source context and keeps `destroyed-1` present in the object registry, then marks `destroyed-1` as equipment-only to prove recovered snapshot, authoritative state and spectator replay-frame validation reject impossible friendly-destroyed equipment destruction context.

## Validation

- Touched-file scoped whitespace format: passed.
- Focused new friendly-destroyed destroyed-object-card context tests: `3/3`.
- Focused `TriggerQueue` filter: `323/323`.
- Focused recovery filter: `1008/1008`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1588/1588`.
- Backend full: `6953/6953`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: passed.
- Matrix JSON parse: passed.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for friendly-destroyed timing trigger-queue payloads only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
