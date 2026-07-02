# Plan B Spell-Played Trigger EffectKind Spec Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGS·006/024` 拉克丝 states that when the controller plays a spell costing at least 5 mana, the source gets +3 power this turn.

Existing engine evidence:

- `BehaviorSpecCatalogParsesUnitHighCostSpellPowerModifierTrigger` proves the OGS Lux text parses to `UNIT_HIGH_COST_SPELL_POWER_MODIFIER` with `MinimumPaidMana=5`, `PowerDelta=3`, and `Duration=UNTIL_END_OF_TURN`.
- `LuxHighCostPaidCostTriggerTests` and `RealTriggerQueueTests` cover paid-cost threshold, trigger queue, power modification, and hidden draw boundaries.
- `MatchRecovery` contains OGS Lux trigger queue recovery / spectator validation for the preserved compatibility effect id.

## Engine Evidence

Before this slice, `CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers` read the TriggerSpec parameters but still used the Core constant `OgsLuxHighCostSpellPowerEffectKind = OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` for trigger ids, trigger queue effectKind, stack item effectKind, and power-modifier reason.

After this slice:

- `CoreRuleEngine` no longer contains `OgsLuxHighCostSpellPowerEffectKind`.
- `CoreRuleEngine` no longer owns `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.
- `CardBehaviorRegistry` stores `UnitHighCostSpellPowerModifierEffectKind=OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` on the official `OGS·006/024` behavior row.
- `BehaviorSpecCatalogBuilder` projects that behavior-row effect id into `TriggerSpec.EffectKind` for the parsed `UNIT_HIGH_COST_SPELL_POWER_MODIFIER` trigger.
- `CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers` emits the compatibility trigger id and effect kind from `triggerSpec.EffectKind`.
- `MatchRecovery` source-card validation now calls `SpellPlayedTriggerSpecRules.TryGetTrigger(..., SpellPlayedTriggerSpecRules.IsUnitHighCostSpellPowerModifierTrigger, ...)` instead of comparing against `OgsLuxHighCostSpellCardNoForRecovery`.
- Public event, trigger queue, fixture, and recovery effectKind values remain `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.

## Test Evidence

- `CardCatalogBaselineTests.BehaviorSpecCatalogParsesUnitHighCostSpellPowerModifierTrigger` locks `TriggerSpec.EffectKind=OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`.
- `CardCatalogBaselineTests.HighCostSpellTriggersDoNotUseLuxSpecificResolver` now blocks reintroducing the Core constant and the old recovery source-card constant.
- `RecoveryValidatorRejectsSnapshotTimingTriggerQueueOgsLuxHighCostSpellSourceCardContextDrift`, `RecoveryValidatorRejectsAuthoritativeStateTriggerQueueOgsLuxHighCostSpellSourceCardContextDrift`, `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceCardContextDriftWithoutCountMismatch`, and `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceCardContextDriftWithCountMismatch` now expect a BehaviorSpec trigger-shape mismatch instead of a fixed `OGS·006/024` card-number mismatch.
- Existing Lux high-cost focused regressions passed unchanged, proving runtime behavior is preserved.
- Red focused gate failed before implementation because `TriggerSpec.EffectKind` did not exist.
- Focused behavior/spec gate passed `48/48`.
- Adjacent / hidden-info gate passed `2330/2330`.
- Backend full conformance passed `9035/9035`.
- DevUi build passed.
- 2026-06-30 recovery follow-up red/green focused guard and drift filter passed `5/5`.
- 2026-06-30 recovery follow-up adjacent / hidden-info gate passed `2356/2356`.
- 2026-06-30 recovery follow-up backend full conformance passed `9049/9049`.

## Non-Claims

This evidence does not claim complete spell-play trigger breadth, complete high-cost paid-mana override breadth, complete `ORDER_TRIGGERS` / APNAP ordering, complete MatchRecovery generic trigger/effect migration, P0 completion, P1, or READY.
