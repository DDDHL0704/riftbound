# Plan B Trigger Payment Source Identity Catalog Source Audit

日期：2026-06-25
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Fiora / Jax 触发支付窗口的来源单位身份从 `CoreRuleEngine` 本地 cardNo allow-list 改为 `CardBehaviorRegistry` 的已实现单位 `EffectKind` 查询。该切片只收窄触发支付 source identity 硬编码，不关闭完整 trigger-payment family、完整 optional payment prompt breadth、完整装备贴附生命周期、Fiora full official、Jax full official 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `docs/CURRENT_PLAN_B_TRIGGER_PAYMENT_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_TRIGGER_PAYMENT_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- trigger payment cost / effect semantics
- Jax attach legality
- Fiora powerful-ready threshold logic
- frontend runtime

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Jax trigger payment source identity no longer duplicates source cardNo lists | `SfdJaxWeaponAttachCardNo`, `SfdJaxWeaponAttachAltCardNo`, and `IsJaxWeaponAttachCardNo` were deleted from `CoreRuleEngine` | Accepted |
| Fiora trigger payment source identity no longer duplicates source cardNo lists | `SfdFioraPowerfulReadyCardNo`, `SfdFioraPowerfulReadyAltCardNo`, and `IsSfdFioraPowerfulReadyCardNo` were deleted from `CoreRuleEngine` | Accepted |
| Runtime source checks consume existing implemented behavior rows | `TryGetJaxWeaponAttachSource` / `TryGetSfdFioraPowerfulReadySource` call source-behavior helpers backed by `CardBehaviorRegistry.IsImplementedUnitWithEffectKind` | Accepted |
| Registry identity is exact enough to distinguish same-name / wrong-mode rows | guard tests accept only `SFD·119/221`, `SFD·119a/221`, `SFD·180/221`, and `SFD·180a/221` for their exact source effect kinds; they reject unrelated Jax, Ezreal, Fiora cross-mode, and wrong effect kind rows | Accepted |
| Existing trigger payment behavior is preserved | Jax attach payment and Fiora yellow payment representatives remain green | Accepted |
| Full official Jax/Fiora | complete Jax equipment lifecycle and complete Fiora official breadth remain residual | Residual, no full-official claim |

## 3. Verification

Focused:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~CardBehaviorRegistryIdentifiesTriggerPaymentSourceUnitsByEffectKind|FullyQualifiedName~CardBehaviorRegistryRejectsNonMatchingTriggerPaymentSourceUnits|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists|FullyQualifiedName~JaxWeaponAttachOpensTriggerPaymentPrompt|FullyQualifiedName~SfdFioraBoonPowerTransitionOpensYellowTriggerPayment"
```

Result: 13/13 passed.

Adjacent:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~Jax|FullyQualifiedName~Fiora|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ActionPrompt"
```

Result: 867/867 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8554/8554 passed.

## 4. Residual Risks

- This does not broaden Jax attach / detach / reattach lifecycle semantics.
- This does not broaden Fiora full official timing, optionality, or multi-source payment edge cases.
- `CardBehaviorRegistry` remains the data source for currently implemented card behavior rows; missing official printings still require catalog/registry data, not engine rule branching.
- Project remains **NOT READY**.
