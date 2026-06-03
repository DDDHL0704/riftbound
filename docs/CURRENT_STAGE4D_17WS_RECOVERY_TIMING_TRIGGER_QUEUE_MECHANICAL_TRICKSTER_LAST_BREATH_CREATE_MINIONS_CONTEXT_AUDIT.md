# Stage 4D-17WS Recovery Timing Trigger Queue Mechanical Trickster Last-Breath Create-Minions Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightens recovery timing validation for Mechanical Trickster standard last-breath create-minions trigger queue entries.

Runtime basis:

- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs standard last-breath trigger queue ids as `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}`.
- Mechanical Trickster uses effect kind `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`.
- Runtime trigger queue entries carry triggered event kind `UNIT_DESTROYED`.
- Source object membership remains covered by existing trigger-queue source-object membership validation because generic parsing of `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}` is hyphen-ambiguous.
- Recovered snapshot and spectator replay-frame readable Mechanical Trickster source visibility must remain `VISIBLE`.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now includes `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` in the standard last-breath trigger queue context validator. The guard applies to:

- recovered player snapshot timing `triggerQueue[]`
- authoritative `MatchState.TriggerQueue`
- spectator replay-frame timing `triggerQueue[]`

The validator now rejects Mechanical Trickster retained-state drift where:

- the standard last-breath trigger id does not carry the expected Mechanical Trickster effect tail
- source visibility is present and not `VISIBLE`
- readable effect kind is not `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`
- readable triggered event kind is not `UNIT_DESTROYED`

## Coverage

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueMechanicalTricksterLastBreathCreateMinionsContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueMechanicalTricksterLastBreathCreateMinionsContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMechanicalTricksterLastBreathCreateMinionsContextDrift`

## Validation

Passed:

- touched-file scoped whitespace format for `MatchRecovery.cs`
- touched-file scoped whitespace format for `MatchRecoveryTests.cs`
- focused new Mechanical Trickster last-breath create-minions context tests `3/3`
- focused `TriggerQueue` filter `131/131`
- focused recovery `816/816`
- adjacent recovery/official-opening/Postgres recovery-store filter `1396/1396`
- backend full `6761/6761`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Gates

This narrows P1-004 replay/recovery determinism and Mechanical Trickster trigger-queue context correctness only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
