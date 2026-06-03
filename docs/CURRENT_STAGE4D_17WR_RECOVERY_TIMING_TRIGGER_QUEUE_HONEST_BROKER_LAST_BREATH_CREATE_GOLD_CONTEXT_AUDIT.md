# Stage 4D-17WR Recovery Timing Trigger Queue Honest Broker Last-Breath Create-Gold Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightens recovery timing validation for Honest Broker standard last-breath create-gold trigger queue entries.

Runtime basis:

- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs standard last-breath trigger queue ids as `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}`.
- Honest Broker uses effect kind `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`.
- Runtime trigger queue entries carry triggered event kind `UNIT_DESTROYED`.
- Source object membership remains covered by existing trigger-queue source-object membership validation because generic parsing of `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}` is hyphen-ambiguous.
- Recovered snapshot and spectator replay-frame readable Honest Broker source visibility must remain `VISIBLE`.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now includes `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` in the standard last-breath trigger queue context validator. The guard applies to:

- recovered player snapshot timing `triggerQueue[]`
- authoritative `MatchState.TriggerQueue`
- spectator replay-frame timing `triggerQueue[]`

The validator now rejects Honest Broker retained-state drift where:

- the standard last-breath trigger id does not carry the expected Honest Broker effect tail
- source visibility is present and not `VISIBLE`
- readable effect kind is not `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`
- readable triggered event kind is not `UNIT_DESTROYED`

## Coverage

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueHonestBrokerLastBreathCreateGoldContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueHonestBrokerLastBreathCreateGoldContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueHonestBrokerLastBreathCreateGoldContextDrift`

## Validation

Passed:

- touched-file scoped whitespace format for `MatchRecovery.cs`
- touched-file scoped whitespace format for `MatchRecoveryTests.cs`
- focused new Honest Broker last-breath create-gold context tests `3/3`
- focused `TriggerQueue` filter `128/128`
- focused recovery `813/813`
- adjacent recovery/official-opening/Postgres recovery-store filter `1393/1393`
- backend full `6758/6758`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Gates

This narrows P1-004 replay/recovery determinism and Honest Broker trigger-queue context correctness only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
