# Stage 4D-03W PaymentEngine Renata Gold Bonus Baseline Evidence

日期：2026-05-14
结论：**BASELINE RECORDED / PROJECT NOT READY**

本文记录 4D-03W 实现前 baseline。该 baseline 只证明当前 HEAD 在交接前回归通过，并固定 4D-03W 的待实现边界；不代表 Renata Gold extra mana bonus 已实现。

## 1. Baseline Scope

目标切片：`RENATA_GOLD_EXTRA_1_MANA` marker 对 Gold token resource skill 的 bonus mana。

当前事实：

- 4D-03V 已实现 `UNL·T05` / `SFD·T03` Gold token destroy + exhaust + gain 1 generic temporary payment-only rune resource。
- Renata near-victory battlefield-hold trigger 已创建带 `RENATA_GOLD_EXTRA_1_MANA` tag 的 Gold token。
- 当前 Gold activation 对带 marker 的 Gold 仍不加 mana；这是 4D-03W 待实现差异。

## 2. Validation Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：passed 313/313。

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gold|FullyQualifiedName~Renata|FullyQualifiedName~Token|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel|FullyQualifiedName~PayCost|FullyQualifiedName~Equipment|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~LegendTrigger"
```

结果：passed 958/958。

## 3. Baseline Notes

- 当前 no-go test `GoldWithRenataBonusTagStillCreatesOnlyOneGenericTemporaryPowerAndNoMana` 会在实现 4D-03W 时被更新为 positive bonus assertion。
- 4D-03W 不需要新增 frontend runtime authority；前端继续只展示服务端 prompt / event / snapshot metadata。
- 未修改未跟踪文件 `riftbound-dotnet.sln`。

## 4. Verdict

4D-03W handoff baseline ready. 项目仍 **NOT READY**。
