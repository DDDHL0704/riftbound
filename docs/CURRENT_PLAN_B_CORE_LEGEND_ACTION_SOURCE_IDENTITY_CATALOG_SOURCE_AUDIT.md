# Plan B Core Legend Action Source Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中三个已经存在于 `TryGetLegendAbility` source 表的传奇来源识别从独立 `Is*LegendCardNo` helper 改为通用 `LegendCardHasAbility(cardNo, abilityId)` 查询。该切片只收窄 Ezreal / Teemo / Irelia legend-action source identity 的重复 cardNo 分支，不改变传奇技能费用、时序、目标、待命布置、响应窗口或完整传奇结算语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/LegendActionSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_ACTION_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_CORE_LEGEND_ACTION_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `TryGetLegendAbility` source lists or ability semantics
- official card catalog JSON
- Teemo standby hide payment semantics
- Ezreal / Irelia / Teemo runtime effects
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Core Ezreal legend source identity no longer has a duplicated card-number helper | `IsEzrealLegendCardNo` was removed; `ControllerHasEzrealLegend` now checks `LegendCardHasAbility(cardObject.CardNo, EzrealLegendAbilityId)` | Accepted |
| Core Teemo standby-hide source identity no longer has a duplicated card-number helper | `IsTeemoLegendCardNo` was removed; Core `HasTeemoStandbyHidePermission` now checks `LegendCardHasAbility(legendState.CardNo, TeemoLegendAbilityId)` | Accepted |
| Core Irelia trigger source identity no longer has a duplicated card-number helper | `IsIreliaLegendCardNo` was removed; `TryGetExhaustedIreliaLegend` now checks `LegendCardHasAbility(candidate.CardNo, IreliaLegendAbilityId)` | Accepted |
| Source identity consumes existing legend-action ability rows | `LegendCardHasAbility` reads `TryGetLegendAbility(abilityId).SourceCardNos` | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full official legend-action breadth | complete legend-action data modeling and remaining Core legend helper migration remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsEzrealLegendCardNo`, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~Ezreal|FullyQualifiedName~Teemo|FullyQualifiedName~Irelia|FullyQualifiedName~HideCard|FullyQualifiedName~RevealCard|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 544/544 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8580/8580 passed.

## 4. Residual Risks

- This does not move the full legend-action ability table out of `CoreRuleEngine`.
- This does not remove the remaining non-action Core legend helpers.
- This does not broaden Ezreal / Teemo / Irelia official behavior beyond the already implemented representative paths.
- Project remains **NOT READY**.
