# Stage 4D-03F Payment Engine PAY_COST Resource Audit

日期：2026-05-13
结论：**4D-03F FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03F 的 pending `PAY_COST` payment resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md` 的最小推进要求，让普通 pending `PAY_COST` 可以在同一支付窗口内先执行合法 `RECYCLE_RUNE:*` resource action，再通过 shared `PaymentPlan` / commit 扣除 typed power；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/MatchSession.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`
- Focused regression：55/55 passed
- Adjacent regression：233/233 passed
- Backend full：3804/3804 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `PendingPaymentState` 现在记录 `PaymentResourceActionIds`，并在 state normalization、snapshot pending payment view、`PAY_COST` prompt metadata 中保留 resource action 审计字段。
- `ActionPromptBuilder` 将 pending `PAY_COST` 的 spend choices 与 resource choices 分开展示：`paymentChoices` 只包含 spend choices，`paymentResourceChoices` / `paymentResourcePowerByChoice` / `availablePowerWithPaymentResources` 描述可回收符文资源动作。
- 普通 pending `PAY_COST` command path 现在区分合法 spend choices 与合法 `RECYCLE_RUNE:*` actions，先事务化应用 `ApplyRecycleRunePaymentResourceActions`，再调用 `PaymentCostRules.TryCommitPayment`。
- `COST_PAID` 仍保留 `mana`、`power`、`powerByTrait`、`paymentChoiceIds`，并通过 plan-driven payload 补齐 `paymentResourceActions`、`legalPaymentChoiceIds`、remaining pool metadata 与 `recycledRuneObjectIds`。
- 现有 `TRIGGER_PAYMENT` mana-only 代表路径仍走原有 single-choice `SPEND_MANA:1` / `DECLINE` 分支；本切片未把任何具体 trigger payment 改造成 power-cost trigger。
- 本切片未修改前端、卡牌矩阵、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` resource skill model、concrete trigger payment power-cost representative、`LEGEND_ACT` / battlefield held score resource action、所有 `ACTIVATE_ABILITY` / keyword / replacement / alternative / extra / optional cost 窗口仍未统一。
- 4D-03F 只处理普通 pending `PAY_COST` resource action foundation，不冻结长期 PAY_COST DTO / UX 契约。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续扩展 typed payment engine 到 concrete trigger payment resource action、完整 `[A]` / `[C]` resource skills、`LEGEND_ACT` / battlefield held score resource action，以及 Haste / Echo / Spellshield / replacement / extra / optional cost 的全路径 prompt quote parity。
