# Stage 4D-03D Payment Engine Activate Resource Audit

日期：2026-05-13
结论：**4D-03D FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03D 的 `ACTIVATE_ABILITY` payment-resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_HANDOFF.md` 的最小推进要求，把 Vi / Xerath 代表性激活技能支付窗口接入 `RECYCLE_RUNE:*` payment resource action 的 quote / authorize / commit / audit 口径；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`
- Focused regression：84/84 passed
- Adjacent regression：257/257 passed
- Backend full：3796/3796 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `ACTIVATE_ABILITY` prompt 现在在 Vi / Xerath 代表路径中暴露 `paymentResourceChoices`、`paymentResourcePowerByChoice`、`availablePower` 与 `availablePowerWithPaymentResources` 等 source requirement metadata。
- `CoreRuleEngine` 在 Vi / Xerath 激活技能路径中拆分 `RECYCLE_RUNE:*` payment resource action 与行为可选费用，拒绝无效、重复、不必要或过量资源动作，失败保持 no-mutation。
- 合法回收符文会在同一个 `ACTIVATE_ABILITY` `paymentId` 下产生 `RUNE_RECYCLED` / `POWER_GAINED`，再进入 shared `PaymentPlan` authorize / commit 和 `COST_PAID` audit payload。
- Xerath Spellshield tax 仍由当前 mana 支付；即使可回收符文补足 power，mana 不足也会拒绝且不改变 state。
- 本切片未修改 `PaymentCostRules.cs`、前端、卡牌矩阵或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` resource skill model、`PAY_COST` pending trigger payment 资源动作、`LEGEND_ACT` / battlefield held score resource action、所有 `ACTIVATE_ABILITY` / keyword / replacement / alternative / extra / optional cost 窗口仍未统一。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续扩展 typed payment engine 到 rune / legend / battlefield / keyword 全路径，优先补完整 `[A]` / `[C]` resource skills、trigger payment resource action、替代 / 加减 / 额外 / 可选费用和全路径 prompt quote parity。
