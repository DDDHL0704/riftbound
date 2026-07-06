# Plan B Teemo Unit Identity Catalog Source Audit

日期：2026-06-25；2026-07-06 增量
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Teemo legend-action domain 中“自有 Teemo 单位”目标识别从 `CoreRuleEngine` / `MatchSession` 两份重复 cardNo 白名单改为共享单位身份目录。2026-07-06 增量进一步把规则路径中的 `CardBehaviorRegistry.IsImplementedUnitNamed(targetState.CardNo, "提莫")` 显示名判断提升为 `UnitIdentityCatalog.IsSourceCardNoForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId, targetState.CardNo)`；`UnitIdentityCatalog` 从当前已实现单位行为数据导出 Teemo 单位来源集合。该切片只收窄 `LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT` 的目标身份硬编码，不关闭 Teemo full official、standby replacement breadth、hidden-info / random-zone breadth、完整 legend-action official breadth、PaymentEngine full matrix 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/UnitIdentityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TeemoLegendActionDomainGuardTests.cs`
- `docs/CURRENT_PLAN_B_TEEMO_UNIT_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_TEEMO_UNIT_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- Teemo legend action cost / effect semantics
- standby replacement runtime
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Teemo unit identity no longer duplicated as engine cardNo lists | `CoreRuleEngine.IsTeemoUnitCardNo` and `MatchSession.LegendActionIsTeemoUnitCardNo` were deleted | Accepted |
| Prompt and command target validation share catalog identity | both target checks now call `UnitIdentityCatalog.IsSourceCardNoForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId, targetState.CardNo)` after existing owner / visibility / unit tag checks | Accepted |
| Rule paths no longer embed the Teemo display name | guard test rejects `"提莫"` and `CardBehaviorRegistry.IsImplementedUnitNamed` in `CoreRuleEngine.cs` and `MatchSession.cs`; the display name is isolated to `UnitIdentityCatalog` data mapping | Accepted |
| Registry identity covers current implemented Teemo units | guard test covers `FND-196/298`, `OGN·121/298`, `OGN·121a/298`, `OGN·197/298`, `OGN·197a/298`, `OGN·197b/298`, `SFD·230/221`, `SFD·230*/221` | Accepted |
| Registry identity excludes legends and other units | guard test rejects `OGN·263/298`, `OGN·307/298`, and `SFD·082/221` | Accepted |
| Existing legend-action behavior preserved | focused `P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit` remains green | Accepted |
| Full official Teemo | standby replacement breadth, FAQ adjudication, hidden-info / random-zone breadth and complete legend-action matrix remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TeemoLegendActionDomainGuardTests|FullyQualifiedName~P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit"
```

Result: 14/14 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~Teemo|FullyQualifiedName~LegendAct|FullyQualifiedName~LegendAction|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result: 1151/1151 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore
```

Result: 9183/9183 passed.

## 4. Residual Risks

- This does not broaden Teemo standby replacement or reaction timing semantics.
- This does not adjudicate Teemo FAQ residuals or full legend-action official breadth.
- `CardBehaviorRegistry` remains the underlying data source for currently implemented unit behavior rows consumed by `UnitIdentityCatalog`; missing official Teemo printings would still require catalog/registry data, not engine rule branching.
- Project remains **NOT READY**.
