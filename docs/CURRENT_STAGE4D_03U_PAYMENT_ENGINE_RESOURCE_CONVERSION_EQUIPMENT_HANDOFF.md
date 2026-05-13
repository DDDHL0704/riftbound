# Stage 4D-03U PaymentEngine Resource Conversion Equipment Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03U 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03R / 4D-03S / 4D-03T 已验收 SFD + OGN Sigil typed payment-only resource family。4D-03U 下一步锁定三张仍在 evidence index 中标记为“横置资源技能暂缓”的装备资源转换技能：

- `OGN·098/298` 能量通道：`{{横置}}：{{反应}}—{{获得}}{{1}}，用以支付法力费用。`
- `SFD·117/221` 远古簇碑：`{{横置}}：{{反应}}—支付任意数量的法力来{{获得}}等量的{{A}}。`
- `SFD·083/221` 海克斯异常体：`{{横置}}：{{反应}} — 支付任意数量的{{A}}来{{获得}}等量的法力。`

成功路径仍为 controlled face-up ready base equipment source，在合法 stack-priority reaction window 中横置 source 并立即结算，不创建 ordinary stack item。该切片只处理上述三张 resource conversion equipment，不扩展到其他装备、target-bearing abilities、keyword costs 或完整 `[A]` / `[C]` resource skill family。

## 2. 当前代码事实

- `CardBehaviorRegistry` 已有三张装备的 0-target ordinary play path：
  - `SFD·117/221` 远古簇碑，打出费用 2。
  - `SFD·083/221` 海克斯异常体，打出费用 3。
  - `OGN·098/298` 能量通道，打出费用 3。
- `rules-evidence-index.md` 已有三张普通装备打出与显式目标拒绝证据，当前仍写明横置资源技能暂缓。
- `P4ActivatedAbilityCatalog` 已有 `DragonSoulSageResourceAbilityId` 的 reaction-speed gain-mana representative，以及 Sigil typed temporary payment-only resource profiles/helper。
- `TemporaryPaymentResourceState` 当前只表达 temporary payment-only rune/power resource；mana gain path 当前通过 normal `RunePool.Mana` 和 `resourceLifecycle=rune-pool-mana-reset-at-turn-cleanup` 表达。

## 3. 建议实现口径

### 能量通道

- 建议 ability id：`ENERGY_CHANNEL_REACTION_EXHAUST_GAIN_1_MANA`。
- 复用 Dragon Soul Sage 的 reaction timing / no-stack / mana gain lifecycle。
- Source 要求为 controller base equipment，而不是 battlefield unit。
- 成功：横置 source，`RunePool.Mana += 1`，事件与 prompt metadata 标明 resource skill / reaction / generatedMana / no ordinary stack item / mana cleanup lifecycle。

### 远古簇碑

- 建议 ability id：`ANCIENT_STELE_REACTION_PAY_MANA_GAIN_GENERIC_POWER`。
- 命令必须带 explicit conversion amount。当前 `ActivateAbilityCommand` 没有独立 amount 字段，建议通过服务端 prompt 暴露的 conversion choice 承载，例如 `OptionalCosts` 中的 `CONVERT_MANA_TO_GENERIC_POWER:<amount>`；除该服务端定义 conversion choice 外的 optional cost / payment resource action 均 rejected no-mutation。
- 第一切片只要求 positive representative amount；缺失、0、负数、超过可用 mana 均 rejected no-mutation。
- 成功：横置 source，扣除等量 mana，创建等量 generic temporary payment-only rune resource ledger，可支付 generic rune cost，不可支付 mana-only / experience / non-rune cost。
- Prompt metadata 应表达 `conversionKind=mana-to-generic-power`、`maxConversionAmount`、`resourceLifecycle=temporary-payment-resource-ledger`、`stackPolicy=no-ordinary-stack-item`。

### 海克斯异常体

- 建议 ability id：`HEXTECH_ANOMALY_REACTION_PAY_GENERIC_POWER_GAIN_MANA`。
- 命令必须带 explicit conversion amount。当前 `ActivateAbilityCommand` 没有独立 amount 字段，建议通过服务端 prompt 暴露的 conversion choice 承载，例如 `OptionalCosts` 中的 `CONVERT_GENERIC_POWER_TO_MANA:<amount>`；除该服务端定义 conversion choice 外的 optional cost / payment resource action 均 rejected no-mutation。
- 第一切片只要求从 ordinary runePool generic power 支付；temporary resource chaining 可作为后续扩展，除非 B 侧能在本切片中安全证明 no-mutation 与 audit。
- 成功：横置 source，通过 shared payment plan / payment validator 消耗等量 generic rune power，`RunePool.Mana += amount`，不创建 ordinary stack item。
- 缺失、0、负数、超过可用 power、mana-only payload、target / unrelated optional-cost payload 均 rejected no-mutation。
- Prompt metadata 应表达 `conversionKind=generic-power-to-mana`、`maxConversionAmount`、`resourceLifecycle=rune-pool-mana-reset-at-turn-cleanup`、`stackPolicy=no-ordinary-stack-item`。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增 `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- 非三张 conversion equipment resource skills。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 5. 必补测试

- 三张 catalog definitions 存在，cardNo / effectKind / source type / reaction / no-target / no-stack metadata 正确。
- 合法 stack-priority reaction prompt 暴露三张 source requirement 与 conversion metadata。
- 能量通道 activation 横置 source、获得 1 mana、不入栈、wrong timing / target / invalid source no-mutation。
- 远古簇碑 activation 以 mana amount 2 为 representative：扣 mana、创建 generic temporary payment-only ledger、可支付 generic rune cost、拒绝 mana-only / experience / overpay / missing amount no-mutation。
- 海克斯异常体 activation 以 generic power amount 2 为 representative：扣 generic power、获得 2 mana、不入栈、拒绝 overpay / missing amount / target / invalid source no-mutation。
- 三张普通装备打出 fixtures 与 target rejection fixtures 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AncientStele|FullyQualifiedName~HextechAnomaly|FullyQualifiedName~EnergyChannel|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost"
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

- 不要扩展到其他 equipment activated skills。
- 不要把 resource conversion skill 做成普通 stack item。
- 不要让 generated temporary power 支付 mana-only、experience 或 non-rune costs。
- 不要在没有测试的情况下允许 temporary resource chaining 支付海克斯异常体的 generic power cost。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03U 通过而关闭完整 `[A]` / `[C]` resource skill family、P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03U 是 Sigil family 之后的 resource conversion equipment focused slice。它应把固定 mana gain、mana-to-generic-power、generic-power-to-mana 三种小转换路径纳入服务端 prompt / command / payment audit，同时继续保持项目 **NOT READY**。
