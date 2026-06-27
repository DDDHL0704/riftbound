# Plan B Legend Conquest Trigger Spec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete evidence for routing legend-source conquered-battlefield ready-self triggers through BehaviorSpec instead of single-legend Core resolvers.

## 1. Official Source

- `data/official/card-catalog.zh-CN.json`: `SFD·195/221` 刀锋舞者 has official text `当你征服一处战场时，你可以选择支付{{1}}，以此让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·195a/221·P` 刀锋舞者 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `SFD·246/221` 刀锋舞者 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `OGN·269/298` 腕豪 has official text `当你征服一处战场时，让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `OGN·310/298` 腕豪 has the same conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `OGN·310*/298` 腕豪 has the same conquest ready-self text.

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

## 3. Runtime Evidence

- `LegendConquestTriggerSpecRules.TryGetLegendConquestPayReadySelfTrigger(cardNo, out trigger)` builds its trigger map from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveLegendConquestPayReadySelfTrigger` scans the conquering player's legend zone and accepts only controlled, exhausted legends whose card number has the parsed `LEGEND_CONQUEST_PAY_1_READY_SELF` trigger.
- Runtime reads the mana cost and ready-source shape from the parsed `TriggerSpec`, pays the parsed mana cost, and readies the source legend.
- The old `ResolveIreliaLegendConquerReadyTrigger` helper is removed, and `LegendConquestPayReadySelfTriggerDoesNotUseIreliaSpecificResolver` blocks reintroducing that Irelia-specific resolver.
- `P79LegendTriggerIreliaPaysOneToReadyLegendOnConquer` proves the accepted `DECLARE_BATTLE` path pays one mana and readies the exhausted Blade Dancer legend.
- `P79LegendTriggerIreliaRequiresManaToReadyLegendOnConquer` proves the trigger does not resolve when the player lacks the parsed mana cost.
- `LegendConquestTriggerSpecRules.TryGetLegendConquestReadySelfTrigger(cardNo, out trigger)` reads the no-cost ready-self shape from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveLegendConquestReadySelfTrigger` scans the conquering player's legend zone and accepts only controlled, exhausted legends whose card number has the parsed `LEGEND_CONQUEST_READY_SELF` trigger.
- The old `ResolveSettLegendConquerReadyTrigger` helper is removed, and `LegendConquestReadySelfTriggerDoesNotUseSettSpecificResolver` blocks reintroducing that Sett-specific resolver.
- `P79LegendTriggerSettReadiesOnConquer` and `SettLegendExhaustedReprintReadiesOnConquer` prove the accepted `DECLARE_BATTLE` path readies the exhausted Sett legend.

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

## 5. Non-Closure

This is a focused representative slice. It does not close optional trigger decline prompts, complete legend trigger ordering, complete legend trigger BehaviorSpec extraction, complete PaymentEngine / PAY_COST breadth, or project READY.
