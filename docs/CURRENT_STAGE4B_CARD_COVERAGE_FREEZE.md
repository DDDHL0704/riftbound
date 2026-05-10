# Stage 4B Card Coverage Freeze

日期：2026-05-09；4C-1 / 4C-2 / 4C-3 overlay 追加：2026-05-09；4C-4 / 4C-5 / 4C-6 / 4C-7 / 4C-8 / 4C-9 / 4C-10 / 4C-11 / 4C-12 / 4C-13 / 4C-14 / 4C-15A / 4C-15B / 4C-16 / 4C-17 / 4C-18 overlay 追加：2026-05-10

阶段：**阶段 4B / E 卡牌覆盖矩阵冻结**

结论：**4B freeze 完成；4C-1 / 4C-2 / 4C-3 / 4C-4 / 4C-5 / 4C-6 / 4C-7 / 4C-8 / 4C-9 / 4C-10 / 4C-11 / 4C-12 / 4C-13 / 4C-14 / 4C-15A / 4C-15B / 4C-16 / 4C-17 / 4C-18 post-freeze overlay 已记录；NOT READY；不允许 1009 张卡批量实现。**

本文冻结卡牌覆盖矩阵、official text / FAQ evidence、functional units、测试优先级和阶段 4 批量顺序。4C-1 只在冻结矩阵上追加 APNAP `ORDER_TRIGGERS` / battle initial stack / hidden trigger metadata redaction overlay；4C-2 / 4C-3 只追加 Watchful Sentinel 与 Honest Broker real trigger enqueue overlay；4C-4 只追加 Treasure Pile trigger payment overlay；4C-5 / 4C-6 只追加 visible Watchful Sentinel / Honest Broker state-based cleanup trigger enqueue overlay；4C-7 / 4C-8 只追加 visible Scouting Warhawk explicit destroy / state-based cleanup trigger enqueue overlay；4C-9 只追加 visible Sad/Loyal Poro conditional state-based cleanup trigger enqueue overlay；4C-10 只追加 visible Unsung Hero powerful state-based cleanup trigger enqueue overlay；4C-11 只追加 visible surviving friendly Ghostly Centaur friendly-destroyed cleanup trigger enqueue overlay；4C-12 只追加 visible surviving friendly Resonant Soul first-friendly-destroyed cleanup trigger enqueue overlay；4C-13 只追加 Ghostly Centaur / Resonant Soul true stack destruction route migration overlay；4C-14 只追加 Savage Jawfish true stack / cleanup friendly-destroyed experience trigger enqueue overlay；4C-15A 只追加 Minion token family model / infrastructure overlay；4C-15B 只追加 Viktor destroyed non-Minion trigger enqueue representative baseline overlay；4C-16 / 4C-17 只追加 Mechanical Trickster / Ironclad Vanguard true stack last-breath trigger enqueue representative baseline overlay；4C-18 只追加这两个 FU 的 state-based cleanup last-breath trigger enqueue representative baseline overlay。E 不修改服务端/前端代码，不修改 A checkpoint，不触碰 `riftbound-dotnet.sln`。

## 1. Source Snapshot

本轮只读取固定官网快照：

- `data/official/card-catalog.zh-CN.json`
- `fetchedAt = 2026-04-27`
- `total = 1009`
- `.cards.length = 1009`
- unique `id` = 1009
- unique exact collector id / `cardNo` = 1009

未实时抓取官网。

## 2. Frozen Identity Rules

| 项 | 冻结口径 |
|---|---|
| `cardId` | 官方 `.cards[].id`，每个 snapshot entry 唯一。 |
| `collectorId` | 官方精确 `cardNo`；不规范化 `·`、`-`、lowercase suffix、`*`、`·P`、token collector。 |
| card entry | 官方 `.cards[]` 的一行；1009/1009 全部计入。 |
| functional unit | 行为等价 oracle 分组；当前冻结为 811 个。 |
| oracle/effectId | 当前实现映射中的 `effectKinds`；冻结为 807 个 unique ids。 |
| shared oracle | 多个 card entries 共用一个 functional unit / effect implementation；不减少 1009 card entry 计数。 |

token、rune、battlefield、promo、异画/后缀均计入 snapshot entries：

| 类别 | 数量 |
|---|---:|
| token entries | 13 |
| rune entries | 48 |
| battlefield entries | 59 |
| promo `·P` entries | 4 |
| `*` variant entries | 36 |
| lowercase suffix / alternate-art entries | 100 |
| exact collector ids | 1009 |
| base collector ids | 873 |
| base collector duplicate groups | 127 |
| base collector duplicate entries | 263 |

## 3. Matrix Fields

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已冻结以下 4B 字段：

| 区域 | 字段 |
|---|---|
| top-level | `stage4BCardCoverageFreeze` |
| snapshot entry | `stage4B.collectorId`, `stage4B.functionalUnitId`, `stage4B.freezeStatus`, `stage4B.statusFlags`, `stage4B.oracleEffectIds`, `stage4B.rulesRefs`, `stage4B.faqRefs`, `stage4B.automatedTestStatus` |
| functional unit | `stage4B.freezeStatus`, `stage4B.statusFlags`, `stage4B.effectImplementation`, `stage4B.evidence`, `stage4B.automatedTests`, `stage4B.fullOfficialBlockers` |

这使矩阵可以从 `cardId -> functionalUnitId -> oracle/effectIds -> rules / FAQ evidence -> automated tests -> status` 解释每张卡的冻结状态。

## 4. Status Counts

Primary status counts by functional unit：

| status | FUs |
|---|---:|
| `IMPLEMENTED_TESTED` | 50 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 102 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Primary status counts by snapshot entry：

| status | entries |
|---|---:|
| `IMPLEMENTED_TESTED` | 77 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 273 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Status flags are non-exclusive. A FU can be `IMPLEMENTED_TESTED` and still carry `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW`; this is representative evidence, not full-official.

## 5. Uncovered Summary

4B grants zero full-official coverage:

- full-official functional units = 0
- full-official snapshot entries = 0
- `stage4BCardCoverageFreeze.uncoveredSummary.fullOfficialUncoveredFunctionalUnitIds` contains 811/811 FUs.

Top uncovered / full-official blockers:

| FU | Representative | status | reason |
|---|---|---|---|
| `FU-fb79eea7fc` | `OGN·077/298` 中娅沙漏 | `NEEDS_FAQ_REVIEW` | FAQ + cleanup/replacement + payment + hidden/zone risk. |
| `FU-2653af0380` | `OGN·242/298` 海兽钓钩 | `NEEDS_FAQ_REVIEW` | FAQ + layer/payment/hidden/zone risk. |
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | `IMPLEMENTED_TESTED` | Representative evidence exists, but engine + FAQ blockers remain. |
| `FU-964b214448` | `SFD·020/221` 德莱文 | `IMPLEMENTED_TESTED` | Representative evidence exists, but engine + FAQ blockers remain. |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | `IMPLEMENTED_TESTED` | Combat damage / movement / FAQ blockers remain. |
| `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | `IMPLEMENTED_TESTED` | spellshield / stun / spell duel blockers remain. |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | `IMPLEMENTED_TESTED` | attack trigger / hidden info / ordering blockers remain. |
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | `IMPLEMENTED_TESTED` | battlefield / scoring / FAQ blockers remain. |

Full uncovered lists are in:

- `stage4BCardCoverageFreeze.uncoveredSummary.blockingFunctionalUnitIdsByPrimaryStatus`
- `stage4BCardCoverageFreeze.uncoveredSummary.blockingFunctionalUnitIdsByFlag`
- `stage4BCardCoverageFreeze.uncoveredSummary.fullOfficialUncoveredFunctionalUnitIds`

## 6. Top Risk Summary

Top risk remains the Stage 2 Top20 with 4B statuses overlaid:

| Rank | FU | Representative | 4B primary status |
|---:|---|---|---|
| 1 | `FU-fb79eea7fc` | `OGN·077/298` 中娅沙漏 | `NEEDS_FAQ_REVIEW` |
| 2 | `FU-2653af0380` | `OGN·242/298` 海兽钓钩 | `NEEDS_FAQ_REVIEW` |
| 3 | `FU-104211dbbc` | `SFD·148/221` 德莱文 | `IMPLEMENTED_TESTED` |
| 4 | `FU-964b214448` | `SFD·020/221` 德莱文 | `IMPLEMENTED_TESTED` |
| 5 | `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | `IMPLEMENTED_TESTED` |
| 6 | `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | `IMPLEMENTED_TESTED` |
| 7 | `FU-422b450261` | `SFD·170/221` 雷克塞 | `IMPLEMENTED_TESTED` |
| 8 | `FU-05ce012700` | `SFD·218/221` 沉没神庙 | `IMPLEMENTED_TESTED` |
| 9 | `FU-1945f6918c` | `SFD·029/221` 雷克塞 | `IMPLEMENTED_TESTED` |
| 10 | `FU-813144e7d4` | `OGN·168/298` 战或逃 | `IMPLEMENTED_TESTED` |

## 7. 4C Batch Order Suggestion

4B freeze 当时不进入 4C；在 4C-1 overlay 追加后，后续批量顺序建议更新为：

1. P0/P1 engine support blockers：先收完整 trigger engine、full damage assignment、PaymentEngine、battle/spell duel lifecycle、LayerEngine/replacement/prevention、hidden info、control/zone movement。
2. FAQ adjudication and ruling-backed tests：把 PDF/FAQ 候选判定为适用 / 不适用 / 通用规则，再补 FAQ tests。
3. Reusable oracle/effectId clusters：先锁 2+ card entries 的 shared oracle 映射，确认所有 member `cardId/cardNo`。
4. Implemented but untested direct FUs：补简单 direct FU 的正/反测试。
5. Representative tested FUs upgrade review：在 blocker 清零后，逐 FU / card entry 审核是否可升 full-official。

## 8. Post-Freeze 4C-1 Overlay

4C-1 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过 full test 的局部 runtime 证据：

| 项 | 4C-1 记录 |
|---|---|
| `ORDER_TRIGGERS` | 从 3D 最小 runtime window 升级为 `APNAP_CONTROLLER_BLOCKS_CONSERVATIVE` controller-block 子集。 |
| prompt metadata | `triggerIds` = raw queue order；`orderedTriggerIds` = 服务端推荐合法 APNAP resolution top-first 提交顺序。 |
| legality | `crossControllerReorderingAllowed=false`，`withinControllerReorderingAllowed=true`；非法跨控制者 block 重排零副作用失败。 |
| battle initial stack | active battle attacker / defender initial trigger 有代表测试进入 `ORDER_TRIGGERS`，再进入 stack priority。 |
| hidden metadata | 不可见 face-down standby trigger source 在 prompt / snapshot 中按 viewer 脱敏。 |
| backend validation | `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3337/3337。 |

矩阵 overlay 数字：

- `ORDER_TRIGGERS` dependency FUs：67
- `stage4C1` tagged FUs：67
- Top20 中 blocker 被部分降低的 FUs：6（`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`）
- battle initial stack pressure FUs：31
- hidden trigger metadata redaction candidates：11
- full-official upgrades：0

仍缺：完整 trigger engine、真实卡牌全触发生成、trigger payment / decline / payment failure、完整 APNAP 多玩家独立排序、FAQ adjudication、完整 battle initial stack 和 1009/811 full-official 覆盖。

下一批建议：优先做完整 trigger engine + real card-trigger enqueue，其次 trigger payment / decline，再做 FAQ adjudication + battle/damage pressure + hidden-info regression。

## 9. Post-Freeze 4C-2 Overlay

4C-2 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-2 记录 |
|---|---|
| verified FU | `FU-67568b793d` / `OGN·096/298`《警觉的哨兵》 |
| real trigger route | `UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS prompt -> StackItems -> pass priority -> TRIGGER_RESOLVED / CARD_DRAWN` |
| effect kinds | `WATCHFUL_SENTINEL_PLAY_UNIT` -> `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` |
| APNAP evidence | 默认 `orderedTriggerIds` 可直接提交 accepted；非法跨控制者排序拒绝且 no mutation。 |
| compatibility | 单个 Watchful Sentinel 仍保留旧即时结算兼容路径。 |
| validation | focused 11/11、backend full 3338/3338、frontend build passed、Chrome smoke passed、stage3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C2` verified FUs：1
- `stage4C2` verified snapshot entries：1
- next-pressure candidate FUs：24
- full-official upgrades：0

同族 last-breath / destroyed / on-play trigger / attack / defense / conquer FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、其他 last-breath 族、trigger payment / decline、state-based cleanup trigger enqueue、FAQ adjudication 和 1009/811 full-official 覆盖。

## 10. Post-Freeze 4C-3 Overlay

4C-3 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-3 记录 |
|---|---|
| verified FU | `FU-3acf92c924` / `SFD·155/221`《诚实掮客》 |
| real trigger route | `UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED` |
| effect kinds | `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT` -> `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` |
| compatibility | 单个 Watchful / Honest Broker 仍保留旧即时结算兼容；多个官方化 last-breath 触发同时产生时进入排序窗口。 |
| validation | focused 13/13、backend full 3339/3339、frontend build passed、Chrome smoke passed、stage3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C3` verified FUs：1
- `stage4C3` verified snapshot entries：1
- cumulative real-trigger enqueue verified FUs：2
- cumulative real-trigger enqueue verified snapshot entries：2
- next-pressure candidate FUs：23
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / on-play trigger / attack / defense / conquer FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed 族、state-based cleanup trigger enqueue、trigger payment / decline、FAQ adjudication 和 1009/811 full-official 覆盖。

## 11. Post-Freeze 4C-4 Overlay

4C-4 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-4 记录 |
|---|---|
| verified FU | `FU-4694e33f45` / `SFD·220/221`《珍宝堆》 |
| trigger payment route | `BATTLEFIELD_CONQUERED -> PAYMENT_WINDOW_OPENED -> PAY_COST(SPEND_MANA:1 or DECLINE) -> PAYMENT_WINDOW_CLOSED` |
| accepted path | `COST_PAID -> BATTLEFIELD_TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED` |
| declined path | `TRIGGER_PAYMENT_DECLINED`，不扣费、不创建指示物。 |
| validation | wrong player / stale prompt / unknown choice / duplicate choice / pay+decline / malformed payload / insufficient mana 拒绝且 no mutation。 |
| validation commands | focused 11/11、trigger regression 13/13、backend full 3344/3344、frontend build passed、Chrome smoke passed、stage3 preflight passed after sequential rerun。 |

矩阵 overlay 数字：

- `stage4C4` verified FUs：1
- `stage4C4` verified snapshot entries：1
- cumulative trigger-payment verified FUs：1
- full-official upgrades：0

其他 triggered-cost / PaymentEngine FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 PaymentEngine、其他 triggered-cost functional units、state-based cleanup trigger payment、FAQ adjudication 和 1009/811 full-official 覆盖。

## 12. Post-Freeze 4C-5 Overlay

4C-5 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-5 记录 |
|---|---|
| verified FU | `FU-67568b793d` / `OGN·096/298`《警觉的哨兵》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall damage -> state-based cleanup LETHAL_DAMAGE -> visible Watchful Sentinel WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN` |
| visibility guard | hidden / face-down / standby Watchful Sentinel 不入队，避免 trigger metadata 泄漏。 |
| overlay status | `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 4/4、backend full 3346/3346、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C5` verified FUs：1
- `stage4C5` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative state-based cleanup trigger enqueue verified FUs：1
- next-pressure candidate FUs：12
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin trigger FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、visible Watchful cleanup slice 之外的 last-breath / destroyed / friendly-destroyed functional units、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 13. Post-Freeze 4C-6 Overlay

4C-6 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-6 记录 |
|---|---|
| verified FU | `FU-3acf92c924` / `SFD·155/221`《诚实掮客》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall damage -> state-based cleanup LETHAL_DAMAGE -> visible Honest Broker HONEST_BROKER_LAST_BREATH_CREATE_GOLD -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED` |
| visibility guard | hidden / face-down / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏。 |
| overlay status | `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 6/6、backend full 3348/3348、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C6` verified FUs：1
- `stage4C6` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative state-based cleanup trigger enqueue verified FUs：2
- next-pressure candidate FUs：16
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin trigger FUs 只记录为 next-pressure candidates，不标为已实现。4C-3 Honest Broker real enqueue overlay 保留不回退。

仍缺：完整 trigger engine、visible Watchful / Honest cleanup slices 之外的 last-breath / destroyed / friendly-destroyed functional units、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 14. Post-Freeze 4C-7 Overlay

4C-7 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-7 记录 |
|---|---|
| verified FU | `FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》 |
| supporting source | `FU-a9dc3495e1` / `OGN·256/298`《妖异狐火》 |
| explicit destroy route | `UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> RUNES_CALLED` |
| visibility guard | hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`。 |
| overlay status | `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| compatibility | single-trigger compatibility 保留；没有协议 / 前端变化。 |
| validation | focused RealTriggerQueue 9/9、backend full 3350/3350、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C7` verified FUs：1
- `stage4C7` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：3
- cumulative state-based cleanup trigger enqueue verified FUs：2
- next-pressure candidate FUs：15
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin trigger FUs 只记录为 next-pressure candidates，不标为已实现。Scouting Warhawk state-based cleanup enqueue 仍需后续单独验证。

仍缺：完整 trigger engine、Scouting Warhawk state-based cleanup enqueue、其他 last-breath / destroyed / friendly-destroyed functional units、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 15. Post-Freeze 4C-8 Overlay

4C-8 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-8 记录 |
|---|---|
| verified FU | `FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> RUNES_CALLED` |
| visibility guard | hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`。 |
| overlay status | `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| compatibility | 4C-7 explicit destroy overlay 保留；本批是同一 FU 的 cleanup overlay。 |
| validation | focused RealTriggerQueue 11/11、backend full 3352/3352、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C8` verified FUs：1
- `stage4C8` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：3
- cumulative state-based cleanup trigger enqueue verified FUs：3
- next-pressure candidate FUs：15
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin trigger FUs 只记录为 next-pressure candidates，不标为已实现。4C-7 Scouting Warhawk explicit destroy overlay 保留不回退。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed functional units、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 16. Post-Freeze 4C-9 Overlay

4C-9 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-9 记录 |
|---|---|
| verified FUs | `FU-f8bfd5c6f9` / `SFD·036/221`《哀哀魄罗》；`FU-938b749c23` / `UNL-221/219`《哀哀魄罗》；`FU-0415e3b46d` / `UNL-156/219`《忠忠魄罗》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Poro condition -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN` |
| condition guard | Sad Poro：同位置无其他友方正面非待命单位；Loyal Poro：同位置有其他友方正面非待命单位，且该其他友方不在本轮 cleanup removal set。 |
| visibility guard | hidden / face-down / standby Poro 不入队、不泄漏、不抽牌。 |
| overlay status | `CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| non-full-official guard | 同时死亡落单判定仍未 full-official；本批只做 guarded visible cleanup slice。 |
| validation | focused RealTriggerQueue 21/21、backend full 3358/3358、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C9` verified FUs：3
- `stage4C9` verified snapshot entries：3
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：6
- cumulative state-based cleanup trigger enqueue verified FUs：6
- next-pressure candidate FUs：12
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin / simultaneous-condition FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed functional units、同时死亡落单判定 full-official、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 17. Post-Freeze 4C-10 Overlay

4C-10 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-10 记录 |
|---|---|
| verified FU | `FU-1701d1d89a` / `SFD·167/221`《无名英雄》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Unsung Hero CardObjectState.Power >= 5 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN x2` |
| condition guard | `Power < 5` 不入队、不抽牌。 |
| visibility guard | hidden / face-down / standby Unsung Hero 不入队、不泄漏、不抽牌。 |
| overlay status | `POWERFUL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| non-full-official guard | 只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。 |
| validation | focused RealTriggerQueue 21/21、backend full 3361/3361、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。 |

矩阵 overlay 数字：

- `stage4C10` verified FUs：1
- `stage4C10` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：7
- cumulative state-based cleanup trigger enqueue verified FUs：7
- next-pressure candidate FUs：11
- full-official upgrades：0

同族 last-breath / destroyed / friendly-destroyed / hidden-origin / effective-power FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、LayerEngine / effective power / temporary modifier powerful adjudication、battlefield objectLocation 全矩阵、explicit destroy migration、其他 last-breath / destroyed / friendly-destroyed functional units、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 18. Post-Freeze 4C-11 Overlay

4C-11 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-11 记录 |
|---|---|
| verified FU | `FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> visible surviving friendly Ghostly source -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> POWER_MODIFIED_UNTIL_END_OF_TURN +2` |
| source guards | hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不加战力；source 也在 cleanup removal set 时不入队。 |
| simultaneity guard | same source 同一轮 cleanup pass 中多个友方同时死亡保守封顶为 1 个 trigger；不是 full-official。 |
| non-covered FUs | 不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent。 |
| compatibility | true stack destruction immediate P79 compatibility 保留，本批不迁移。 |
| overlay status | `FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 23/23、backend full 3364/3364、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。 |

矩阵 overlay 数字：

- `stage4C11` verified FUs：1
- `stage4C11` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：8
- cumulative state-based cleanup trigger enqueue verified FUs：8
- next-pressure candidate FUs：10
- full-official upgrades：0

同族 destroyed / friendly-destroyed / complex last-breath / hidden-origin FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent、same-source 多友方同时死亡 full-official、true stack destruction queued migration、LayerEngine / temporary power duration cleanup matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 19. Post-Freeze 4C-12 Overlay

4C-12 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-12 记录 |
|---|---|
| verified FU | `FU-c146331876` / `OGN·118/298`《残响之魂》 |
| supporting source | `FU-56d6b01aa1` / `OGN·029/298`《星落》 |
| state cleanup route | `Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> visible surviving friendly Resonant source and owner not already destroyed this turn -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN 1` |
| source guards | hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不抽牌；source 也在 cleanup removal set 时不入队。 |
| owner guard | owner already in `DestroyedUnitOwnerIdsThisTurn` no enqueue/no draw。 |
| simultaneity guard | per owner per cleanup pass uses first destroyed unit only；simultaneous multiple units 不是 full-official。 |
| non-covered FUs | 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。 |
| compatibility | true stack destruction immediate P79 compatibility 保留，本批不迁移。 |
| overlay status | `FIRST_FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 27/27、backend full 3368/3368、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。 |

矩阵 overlay 数字：

- `stage4C12` verified FUs：1
- `stage4C12` verified snapshot entries：1
- supporting source snapshot entries：1
- cumulative real-trigger enqueue verified FUs：9
- cumulative state-based cleanup trigger enqueue verified FUs：9
- next-pressure candidate FUs：9
- full-official upgrades：0

同族 destroyed / friendly-destroyed / complex last-breath / hidden-origin FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、Viktor / Kogmaw / Karthus / Undercover Agent、simultaneous multiple units first-only full-official、true stack destruction queued migration、per-turn destroyed owner memory full reset matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 20. Post-Freeze 4C-13 Overlay

4C-13 不改变 4B frozen counts、primary status counts 或 full-official 口径；它是 route migration，不新增 unique FU coverage：

| 项 | 4C-13 记录 |
|---|---|
| route-upgraded FUs | `FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》；`FU-c146331876` / `OGN·118/298`《残响之魂》 |
| route | `true stack destruction non-cleanup UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED` |
| result | Ghostly Centaur：`POWER_MODIFIED_UNTIL_END_OF_TURN +2`；Resonant Soul：`CARD_DRAWN 1` |
| cleanup interaction | cleanup path 仍由 4C-11 / 4C-12 覆盖，并从 old stack helper 排除以避免 duplicate enqueue。 |
| P79 compatibility | old P79 immediate compatibility 已移除 / 迁移；P79 现在断言 queue / priority semantics。 |
| visibility guard | hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不生效。 |
| non-covered FUs | 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent；same-source multiple simultaneous deaths full matrix 仍未覆盖。 |
| overlay status | `TRUE_STACK_DESTRUCTION_TRIGGER_QUEUE_MIGRATED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 30/30、backend full 3370/3370、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。 |

矩阵 overlay 数字：

- `stage4C13RouteUpgradedFunctionalUnits`：2
- `stage4C13RouteUpgradedSnapshotEntries`：2
- unique new FU coverage：0
- cumulative real-trigger enqueue verified FUs：9
- cumulative state-based cleanup trigger enqueue verified FUs：9
- next-pressure candidate FUs：9
- full-official upgrades：0

同族 destroyed / friendly-destroyed / complex last-breath / hidden-origin FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、Viktor / Kogmaw / Karthus / Undercover Agent、same-source / same-owner multiple simultaneous deaths full-official、per-turn destroyed owner memory full reset matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 21. Post-Freeze 4C-14 Overlay

4C-14 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-14 记录 |
|---|---|
| verified FU | `FU-bd94334cc5` / `UNL-129/219` Savage Jawfish / 《凶残颚鱼》 |
| route | true stack `UNIT_DESTROYED` 与 Starfall lethal cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED +1` |
| source guard | source must remain field, face-up, non-standby, same controller, and not destroyed/removal set。 |
| visibility guard | hidden face-down / standby / opponent-controlled source 不入队、不泄漏、不获得经验。 |
| multiplicity guard | same source same pass multi-destroy trigger multiplicity 仍为 P1/TODO，不是 full-official。 |
| non-covered FUs | 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。 |
| overlay status | `FRIENDLY_DESTROYED_STACK_AND_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | focused RealTriggerQueue 33/33、backend full 3374/3374、frontend build passed、Chrome smoke passed、Stage3 preflight passed、diff check passed。 |

矩阵 overlay 数字：

- `stage4C14` verified FUs：1
- `stage4C14` verified snapshot entries：1
- cumulative real-trigger enqueue verified FUs：10
- cumulative state-based cleanup trigger enqueue verified FUs：10
- next-pressure candidate FUs：9
- full-official upgrades：0

同族 destroyed / friendly-destroyed / complex last-breath / hidden-origin FUs 只记录为 next-pressure candidates，不标为已实现。

仍缺：完整 trigger engine、same source same pass multi-destroy trigger multiplicity、Viktor / Kogmaw / Karthus / Undercover Agent、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 22. Post-Freeze 4C-15A Overlay

4C-15A 不改变 4B frozen counts、primary status counts 或 full-official 口径；它是 model / infrastructure overlay，不实现 Viktor 本体：

| 项 | 4C-15A 记录 |
|---|---|
| stable marker | `TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily` |
| official factories | `OGN·271/298`、`OGN·272/298`、`OGN·273/298` 带 marker。 |
| shared helper | `CreateBaseUnitTokens(tokenName == "随从")` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`；Viktor legend 直接随从创建也同步。 |
| positive evidence | Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield-held minion 等随从 token 带 marker。 |
| negative evidence | 普通单位不带；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”不带。 |
| hidden info guard | hidden face-down standby opponent snapshot 不泄漏 tags / cardNo / power。 |
| Viktor boundary | `FU-b5cb36a5c9` 仍 `NEEDS_ENGINE_SUPPORT` / `fullOfficial=false`；Viktor trigger 未关闭。 |
| validation | backend full 3375/3375、`git diff --check` passed。 |

矩阵 overlay 数字：

- `stage4C15AVerifiedInfrastructure`：true
- `stage4C15AFullOfficialFunctionalUnits`：0
- `stage4C15AFullOfficialSnapshotEntries`：0
- `stage4C15AFUOverlayTags`：0
- cumulative real-trigger enqueue verified FUs：10
- cumulative state-based cleanup trigger enqueue verified FUs：10
- full-official upgrades：0

4C-15A 可降低 token subtype / family / minion-classification 前置 blocker，但不关闭 Viktor trigger，也不覆盖 Kogmaw / Karthus / Undercover Agent。

仍缺：Viktor destroyed non-minion trigger behavior、完整 trigger engine、same-source / same-pass / multi-destroy multiplicity and non-minion classification in real trigger contexts、隐藏 / face-down original visibility modeling、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 23. Post-Freeze 4C-15B Overlay

4C-15B 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-15B 记录 |
|---|---|
| verified FU | `FU-b5cb36a5c9` / Viktor |
| associated cardNos | `ARC-006/006`、`OGN·246/298`、`OGN·246a/298` |
| route | true stack `UNIT_DESTROYED` and Starfall lethal cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` 1-power Zaun minion `OGN·273/298` with `TOKEN_FAMILY:MINION` |
| destroyed target filter | unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily` |
| source guard | field、face-up、non-standby、same controller、not removal set |
| no-enqueue guards | destroyed minion target；hidden / face-down / standby / opponent source；source also dying |
| non-covered FUs | 不覆盖 Kogmaw / Karthus / Undercover Agent。 |
| validation | 5 new `RealTriggerQueueTests`；backend full 3380/3380 passed by A。 |

矩阵 overlay 数字：

- `stage4C15B` verified FUs：1
- `stage4C15B` verified snapshot entries：3
- cumulative real-trigger enqueue verified FUs：11
- cumulative state-based cleanup trigger enqueue verified FUs：11
- full-official upgrades：0

4C-15B 关闭 representative trigger enqueue baseline，但不关闭 full official trigger-count matrix 或 full trigger engine。

仍缺：full official trigger-count matrix for Viktor、完整 trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity、隐藏 / face-down original visibility modeling、Kogmaw / Karthus / Undercover Agent、FAQ adjudication / regression、正式 18-step E2E 和 1009/811 full-official 覆盖。

## 24. Post-Freeze 4C-16 Overlay

4C-16 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-16 记录 |
|---|---|
| verified FU | `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster / 《机械戏法师》 |
| trigger effect kind | `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` |
| route | true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION` |
| source guard | face-down / standby source 不入队、不泄漏 metadata、不创建 token。 |
| P79 compatibility | `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` updated to queue / priority semantics。 |
| non-covered FUs | 不覆盖 Ironclad Vanguard / Kogmaw / Karthus / Undercover Agent。 |
| overlay status | `MECHANICAL_TRICKSTER_TRUE_STACK_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | `RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`、`RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`、P79 fixture updated；backend full 3382/3382 passed by A。 |

矩阵 overlay 数字：

- `stage4C16` verified FUs：1
- `stage4C16` verified snapshot entries：1
- cumulative real-trigger enqueue verified FUs：12
- cumulative state-based cleanup trigger enqueue verified FUs：11
- full-official upgrades：0

4C-16 只覆盖 true stack route。Mechanical Trickster state-based cleanup route、full trigger engine、multi-source / simultaneous trigger multiplicity、同族复杂 last-breath FUs 均未关闭。

## 25. Post-Freeze 4C-17 Overlay

4C-17 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录 B/A 已完成并通过测试的局部 runtime 证据：

| 项 | 4C-17 记录 |
|---|---|
| verified FU | `FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard / 《铁甲先锋》 |
| trigger effect kind | `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` |
| route | true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x2` robots |
| source guard | face-down / standby source 不入队、不泄漏 metadata、不创建 token。 |
| P79 compatibility | `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` updated to queue / priority semantics。 |
| non-covered FUs | 不覆盖 Kogmaw / Karthus / Undercover Agent；不覆盖 Ironclad state-based cleanup route。 |
| overlay status | `IRONCLAD_VANGUARD_TRUE_STACK_LAST_BREATH_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL` |
| validation | `RealIroncladVanguardLastBreathTriggersOrderAndCreateRobotsThroughStack`、`RealIroncladVanguardHiddenSourcesDoNotEnqueueOrCreateRobots`、P79 fixture updated；backend full 3384/3384 passed by A。 |

矩阵 overlay 数字：

- `stage4C17` verified FUs：1
- `stage4C17` verified snapshot entries：1
- cumulative real-trigger enqueue verified FUs：13
- cumulative state-based cleanup trigger enqueue verified FUs：11
- full-official upgrades：0

4C-17 只覆盖 true stack route。Ironclad Vanguard state-based cleanup route、full trigger engine、multi-source / simultaneous trigger multiplicity、同族复杂 last-breath FUs 均未关闭。

## 26. Post-Freeze 4C-18 Overlay

4C-18 不改变 4B frozen counts、primary status counts 或 full-official 口径，只记录局部 cleanup-route runtime 证据：

| 项 | 4C-18 记录 |
|---|---|
| verified FUs | `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster；`FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard |
| route | state-based cleanup `LETHAL_DAMAGE / UNIT_DESTROYED` -> last-breath trigger queued -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` |
| Mechanical result | `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION` |
| Ironclad result | `UNIT_TOKEN_CREATED x2` robots |
| status impact | 4B `freezeStatus` / `statusFlags` unchanged；`fullOfficial=false`；full-official upgrades = 0。 |
| non-covered FUs | 不覆盖 Kogmaw / Karthus / Undercover Agent；不创建不存在 FU。 |

矩阵 overlay 数字：

- `stage4C18` verified FUs：2
- `stage4C18` verified snapshot entries：2
- cumulative real-trigger enqueue verified FUs：13
- cumulative state-based cleanup trigger enqueue verified FUs：13
- full-official upgrades：0
- full-official still uncovered FUs：811

4C-18 只覆盖这两个 FU 的 cleanup route。完整 trigger engine、multi-source / simultaneous trigger multiplicity、同族复杂 last-breath FUs 均未关闭。

## 27. 4B / 4C-1 / 4C-2 / 4C-3 / 4C-4 / 4C-5 / 4C-6 / 4C-7 / 4C-8 / 4C-9 / 4C-10 / 4C-11 / 4C-12 / 4C-13 / 4C-14 / 4C-15A / 4C-15B / 4C-16 / 4C-17 / 4C-18 Blocker

4B freeze、4C-1 overlay、4C-2 overlay、4C-3 overlay、4C-4 overlay、4C-5 overlay、4C-6 overlay、4C-7 overlay、4C-8 overlay、4C-9 overlay、4C-10 overlay、4C-11 overlay、4C-12 overlay、4C-13 overlay、4C-14 overlay、4C-15A overlay、4C-15B overlay、4C-16 overlay、4C-17 overlay 和 4C-18 overlay 本身无文档阻断。仍阻断 READY / full-official 的事项：

- 0/811 functional units 获得 full-official。
- P0/P1 engine support 仍影响 762 FUs by status flag。
- FAQ review 仍影响 179 FUs by status flag。
- Representative automated evidence 不是 1009 张卡 full-official tests。
- 4C 后续批量实现仍需要 A 另行授权写入锁。

是否允许进入卡牌效果批量覆盖：**不允许。**
