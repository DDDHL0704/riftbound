# Stage 4D-03G Payment Engine Battlefield Held Resource Audit

日期：2026-05-13
结论：**4D-03G FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03G 的 battlefield held score payment resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_HANDOFF.md` 的最小推进要求，让 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 可在当前 power 不足时用合法 `RECYCLE_RUNE:*` resource action 补足 4 点 power，再通过 shared `PaymentPlan` / commit 扣费；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`
- Focused regression：22/22 passed
- Adjacent regression：224/224 passed
- Backend full：3809/3809 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `DECLARE_BATTLE` 的 minimal battle guard 现在允许 `COMBAT_ASSIGNMENT` 搭配必要的 `RECYCLE_RUNE:*` payment resource action，但只在 held-score 战场来源和支付玩家资源合法时通过。
- `TryResolveBattlefieldHeldPayPowerScoreTrigger` 复用既有 recycle-rune payment helper，在提交 `COST_PAID` 前先应用合法回收，生成同一 `paymentId` / `paymentWindow` 下的 `RUNE_RECYCLED` 与 `POWER_GAINED`。
- `PaymentCostRules.PaymentPlan` 记录 `paymentResourceActionIds`，`COST_PAID` payload 保留既有 cost metadata，并补齐 `paymentResourceActions`、`recycledRuneObjectIds` 与 remaining pool metadata。
- 新增 no-mutation tests 覆盖不必要回收、错玩家符文、缺少 `cardNo` 的符文和重复资源动作。
- 现有 mana-only `TRIGGER_PAYMENT` 代表路径保持不变；本切片未修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` resource skill model、concrete trigger payment resource action、`LEGEND_ACT` resource action、所有 keyword / replacement / alternative / extra / optional cost 窗口仍未统一。
- 4D-03G 只处理 battlefield held score 代表支付路径，不冻结长期 `DECLARE_BATTLE` optional cost 或 payment UI 契约。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续扩展 typed payment engine 到 concrete trigger payment resource action、完整 `[A]` / `[C]` resource skills、`LEGEND_ACT` resource action，以及 Haste / Echo / Spellshield / replacement / extra / optional cost 的全路径 prompt quote parity。
