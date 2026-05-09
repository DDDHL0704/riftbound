# Stage 4C-4 Trigger Payment Evidence Overlay

日期：2026-05-10

阶段：**阶段 4C-4 / E 卡牌覆盖矩阵 overlay**

结论：**4C-4 只部分降低 `SFD·220/221`《珍宝堆》的触发支付 / 拒付 blocker；NOT READY；不授予 full-official；不进入 1009 张卡批量实现。**

本文只记录 B/A 已完成 runtime 事实在卡牌覆盖矩阵里的证据边界。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Boundary

- 卡牌快照仍使用 `data/official/card-catalog.zh-CN.json` 的 2026-04-27 固定官网数据：1009 snapshot entries / 811 functional units。
- `SFD·220/221`《珍宝堆》在矩阵中对应 `FU-4694e33f45`，不是猜测。
- 官网固定快照文本摘要：当你征服此处时，可以支付 `1`，以此打出一个休眠的“金币”装备指示物。
- 规则 / FAQ 证据入口：`JFAQ-251023` p2-p4 q2.5，触发式技能产生费用时玩家可以支付或拒付；拒付后该技能不再待处理。

## 2. 4C-4 Closed Slice

| 域 | 4C-4 已有证据 |
|---|---|
| verified card | `SFD·220/221`《珍宝堆》 |
| verified FU | `FU-4694e33f45` |
| registry effect | `BATTLEFIELD_RULE_DOMAIN` |
| trigger effect | `BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD` |
| payment window | `TRIGGER_PAYMENT` |
| command | `PAY_COST` |
| legal choices | `SPEND_MANA:1` / `DECLINE` |
| accepted path | `PAYMENT_WINDOW_OPENED -> PAY_COST(SPEND_MANA:1) -> COST_PAID -> BATTLEFIELD_TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED -> PAYMENT_WINDOW_CLOSED` |
| declined path | `PAYMENT_WINDOW_OPENED -> PAY_COST(DECLINE) -> TRIGGER_PAYMENT_DECLINED -> PAYMENT_WINDOW_CLOSED` |
| validation | wrong player / stale prompt / unknown choice / duplicate choice / pay+decline / malformed payload / insufficient mana all reject without authoritative mutation. |

## 3. Test Evidence

| 验证项 | 结果 |
|---|---|
| trigger payment focused tests | 11/11 passed |
| trigger ordering regression | 13/13 passed |
| backend full tests | 3344/3344 passed |
| frontend build | passed |
| Chrome smoke | passed |
| stage3 preflight | passed after sequential rerun; first parallel attempt hit local API port contention. |

## 4. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增：

- 顶层 `stage4CBatch4TriggerPayment`
- `functionalUnits[].stage4C4`，仅用于 `FU-4694e33f45`
- `fieldDefinitions.functionalUnits.stage4C4`
- `fieldDefinitions.stage4CBatch4TriggerPayment`
- `stage4BCardCoverageFreeze.postFreezeOverlays[]` 中的 4C-4 记录

`stage4C4` 是 overlay，不是 primary status。4B 的 `freezeStatus` / `statusFlags` 不变，`fullOfficial` 仍为 `false`。

## 5. Counts

| 项 | 数量 |
|---|---:|
| frozen snapshot entries | 1009 |
| frozen functional units | 811 |
| `stage4C4` verified FUs | 1 |
| `stage4C4` verified snapshot entries | 1 |
| cumulative trigger-payment verified FUs | 1 |
| full-official upgrades | 0 |

## 6. Verified FUs

| FU | Representative | verified by | overlay status | still blocked |
|---|---|---|---|---|
| `FU-4694e33f45` | `SFD·220/221` 珍宝堆 | 4C-4 | `TRIGGER_PAYMENT_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` | `NEEDS_ENGINE_SUPPORT` / complete PaymentEngine / other triggered-cost FUs / FAQ adjudication remain. |

## 7. Next Pressure Candidates

这些 FUs / families 只记录为候选，未被 4C-4 标为已实现：

- triggered costs：`FU-67c6b0186e`、`FU-c170628e3a`、`FU-b829fb32b9`、`FU-f18a49e06d`、`FU-05ce012700`、`FU-c027639a3c`
- payment-engine families：替代 / 额外费用、触发费用进入结算链项目、state-based cleanup 生成的支付窗口、跨触发族 FAQ 拒付语义。

## 8. Still Missing P0/P1

- 完整 PaymentEngine。
- `SFD·220/221` 之外的 trigger payment / decline / payment failure functional units。
- 完整 trigger engine 与 state-based cleanup trigger enqueue。
- FAQ adjudication 与 ruling-backed tests。
- 1009 snapshot entries / 811 functional units 的 full-official 覆盖。

是否允许批量 full-official 覆盖：**不允许。**
