# Stage 4D-03D Payment Engine Activate Resource Baseline Evidence

日期：2026-05-13
状态：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03D 实现前测试基线。该基线只说明当前 `ACTIVATE_ABILITY`、基础符文资源技能和既有 payment-resource 代表路径绿色，可作为下一步迁移 `ACTIVATE_ABILITY` payment resource action 的回归护栏；不表示 4D-03D 已实现，不关闭 P0-005。

## 1. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource|FullyQualifiedName~CoreRuleEngineRejectsTapRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineTapsBasicRuneAndReconcilesObjectLocation|FullyQualifiedName~CoreRuleEnginePromptsAndTapsLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|FullyQualifiedName~CoreRuleEngineRecyclesLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneAndReconcilesObjectLocations"
```

结果：passed，79/79。

覆盖点：

- `PaymentEngineUnificationTests` shared plan / audit baseline。
- Vi / Xerath `ACTIVATE_ABILITY` 代表路径。
- `PLAY_CARD` / `ASSEMBLE_EQUIPMENT` 既有 `RECYCLE_RUNE:*` payment resource action。
- 基础 `TAP_RUNE` / `RECYCLE_RUNE` open-main 资源技能、legacy owner fallback、object location reconciliation 与 no-cardNo guard。

## 2. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune|FullyQualifiedName~TapRune|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：passed，252/252。

## 3. Baseline Conclusion

当前代表路径绿色，可以进入 4D-03D 服务端实现；迁移后必须至少复跑上述 focused / adjacent filters、backend full 和 `git diff --check`。本基线不覆盖完整 `[A]` / `[C]` resource skills、`PAY_COST` pending trigger payment 资源动作、所有 `ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield 支付窗口、完整 LayerEngine 或 full-card official matrix。
