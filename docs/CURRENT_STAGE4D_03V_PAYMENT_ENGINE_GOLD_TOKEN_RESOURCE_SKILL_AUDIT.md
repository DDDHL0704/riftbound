# Stage 4D-03V PaymentEngine Gold Token Resource Skill Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

4D-03V 已验收 Gold token resource / reaction ability representative。该切片承接 4D-03U 后的 resource skill breadth，但只覆盖 `UNL·T05` 与 `SFD·T03` 金币 equipment token 的 destroy + exhaust + gain generic temporary payment-only rune resource path，不关闭 Renata Gold extra mana bonus、equipment-token full rules、完整 `[A]` / `[C]` resource skill family、完整 PaymentEngine、coverage matrix full-official 或最终 READY。

## 1. Scope

本切片实现：

- `UNL·T05` 金币：`{{反应}} — 摧毁此牌，{{横置}}：{{获得}}{{A}}。`
- `SFD·T03` 金币：`摧毁此牌，{{横置}}：{{反应}}—{{获得}}{{A}}。`

本切片锁定 stack-priority reaction window、controlled face-up ready base equipment token source、no-target / no-stack immediate resolution、source self destroy-as-cost，以及 1 点 generic temporary payment-only rune ledger。

## 2. Implementation Facts

- `P4ActivatedAbilityCatalog` 新增两个 executable resource skill definitions：
  - `GOLD_TOKEN_UNL_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER`
  - `GOLD_TOKEN_SFD_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER`
- 两个 definition 均表达 `IsResourceSkill=true`、`ReactionSpeed=true`、`PaymentOnlyResource=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`、`GeneratedPower=1`。
- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 已移除两个 Gold activated resource deferred surfaces，保留 Image / Brush / Baron Nest deferred surfaces。
- `CoreRuleEngine.ResolveActivateAbility` 对两个 Gold ability 分派到 dedicated resolver。
- 成功 activation 会横置 source、摧毁 source、移出 controller base、进入 owner graveyard，并创建 1 点 generic temporary payment-only rune resource ledger。
- 成功 activation 不创建 ordinary stack item，不改变既有 stack item / priority owner，并清空 passed priority players。
- `MatchSession` / `ActionPromptBuilder` 只在 stack-priority reaction window 为当前 priority player 暴露 controlled face-up ready base Gold equipment token source。
- Prompt metadata 暴露 `resourceSkill`、`reactionSpeed`、`paymentOnly`、`requiresBaseEquipmentSource`、`usesSourceAsDestroyCost`、`generatedGenericPower=1`、`resourceRestriction=PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V`、`resourceLifecycle=temporary-payment-resource-ledger`、`stackPolicy=no-ordinary-stack-item`。
- Temporary payment resource snapshot / prompt restriction 对 Gold ledger 使用 `PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V`。
- Renata Gold bonus marker 保持 no-go：带 `RENATA_GOLD_EXTRA_1_MANA` tag 的 Gold 仍只生成 1 点 generic temporary payment-only rune resource，不加 mana。

## 3. Tests Added

新增 `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`，覆盖：

- 两个 Gold ability catalog definitions。
- Gold deferred activated resource surfaces 已移除，其他 token deferred surfaces 保留。
- 合法 reaction priority prompt 中只向 priority player 暴露 Gold source requirements、destroy-as-cost metadata、temporary ledger lifecycle 与 no-stack policy。
- 非 priority player prompt 不暴露 Gold `ACTIVATE_ABILITY`。
- `UNL·T05` 与 `SFD·T03` 成功 activation 均摧毁 source、移出 base、进入 owner graveyard、创建 generic temporary payment-only ledger、不创建 ordinary stack item。
- Gold temporary resource 可支付 generic rune cost，并在 payment close 后清理。
- Gold temporary resource 拒绝 mana-only、wrong trait 与 unnecessary use，保持 no-mutation。
- 带 Renata bonus tag 的 Gold 仍只创建 1 点 generic temporary power，不获得 mana。
- wrong timing、target、optional cost、temporary resource payload、recycle rune payload、wrong controller、not base、face-down、exhausted、non-equipment、missing Gold tag、wrong card、missing source 均 rejected no-mutation。

同时更新 `ConformanceFixtureRunnerTests` executable ability audit list、P6 deferred token command surface guard 与 `CardCatalogBaselineTests` deferred count。

## 4. Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 288/288。

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment"
```

结果：passed 782/782。

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：passed 4113/4113。

Whitespace:

```sh
git diff --check
```

结果：无输出。

## 5. Residual Risks

- Renata Gold extra mana bonus 未实现，已由 no-go 测试固定。
- Equipment token ownership / controller / visibility / zone full matrix 未关闭；本切片只证明 controlled face-up ready base Gold source representative。
- Image / Brush / Baron Nest token surfaces 仍 deferred。
- 其他 target-bearing colored-cost activated abilities、remaining payment windows、keyword payment branches、Spellshield full-window tax、Echo costs、replacement / optional / alternative costs 仍待后续切片。
- Coverage matrix full-official 状态未升级。
- 前端运行时代码未修改，前端仍只能展示并提交服务端 `ActionPrompt` / snapshot 暴露的 source、timing、destroy-as-cost metadata 与 temporary resource ledger。

## 6. Verdict

4D-03V focused slice accepted. `UNL·T05` 与 `SFD·T03` Gold token resource / reaction ability 已接入服务端 prompt / command / destroy-cost / temporary ledger / payment / audit representative path；项目仍 **NOT READY**。
