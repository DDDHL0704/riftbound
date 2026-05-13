# Stage 4D-03B Payment Engine Non-Play Audit

日期：2026-05-13
结论：**4D-03B FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03B 的 non-play payment window focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` 的最小推进要求，把 Vi / Xerath `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 代表窗口接入 shared `PaymentPlan` / `TryCommitPayment` 语义；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`
- Focused regression：18/18 passed
- Adjacent regression：318/318 passed
- Backend full：3791/3791 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `ResolveActivateAbility` 的 Vi 代表路径不再直接 open-code `CanPayRuneCosts` / `PayRuneCosts`，而是构建 `ACTIVATE_ABILITY` payment plan 并通过 shared commit 扣资源。
- `ResolveXerathDamageAbility` 把 Spellshield tax mana 与技能 power 放进同一个 `ACTIVATE_ABILITY` plan envelope，保留 `spellshieldTaxMana` / `spellshieldTaxTargetObjectIds` 兼容字段。
- `ResolveLegendAct` 通过 `AuthorizePayment` / `TryCommitPayment` 统一处理 mana 与 experience cost，保留 insufficient mana / insufficient experience 的既有 no-mutation rejection。
- `TryResolveBattlefieldHeldPayPowerScoreTrigger` 使用 `BATTLEFIELD_HELD` payment plan commit 支付 4 power，并在 `COST_PAID` payload 中输出 payment id/window、source、total cost 与 remaining pool metadata。
- 新增/强化 fixture 断言锁住 Vi、Xerath、Legend Act、battlefield held score 的 plan metadata envelope。
- 本切片未修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` 资源技能、Haste / Echo / Spellshield 全窗口、替代 / 额外 / 可选费用、费用目标选择、replacement/prevention 交互和全部 prompt quote parity 仍未完成。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续从代表路径扩展到 full PaymentEngine breadth，优先处理 `[A]` / `[C]` resource skills、Haste / Echo / Spellshield 全窗口、替代 / 额外 / 可选费用与 prompt sourceRequirements / command commit 的 quote parity。
