# Stage 4D-03W PaymentEngine Renata Gold Bonus Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03W 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03V 已验收 `UNL·T05` 与 `SFD·T03` Gold token resource / reaction ability representative，但明确保留 Renata Gold extra mana bonus。4D-03W 下一步只打开这个已被 token marker 建模的 bonus：

- `SFD·201/221` / `SFD·249/221` 炼金男爵据守战场创建 Gold token。
- 当 Renata controller 距离胜利分数不超过 3 分时，创建的 Gold token 已带 `RENATA_GOLD_EXTRA_1_MANA` marker。
- 带该 marker 的 Gold token 激活 4D-03V resource skill 时，除 1 点 generic temporary payment-only rune resource 外，还应给 controller 增加 1 点 mana。

该切片只处理 `RENATA_GOLD_EXTRA_1_MANA` marker 对 Gold token resource activation 的 bonus mana，不扩展 Renata 据守触发本身、其他 Gold 创建来源、equipment-token full rules、token ownership / controller / zone matrix、完整 `[A]` / `[C]` resource skill family 或 full PaymentEngine。

## 2. 当前代码事实

- `CoreRuleEngine` 已有：
  - `RenataLegendCardNo = "SFD·201/221"`。
  - `RenataGoldBonusWinningScoreDistance = 3`。
  - `RenataGoldBonusTag = "RENATA_GOLD_EXTRA_1_MANA"`。
- `ConformanceFixtureRunnerTests` 已有 `P79LegendTriggerRenataCreatesDormantGoldWhenControllerHoldsBattlefield` 与 `P79LegendTriggerRenataGoldBonusRequiresScoreWithinThree`，证明 Renata near-victory trigger 会给 created Gold token 加 marker，并在非 near-victory 时不加 marker。
- `ResolveGoldTokenResourceSkill` 当前对带 marker 的 Gold 仍只创建 1 点 generic temporary payment-only resource，并通过测试 `GoldWithRenataBonusTagStillCreatesOnlyOneGenericTemporaryPowerAndNoMana` 固定为 4D-03V no-go。
- Gold resource skill 成功路径已有 source destroy-as-cost、no ordinary stack item、temporary payment resource ledger 与 event payload。
- Rune pool mana 是 turn-scoped pool state；4D-03W 应只增加当前 controller 的 mana，不把 bonus 做成 payment-only temporary resource。

## 3. 建议实现口径

- 新增稳定常量建议：
  - `GoldTokenRenataBonusMana = 1`
  - `GoldTokenRenataBonusTag = "RENATA_GOLD_EXTRA_1_MANA"` 或复用 `CoreRuleEngine` 内既有常量。
- 成功 activation：
  - 当 source tags 包含 `RENATA_GOLD_EXTRA_1_MANA` 时，Gold resource skill 仍创建 1 点 generic temporary payment-only rune resource。
  - 同一 transaction 中为 `intent.PlayerId` 的 rune pool 增加 1 点 mana。
  - 写入 event payload：`renataGoldExtraManaApplied=true`、`generatedMana=1`、`bonusTag=RENATA_GOLD_EXTRA_1_MANA`。
  - `POWER_GAINED` 可继续代表 temporary resource；建议额外新增或扩展一个 mana event，避免把 mana 与 payment-only power 混在同一语义里。
- Prompt / metadata：
  - 带 marker 的 Gold source requirement 应暴露 `renataGoldExtraManaAvailable=true`、`bonusMana=1`。
  - 普通 Gold source requirement 应暴露 false 或省略；测试需避免前端自行推断。
- No mutation / guards：
  - 无 marker 的 Gold 仍只创建 1 generic temporary payment-only resource，不增加 mana。
  - marker 只在 activation 成功时生效；wrong timing、invalid source、target / optional cost payload、wrong controller 等 rejected path 不增加 mana。
  - bonus mana 不能作为 temporary payment resource，也不能绕过 existing `PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V` 限制。

## 4. 建议写入范围

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`（仅当需要公开稳定常量 / deferred note）
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- 必要时更新 `ConformanceFixtureRunnerTests` 中 Renata marker assertions。

不建议写入：

- 前端运行时代码。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- Gold token creation source behaviors，除非只补 metadata assertion。
- 非 Renata marker Gold token behavior。
- 未跟踪文件 `riftbound-dotnet.sln`。

## 5. 必补测试

- Prompt metadata：带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold source 暴露 bonus mana metadata；普通 Gold 不暴露或暴露 false。
- 成功 activation：带 marker 的 `UNL·T05` / `SFD·T03` Gold 仍摧毁 source、创建 1 generic temporary payment-only ledger，同时给 controller +1 mana。
- 普通 Gold regression：无 marker 时仍不加 mana。
- Temporary resource restriction regression：bonus mana 留在 rune pool mana；temporary Gold resource 仍只支付 rune cost，不能支付 mana-only cost。
- No-mutation guard：带 marker 的 Gold 在 wrong timing、target、optional cost、wrong controller、face-down、exhausted、missing source 等 rejected path 不增加 mana。
- Renata creation marker regression：near-victory Renata created Gold 仍带 marker，非 near-victory 不带 marker。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~LegendTrigger"
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

- 不要改 Renata 据守触发创建 Gold 的触发条件；本切片只消费已存在的 marker。
- 不要让 bonus mana 进入 temporary payment resource ledger。
- 不要改变普通 Gold 的 generated generic power = 1。
- 不要扩展到非 Gold token surfaces、其他 legend trigger、equipment-token full rules 或 token ownership matrix。
- 不要修改前端运行时代码或 coverage matrix full-official 状态。
- 不要因为 4D-03W 通过而关闭完整 `[A]` / `[C]` resource skill family、P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03W 是 4D-03V Gold token resource skill 后的 Renata marker bonus follow-up。它应把 `RENATA_GOLD_EXTRA_1_MANA` 从 passive marker 推进到服务端 activation bonus，同时继续保持项目 **NOT READY**。
