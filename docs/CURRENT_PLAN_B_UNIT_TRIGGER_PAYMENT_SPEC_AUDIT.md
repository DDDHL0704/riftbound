# Plan B Unit Trigger Payment TriggerSpec Audit

日期：2026-06-27
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 `SFD·119/221` / `SFD·119a/221` Jax 武装贴附后支付 1 抽 1，以及 `SFD·180/221` / `SFD·180a/221` Fiora 己方单位变为强力后支付黄色使其活跃，从 `CoreRuleEngine` 的 trigger-payment source-effect 分支进一步迁移到官方文本解析出的 `BehaviorSpec.Triggers`。该切片只收窄 Jax / Fiora 两条已实现 trigger-payment representative 的 source / cost / effect shape 来源，不关闭完整 trigger-payment family、完整装备贴附生命周期、Fiora full official、Jax full official、完整 PaymentEngine / PAY_COST breadth、P0 full objective 或 READY。

## 1. Scope

Changed:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`
- `src/Riftbound.Engine/UnitTriggerPaymentSpecRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.DevUi/src/types/catalog.ts`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `docs/CURRENT_PLAN_B_UNIT_TRIGGER_PAYMENT_SPEC_AUDIT.md`
- `docs/CURRENT_PLAN_B_UNIT_TRIGGER_PAYMENT_SPEC_EVIDENCE.md`
- `docs/CURRENT_PLAN_B_TRIGGER_PAYMENT_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_TRIGGER_PAYMENT_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`

Not changed:

- official card catalog JSON
- trigger-payment prompt / event payload names
- Jax attach legality and attach lifecycle
- Fiora powerful threshold semantics
- payment engine authorization / commit primitives
- card matrix full-official status

## 2. Official Inputs

- `data/official/card-catalog.zh-CN.json`: `SFD·119/221` and `SFD·119a/221` contain “当你为我贴附武装时，可以选择支付{{1}}，以此抽一张牌。”
- `data/official/card-catalog.zh-CN.json`: `SFD·180/221` and `SFD·180a/221` contain “当你控制的一名单位变为{{强力}}时，你可以选择支付{{黄色}}，以此让其变为活跃状态。（战力达到5或以上时，即为强力单位。）”

## 3. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Jax trigger payment source / cost / draw shape comes from `TriggerSpec` | `RuleTextParser` emits `TriggerKinds.UnitArmamentAttachedPayDraw`, `Timing=UNIT_ARMAMENT_ATTACHED`, `TargetScope=FRIENDLY_EQUIPMENT`, `ManaCost=1`, `DrawCount=1`, `Optional=true`; `CoreRuleEngine` reads it through `UnitTriggerPaymentSpecRules.TryGetUnitArmamentAttachedPayDrawTrigger(...)` | Accepted |
| Fiora trigger payment source / cost / ready shape comes from `TriggerSpec` | `RuleTextParser` emits `TriggerKinds.UnitControlledUnitPowerfulPayPowerReady`, `Timing=CONTROLLED_UNIT_BECAME_POWERFUL`, `TargetScope=CONTROLLED_UNIT_ON_FIELD`, `PowerCost=1`, `PowerCostTrait=yellow`, `RequiredPowerThreshold=5`, `UnitReadyCount=1`, `Optional=true`; `CoreRuleEngine` reads it through `UnitTriggerPaymentSpecRules.TryGetUnitControlledUnitPowerfulPayPowerReadyTrigger(...)` | Accepted |
| Core no longer duplicates Jax / Fiora trigger-payment source-effect selectors | `SfdJaxWeaponAttachSourceEffectKind`, `SfdJaxWeaponAttachAltSourceEffectKind`, `IsJaxWeaponAttachSourceBehavior`, `SfdFioraPowerfulReadySourceEffectKind`, `SfdFioraPowerfulReadyAltSourceEffectKind`, and `IsSfdFioraPowerfulReadySourceBehavior` were removed from `CoreRuleEngine` | Accepted |
| Existing wire/event compatibility is preserved | trigger/effect strings remain `JAX_WEAPON_ATTACH_PAY_1_DRAW_1` and `SFD_FIORA_POWERFUL_READY_PAY_YELLOW_READY`; prompt/event payload shape is unchanged | Accepted |
| Frontend shared catalog type stays aligned | `src/Riftbound.DevUi/src/types/catalog.ts` now includes `powerCostTrait?: string | null` | Accepted |
| Full official breadth | complete Jax/Fiora timing, multiplicity, ordering, target selection, equipment lifecycle and payment edge cases remain residual | Residual, no full-official claim |

## 4. Verification

Initial focused TDD guard failed before implementation because the shared contract did not yet expose the new unit trigger-payment trigger kinds / timings. After implementation:

```sh
/Users/dinghaolin/.dotnet/dotnet build src/Riftbound.Engine/Riftbound.Engine.csproj -c Debug --nologo
```

Result: build passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~SfdFiora|FullyQualifiedName~TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists|FullyQualifiedName~BehaviorSpecCatalogParsesUnitTriggerPaymentTriggers" --nologo
```

Result: 40/40 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~Jax|FullyQualifiedName~Fiora|FullyQualifiedName~PaymentEngine|FullyQualifiedName~MatchRecovery|FullyQualifiedName~CardCatalogBaseline" --nologo
```

Result: 3175/3175 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8813/8813 passed.

```sh
/opt/homebrew/bin/npm --prefix src/Riftbound.DevUi run build
```

Result: passed. npm emitted existing config warnings and Vite emitted the existing chunk-size warning.

## 5. Residual Risks

- This does not broaden Jax attach / detach / reattach lifecycle semantics.
- This does not broaden Fiora full official timing, optionality, simultaneous trigger ordering, or multi-source payment edge cases.
- This does not migrate Icevale Archer attack-payment from catalog source-effect selection to `TriggerSpec`.
- This does not close full PaymentEngine / PAY_COST breadth, card matrix full-official, frontend final validation, formal E2E, P0 full objective or READY.
