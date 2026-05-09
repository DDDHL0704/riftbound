# 当前卡牌效果高风险 Top20

更新时间：2026-05-10

阶段：**阶段 4C-9 / E 卡牌覆盖矩阵 post-freeze overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文以阶段 2 风险排序为基础，并叠加阶段 3A/3B/3C/3D 的最小证据 overlay、阶段 4B 冻结状态、阶段 4C-1 APNAP `ORDER_TRIGGERS` 部分 blocker 降低、阶段 4C-2/4C-3 real trigger enqueue、阶段 4C-4 trigger payment、阶段 4C-5 Watchful state-based cleanup trigger enqueue、阶段 4C-6 Honest Broker state-based cleanup trigger enqueue、阶段 4C-7 Scouting Warhawk explicit destroy trigger enqueue、阶段 4C-8 Scouting Warhawk state-based cleanup trigger enqueue 和阶段 4C-9 Sad/Loyal Poro conditional cleanup trigger enqueue；它不是功能实现清单，也不是错误断言。排名用于告诉后续阶段先审哪里：哪些 functional unit 同时碰到 FAQ、费用、触发/替换、持续效果、战斗/法术对决、隐藏信息或非 PLAY_CARD 规则域。

## 1. 数据来源

- 官方快照：`data/official/card-catalog.zh-CN.json`，`fetchedAt = 2026-04-27`，1009 entries。
- functional unit 口径：复用仓库 `FunctionalUnitBuilder` 的签名字段，得到 811 functional units。
- 现有实现映射：只读解析 `src/Riftbound.Engine/CardBehaviorRegistry.cs` 与非 PLAY_CARD 域绑定，作为 representative route，不代表 full-official。
- FAQ/规则页码：用 `pdftotext` 对五份 PDF 逐页抽取，按卡名精确命中生成候选页码；不读取、不依赖 `cardQaList`。
- 机器可读骨架：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。

## 2. 矩阵字段

机器可读骨架已经包含以下核心字段：

| 矩阵区 | 字段 |
|---|---|
| snapshot entry | `cardId`, `cardNo`, `baseCollectorNo`, `setPrefix`, `cardName`, `cardCategoryName`, `functionalUnitId`, `functionalRepresentativeNo`, `functionalUnitSize`, `officialTextHash`, `faqRefs`, `implementationStatus`, `implementedEffectKinds`, `uncoveredEffectCategories` |
| functional unit | `functionalUnitId`, `signatureHash`, `representativeCardNo`, `cardNos`, `cardIds`, `size`, `implementation`, `faqRefs`, `rulesRefs`, `uncoveredEffectCategories`, `dependsOnP0RuleDomains`, `riskScore` |
| FAQ evidence candidate | `source`, `page`, `kind`, card-name hit via functional unit `name` |

阶段 2 统计：

| 项 | 数量 |
|---|---:|
| snapshot entries | 1009 |
| functional units | 811 |
| direct card behavior FUs | 694 |
| non PLAY_CARD domain representative FUs | 117 |
| FAQ candidate FUs | 179 |
| FAQ candidate snapshot entries | 227 |

## 3. Stage 3A Smoke / PAY_COST Holdback

阶段 3A 只服务 Smoke 基线、强类型复杂命令解析、`PAY_COST` 最小 runtime 切片与对战桌面外壳，不进入 Top20 批量实现。以下口径已同步到 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage3ASmokePayCost`：

| FU | 代表卡 | Stage 3A 处理 |
|---|---|---|
| `FU-b646702ec0` | `OGN·268/298` 弹幕时间 | 允许进入 3A，但只作为 `PAY_COST` 最小 runtime P0 代表；不扩张到完整伤害分配、战斗或法术对决。 |
| `FU-0ec69ae7e6` | `OGN·007/298` 炽烈符文 | 允许进入 3A，作为红色符文 / 基础符文支付资源域代表；不是全部符文 full-official。 |
| `FU-39041f4562` | `OGN·042/298` 翠意符文 | 仅作 payment-resource filtering support；不 adjudicate 全部颜色/特性规则。 |
| `FU-95b4531e4e` | `SFD·125/221` 大力仙灵 | 仅作《弹幕时间》目标 / body fixture；不执行该卡自身移动支付文本。 |
| `FU-5accdd09f9` | `SFD·022/221` 长剑 | 不进入 3A；装备、装配、LayerEngine 完整链留到后续阶段。 |
| `FU-5bcc4063c2` | `SFD·143/221` 希维尔 | 不进入 3A；haste optional payment 和持续/清理风险留到后续切片。 |
| `FU-00ee09c2cc` | `SFD·202/221` 恶意收购 | Top20 #16；控制权改变、隐藏信息、FAQ 与支付风险过高，不进入 3A。 |
| Top20 其余 FU | 见下表 | 默认阶段 4+，除非 A 明确指定单卡切片并给出写入锁。 |

3A smoke-only 候选只限：`FU-02075a26e3` 黑默丁格、`FU-af2c43c430` 嚼火者手雷、`FU-441cb9fb7f` 海克斯射线。它们只用于官方开局 / 桌面 shell 可见性和详情 smoke，不进入 spell duel / stack resolution / damage runtime。

## 4. Stage 3B Battlefield / Cleanup Overlay

阶段 3B 只服务 Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片，不进入 Top20 批量实现，也不关闭 full battle / damage assignment。以下口径已同步到 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage3BBattlefieldLifecycle`：

| FU | 代表卡 | Stage 3B 处理 |
|---|---|---|
| `FU-05ce012700` | `SFD·218/221` 沉没神庙 | 3B P0；战场对象 / 控制状态 / 征服据守证据核心，但不是所有战场 full-official。 |
| `FU-00ee09c2cc` | `SFD·202/221` 恶意收购 | 3B P0；控制权改变和控制冻结 FAQ 候选，只做 lifecycle 证据，不扩张到完整战斗或隐藏信息。 |
| `FU-813144e7d4` | `OGN·168/298` 战或逃 | 3B P0；战场单位回基地、移动后 cleanup queue 证据，不关闭完整 spell duel / targeting / payment。 |
| `FU-8dae5c40be` | `OGN·121/298` 提莫 | 3B P1；待命区隐私、失控待命清理候选，不执行完整战斗文本。 |
| `FU-e3dcc3b30f` | `OGN·199/298` 控潮者 | 3B P1；standby swap / 移动 / 隐私候选，不概括全部 standby。 |
| `FU-7f4a387b92` | `OGN·056/298` 自适应机器人 | 3B P1；征服 trigger hook 候选，不完成 boon / LayerEngine。 |
| `FU-c027639a3c` | `OGN·035/298` 薇恩 | 3B P1；征服 recall / control-zone movement 候选，不拉入完整 assault / combat damage。 |
| `FU-90673ef9fd` | `OGN·285/298` 劫掠船巷 | 3B P1；战场 FAQ、control freeze、cleanup 候选。 |
| `FU-6c99fc0e2e` | `OGN·277/298` 后巷酒吧 | 3B P1；cleanup / control battlefield-domain 支撑。 |
| `FU-d18ac7cbec` | `UNL-209/219` 暮色玫瑰实验室 | 3B P1；cleanup / control / visibility 支撑。 |

3B 依赖统计：649 个 lifecycle/cleanup 广义候选、520 个 3B core 候选、287 个 battle/spell-duel 候选、282 个 cleanup 候选、286 个 control/zone/movement 候选、54 个 battlefield rule-domain FUs、12 个 standby 命名或 effect-kind 候选、44 个 legend action domain FUs。这些统计是风险范围，不是实施授权。

## 5. Stage 3C Spell Duel / Battle / Damage Overlay

阶段 3C 只服务 spell duel / battle / `ASSIGN_COMBAT_DAMAGE` 最小证据切片，不进入 1009 张卡全量实现，也不关闭完整 battle runtime。以下口径已同步到 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage3CBattleDamage`：

| FU | 代表卡 | Stage 3C 处理 |
|---|---|---|
| `FU-fda6183f9d` | `OGS·007/024` 盖伦 | 3C P0；只作 declare-battle attacker/body fixture 和 damage payload guard，不宣称盖伦 full-official。 |
| `FU-6582231b22` | `UNL-036/219` 变异猫咪 | 3C P0；只作 defender/body fixture 和 damage payload guard，不扩张到全战斗模型。 |
| `FU-44f29ad8f7` | `OGN·004/298` 顺劈 | 3C P0；只作 swift spell duel / focus pass / combat trick 代表，不批量开放所有迅捷/反应卡。 |
| `FU-104211dbbc` | `SFD·148/221` 德莱文 | 3C P0；Top20 #3，高风险 battle lifecycle / FAQ 候选，只做证据边界。 |
| `FU-964b214448` | `SFD·020/221` 德莱文 | 3C P0；Top20 #4，较简单 battle body / FAQ baseline，仍非 full-official。 |
| `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | 3C P0；Top20 #5，combat damage 后续移动压力候选，不实现完整触发/移动链。 |
| `FU-422b450261` | `SFD·170/221` 雷克塞 | 3C P1；attack trigger / hidden-info / `ORDER_TRIGGERS` 压测候选，3C 不实现完整 reveal 或触发排序。 |
| `FU-1945f6918c` | `SFD·029/221` 雷克塞 | 3C P1；overwhelm / battle-damage 压力候选，不关闭关键字全族。 |
| `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | 3C P1；spellshield / stun timing 边界，不关闭 spell duel 全族。 |
| `FU-b646702ec0` | `OGN·268/298` 弹幕时间 | 3C holdback；3A 只覆盖 `PAY_COST` 最小 runtime，非战斗伤害不进入 3C battle damage assignment。 |

3C 依赖统计：287 个 battle / spell-duel / `ASSIGN_COMBAT_DAMAGE` 广义候选、7 个 spell-duel effect-kind FUs、145 个 battle / attack / combat effect-kind FUs、60 个 damage effect-kind FUs、55 个 `ORDER_TRIGGERS` 压测候选。这些统计是风险范围，不是实施授权。

后续 `ORDER_TRIGGERS` 压测候选：`FU-422b450261`、`FU-67c6b0186e`、`FU-bf81341dd2`、`FU-5cea85e7c3`、`FU-c170628e3a`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-8dae5c40be`。3C 只记录这些候选，不进入 runtime 实现。

## 6. Stage 3D Complex Prompt / Trigger Overlay

阶段 3D 建立 complex prompt 与 lifecycle dependency 索引，并对齐当时 B 已关闭的 `ORDER_TRIGGERS` 最小 runtime window：prompt、`orderedTriggerIds` command、validation、合法排序入 `StackItems`、事件日志。4C-1 已在此基础上升级为保守 APNAP controller-block 子集；仍不启动最终 18 步 E2E，不进入完整 trigger engine，也不进入 1009 张卡 full-official 覆盖。以下口径已同步到 `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 的 `stage3DComplexPromptLifecycle`：

| 依赖桶 | FU 数 | 来源阶段 | Stage 4 用途 |
|---|---:|---|---|
| `PAY_COST` | 370 | 3A | PaymentEngine、替代/额外费用、资源支付和 card text 费用窗口优先级。 |
| `ASSIGN_COMBAT_DAMAGE` | 287 | 3C | battle damage assignment、damage pool、合法分配和零副作用拒绝矩阵。 |
| `ORDER_TRIGGERS` / battle initial stack | 67 | 3D -> 4C-1 | 4C-1 已部分关闭保守 APNAP controller-block 排序；继续压测完整 trigger engine、真实卡牌触发入队、完整 APNAP 多玩家独立排序、trigger cost / decline / payment。 |
| battlefield / control / conquer | 358 | 3B | 战场、控制权、待命、征服/据守、zone movement lifecycle 压测。 |
| spell duel / battle | 288 | 3C | spell duel、battle task、attack/defense identity、combat trick 压测。 |

3D 还标注 179 个 FAQ 命中 FUs、113 个可复用 oracle / effectId implementation 候选，以及 61 个阶段 4 优先 / FAQ / 压测 / 复用候选 `functionalUnits[].stage3D` 标签。

后续 `ORDER_TRIGGERS` / battle initial stack / trigger ordering 压测首批候选：`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`、`FU-50ceb593ab`、`FU-8dae5c40be`、`FU-201e46695b`、`FU-f076dbf9ee`、`FU-f9f5c508c0`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-5164c0d190`、`FU-c027639a3c`、`FU-16d3a6dd4e`、`FU-3e9cb3904e`、`FU-7d0b8868b`、`FU-1563edad5f`、`FU-67c6b0186e`、`FU-bf81341dd2`、`FU-5cea85e7c3`、`FU-e3dcc3b30f`、`FU-7f4a387b92`。4C-1 部分 blocker 降低不等于这些 FUs 的 full-official 完成。

## 7. Stage 4B Freeze Status

阶段 4B 冻结 2026-04-27 官方快照，不实时抓官网；4B 本身不进入 4C 实现。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4BCardCoverageFreeze`、`snapshotEntries[].stage4B`、`functionalUnits[].stage4B`。

冻结统计：1009 snapshot entries、1009 unique cardIds、1009 exact collectorIds、811 functional units、807 unique oracle/effectIds。token、rune、battlefield、promo、`*` 变体、lowercase suffix / 异画均按官方 snapshot entry 计入 1009；functional unit 复用不减少 card entry。

Functional unit primary status：

| status | FUs |
|---|---:|
| `IMPLEMENTED_TESTED` | 50 |
| `IMPLEMENTED_UNTESTED` | 30 |
| `SHARED_ORACLE_IMPLEMENTATION` | 102 |
| `NEEDS_ENGINE_SUPPORT` | 501 |
| `NEEDS_FAQ_REVIEW` | 128 |
| `BLOCKED` | 0 |

Top uncovered/full-official blockers 仍从 Top20 开始：`FU-fb79eea7fc`、`FU-2653af0380`、`FU-104211dbbc`、`FU-964b214448`、`FU-2dca1ad450`、`FU-9f7cb73dc4`、`FU-422b450261`、`FU-05ce012700`、`FU-1945f6918c`、`FU-813144e7d4`。即使 primary status 是 `IMPLEMENTED_TESTED`，只要带 `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW` flag，就不能升级 full-official。

4C 批量顺序建议：4C-1 已先降低 `ORDER_TRIGGERS` 的保守 APNAP controller-block / battle initial stack 代表路径 / hidden trigger metadata redaction blocker；4C-2 / 4C-3 已验证 Watchful Sentinel 与 Honest Broker 两个 last-breath real enqueue 切片；4C-4 已验证 Treasure Pile trigger payment；4C-5 / 4C-6 已验证 visible Watchful / Honest Broker state-based cleanup trigger enqueue；4C-7 / 4C-8 已验证 Scouting Warhawk explicit destroy 与 state-based cleanup real trigger enqueue；4C-9 已验证 Sad / Loyal Poro conditional cleanup draw enqueue。下一批应扩展 Unsung Hero、Kogmaw/Karthus/Undercover Agent、destroyed / friendly-destroyed families、hidden origin visibility、simultaneous-death condition adjudication、FAQ adjudication、battle/damage 压测和 full E2E guardrails。

## 8. Stage 4C-1 Trigger Ordering Overlay

4C-1 只更新覆盖矩阵 / 风险证据，不实现卡牌效果，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch1TriggerOrdering`，并为 67 个 `ORDER_TRIGGERS` dependency FUs 增加 `functionalUnits[].stage4C1` 轻量 overlay。

4C-1 已部分降低的 blocker：

- `ORDER_TRIGGERS` APNAP conservative controller-block ordering：`triggerIds` 是 raw queue order，`orderedTriggerIds` 是服务端推荐的合法 stack-resolution top-first 提交顺序。
- `legalOrderingConstraints` 明确 `orderingPolicy=APNAP_CONTROLLER_BLOCKS_CONSERVATIVE`、`orderedTriggerIdsSemantics=STACK_RESOLUTION_ORDER_TOP_FIRST`、`controllerBlockOrder`、`legalResolutionControllerBlockOrder`、`crossControllerReorderingAllowed=false`、`withinControllerReorderingAllowed=true`。
- 跨控制者 block 非法重排零副作用失败；同控制者 block 内重排合法；合法排序进入 `StackItems` / stack priority。
- active battle attacker / defender initial trigger 有代表测试进入 `ORDER_TRIGGERS`；不可见 face-down standby trigger source 有 viewer-level prompt / snapshot 脱敏。
- A full test 已通过：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` => 3337/3337。

Top20 中 `ORDER_TRIGGERS` / battle initial stack blocker 被部分降低的 FUs：`FU-104211dbbc`、`FU-2dca1ad450`、`FU-964b214448`、`FU-05ce012700`、`FU-422b450261`、`FU-813144e7d4`。这些仍带 `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW`，不能升级 full-official。

4C 后续批量顺序建议：

1. `4C-2/4C-3` complete trigger engine + real card-trigger enqueue：已完成 Watchful Sentinel 与 Honest Broker 最小切片；仍需扩展到其他 last-breath / destroyed / conquer / attack / defense / standby trigger queue。
2. `4C-3` trigger payment / decline / payment failure：覆盖触发费用、可选/拒绝、支付失败 rollback。
3. `4C-4` FAQ adjudication for trigger / battle ordering：把 PDF/FAQ 候选判定为适用 / 不适用 / 通用规则并补 ruling-backed tests。
4. 后续 battle initial stack + `ASSIGN_COMBAT_DAMAGE` pressure matrix：扩展到完整 battle initial stack、伤害分配、预防/替代和 battle cleanup ordering。
5. `4C-6` hidden information regression pack：压测 face-down standby、reveal、随机/牌堆、viewer-specific prompt metadata。

仍缺：完整 trigger engine、Watchful / Honest / Treasure Pile 之外的真实卡牌全触发生成与支付窗口、完整 APNAP 多玩家独立排序、FAQ adjudication、1009/811 full-official 覆盖。

## 9. Stage 4C-2 Real Trigger Enqueue Overlay

4C-2 只更新覆盖矩阵 / 风险证据，不实现卡牌效果，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch2RealTriggerEnqueue`，并只为 `FU-67568b793d` 增加 `functionalUnits[].stage4C2` overlay。

4C-2 已部分降低的 blocker：

- `OGN·096/298`《警觉的哨兵》对应 `FU-67568b793d`，registry effect kind 为 `WATCHFUL_SENTINEL_PLAY_UNIT`。
- 多个 Watchful Sentinel 的真实 `UNIT_DESTROYED` 路径产生 `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`，进入 `TriggerQueue -> ORDER_TRIGGERS prompt -> StackItems -> pass priority -> TRIGGER_RESOLVED / CARD_DRAWN`。
- 单个 Watchful Sentinel 仍保留旧即时结算兼容路径。
- 跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交 accepted；非法跨控制者排序拒绝且 no mutation。
- A 验证结果：focused 11/11、backend full 3338/3338、frontend build passed、Chrome smoke passed、stage3 preflight passed。

已验证 FU：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》。overlay status：`REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`。该 FU 仍保留 `NEEDS_ENGINE_SUPPORT` / `NEEDS_FAQ_REVIEW`，不能升级 full-official。

4C-2 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- last-breath：`FU-3acf92c924`、`FU-6a52b04cb2`、`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-b829fb32b9`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-1701d1d89a`
- on-play registered trigger：`FU-d5e1143438`、`FU-bf81341dd2`、`FU-e8d8846d73`、`FU-808f8b89db`、`FU-f18a49e06d`、`FU-67c6b0186e`
- attack / defense / conquer：`FU-661793867e`、`FU-5cea85e7c3`、`FU-422b450261`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-3e9cb3904e`

后续批量顺序建议：先扩展 last-breath / destroyed-family real enqueue，再做 trigger payment / decline / payment failure，再补 state-based cleanup trigger enqueue，之后才压 attack / defense / conquer real trigger enqueue 与 FAQ adjudication。

仍缺：完整 trigger engine、其他 last-breath 族、trigger payment / decline、state-based cleanup trigger enqueue、FAQ adjudication、1009/811 full-official 覆盖。

## 10. Stage 4C-3 Honest Broker Last-Breath Enqueue Overlay

4C-3 只更新覆盖矩阵 / 风险证据，不实现卡牌效果，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch3LastBreathEnqueue`，并只为 `FU-3acf92c924` 增加 `functionalUnits[].stage4C3` overlay。

4C-3 已部分降低的 blocker：

- `SFD·155/221`《诚实掮客》对应 `FU-3acf92c924`，registry effect kind 为 `HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT`。
- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 进入真实多触发路径：`UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED`。
- 单个 Watchful / Honest Broker 仍保留旧即时结算兼容；多个官方化 last-breath 触发同时产生时进入排序窗口。
- A 验证结果：focused 13/13、backend full 3339/3339、frontend build passed、Chrome smoke passed、stage3 preflight passed。

本批已验证 FU：`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。累计 verified FUs：`FU-67568b793d`《警觉的哨兵》与 `FU-3acf92c924`《诚实掮客》。overlay status 均为 `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，仍不能升级 full-official。

4C-3 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- last-breath：`FU-6a52b04cb2`、`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-b829fb32b9`、`FU-16d3a6dd4e`、`FU-4e2e19359f`、`FU-f9eb8c6f71`、`FU-1701d1d89a`
- on-play registered trigger：`FU-d5e1143438`、`FU-bf81341dd2`、`FU-e8d8846d73`、`FU-808f8b89db`、`FU-f18a49e06d`、`FU-67c6b0186e`
- attack / defense / conquer：`FU-661793867e`、`FU-5cea85e7c3`、`FU-422b450261`、`FU-7f4a387b92`、`FU-c027639a3c`、`FU-3e9cb3904e`

后续批量顺序建议：先扩展 destroyed / friendly-destroyed real enqueue，再补 state-based cleanup trigger enqueue，再做 trigger payment / decline / payment failure，之后才压 attack / defense / conquer real trigger enqueue 与 FAQ adjudication。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed 族、state-based cleanup trigger enqueue、trigger payment / decline、FAQ adjudication、1009/811 full-official 覆盖。

## 11. Stage 4C-4 Trigger Payment Overlay

4C-4 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch4TriggerPayment`，并只为 `FU-4694e33f45` 增加 `functionalUnits[].stage4C4` overlay。

4C-4 已部分降低的 blocker：

- `SFD·220/221`《珍宝堆》对应 `FU-4694e33f45`，冻结实现口径仍是非 PLAY_CARD 战场规则域 `BATTLEFIELD_RULE_DOMAIN`。
- 征服该战场后打开服务端权威 `TRIGGER_PAYMENT`，前端只提交服务端 `PAY_COST` candidate。
- `SPEND_MANA:1` 路径扣 1 点法力并创建休眠“金币”装备指示物；`DECLINE` 路径关闭窗口且不扣费、不创建指示物。
- wrong player / stale prompt / unknown choice / duplicate choice / pay+decline / malformed payload / insufficient mana 都拒绝且 no mutation。
- A 验证结果：focused trigger payment 11/11、trigger regression 13/13、backend full 3344/3344、frontend build passed、Chrome smoke passed、stage3 preflight passed after sequential rerun。

本批已验证 FU：`FU-4694e33f45` / `SFD·220/221`《珍宝堆》。overlay status 为 `TRIGGER_PAYMENT_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，仍不能升级 full-official。

4C-4 next-pressure 候选只记录在顶层，不标已实现：

- triggered costs：`FU-67c6b0186e`、`FU-c170628e3a`、`FU-b829fb32b9`、`FU-f18a49e06d`、`FU-05ce012700`、`FU-c027639a3c`
- PaymentEngine families：替代 / 额外费用、触发费用进入结算链项目、state-based cleanup 生成的支付窗口、跨触发族 FAQ 拒付语义。

后续批量顺序建议：先补 state-based cleanup trigger enqueue，再扩展 triggered-cost payment windows，之后才压 attack / defense / conquer real trigger enqueue 与 FAQ adjudication。

仍缺：完整 PaymentEngine、`SFD·220/221` 之外 triggered-cost FUs、state-based cleanup trigger payment、FAQ adjudication、1009/811 full-official 覆盖。

## 12. Stage 4C-5 State-Based Cleanup Trigger Enqueue Overlay

4C-5 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch5StateCleanupTriggerEnqueue`，并只为 `FU-67568b793d` 增加 `functionalUnits[].stage4C5` overlay。

4C-5 已部分降低的 blocker：

- `OGN·096/298`《警觉的哨兵》对应 `FU-67568b793d`，4C-5 代表路径仍复用 `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`，但触发来源从 4C-2 的普通 `UNIT_DESTROYED` 扩展到 state-based cleanup `LETHAL_DAMAGE`。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为致命伤害来源，不被本批标为已实现或 full-official。
- 验证路径：`Starfall damage -> state-based cleanup LETHAL_DAMAGE -> visible Watchful Sentinel WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN`。
- hidden / face-down / standby Watchful Sentinel 不入队，作为 metadata leak guard；这不是完整隐藏来源触发建模。
- A 验证结果：focused RealTriggerQueue 4/4、backend full 3346/3346、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FU：`FU-67568b793d` / `OGN·096/298`《警觉的哨兵》。overlay status 为 `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-5 next-pressure 候选只记录在顶层，不标已实现：

- state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-16d3a6dd4e`、`FU-1701d1d89a`、`FU-b829fb32b9`、`FU-f67078d119`
- state cleanup destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先扩展可见 state-based cleanup last-breath / destroyed enqueue，再做 hidden / face-down trigger original visibility model，之后补 FAQ adjudication、battle / damage pressure 和正式 E2E guardrails。

仍缺：完整 trigger engine、visible Watchful cleanup slice 之外的 last-breath / destroyed / friendly-destroyed FUs、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 13. Stage 4C-6 Honest Broker Cleanup Trigger Enqueue Overlay

4C-6 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch6HonestCleanupTriggerEnqueue`，并只为 `FU-3acf92c924` 增加 `functionalUnits[].stage4C6` overlay；原有 `stage4C3` overlay 保留不回退。

4C-6 已部分降低的 blocker：

- `SFD·155/221`《诚实掮客》对应 `FU-3acf92c924`，4C-6 代表路径仍复用 `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`，但触发来源从 4C-3 的普通 `UNIT_DESTROYED` 扩展到 state-based cleanup `LETHAL_DAMAGE`。
- 支撑源牌仍为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为致命伤害来源，不被本批标为已实现或 full-official。
- 验证路径：`Starfall damage -> state-based cleanup LETHAL_DAMAGE -> visible Honest Broker HONEST_BROKER_LAST_BREATH_CREATE_GOLD -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EQUIPMENT_TOKEN_CREATED`。
- hidden / face-down / standby Honest Broker 不入队、不创建 token，作为 metadata leak guard；这不是完整隐藏来源触发建模。
- A 验证结果：focused RealTriggerQueue 6/6、backend full 3348/3348、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FU：`FU-3acf92c924` / `SFD·155/221`《诚实掮客》。overlay status 为 `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-6 next-pressure 候选只记录在顶层，不标已实现：

- Poro conditional draw：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- simple state cleanup last-breath：`FU-0500c77a70`、`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先扩展 Sad / Loyal Poro conditional cleanup draw，再做 Scouting Warhawk / Unsung Hero cleanup enqueue，之后再压 destroyed / friendly-destroyed families 与 hidden-origin trigger model。

仍缺：完整 trigger engine、visible Watchful / Honest cleanup slices 之外的 last-breath / destroyed / friendly-destroyed FUs、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 14. Stage 4C-7 Scouting Warhawk Trigger Enqueue Overlay

4C-7 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch7ScoutingWarhawkTriggerEnqueue`，并只为 `FU-0500c77a70` 增加 `functionalUnits[].stage4C7` overlay。

4C-7 已部分降低的 blocker：

- `OGN·216/298`《侦察飞鹰》对应 `FU-0500c77a70`，本批验证 explicit destroy 产生的 `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` 真实入队路径。
- 支撑源牌为 `OGN·256/298`《妖异狐火》，对应 `FU-a9dc3495e1` / `SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4`；该 FU 只作为 explicit destroy source，不被本批标为已实现或 full-official。
- 验证路径：`UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`；这不是完整隐藏来源触发建模。
- single-trigger compatibility 保留；没有协议 / 前端变化。
- A 验证结果：focused RealTriggerQueue 9/9、backend full 3350/3350、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。overlay status 为 `REAL_CARD_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-7 next-pressure 候选只记录在顶层，不标已实现：

- same FU future pressure：`FU-0500c77a70` 的 state-based cleanup enqueue 尚未由 4C-7 覆盖。
- Poro conditional draw：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- simple state cleanup last-breath：`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先补 Scouting Warhawk state-based cleanup enqueue，再扩展 Sad / Loyal Poro conditional cleanup draw，之后再做 destroyed / friendly-destroyed families 与 hidden-origin trigger model。

仍缺：完整 trigger engine、Scouting Warhawk state-based cleanup enqueue、其他 last-breath / destroyed / friendly-destroyed FUs、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 15. Stage 4C-8 Scouting Warhawk Cleanup Trigger Enqueue Overlay

4C-8 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch8ScoutingWarhawkCleanupTriggerEnqueue`，并只为 `FU-0500c77a70` 增加 `functionalUnits[].stage4C8` overlay；4C-7 explicit destroy overlay 保留不回退。

4C-8 已部分降低的 blocker：

- `OGN·216/298`《侦察飞鹰》对应 `FU-0500c77a70`，本批验证 state-based cleanup lethal damage 产生的 `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` 真实入队路径。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为 lethal damage + cleanup source，不被本批标为已实现或 full-official。
- 验证路径：`Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible Scouting Warhawk SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不泄漏、不 `RUNES_CALLED`；这不是完整隐藏来源触发建模。
- A 验证结果：focused RealTriggerQueue 11/11、backend full 3352/3352、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FU：`FU-0500c77a70` / `OGN·216/298`《侦察飞鹰》。overlay status 为 `STATE_BASED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-8 next-pressure 候选只记录在顶层，不标已实现：

- Poro conditional draw：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`
- simple state cleanup last-breath：`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先扩展 Sad / Loyal Poro conditional cleanup draw，再做 Unsung Hero cleanup draw，之后再压 destroyed / friendly-destroyed families 与 hidden-origin trigger model。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed FUs、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 16. Stage 4C-9 Sad/Loyal Poro Cleanup Trigger Enqueue Overlay

4C-9 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch9PoroCleanupTriggerEnqueue`，并只为 `FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d` 增加 `functionalUnits[].stage4C9` overlay。

4C-9 已部分降低的 blocker：

- `SFD·036/221`《哀哀魄罗》对应 `FU-f8bfd5c6f9`，本批验证 state-based cleanup lethal damage 产生的 `SAD_PORO_LAST_BREATH_DRAW_1` 真实入队路径。
- `UNL-221/219`《哀哀魄罗》对应 `FU-938b749c23`，复用 `SAD_PORO_LAST_BREATH_DRAW_1`；该 FU 独立计数，不与 SFD 版本合并。
- `UNL-156/219`《忠忠魄罗》对应 `FU-0415e3b46d`，本批验证 `LOYAL_PORO_LAST_BREATH_DRAW_1` 真实入队路径。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为 lethal damage + cleanup source，不被本批标为已实现或 full-official。
- 验证路径：`Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Poro condition -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN`。
- Sad Poro 条件为同位置无其他友方正面非待命单位；Loyal Poro 条件为同位置有其他友方正面非待命单位，且该其他友方不在本轮 cleanup removal set。
- hidden / face-down / standby Poro 不入队、不泄漏、不 `CARD_DRAWN`；同时死亡落单判定仍不是 full-official。
- A 验证结果：focused RealTriggerQueue 21/21、backend full 3358/3358、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FUs：`FU-f8bfd5c6f9`、`FU-938b749c23`、`FU-0415e3b46d`。overlay status 为 `CONDITIONAL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-9 next-pressure 候选只记录在顶层，不标已实现：

- simple state cleanup last-breath：`FU-1701d1d89a`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先做 Unsung Hero cleanup draw，再压 destroyed / friendly-destroyed families；hidden-origin 与 simultaneous-death condition adjudication 需要 D/用户规则判断后再扩张。

仍缺：完整 trigger engine、其他 last-breath / destroyed / friendly-destroyed FUs、同时死亡落单判定 full-official、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 17. Top20 高风险 Functional Units

| # | FU | Representative | 类型/条目数 | 当前代表映射 | FAQ 候选页 | 风险依据 | 依赖规则域 |
|---:|---|---|---:|---|---|---|---|
| 1 | `FU-fb79eea7fc` | `OGN·077/298` 中娅沙漏 | 装备 / 1 | 代表路径：ZHONYAS_HOURGLASS_PLAY_EQUIPMENT | SOUL-JFAQ-260114 p15<br>SOUL-JFAQ-260114 p2<br>SOUL-JFAQ-260114 p23<br>SOUL-JFAQ-260114 p9<br>SOUL-OFAQ-260114 p8 | 清理/替换/持续时间、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、费用/支付、目标/结算链/时机 | CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 2 | `FU-2653af0380` | `OGN·242/298` 海兽钓钩 | 装备 / 1 | 代表路径：SEA_MONSTER_HOOK_PLAY_EQUIPMENT | BREAK-JFAQ-260416 p9<br>JFAQ-251023 p2<br>JFAQ-251023 p3<br>SOUL-JFAQ-260114 p22 | 清理/替换/持续时间、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、费用/支付、目标/结算链/时机 | CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 3 | `FU-104211dbbc` | `SFD·148/221` 德莱文 | 英雄单位 / 2 | 代表路径：SFD_DRAVEN_ALT_A_PLAY_KEYWORD_UNIT, SFD_DRAVEN_PLAY_KEYWORD_UNIT | BREAK-JFAQ-260416 p4<br>SOUL-JFAQ-260114 p25<br>SOUL-JFAQ-260114 p4<br>SOUL-OFAQ-260114 p16<br>SOUL-OFAQ-260114 p17 | 战斗/法术对决、清理/替换/持续时间、FAQ 提及、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST |
| 4 | `FU-964b214448` | `SFD·020/221` 德莱文 | 英雄单位 / 2 | 代表路径：SFD_020_DRAVEN_VANILLA_PLAY_UNIT, SFD_020A_DRAVEN_VANILLA_PLAY_UNIT | BREAK-JFAQ-260416 p4<br>SOUL-JFAQ-260114 p25<br>SOUL-JFAQ-260114 p4<br>SOUL-OFAQ-260114 p16<br>SOUL-OFAQ-260114 p17 | 战斗/法术对决、清理/替换/持续时间、FAQ 提及、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST |
| 5 | `FU-2dca1ad450` | `SFD·082/221` 伊泽瑞尔 | 英雄单位 / 3 | 代表路径：SFD_082_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT, SFD_082A_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT, SFD_082B_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT | BREAK-JFAQ-260416 p5<br>SOUL-JFAQ-260114 p19<br>SOUL-JFAQ-260114 p25<br>SOUL-OFAQ-260114 p20 | 战斗/法术对决、控制权/区域移动、FAQ 提及、效果层/持续效果、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |
| 6 | `FU-9f7cb73dc4` | `UNL-150/219` 薇古丝 | 英雄单位 / 1 | 代表路径：VEX_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC | BREAK-JFAQ-260416 p5<br>BREAK-JFAQ-260416 p9<br>SOUL-JFAQ-260114 p17 | 战斗/法术对决、清理/替换/持续时间、控制权/区域移动、FAQ 提及、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |
| 7 | `FU-422b450261` | `SFD·170/221` 雷克塞 | 英雄单位 / 2 | 代表路径：SFD_170_REKSAI_ATTACK_REVEAL_PLAY_UNIT, SFD_170A_REKSAI_ATTACK_REVEAL_PLAY_UNIT | BREAK-JFAQ-260416 p3<br>SOUL-JFAQ-260114 p19<br>SOUL-OFAQ-260114 p4 | 战斗/法术对决、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 8 | `FU-05ce012700` | `SFD·218/221` 沉没神庙 | 战场 / 1 | 非 PLAY_CARD 域：BATTLEFIELD_RULE_DOMAIN | SOUL-JFAQ-260114 p8<br>SOUL-OFAQ-260114 p15 | 战斗/法术对决、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、非 PLAY_CARD 规则域、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones |
| 9 | `FU-1945f6918c` | `SFD·029/221` 雷克塞 | 英雄单位 / 2 | 代表路径：REKSAI_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE_OVERWHELM, REKSAI_PLAY_UNIT_NO_OPTIONAL_HASTE_OVERWHELM | BREAK-JFAQ-260416 p3<br>SOUL-JFAQ-260114 p19<br>SOUL-OFAQ-260114 p4 | 战斗/法术对决、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones |
| 10 | `FU-813144e7d4` | `OGN·168/298` 战或逃 | 法术 / 1 | 代表路径：BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE | JFAQ-251023 p4<br>SOUL-JFAQ-260114 p12<br>SOUL-JFAQ-260114 p16 | 战斗/法术对决、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 11 | `FU-464ec8c275` | `SFD·112/221` 巨腕加藤 | 单位 / 1 | 代表路径：GIANT_ARM_KATO_PLAY_KEYWORD_UNIT | SOUL-JFAQ-260114 p12<br>SOUL-JFAQ-260114 p3<br>SOUL-OFAQ-260114 p12 | 清理/替换/持续时间、控制权/区域移动、FAQ 提及、效果层/持续效果、费用/支付、目标/结算链/时机 | CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |
| 12 | `FU-6e7d0dba2c` | `SFD·187/221` 虚空遁地兽 | 传奇 / 2 | 非 PLAY_CARD 域：LEGEND_ACTION_DOMAIN | SOUL-JFAQ-260114 p14<br>SOUL-OFAQ-260114 p4 | 战斗/法术对决、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、非 PLAY_CARD 规则域、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 13 | `FU-0b6332bbf0` | `SFD·145/221` 换换乐 | 法术 / 1 | 代表路径：SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS | SOUL-JFAQ-260114 p14 | 战斗/法术对决、清理/替换/持续时间、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones |
| 14 | `FU-6308c2db01` | `OGN·269/298` 腕豪 | 传奇 / 3 | 非 PLAY_CARD 域：LEGEND_ACTION_DOMAIN | 无页码级 FAQ 命中；按规则域复杂度入选 | 战斗/法术对决、清理/替换/持续时间、控制权/区域移动、效果层/持续效果、非 PLAY_CARD 规则域、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |
| 15 | `FU-7419ee7d9d` | `SFD·109/221` 阿克尚 | 英雄单位 / 1 | 代表路径：AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT | SOUL-JFAQ-260114 p18<br>SOUL-JFAQ-260114 p23 | 清理/替换/持续时间、控制权/区域移动、FAQ 提及、效果层/持续效果、费用/支付、目标/结算链/时机 | CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |
| 16 | `FU-00ee09c2cc` | `SFD·202/221` 恶意收购 | 专属法术 / 1 | 代表路径：HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT | SOUL-JFAQ-260114 p22<br>SOUL-OFAQ-260114 p21 | 战斗/法术对决、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 17 | `FU-b05eda44ce` | `OGN·025/298` 暴怒冲动 | 法术 / 1 | 代表路径：BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT | SOUL-OFAQ-260114 p4 | 战斗/法术对决、控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、费用/支付、目标/结算链/时机 | BattleEngine/SpellDuelLifecycle/ASSIGN_COMBAT_DAMAGE, FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 18 | `FU-081d97eb3e` | `SFD·078/221` 预时之门 | 装备 / 1 | 代表路径：TIME_GATE_PLAY_EQUIPMENT | BREAK-JFAQ-260416 p11<br>SOUL-JFAQ-260114 p15<br>SOUL-JFAQ-260114 p19<br>SOUL-JFAQ-260114 p25<br>SOUL-JFAQ-260114 p6<br>SOUL-OFAQ-260114 p21 | 清理/替换/持续时间、FAQ 提及、费用/支付、目标/结算链/时机 | CleanupQueue/ReplacementEffects, FEPR/Targeting/TimingWindows, PaymentEngine/PAY_COST |
| 19 | `FU-804412488c` | `SFD·139/221` 夜之锋刃 | 装备 / 1 | 代表路径：EDGE_OF_NIGHT_PLAY_EQUIPMENT | SOUL-OFAQ-260114 p10<br>SOUL-OFAQ-260114 p9 | 控制权/区域移动、FAQ 提及、隐藏信息/随机/牌堆、效果层/持续效果、费用/支付、目标/结算链/时机 | FEPR/Targeting/TimingWindows, LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, VisibilityFilter/RandomAndHiddenZones, ZoneOwnership/ControlChange/Movement |
| 20 | `FU-9a623b3185` | `SFD·059/221` 斯弗尔尚歌 | 装备 / 1 | 代表路径：SFUR_SONG_PLAY_EQUIPMENT | SOUL-JFAQ-260114 p24<br>SOUL-JFAQ-260114 p25<br>SOUL-JFAQ-260114 p8<br>SOUL-OFAQ-260114 p18<br>SOUL-OFAQ-260114 p19 | 控制权/区域移动、FAQ 提及、效果层/持续效果、费用/支付 | LayerEngine/ContinuousEffects, PaymentEngine/PAY_COST, ZoneOwnership/ControlChange/Movement |

## 18. 未覆盖效果分类

| 分类 | 含义 | 当前阻断关系 |
|---|---|---|
| `payment-cost` | 费用、额外费用、符能/法力、法盾、回响、急速、装配、百炼。 | PaymentEngine / `PAY_COST` 未完整官方化。 |
| `battle-spell-duel` | 进攻、防守、征服、据守、法术对决、战斗伤害。 | battle / spell duel lifecycle 与 `ASSIGN_COMBAT_DAMAGE` 仍是 P0。 |
| `cleanup-replacement-duration` | 摧毁、替代、持续到本回合、绝念、瞬息、抵挡。 | central cleanup queue、替代效果、持续时间仍未清零。 |
| `layer-continuous-effect` | 战力、关键词授予、装备贴附、等级、复制、增益。 | LayerEngine / 持续效果 / 禁止效果仍是 P1。 |
| `hidden-info-random-zone` | 查看、展示、手牌/牌堆、正面朝下、待命、抽牌、随机。 | Visibility filter 与隐藏信息回归必须逐卡确认。 |
| `targeting-stack-timing` | 目标选择、反应/迅捷、无效化、结算链、时机窗口。 | FEPR、目标失效和复杂 prompt schema 未全量完成。 |
| `control-zone-movement` | 控制权改变、移动、召回、放逐、回收、所属者区域。 | control / zone / battlefield task lifecycle 仍未官方化。 |
| `non-play-domain` | 传奇、战场、符文、指示物等非普通 PLAY_CARD 域。 | 需要专门域矩阵，不可与普通出牌效果混算。 |
| `faq-mentioned` | 五份 PDF/FAQ 中出现卡名的候选项。 | 必须人工判定问题是否真的约束该 FU，并补测试。 |

## 19. P0/P1 仍未清零

P0：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- `PAY_COST` 已有 3A 最小 runtime，4C-4 已验证 `SFD·220/221` `TRIGGER_PAYMENT` 支付 / 拒付代表路径，`ASSIGN_COMBAT_DAMAGE` 已有 3C 最小 runtime，`ORDER_TRIGGERS` 已升级为 4C-1 保守 APNAP controller-block 子集，4C-2 / 4C-3 只验证 Watchful Sentinel 与 Honest Broker real trigger enqueue，4C-5 / 4C-6 只验证 visible Watchful Sentinel 与 visible Honest Broker 的 state-based cleanup trigger enqueue，4C-7 / 4C-8 只验证 visible Scouting Warhawk explicit destroy 与 state-based cleanup trigger enqueue，4C-9 只验证 visible Sad/Loyal Poro conditional cleanup trigger enqueue；完整 PaymentEngine、完整 damage assignment 全规则矩阵、完整 trigger engine / battle initial stack 全规则仍未完成。
- 正式 18 步 E2E 未最终收口。
- 1009 entries / 811 FUs 的 FAQ 证据与 full-official 测试矩阵未完成。

P1：

- PaymentEngine 仍未统一到完整官方支付窗口。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- FAQ 候选页码尚未全部人工 adjudicate 为“适用 / 不适用 / 通用规则”。
- 当前实现映射仍是 representative route，不能升级为 `full-official-rule-pass`。

是否允许批量覆盖：**不允许。**
