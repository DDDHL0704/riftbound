# 阶段 4C-4 触发支付 / 拒付审计

日期：2026-05-10
结论：**NOT READY**

本文记录 D 对 B 阶段 4C-4 `SFD·220/221`《珍宝堆》trigger payment 小批的规则证据与 P0/P1 审计口径。D 本轮只更新文档，不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。

## 1. 本批关闭子项

4C-4 可以关闭以下 P0 子项：

- `SFD·220/221`《珍宝堆》征服触发不再自动支付并自动创建金币，而是进入服务端权威 `TRIGGER_PAYMENT` 支付窗口。
- `PAY_COST` 支持本触发窗口的两个服务端合法选项：`SPEND_MANA:1` 与 `DECLINE`。
- 支付路径会扣除 1 点法力，创建休眠“金币”装备指示物，并广播 `COST_PAID`、`BATTLEFIELD_TRIGGER_RESOLVED`、`EQUIPMENT_TOKEN_CREATED`、`PAYMENT_WINDOW_CLOSED`。
- 拒付路径会关闭窗口且不扣费、不创建指示物，并广播 `TRIGGER_PAYMENT_DECLINED`、`PAYMENT_WINDOW_CLOSED`。
- wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 都已覆盖拒绝 / no mutation。
- 前端只补事件中文 label；仍只展示服务端事件 / prompt，不计算支付合法性或触发结果。

验证记录：

- A focused trigger payment：11/11 通过。
- A trigger ordering regression：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3344/3344。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：sequential rerun passed；第一次并行启动与 smoke 抢 API 端口失败，判定为本地调度噪声。

## 2. 规则证据入口

| 规则域 | 证据入口 | 4C-4 审计口径 | 仍需补证 |
| --- | --- | --- | --- |
| Trigger payment / decline | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5；`CATALOG` SFD·220/221 | `SFD·220/221` 征服触发进入 `TRIGGER_PAYMENT`，玩家可以支付 1 或拒付；拒付关闭窗口且不结算效果 | 其他触发费用、替代/额外费用、完整 PaymentEngine、触发费用进栈语义仍需补 |
| `PAY_COST` runtime validation | `CORE-260330` p39-p42 rules 356-357；p52-p55 rules 403-405 | 本窗口 wrong player / stale prompt / malformed / insufficient / illegal choice 都不改变权威状态 | 非本窗口的费用组合、来源选择、强制额外费用、减费/增费仍需补 |
| Battlefield conquer trigger | `CORE-260330` p77-p78 rules 454-461；`CATALOG` SFD·220/221 | 征服战场后触发支付窗口出现；支付成功后创建休眠金币装备指示物 | 完整战场触发队列、控制冻结、battle cleanup 与更多战场卡仍需补 |
| Frontend authority | 阶段 4 服务端权威原则；`src/Riftbound.DevUi/src/components/match/EventLog.tsx` | 前端只补 `PAYMENT_WINDOW_OPENED` / `TRIGGER_PAYMENT_DECLINED` 中文 label，不新增本地支付裁决 | 产品级 trigger payment UX 和更多 prompt fixture 可留后续 |

## 3. 4C-4 与前序批次合并口径

现在已有以下局部 blocker reduction：

- 4C-1：`ORDER_TRIGGERS` APNAP conservative controller-block 子集。
- 4C-2：Watchful Sentinel 多真实遗言触发入队。
- 4C-3：Honest Broker 多真实遗言金币触发入队。
- 4C-4：Treasure Pile 征服触发支付 / 拒付 / 支付失败 no-mutation 代表路径。

这些证据合并后说明复杂 prompt 三族正在逐步从代表路径走向真实卡牌路径，但仍不等于完整 trigger engine、完整 PaymentEngine 或 full-official card coverage。

## 4. 仍缺 P0/P1

仍缺 P0：

- 完整 PaymentEngine。
- `SFD·220/221` 之外的 triggered-cost functional units。
- 完整 trigger engine、state-based cleanup trigger enqueue。
- FAQ regression。
- 1009 / 811 full-official 覆盖。
- 最终正式 18 步 E2E。

仍缺 P1：

- `TRIGGER_PAYMENT` 长期 DTO / 解释字段 / UX 契约冻结。
- 触发支付与 stack item / optional trigger / decline semantics 的通用化。
- 前端产品级支付窗口文案和更多 fixture 展示。

## 5. D 审计结论

4C-4 可作为 PaymentEngine / trigger payment 的第一个真实卡牌阶段性关闭点：`SFD·220/221`《珍宝堆》已从自动结算改为服务端权威可支付 / 可拒付窗口，并有 no-mutation 负例和 Hub stale prompt 证据。

这不等于完整支付系统 READY，不等于 1009 / 811 full-official，不等于最终验收版 18 步 E2E。项目仍 **NOT READY**。
