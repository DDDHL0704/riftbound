# 当前卡牌效果高风险 Top20

更新时间：2026-05-11

阶段：**阶段 4C-47 / E 卡牌覆盖矩阵 post-freeze overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文以阶段 2 风险排序为基础，并叠加阶段 3A/3B/3C/3D 的最小证据 overlay、阶段 4B 冻结状态、阶段 4C-1 APNAP `ORDER_TRIGGERS` 部分 blocker 降低、阶段 4C-2/4C-3 real trigger enqueue、阶段 4C-4 trigger payment、阶段 4C-5 Watchful state-based cleanup trigger enqueue、阶段 4C-6 Honest Broker state-based cleanup trigger enqueue、阶段 4C-7 Scouting Warhawk explicit destroy trigger enqueue、阶段 4C-8 Scouting Warhawk state-based cleanup trigger enqueue、阶段 4C-9 Sad/Loyal Poro conditional cleanup trigger enqueue、阶段 4C-10 Unsung Hero powerful cleanup trigger enqueue、阶段 4C-11 Ghostly Centaur friendly-destroyed cleanup trigger enqueue、阶段 4C-12 Resonant Soul first-friendly-destroyed cleanup trigger enqueue、阶段 4C-13 true stack destruction route migration、阶段 4C-14 Savage Jawfish friendly-destroyed experience trigger enqueue、阶段 4C-15A Minion token family model overlay、阶段 4C-15B Viktor destroyed non-Minion trigger enqueue、阶段 4C-16 / 4C-17 Mechanical Trickster / Ironclad Vanguard true stack last-breath trigger enqueue、阶段 4C-18 Mechanical Trickster + Ironclad Vanguard cleanup trigger enqueue、阶段 4C-19 Kogmaw last-breath AoE damage representative route、阶段 4C-20B Undercover Agent triggered hand-choice prompt、阶段 4C-21 Sunken Temple trigger payment、阶段 4C-22 Muddy Dredger Warhawk representative baseline、阶段 4C-23 Lux high-cost spell temporary power representative baseline、阶段 4C-24 Vayne conquer pay-one recall representative baseline、阶段 4C-25 Icevale Archer attack payment target-selection representative baseline、阶段 4C-26 Jax weapon attach pay-one draw-one representative baseline、阶段 4C-27 Treasure Hunter move-create dormant Gold representative baseline、阶段 4C-28 Battle or Flight move battlefield unit to owner base target-guard representative baseline、阶段 4C-29 Gust power-three-or-less return-to-owner-hand target-guard representative baseline、阶段 4C-30 Hunt the Weak power-three-or-less destroy-target guard representative baseline、阶段 4C-31 Reprimand return-to-owner-hand target-guard representative baseline、阶段 4C-32 Ride the Wind move-friendly-battlefield-unit-to-owner-base ready target-guard representative baseline、阶段 4C-33 Charm move-enemy-battlefield-unit-to-owner-base target-guard representative baseline、阶段 4C-34 Isolate move-enemy-battlefield-unit-to-owner-base no-draw target-guard representative baseline、阶段 4C-35 Vengeance public-unit destroy-target guard representative baseline、阶段 4C-36 Hostile Takeover control-ready target-guard representative baseline、阶段 4C-37 Berserk Impulse opponent top-unit play target-guard representative baseline、阶段 4C-38 Edge of Night play-equipment / assemble-purple guard representative baseline、阶段 4C-39 Zhonya's Hourglass play-equipment guard representative baseline、阶段 4C-40 Sea Monster Hook play-equipment guard representative baseline、阶段 4C-41 Giant Arm Kato play-keyword-unit guard representative baseline、阶段 4C-42 Time Gate play-equipment guard representative baseline、阶段 4C-43 Sfur Song play-equipment guard representative baseline、阶段 4C-44 Akshan play-unit guard representative baseline、阶段 4C-45 Switcheroo battlefield power-swap guard representative baseline、阶段 4C-46 Void Burrower legend-domain shared-oracle design-gated baseline 和阶段 4C-47 Draven battle body / play-unit guard representative baseline；它不是功能实现清单，也不是错误断言。排名用于告诉后续阶段先审哪里：哪些 functional unit 同时碰到 FAQ、费用、触发/替换、持续效果、战斗/法术对决、隐藏信息或非 PLAY_CARD 规则域。

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

4C 批量顺序建议：4C-1 已先降低 `ORDER_TRIGGERS` 的保守 APNAP controller-block / battle initial stack 代表路径 / hidden trigger metadata redaction blocker；4C-2 / 4C-3 已验证 Watchful Sentinel 与 Honest Broker 两个 last-breath real enqueue 切片；4C-4 已验证 Treasure Pile trigger payment；4C-5 / 4C-6 已验证 visible Watchful / Honest Broker state-based cleanup trigger enqueue；4C-7 / 4C-8 已验证 Scouting Warhawk explicit destroy 与 state-based cleanup real trigger enqueue；4C-9 已验证 Sad / Loyal Poro conditional cleanup draw enqueue；4C-10 已验证 Unsung Hero powerful cleanup draw-2 enqueue；4C-11 已验证 Ghostly Centaur friendly-destroyed cleanup +2 power enqueue；4C-12 已验证 Resonant Soul first-friendly-destroyed cleanup draw enqueue；4C-13 已迁移 Ghostly / Resonant true stack destruction non-cleanup route；4C-14 已验证 Savage Jawfish true stack 与 Starfall cleanup friendly-destroyed experience enqueue；4C-15A 已记录 Minion token family marker infrastructure；4C-15B 已验证 Viktor destroyed non-Minion trigger enqueue representative baseline。下一批应扩展 Kogmaw/Karthus/Undercover Agent、hidden origin visibility、simultaneous-death / effective-power condition adjudication、FAQ adjudication、battle/damage 压测和 full E2E guardrails。

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

## 17. Stage 4C-10 Unsung Hero Cleanup Trigger Enqueue Overlay

4C-10 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch10UnsungHeroCleanupTriggerEnqueue`，并只为 `FU-1701d1d89a` 增加 `functionalUnits[].stage4C10` overlay。

4C-10 已部分降低的 blocker：

- `SFD·167/221`《无名英雄》对应 `FU-1701d1d89a`，本批验证 state-based cleanup lethal damage 产生的 `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2` 真实入队路径。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为 lethal damage + cleanup source，不被本批标为已实现或 full-official。
- 验证路径：`Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE UNIT_DESTROYED -> visible base-zone Unsung Hero CardObjectState.Power >= 5 -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN x2`。
- `Power < 5` 不入队、不抽牌；hidden / face-down / standby Unsung Hero 不入队、不泄漏、不抽牌。
- 严格边界：只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- A 验证结果：focused RealTriggerQueue 21/21、backend full 3361/3361、frontend build passed、Chrome smoke passed、Stage 3 preflight passed。

本批已验证 FU：`FU-1701d1d89a` / `SFD·167/221`《无名英雄》。overlay status 为 `POWERFUL_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-10 next-pressure 候选只记录在顶层，不标已实现：

- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`、`FU-0f2c4a3ea5`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先压 destroyed / friendly-destroyed families；hidden-origin、simultaneous-death 与 effective-power condition adjudication 需要 D/用户规则判断后再扩张。

仍缺：完整 trigger engine、LayerEngine / effective power / temporary modifier powerful adjudication、battlefield objectLocation 全矩阵、explicit destroy migration、其他 last-breath / destroyed / friendly-destroyed FUs、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 18. Stage 4C-11 Ghostly Centaur Cleanup Trigger Enqueue Overlay

4C-11 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch11GhostlyCentaurCleanupTriggerEnqueue`，并只为 `FU-0f2c4a3ea5` 增加 `functionalUnits[].stage4C11` overlay。

4C-11 已部分降低的 blocker：

- `UNL-068/219`《幽魂半人马》对应 `FU-0f2c4a3ea5`，本批验证 state-based cleanup 中“另一名友方单位被摧毁”产生的 `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` 真实入队路径。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为 lethal damage + cleanup source，不被本批标为已实现或 full-official。
- 验证路径：`Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> visible surviving friendly Ghostly source -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> POWER_MODIFIED_UNTIL_END_OF_TURN +2`。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不加战力。
- source 也在本轮 cleanup removal set 时不入队。
- same source 同一轮 cleanup pass 中多个友方同时死亡保守封顶为 1 个 trigger；不是 full-official。
- true stack destruction immediate P79 compatibility 保留，本批不迁移。
- 不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent。
- A 验证结果：focused RealTriggerQueue 23/23、backend full 3364/3364、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

本批已验证 FU：`FU-0f2c4a3ea5` / `UNL-068/219`《幽魂半人马》。overlay status 为 `FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-11 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`、`FU-c146331876`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先做 Resonant Soul first-friendly-destroyed draw memory，再做 Viktor destroyed non-minion token family；Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback。

仍缺：完整 trigger engine、Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent、same-source 多友方同时死亡 full-official、true stack destruction queued migration、LayerEngine / temporary power duration cleanup matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 19. Stage 4C-12 Resonant Soul Cleanup Trigger Enqueue Overlay

4C-12 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch12ResonantSoulCleanupTriggerEnqueue`，并只为 `FU-c146331876` 增加 `functionalUnits[].stage4C12` overlay。

4C-12 已部分降低的 blocker：

- `OGN·118/298`《残响之魂》对应 `FU-c146331876`，本批验证 state-based cleanup 中“每回合首次友方单位被摧毁”产生的 `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1` 真实入队路径。
- 支撑源牌为 `OGN·029/298`《星落》，对应 `FU-56d6b01aa1` / `STARFALL_DAMAGE_3_TWICE`；该 FU 只作为 lethal damage + cleanup source，不被本批标为已实现或 full-official。
- 验证路径：`Starfall lethal damage -> state-based cleanup LETHAL_DAMAGE / UNIT_DESTROYED -> visible surviving friendly Resonant source and owner not already destroyed this turn -> TriggerQueue -> ORDER_TRIGGERS -> StackItems -> priority pass -> TRIGGER_RESOLVED -> CARD_DRAWN 1`。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不抽牌。
- source 也在本轮 cleanup removal set 时不入队；owner already in `DestroyedUnitOwnerIdsThisTurn` 时不入队、不抽牌。
- per owner per cleanup pass uses first destroyed unit only；simultaneous multiple units 不是 full-official。
- true stack destruction immediate P79 compatibility 保留，本批不迁移。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。
- A 验证结果：focused RealTriggerQueue 27/27、backend full 3368/3368、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

本批已验证 FU：`FU-c146331876` / `OGN·118/298`《残响之魂》。overlay status 为 `FIRST_FRIENDLY_DESTROYED_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

4C-12 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：先做 Viktor destroyed non-minion token family；Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback。

仍缺：完整 trigger engine、Viktor / Kogmaw / Karthus / Undercover Agent、simultaneous multiple units first-only full-official、true stack destruction queued migration、per-turn destroyed owner memory full reset matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 20. Stage 4C-13 Stack Destroyed Trigger Migration Overlay

4C-13 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch13StackDestroyedTriggerMigration`，并只为 `FU-0f2c4a3ea5` 与 `FU-c146331876` 增加 `functionalUnits[].stage4C13` overlay。

4C-13 已部分降低的 blocker：

- `UNL-068/219`《幽魂半人马》对应 `FU-0f2c4a3ea5`，true stack destruction non-cleanup `UNIT_DESTROYED` route 迁移为 `GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2` 入队并通过 priority resolve 为本回合 +2。
- `OGN·118/298`《残响之魂》对应 `FU-c146331876`，true stack destruction non-cleanup `UNIT_DESTROYED` route 迁移为 `RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1` 入队并通过 priority resolve 为抽 1。
- route：`true stack destruction non-cleanup UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> effect result`。
- cleanup path 仍由 4C-11 / 4C-12 覆盖，并从 old stack helper 排除以避免 duplicate enqueue。
- old P79 immediate compatibility 已移除 / 迁移；P79 现在断言 queue / priority semantics。
- hidden / face-down / standby / opponent-controlled source 不入队、不泄漏、不生效。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent；same-source multiple simultaneous deaths full matrix 仍未覆盖。
- A 验证结果：focused RealTriggerQueue 30/30、backend full 3370/3370、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

本批是 route migration，不新增 unique FU coverage。`stage4C13RouteUpgradedFunctionalUnits = 2`，cumulative real-trigger enqueue verified FUs 保持 9，cumulative state-based cleanup trigger enqueue verified FUs 保持 9，full-official upgrades 保持 0。

4C-13 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：优先做 Viktor destroyed non-minion token family；Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback。

仍缺：完整 trigger engine、Viktor / Kogmaw / Karthus / Undercover Agent、same-source / same-owner multiple simultaneous deaths full-official、per-turn destroyed owner memory full reset matrix、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 21. Stage 4C-14 Savage Jawfish Trigger Enqueue Overlay

4C-14 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch14SavageJawfishTriggerEnqueue`，并只为 `FU-bd94334cc5` 增加 `functionalUnits[].stage4C14` overlay。

4C-14 已部分降低的 blocker：

- `UNL-129/219` Savage Jawfish / 《凶残颚鱼》对应 `FU-bd94334cc5`，本批验证 true stack 与 Starfall lethal cleanup 两条 `UNIT_DESTROYED` 路径产生的 `SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1` 真实入队路径。
- 路径：`UNIT_DESTROYED -> TriggerQueue -> ORDER_TRIGGERS or single-trigger auto-stack -> StackItems -> priority pass -> TRIGGER_RESOLVED -> EXPERIENCE_GAINED +1`。
- source must remain field, face-up, non-standby, same controller, and not destroyed/removal set。
- hidden face-down / standby / opponent-controlled source 不入队、不泄漏、不获得经验。
- same source same pass multi-destroy trigger multiplicity 仍为 P1/TODO，不是 full-official。
- 不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent。
- A 验证结果：focused RealTriggerQueue 33/33、backend full 3374/3374、frontend build passed、Chrome smoke passed、Stage3 preflight passed、diff check passed。

本批已验证 FU：`FU-bd94334cc5` / `UNL-129/219`《凶残颚鱼》。overlay status 为 `FRIENDLY_DESTROYED_STACK_AND_CLEANUP_TRIGGER_ENQUEUE_PARTIALLY_REDUCED_NOT_FULL_OFFICIAL`，4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

矩阵数字：`stage4C14` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 10，cumulative state-based cleanup trigger enqueue verified FUs = 10，full-official upgrades = 0。

4C-14 next-pressure 候选只记录在顶层，不标已实现：

- destroyed / friendly-destroyed：`FU-b5cb36a5c9`
- complex state cleanup last-breath：`FU-af8b05c294`、`FU-ee1dfb3ed3`、`FU-6a52b04cb2`、`FU-b829fb32b9`、`FU-f67078d119`、`FU-16d3a6dd4e`
- high-complexity holdback：`FU-4e2e19359f`、`FU-f9eb8c6f71`

后续批量顺序建议：优先做 Viktor destroyed non-minion token family；Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback。

仍缺：完整 trigger engine、same source same pass multi-destroy trigger multiplicity、Viktor / Kogmaw / Karthus / Undercover Agent、隐藏 / face-down trigger original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 22. Stage 4C-15A Minion Token Family Model Overlay

4C-15A 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch15AMinionTokenFamilyModel`。本批是 model / infrastructure overlay，不给任何 FU 增加 full-official 证据。

4C-15A 已降低的前置 blocker：

- 新稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- 官方随从 token factory `OGN·271/298`、`OGN·272/298`、`OGN·273/298` 带 marker。
- `CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`；Viktor legend 直接随从创建也同步。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield-held minion 等随从 token 带 marker。
- 普通单位不带；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”不带。
- hidden face-down standby opponent snapshot 不泄漏 tags / cardNo / power。
- A 独立复核：backend full 3375/3375、`git diff --check` passed。

Viktor boundary：

- 4C-15A 可降低或关闭 token subtype / family / minion-classification 前置 blocker。
- `FU-b5cb36a5c9` Viktor trigger 未关闭，仍 `SHARED_ORACLE_IMPLEMENTATION` / `NEEDS_ENGINE_SUPPORT`，`fullOfficial=false`。
- 本批不实现 Viktor 本体，不覆盖 Kogmaw / Karthus / Undercover Agent。

矩阵数字：`stage4C15AVerifiedInfrastructure = true`，`stage4C15AFullOfficialFunctionalUnits = 0`，`stage4C15AFullOfficialSnapshotEntries = 0`，`stage4C15AFUOverlayTags = 0`，full-official upgrades = 0。

后续批量顺序建议：优先做 4C-15B Viktor destroyed non-minion trigger；Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback。

仍缺：Viktor destroyed non-minion trigger behavior、完整 trigger engine、same-source / same-pass / multi-destroy multiplicity and non-minion classification in real trigger contexts、隐藏 / face-down original visibility modeling、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 23. Stage 4C-15B Viktor Trigger Enqueue Overlay

4C-15B 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch15BViktorTriggerEnqueue`，并只为 `FU-b5cb36a5c9` 增加 `functionalUnits[].stage4C15B` overlay。

4C-15B 已部分降低的 blocker：

- `FU-b5cb36a5c9` / Viktor destroyed non-Minion token trigger 的最小代表性 baseline。
- 关联 cardNos：`ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- runtime path：true stack `UNIT_DESTROYED` and Starfall lethal cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` 1-power Zaun minion `OGN·273/298` with `TOKEN_FAMILY:MINION`。
- destroyed target pre-removal filter：unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily`。
- source guard：field、face-up、non-standby、same controller、not removal set。
- no-enqueue guards：destroyed minion target；hidden / face-down / standby / opponent source；source also dying。
- tests：5 new `RealTriggerQueueTests`；backend full 3380/3380 passed by A。
- 不覆盖 Kogmaw / Karthus / Undercover Agent。

矩阵数字：`stage4C15B` verified FUs = 1，verified snapshot entries = 3，cumulative real-trigger enqueue verified FUs = 11，cumulative state-based cleanup trigger enqueue verified FUs = 11，full-official upgrades = 0。

本批关闭 representative trigger enqueue baseline，但不关闭 full official trigger-count matrix 或 full trigger engine。`FU-b5cb36a5c9` 的 4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

后续批量顺序建议：Kogmaw / Karthus / Undercover Agent 继续留作 complex last-breath holdback；Viktor 仍需 full trigger-count matrix、multiplicity、visibility、FAQ regression 后才能考虑 full-official。

仍缺：full official trigger-count matrix for Viktor、完整 trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity、隐藏 / face-down original visibility modeling、Kogmaw / Karthus / Undercover Agent、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 25. Stage 4C-17 Ironclad Vanguard Trigger Enqueue Overlay

4C-17 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch17IroncladVanguardTriggerEnqueue`，并只为 `FU-6d0971786b` 增加 `functionalUnits[].stage4C17` overlay。

4C-17 已部分降低的 blocker：

- `FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard / 《铁甲先锋》的 true stack last-breath trigger enqueue 代表路径。
- trigger effect kind：`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- runtime path：true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x2` robots。
- guard：face-down / standby source no enqueue / no metadata / no token。
- P79 fixture updated to queue / priority semantics。
- tests：`RealIroncladVanguardLastBreathTriggersOrderAndCreateRobotsThroughStack`、`RealIroncladVanguardHiddenSourcesDoNotEnqueueOrCreateRobots`、`P79IroncladVanguardCreatesTwoRobotsWhenDestroyed updated`；backend full 3384/3384 passed by A。
- 不覆盖 Kogmaw / Karthus / Undercover Agent，也不覆盖 Ironclad state-based cleanup route。

矩阵数字：`stage4C17` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 13，cumulative state-based cleanup trigger enqueue verified FUs = 11，full-official upgrades = 0。

本批关闭 Ironclad Vanguard true stack representative trigger enqueue baseline，但不覆盖 state-based cleanup route、full trigger engine 或 full trigger-count matrix。`FU-6d0971786b` 的 4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

后续批量顺序建议：优先考虑安全的 cleanup route 或小型 trigger FU；Kogmaw / Karthus / Undercover Agent 继续保留为需要规则裁决的 complex last-breath holdback。

仍缺：Ironclad Vanguard cleanup route、Mechanical Trickster cleanup route、完整 trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity、隐藏 / face-down original visibility modeling、Kogmaw / Karthus / Undercover Agent、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 26. Stage 4C-18 Mechanical Trickster + Ironclad Cleanup Trigger Enqueue Overlay

4C-18 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch18MechanicalIroncladCleanupTriggerEnqueue`，并只为 `FU-1a392a4ae2` 与 `FU-6d0971786b` 增加 `functionalUnits[].stage4C18` overlay。

4C-18 已部分降低的 blocker：

- `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster / 《机械戏法师》的 state-based cleanup last-breath trigger enqueue 代表路径。
- `FU-6d0971786b` / `SFD·021/221` Ironclad Vanguard / 《铁甲先锋》的 state-based cleanup last-breath trigger enqueue 代表路径。
- runtime path：state-based cleanup `LETHAL_DAMAGE / UNIT_DESTROYED` -> last-breath trigger queued -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Mechanical `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION`; Ironclad `UNIT_TOKEN_CREATED x2` robots。
- 4B `freezeStatus` / `statusFlags` 不变，`fullOfficial=false`。
- 不覆盖 Kogmaw / Karthus / Undercover Agent，不创建不存在 FU。

矩阵数字：`stage4C18` verified FUs = 2，verified snapshot entries = 2，cumulative real-trigger enqueue verified FUs = 13，cumulative state-based cleanup trigger enqueue verified FUs = 13，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批关闭这两个 FU 的 cleanup-route representative trigger enqueue baseline，但不关闭 full trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity 或 full trigger-count matrix。

后续批量顺序建议：Kogmaw / Karthus / Undercover Agent 继续保留为需要规则裁决的 complex last-breath holdback；下一批若继续 cleanup family，应先确认 FU identity 和 FAQ 边界。

仍缺：完整 trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity、隐藏 / face-down original visibility modeling、Kogmaw / Karthus / Undercover Agent、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 27. Stage 4C-19 Kogmaw Last-Breath AoE Damage Overlay

4C-19 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch19KogmawLastBreathAoeDamage`，并只为 `FU-af8b05c294` 增加 `functionalUnits[].stage4C19` overlay。

身份核对：

- `OGN·190/298` Kogmaw / 《克格莫》对应冻结矩阵 `FU-af8b05c294`。
- 当前 oracle/effectId：`OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT`。
- 4B freezeStatus：`NEEDS_FAQ_REVIEW`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`。
- FAQ/rules refs：`JFAQ-251023 p7`。

4C-19 已部分降低的 blocker：

- visible Kogmaw last-breath AoE damage representative route。
- runtime path：visible Kogmaw last-breath source -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `AOE_DAMAGE_RESOLVED` -> `DAMAGE_CLEANUP_RUN`。
- Kogmaw remains representative only, not full-official。
- Karthus / Undercover Agent remain unmarked。

矩阵数字：`stage4C19` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 14，cumulative state-based cleanup trigger enqueue verified FUs = 13，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 Kogmaw FAQ adjudication、完整 AoE damage matrix、完整 trigger engine 或 post-damage cleanup edge cases。

后续批量顺序建议：Karthus / Undercover Agent 继续保留为需要规则裁决的 complex last-breath holdback；如继续 AoE damage family，应先补 FAQ adjudication 与 damage-prevention/replacement 边界。

仍缺：Kogmaw `JFAQ-251023 p7` FAQ adjudication、完整 trigger engine、full AoE damage target/set/prevention/replacement matrix、post-damage cleanup edge cases、Karthus / Undercover Agent、1009/811 full-official 覆盖、正式 18-step E2E。

## 28. Stage 4C-20B Undercover Agent Hand-Choice Overlay

4C-20B 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch20BUndercoverHandChoice`，并只为 `FU-6a52b04cb2` 增加 `functionalUnits[].stage4C20B` overlay。

身份核对：

- `OGN·178/298` Undercover Agent / 《卧底特工》对应冻结矩阵 `FU-6a52b04cb2`。
- 当前 oracle/effectId：`UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`。
- 4B freezeStatus：`NEEDS_ENGINE_SUPPORT`；statusFlags：`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`。
- rules refs：`CORE-260330 p62` / rule `422.4`。

4C-20B 已部分降低的 blocker：

- visible face-up field source -> Last Breath trigger -> Stack -> `HAND_CHOICE` prompt if 2+ hand -> `CHOOSE_HAND_CARDS` validation -> discard chosen / max possible -> draw two。
- 1/0 hand shortfall by `CORE-260330 p62` / rule `422.4`。
- hidden / face-down / standby source no trigger / no leak。
- automated tests：`UndercoverAgentTriggerTests`。
- A 验证结果：focused Undercover 6/6、backend full 3398/3398、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff / JSON / matrix assertions passed。
- Karthus and other FUs remain unmarked。

矩阵数字：`stage4C20B` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 15，cumulative state-based cleanup trigger enqueue verified FUs = 13，cumulative hand-choice prompt verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭通用 discard hand-choice engine、其它 hidden hand-choice FUs、完整 trigger engine 或 hidden-info 全族回归。

后续批量顺序建议：Karthus optional trigger repeat、通用 discard hand-choice engine、其它 hidden hand-choice FUs 继续作为高风险后续。

仍缺：Karthus optional trigger repeat、通用 discard hand-choice engine、其它 hidden hand-choice FUs、完整 trigger engine、hidden / face-down / standby visibility regression beyond this guard、1009/811 full-official 覆盖、正式 18-step E2E。

## 24. Stage 4C-16 Mechanical Trickster Trigger Enqueue Overlay

4C-16 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch16MechanicalTricksterTriggerEnqueue`，并只为 `FU-1a392a4ae2` 增加 `functionalUnits[].stage4C16` overlay。

4C-16 已部分降低的 blocker：

- `FU-1a392a4ae2` / `OGN·239/298` Mechanical Trickster / 《机械戏法师》的 true stack last-breath trigger enqueue 代表路径。
- trigger effect kind：`MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- runtime path：true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` for multi-trigger or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED x3` minions with `TOKEN_FAMILY:MINION`。
- guard：face-down / standby source no enqueue / no metadata / no token。
- P79 fixture updated to queue / priority semantics。
- tests：`RealMechanicalTricksterLastBreathTriggersOrderAndCreateMinionsThroughStack`、`RealMechanicalTricksterHiddenSourcesDoNotEnqueueOrCreateMinions`、`P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed updated`；backend full 3382/3382 passed by A。
- 不覆盖 Ironclad Vanguard / Kogmaw / Karthus / Undercover Agent。

矩阵数字：`stage4C16` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 12，cumulative state-based cleanup trigger enqueue verified FUs = 11，full-official upgrades = 0。

本批关闭 Mechanical Trickster true stack representative trigger enqueue baseline，但不覆盖 state-based cleanup route、full trigger engine 或 full trigger-count matrix。`FU-1a392a4ae2` 的 4B `freezeStatus` / `statusFlags` 不变，仍不能升级 full-official。

后续批量顺序建议：如果 A 明确授权，可在下一批二选一收口 Mechanical Trickster cleanup route，或继续推进 Kogmaw / Karthus / Undercover Agent 等 complex last-breath holdback；不要把 Ironclad Vanguard 误标为本批已覆盖。

仍缺：Mechanical Trickster cleanup route、完整 trigger engine、multi-source / multi-destroy / simultaneous trigger multiplicity、隐藏 / face-down original visibility modeling、Ironclad Vanguard / Kogmaw / Karthus / Undercover Agent、FAQ adjudication / regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 29. Stage 4C-21 Sunken Temple Trigger Payment Overlay

4C-21 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch21SunkenTempleTriggerPayment`，并只为 `FU-05ce012700` 增加 `functionalUnits[].stage4C21` overlay。

4C-21 已部分降低的 blocker：

- `FU-05ce012700` / `SFD·218/221` Sunken Temple / 《沉没神庙》的征服强力单位触发支付代表路径。
- route：conquer with powerful unit -> authoritative `TRIGGER_PAYMENT` / `PAY_COST` window。
- accept：`PAY_COST(SPEND_MANA:1)` 支付 1 魔力并抽 1。
- decline：`PAY_COST(DECLINE)` 关闭窗口，不支付、不抽牌。
- invalid / stale / insufficient / non-powerful paths no mutation。
- A 验证结果：focused 13/13、backend full 3404/3404、frontend build passed、Chrome smoke passed、Stage 3 preflight passed、diff check passed。

矩阵数字：`stage4C21` verified FUs = 1，verified snapshot entries = 1，cumulative trigger-payment verified FUs = 2，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭完整 PaymentEngine、complete conquer / powerful / battlefield lifecycle matrix、LayerEngine effective power edge cases、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：继续扩 triggered-cost FUs、PaymentEngine Quote / Authorize / Commit、LayerEngine effective power matrix 与 FAQ regression。

仍缺：完整 PaymentEngine、完整战场征服 / 强力时序、更多非出牌支付窗口、hidden / replay full regression、1009/811 full-official 覆盖、正式 18-step E2E。

## 30. Stage 4C-22 Muddy Dredger Warhawk Overlay

4C-22 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch22MuddyDredgerCleanupTriggerEnqueue`，并只为 `FU-b829fb32b9` 增加 `functionalUnits[].stage4C22` overlay。

4C-22 已部分降低的 blocker：

- `FU-b829fb32b9` / `UNL-153/219` Muddy Dredger / 《腐泥疏浚工》的 visible face-up state-based cleanup Last Breath representative route。
- route：state-based cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or stack priority -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` Warhawk `UNL·T02` in controller base。
- hidden / face-down / standby / invalid source no enqueue / no leak / no token。
- oracle/effectId 保持 `MUDDY_DREDGER_LAST_BREATH_WARHAWK_STATIC`；runtime representative effect kind 为 `MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK`。
- A 验证结果：focused 52/52、backend full 3407/3407、frontend build passed、Chrome smoke passed、JSON / diff check passed。

矩阵数字：`stage4C22` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 16，cumulative state-based cleanup trigger enqueue verified FUs = 14，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 true stack Muddy route、完整 Last Breath family、Warhawk “打出”完整语义、Spellshield target tax、hidden original visibility、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Aphelios / `FU-67c6b0186e` 保留为 high-payoff next candidate；继续扩 Last Breath / Warhawk / Spellshield 相关 FUs、hidden source policy 与 FAQ regression。

仍缺：完整 trigger engine、complete APNAP / trigger batch、complete Last Breath / destroyed family、Spellshield target tax full matrix、1009/811 full-official 覆盖、正式 18-step E2E。

## 31. Stage 4C-23 Lux High-Cost Spell Power Overlay

4C-23 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch23LuxHighCostSpellTriggerPower`，并只为 `FU-f18a49e06d` 增加 `functionalUnits[].stage4C23` overlay。

4C-23 已部分降低的 blocker：

- `FU-f18a49e06d` / `OGS·006/024` Lux / 《拉克丝》的 high-cost spell temporary power representative route。
- route：controller plays cost >= 5 spell -> visible face-up Lux source -> `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +3。
- low-cost spell / opponent spell / face-down / standby / invalid source no trigger / no mutation。
- oracle/effectId 保持 `OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT`；runtime representative effect kind 为 `OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3`。
- A 验证结果：focused 67/67、backend full 3413/3413、frontend build passed、Chrome smoke passed、JSON / diff check passed。

矩阵数字：`stage4C23` verified FUs = 1，verified snapshot entries = 1，cumulative real-trigger enqueue verified FUs = 16; cumulative spell-played immediate trigger-event verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭完整 high-cost spell family、paid-cost override、PaymentEngine、LayerEngine temporary modifier full matrix、complete trigger engine、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Aphelios / `FU-67c6b0186e` 保留为 dedicated weapon-attachment three-mode candidate；Icevale Archer / Vayne 继续作为 triggered-cost / conquer pressure candidates。

仍缺：完整 trigger engine、PaymentEngine paid-cost matrix、LayerEngine temporary modifier matrix、hidden original visibility、1009/811 full-official 覆盖、正式 18-step E2E。

## 32. Stage 4C-24 Vayne Conquer Pay-One Recall Overlay

4C-24 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch24VayneConquerPayOneRecall`，并只为 `FU-c027639a3c` 增加 `functionalUnits[].stage4C24` overlay。

4C-24 已部分降低的 blocker：

- `FU-c027639a3c` / `OGN·035/298` Vayne / 《薇恩》的 visible face-up conquer payment recall representative route。
- route：visible face-up Vayne source -> conquer event -> `TRIGGER_PAYMENT` / `PAY_COST` pay 1 -> accepted payment returns Vayne herself to owner hand。
- decline / invalid source no recall / no payment / no mutation。
- oracle/effectId 保持 `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT`。
- B 验证结果：4C-24 服务端代码已完成，focused 52/52 passed。

矩阵数字：`stage4C24` verified FUs = 1，verified snapshot entries = 1，cumulative trigger-payment verified FUs = 3，cumulative conquer-payment recall verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 SFD reprint / promo family full-official、Assault3、active-entry、complete conquer/control-zone movement、hidden/random full matrix、PaymentEngine full matrix、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Aphelios / `FU-67c6b0186e` 保留为 dedicated weapon-attachment three-mode candidate；Icevale Archer / `FU-c170628e3a` 继续作为 attack trigger payment target-selection candidate。

仍缺：完整 trigger engine、PaymentEngine paid-cost matrix、complete Assault3 / conquer / control-zone movement、hidden/random full matrix、1009/811 full-official 覆盖、正式 18-step E2E。

## 33. Stage 4C-25 Icevale Archer Attack Payment Overlay

4C-25 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch25IcevaleArcherAttackPaymentTargetSelection`，并只为 `FU-c170628e3a` 增加 `functionalUnits[].stage4C25` overlay。

4C-25 已部分降低的 blocker：

- `FU-c170628e3a` / `UNL-065/219` Icevale Archer / 《冰谷弓箭手》的 active start-battle attack trigger payment target-selection representative route。
- route：active start-battle task -> visible face-up Icevale attacks -> `DeclareBattleCommand.BattlefieldTargetObjectIds` preselects same-battlefield face-up unit target -> `TRIGGER_PAYMENT` / `PAY_COST` pay 1 -> accepted payment gives target -1 power until end of turn。
- decline / invalid target / invalid source no payment / no mutation / no leak。
- oracle/effectId 保持 `ICEVALE_ARCHER_ATTACK_PAYMENT_PLAY_UNIT`；runtime representative effect kind 为 `ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1`。
- A 验证结果：focused 102/102、backend full 3429/3429、frontend build passed、Chrome smoke passed、JSON / diff check passed。

矩阵数字：`stage4C25` verified FUs = 1，verified snapshot entries = 1，cumulative trigger-payment verified FUs = 4，cumulative attack-payment target-selection verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete attack/battle lifecycle、target prompt selection UI、FEPR target legality matrix、complete PaymentEngine、Spellshield target tax、LayerEngine temporary modifier matrix、hidden original visibility、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Aphelios / `FU-67c6b0186e` 保留为 dedicated weapon-attachment three-mode candidate。Icevale Archer 已有 4C-25 代表覆盖，但 full-official blocker 仍保留。

仍缺：完整 trigger engine、PaymentEngine paid-cost matrix、battle/attack lifecycle、targeting matrix、LayerEngine temporary modifier matrix、hidden original visibility、1009/811 full-official 覆盖、正式 18-step E2E。

## 34. Stage 4C-26 Jax Weapon Attach Pay-One Draw-One Overlay

4C-26 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch26JaxWeaponAttachPayOneDrawOne`，并只为 `FU-73f3be35df` 增加 `functionalUnits[].stage4C26` overlay。

4C-26 已部分降低的 blocker：

- `FU-73f3be35df` / `SFD·119/221` + `SFD·119a/221` Jax / 《贾克斯》的 weapon / armament attach trigger payment draw representative route。
- route：visible face-up Jax source -> weapon / armament attached to Jax -> `TRIGGER_PAYMENT` / `PAY_COST` pay 1 -> `SPEND_MANA:1` -> draw 1。
- `DECLINE` no draw / no mutation；non-Jax / non-armament no prompt；hidden / face-down / standby / opponent-controlled source no leak；insufficient payment no draw。
- oracle/effectIds 保持 `SFD_119_JAX_ALT_A_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`、`SFD_119_JAX_NO_OPTIONAL_ASSEMBLE_PLAY_UNIT`。
- FAQ refs `SOUL-JFAQ-260114 p20`、`SOUL-OFAQ-260114 p11` 仍需 FAQ review；4C-26 不关闭 `百炼` / Assemble 或 attachment FAQ adjudication。

矩阵数字：`stage4C26` verified FUs = 1，verified snapshot entries = 2，cumulative trigger-payment verified FUs = 5，cumulative weapon-attachment payment-draw verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete weapon / equipment attachment rules、complete `百炼` / Assemble flow、SFD-119 family full-official、complete PaymentEngine、hidden/random-zone draw matrix、LayerEngine continuous-effect interactions、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Aphelios / `FU-67c6b0186e` 继续 design-gated，等待 mode-choice / mode-memory 契约；Jax 已有 4C-26 代表覆盖，但 full-official blocker 仍保留。

仍缺：完整 trigger engine、PaymentEngine paid-cost matrix、weapon / equipment attachment matrix、hidden source visibility、hidden/random draw matrix、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 35. Stage 4C-27 Treasure Hunter Move Create Dormant Gold Overlay

4C-27 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch27TreasureHunterMoveCreateDormantGold`，并只为 `FU-6144ab0271` 增加 `functionalUnits[].stage4C27` overlay。

4C-27 已部分降低的 blocker：

- `FU-6144ab0271` / `SFD·130/221` Treasure Hunter / 《寻宝猎人》的 movement trigger create dormant Gold representative route。
- route：visible face-up Treasure Hunter source -> Treasure Hunter moved -> movement trigger evaluated -> create / play 1 dormant Gold equipment token。
- non-Treasure Hunter / non-move no Gold；hidden / face-down / standby / opponent-controlled source no trigger / no leak。
- oracle/effectId 保持 `TREASURE_HUNTER_MOVE_GOLD_PLAY_UNIT`。
- rules / FAQ refs `CORE-260330 p48`、`SOUL-JFAQ-260114 p21` 仍需 review；4C-27 不关闭 movement 或 Gold-token FAQ adjudication。

矩阵数字：`stage4C27` verified FUs = 1，verified snapshot entries = 1，cumulative movement-Gold creation verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete ZoneOwnership / ControlChange / Movement matrix、complete move-trigger source family、complete Gold equipment token creation / destination matrix、hidden / face-down / standby / opponent-controlled source visibility model、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 保持 untagged / design-gated，等待 last-breath static extra-trigger repeat semantics 与 FAQ adjudication；Aphelios / `FU-67c6b0186e` 继续等待 mode-choice / mode-memory 契约。

仍缺：完整 movement matrix、Gold token destination matrix、hidden source visibility、Karthus static last-breath extra-trigger semantics、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 36. Stage 4C-28 Battle or Flight Move To Owner Base Overlay

4C-28 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch28BattleOrFlightMoveBattlefieldUnitToOwnerBase`，并只为 `FU-813144e7d4` 增加 `functionalUnits[].stage4C28` overlay。

4C-28 已部分降低的 blocker：

- `FU-813144e7d4` / `OGN·168/298` Battle or Flight / 《战或逃》的 targeted battlefield unit move-to-owner-base representative route。
- route：Battle or Flight played -> valid face-up battlefield unit target selected -> target guard hardened -> move target to owner base。
- non-battlefield / non-unit / hidden / face-down / standby / invalid target no move / no leak。
- oracle/effectId 保持 `BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE`。
- rules / FAQ refs `CORE-260330 p46`、`JFAQ-251023 p4`、`SOUL-JFAQ-260114 p12`、`SOUL-JFAQ-260114 p16` 仍需 review；4C-28 不关闭 movement、battle/spell timing 或 FAQ adjudication。

矩阵数字：`stage4C28` verified FUs = 1，verified snapshot entries = 1，cumulative targeted movement-to-owner-base verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete spell-duel / battle lifecycle、complete FEPR target selection / target-change matrix、complete ZoneOwnership / ControlChange / Movement matrix、hidden / face-down / standby target visibility model、PaymentEngine full matrix、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为后续候选；Karthus / `FU-ee1dfb3ed3` 继续 untagged / design-gated。

仍缺：完整 targeting matrix、movement/control-zone matrix、battle/spell timing、hidden visibility、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 37. Stage 4C-29 Gust Return To Owner Hand Target Guard Overlay

4C-29 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch29GustReturnPowerThreeOrLessToOwnerHand`，并只为 `FU-48662b7661` 增加 `functionalUnits[].stage4C29` overlay。

4C-29 已部分降低的 blocker：

- `FU-48662b7661` / `OGN·169/298` Gust / 《罡风》的 public battlefield unit power <= 3 return-to-owner-hand representative route。
- route：Gust played -> valid public battlefield unit power <= 3 target selected -> service-authoritative target guard -> return target to owner hand。
- invalid target guards：power > 3、non-battlefield / base unit、stale object、face-down standby、battlefield equipment no return / no leak。
- oracle/effectId 保持 `GUST_RETURN_BATTLEFIELD_UNIT_POWER_3_OR_LESS_TO_HAND`。
- Focused：Gust / Return / Hand 112/112 passed；Gust / BattleOrFlight 13/13 passed。
- full backend 3458/3458、frontend build passed、Chrome smoke passed；不以 focused tests 代替正式 18-step E2E。

矩阵数字：`stage4C29` verified FUs = 1，verified snapshot entries = 1，cumulative return-to-owner-hand target-guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 all ReturnsTargetToHand cards、full Gust official completion beyond this representative guard slice、complete FEPR target selection / target-change matrix、complete ZoneOwnership / ControlChange / Movement matrix、hidden / face-down / standby target visibility model、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、return-to-hand card family matrix、movement/control-zone matrix、hidden visibility、1009/811 full-official 覆盖、正式 18-step E2E。

## 38. Stage 4C-30 Hunt the Weak Destroy Target Guard Overlay

4C-30 只更新覆盖矩阵 / 风险证据，不升级 full-official。`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已新增 `stage4CBatch30HuntTheWeakDestroyPowerThreeOrLessGuard`，并只为 `FU-282b6e3149` 增加 `functionalUnits[].stage4C30` overlay。

4C-30 已部分降低的 blocker：

- `FU-282b6e3149` / `UNL-159/219` Hunt the Weak / 《狩魂》的 public battlefield unit power <= 3 destroy representative route。
- route：Hunt the Weak played -> valid public battlefield unit power <= 3 target selected -> service-authoritative target guard -> destroy target。
- invalid target guards：power > 3、non-battlefield / base unit、stale object、face-down standby、battlefield equipment no destroy / no leak。
- oracle/effectId 保持 `HUNT_THE_WEAK_DESTROY_BATTLEFIELD_UNIT_POWER_3_OR_LESS`。
- Focused：Hunt the Weak focused guard 34/34 passed；adjacent guard 19/19 passed。
- full backend 3464/3464、frontend build passed、Chrome smoke passed；不以 focused tests / smoke 代替正式 18-step E2E。

矩阵数字：`stage4C30` verified FUs = 1，verified snapshot entries = 1，cumulative destroy-target guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 all destroy-target cards、full Hunt the Weak official completion beyond this representative guard slice、complete FEPR target selection / target-change matrix、replacement / prevention / cleanup after destruction matrix、hidden / face-down / standby target visibility model、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、destroy-target card family matrix、replacement / prevention / cleanup matrix、hidden visibility、1009/811 full-official 覆盖、正式 18-step E2E。

## 39. Stage 4C-31 Reprimand Return To Owner Hand Target Guard Overlay

4C-31 只更新覆盖矩阵 / 风险证据 prose，不升级 full-official。正确身份为 `FU-d0383ed260` / `OGN·172/298` / cardId `31402` / Reprimand / 《责退》，oracle/effectId 为 `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND`。

4C-31 已部分降低的 blocker：

- valid public battlefield unit target -> service-authoritative guard -> return target to owner hand。
- invalid target guards：base unit、stale object、face-down standby、battlefield equipment、battlefield spell object、battlefield rune object no mutation / no leak。
- 验证结果：focused 58/58 passed；adjacent guard 24/24 passed；backend full 3471/3471 passed；frontend build passed；Chrome smoke passed。
- 4B primary status / flags 不变，`fullOfficial=false`，full-official upgrades = 0。

矩阵数字口径：`stage4C31` verified FUs = 1，verified snapshot entries = 1，cumulative return-to-owner-hand target-guard verified FUs = 2，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete FEPR targeting / target legality matrix、complete movement / control-zone matrix、hidden visibility / face-down / standby visibility model、all return-to-hand family、FAQ adjudication、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、return-to-hand card family matrix、movement/control-zone matrix、hidden visibility、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 40. Stage 4C-32 Ride the Wind Move To Owner Base Ready Guard Overlay

4C-32 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-6f84196631` / `OGN·173/298` / cardId `31403` / Ride the Wind / 《驭风而行》，oracle/effectId 为 `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY`。

4C-32 已部分降低的 blocker：

- friendly public battlefield unit target -> service-authoritative guard -> ready target -> move target to owner base。
- invalid target guards：enemy battlefield unit、friendly base unit、stale unit、face-down standby、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object no mutation / no leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，full-official upgrades = 0。

矩阵数字口径：`stage4C32` verified FUs = 1，verified snapshot entries = 1，cumulative targeted movement-to-owner-base verified FUs = 2，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete movement / roam / precise battlefield model、FAQ adjudication for `JFAQ-251023 p4`、all movement spell family、complete FEPR targeting / target legality matrix、hidden visibility、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、movement / roam / precise battlefield model、movement spell family matrix、hidden visibility、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 41. Stage 4C-33 Charm Move To Owner Base Target Guard Overlay

4C-33 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-1586b6cdd9` / `OGN·043/298` / cardId `31255` / Charm / 《魅惑妖术》，oracle/effectId 为 `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE`。

4C-33 已部分降低的 blocker：

- enemy public battlefield unit target -> service-authoritative guard -> move target to owner base。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object no mutation / no leak。
- 4B primary status / flags 不变：`NEEDS_ENGINE_SUPPORT`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`；`fullOfficial=false`，full-official upgrades = 0。

矩阵数字口径：`stage4C33` verified FUs = 1，verified snapshot entries = 1，cumulative targeted movement-to-owner-base verified FUs = 3，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete movement / roam / precise battlefield model、all movement spell family、complete FEPR targeting / target legality matrix、hidden visibility、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、movement / roam / precise battlefield model、movement spell family matrix、hidden visibility、1009/811 full-official 覆盖、正式 18-step E2E。

## 42. Stage 4C-34 Isolate Move To Owner Base No-Draw Target Guard Overlay

4C-34 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-175d573ae4` / `UNL-124/219` / cardId `34667` / Isolate / 《隔绝》，oracle/effectId 为 `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW`。

4C-34 已部分降低的 blocker：

- enemy public battlefield unit target -> service-authoritative guard -> move target to owner base -> no card draw。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object no mutation / no draw / no leak。
- 4B primary status / flags 不变：`NEEDS_ENGINE_SUPPORT`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`；`fullOfficial=false`，full-official upgrades = 0。

矩阵数字口径：`stage4C34` verified FUs = 1，verified snapshot entries = 1，cumulative targeted movement-to-owner-base verified FUs = 4，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 Isolate isolated-enemy draw branch、complete movement / roam / precise battlefield model、all movement spell family、complete FEPR targeting / target legality matrix、hidden visibility、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Vengeance / `FU-07104fa58a` 可作为下一低耦合 destroy-target 候选；Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 target legality matrix、movement / roam / precise battlefield model、movement spell family matrix、hidden visibility、Isolate draw branch、1009/811 full-official 覆盖、正式 18-step E2E。

## 43. Stage 4C-35 Vengeance Destroy Unit Target Guard Overlay

4C-35 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-07104fa58a` / `OGN·229/298` / cardId `31467` / Vengeance / 《复仇》，oracle/effectId 为 `VENGEANCE_DESTROY_UNIT`。

4C-35 已部分降低的 blocker：

- public unit target -> service-authoritative guard -> destroy target to owner graveyard。
- valid target guards：friendly / enemy public unit targets in base / battlefield 均可摧毁。
- invalid target guards：stale unit、face-down standby、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit no mutation / no destroy / no leak。
- 4B primary status / flags 不变：`NEEDS_ENGINE_SUPPORT`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`；`fullOfficial=false`，full-official upgrades = 0。

矩阵数字口径：`stage4C35` verified FUs = 1，verified snapshot entries = 1，cumulative destroy-target guard verified FUs = 2，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete destroy / cleanup / replacement / prevention / Last Breath interaction、complete FEPR target selection / target legality matrix、hidden visibility、Spellshield target tax、attached-equipment detach / replacement breadth、destroyed-this-turn memory、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Hostile Takeover / `FU-00ee09c2cc`、Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 destroy family matrix、完整 target legality matrix、replacement / prevention / Last Breath timing、hidden visibility、1009/811 full-official 覆盖、正式 18-step E2E。


## 44. Stage 4C-36 Hostile Takeover Control Ready Target Guard Overlay

4C-36 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-00ee09c2cc` / `SFD·202/221` / cardId `33301` / Hostile Takeover / 《恶意收购》，oracle/effectId 为 `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT`。

4C-36 已部分降低的 blocker：

- enemy public battlefield unit target -> service-authoritative guard -> gain control -> ready target。
- invalid target guards：friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object no mutation / no control change / no leak。
- 4B primary status / flags 不变：`IMPLEMENTED_TESTED`，`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C36` verified FUs = 1，verified snapshot entries = 1，cumulative control-ready target guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete control-change matrix、ready / exhaust semantics、complete FEPR target selection / target legality matrix、payment / battle / hidden-zone interactions、FAQ adjudication for `SOUL-JFAQ-260114 p22` / `SOUL-OFAQ-260114 p21`、all gain-control cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Berserk Impulse / `FU-b05eda44ce`、Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 control-change matrix、ready / exhaust semantics、完整 target legality matrix、payment / battle / hidden-zone interactions、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 45. Stage 4C-37 Berserk Impulse Opponent Top Unit Guard Overlay

4C-37 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-b05eda44ce` / `OGN·025/298` / cardId `31231` / Berserk Impulse / 《暴怒冲动》，oracle/effectId 为 `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT`。

4C-37 已部分降低的 blocker：

- valid opponent top main-deck unit -> service-authoritative guard -> representative play-opponent-top-unit route。
- invalid guards：opponent top non-unit、non-top object、friendly deck object、private / unauthorized object、face-down / hidden invalid object、dirty resolution / stale top object no play / no mutation / no leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ ref remains open，full-official upgrades = 0。

矩阵数字口径：`stage4C37` verified FUs = 1，verified snapshot entries = 1，cumulative opponent top-unit play guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 complete opponent-deck reveal / choice / recycle semantics、complete hidden-zone visibility matrix、complete FEPR timing / target selection matrix、payment / battle / control-zone interactions、FAQ adjudication for `SOUL-OFAQ-260114 p4`、all opponent-deck play effects、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Edge of Night / `FU-804412488c` 继续保留为高风险后续候选；Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated。

仍缺：完整 hidden-zone reveal / choice / recycle semantics、完整 target legality matrix、payment / battle / control-zone interactions、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 46. Stage 4C-38 Edge of Night Play Equipment Assemble Guard Overlay

4C-38 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-804412488c` / `SFD·139/221` / cardId `33229` / Edge of Night / 《夜之锋刃》，oracle/effectId 为 `EDGE_OF_NIGHT_PLAY_EQUIPMENT`。

4C-38 已部分降低的 blocker：

- ordinary play-equipment hand route：0 target -> stack / pass-pass -> base equipment。
- explicit play target rejected without payment or mutation。
- valid `ASSEMBLE_PURPLE` route：face-up controlled base Edge of Night -> friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`。
- invalid guards：face-down / hidden source、source in hand、opponent source、already-attached source、unknown source、unknown / opponent / face-down standby / non-unit target、missing / wrong optional cost、insufficient purple no tick / no events / no payment / no stack / no attach / no leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C38` verified FUs = 1，verified snapshot entries = 1，cumulative equipment assemble guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 full Edge of Night official completion、standby immediate attach、complete hidden-zone visibility / redaction、complete equipment layer / attach / detach / replacement matrix、FAQ adjudication for `SOUL-OFAQ-260114 p10` / `SOUL-OFAQ-260114 p9`、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated；下一批应先做新的只读门禁，避免在缺协议解释时推进。

仍缺：完整 standby immediate attach、完整 equipment lifecycle / LayerEngine、hidden-zone prompt / redaction、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 47. Stage 4C-39 Zhonyas Hourglass Play Guard Overlay

4C-39 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-fb79eea7fc` / `OGN·077/298` / cardId `31291` / Zhonya's Hourglass / 《中娅沙漏》，oracle/effectId 为 `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT`。

4C-39 已部分降低的 blocker：

- ultra-narrow representative play-equipment guard baseline for Zhonya's Hourglass。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C39` verified FUs = 1，verified snapshot entries = 1，cumulative equipment play guard verified FUs = 2，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 full standby / reaction semantics、destroy replacement matrix、recall interactions、complete equipment layer / continuous-effect matrix、FAQ adjudication for Zhonya refs、all equipment cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated；下一批应先做新的只读门禁。

仍缺：完整 standby / reaction、destroy replacement、recall interactions、equipment lifecycle / LayerEngine、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 48. Stage 4C-40 Sea Monster Hook Play Equipment Guard Overlay

4C-40 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-2653af0380` / `OGN·242/298` / cardId `31482` / Sea Monster Hook / 《海兽钓钩》，oracle/effectId 为 `SEA_MONSTER_HOOK_PLAY_EQUIPMENT`。

4C-40 已部分降低的 blocker：

- ultra-narrow representative play-equipment guard baseline for Sea Monster Hook。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C40` verified FUs = 1，verified snapshot entries = 1，cumulative equipment play guard verified FUs = 3，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 activated ability semantics、top-five search / reveal / choice semantics、free play route、recycle route、complete equipment layer / continuous-effect matrix、FAQ adjudication for Sea Monster Hook refs、all equipment cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated；下一批应先做新的只读门禁。

仍缺：activated ability、top-five / free play / recycle、equipment lifecycle / LayerEngine、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 49. Stage 4C-41 Giant Arm Kato Play Keyword Unit Guard Overlay

4C-41 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-464ec8c275` / `SFD·112/221` / cardId `33198` / Giant Arm Kato / 《巨腕加藤》，oracle/effectId 为 `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT`。

4C-41 已部分降低的 blocker：

- ultra-narrow representative play-keyword-unit guard baseline for Giant Arm Kato。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C41` verified FUs = 1，verified snapshot entries = 1，cumulative play-keyword-unit guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 Spellshield target tax、move trigger、keyword grant semantics、+power until EOT semantics、complete LayerEngine / continuous-effect matrix、FAQ adjudication for Giant Arm Kato refs、all keyword-unit cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated；下一批应先做新的只读门禁。

仍缺：Spellshield target tax、move trigger、keyword grant、+power until EOT、LayerEngine、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 50. Stage 4C-42 Time Gate Play Equipment Guard Overlay

4C-42 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-081d97eb3e` / `SFD·078/221` / cardId `33158` / Time Gate / 《预时之门》，oracle/effectId 为 `TIME_GATE_PLAY_EQUIPMENT`。

4C-42 已部分降低的 blocker：

- ultra-narrow representative play-equipment guard baseline for Time Gate。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C42` verified FUs = 1，verified snapshot entries = 1，cumulative equipment play guard verified FUs = 4，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 activated/tap ability、next spell Echo semantics、optional echo payment/repeat route、duration cleanup、FAQ adjudication for Time Gate refs、all equipment cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Karthus / `FU-ee1dfb3ed3` 与 Aphelios / `FU-67c6b0186e` 继续 design-gated；下一批应先做新的只读门禁。

仍缺：activated/tap ability、next spell Echo、optional echo payment/repeat、duration cleanup、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 51. Stage 4C-43 Sfur Song Play Equipment Guard Overlay

4C-43 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-9a623b3185` / `SFD·059/221` / cardId `33139` / Sfur Song / 《斯弗尔尚歌》，oracle/effectId 为 `SFUR_SONG_PLAY_EQUIPMENT`。

4C-43 已部分降低的 blocker：

- ultra-narrow representative play-equipment guard baseline for Sfur Song。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C43` verified FUs = 1，verified snapshot entries = 1，cumulative equipment play guard verified FUs = 5，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 copied host skill text、continuous text / LayerEngine semantics、complete assemble / equipment attach lifecycle、equipment control / zone movement、FAQ full behavior for Sfur Song refs、all equipment cards、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Akshan / `FU-7419ee7d9d` 保持下一单 entry direct-card candidate；Switcheroo / `FU-0b6332bbf0` 可作为 tested-but-high-risk guard overlay candidate；Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 继续等待 legend-domain/shared-oracle design batch。

仍缺：copied host skill text、continuous text / LayerEngine、complete assemble / attach lifecycle、equipment control / zone movement、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 52. Stage 4C-44 Akshan Play Unit Guard Overlay

4C-44 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-7419ee7d9d` / `SFD·109/221` / cardId `33194` / Akshan / 《阿克尚》，oracle/effectId 为 `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT`。

4C-44 已部分降低的 blocker：

- ultra-narrow representative play-unit guard baseline for Akshan。
- invalid source / target / cost rejected without mutation or leak。
- 4B primary status / flags 不变：`NEEDS_FAQ_REVIEW`，`IMPLEMENTED_UNTESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C44` verified FUs = 1，verified snapshot entries = 1，cumulative play-unit guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 optional assemble、extra play semantics、cleanup / replacement / duration matrix、movement / control-zone matrix、payment / cost windows、targeting / stack timing、LayerEngine / continuous effects、FAQ adjudication for Akshan refs、all Akshan official text、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Switcheroo / `FU-0b6332bbf0` 保持 tested-but-high-risk guard overlay candidate；Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 继续等待 legend-domain/shared-oracle design batch。

仍缺：optional assemble、extra play、cleanup/replacement/duration、movement/control-zone、payment/cost、targeting/stack timing、LayerEngine、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 53. Stage 4C-45 Switcheroo Battlefield Power-Swap Guard Overlay

4C-45 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-0b6332bbf0` / `SFD·145/221` / cardId `33237` / Switcheroo / 《换换乐》，oracle/effectId 为 `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS`。

4C-45 已部分降低的 blocker：

- ultra-narrow representative battlefield power-swap guard baseline for Switcheroo。
- invalid source / target / timing rejected without mutation or leak。
- 4B primary status / flags 不变：`IMPLEMENTED_TESTED`，`IMPLEMENTED_TESTED`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C45` verified FUs = 1，verified snapshot entries = 1，cumulative battlefield power-swap guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 full battle math、LayerEngine / continuous effect semantics、cleanup / replacement / duration matrix、hidden / random-zone behavior、payment / timing matrix、FAQ adjudication for Switcheroo refs、all Switcheroo official text、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Void Burrower / `FU-6e7d0dba2c` 与 Sett / `FU-6308c2db01` 继续等待 legend-domain/shared-oracle design batch。

仍缺：full battle math、LayerEngine、cleanup/replacement/duration、hidden/random-zone behavior、payment/timing、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 54. Stage 4C-46 Void Burrower Legend-Domain Design Gate Overlay

4C-46 只更新覆盖矩阵 / 风险证据，不记录 verified implementation，不升级 full-official。正确身份为 `FU-6e7d0dba2c` / representative `SFD·187/221` / cardId `33285` / Void Burrower / 《虚空遁地兽》，snapshot entries 为 `SFD·187/221` / `33285` 与 `SFD·243/221` / `33354`，oracle/effectId 为 `LEGEND_ACTION_DOMAIN`。

4C-46 已记录的 design gate：

- NO-GO direct implementation for Void Burrower `LEGEND_ACTION_DOMAIN` shared-oracle route。
- required design gates：`LegendActivePredicate`、`LegendOptionalTrigger`、`RevealChoice`、`ReplacementPayment`、shared-oracle mapping、hidden/reveal redaction。
- 4B primary status / flags 不变：`SHARED_ORACLE_IMPLEMENTATION`，`IMPLEMENTED_UNTESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C46` design-gated FUs = 1，design-gated snapshot entries = 2，verified FUs = 0，verified snapshot entries = 0，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 direct implementation、verified runtime slice、full legend action domain、battle / spell duel lifecycle、movement / control-zone matrix、hidden / reveal / redaction matrix、LayerEngine / continuous effects、targeting / stack timing、FAQ adjudication for Void Burrower refs、Sett follow-on design、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Sett / `FU-6308c2db01` 保持 follow-on `LEGEND_ACTION_DOMAIN` shared-oracle design candidate；如果 A 需要低耦合实现切片，应另开只读门禁挑单 entry direct-card behavior。

仍缺：LegendActivePredicate、LegendOptionalTrigger、RevealChoice、ReplacementPayment、shared-oracle mapping、hidden/reveal redaction、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 55. Stage 4C-47 Draven Battle Body Guard Overlay

4C-47 只更新覆盖矩阵 / 风险证据，不升级 full-official。正确身份为 `FU-964b214448` / representative `SFD·020/221` / cardId `33092` / Draven / 《德莱文》，snapshot entries 为 `SFD·020/221` / `33092` 与 `SFD·020a/221` / `33093`，oracle/effectId 为 `SFD_020_DRAVEN_VANILLA_PLAY_UNIT` / `SFD_020A_DRAVEN_VANILLA_PLAY_UNIT`。

4C-47 已部分降低的 blocker：

- ultra-narrow representative battle body / play-unit guard baseline for Draven shared-oracle FU。
- invalid source / target / timing rejected without mutation or leak。
- 4B primary status / flags 不变：`IMPLEMENTED_TESTED`，`IMPLEMENTED_TESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT`、`NEEDS_FAQ_REVIEW`；`fullOfficial=false`，FAQ refs remain open，full-official upgrades = 0。

矩阵数字口径：`stage4C47` verified FUs = 1，verified snapshot entries = 2，cumulative battle-body guard verified FUs = 1，full-official upgrades = 0，full-official still uncovered FUs = 811。

本批不关闭 battle win Gold、attack / defense red payment、+2 until EOT、PaymentEngine、Layer / duration、battle lifecycle full matrix、FAQ adjudication for Draven refs、all Draven official text、1009/811 full-official 或正式 18-step E2E。

后续批量顺序建议：Vex / `FU-9f7cb73dc4` 可作为 lower-coupling single-entry direct-card candidate；Sett / `FU-6308c2db01` 继续等待 `LEGEND_ACTION_DOMAIN` shared-oracle design batch。

仍缺：battle win Gold、attack/defense red payment、+2 until EOT、PaymentEngine、Layer/duration、battle lifecycle full matrix、FAQ adjudication、1009/811 full-official 覆盖、正式 18-step E2E。

## 56. Top20 高风险 Functional Units

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

## 57. 未覆盖效果分类

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

## 58. P0/P1 仍未清零

P0：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- `PAY_COST` 已有 3A 最小 runtime，4C-4 已验证 `SFD·220/221` `TRIGGER_PAYMENT` 支付 / 拒付代表路径，4C-21 已验证 `SFD·218/221` Sunken Temple 征服强力单位 `TRIGGER_PAYMENT` / `PAY_COST` 支付抽牌代表路径，`ASSIGN_COMBAT_DAMAGE` 已有 3C 最小 runtime，`ORDER_TRIGGERS` 已升级为 4C-1 保守 APNAP controller-block 子集，4C-2 / 4C-3 只验证 Watchful Sentinel 与 Honest Broker real trigger enqueue，4C-5 / 4C-6 只验证 visible Watchful Sentinel 与 visible Honest Broker 的 state-based cleanup trigger enqueue，4C-7 / 4C-8 只验证 visible Scouting Warhawk explicit destroy 与 state-based cleanup trigger enqueue，4C-9 只验证 visible Sad/Loyal Poro conditional cleanup trigger enqueue，4C-10 只验证 visible Unsung Hero powerful cleanup trigger enqueue，4C-11 只验证 visible surviving friendly Ghostly Centaur friendly-destroyed cleanup trigger enqueue，4C-12 只验证 visible surviving friendly Resonant Soul first-friendly-destroyed cleanup trigger enqueue，4C-13 只迁移 Ghostly / Resonant true stack destruction non-cleanup route，4C-14 只验证 Savage Jawfish true stack / cleanup friendly-destroyed experience trigger enqueue，4C-15A 只记录 Minion token family infrastructure，4C-15B 只验证 Viktor destroyed non-Minion representative trigger enqueue baseline，4C-16 / 4C-17 只验证 Mechanical Trickster / Ironclad Vanguard true stack last-breath representative trigger enqueue baseline，4C-18 只验证这两个 FU 的 cleanup-route representative trigger enqueue baseline，4C-19 只验证 Kogmaw visible last-breath AoE damage representative route，4C-20B 只验证 Undercover Agent triggered hand-choice prompt 微切片，4C-22 只验证 Muddy Dredger state-based cleanup Warhawk token representative route；完整 PaymentEngine、完整 damage assignment 全规则矩阵、完整 trigger engine / battle initial stack 全规则仍未完成。
- 4C-29 / 4C-30 / 4C-31 / 4C-32 / 4C-33 / 4C-34 / 4C-35 / 4C-36 / 4C-37 / 4C-38 / 4C-39 / 4C-40 / 4C-41 / 4C-42 / 4C-43 / 4C-44 / 4C-45 / 4C-47 只验证 Gust return-to-owner-hand、Hunt the Weak destroy-target、Reprimand return-to-owner-hand、Ride the Wind ready/move-to-base、Charm move-to-base、Isolate move-to-base no-draw、Vengeance destroy-target、Hostile Takeover control-ready、Berserk Impulse opponent top-unit play、Edge of Night play-equipment / assemble-purple、Zhonya's Hourglass play-equipment guard、Sea Monster Hook play-equipment guard、Giant Arm Kato play-keyword-unit guard、Time Gate play-equipment guard、Sfur Song play-equipment guard、Akshan play-unit guard、Switcheroo battlefield power-swap guard 与 Draven battle body / play-unit guard 的代表切片；4C-46 只记录 Void Burrower legend-domain shared-oracle design gate，verified implementation 仍为 0。完整 target legality、movement/control-zone、movement/roam、replacement / prevention / cleanup、visibility / reveal / recycle、control-change、equipment layer、activated ability、top-five/free-play/recycle、Spellshield target tax、keyword grant、+power until EOT、next spell Echo、optional echo payment/repeat、duration cleanup、copied host skill text、complete assemble / attach lifecycle、optional assemble、extra play semantics、full battle math、power-swap layer semantics、battle win Gold、attack/defense red payment、legend-domain active/optional/reveal/payment/redaction design、FAQ adjudication 与全 card-family matrix 仍未完成。
- 正式 18 步 E2E 未最终收口。
- 1009 entries / 811 FUs 的 FAQ 证据与 full-official 测试矩阵未完成。

P1：

- PaymentEngine 仍未统一到完整官方支付窗口。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- FAQ 候选页码尚未全部人工 adjudicate 为“适用 / 不适用 / 通用规则”。
- 当前实现映射仍是 representative route，不能升级为 `full-official-rule-pass`。

是否允许批量覆盖：**不允许。**
