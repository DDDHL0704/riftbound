# Stage 4D-03V PaymentEngine Gold Token Resource Skill Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03V 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03R / 4D-03S / 4D-03T 已验收 SFD + OGN Sigil typed payment-only resource family，4D-03U 已验收三张 resource conversion equipment skills。4D-03V 下一步锁定 Gold equipment token 的 full resource / reaction ability 缺口：

- `UNL·T05` 金币：`{{反应}} — 摧毁此牌，{{横置}}：{{获得}}{{A}}。（获得费用资源的技能无法成为其他法术的反应目标。）`
- `SFD·T03` 金币：`摧毁此牌，{{横置}}：{{反应}}—{{获得}}{{A}}。（获得费用资源的技能无法成为其他法术的反应目标。）`

成功路径应为 controlled face-up ready base equipment token source，在合法 stack-priority reaction window 中以 source 自身作为费用横置并摧毁，立即创建 1 点 generic temporary payment-only rune resource ledger，不创建 ordinary stack item。该切片只处理 Gold token 的 activated resource skill，不扩展到 Image / Brush / Baron Nest token surfaces、装备 token 全规则、Gold 生成来源全矩阵、Renata extra-mana bonus 或完整 `[A]` / `[C]` resource skill family。

## 2. 当前代码事实

- `P6TokenFactoryCatalog` 已登记两个 Gold equipment token identity：
  - `UNL·T05` 金币，tags 含 `CARD_TYPE:EQUIPMENT`、`金币`、`反应`。
  - `SFD·T03` 金币，tags 含 `CARD_TYPE:EQUIPMENT`、`金币`、`反应`。
- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` 当前仍列出：
  - `TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_UNL`
  - `TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_SFD`
- `rules-evidence-index.md` 的 Gold equipment token identity 行仍明确缺口为 `Gold token full resource / reaction ability、equipment token 全规则、token ownership / controller / zone matrix`。
- 多条现有路径已创建 dormant / exhausted Gold equipment token，包括 Treasure Hunter、Honest Broker、Treasure Golem、Painful Payoff、Jungle Ambush、Blood Money、Battlefield Conquer Gold、Sivir / Renata 触发等；本切片不需要改动这些 Gold 生成逻辑。
- `CoreRuleEngine` 已有 `RenataGoldBonusTag = RENATA_GOLD_EXTRA_1_MANA`，且现有测试只证明生成的 Gold token 可带该 marker。本切片不实现 Renata Gold extra mana bonus；带该 tag 的 Gold 在 4D-03V 中仍按 ordinary Gold 生成 1 点 generic temporary payment-only rune resource。
- 现有 Malzahar / Sigil / conversion equipment slices 已提供 reusable patterns：source validation、reaction timing、temporary payment resource ledger、no ordinary stack item、payment-only cleanup 与 no-mutation guards。

## 3. 建议实现口径

- 建议 ability ids：
  - `GOLD_TOKEN_UNL_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER`
  - `GOLD_TOKEN_SFD_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER`
- Source 要求：
  - 当前 player 控制。
  - source 在 controller base。
  - source 为 face-up ready equipment token。
  - `CardNo` 为 `UNL·T05` 或 `SFD·T03`，且 identity tags 包含 `金币` / `反应`。
- Timing：
  - 只在 stack-priority reaction window，且当前 player 持有 priority 时 prompt / command 合法。
  - 不在 open-main、spell-duel wrong focus、battle wrong focus、pending payment 或 cleanup task 中开放。
- Payload：
  - 无 target。
  - 无 optional cost。
  - 无 `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` / conversion choice。
  - `sourceObjectId` 必须就是被摧毁的 Gold token source。
- 成功：
  - 在同一 transaction 中横置 source 并摧毁 source，移出 base，并进入 owner graveyard 或沿用当前 engine 的 equipment-token destroy semantics。
  - 记录 destroy event，建议 payload 含 `reason=RESOURCE_SKILL_COST`、`destroyedCostObjectId=sourceObjectId`、`abilityId`。
  - 创建 1 点 generic temporary payment-only rune resource ledger；建议 restriction 为 `PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V` 或同等稳定常量。
  - 该 ledger 可支付 generic rune cost，不可支付 mana-only、experience、non-rune cost；支付关闭 / 回合资源清理时清除。
  - 不创建 ordinary stack item，不改变已有 stack item / focus / priority owner。
- Prompt metadata 应表达 `resourceSkill=true`、`reactionSpeed=true`、`paymentOnly=true`、`generatedGenericPower=1`、`requiresBaseEquipmentSource=true`、`usesSourceAsDestroyCost=true`、`resourceLifecycle=temporary-payment-resource-ledger`、`stackPolicy=no-ordinary-stack-item`。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/P6TokenFactoryCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增 `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 非 Gold token activated resource skill。
- Gold token creation source behaviors，除非必要测试 fixture 需要最小 seed helper。
- Renata Gold extra mana bonus。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 5. 必补测试

- Catalog executable definitions 存在，`UNL·T05` / `SFD·T03` Gold deferred resource surfaces 从 P6 deferred list 移除，其他 token deferred surfaces 保持不变。
- 合法 stack-priority reaction prompt 暴露 controlled face-up ready base Gold token source；metadata 含 reaction / resource skill / payment-only / destroy-as-cost / no-stack / generated power / temporary ledger lifecycle。
- `UNL·T05` 与 `SFD·T03` 成功 activation 均会横置并摧毁 source、移出 base、写入 owner graveyard 或当前 destroy semantics、创建 1 点 generic temporary payment-only ledger、不创建 ordinary stack item。
- Temporary Gold resource 可支付 generic rune cost，并在 payment close / resource cleanup 后移除。
- Temporary Gold resource 拒绝 mana-only、experience、wrong trait / non-rune cost、unnecessary use，并保持 no-mutation。
- 缺失 source、wrong cardNo、wrong controller、source 不在 base、face-down、exhausted、non-equipment、缺 Gold identity tag、target payload、optional cost payload、`TEMP_PAYMENT_RESOURCE:*` payload、`RECYCLE_RUNE:*` payload、wrong timing 均 rejected no-mutation。
- 带 `RENATA_GOLD_EXTRA_1_MANA` tag 的 Gold token 在本切片仍只生成 1 点 generic temporary payment-only rune resource，或明确 no-go test 保持额外法力行为 deferred。
- 现有 Gold token creation / trigger payment / Sigil / conversion equipment / temporary payment resource 回归继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment"
```

如触及 shared payment plan / temporary resource / prompt path，A 验收时应追加 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要实现 Renata Gold extra mana bonus；该 bonus 另开切片。
- 不要把 Gold resource skill 做成 ordinary stack item。
- 不要让 Gold temporary resource 支付 mana-only、experience 或 non-rune costs。
- 不要扩展到非 Gold token surfaces。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03V 通过而关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family、P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03V 是 resource conversion equipment 之后的 Gold token resource / reaction ability focused slice。它应把两个 Gold equipment token 的 destroy + exhaust + gain generic payment-only rune resource 能力纳入服务端 prompt / command / payment audit，同时继续保持项目 **NOT READY**。
