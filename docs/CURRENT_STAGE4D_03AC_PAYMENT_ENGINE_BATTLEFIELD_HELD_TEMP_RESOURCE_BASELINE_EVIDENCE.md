# Stage 4D-03AC PaymentEngine Battlefield Held Temporary Resource Baseline Evidence

日期：2026-05-14
结论：**BASELINE READY / PROJECT NOT READY**

本文件记录 4D-03AC 实现前基线。当前代码尚未实现 `BATTLEFIELD_HELD` 对 `TEMP_PAYMENT_RESOURCE:*` 的 quote / command parity；本基线只证明相邻 PaymentEngine / battlefield held / prompt 回归在实现前为绿色。

## Baseline Findings

- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 已支持必要 `RECYCLE_RUNE:*` payment resource action。
- `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` 已支持 inline temporary payment resource consumption。
- pending `PAY_COST` 已支持 snapshot / prompt 合并 temporary payment resource choices。
- `DECLARE_BATTLE` held-score path 仍未暴露 temporary payment resource choices，也未在 command path 消费 `TEMP_PAYMENT_RESOURCE:*`。
- `LEGEND_ACT` 本轮不作为 4D-03AC 目标：现有 implemented legend actions 是 mana / experience cost 或 resource-producing action，没有 power-cost payment window，不能用 `RECYCLE_RUNE:*` 支付 mana。

## Validation Commands

Exploratory PaymentEngine / legend prompt baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~P79LegendAct|FullyQualifiedName~ActionPromptLegendAct"
```

Result: passed 73/73.

Focused battlefield held / declare battle / PaymentEngine prompt baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt"
```

Result: passed 208/208.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AC is ready for implementation as a narrow battlefield-held temporary payment resource parity slice. The project remains **NOT READY**.
