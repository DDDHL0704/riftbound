# Plan B Legend Conquest Trigger Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for routing legend-source conquered-battlefield triggers through BehaviorSpec instead of single-legend Core resolvers. 2026-07-01 follow-up: the per-effect `TryGetLegendConquest*` helper surface is also removed; runtime source selection now uses generic `TryGetTrigger(cardNo, predicate)` plus TriggerSpec shape predicates.

## 1. Official Source

- `data/official/card-catalog.zh-CN.json`: `SFD·195/221` 刀锋舞者 has official text `当你征服一处战场时，你可以选择支付{{1}}，以此让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·195a/221·P` 刀锋舞者 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `SFD·246/221` 刀锋舞者 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `OGN·269/298` 腕豪 has official text `当你征服一处战场时，让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `OGN·310/298` 腕豪 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `OGN·310*/298` 腕豪 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `UNL-187/219` 皮城执法官 has official text `当你征服一处战场时，如果你给敌方单位分配了不低于3点的过量伤害，则你可以选择让我变为休眠状态，以此让一名单位变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `UNL-229/219` 皮城执法官 has the same overkill exhaust-ready-unit text.
- `data/official/card-catalog.zh-CN.json`: `UNL-229*/219` 皮城执法官 has the same overkill exhaust-ready-unit text.

## 2. BehaviorSpec Evidence

`RuleTextParser` now parses the official Blade Dancer conquest text into:

- `Kind = LEGEND_CONQUEST_PAY_1_READY_SELF`
- `Timing = BATTLEFIELD_CONQUERED`
- `TargetScope = SOURCE_LEGEND`
- `ManaCost = 1`
- `LegendReadyCount = 1`
- `ReadiesSource = true`

The parser evidence is covered by `BehaviorSpecCatalogParsesLegendConquestPayReadySelfTrigger`, which checks all three official Blade Dancer entries.

`RuleTextParser` now parses the official Sett conquest text into:

- `Kind = LEGEND_CONQUEST_READY_SELF`
- `Timing = BATTLEFIELD_CONQUERED`
- `TargetScope = SOURCE_LEGEND`
- `LegendReadyCount = 1`
- `ReadiesSource = true`

The parser evidence is covered by `BehaviorSpecCatalogParsesLegendConquestReadySelfTrigger`, which checks all three official Sett entries.

`RuleTextParser` now parses the official Vi conquest text into:

- `Kind = LEGEND_CONQUEST_OVERKILL_EXHAUST_READY_UNIT`
- `Timing = BATTLEFIELD_CONQUERED`
- `TargetScope = EXHAUSTED_UNIT_ON_FIELD`
- `RequiredOverkillDamage = 3`
- `ExhaustsSource = true`
- `UnitReadyCount = 1`
- `Optional = true`

The parser evidence is covered by `BehaviorSpecCatalogParsesLegendConquestOverkillExhaustReadyUnitTrigger`, which checks all three official Vi entries.

## 3. Runtime Evidence

- `LegendConquestTriggerSpecRules.TryGetTrigger(cardNo, IsLegendConquestPayReadySelfTrigger, out trigger)` builds its trigger map from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveLegendConquestPayReadySelfTrigger` scans the conquering player's legend zone and accepts only controlled, exhausted legends whose card number has the parsed `LEGEND_CONQUEST_PAY_1_READY_SELF` trigger.
- Runtime reads the mana cost and ready-source shape from the parsed `TriggerSpec`, pays the parsed mana cost, and readies the source legend.
- The old `ResolveIreliaLegendConquerReadyTrigger` helper is removed, and `LegendConquestPayReadySelfTriggerDoesNotUseIreliaSpecificResolver` blocks reintroducing that Irelia-specific resolver.
- `P79LegendTriggerIreliaPaysOneToReadyLegendOnConquer` proves the accepted `DECLARE_BATTLE` path pays one mana and readies the exhausted Blade Dancer legend.
- `P79LegendTriggerIreliaRequiresManaToReadyLegendOnConquer` proves the trigger does not resolve when the player lacks the parsed mana cost.
- `LegendConquestTriggerSpecRules.TryGetTrigger(cardNo, IsLegendConquestReadySelfTrigger, out trigger)` reads the no-cost ready-self shape from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveLegendConquestReadySelfTrigger` scans the conquering player's legend zone and accepts only controlled, exhausted legends whose card number has the parsed `LEGEND_CONQUEST_READY_SELF` trigger.
- The old `ResolveSettLegendConquerReadyTrigger` helper is removed, and `LegendConquestReadySelfTriggerDoesNotUseSettSpecificResolver` blocks reintroducing that Sett-specific resolver.
- `P79LegendTriggerSettReadiesOnConquer` and `SettLegendExhaustedReprintReadiesOnConquer` prove the accepted `DECLARE_BATTLE` path readies the exhausted Sett legend.
- `LegendConquestTriggerSpecRules.TryGetTrigger(cardNo, IsLegendConquestOverkillExhaustReadyUnitTrigger, out trigger)` reads the overkill threshold, source-exhaust policy, and unit-ready shape from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveLegendConquestOverkillExhaustReadyUnitTrigger` scans the conquering player's active legend sources by parsed trigger, verifies the battle-assigned overkill count against `RequiredOverkillDamage`, exhausts the source legend, and readies one exhausted field unit.
- The old `ResolveViLegendOverkillConquerTrigger` helper is removed, and `LegendConquestOverkillReadyUnitTriggerDoesNotUseViSpecificResolver` blocks reintroducing that Vi-specific resolver.
- `P79LegendTriggerViReadiesUnitOnOverkillConquer` proves the accepted `DECLARE_BATTLE` path with 4 assigned overkill damage exhausts the source Vi legend and readies one exhausted unit.
- `P79LegendTriggerViRequiresThreeOverkillOnConquer` proves the trigger does not resolve when the assigned overkill damage is below the parsed threshold.
- `LegendConquestTriggerRoutingUsesBehaviorSpecPredicatesInsteadOfEffectHelperAllowList` proves Core no longer calls `LegendConquestTriggerSpecRules.TryGetLegendConquest*`, `LegendConquestTriggerSpecRules` no longer exposes public `TryGetLegendConquest*` helpers, and the shared rule helper exposes `TriggersForCard(...)` plus generic `TryGetTrigger(...)` / shape predicates instead.

The existing Hall of Legends battlefield route remains separate: `BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND` still describes a battlefield source readying a controlled legend; `LEGEND_CONQUEST_PAY_1_READY_SELF` describes a legend source readying itself when its controller conquers a battlefield.
The no-cost Sett route is also distinct: `LEGEND_CONQUEST_READY_SELF` describes a legend source readying itself without a parsed payment cost.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestPayReadySelf" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.LegendConquestPayReadySelf` / `TriggerTargetScopes.SourceLegend`; then 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79LegendTriggerIrelia" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestPayReadySelf|FullyQualifiedName~P79LegendTriggerIrelia|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~DeclareBattle|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2418/2418 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8787/8787 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestReadySelf|FullyQualifiedName~P79LegendTriggerSettReadiesOnConquer|FullyQualifiedName~SettLegendExhaustedReprintReadiesOnConquer" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.LegendConquestReadySelf`; then 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestReadySelf|FullyQualifiedName~SettLegend|FullyQualifiedName~P79LegendTriggerSett|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~DeclareBattle|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2430/2430 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8789/8789 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestOverkill|FullyQualifiedName~P79LegendTriggerVi" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.LegendConquestOverkillExhaustReadyUnit` / `TriggerTargetScopes.ExhaustedUnitOnField`; then 4/4 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestOverkill|FullyQualifiedName~P79LegendTriggerVi|FullyQualifiedName~ViLegend|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~DeclareBattle|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2422/2422 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8791/8791 passed.

2026-07-01 helper-surface follow-up focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquest" --nologo -m:1
```

Result: first failed on old `LegendConquestTriggerSpecRules.TryGetLegendConquest*` calls; then 7/7 passed.

2026-07-01 helper-surface follow-up adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquest|FullyQualifiedName~P79LegendTrigger|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~DeclareBattle|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo -m:1
```

Result: 2527/2527 passed.

2026-07-01 helper-surface follow-up full backend:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-build --nologo -m:1
```

Result: 9079/9079 passed.

## 5. Non-Closure

This is a focused representative slice. It does not close optional trigger decline prompts, complete legend trigger ordering, complete legend trigger BehaviorSpec extraction, complete PaymentEngine / PAY_COST breadth, or project READY.
