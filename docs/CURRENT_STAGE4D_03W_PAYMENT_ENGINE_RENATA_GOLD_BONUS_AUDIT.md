# Stage 4D-03W PaymentEngine Renata Gold Bonus Audit

日期：2026-05-14
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

4D-03W 已验收 Renata Gold extra mana bonus focused slice。该切片承接 4D-03V Gold token resource skill，只覆盖带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold token 激活 resource skill 时额外获得 1 mana 的服务端路径；不关闭 equipment-token full rules、token ownership / controller / zone matrix、完整 `[A]` / `[C]` resource skill family、完整 PaymentEngine、coverage matrix full-official 或最终 READY。

## 1. Scope

本切片实现：

- Renata near-victory battlefield-hold trigger 已创建带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold token。
- 带 marker 的 `UNL·T05` / `SFD·T03` Gold token 激活 4D-03V resource skill 时，照常摧毁自身并创建 1 点 generic temporary payment-only rune resource，同时给 controller +1 mana。
- 普通 Gold token 行为保持不变：只创建 1 点 generic temporary payment-only rune resource，不加 mana。

## 2. Implementation Facts

- `P4ActivatedAbilityCatalog` 新增稳定常量：
  - `GoldTokenRenataBonusTag = "RENATA_GOLD_EXTRA_1_MANA"`
  - `GoldTokenRenataBonusMana = 1`
- `CoreRuleEngine.ResolveGoldTokenResourceSkill` 消费该 marker：
  - activation 成功时为当前 controller 的 rune pool 增加 1 mana。
  - `ABILITY_ACTIVATED` / `POWER_GAINED` payload 暴露 `renataGoldExtraManaApplied`、`generatedMana`、`bonusTag`。
  - marker Gold 成功时新增 `MANA_GAINED` event，payload 包含 `generatedMana=1`、`manaAfter` 与 bonus marker。
- `MatchSession` / `ActionPromptBuilder` 对 Gold source requirement 暴露 `renataGoldExtraManaAvailable`、`bonusMana`、`bonusTag`；普通 Gold 为 false / 0 / empty。
- Bonus mana 留在 normal rune pool mana；Gold temporary resource 仍保持 `PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V` payment-only rune resource 限制。

## 3. Tests Added / Updated

更新 `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`，覆盖：

- 普通 Gold prompt metadata 不显示可用 bonus。
- 带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold prompt metadata 显示 bonus mana。
- 普通 Gold activation 仍不加 mana。
- 带 marker 的 `UNL·T05` / `SFD·T03` Gold activation 均增加 1 mana，同时只创建 1 generic temporary payment-only resource。
- 带 marker 的 Gold temporary resource 仍不能用于 mana-only pending cost。
- 带 marker 的 invalid activation rejected no-mutation，不增加 mana。

## 4. Validation

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 320/320。

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~LegendTrigger"
```

结果：passed 965/965。

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：passed 4120/4120。

Whitespace:

```sh
git diff --check
```

结果：无输出。

## 5. Residual Risks

- Equipment token ownership / controller / visibility / zone full matrix 未关闭；本切片只消费已存在 marker。
- Image / Brush / Baron Nest token surfaces 仍 deferred。
- 其他 target-bearing colored-cost activated abilities、remaining payment windows、keyword payment branches、Spellshield full-window tax、Echo costs、replacement / optional / alternative costs 仍待后续切片。
- Coverage matrix full-official 状态未升级。
- 前端运行时代码未修改，前端仍只能展示并提交服务端 `ActionPrompt` / snapshot 暴露的 bonus metadata、event payload 与 resource ledger。

## 6. Verdict

4D-03W focused slice accepted. `RENATA_GOLD_EXTRA_1_MANA` 已从 passive token marker 推进到 Gold resource activation +1 mana bonus；项目仍 **NOT READY**。
