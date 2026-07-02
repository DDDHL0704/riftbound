# Plan B Spell-Played Trigger EffectKind Spec Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the OGS Lux high-cost spell power emitted-effect constant from `CoreRuleEngine`.

The public compatibility effect id `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` is preserved for trigger queue, event, fixture, and recovery compatibility, but it now lives on the `OGS·006/024` behavior row and is projected into `TriggerSpec.EffectKind`. Core reads the effect id from the parsed trigger spec instead of owning an OGS Lux-specific constant.

2026-06-30 follow-up: `MatchRecovery` no longer owns `OgsLuxHighCostSpellCardNoForRecovery`. Recovered snapshot, authoritative-state, and spectator replay source-card validation now use the same `SpellPlayedTriggerSpecRules.TryGetTrigger(sourceCardNo, SpellPlayedTriggerSpecRules.IsUnitHighCostSpellPowerModifierTrigger, out _)` route as runtime TriggerSpec source selection while preserving the public effect id.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGS·006/024` 拉克丝: when the controller plays a spell costing at least 5 mana, the source gets +3 power this turn.
- Existing evidence index entry `p2-preflight-play-ogs-lux-high-cost-spell-static` records the official card row and representative high-cost spell power behavior.
- `docs/CURRENT_PLAN_B_SPELL_PLAYED_TRIGGER_SPEC_EVIDENCE.md` records the prior TriggerSpec migration for OGS Lux, Ravenbloom Student, Diana, and Jhin.

## Implementation

- `TriggerSpec` now carries optional `EffectKind`.
- `ImplementedCardBehavior` now carries optional `TriggerEffectKinds`.
- `CardBehaviorRegistry` records `UnitHighCostSpellPowerModifierEffectKind=OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` on `OGS·006/024`.
- `BehaviorSpecCatalogBuilder` projects matching trigger effect ids onto parsed trigger specs.
- `CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers` now requires and emits `triggerSpec.EffectKind`.
- `src/Riftbound.DevUi/src/types/catalog.ts` includes optional `effectKind` on catalog trigger specs.

## Validation

- Baseline before this slice: backend full conformance passed `9035/9035`.
- Red focused gate failed before implementation because `TriggerSpec.EffectKind` did not exist.
- Green focused gate: `BehaviorSpecCatalogParsesUnitHighCostSpellPowerModifierTrigger|HighCostSpellTriggersDoNotUseLuxSpecificResolver|LuxHighCost|UnitHighCostSpellPowerModifier` passed `48/48`.
- Adjacent / hidden-info gate: `SpellPlayed|HighCostSpell|LuxHighCost|CardCatalogBaselineTests|MatchRecovery` passed `2330/2330`.
- Backend full conformance passed `9035/9035`.
- DevUi build passed.
- 2026-06-30 recovery follow-up focused guard and drift filter `HighCostSpellTriggersDoNotUseLuxSpecificResolver|OgsLuxHighCostSpellSourceCardContextDrift` passed `5/5`.
- 2026-06-30 recovery follow-up adjacent / hidden-info gate `SpellPlayed|HighCostSpell|LuxHighCost|CardCatalogBaselineTests|MatchRecovery|TriggerSourceIdentityGuard` passed `2356/2356`.
- 2026-06-30 recovery follow-up backend full conformance passed `9049/9049`.

## Holdbacks

This does not close complete spell-play trigger breadth, complete high-cost paid-mana override breadth, complete `ORDER_TRIGGERS` / APNAP ordering, complete MatchRecovery generic trigger/effect migration, P0 full objective, P1, or READY.
