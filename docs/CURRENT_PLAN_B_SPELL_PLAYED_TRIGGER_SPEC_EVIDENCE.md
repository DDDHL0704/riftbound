# Plan B Spell-Played Trigger Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records concrete evidence for routing OGS Lux high-cost spell, Ravenbloom Student spell-play power, and Diana spell-play power representative triggers through BehaviorSpec instead of single-card Core resolvers.

## 1. Official Source

- `data/official/card-catalog.zh-CN.json`: `OGS·006/024` 拉克丝 has official text `每当你打出费用不低于{{5}}的法术时，让我本回合内{{S}}+3。`
- `data/official/card-catalog.zh-CN.json`: `OGS·021/024` 光辉女郎 has official text `每当你打出一张费用不低于{{5}}的法术时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·103/298` 拉文布鲁姆学生 has official text `每当你打出一张法术牌时，让我本回合内{{S}}+1。`
- `data/official/card-catalog.zh-CN.json`: `UNL-149/219` 黛安娜 has official text `{{伏击}}（你可以选择将我作为{{反应}}牌，打出到有己方单位的战场。）\n每当你打出一个法术时，让我本回合内{{S}}+2。`
- `data/official/card-catalog.zh-CN.json`: `UNL-149a/219` 黛安娜 has official text `{{伏击}}\n每当你打出一个法术时，让我本回合内{{S}}+2。`
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

`RuleTextParser` now parses the official Ravenbloom Student and Diana unit texts into:

- `Kind = UNIT_SPELL_PLAYED_POWER_MODIFIER`
- `Timing = BATTLEFIELD_SPELL_PLAYED`
- `TargetScope = SOURCE_UNIT`
- Ravenbloom Student `PowerDelta = 1`; Diana `PowerDelta = 2`
- `Duration = UNTIL_END_OF_TURN`

The parser evidence is covered by `BehaviorSpecCatalogParsesUnitSpellPlayedPowerModifierTrigger` for `OGN·103/298`, `UNL-149/219`, and `UNL-149a/219`.

`RuleTextParser` now parses the official OGS Lux intro legend text into:

- `Kind = LEGEND_HIGH_COST_SPELL_DRAW_ONE`
- `Timing = BATTLEFIELD_SPELL_PLAYED`
- `MinimumPaidMana = 5`
- `DrawCount = 1`

The parser evidence is covered by `BehaviorSpecCatalogParsesLegendHighCostSpellDrawTrigger`.

## 3. Runtime Evidence

- `SpellPlayedTriggerSpecRules.TryGetUnitHighCostSpellPowerModifierTrigger(cardNo, out trigger)`, `TryGetUnitSpellPlayedPowerModifierTrigger(cardNo, out trigger)`, and `TryGetLegendHighCostSpellDrawTrigger(cardNo, out trigger)` build their trigger map from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveUnitHighCostSpellPowerModifierTriggers` scans the spell player's controlled field units, requires a face-up non-standby unit, reads `MinimumPaidMana`, `PowerDelta`, and `Duration` from the parsed `TriggerSpec`, and applies the power modifier to the source unit.
- The old `ResolveOgsLuxHighCostSpellPlayedTriggers` helper is removed, and `HighCostSpellTriggersDoNotUseLuxSpecificResolver` blocks reintroducing that Lux-specific resolver.
- `CoreRuleEngine.ResolveUnitSpellPlayedPowerModifierTriggers` scans the spell player's controlled field units, requires a face-up non-standby unit whose card number has the parsed no-threshold spell-play power trigger, reads `PowerDelta` and `Duration` from the parsed `TriggerSpec`, and applies the power modifier to the source unit.
- The old `ResolveRavenbloomStudentSpellPlayedTriggers` helper is removed, and `SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver` blocks reintroducing that Ravenbloom-specific resolver.
- `CoreRuleEngine.TryGetLegendHighCostSpellDrawTriggerSource` scans the spell player's legend zone, requires a controlled or legacy-owned source whose card number has the parsed legend high-cost spell draw trigger, reads the parsed threshold and draw count, and applies the draw.
- The current OGS Lux unit power path keeps effectKind `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` for recovery / replay compatibility; the trigger source and parameters are now BehaviorSpec driven.
- The current no-threshold unit spell-play power path keeps trigger id `RAVENBLOOM_STUDENT_SPELL_POWER_PLUS_1` for event / fixture compatibility; the trigger source and parameters are now BehaviorSpec driven.
- `LuxHighCostPaidCostTriggerTests` prove paid-cost semantics remain intact and opponent snapshots do not reveal the hidden drawn card.
- `RealTriggerQueueTests.LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn` continues to cover the trigger queue / power-modifier representative path.
- `P79LegendTriggerLuxDrawsWhenControllerPlaysHighCostSpell` covers the intro legend draw representative path and now expects `LEGEND_HIGH_COST_SPELL_DRAW_ONE`.
- `CoreRuleEngineTriggersRavenbloomStudentWhenSpellPlayed` and `CoreRuleEngineSkipsRavenbloomStudentSpellTriggerWhenSourceIsStandby` cover the Ravenbloom Student representative path.
- `CoreRuleEngineTriggersDianaUnitSpellPlayedPowerModifierWhenSpellPlayed` covers both Diana printings from already-controlled field-unit sources, proving the same resolver reads `PowerDelta=2` from BehaviorSpec and applies +2 until end of turn when the controller plays a spell.

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
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver|FullyQualifiedName~RavenbloomStudent" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.UnitSpellPlayedPowerModifier`; then 5/5 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~SpellPlayedPowerTriggersDoNotUseRavenbloomSpecificResolver|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2361/2361 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~DianaUnitSpellPlayedPowerModifier|FullyQualifiedName~RavenbloomStudent" --nologo
```

Result: first failed before implementation because Diana parsed as generic `on-play` and runtime remained at 3 power; then 6/6 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UnitSpellPlayedPowerModifier|FullyQualifiedName~DianaUnitSpellPlayedPowerModifier|FullyQualifiedName~RavenbloomStudent|FullyQualifiedName~SpellPlayed|FullyQualifiedName~Diana|FullyQualifiedName~LuxHighCost|FullyQualifiedName~HighCostSpell|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery|FullyQualifiedName~TriggerSourceIdentityGuard" --nologo
```

Result: 2373/2373 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8798/8798 passed.

## 5. Non-Closure

This is a focused representative slice. It does not close complete spell-play trigger extraction, complete trigger ordering / APNAP, Jhin high-cost spell banish migration, Diana ambush / reaction hand-to-battlefield timing, PaymentEngine full breadth, MatchRecovery generic effectKind migration, or project READY.
