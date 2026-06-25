# Plan B Battlefield Spec Domain Helper Naming Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `CoreRuleEngine` 中已经通过 BehaviorSpec / spec-rule domain 查询的战场聚合 helper 从 `Is*CardNo` 命名改为 `Has*Spec` 命名。该切片不改变战场规则语义，只收窄剩余 card-number helper 口径，避免把数据驱动 spec 聚合路径误判为卡号白名单。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `docs/CURRENT_PLAN_B_BATTLEFIELD_SPEC_DOMAIN_HELPER_NAMING_AUDIT.md`
- `docs/CURRENT_PLAN_B_BATTLEFIELD_SPEC_DOMAIN_HELPER_NAMING_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- BehaviorSpec parser / spec model
- battlefield scoring semantics
- battlefield object recognition semantics
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Battlefield spec-domain aggregation is not maintained under `Is*CardNo` helper names | `IsImplementedBattlefieldCardNo` and `IsDedicatedBattlefieldScoreRuleCardNo` were renamed to `HasImplementedBattlefieldRuleSpec` and `HasDedicatedBattlefieldScoreRuleSpec` | Accepted |
| Helper bodies remain spec-domain queries, not card-number allow-lists | guard test requires `BattlefieldTriggerSpecRules.TryGetBattlefield`, `BattlefieldStaticAbilitySpecRules.TryGetBattlefield`, and `StaticAuraSpecRules.TryGetBattlefield` usage | Accepted |
| Battlefield card recognition behavior is preserved | `IsBattlefieldCardObject` still accepts P6 battlefield token tags and implemented battlefield spec rows | Accepted |
| Turn-start held scoring behavior is preserved | dedicated battlefield score specs are still excluded through the same spec-rule checks | Accepted |
| Hidden-info / recovery boundary | `MatchRecovery` remains green | Accepted |
| Full official battlefield breadth | complete battlefield lifecycle / scoring / trigger breadth remains residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames|FullyQualifiedName~P6FunctionalUnitCoverageAuditsSameTextVariantsAndReprints|FullyQualifiedName~BattlefieldFirstTurnScoreTriggerDoesNotUseCardNumberAllowList|FullyQualifiedName~BattlefieldScoreDelayStaticAbilityDoesNotUseCardNumberAllowList"
```

Result: 4/4 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldTriggerSpec|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~StaticAura|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~FullGameEndToEnd"
```

Result: 1107/1107 passed.

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

- This is a naming / audit-scope slice; it does not broaden battlefield trigger, static ability, or static aura official coverage.
- Remaining `private static bool Is*CardNo(...)` helpers are still separate migration candidates and need focused slices before removal.
- Project remains **NOT READY**.
