# Plan B Teemo Legend Action Source Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `MatchSession` 中 Teemo 待命布置权限的传奇来源识别从独立 `IsTeemoLegendCardNo` helper 改为已实现 `LEGEND_ACT` ability source 表查询，并删除未使用的 `IsImplementedLegendActionCardNo` helper。该切片只收窄 `MatchSession` 的 `Is*CardNo` helper 口径，不改变 Teemo 待命布置、Teemo legend action、隐藏信息或完整传奇结算语义。

## 1. Scope

Changed:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TeemoLegendActionDomainGuardTests.cs`
- `docs/CURRENT_PLAN_B_TEEMO_LEGEND_ACTION_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_TEEMO_LEGEND_ACTION_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- `CoreRuleEngine` legend resolution semantics
- official card catalog JSON
- Teemo legend action cost / effect semantics
- Teemo standby hide payment semantics
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Teemo standby hide source identity no longer has a separate MatchSession card-number helper | `IsTeemoLegendCardNo` was removed; `HasTeemoStandbyHidePermission` now checks `HasImplementedLegendActionAbility(legendState.CardNo, TeemoLegendAbilityId)` | Accepted |
| Dead legend-action cardNo helper is removed | unused `IsImplementedLegendActionCardNo` was deleted | Accepted |
| Source identity consumes existing implemented legend-action ability rows | `HasImplementedLegendActionAbility` searches `ImplementedLegendActionAbilities()` by `AbilityId` and source list | Accepted |
| Existing Teemo legend-action target identity remains registry-backed | `CardBehaviorRegistry.IsImplementedUnitNamed(targetState.CardNo, "提莫")` guard remains green | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full official legend-action breadth | complete legend-action data modeling and Core legend helper migration remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists"
```

Result: failed before implementation on `IsTeemoLegendCardNo`, then 1/1 passed after implementation.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TeemoLegendAction|FullyQualifiedName~LegendAction|FullyQualifiedName~LegendAct|FullyQualifiedName~P6LegendAbilityCatalog|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 327/327 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8579/8579 passed.

## 4. Residual Risks

- This does not move the full legend-action ability definition table out of `MatchSession`.
- This does not remove the remaining `CoreRuleEngine` legend-specific helpers.
- This does not broaden Teemo standby replacement, reaction timing, or FAQ adjudication.
- Project remains **NOT READY**.
