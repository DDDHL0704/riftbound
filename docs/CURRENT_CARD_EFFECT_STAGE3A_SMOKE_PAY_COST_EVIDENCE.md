# Stage 3A Smoke / PAY_COST Card Evidence Overlay

更新日期：2026-05-09

结论：本文件只服务阶段 3A：Smoke 基线、强类型复杂命令解析、`PAY_COST` 最小 runtime 切片、对战桌面外壳。它不是阶段 3 完整核心流程矩阵，不进入完整 battle / damage / order runtime，也不授权 1009 张卡 full-official 覆盖。

FAQ 来源策略：沿用阶段 2 从五份 PDF/FAQ 抽取的候选页码，不使用 `cardQaList`。

## 1. 3A 范围

3A 可覆盖：

- 官方开局 / 对战桌面 shell smoke 中卡牌可见、卡牌详情和候选显示。
- `PLAY_CARD.optionalCosts` 的强类型解析与提交，包括 `SPEND_POWER:red:2`、`SPEND_POWER:2`、`RECYCLE_RUNE:<objectId>`。
- `PAY_COST` 最小 runtime：服务端候选、资源动作过滤、支付资源贡献、`COST_PAID` envelope、资源扣减、prompt stamp。

3A 不覆盖：

- 1009 张卡 full-official 实现。
- 完整 battle lifecycle / spell duel lifecycle。
- `ASSIGN_COMBAT_DAMAGE` 完整伤害分配。
- `ORDER_TRIGGERS` runtime。
- 装备 / 装配 / LayerEngine 扩张。
- 控制权改变、隐藏信息牌堆链、完整触发排序。

## 2. PAY_COST 最小 Runtime Functional Units

| FU | 代表卡 | 3A 用法 | priority | FAQ | 风险标签 | 边界 |
|---|---|---|---|---|---|---|
| `FU-b646702ec0` | `OGN·268/298` 弹幕时间 | `PAY_COST` 主代表；typed power、`SPEND_POWER` 金额、`RECYCLE_RUNE` 支付资源、`COST_PAID` envelope | P0 | `JFAQ-251023 p6` | pay-cost-min-runtime, typed-power, no-damage-assignment | 只断言费用解析/支付/stack metadata；不声明完整伤害分配或战斗结算。 |
| `FU-0ec69ae7e6` | `OGN·007/298` 炽烈符文 | 红色符文 / 基础符文资源域代表 | P0 | 无 | rune-resource-domain, non-play-domain | 只作为支付资源贡献证据；不声明全部符文卡 full-official。 |
| `FU-39041f4562` | `OGN·042/298` 翠意符文 | off-trait / payment-resource filtering fixture | P1 | 无 | payment-resource-filtering, non-play-domain | 只用于证明服务端 `paymentResourcePowerByChoice` 驱动过滤；不在 3A adjudicate 全部颜色/特性。 |
| `FU-95b4531e4e` | `SFD·125/221` 大力仙灵 | 《弹幕时间》目标 / body fixture | P1 | 无 | fixture-body, no-card-text-claim | 只作为被引用对象；不执行大力仙灵自身移动支付文本。 |

## 3. Smoke Baseline Functional Units

| FU | 代表卡 | 3A 用法 | priority | FAQ | 边界 |
|---|---|---|---|---|---|
| `FU-02075a26e3` | `ARC-003/006` 黑默丁格 | 官方开局 observed hand / 桌面 shell card candidate | P1 | `SOUL-JFAQ-260114 p11`, `SOUL-JFAQ-260114 p22` | 只做可见性/详情/候选 smoke；不执行横置技能复制。 |
| `FU-af2c43c430` | `OGN·006/298` 嚼火者手雷 | 官方开局 observed hand / 桌面 shell card candidate | P1 | 无 | 只做 smoke；不执行弃牌替代费用和清理链。 |
| `FU-441cb9fb7f` | `OGN·009/298` 海克斯射线 | 官方开局 observed hand / 桌面 shell card candidate | P1 | `BREAK-JFAQ-260416 p7/p9`, `JFAQ-251023 p7`, `SOUL-JFAQ-260114 p10` | 3A 不用它进入 spell duel / stack resolution / damage runtime。 |

## 4. 3A 测试卡组口径

PAY_COST runtime seed：

- `typed-power-payment`
- `typed-power-payment-recycle`
- `typed-power-payment-double-recycle`
- `typed-power-payment-mixed-recycle`
- `typed-power-payment-generic-mixed-recycle`

这些 seed 的卡牌结论仅限 `FU-b646702ec0` + 符文资源域 + body fixture。3A 可以检查 command/prompt/resource 证据，不把《弹幕时间》的官方伤害文本扩张到完整战斗 / 伤害分配。

官方开局 smoke：

- `ARC-003/006`
- `OGN·006/298`
- `OGN·009/298`

这些卡只作为 observed hand / desktop shell / card detail smoke，不作为出牌效果 completion。

## 5. 明确不进入 3A

| FU / 域 | 原因 |
|---|---|
| `FU-5accdd09f9` 长剑 | equipment / assemble / LayerEngine，不进入 3A。 |
| `FU-5bcc4063c2` 希维尔 | haste optional payment 与持续/清理风险，不进入 3A。 |
| `FU-00ee09c2cc` 恶意收购 | Top20 控制权改变 / 隐藏信息 / FAQ 风险，不进入 3A。 |
| `FU-441cb9fb7f` 海克斯射线的法术对决路径 | 可做 smoke card candidate，但 spell duel runtime 不进 3A。 |
| battle / battlefield contest FUs | 完整 battle lifecycle、damage assignment、control/standby cleanup 不进 3A。 |
| `ORDER_TRIGGERS` 相关 FUs | 触发排序 runtime 不进 3A。 |
| Top20 其余高风险 FUs | 保留为阶段 4+ 风险输入。 |

是否允许批量覆盖：**不允许。**
