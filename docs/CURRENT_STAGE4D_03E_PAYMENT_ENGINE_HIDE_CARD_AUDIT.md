# Stage 4D-03E Payment Engine Hide Card Audit

日期：2026-05-13
结论：**4D-03E FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03E 的 `HIDE_CARD` standby payment focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md` 的最小推进要求，把待命暗置支付窗口迁移到 shared `PaymentPlan` / authorize / commit / audit 口径；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`
- Focused regression：88/88 passed
- Adjacent regression：290/290 passed
- Backend full：3800/3800 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `ResolveHideCard` 现在为标准 `STANDBY_A`、Teemo `STANDBY_TEEMO_MANA` 和 Guerrilla Warfare `STANDBY_FREE` 构造 `PaymentCostRules.PaymentPlan`。
- 费用 authorize / commit 在任何手牌移动、对象翻面、zone 更新或事件生成之前完成；费用不足仍 rejected 且 state hash no-mutation。
- `COST_PAID` 使用 plan-driven payload helper，保留兼容键 `mana`、`power`、`optionalCosts`、`standbyHideCostWaived`、`teemoStandbyHideReplacement`，并新增/保持 `paymentWindow = HIDE_CARD`、`sourceObjectId`、`reason = STANDBY_HIDE`、base / total cost 与 remaining pool metadata。
- Bandle Tree extra standby、`CARD_HIDDEN` hidden-info payload、object location reconciliation 与 Ember Monk trigger 顺序保持兼容。
- 本切片未修改 `MatchSession.cs`、`PaymentCostRules.cs`、前端、卡牌矩阵或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` resource skill model、`PAY_COST` pending trigger payment 资源动作、`LEGEND_ACT` / battlefield held score resource action、所有 `ACTIVATE_ABILITY` / keyword / replacement / alternative / extra / optional cost 窗口仍未统一。
- `REVEAL_CARD` / 完整 standby reaction lifecycle 未在本切片实现。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续扩展 typed payment engine 到 rune / legend / battlefield / keyword 全路径，优先补完整 `[A]` / `[C]` resource skills、trigger payment resource action、`LEGEND_ACT` / battlefield held score resource action、替代 / 加减 / 额外 / 可选费用和全路径 prompt quote parity。
