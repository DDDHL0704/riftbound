# Plan B On-Play Source-Power Trigger Spec Audit

Date: 2026-06-29

Project status: **NOT READY**.

## Scope

This slice removes the remaining Teemo effect-name dependency from the runtime path that queues an on-play source-unit power trigger. The affected method is `CoreRuleEngine.IsQueuedOnPlaySourcePowerTrigger`.

The existing Teemo representatives remain the same official cards and effect ids:

- `OGN·197/298` -> `TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`
- `OGN·197a/298` -> `TEEMO_ALT_A_PLAY_UNIT_SELF_POWER_PLUS_3`
- `OGN·197b/298` -> `TEEMO_ALT_B_PLAY_UNIT_SELF_POWER_PLUS_3`
- `FND-196/298` -> `FND_TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3`

## Authority

- `data/official/card-catalog.zh-CN.json` contains the four Teemo rows with the official on-play text that gives the source unit `{{S}}+3` until end of turn.
- `docs/rules-authority-and-audit.md` keeps official data / official rule sources as the only gameplay authorities.
- `docs/rules-evidence-index.md` already records the Teemo self-power fixture and recovery coverage history.

## Implementation

- Added `OnPlaySourcePowerTriggerSourceGuardTests` to pin the shared source predicate against effect-kind string matching.
- `CoreRuleEngine.IsQueuedOnPlaySourcePowerTrigger` now derives this trigger queue route from behavior fields:
  - `PlaysSourceToBaseAsUnit`
  - `AppliesPowerModifierToSourceUnit`
  - `PowerModifierAmount != 0`
- `CoreRuleEngine.BuildOnPlayTriggerQueueItem` is unchanged. Trigger ids remain `TRIGGER-{stackItemId}-{effectKind}`, `sourceObjectId` remains the played unit, and `triggeredByEventKind` remains `UNIT_PLAYED_TO_BASE`.
- Runtime Teemo effect ids, event payloads, stack item shape, recovery expectations, and hidden-information boundaries are unchanged.

## Validation

- Red focused source guard: failed before runtime change because the predicate did not read `behavior.PlaysSourceToBaseAsUnit`.
- Green focused source guard: `OnPlaySourcePowerTriggerSourceGuardTests` passed `1/1`.
- Adjacent Teemo / trigger queue / recovery filter passed `2097/2097`.
- Backend full conformance passed `9024/9024`.

## Holdbacks

This does not close complete on-play trigger breadth, complete BehaviorSpec effect extraction for every source-power trigger, complete trigger ordering / optional prompt matrix, PaymentEngine breadth, the P0 full objective, or READY.
