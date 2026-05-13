# Stage 4D-03G Payment Engine Battlefield Held Resource Baseline Evidence

日期：2026-05-13
状态：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03G 实现前测试基线。该基线只说明当前 battlefield held score、PaymentEngine foundation 与相邻 prompt / Hub 路径绿色，可作为下一步接入 held-score `RECYCLE_RUNE:*` payment resource action 的回归护栏；不表示 4D-03G 已实现，不关闭 P0-005。

## 1. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：passed，21/21。

覆盖点：

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 现有普通 power 与 typed power 支付路径。
- third-turn delay / prevent 的据守得分支付阻断路径。
- `PaymentEngineUnificationTests` shared payment plan / audit foundation 与既有 resource-action regression。

## 2. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：passed，219/219。

覆盖点：

- BattlefieldHeld 相关 Core / Hub / prompt regression 保持绿色。
- ActionPrompt / GameHub 中现有 payment resource choices、sourceRequirements 和服务端候选恢复路径保持绿色。
- 4D-03D / 4D-03F 已验收的 payment resource action prompt / command guard 仍可作为邻域护栏。

## 3. Current Gap

当前代码面：

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 已经能通过 shared `PaymentPlan` / `TryCommitPayment` 支付 4 点 power。
- 该路径能使用当前 `Power` / `PowerByTrait` 支付泛化 4 power，但尚不能在同一个据守得分支付中先执行 `RECYCLE_RUNE:*` payment resource action 来补足 power。
- 当前普通 pending `PAY_COST` 已具备 payment resource action foundation，可作为 held-score resource action 的实现参考。

## 4. Baseline Conclusion

当前代表路径绿色，可以进入 4D-03G 服务端实现；迁移后必须至少复跑上述 focused / adjacent filters、backend full 和 `git diff --check`。本基线不覆盖完整 trigger payment resource action、完整 `[A]` / `[C]` resource skills、Legend resource action、所有替代 / 额外 / 可选费用窗口、LayerEngine 或 full-card official matrix。
