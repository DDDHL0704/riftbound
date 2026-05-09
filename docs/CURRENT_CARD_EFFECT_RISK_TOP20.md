# 当前卡牌效果高风险 Top20

更新时间：2026-05-09

阶段：**阶段 3B / E 卡牌覆盖矩阵与 Battlefield lifecycle 证据 overlay**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文以阶段 2 风险排序为基础，并叠加阶段 3A/3B 的最小证据 overlay；它不是功能实现清单，也不是错误断言。排名用于告诉后续阶段先审哪里：哪些 functional unit 同时碰到 FAQ、费用、触发/替换、持续效果、战斗/法术对决、隐藏信息或非 PLAY_CARD 规则域。

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

## 5. Top20 高风险 Functional Units

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

## 6. 未覆盖效果分类

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

## 7. P0/P1 仍未清零

P0：

- central cleanup queue 未完整官方化。
- spell duel / battle 完整生命周期仍未完成。
- `PAY_COST`, `ASSIGN_COMBAT_DAMAGE`, `ORDER_TRIGGERS` 等复杂 prompt / command / payload schema 未完成。
- 正式 18 步 E2E 未最终收口。
- 1009 entries / 811 FUs 的 FAQ 证据与 full-official 测试矩阵未完成。

P1：

- PaymentEngine 仍未统一到完整官方支付窗口。
- LayerEngine / 持续效果 / 替代效果 / 禁止效果仍未达到最终完整模型。
- FAQ 候选页码尚未全部人工 adjudicate 为“适用 / 不适用 / 通用规则”。
- 当前实现映射仍是 representative route，不能升级为 `full-official-rule-pass`。

是否允许批量覆盖：**不允许。**
