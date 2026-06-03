# Stage 4D-17WQ Recovery Timing Trigger Queue Loyal Poro Last-Breath Draw Context Audit

Date: 2026-06-03

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

This slice tightens recovery timing validation for Loyal Poro standard last-breath draw trigger queue entries.

Runtime basis:

- `CoreRuleEngine.BuildLastBreathTriggerQueueItem` constructs standard last-breath trigger queue ids as `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}`.
- Loyal Poro uses effect kind `LOYAL_PORO_LAST_BREATH_DRAW_1`.
- Runtime trigger queue entries carry triggered event kind `UNIT_DESTROYED`.
- Source object membership remains covered by existing trigger-queue source-object membership validation because generic parsing of `TRIGGER-{stackItemId}-{sourceObjectId}-{effectKind}` is hyphen-ambiguous.
- Recovered snapshot and spectator replay-frame readable Loyal Poro source visibility must remain `VISIBLE`.

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now includes `LOYAL_PORO_LAST_BREATH_DRAW_1` in the standard last-breath trigger queue context validator. The guard applies to:

- recovered player snapshot timing `triggerQueue[]`
- authoritative `MatchState.TriggerQueue`
- spectator replay-frame timing `triggerQueue[]`

The validator now rejects Loyal Poro retained-state drift where:

- the standard last-breath trigger id does not carry the expected Loyal Poro effect tail
- source visibility is present and not `VISIBLE`
- readable effect kind is not `LOYAL_PORO_LAST_BREATH_DRAW_1`
- readable triggered event kind is not `UNIT_DESTROYED`

## Coverage

Added `MatchRecoveryTests` coverage:

- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueLoyalPoroLastBreathDrawContextDrift`
- `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueLoyalPoroLastBreathDrawContextDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueLoyalPoroLastBreathDrawContextDrift`

## Validation

Passed:

- touched-file scoped whitespace format for `MatchRecovery.cs`
- touched-file scoped whitespace format for `MatchRecoveryTests.cs`
- focused new Loyal Poro last-breath draw context tests `3/3`
- focused `TriggerQueue` filter `125/125`
- focused recovery `810/810`
- adjacent recovery/official-opening/Postgres recovery-store filter `1390/1390`
- backend full `6755/6755`
- `git diff --check`
- anchored conflict-marker scan over `src`, `tests` and `docs`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Gates

This narrows P1-004 replay/recovery determinism and Loyal Poro trigger-queue context correctness only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
