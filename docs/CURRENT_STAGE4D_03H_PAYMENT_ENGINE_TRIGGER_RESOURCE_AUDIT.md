# Stage 4D-03H Payment Engine Trigger Resource Audit

日期：2026-05-13
结论：**4D-03H FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03H 的 trigger payment resource focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_HANDOFF.md` 的最小推进要求，让 `SFD·180/221` / `SFD·180a/221` 菲奥娜在己方单位从非强力变为强力后打开黄色 `TRIGGER_PAYMENT`，并允许用必要的 `RECYCLE_RUNE:*` resource action 补足黄色符能；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`
- Focused regression：69/69 passed
- Adjacent regression：242/242 passed
- Backend full：3818/3818 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- Fiora trigger window 只从可验证的 before/after power transition 打开：`BOON_GRANTED` / `POWER_MODIFIED_UNTIL_END_OF_TURN` 事件必须从 `< 5` 变为 `>= 5`。
- `PendingPaymentState.Reason` 保留 source Fiora 与 target unit context，`PAY_COST` 时重新校验 source、target、控制者、公开正面场上状态和 target 仍为强力。
- `TRIGGER_PAYMENT` 现在支持一个 spend choice 搭配合法 `RECYCLE_RUNE:*` payment resource action；mana-only trigger payment 代表路径保持兼容。
- Fiora 支付复用既有 recycle-rune payment helper 与 shared `PaymentPlan` / `TryCommitPayment`，在同一个 `paymentId` / `TRIGGER_PAYMENT` window 下记录 `RUNE_RECYCLED`、`POWER_GAINED`、`COST_PAID`、`TRIGGER_RESOLVED`、`UNIT_READIED` 与 `PAYMENT_WINDOW_CLOSED`。
- 新增 no-mutation tests 覆盖重复 resource action、不必要回收、source stale 与 target stale。
- 本切片未修改前端、卡牌矩阵、`PaymentCostRules.cs`、`MatchSession.cs` 或未跟踪的 `riftbound-dotnet.sln`。

## 3. Remaining No-Ready Items

- P0-005 仍未 full-official resolved：完整 `[A]` / `[C]` resource skill model、`LEGEND_ACT` resource action、reaction payment windows、所有 keyword / replacement / alternative / extra / optional cost 窗口仍未统一。
- 4D-03H 只处理 SFD Fiora 的 concrete trigger payment resource representative，不冻结完整 trigger engine、trigger batching、多源排序或完整 optional trigger prompt 契约。
- P0-002 / P0-003 / P0-004 的 full-official board / cleanup / spell-duel / battle lifecycle 残余仍未关闭。
- P1 LayerEngine、关键词 full-pass、1009/811 full-official matrix、最终 Chrome / hidden-info / replay-hash audit 仍未收口。

## 4. Next Step

后续 P0-005 应继续扩展 typed payment engine 到完整 `[A]` / `[C]` resource skills、`LEGEND_ACT` resource action、reaction payment windows，以及 Haste / Echo / Spellshield / replacement / extra / optional cost 的全路径 prompt quote parity。
