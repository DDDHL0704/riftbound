# Stage 4D-03E Payment Engine Hide Card Baseline Evidence

日期：2026-05-13
状态：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03E 实现前测试基线。该基线只说明当前 `HIDE_CARD`、待命暗置 / 翻开相邻窗口、ActionPrompt、GameHub 和 shared PaymentEngine foundation 相关代表路径绿色，可作为下一步迁移 `HIDE_CARD` standby payment plan 的回归护栏；不表示 4D-03E 已实现，不关闭 P0-005。

## 1. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：passed，84/84。

覆盖点：

- `HIDE_CARD` 标准 `STANDBY_A` 待命暗置、Teemo `STANDBY_TEEMO_MANA` 替代费用和 Guerrilla Warfare `STANDBY_FREE` 免费待命代表路径。
- `HIDE_CARD` 费用不足、非法来源、非法去向、错误 optional cost、stack / window guard、unknown card identity 等 no-mutation 边界。
- Bandle Tree 额外待命目的地、Ember Monk 待命触发与 standby prompt source filtering。
- `PaymentEngineUnificationTests` shared plan / audit baseline。

## 2. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~RevealCard|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：passed，286/286。

覆盖点：

- `REVEAL_CARD` standby reveal / reaction 相邻窗口保持绿色。
- `ActionPrompt` / `GameHub` 中待命暗置、待命翻开、未知隐藏来源和相关 prompt metadata 仍可恢复。
- 既有 shared PaymentEngine representative tests 与 standby UI contract 相关 Hub regression 仍为下一步实现护栏。

## 3. Baseline Conclusion

当前代表路径绿色，可以进入 4D-03E 服务端实现；迁移后必须至少复跑上述 focused / adjacent filters、backend full 和 `git diff --check`。本基线不覆盖完整待命 / reaction lifecycle、`HIDE_CARD` payment resource action、`PAY_COST` pending trigger payment、完整 `[A]` / `[C]` resource skills、所有替代 / 额外 / 可选费用窗口、LayerEngine 或 full-card official matrix。
