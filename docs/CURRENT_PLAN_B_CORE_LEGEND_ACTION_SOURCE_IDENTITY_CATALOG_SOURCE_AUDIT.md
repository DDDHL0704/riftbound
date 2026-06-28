# Plan B Core Legend Action Source Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中已经存在于 `TryGetLegendAbility` source 表的传奇来源识别从独立 `Is*LegendCardNo` helper 改为通用 `LegendCardHasAbility(cardNo, abilityId)` 查询。该切片已覆盖 Azir / Ezreal / Teemo / Irelia legend-action source identity 的重复 cardNo 分支，不改变传奇技能费用、时序、目标、待命布置、响应窗口或完整传奇结算语义。

## 2026-06-28 Azir / Lillia Source-Group Supplement

新增 `LegendActionAbilityCatalog`，先把 `LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT` 与 `LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE` 的 source-card groups 收为共享 source rows。`CoreRuleEngine.TryGetLegendAbility` 与 `MatchSession.ImplementedLegendActionAbilities` 现在都调用 `LegendActionAbilityCatalog.SourceCardNosForAbility(...)`，不再分别维护 Azir / Lillia 来源卡号列表。新增 `LegendActionSourceIdentityGuardTests.AzirAndLilliaLegendActionSourceGroupsUseSharedCatalog` 覆盖 source groups、正反例和 Core/MatchSession 源码守卫。Validation passed: focused guard 1/1；LegendActionSourceIdentity / Azir / Lillia / LegendAct / LegendAction / SandSoldier / Faerie representatives 168/168；LegendAction / LegendAct / Azir / Lillia / SandSoldier / Faerie / FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2766/2766；backend full 8870/8870。项目仍 **NOT READY**。

## 2026-06-28 Reaction / Dynamic Source-Group Supplement

`LegendActionAbilityCatalog` 继续收进 Darius / Diana / Kai'Sa / Ornn / Ezreal / Irelia / Teemo 七组 legend-action source-card rows。`CoreRuleEngine.TryGetLegendAbility` 与 `MatchSession.ImplementedLegendActionAbilities` 现在均通过 `LegendActionAbilityCatalog.SourceCardNosForAbility(...)` 读取这些 rows，不再分别维护同一组来源卡号数组。新增 `LegendActionSourceIdentityGuardTests.ReactionAndDynamicLegendActionSourceGroupsUseSharedCatalog`，覆盖七组 source rows、正反例，以及 Core/MatchSession 不再出现旧数组的源码守卫。Validation passed: focused guard 1/1；LegendActionSourceIdentity / Darius / Diana / Kaisa / Ornn / Ezreal / Irelia / Teemo / LegendAct / LegendAction representatives 264/264；LegendAction / LegendAct / Darius / Diana / Kaisa / Ornn / Ezreal / Irelia / Teemo / FullGameEndToEnd / GameHubJoin / CardCatalogBaseline / MatchRecovery adjacent 2826/2826；backend full 8871/8871。项目仍 **NOT READY**。

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
- Azir / Ezreal / Irelia / Teemo runtime effects
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Core Azir legend source identity no longer has a duplicated card-number helper | `IsAzirLegendCardNo` was removed; `ControllerHasAzirLegend` now checks `LegendCardHasAbility(legendState.CardNo, AzirLegendAbilityId)` | Accepted |
| Core Ezreal legend source identity no longer has a duplicated card-number helper | `IsEzrealLegendCardNo` was removed; `ControllerHasEzrealLegend` now checks `LegendCardHasAbility(cardObject.CardNo, EzrealLegendAbilityId)` | Accepted |
| Core Teemo standby-hide source identity no longer has a duplicated card-number helper | `IsTeemoLegendCardNo` was removed; Core `HasTeemoStandbyHidePermission` now checks `LegendCardHasAbility(legendState.CardNo, TeemoLegendAbilityId)` | Accepted |
| Core Irelia reaction ability source identity no longer has a duplicated card-number helper | `IsIreliaLegendCardNo` was removed; the `LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT` source row is still consumed through `LegendCardHasAbility(cardNo, IreliaLegendAbilityId)`. The separate conquest ready-self trigger was later moved to `LegendConquestTriggerSpecRules`. | Accepted |
| Source identity consumes existing legend-action ability rows | `LegendCardHasAbility` reads `TryGetLegendAbility(abilityId).SourceCardNos` | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full official legend-action breadth | complete legend-action data modeling and remaining Core legend helper migration remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers"
```

Result: failed before implementation on `IsEzrealLegendCardNo` in the first slice and `IsAzirLegendCardNo` in the follow-up slice, then 1/1 passed after each implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Azir|FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~Armament|FullyQualifiedName~SandSoldier|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 384/384 passed.

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
- This does not broaden Azir / Ezreal / Teemo / Irelia official behavior beyond the already implemented representative paths.
- Project remains **NOT READY**.
