# 当前卡牌效果高风险 Top20

更新时间：2026-05-09

阶段：**阶段 2 / E 卡牌覆盖矩阵与 FAQ 证据候选**

结论：**NOT READY；不允许进入 1009 张卡牌效果批量覆盖。**

本文是阶段 2 的风险排序与证据候选，不是功能实现清单，也不是错误断言。排名用于告诉后续阶段先审哪里：哪些 functional unit 同时碰到 FAQ、费用、触发/替换、持续效果、战斗/法术对决、隐藏信息或非 PLAY_CARD 规则域。

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

## 3. Top20 高风险 Functional Units

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

## 4. 未覆盖效果分类

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

## 5. P0/P1 仍未清零

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
