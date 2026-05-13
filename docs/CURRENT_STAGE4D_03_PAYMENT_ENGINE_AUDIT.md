# Stage 4D-03 Payment Engine Audit

日期：2026-05-13
结论：**4D-03 FOCUSED FOUNDATION ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03 的第一片 PaymentEngine foundation。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` 的最小 focused 推进要求，可以作为后续 P0-005 扩展的 shared payment plan / commit 基座；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/PaymentCostRules.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- 新增测试：
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`
- Focused regression：56/56 passed
- Adjacent regression：245/245 passed
- Backend full：3791/3791 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `PaymentCostRules` 新增 `PaymentPlan`、`AuthorizePayment`、`TryCommitPayment` 与 plan-driven `BuildCostPaidPayload`，把 mana、generic power、typed power、experience、optional / extra cost labels、payment resource actions、legal choices 和 audit metadata 放进同一个可审计 envelope。
- `ResolvePlayCard` 现在先构建 `PaymentPlan`，再在本地副本上应用 `RECYCLE_RUNE:*` payment resource action，最后通过 shared commit 扣除 rune pool / experience。若后续 typed cost 不足，返回原始 state，新增 hash 测试覆盖 hand/base/runeDeck/runePool/stack no-mutation。
- `ResolvePayCost` 与当前触发费用代表路径改为通过 shared payment plan / commit，`COST_PAID` 事件继续保留兼容字段，并补充 `paymentWindow`、cost、remaining pool、reason/source 等审计 metadata。
- `ResolveAssembleEquipment` 作为非出牌代表路径改为使用同一 payment plan / authorization / commit helper，保留 typed-power 与 experience 扣费语义。
- 本切片未修改前端、卡牌矩阵或 READY 结论；未触碰未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：`[A]` / 支付步骤中的 `[C]` 资源技能、所有 Haste / Echo / Spellshield 分支、replacement / optional / extra cost 全矩阵、所有非出牌窗口、完整 prompt/command quote parity 与 trigger context de-stringification 仍需后续切片。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 4D-03 扩展应在本 foundation 上继续迁移更多支付窗口，而不是回到 resolver 内复制校验/扣费逻辑。优先顺序建议：`ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield held score 的 plan 化、prompt sourceRequirements 与 command commit 的同源 quote、trigger payment context typed metadata、replacement / optional / extra cost 的事务化回滚。
