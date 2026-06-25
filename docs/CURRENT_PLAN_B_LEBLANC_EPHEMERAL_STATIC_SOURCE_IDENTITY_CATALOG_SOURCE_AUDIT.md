# Plan B LeBlanc Ephemeral Static Source Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把乐芙兰“同战场瞬息效果不会触发”的开始阶段清理抑制 source identity 从 `CoreRuleEngine` 本地 cardNo allow-list 改为 `CardBehaviorRegistry` 的已实现单位 `EffectKind` 查询。该切片只收窄 LeBlanc ephemeral static source identity 硬编码，不关闭完整瞬息 replacement / cleanup breadth、完整 LeBlanc official、完整 lifecycle matrix 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_LEBLANC_EPHEMERAL_STATIC_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_LEBLANC_EPHEMERAL_STATIC_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- LeBlanc cleanup suppression semantics
- `CardBehaviorRegistry` data rows
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| LeBlanc ephemeral static source identity no longer duplicates source cardNo lists | `LeblancEphemeralStaticUnitCardNo` and `IsLeblancEphemeralStaticUnitCardNo` were deleted from `CoreRuleEngine`; guard test blocks reintroduction | Accepted |
| Runtime source checks consume existing implemented behavior rows | `IsEphemeralTurnStartSuppressedByLeblancStatic` now calls `IsLeblancEphemeralStaticSourceBehavior`, backed by `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` | Accepted |
| Registry identity distinguishes LeBlanc static rows from other LeBlanc rows | tests accept `UNL-090/219` with `LEBLANC_PLAY_KEYWORD_UNIT` and `UNL-090a/219` with `LEBLANC_ALT_A_BACK_ROW_STATIC_PLAY_UNIT`, while rejecting `UNL-172/219` and cross-effect matches | Accepted |
| Existing cleanup behavior is preserved | focused `CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield` remains green | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full official lifecycle | complete ephemeral / replacement / cleanup lifecycle breadth remains residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardBehaviorRegistryIdentifiesLeblancEphemeralStaticSuppressionSourcesByEffectKind|FullyQualifiedName~CardBehaviorRegistryRejectsNonMatchingLeblancEphemeralStaticSuppressionSources|FullyQualifiedName~LeblancEphemeralStaticSuppressionDoesNotUseDuplicatedCardNumberAllowList|FullyQualifiedName~CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield"
```

Result: 8/8 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Leblanc|FullyQualifiedName~Ephemeral|FullyQualifiedName~Lifecycle|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~GameHub|FullyQualifiedName~FullGameEndToEnd"
```

Result: 595/595 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8578/8578 passed.

## 4. Residual Risks

- This does not broaden complete `瞬息` cleanup/replacement ordering.
- This does not close LeBlanc full official breadth, legend-trigger breadth, or all lifecycle edge cases.
- `CardBehaviorRegistry` remains the data source for currently implemented LeBlanc unit behavior rows; missing official printings still require catalog/registry data, not Core cardNo branching.
- Project remains **NOT READY**.
