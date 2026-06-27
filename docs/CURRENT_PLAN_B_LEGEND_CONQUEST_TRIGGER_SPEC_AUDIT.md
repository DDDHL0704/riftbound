# Plan B Legend Conquest Trigger Spec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把刀锋舞者的“当你征服一处战场时，支付 1 让自己变为活跃状态”从 `CoreRuleEngine` 的 Irelia 专用 resolver 迁移到 BehaviorSpec 驱动的传奇征服触发路径。该切片不改变刀锋舞者的传奇反应技能、支付语义、战斗征服条件或完整传奇触发排序语义。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/LegendConquestTriggerSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_PLAN_B_LEGEND_CONQUEST_TRIGGER_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_LEGEND_CONQUEST_TRIGGER_SPEC_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `TryGetLegendAbility` source list for the Irelia reaction ability
- the existing representative auto-pay behavior for optional trigger costs
- battlefield `BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND` / Hall of Legends routing
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Official Blade Dancer conquest ready-self text parses into BehaviorSpec | `SFD·195/221`, `SFD·195a/221·P`, and `SFD·246/221` parse to `LEGEND_CONQUEST_PAY_1_READY_SELF` with `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=SOURCE_LEGEND`, `ManaCost=1`, `LegendReadyCount=1`, `ReadiesSource=true` | Accepted |
| Core no longer uses an Irelia-specific conquest resolver | `ResolveIreliaLegendConquerReadyTrigger` is removed; `CoreRuleEngine` calls `ResolveLegendConquestPayReadySelfTrigger` and checks `LegendConquestTriggerSpecRules.TryGetLegendConquestPayReadySelfTrigger` | Accepted |
| Runtime behavior remains covered | Existing Irelia conquest representatives still pay one mana, ready the exhausted source legend, and skip when mana is unavailable | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` adjacent representatives remain green | Accepted |
| Complete legend-trigger breadth | Multi-source ordering, optional decline prompts, and the rest of legend conquest trigger family remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestPayReadySelf" --nologo
```

Result: first failed before implementation on missing `TriggerKinds.LegendConquestPayReadySelf` / `TriggerTargetScopes.SourceLegend`; then 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79LegendTriggerIrelia" --nologo
```

Result: 2/2 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendConquestPayReadySelf|FullyQualifiedName~P79LegendTriggerIrelia|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~DeclareBattle|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2418/2418 passed.

Full backend:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8787/8787 passed.

## 4. Residuals

- Optional yes/no prompt semantics for this and adjacent optional triggers remain representative auto-resolution.
- Simultaneous multiple legend conquest sources and APNAP ordering remain open.
- Complete legend trigger BehaviorSpec extraction remains open.
- Project remains NOT READY.
