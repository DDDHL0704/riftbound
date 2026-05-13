# Stage 4D-03F Payment Engine PAY_COST Resource Baseline Evidence

日期：2026-05-13
状态：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03F 实现前测试基线。该基线只说明当前 payment foundation、trigger payment 和 `PAY_COST` 代表路径绿色，可作为下一步接入 pending `PAY_COST` resource action 的回归护栏；不表示 4D-03F 已实现，不关闭 P0-005。

## 1. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"
```

结果：passed，51/51。

覆盖点：

- `PaymentEngineUnificationTests` shared payment plan / audit foundation。
- `TriggerPaymentTests` 当前 mana-only `TRIGGER_PAYMENT` 支付、拒付、非法选择、资源不足 no-mutation 代表路径。
- 既有 `PAY_COST` command / pending payment minimal runtime regression。

## 2. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：passed，229/229。

覆盖点：

- ActionPrompt / GameHub 中现有 payment resource choices、trigger payment prompt、pending payment metadata 和服务端候选恢复路径保持绿色。
- 4D-03D / 4D-03E 已验收的 `ACTIVATE_ABILITY` resource action 与 `HIDE_CARD` payment plan 相关 prompt / Hub regression 仍可作为邻域护栏。

## 3. Current Gap

当前代码面：

- `PendingPaymentState` 不记录 payment resource action ids；`NormalizePendingPayment`、snapshot view 和 `PayCostMetadataFor` 也没有独立 resource action metadata。
- 普通 pending `PAY_COST` path 会直接基于当前 `state.RunePools` 调用 `PaymentCostRules.TryCommitPayment`，不会先应用 `RECYCLE_RUNE:*` resource action。
- 现有 concrete `TRIGGER_PAYMENT` 代表路径均为 `SPEND_MANA:1` / `DECLINE`，尚没有 power-cost trigger resource action 代表窗口。

## 4. Baseline Conclusion

当前代表路径绿色，可以进入 4D-03F 服务端实现；迁移后必须至少复跑上述 focused / adjacent filters、backend full 和 `git diff --check`。本基线不覆盖完整 trigger payment resource action、完整 `[A]` / `[C]` resource skills、Legend / battlefield held score resource action、所有替代 / 额外 / 可选费用窗口、LayerEngine 或 full-card official matrix。
