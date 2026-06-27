# Plan B Spell-Played Trigger Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for routing OGS Lux high-cost spell representative triggers through BehaviorSpec instead of a Lux-specific Core resolver.

## 1. Official Source

- `data/official/card-catalog.zh-CN.json`: `OGS·006/024` 拉克丝 has official text `每当你打出费用不低于{{5}}的法术时，让我本回合内{{S}}+3。`
- `data/official/card-catalog.zh-CN.json`: `OGS·021/024` 光辉女郎 has official text `每当你打出一张费用不低于{{5}}的法术时，抽一张牌。`
- `docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_EVIDENCE.md` records the prior OGS Lux unit high-cost spell power representative evidence.
- `docs/CURRENT_STAGE4D_04S_PAYMENTENGINE_LUX_HIGH_COST_PAID_COST_EVIDENCE.md` records the paid-cost threshold evidence: cost reduction below threshold does not trigger; Spellshield tax raising paid mana to threshold does trigger.

## 2. BehaviorSpec Evidence

`RuleTextParser` now parses the official OGS Lux unit text into:

- `Kind = UNIT_HIGH_COST_SPELL_POWER_MODIFIER`
- `Timing = BATTLEFIELD_SPELL_PLAYED`
- `TargetScope = SOURCE_UNIT`
- `MinimumPaidMana = 5`
- `PowerDelta = 3`
- `Duration = UNTIL_END_OF_TURN`

The parser evidence is covered by `BehaviorSpecCatalogParsesUnitHighCostSpellPowerModifierTrigger`.

`RuleTextParser` now parses the official OGS Lux intro legend text into:

- `Kind = LEGEND_HIGH_COST_SPELL_DRAW_ONE`
- `Timing = BATTLEFIELD_SPELL_PLAYED`
- `MinimumPaidMana = 5`
- `DrawCount = 1`

The parser evidence is covered by `BehaviorSpecCatalogParsesLegendHighCostSpellDrawTrigger`.

## 3. Runtime Evidence

- `SpellPlayedTriggerSpecRules.TryGetUnitHighCostSpellPowerModifierTrigger(cardNo, out trigger)` and `TryGetLegendHighCostSpellDrawTrigger(cardNo, out trigger)` build their trigger map from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers` scans the spell player's controlled field units, requires a face-up non-standby unit, reads `MinimumPaidMana`, `PowerDelta`, and `Duration` from the parsed `TriggerSpec`, and applies the power modifier to the source unit.
- The old `ResolveOgsLuxHighCostSpellPlayedTriggers` helper is removed, and `HighCostSpellTriggersDoNotUseLuxSpecificResolver` blocks reintroducing that Lux-specific resolver.
- `CoreRuleEngine.TryGetLegendHighCostSpellDrawTriggerSource` scans the spell player's legend zone, requires a controlled or legacy-owned source whose card number has the parsed legend high-cost spell draw trigger, reads the parsed threshold and draw count, and applies the draw.
- The current OGS Lux unit power path keeps effectKind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` for recovery / replay compatibility; the trigger source and parameters are now BehaviorSpec driven.
- `LuxHighCostPaidCostTriggerTests` prove paid-cost semantics remain intact and opponent snapshots do not reveal the hidden drawn card.
- `RealTriggerQueueTests.LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn` continues to cover the trigger queue / power-modifier representative path.
- `P79LegendTriggerLuxDrawsWhenControllerPlaysHighCostSpell` covers the intro legend draw representative path and now expects `LEGEND_HIGH_COST_SPELL_DRAW_ONE`.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitHighCostSpellPowerModifier|FullyQualifiedName~LegendHighCostSpellDraw|FullyQualifiedName~HighCostSpellTriggersDoNotUseLuxSpecificResolver|FullyQualifiedName~LuxHighCostPaidCostTriggerTests" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.UnitHighCostSpellPowerModifier` / `TriggerKinds.LegendHighCostSpellDrawOne`; then 9/9 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitHighCostSpellPowerModifier|FullyQualifiedName~LegendHighCostSpellDraw|FullyQualifiedName~HighCostSpellTriggersDoNotUseLuxSpecificResolver|FullyQualifiedName~LuxHighCostPaidCostTriggerTests|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~LegendActionSourceIdentityGuard|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2363/2363 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8794/8794 passed.

## 5. Non-Closure

This is a focused representative slice. It does not close complete spell-play trigger extraction, complete trigger ordering / APNAP, Jhin high-cost spell banish migration, Ravenbloom Student migration, PaymentEngine full breadth, MatchRecovery generic effectKind migration, or project READY.
