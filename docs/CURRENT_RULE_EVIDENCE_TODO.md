# 当前规则证据 TODO

更新时间：2026-05-10
结论：**NOT READY**

本文记录 E 证据/审计 worker 第一轮 P0 交接项、阶段 1 D 协议审计、阶段 2 D P0 规则证据链和 A 主控验收结果，不替代 `docs/CURRENT_SERVER_RULE_AUDIT.md`。

当前 HEAD / 3B checkpoint：`a74beac`
阶段 1 基线提交：`78b6896`
阶段 2 证据链计划：`docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`
阶段 3 核心流程审计：`docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`
阶段 3A 完成记录：`docs/CURRENT_STAGE3A_PLAN.md`
阶段 3B 当前计划：`docs/CURRENT_STAGE3B_PLAN.md`
阶段 3C 当前证据：`docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`
阶段 3D / 第三阶段收口审计：`docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`
阶段 4C-1 触发排序审计：`docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`
阶段 4C-2 真实触发入队审计：`docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`
阶段 4C-3 绝念真实触发入队审计：`docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`
阶段 4C-4 触发支付 / 拒付审计：`docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`
阶段 4C-5 state-based cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-6 Honest Broker cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-7 Scouting Warhawk trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`
阶段 4C-8 Scouting Warhawk cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-9 Poro cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-10 Unsung Hero cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-11 Ghostly Centaur cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-12 Resonant Soul cleanup trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-13 Stack destroyed trigger migration 审计：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`
阶段 4C-14 Savage Jawfish trigger enqueue 审计：`docs/CURRENT_STAGE4C_BATCH14_SAVAGE_JAWFISH_TRIGGER_AUDIT.md`
阶段 4C-15 Viktor feasibility blocker 审计：`docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`
阶段 4C-15A Minion token family 审计：`docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_AUDIT.md`
阶段 4C-15B Viktor trigger baseline 审计：`docs/CURRENT_STAGE4C_BATCH15B_VIKTOR_TRIGGER_AUDIT.md`
阶段 4C-16 Mechanical Trickster trigger 审计：`docs/CURRENT_STAGE4C_BATCH16_MECHANICAL_TRICKSTER_TRIGGER_AUDIT.md`
阶段 4C-17 Ironclad Vanguard trigger 审计：`docs/CURRENT_STAGE4C_BATCH17_IRONCLAD_VANGUARD_TRIGGER_AUDIT.md`
阶段 4C-18 Mechanical + Ironclad cleanup trigger 审计：`docs/CURRENT_STAGE4C_BATCH18_MECHANICAL_IRONCLAD_CLEANUP_TRIGGER_AUDIT.md`
阶段 4C-19 Kogmaw last-breath AoE 审计：`docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_AUDIT.md`
阶段 4C-20B Undercover Agent triggered hand-choice 审计：`docs/CURRENT_STAGE4C_BATCH20B_UNDERCOVER_HAND_CHOICE_AUDIT.md`
阶段 4C-21 Sunken Temple trigger payment 审计：`docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_AUDIT.md`
阶段 4C-21 Sunken Temple trigger payment 证据：`docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_EVIDENCE.md`
阶段 4C-22 Muddy Dredger Warhawk baseline 审计：`docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_AUDIT.md`
阶段 4C-22 Muddy Dredger Warhawk baseline 证据：`docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_EVIDENCE.md`
阶段 4C-23 Lux high-cost spell power 审计：`docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_AUDIT.md`
阶段 4C-23 Lux high-cost spell power 证据：`docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_EVIDENCE.md`
阶段 4C-24 Vayne conquer recall 审计：`docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_AUDIT.md`
阶段 4C-24 Vayne conquer recall 证据：`docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_EVIDENCE.md`
阶段 4C-25 Icevale Archer attack payment 审计：`docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_AUDIT.md`
阶段 4C-25 Icevale Archer attack payment 证据：`docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_EVIDENCE.md`
阶段 4C-26 Jax weapon attach payment draw 审计：`docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_AUDIT.md`
阶段 4C-26 Jax weapon attach payment draw 证据：`docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_EVIDENCE.md`
阶段 4C-27 Treasure Hunter move Gold 审计：`docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_AUDIT.md`
阶段 4C-27 Treasure Hunter move Gold 证据：`docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_EVIDENCE.md`
阶段 4C-28 Battle or Flight move to base 审计：`docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_AUDIT.md`
阶段 4C-28 Battle or Flight move to base 证据：`docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_EVIDENCE.md`
阶段 4C-29 Gust return-to-hand guard 审计：`docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_AUDIT.md`
阶段 4C-29 Gust return-to-hand guard 证据：`docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_EVIDENCE.md`
阶段 4C-30 Hunt the Weak destroy guard 审计：`docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_AUDIT.md`
阶段 4C-30 Hunt the Weak destroy guard 证据：`docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_EVIDENCE.md`
阶段 4C-31 Reprimand return-to-hand guard 审计：`docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_AUDIT.md`
阶段 4C-31 Reprimand return-to-hand guard 证据：`docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_EVIDENCE.md`
阶段 4C-32 Ride the Wind move guard 审计：`docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_AUDIT.md`
阶段 4C-32 Ride the Wind move guard 证据：`docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_EVIDENCE.md`
阶段 4C-33 Charm move guard 审计：`docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_AUDIT.md`
阶段 4C-33 Charm move guard 证据：`docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_EVIDENCE.md`
阶段 4C-34 Isolate move guard 审计：`docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_AUDIT.md`
阶段 4C-34 Isolate move guard 证据：`docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_EVIDENCE.md`
阶段 4C-35 Vengeance destroy target guard 审计：`docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_AUDIT.md`
阶段 4C-35 Vengeance destroy target guard 证据：`docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_EVIDENCE.md`
阶段 4C-36 Hostile Takeover control ready guard 审计：`docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_AUDIT.md`
阶段 4C-36 Hostile Takeover control ready guard 证据：`docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_EVIDENCE.md`
阶段 4C-37 Berserk Impulse opponent top unit guard 审计：`docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_AUDIT.md`
阶段 4C-37 Berserk Impulse opponent top unit guard 证据：`docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_EVIDENCE.md`
阶段 4C-38 Edge of Night assemble guard 审计：`docs/CURRENT_STAGE4C_BATCH38_EDGE_OF_NIGHT_ASSEMBLE_GUARD_AUDIT.md`
阶段 4C-38 Edge of Night assemble guard 证据：`docs/CURRENT_STAGE4C_BATCH38_EDGE_OF_NIGHT_ASSEMBLE_GUARD_EVIDENCE.md`
阶段 4C-39 Zhonya's Hourglass play guard 审计：`docs/CURRENT_STAGE4C_BATCH39_ZHONYAS_HOURGLASS_PLAY_GUARD_AUDIT.md`
阶段 4C-39 Zhonya's Hourglass play guard 证据：`docs/CURRENT_STAGE4C_BATCH39_ZHONYAS_HOURGLASS_PLAY_GUARD_EVIDENCE.md`
阶段 4C-40 Sea Monster Hook play guard 审计：`docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_AUDIT.md`
阶段 4C-40 Sea Monster Hook play guard 证据：`docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_EVIDENCE.md`
阶段 4C-41 Giant Arm Kato play keyword guard 审计：`docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_AUDIT.md`
阶段 4C-41 Giant Arm Kato play keyword guard 证据：`docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_EVIDENCE.md`
阶段 4C-42 Time Gate play guard 审计：`docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_AUDIT.md`
阶段 4C-42 Time Gate play guard 证据：`docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_EVIDENCE.md`
阶段 4C-43 Sfur Song play guard 审计：`docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_AUDIT.md`
阶段 4C-43 Sfur Song play guard 证据：`docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_EVIDENCE.md`
阶段 4C-44 Akshan play guard 审计：`docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_AUDIT.md`
阶段 4C-44 Akshan play guard 证据：`docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_EVIDENCE.md`
阶段 4C-45 Switcheroo swap guard 审计：`docs/CURRENT_STAGE4C_BATCH45_SWITCHEROO_SWAP_GUARD_AUDIT.md`
阶段 4C-45 Switcheroo swap guard 证据：`docs/CURRENT_STAGE4C_BATCH45_SWITCHEROO_SWAP_GUARD_EVIDENCE.md`
阶段 4C-46 legend-domain / shared-oracle design gate：`docs/CURRENT_STAGE4C_BATCH46_LEGEND_DOMAIN_SHARED_ORACLE_DESIGN_GATE.md`
阶段 4C-46 legend-domain / shared-oracle evidence：`docs/CURRENT_STAGE4C_BATCH46_LEGEND_DOMAIN_SHARED_ORACLE_EVIDENCE.md`
阶段 4C-47 Draven battle body guard 审计：`docs/CURRENT_STAGE4C_BATCH47_DRAVEN_BATTLE_BODY_GUARD_AUDIT.md`
阶段 4C-47 Draven battle body guard 证据：`docs/CURRENT_STAGE4C_BATCH47_DRAVEN_BATTLE_BODY_GUARD_EVIDENCE.md`
阶段 4C-48 Vex spellshield stun guard 审计：`docs/CURRENT_STAGE4C_BATCH48_VEX_SPELLSHIELD_STUN_GUARD_AUDIT.md`
阶段 4C-48 Vex spellshield stun guard 证据：`docs/CURRENT_STAGE4C_BATCH48_VEX_SPELLSHIELD_STUN_GUARD_EVIDENCE.md`
阶段 4C-49 Ezreal play-unit guard 审计：`docs/CURRENT_STAGE4C_BATCH49_EZREAL_PLAY_UNIT_GUARD_AUDIT.md`
阶段 4C-49 Ezreal play-unit guard 证据：`docs/CURRENT_STAGE4C_BATCH49_EZREAL_PLAY_UNIT_GUARD_EVIDENCE.md`
阶段 4C-50 Draven keyword-unit guard 审计：`docs/CURRENT_STAGE4C_BATCH50_DRAVEN_KEYWORD_UNIT_GUARD_AUDIT.md`
阶段 4C-50 Draven keyword-unit guard 证据：`docs/CURRENT_STAGE4C_BATCH50_DRAVEN_KEYWORD_UNIT_GUARD_EVIDENCE.md`
阶段 4C-51 Rek'Sai attack reveal guard 审计：`docs/CURRENT_STAGE4C_BATCH51_REKSAI_ATTACK_REVEAL_GUARD_AUDIT.md`
阶段 4C-51 Rek'Sai attack reveal guard 证据：`docs/CURRENT_STAGE4C_BATCH51_REKSAI_ATTACK_REVEAL_GUARD_EVIDENCE.md`
阶段 4C-52 Rek'Sai haste / overwhelm guard 审计：`docs/CURRENT_STAGE4C_BATCH52_REKSAI_HASTE_OVERWHELM_GUARD_AUDIT.md`
阶段 4C-52 Rek'Sai haste / overwhelm guard 证据：`docs/CURRENT_STAGE4C_BATCH52_REKSAI_HASTE_OVERWHELM_GUARD_EVIDENCE.md`
阶段 4C-53 Sett legend-domain guard 审计：`docs/CURRENT_STAGE4C_BATCH53_SETT_LEGEND_DOMAIN_GUARD_AUDIT.md`
阶段 4C-53 Sett legend-domain guard 证据：`docs/CURRENT_STAGE4C_BATCH53_SETT_LEGEND_DOMAIN_GUARD_EVIDENCE.md`
阶段 4C-54 Void Burrower legend-domain guard 审计：`docs/CURRENT_STAGE4C_BATCH54_VOID_BURROWER_LEGEND_DOMAIN_GUARD_AUDIT.md`
阶段 4C-54 Void Burrower legend-domain guard 证据：`docs/CURRENT_STAGE4C_BATCH54_VOID_BURROWER_LEGEND_DOMAIN_GUARD_EVIDENCE.md`

## B 修复验收

### 0/负战力

证据锚点：`SOUL-OFAQ-260114` p19-p20；`CORE-260330` p14-p15 rules 142-143、p31-p33 rules 318-324、p77 rule 460。

验收条目：
- `Power <= 0 && Damage == 0` 的正面场上单位不得暴露 `DESTROY_ZERO_POWER_UNIT` blocking task，不得自动进入废牌堆。
- `Power <= 0 && Damage >= 1` 的正面场上单位应在清理中被摧毁；事件/任务命名不得再表达为“仅因 0 战力死亡”。
- 负战力单位参与战斗时，战斗伤害输出按 0 计算，但对象实际战力值必须保留，用于后续增减计算。
- `Damage == 0` 不是有效伤害，不能触发 0/负战力单位死亡。

### 具体战场 destination 大小写

证据锚点：`data/official/card-catalog.zh-CN.json` 中 `OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298`。

验收条目：
- `BATTLEFIELD:<objectId>` 只规范化 `BATTLEFIELD` zone；冒号后的 objectId/cardNo 大小写逐字保留。
- 小写 `a` 战场应覆盖 prompt destination、submit destination、`ObjectLocations.BattlefieldObjectId`、`BattlefieldStates` snapshot。
- 重连/recovery 后不应出现 `276A`、`278A`、`293A` 之类被上转的 object id。

## 已验收但需防回归

- 0/负战力清理语义：B 已完成代码修复，A 主控已用聚焦测试与后端 full test 验收；后续不应再列为未清零 P0，但需要保留证据锚点与回归测试。
- 具体战场 destination 大小写：B 已完成代码修复，A 主控已用小写 `a` 战场移动聚焦测试验收；后续不应再列为未清零 P0，但需要保留官方卡号与重连/recovery 防回归。

## 阶段 1 D 协议/前端证据汇总

- 已新增 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，记录当前真实协议字段：`SnapshotDto`、`ActionPromptDto`、`PromptViewDto`、`ActionPromptCandidateDto`、`GameEvent`、`ErrorDto`。
- 已确认当前没有独立 `MatchSnapshot`、`LegalAction`、`RoomState`、`GameLogEntry`、`ActionError` DTO；后续文档和任务不应把这些名称当成已存在协议。
- 已更新 `docs/CURRENT_FRONTEND_CONTRACT_GAPS.md`：复杂 prompt 的通用降级展示已经可作为安全承接路径；阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 `INVALID_PAYLOAD`；阶段 3A 已补 `PAY_COST` 最小 runtime，阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime。除这些最小切片外，完整 runtime 状态机、专用交互和错误 details 仍是 P0。
- 阶段 1 文档结论仍是 **NOT READY**；D 本轮没有关闭新的功能 P0/P1，只关闭了“文档描述不准”的口径风险。

## 阶段 2 D P0 规则证据链汇总

本轮 D 已新增 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，并把同一套 P0 证据链同步到 `docs/CURRENT_SERVER_RULE_AUDIT.md` 与 `docs/rules-evidence-index.md`。

| P0 | 规则依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| battlefield / standby / control / held / conquer lifecycle | `CORE-260330` rules 107.2-107.3, 187-189, 315.2.b.2, 319-323, 344-348, 461-464；`JFAQ-251023` q4.1-5.4；`SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p4-p5 | `ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks`、具体战场移动已有；完整 control freeze/release、standby removal、held/conquer scoring 未完成 | B 主实现；E 证据 fixture；C 等 schema；D 文档 | 建 board task model，覆盖控制检查、待命移除、征服/据守得分 |
| cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking guard 已有；未覆盖全部状态变化和替代/控制权任务 | B 主实现；E 场景证据；D 文档 | 所有 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue cleanup |
| spell duel / battle lifecycle | `CORE-260330` rules 307-313, 333-348, 454-461；`JFAQ-251023` q2.3-q2.4, q3.1-q3.3 | `SpellDuelState`、`BattleState`、关联 id 和焦点恢复已有；`DECLARE_BATTLE` 仍是同步代表路径 | B 主实现；E 初始链/焦点/触发 fixture；C 等 typed prompt；D 文档 | 由 cleanup queue 创建并推进 spell duel / battle task |
| damage assignment | `CORE-260330` rules 142-143, 417, 460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 阶段 3C 已补最小 `ASSIGN_COMBAT_DAMAGE` runtime prompt、damagePool/legalTargets、合法/非法/stale、零副作用拒绝和 simultaneous commit；完整壁垒/后排/负战力/不可分配矩阵仍缺 | B 主实现；E 多单位/壁垒/后排 fixture；C 仅同步类型/调试展示；D 文档 | 后续扩展完整 damage assignment constraints 和 battle task |
| `PAY_COST` / payment windows | `CORE-260330` rules 131, 135.2.e, 162-167, 356-357, 377, 403-405, 414, 416；`JFAQ-251023` q2.5；`SOUL-OFAQ-260114` p1-p4, p15, p19-p21 | `PaymentCostRules`、typed `RunePool`、代表性 `COST_PAID` 已有；阶段 2 B 已补 `PAY_COST` command/schema skeleton 与 `INVALID_PAYLOAD`；阶段 3A 已补最小 pending payment prompt/submit；阶段 4C-4 已补 `SFD·220/221` `TRIGGER_PAYMENT` 支付 / 拒付 / 支付失败 no-mutation 代表路径；阶段 4C-21 已补 `SFD·218/221`《沉没神庙》征服强力单位后的 `TRIGGER_PAYMENT` / `PAY_COST` 支付或拒付代表路径；阶段 4C-24 已补 `OGN·035/298`《薇恩》征服后的 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 回手或拒付不变更代表路径；阶段 4C-25 已补 `UNL-065/219`《冰谷弓箭手》进攻后的 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 令同处目标本回合 -1 或拒付不变更代表路径；阶段 4C-26 已补 `SFD·119/221` / `SFD·119a/221` Jax 贴附武装后的 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 抽 1 或拒付不变更代表路径；阶段 4C-28 已补 `OGN·168/298`《战或逃》普通打出费用支付后结算与 invalid target no-payment/no-mutation guard；阶段 4C-29 已补 `OGN·169/298`《罡风》普通打出费用支付后结算与 invalid target no-payment/no-mutation guard；阶段 4C-30 已补 `UNL-159/219`《狩魂》普通打出费用支付后结算与 invalid target no-payment/no-mutation guard；阶段 4C-31 已补 `OGN·172/298`《责退》普通打出费用支付后结算与 invalid target no-payment/no-mutation guard；阶段 4C-32 已补 `OGN·173/298`《驭风而行》普通打出费用支付后结算与 invalid target no-payment/no-mutation guard；阶段 4C-35 已补 `OGN·229/298`《复仇》普通打出费用后结算与 invalid target no-payment/no-mutation guard；阶段 4C-37 已补 `OGN·025/298`《暴怒冲动》普通打出费用后对手牌堆顶单位代表结算与 invalid target no-payment/no-mutation guard；完整 PaymentEngine、替代/额外费用、非出牌支付窗口仍缺 | B 主实现；E 支付/拒付 fixture；C 仅同步类型/调试展示；D 文档 | 建 `PaymentPlan/paymentPlanId/paymentWindow` 与 Quote/Authorize/Commit，并扩到更多 triggered-cost FUs |
| `ORDER_TRIGGERS` / trigger payment | `CORE-260330` rules 318-324, 333-340, 377, 383.3.d-383.3.e, 403-405；`JFAQ-251023` q2.2-q2.3, q2.5 | 阶段 3D 已补最小 runtime window / UI / evidence；阶段 4C-1 已补保守 APNAP controller-block 子集、battle initial stack 代表路径和 face-down standby source 脱敏；阶段 4C-2 至 4C-22 已补多条 real trigger / state cleanup / trigger payment / hand-choice 代表路径；阶段 4C-23 已补 Lux high-cost spell temporary power compatibility trigger-event 代表路径；阶段 4C-24 已补 Vayne `OGN·035/298` / `FU-c027639a3c` 征服后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 回手或拒付不变更代表路径，并确认 hidden / face-down / standby / opponent-controlled source no trigger / no leak；阶段 4C-25 已补 Icevale Archer `UNL-065/219` / `FU-c170628e3a` active start-battle 进攻后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 令同处目标本回合 -1 或拒付不变更代表路径，并确认 invalid target、hidden / face-down / standby / opponent-controlled source guard；阶段 4C-26 已补 Jax `SFD·119/221` / `SFD·119a/221` / `FU-73f3be35df` existing equipment attach route 后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 抽 1 或拒付不变更代表路径，并确认 non-Jax / non-armament、hidden / face-down / standby / opponent-controlled source 与 insufficient payment guard；阶段 4C-27 已补 Treasure Hunter `SFD·130/221` / `FU-6144ab0271` successful move -> dormant Gold equipment token 代表路径，并确认 non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source、failed move、no-op move guard；完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs、attack-trigger family、weapon-attachment trigger family、move-trigger family、Karthus optional extra Last Breath、full official trigger-count / multiplicity matrix、完整“每回合首次”时序、完整同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation matrix、hidden / face-down 原始触发建模、更多 trigger payment、Spellshield target tax、完整 effect resolution、FAQ regression、1009/811 full-official 仍缺 | B 主实现；E 触发族 / FAQ fixture；C 只提交服务端 prompt；D 文档 | 以 Watchful Sentinel + Honest Broker 两条 last-breath real enqueue、Treasure Pile / Sunken Temple / Vayne / Icevale Archer / Jax 触发支付、Treasure Hunter move Gold、Warhawk explicit / cleanup enqueue、Poro / Unsung / Ghostly / Resonant / Savage cleanup 或 stack enqueue、Viktor non-minion baseline、Mechanical Trickster / Ironclad Vanguard stack 与 cleanup baseline、Kogmaw AoE baseline、Undercover hand-choice prompt、Muddy Dredger Warhawk token baseline、Lux temporary power baseline 为代表基线，继续扩其他 destroyed-family / friendly-destroyed FUs、attack-trigger family、weapon-attachment trigger family、move-trigger family、“每回合首次”时序、同时死亡触发次数、effective power / LayerEngine、battlefield objectLocation 条件矩阵、hidden / face-down trigger policy、更多触发费用拒付、effect resolution 和 FAQ regression |

4C-15 补充：Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger 候选经 B 只读检查后判定为模型阻断。当前 `CardObjectTags` / `CardObjectState` 缺少稳定 minion、subtype、token-family 字段，多个随从创建路径只落成 `CARD_TYPE:UNIT`，无法在摧毁事件中可靠区分“随从单位”和普通单位。4C-15 未实现、未新增测试、不关闭 P0；后续需先冻结 token subtype / family 模型，或由用户确认跳过 Viktor 改做 safe FU。

4C-15A 补充：B 已补 `TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily` 最小前置模型，官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）和 `CreateBaseUnitTokens(tokenName == "随从")` 生成路径带 marker，Viktor legend 直接创建随从同步带 marker；普通单位与 Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker；hidden face-down standby 不向对手泄漏 tags / cardNo / power。这只部分关闭 token subtype / family / minion classification 前置 blocker；4C-15B 已在此前置模型上关闭 Viktor 代表性 trigger baseline。

4C-15B 补充：B 已补 Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 最小官方化代表切片。visible surviving friendly Viktor source 看到另一名友方非随从单位被摧毁时，使用 pre-removal `CardObjectState` 判定 destroyed target 为 unit / same controller / not source / not `CardObjectTags.MinionTokenFamily`，并经 `TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` 创建 1-power Zaun minion `OGN·273/298` 且带 `TOKEN_FAMILY:MINION`。true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均有代表路径；minion target、hidden / face-down / standby / opponent source、source also dying 均不入队、不泄漏、不造 token。该批不覆盖 same-source 多对象 full official trigger-count matrix，不覆盖 Kogmaw / Karthus / Undercover Agent，不代表 full trigger engine。

4C-16 补充：B 已把 Mechanical Trickster / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` 从旧 immediate token create 迁移为真实 TriggerQueue / Stack / Priority 代表路径。true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`；单触发 auto-stack；多触发 `ORDER_TRIGGERS` -> `StackItems`；priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。face-down / standby source 不入队、不泄漏 prompt metadata、不创建 token。该批不覆盖 Ironclad Vanguard、Kogmaw、Karthus、Undercover，不代表 full trigger engine。

4C-17 补充：B 已把 Ironclad Vanguard / `SFD·021/221` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 旧 immediate last-breath create robots -> real TriggerQueue / Stack / Priority 代表路径迁移完成。true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`，单触发 auto-stack，多触发 `ORDER_TRIGGERS` -> `StackItems`，priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x2；face-down / standby source 不入队、不泄漏 prompt metadata、不创建 token；旧 `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` fixture 已更新为 queue / priority semantics。冻结矩阵中本卡正确 FU 为 `FU-6d0971786b`，本批只以 overlay `triggerEffectKind=IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 记录代表路径；不创建不存在的 `FU-a76d38727a`。A 验证后端 full 3384/3384、前端 build、Chrome smoke、diff check、矩阵 JSON/断言均通过；本批只关闭 true stack migration，state-based cleanup route 仍未覆盖。

4C-18 补充：B/A 已验证 Mechanical Trickster + Ironclad Vanguard state-based cleanup last-breath trigger enqueue 代表路径，补齐 lethal damage cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> Mechanical `UNIT_TOKEN_CREATED` x3 / Ironclad `UNIT_TOKEN_CREATED` x2。Starfall cleanup route 触发代表路径、focused/full/smoke/diff 结果已回填；本补充只关闭这两个 FU 的 cleanup-route representative baseline，不关闭 full trigger engine。

4C-19 补充：B/A 已验证 Kogmaw / 克格莫 `OGN·190/298` / `FU-af8b05c294` 绝念 AoE damage 代表切片，路径为 `UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> battlefield units take 4 damage -> cleanup queue stabilizes。focused 4/4、backend full 3392/3392、frontend build、Chrome smoke、diff / JSON / matrix 断言均通过；本补充只关闭 Kogmaw representative baseline，不关闭 full-official。

4C-20B 补充：B/A 已验证 Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` triggered hand-choice server prompt 微切片。服务端在 Undercover 绝念触发结算时打开 `HAND_CHOICE` / `CHOOSE_HAND_CARDS`，只向选择玩家暴露 `handChoices` 候选；wrong player、stale、invalid、malformed / illegal payload 均 no mutation 拒绝；`CORE-260330` p62 / rule `422.4` 关闭 1 / 0 手牌 shortfall 裁决：弃尽可弃数量后仍抽两张。C 已完成前端 `HAND_CHOICE` 展示与 `CHOOSE_HAND_CARDS` 提交接线，前端不结算弃牌或抽牌。A focused backend `UndercoverAgentTriggerTests` 通过 6/6。本补充只关闭 Undercover prompt 微切片，不关闭非 Undercover 的通用 discard / hand-choice engine，不关闭 Karthus 额外绝念。

4C-21 补充：B/A 已验证 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 征服强力单位后的服务端权威 trigger payment 代表切片。旧 immediate auto pay + draw 路径已 superseded；征服此处且战场上留存强力单位时，服务端打开 `TRIGGER_PAYMENT` / `PAY_COST` prompt；`PAY_COST(SPEND_MANA:1)` 支付成功抽 1，`PAY_COST(DECLINE)` 拒付关闭窗口且不抽牌；invalid / stale / insufficient 等 no-mutation 语义已有 focused 覆盖。本补充只关闭 Sunken Temple triggered payment representative baseline，不关闭完整 PaymentEngine 或 full-official timing matrix。

4C-22 补充：A 已决定本批收 Muddy Dredger / 腐泥疏浚工 `UNL-153/219` / `FU-b829fb32b9`，而不是 E 建议的 Aphelios；理由是 B/D 均判断 Muddy 是低耦合服务端代表切片，且代码、focused backend 与 backend full 已通过。B/A 已验证 visible face-up Muddy Dredger 经 state-based cleanup destruction -> `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` Warhawk `UNL·T02` 到 controller base。hidden / face-down / standby / invalid source no enqueue / no leak / no token。Warhawk 的 Spellshield 只以 token tag / identity 代表，Spellshield target tax / additional cost 全矩阵不由本批关闭。本补充只关闭 Muddy Dredger Warhawk representative baseline，不关闭 full trigger engine 或 full-official。

4C-23 补充：A 已验证 Lux / 拉克丝 `OGS·006/024` / `FU-f18a49e06d` high-cost spell temporary power 代表切片。controller 打出 cost >= 5 spell 后，visible face-up Lux 记录 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events 并 `POWER_MODIFIED_UNTIL_END_OF_TURN` +3；low-cost spell、opponent spell、face-down / standby / invalid source no trigger / no mutation。focused 67/67、backend full 3413/3413、frontend build、Chrome smoke、JSON / diff check 均通过。本补充只关闭 Lux representative baseline，不关闭 complete trigger engine、PaymentEngine、LayerEngine 或 full-official。

4C-24 补充：B 已完成 Vayne / 薇恩 `OGN·035/298` / `FU-c027639a3c` 极窄征服支付回手代表切片。visible face-up Vayne 征服战场后打开现有 `TRIGGER_PAYMENT` / `PAY_COST`；`PAY_COST(SPEND_MANA:1)` 后 Vayne 返回 owner hand，`PAY_COST(DECLINE)` 不回手、不变更；hidden / face-down / standby / opponent-controlled source 不触发、不泄漏。focused `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Vayne|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"` 通过 52/52。本补充只关闭 Vayne conquer recall representative baseline，不关闭 full Assault3、active-entry、完整 conquer/control-zone matrix、full PaymentEngine 或 full-official。

4C-25 补充：A 已完成 Icevale Archer / 冰谷弓箭手 `UNL-065/219` / `FU-c170628e3a` 极窄进攻触发支付降战力代表切片。active start-battle task 下 visible face-up Icevale 作为攻击者，使用现有 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标；战斗声明后打开 `TRIGGER_PAYMENT` / `PAY_COST`；`PAY_COST(SPEND_MANA:1)` 后目标本回合 power -1，`PAY_COST(DECLINE)` 不变更；invalid target、hidden / face-down / standby / opponent-controlled source 有 guard。focused `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Icevale|FullyQualifiedName~AttackPayment|FullyQualifiedName~TriggerPayment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Vayne|FullyQualifiedName~Lux"` 通过 102/102。本补充只关闭 Icevale Archer attack payment representative baseline，不关闭 full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点、Spellshield target tax、LayerEngine、full PaymentEngine 或 full-official。

4C-26 补充：A/B 已完成 Jax / 贾克斯 `SFD·119/221`、`SFD·119a/221` / `FU-73f3be35df` 极窄武装贴附触发支付抽牌代表切片。visible face-up Jax 通过现有 equipment attach route 被贴附 weapon / armament 后打开 `TRIGGER_PAYMENT` / `PAY_COST`；`PAY_COST(SPEND_MANA:1)` 后抽 1，`PAY_COST(DECLINE)` 关闭窗口且不抽牌、不变更；non-Jax / non-armament no prompt；hidden / face-down / standby / opponent-controlled source 不触发、不泄漏；insufficient payment rejected without draw。focused `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~TriggerPayment"` 通过 37/37；small regression `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~Icevale|FullyQualifiedName~Vayne|FullyQualifiedName~Lux|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment"` 通过 46/46。本补充只关闭 Jax weapon attach payment draw representative baseline，不关闭 full Forge / 百炼 / assemble lifecycle、full equipment attachment rules、full optional trigger family / order triggers、full PaymentEngine、draw / replacement / hidden-zone matrix、FAQ regression 或 full-official。

4C-27 补充：A/B 已完成 Treasure Hunter / 寻宝猎人 `SFD·130/221` / `FU-6144ab0271` 极窄移动触发创建休眠 Gold 装备指示物代表切片。visible face-up Treasure Hunter 经 existing authoritative move route 成功移动后触发 `TREASURE_HUNTER_MOVE_CREATE_GOLD` 并创建一个休眠 Gold equipment token 到 controller base；base -> battlefield move 与 precise ROAM battlefield A -> battlefield B 均有代表路径；non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source、failed move、no-op move 不触发、不泄漏、不造 token；本批不新增 protocol / frontend shape。A 记录 focused 82/82、small regression 121/121 通过。Karthus / `FU-ee1dfb3ed3` 保持 design-gated。本补充只关闭 Treasure Hunter move Gold representative baseline，不关闭完整 movement / control-zone / roam lifecycle、move-trigger family、Gold token full resource / reaction ability、equipment token full rules、完整 trigger engine、FAQ regression 或 full-official。

4C-28 补充：A/B 已完成 Battle or Flight / 战或逃 `OGN·168/298` / `FU-813144e7d4` 极窄战场单位移动到 owner base 与 target guard hardening 代表切片。P1 打出 Battle or Flight，选择正面战场单位目标，双方 priority pass 后目标移动到 owner base，并保留 damage / power / object identity；battlefield equipment、base unit、stale object、face-down standby object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no unit movement；本批不新增 protocol / frontend shape。A 记录 focused 61/61 通过。本补充只关闭 Battle or Flight move-to-owner-base representative baseline，不关闭 swift spell-duel timing、face-down standby reaction play、完整 movement / control-zone matrix、full target selection prompt、PaymentEngine、FAQ regression 或 full-official。

4C-29 补充：A/B 已完成 Gust / 罡风 `OGN·169/298` / `FU-48662b7661` 极窄公共战场单位 power <= 3 回手与 target guard hardening 代表切片。P1 打出 Gust，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后目标返回 owner hand；power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation；本批不新增 protocol / frontend shape。A 记录 focused 112/112 与 small combined 13/13 通过，backend full / build / smoke pending。本补充只关闭 Gust return-to-hand target guard representative baseline，不关闭 swift / reaction timing、完整 return-to-hand / movement / control-zone matrix、full target selection prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 或 full-official。

4C-30 补充：A/B 已完成 Hunt the Weak / 狩魂 `UNL-159/219` / `FU-282b6e3149` 极窄公共战场单位 power <= 3 摧毁与 target guard hardening 代表切片。P1 打出 Hunt the Weak，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后目标被摧毁并进入 owner graveyard；power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy mutation；face-down standby 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A 记录 focused 34/34 与 adjacent 19/19 通过，backend full / build / smoke pending。本补充只关闭 Hunt the Weak destroy-target guard representative baseline，不关闭 swift / reaction timing、完整 destroy / cleanup / Last Breath trigger interactions、full target selection prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 或 full-official；replacement / prevention / cleanup / full targeting matrix 保持 P1/P2 后续项，不新增为本批 P0。

4C-31 补充：A/B 已完成 Reprimand / 责退 `OGN·172/298` / `FU-d0383ed260` / `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` 极窄公共战场单位回手与 target guard hardening 代表切片。P1 打出 Reprimand，选择正面公共战场单位目标，双方 priority pass 后目标返回 owner hand；base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation；face-down standby 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A 记录 focused 58/58、adjacent guard 24/24、backend full 3471/3471、frontend build passed、Chrome smoke passed。本补充只关闭 Reprimand return-to-hand target guard representative baseline，不关闭 full-official；swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / control-zone matrix 保持 P1/P2 后续项，不新增为本批 P0。

4C-32 补充：A/B 已完成 Ride the Wind / 驭风而行 `OGN·173/298` / cardId `31403` / `FU-6f84196631` / `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 极窄 friendly public battlefield unit ready + move to owner base 与 target guard hardening 代表切片。P1 打出 Ride the Wind，选择合法 friendly public battlefield unit target，双方 priority pass 后目标 ready 并移动到 owner base；enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no ready / no move / no leak；face-down standby 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A/B focused backend 通过 11/11，adjacent guard regression 通过 32/32，backend full 通过 3479/3479，frontend build 通过，Chrome smoke 通过。本补充只关闭 Ride the Wind move-and-ready target guard representative baseline，不关闭 full-official；swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项，不新增为本批 P0。

4C-33 补充：A/B 已完成 Charm / 魅惑妖术 `OGN·043/298` / cardId `31255` / `FU-1586b6cdd9` / `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE` 极窄 enemy public battlefield unit move to owner base 与 target guard hardening 代表切片。P1 打出 Charm，选择合法 enemy public battlefield unit target，双方 priority pass 后目标移动到 owner base，并保留 damage / power / exhausted / object identity；friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no leak；face-down standby 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A/B focused backend 通过 35/35，adjacent guard regression 通过 40/40，backend full 通过 3487/3487，frontend build 通过，Chrome smoke 通过。本补充只关闭 Charm move enemy unit target guard representative baseline，不关闭 full-official；完整目的地选择、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项，不新增为本批 P0。

4C-34 补充：A/B 已完成 Isolate / 隔绝 `UNL-124/219` / cardId `34667` / `FU-175d573ae4` / `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW` 极窄 enemy public battlefield unit move to owner base no-draw 与 target guard hardening 代表切片。P1 打出 Isolate，选择合法 enemy public battlefield unit target，双方 priority pass 后目标移动到 owner base，并保留 damage / power / exhausted / object identity；代表路径确认不产生 `CARD_DRAWN`；friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no draw / no leak；face-down standby 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A/B focused backend 通过 46/46，adjacent guard regression 通过 48/48，backend full 通过 3495/3495，frontend build 通过，Chrome smoke 通过。本补充只关闭 Isolate move enemy unit no-draw target guard representative baseline，不关闭 full-official；落单敌方单位抽牌分支、完整目的地/孤立判定、多位置 battlefield model、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项，不新增为本批 P0。

4C-35 补充：A/B 已完成 Vengeance / 复仇 `OGN·229/298` / cardId `31467` / `FU-07104fa58a` / `VENGEANCE_DESTROY_UNIT` 极窄 public unit destroy target 与 target guard hardening 代表切片。P1 打出 Vengeance，选择合法 public unit target，双方 priority pass 后目标进入 owner graveyard，并从 base / battlefield 与 public object state 移除；friendly / enemy public unit targets in base / battlefield 均为合法目标；stale unit、face-down standby object、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy / no leak；face-down standby 与 private-zone target 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A/B focused backend 通过 107/107，adjacent guard regression 通过 23/23，backend full 通过 3506/3506，frontend build 通过，Chrome smoke 通过，`git diff --check` 通过。本补充只关闭 Vengeance destroy target guard representative baseline，不关闭 full-official；完整 destroy / cleanup / replacement / prevention / Last Breath interaction、attached-equipment detach / replacement breadth、destroyed-this-turn memory、targeting prompt、Spellshield target tax 保持 P1/P2 后续项，不新增为本批 P0。

4C-36 补充：A/B 已完成 Hostile Takeover / 恶意收购 `SFD·202/221` / cardId `33301` / `FU-00ee09c2cc` / `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` 极窄 enemy public battlefield unit gain-control + ready 与 target guard hardening 代表切片。P1 打出 Hostile Takeover，选择合法 enemy public battlefield unit target，双方 priority pass 后 P1 获得该单位控制权并 ready；owner 仍为 P2，controller 变为 P1，对象仍留在 battlefield，并安排 `RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2`；既有 P5 end-turn return / recall fixture 只作为代表证据，不升级 full official。friendly battlefield unit、enemy base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no control / no ready / no leak；face-down standby 与 private-zone target 被拒绝且不暴露真实身份，opponent hidden info 继续受保护；本批不新增 protocol / frontend shape。A/B focused backend 通过 265/265，adjacent guard regression 通过 157/157，backend full 通过 3515/3515，frontend build 通过，Chrome smoke 通过。本补充只关闭 Hostile Takeover control-ready target guard representative baseline，不关闭 full-official；standby / reaction timing、battle-start / conquer branch、完整 battlefield / control-zone lifecycle、owner/controller matrix、end-turn cleanup task model、targeting prompt、Spellshield target tax、PaymentEngine、FAQ regression 保持 P1/P2 后续项，不新增为本批 P0。

4C-37 补充：B 已完成 Berserk Impulse / 暴怒冲动 `OGN·025/298` / cardId `31231` / `FU-b05eda44ce` / `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` 极窄 opponent top main-deck unit target guard 代表切片。P1 打出 Berserk Impulse 并支付 4，选择 P2 已揭示 / 代表性 public top main-deck unit，双方 priority pass 后目标从 P2 main deck 顶部打出到 P1 base；`UNIT_PLAYED_TO_BASE` 记录 source spell、target object、`ownerPlayerId=P2`、`playedByPlayerId=P1`、`sourceZone=MAIN_DECK`、`destinationZone=BASE`；目标 damage / until-end-of-turn effects / power modifier / exhausted 状态重置。friendly top unit、opponent second main-deck unit、top spell / equipment / rune、face-down top unit、private hand / base / battlefield unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no deck movement / no stack item / no unit played / no leak；dirty resolution 中 top changed、non-unit、face-down、wrong controller / ownership target 不移动、不产生 `UNIT_PLAYED_TO_BASE`。focused backend 通过 17/17；D 未运行重测试。本补充只关闭 Berserk Impulse opponent top unit target guard representative baseline，不关闭 full-official；full hidden-zone reveal / choose / recycle 仍为 P0 / design-gated，multi-opponent reveal、未选牌回收、non-unit branch、hidden-zone prompt / redaction、spell duel / reaction timing、PaymentEngine、LayerEngine、FAQ regression 保持后续项，不新增为本批 P0。

4C-38 补充：B 已完成 Edge of Night / 夜之锋刃 `SFD·139/221` / cardId `33229` / `FU-804412488c` / `EDGE_OF_NIGHT_PLAY_EQUIPMENT` test-only narrow play-equipment / assemble-purple target guard representative baseline。已覆盖普通 `PLAY_CARD` hand route 0 target -> stack / pass-pass -> base equipment，explicit target rejected no payment / no mutation；face-up controlled base Edge of Night `ASSEMBLE_PURPLE` -> friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`；face-down / hidden source、source in hand、opponent source、already-attached source、unknown source、unknown / opponent / face-down standby / non-unit target、missing / wrong optional cost、insufficient purple 均 no tick / no events / no payment / no stack / no attach / no leak。A focused filter 通过 98/98；Core gap none；D 未运行重测试。本补充只关闭 narrow assemble / play guard representative evidence，不关闭 full-official；Edge of Night face-down standby immediate attach remains P0 / design-gated，full standby immediate attach、hidden redaction、equipment layer、FAQ、1009/811 与 final 18-step E2E 仍保持 open。

4C-39 补充：B 已完成 Zhonya's Hourglass / 中娅沙漏 `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` / `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT` guard slice。已覆盖普通 hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment；explicit target rejected no mutation；source not in hand / wrong zone、opponent source、face-down standby source、insufficient mana 均 no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。A/B focused 通过 268/268；Core gap none；D 未运行重测试。本补充只关闭 Zhonya ordinary hand play-equipment target guard representative evidence，不关闭 full-official；standby / reaction timing、destroy replacement recall、完整 equipment / layer / FAQ、hidden info、1009/811 与 final 18-step E2E 仍保持 open。

4C-40 补充：B 已完成 Sea Monster Hook / 海兽钓钩 `OGN·242/298` / cardId `31482` / `FU-2653af0380` / `SEA_MONSTER_HOOK_PLAY_EQUIPMENT` guard slice。已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment；explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。A/B focused 通过 272/272；Core gap none；D 未运行重测试。本补充只关闭 Sea Monster Hook ordinary hand play-equipment target guard representative evidence，不关闭 full-official；activated ability：pay 1 + yellow + exhaust、destroy friendly unit、top-five look / choice、free play、recycle remainder、hidden / zone / payment / layer / FAQ、1009/811 与 final 18-step E2E 仍保持 open。

4C-41 补充：B 已完成 Giant Arm Kato / 巨腕加藤 `SFD·112/221` / cardId `33198` / `FU-464ec8c275` / `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT` guard slice。已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tags `CARD_TYPE:UNIT` + `法盾`；explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 no tick / no events / no payment / no hand movement / no stack / no unit entry / no leak。A/B focused 通过 99/99；Core gap none；D 未运行重测试。本补充只关闭 Giant Arm Kato ordinary hand play-unit keyword-tag target guard representative evidence，不关闭 full-official；Spellshield target tax、move-to-battlefield trigger、friendly-unit choice / prompt、keyword grant、+power until EOT、LayerEngine / duration cleanup、movement / control matrix、FAQ、1009/811 与 final 18-step E2E 仍保持 open。

4C-42 补充：B 已完成 Time Gate / 预时之门 `SFD·078/221` / cardId `33158` / `FU-081d97eb3e` / `TIME_GATE_PLAY_EQUIPMENT` guard slice。已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment；explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。A/B focused 通过 292/292；Core gap none；D 未运行重测试。本补充只关闭 Time Gate ordinary hand play-equipment target guard representative evidence，不关闭 full-official；activated / tap ability、payment `[A]`、next spell gains Echo、optional echo payment / repeat、duration cleanup、equipment exhaust / readiness lifecycle、FAQ timing、1009/811 与 final 18-step E2E 仍保持 open。

4C-43 补充：B 已完成 Sfur Song / 斯弗尔尚歌 `SFD·059/221` / cardId `33139` / `FU-9a623b3185` / `SFUR_SONG_PLAY_EQUIPMENT` guard slice。已覆盖 ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment；explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。A rerun focused 通过 268/268；A backend full 3576/3576 通过；A frontend build 通过；A Chrome smoke 通过；Core unchanged；D 未运行 full tests。本补充只关闭 Sfur Song ordinary hand play-equipment target guard representative evidence，不关闭 full-official；复制宿主技能文字、持续文本 / layer、完整 assemble / equipment attach lifecycle、装备控制权 / 区域移动、FAQ full behavior、1009/811 与 final 18-step E2E 仍保持 open。

## superseded / 防误读

- 0/负战力：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- 具体战场 objectId 大小写：阶段 1 已修复并由 A 验收；后续只保留防回归，不再列为未清零 P0。
- replay/final hash：历史“仍缺严格 action-log replay final-state 校验”口径已被当前 P1-004 状态替代；当前有 representative verifier、恢复前审计和 Postgres smoke，剩余风险是全命令/全恢复/全随机 property。
- 复杂 prompt 降级展示：阶段 1 已完成安全降级与 prompt 戳过期保护；历史“完全没有复杂 prompt 入口”已 superseded。
- 复杂 prompt schema：阶段 2 B 已补 `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` command/schema skeleton 与 malformed payload 稳定拒绝；历史“完全没有正式 schema/稳定拒绝语义”已 superseded。阶段 3A 已补 `PAY_COST` 最小 runtime；阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime；阶段 3D 已补 `ORDER_TRIGGERS` 最小 runtime / UI；阶段 4C-1 已补 `ORDER_TRIGGERS` 保守 APNAP controller-block 子集；阶段 4C-2 已补 Watchful Sentinel 真实多触发入队代表路径；阶段 4C-3 已补 Honest Broker 遗言金币真实入队代表路径；阶段 4C-4 已补 `SFD·220/221` trigger payment / decline 代表路径；阶段 4C-5 / 4C-6 已补 state-based cleanup -> visible Watchful / Honest Broker last-breath enqueue 代表路径；阶段 4C-7 已补 Scouting Warhawk explicit destroy real enqueue 代表路径；阶段 4C-8 已补 Scouting Warhawk state-based cleanup lethal damage real enqueue 代表路径；阶段 4C-9 已补 Sad / Loyal Poro state-based cleanup 条件抽牌 real enqueue 代表路径；阶段 4C-10 已补 Unsung Hero state-based cleanup powerful draw-2 real enqueue 代表路径；阶段 4C-11 已补 Ghostly Centaur state-based cleanup friendly-destroyed power +2 real enqueue 代表路径；阶段 4C-12 已补 Resonant Soul state-based cleanup first-friendly-destroyed draw real enqueue 代表路径；阶段 4C-13 已迁移 Ghostly Centaur / Resonant Soul true stack destruction 旧 immediate compatibility 到 real trigger queue / stack / priority 语义；阶段 4C-14 已补 Savage Jawfish true stack 与 state-based cleanup 两条 friendly-destroyed experience +1 real enqueue 代表路径；阶段 4C-15 记录 Viktor non-minion token trigger 模型 blocker；阶段 4C-15A 已补 `TOKEN_FAMILY:MINION` 前置模型切片；阶段 4C-15B 已补 Viktor destroyed non-minion trigger 代表性 baseline；阶段 4C-16 已补 Mechanical Trickster stack trigger migration；阶段 4C-21 已补 `SFD·218/221` Sunken Temple trigger payment 代表路径。`PAY_COST` 完整 PaymentEngine、`ASSIGN_COMBAT_DAMAGE` 全规则矩阵、`ORDER_TRIGGERS` 完整 trigger engine / 其他 destroyed-family / friendly-destroyed FUs / Viktor full official trigger-count matrix / Savage Jawfish full official trigger-count matrix / Ironclad Vanguard state-based cleanup route / 完整“每回合首次”时序 / 完整同时死亡触发次数 / effective power 或 LayerEngine / temporary modifier / battlefield objectLocation matrix / hidden 或 face-down 原始触发建模 / effect resolution / 更多 trigger payment / FAQ regression 仍是 P0。
- 4C-17 / 4C-18 触发口径：Ironclad Vanguard true stack migration 已由 4C-17 关闭；Mechanical Trickster + Ironclad Vanguard state-based cleanup route 已由 4C-18 关闭代表路径，完整 trigger engine 仍按缺口管理。
- 阶段 3A OPEN 口径：阶段 3A 已由 A 验收并关闭 smoke 基线、三类复杂命令强类型映射、`PAY_COST` 最小 runtime 和前端外壳安全接线；历史“3A 仍待验证/未完成”表述已 superseded。不得把 3A 关闭误读为 Stage 3、3B 或 READY。

## 阶段 2 B 已关闭的 P0 子项

- `PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 有稳定 command 名称。
- 三类 command 有首版 payload 字段名：`paymentId/paymentWindow/paymentChoiceIds`、`battleId/battlefieldId/assignments[].sourceObjectId/targetObjectId/damage`、`triggerIds`。
- malformed payload 可稳定返回 `INVALID_PAYLOAD`；合法形状且进入“已识别但未实现”的执行点时仍返回 `UNSUPPORTED_COMMAND`，窗口/前置状态不满足时可能先走 `PHASE_NOT_ALLOWED` 或 `INVALID_TARGET` 等拒绝路径。

## 阶段 3 D 对战桌面 / 核心流程证据框架

阶段 3 围绕本地双人 1v1 对战桌面的连续路径做审计：创建 / 加入、卡组、准备、开局、起手、第一回合、召符文、打牌、移动、争夺或结算链或法术对决、结束回合、投降或胜负结算。当前结论仍为 **NOT READY**。阶段 3A 已完成最小 Chrome smoke、三类复杂命令强类型映射、`PAY_COST` 最小 runtime 和前端桌面外壳；正式 18 步 E2E、3B/3C 连续对战流程、完整 P0/P1 清零仍未完成。

### 阶段 3A 范围修正

阶段 3A 已完成。宽阶段 3 审计框架保留为后续总图，不作为 READY 依据。3A 只收口 smoke 基线、三类复杂命令强类型映射、`PAY_COST` 最小 runtime、前端对战桌面外壳安全接线。

3A 暂不进入：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、完整 battle runtime、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 battlefield / standby / control / held / conquer lifecycle、完整 PaymentEngine / LayerEngine。3D 已关闭的是 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项。

| 3A P0 | 规则 / 契约依据 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | A 目标文档阶段 3 / Chrome smoke；`CORE-260330` rules 107-129 | 已关闭：`npm run smoke:chrome -- --start-api` 通过，覆盖 API、DevUi、Chrome headless-CDP 与 7 个基础路由 | C / A / D | 3B 再扩双人连续流程与隐藏信息断言 |
| 3A-P0-002 三类复杂命令强类型映射 | `CommandTypes`、三类 command DTO、`ActionPromptContracts` | 已关闭：三类 JSON command -> typed command mapper 已落地，malformed payload 稳定拒绝，后端 full test 3324/3324 通过 | B / D | 后续 runtime 逐类开放时补对应合法性测试 |
| 3A-P0-003 `PAY_COST` 最小 runtime | `CORE-260330` rules 131、135.2.e、162-167、356-357、377、403-405、414、416；`JFAQ-251023` q2.5 | 已关闭最小切片：pending payment prompt、choices、合法提交、stale/invalid/零副作用测试已通过 | B / E / C / D | 完整 PaymentEngine、decline、替代/额外费用仍是后续 P0 |
| 3A-P0-004 前端外壳不裁决规则 | 服务端权威原则；`CORE-260330` rules 107-129；`ActionPromptDto` / `SnapshotDto` | 已关闭 3A 外壳：只读 snapshot/prompt、只提交服务端候选；未冻结 complex prompt safe fallback | C / D | 正式复杂交互等待服务端 runtime 冻结 |

### 阶段 3B 范围修正

当前只执行阶段 3B：Battlefield / Standby / Control / Conquer lifecycle + Central cleanup queue 最小官方化切片。阶段 3B 不启动最终 18 步 E2E，不进入 1009 张卡全量实现，不扩大到完整 battle/damage/trigger runtime，不提交 `riftbound-dotnet.sln`。

3B 暂不进入：完整 battle / spell duel lifecycle、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine、LayerEngine、全路径 replay/determinism。3D 后最小排序窗口已关闭，完整触发系统仍 P0。

| 3B 规则域 | 规则 / FAQ 入口 | 当前状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| Battlefield / standby zone | `CORE-260330` rules 107.2-107.3；rules 315.2.b.2、319-323 | 具体战场移动、`BattlefieldStates`、待命数量和 task view 已有；完整 standby 时机和全卡族仍缺 | B / C / D | 固化 snapshot 字段和隐藏信息边界 |
| Control / contest / freeze | `CORE-260330` rules 187-189、344-348、461-464；`JFAQ-251023` q4.1-q5.4；`SOUL-OFAQ-260114` p21 | 战后控制代表路径、`BATTLEFIELD_CONTROL_RESOLVED`、battlefield contest smoke 已有；freeze/release 仍缺 | B / E / D | 3B 先收代表控制结算，freeze/release 继续 P0 |
| Held / conquer scoring | `CORE-260330` rules 315.2.b.2、461-464；`SOUL-JFAQ-260114` p4-p5 | `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED` 大量代表 fixture 和 snapshot 结果已有 | B / E / D | 3B 收一条 held 与一条 conquer 代表链；全战场卡留后续 |
| Central cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-q5.2；`SOUL-OFAQ-260114` p19-p20 | `PendingTaskQueue`、`PendingCleanupTasks`、blocking guard、非法待命/致命伤害/未贴附装备代表任务已有 | B / C / D | 3B 收最小 task view；全触发面 enqueue 仍 P0 |

3B 关闭候选：

- 3B-CAND-001 battlefield/standby snapshot 只读字段。
- 3B-CAND-002 非法待命 cleanup 代表路径与 blocking guard。
- 3B-CAND-003 control / held / conquer 代表结果可 snapshot/reconnect。
- 3B-CAND-004 central cleanup queue 最小 task view。

3B 仍存在 P0/P1：

- 3B-P0-001 cleanup queue 全触发面未完成。
- 3B-P0-002 control freeze/release 未完成。
- 3B-P0-003 delayed illegal standby removal 全时机未完成。
- 3B-P0-004 held/conquer scoring order 和全战场卡未完成。
- 3B-P0-005 3B smoke 证据不是最终 18 步 E2E。
- 3B-P1-001 前端 battlefield / cleanup 字段仍偏 DevUi，正式 DTO 未冻结。

### 阶段 3C 范围修正

当前只执行阶段 3C：spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的规则证据和最小官方化候选。阶段 3C 不启动最终 18 步 E2E，不进入 1009 张卡全量实现，不标记 READY，也不回滚或重判 3B battlefield / standby / control / conquer 前置产物。

3C 暂不进入：`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine / `DECLINE_PAY_COST`、LayerEngine、全路径 replay/determinism、1009 全量卡牌覆盖、最终正式 18 步 E2E。3D 后最小排序窗口已关闭，完整触发系统仍 P0。

| 3C 规则域 | 规则 / FAQ 入口 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| Spell duel lifecycle | `CORE-260330` rules 307-313、333-348；`JFAQ-251023` q3.1-q3.3 | `SpellDuelState`、`PASS_FOCUS`、焦点恢复、swift/反应 timing context 有代表证据；3C 已补 close -> damage assignment -> cleanup/control 连续测试 | B 主实现；C 只读展示；E fixture；D 审计 | 完整 `SPELL_DUEL_ACTION`、全反应链和触发排序仍留 P0 |
| Battle lifecycle | `CORE-260330` rules 454-461；rules 319-324；`JFAQ-251023` q2.3-q2.4 | `DECLARE_BATTLE` 代表路径、`BattleState`、`BattleResolutionState`、多防守/多参与者代表测试已有；仍偏同步结算片段，不是完整 task | B 主实现；E battle fixture；C 只读展示；D 审计 | 把 START_BATTLE -> battle view -> result/cleanup 串成可审计最小 task |
| Damage assignment | `CORE-260330` rules 142-143、417、460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 3C 已补最小 runtime prompt、validation、stale prompt、zero-side-effect reject 与 simultaneous commit | B 主实现；E 多单位/壁垒/后排/负战力 fixture；C runtime UI；D 审计 | 完整壁垒/后排/同优先级/负战力/不可分配矩阵仍留 P0 |
| Battle cleanup | `CORE-260330` rules 319-324、461-464；`JFAQ-251023` q5.1-q5.2 | 3C 已覆盖 battle damage -> lethal cleanup -> battle close -> battlefield control update | B 主实现；E battle cleanup fixture；C 只读展示；D 审计 | 替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺 |

3C 关闭候选：

- 3C-CAND-001 spell duel focus/pass/close 的最小 lifecycle 证据；3C 专项 close -> next task 仍需 B 测试补齐。
- 3C-CAND-002 battle view / battle resolution 的最小 task 证据。
- 3C-CAND-003 `ASSIGN_COMBAT_DAMAGE` runtime 最小 prompt。
- 3C-CAND-004 battle cleanup 最小结果链。

3C 仍存在 P0/P1：

- 3C-P0-001 spell duel 完整 lifecycle 未完成：3C 已补 focus/pass/close 代表链；全反应链、复杂 `SPELL_DUEL_ACTION`、触发排序和全部 close -> next task 全路径仍缺。
- 3C-P0-002 battle 完整 lifecycle 未完成：完整 battle task、战斗响应窗口、所有多攻防组合和初始栈仍缺。
- 3C-P0-003 `ASSIGN_COMBAT_DAMAGE` full-rule runtime 未完成：3C 最小 prompt / constraints / submit-reject / simultaneous commit 已落地；完整壁垒/后排/同优先级/负战力/不可分配矩阵仍缺。
- 3C-P0-004 battle cleanup 全路径未完成：3C 已有 battle damage -> cleanup -> control update 代表链；替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺。
- 3C-P0-005 3C smoke/evidence 不是最终 18 步 E2E。
- 3C-P1-001 前端 `timing.spellDuel/battle/battleResolutions` 仍是 dictionary view，正式 DTO 未冻结。

### 阶段 3D / 第三阶段收口审计

3D 只做文档 / 规则证据 / 第三阶段收口审计，不实现功能代码，不启动最终验收版 18 步 E2E，不进入 1009 张卡全量，不标记 READY。

第三阶段收口：

- 3A 已关闭：Chrome route smoke、三类复杂命令 typed mapper、`PAY_COST` 最小 runtime、前端外壳不裁决规则。
- 3B 已关闭：battlefield / standby snapshot 只读字段、非法待命 cleanup 代表路径、control / held / conquer 代表结果、central cleanup queue 最小 task view。
- 3C 已关闭：spell duel focus/pass/close 代表链、battle view / battle resolution 最小 task、`ASSIGN_COMBAT_DAMAGE` 最小 runtime prompt / submit / reject / simultaneous commit、battle damage -> cleanup -> control update 代表链。
- 3D 关闭：`ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项、第三阶段审计口径、阶段 4 与最终验收边界的文档风险。

3D 证据状态：

| 规则域 | 当前证据状态 | 仍缺口 | 下一阶段 |
| --- | --- | --- | --- |
| priority / focus | `PASS_PRIORITY`、`PASS_FOCUS`、spell duel focus、prompt stamp、stale prompt 代表证据已有 | 完整 `SPELL_DUEL_ACTION`、全反应 / 迅捷 / 反制链、触发排序交织 | 阶段 4 |
| spell duel close | 3C 已有 close -> damage assignment -> cleanup/control update 代表链 | 所有 close -> next task、非战斗法术对决、触发排序 | 阶段 4 |
| battle lifecycle | `BattleState`、`BattleResolutionState`、多攻防代表路径和 3C 最小 damage assignment 已有 | 完整 battle task、initial stack、响应窗口、freeze/release | 阶段 4 |
| damage assignment | 3C 最小 prompt / validation / submit / reject / simultaneous commit 已关闭 | 壁垒、后排、同优先级、负战力、不可分配、替代/预防矩阵 | 阶段 4 |
| battle cleanup | 3C 已有 battle damage -> lethal cleanup -> battle close -> control update | cleanup queue 全触发面、LayerEngine、control freeze/release | 阶段 4 |
| battlefield control update | 3B/3C 有战后 control update 与重连展示代表证据 | 战斗 / 法术对决期间 freeze 与关闭后 release，全控制改变卡 | 阶段 4 |
| conquer / hold scoring | 3B 有 held/conquer 代表路径与 snapshot 结果 | scoring order、全战场卡、得分替代、付费触发拒付、同时触发排序 | 阶段 4 |
| standby visibility | 3B 有 standby / faceDown count / illegal cleanup 代表路径 | 全 standby 卡族、失控待命全时机、freeze 期间不提前移除、正式 DTO | 阶段 4 |
| cleanup queue | 3B 有 central queue 最小 task view、active task、blocking guard | 全 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue 与 repeat-until-stable audit | 阶段 4 |

`ORDER_TRIGGERS` / 多触发排序：

- 规则入口：`CORE-260330` rules 333-340、383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3、q2.5；battle initial stack 关联 rules 454-461 与 q2.3-q2.4。
- 已有证据：`ORDER_TRIGGERS(triggerIds)` command/schema skeleton、malformed payload `INVALID_PAYLOAD`、`TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 代表事件、部分 `triggerQueue` view。
- 3D 新增证据：B 已实现最小 runtime window，prompt metadata 包含 `orderingPlayerId/orderedTriggerIds/triggerIds/triggers/triggerChoices/legalOrderingConstraints/triggeredByEventKind`；command 支持 `orderedTriggerIds` 并兼容 `triggerIds`；合法排序清空 `TriggerQueue`、按顺序加入 `StackItems`、设置 priority player，事件 `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`。
- B 验证：`ConformanceFixtureShapeTests` 109/109 通过；full `dotnet test Riftbound.slnx --no-restore` 3333/3333 通过；`git diff --check` 通过。
- C 已实现 `ORDER_TRIGGERS` UI，上移 / 下移排序，提交 `orderedTriggerIds`，不本地结算；build / smoke / `stage3-preflight.mjs` 通过。
- E 已补 stage3D 矩阵 overlay 和 `ORDER_TRIGGERS` 证据文档。
- 仍缺 P0：完整 trigger engine、完整 effect resolution、真实卡牌全触发生成、完整 APNAP 多玩家独立排序、battle initial stack 全官方规则、trigger cost / decline / payment。
- 阶段 4C-1 已把最小 runtime 推进到 APNAP controller-block 子集；后续继续扩完整 APNAP / 多玩家独立排序、battle initial stack / attacker-defender order、真实卡牌触发生成和触发费用拒付 / PaymentEngine。

阶段 4 / 最终边界：

- 必须进阶段 4：完整 trigger engine / APNAP / battle initial stack / trigger payment、priority/focus 与 `SPELL_DUEL_ACTION`、完整 battle task、damage assignment 全规则矩阵、battle cleanup / control freeze-release、cleanup queue 全触发面、PaymentEngine、正式 snapshot/prompt DTO 与双窗口隐藏信息 smoke。
- 必须留到最终验收：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、LayerEngine / 替代 / 预防全模型、replay/recovery/determinism 全边界、产品 UI polish。
- A 主控 final validation 已通过，第三阶段可判定 **DONE**；可以准备进入阶段 4，但仍 **NOT READY**。

### 阶段 4C-1 触发排序审计

4C-1 证据入口：`docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 full-official。

4C-1 关闭的 P0 子项：

- `ORDER_TRIGGERS` 升级为保守 APNAP controller-block 子集。
- prompt metadata 约定：`orderedTriggerIds` 是合法 APNAP resolution top-first 默认提交顺序，`triggerIds` 是 raw queue order。
- `legalOrderingConstraints` 明确 APNAP policy、top-first semantics、controller block order、legal resolution block order、跨控制者不可重排、同控制者可重排。
- runtime 校验覆盖合法排序 accepted；非法跨控制者重排 rejected 且 no state mutation。
- `ORDER_TRIGGERS` prompt 优先于 `START_BATTLE` / task prompt。
- battle initial stack 代表证据覆盖 active battle attacker / defender 初始触发 -> `ORDER_TRIGGERS` -> stack priority。
- trigger prompt / snapshot 对不可见 face-down standby source 做 viewer 级脱敏。

规则证据入口：

- Trigger ordering / APNAP：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- Battle initial stack：`CORE-260330` p35-p36 rules 341-348；`CORE-260330` p77-p78 rules 454-461；`JFAQ-251023` p2-p4 q2.2-q2.4。
- Hidden information / face-down standby source：`CORE-260330` p4-p8 rules 107-129；待命 / 显露相关 evidence 继续复用 `CORE-260330` p39-p42 rules 355-356；更精确 FAQ 页码暂为 evidence TODO。

验证记录：

- A 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3337/3337。

仍缺 P0/P1：

- P0：完整 trigger engine、完整 effect resolution、真实卡牌全触发生成。
- P0：trigger payment / decline / payment failure 与完整 PaymentEngine 统一。
- P0：完整 APNAP 多玩家独立排序；4C-1 只关闭 controller-block 子集。
- P0：battle initial stack 全官方规则、攻防触发特殊排序、battle response window 与 FAQ 回归。
- P0：最终正式 18 步 E2E、1009 张卡 full-official 覆盖。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、产品解释字段、多语言 UI 文案和证据链接。

### 阶段 4C-2 真实触发入队审计

4C-2 证据入口：`docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-2 关闭的 P0 子项：

- 真实 `UNIT_DESTROYED` 路径中，多张 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）遗言抽牌触发已接入 `TriggerQueue`。
- 多触发代表路径已串成 `TriggerQueue` -> `ORDER_TRIGGERS` prompt -> `StackItems` -> pass priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- 单个 Watchful Sentinel 保留即时结算兼容；本批不宣称统一单触发策略。
- 跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交并 accepted；非法跨控制者排序 rejected 且 no state mutation。
- 未改协议 / 前端。

规则证据入口：

- 真实卡牌触发入队：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3338/3338。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family / friendly-destroyed / attack / conquer 触发族。
- P0：state-based cleanup 触发统一入队。
- P0：trigger payment / decline / payment failure。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段、单触发即时结算兼容策略文档化。

### 阶段 4C-3 绝念真实触发入队审计

4C-3 证据入口：`docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-3 关闭的 P0 子项：

- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 从直接结算扩展到真实多触发路径。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）遗言金币代表路径已串成 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EQUIPMENT_TOKEN_CREATED`。
- 与 4C-2 合并后，Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）和 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）两条 last-breath 代表路径已有 real enqueue 证据。
- 跨控制者真实 last-breath APNAP 默认顺序可直接提交 accepted；非法跨控制者排序 rejected 且 no state mutation。
- 单触发 Watchful Sentinel / Honest Broker 仍保留即时结算兼容；本批不宣称统一单触发策略完成。

规则证据入口：

- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Watchful Sentinel last-breath enqueue：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3339/3339。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family、state-based cleanup 触发入队。
- P0：trigger payment / decline / payment failure。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段、单触发即时结算兼容策略文档化。

### 阶段 4C-4 触发支付 / 拒付审计

4C-4 证据入口：`docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-4 关闭的 P0 子项：

- `SFD·220/221`《珍宝堆》征服触发进入服务端权威 `TRIGGER_PAYMENT`。
- `PAY_COST` 支持 `SPEND_MANA:1` 和 `DECLINE` 两个服务端合法选项。
- 支付成功扣 1 点法力并创建休眠“金币”装备指示物；拒付关闭窗口且不扣费、不创建指示物。
- wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 都拒绝且 no state mutation。

规则证据入口：

- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5；`CATALOG` SFD·220/221。
- `PAY_COST` runtime validation：`CORE-260330` p39-p42 rules 356-357；p52-p55 rules 403-405。
- Battlefield conquer trigger：`CORE-260330` p77-p78 rules 454-461；`CATALOG` SFD·220/221。

验证记录：

- A focused trigger payment：11/11 通过。
- A trigger ordering regression：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3344/3344。
- A frontend build / Chrome smoke / stage3 preflight：通过。

仍缺 P0/P1：

- P0：完整 PaymentEngine。
- P0：`SFD·220/221` 之外 triggered-cost functional units。
- P0：完整 trigger engine、state-based cleanup trigger enqueue、完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TRIGGER_PAYMENT` 长期 DTO / 解释字段 / UX 契约冻结。

### 阶段 4C-5 State-Based Cleanup Trigger Enqueue 审计

4C-5 证据入口：`docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-5 关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Watchful Sentinel last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两名 Watchful，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- hidden / standby Watchful Sentinel 不入队，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Watchful Sentinel last-breath enqueue：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused RealTriggerQueueTests：4/4 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3346/3346。
- A frontend build：通过，仅有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：隐藏 / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

### 阶段 4C-6 Honest Broker Cleanup Trigger Enqueue 审计

4C-6 证据入口：`docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-6 关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两个 Honest Broker，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不改协议或前端，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused RealTriggerQueueTests：6/6 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3348/3348。
- A frontend build：通过，仅有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

### 阶段 4C-7 Scouting Warhawk Trigger Enqueue 审计

4C-7 证据入口：`docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-7 关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）explicit destroy real trigger enqueue representative。
- Spirit Fire / 《妖异狐火》（`CATALOG` OGN·256/298）作为 explicit destroy source；本批不是 state cleanup。
- explicit destroy `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- single-trigger compatibility 保留，既有 `P79ScoutingWarhawk` 测试继续通过。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Spirit Fire explicit destroy source：`CATALOG` OGN·256/298；`CORE-260330` p39-p42 rules 355-356；`CORE-260330` p62-p63 rule 428。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：9/9 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3350/3350。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：state cleanup Warhawk（4C-8 后续已补代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-8 Scouting Warhawk Cleanup Trigger Enqueue 审计

4C-8 证据入口：`docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-8 关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）state-based cleanup lethal damage real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source；本批不是 explicit destroy source 的新增覆盖。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- 4C-7 explicit destroy 路径和 single-trigger compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3352/3352。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：Sad / Loyal Poro（4C-9 后续已补 state-based cleanup 条件抽牌代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-9 Poro Cleanup Trigger Enqueue 审计

4C-9 证据入口：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-9 关闭的 P0 子项：

- Sad Poro / 《哀哀魄罗》（`CATALOG` SFD·036/221，`FU-f8bfd5c6f9`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Sad Poro / 《哀哀魄罗》（`CATALOG` UNL-221/219，`FU-938b749c23`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Loyal Poro / 《忠忠魄罗》（`CATALOG` UNL-156/219，`FU-0415e3b46d`）`LOYAL_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad 条件：base-zone、visible、非 face-down、非 standby，且同位置无其他友方正面非待命单位时触发。
- Loyal 条件：base-zone、visible、非 face-down、非 standby，且同位置有至少一个其他友方正面非待命单位，并且该友方不在本轮 cleanup removal set 中时触发。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 同位置其他友方也同时被 cleanup 摧毁的落单判定未官方化；runtime 对 Loyal 采取保守不入队，本批不宣称完整规则。
- 现有 explicit destroy P79 Sad / Loyal immediate compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Sad Poro condition draw：`CATALOG` SFD·036/221；`CATALOG` UNL-221/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Loyal Poro condition draw：`CATALOG` UNL-156/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3358/3358。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：battlefield objectLocation Poro condition matrix。
- P0：simultaneous cleanup condition timing，尤其同位置其他友方也同时被 cleanup 摧毁时的 Sad / Loyal 判定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-10 Unsung Hero Cleanup Trigger Enqueue 审计

4C-10 证据入口：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-10 关闭的 P0 子项：

- Unsung Hero / 《无名英雄》（`CATALOG` SFD·167/221，`FU-1701d1d89a`）`UNSUNG_HERO_LAST_BREATH_DRAW_2_IF_POWERFUL` state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero power >= 5 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN` x2。
- power < 5 cleanup 路径不入队、不抽牌。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Unsung immediate compatibility 保留。
- 严格边界：本批只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Unsung Hero powerful last-breath draw：`CATALOG` SFD·167/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Power threshold guard：`CATALOG` SFD·167/221；`CORE-260330` p57 rule 413.4；相关强力证据见 `rules-evidence-index.md` strong / powerful fixtures。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3361/3361。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：effective power / LayerEngine、temporary modifier 和完整强力判定。
- P0：battlefield objectLocation matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-11 Ghostly Centaur Cleanup Trigger Enqueue 审计

4C-11 证据入口：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-11 关闭的 P0 子项：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）friendly-destroyed power state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Ghostly source -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +2。
- hidden / face-down / standby / opponent-controlled Ghostly source 不入队、不显示 prompt metadata、不加战力。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 同一 Ghostly source 在同一个 cleanup pass 中最多入队一次，这是 4C-11 保守边界，不代表完整规则。
- 真实 stack destruction Ghostly 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；这是 4C-11 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Ghostly Centaur friendly-destroyed power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed"` 通过，23/23。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3364/3364。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整同时死亡触发次数、同一 cleanup pass 多对象时序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：真实 stack destruction Ghostly 从 immediate compatibility 迁移到 `TriggerQueue`。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-12 Resonant Soul Cleanup Trigger Enqueue 审计

4C-12 证据入口：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-12 关闭的 P0 子项：

- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）first-friendly-destroyed draw state-based cleanup real trigger enqueue representative。
- Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source。
- Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant Soul source，owner not already in `DestroyedUnitOwnerIdsThisTurn` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN` 1。
- hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队、不显示 prompt metadata、不抽牌。
- source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队。
- true stack destruction Resonant 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；cleanup 事件跳过旧 immediate helper 防重复；这是 4C-12 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Per-owner first destroy guard：`CATALOG` OGN·118/298；`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂为 TODO。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn"` 通过，27/27。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3368/3368。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：true stack destruction Resonant Soul 从 immediate compatibility 迁移到 `TriggerQueue`。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-13 Stack Destroyed Trigger Migration 审计

4C-13 证据入口：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

4C-13 不新增 FU；本批迁移 / 关闭 4C-11 / 4C-12 留下的 P1：Ghostly Centaur + Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue。

覆盖 FUs：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）。
- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）。

4C-13 关闭的 P1 / P0 子项：

- P1：Ghostly Centaur true stack destruction friendly-destroyed power +2 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P1：Resonant Soul true stack destruction first-friendly-destroyed draw 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P0 子项：两个既有 FU 现在同时具备 cleanup representative 与 true stack destruction representative 的 real enqueue 证据。
- true stack destruction 非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1。
- cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免重复入队。
- hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller。
- Resonant 继续尊重 `DestroyedUnitOwnerIdsThisTurn`。
- 旧 P79 tests 已更新为 queue / stack / priority 语义。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Stack destruction `UNIT_DESTROYED` 触发入队：`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Ghostly Centaur power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Cleanup / stack helper 防重复：`CORE-260330` p31-p33 rules 318-324；工程事件来源契约。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂为 TODO。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaur|FullyQualifiedName~P79ResonantSoul"` 通过，30/30。
- B full backend：passed，3370/3370。
- A backend full：passed，3370/3370。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 通过。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

### 阶段 4C-14 Savage Jawfish Trigger Enqueue 审计

4C-14 证据入口：`docs/CURRENT_STAGE4C_BATCH14_SAVAGE_JAWFISH_TRIGGER_AUDIT.md`。本节只更新规则证据 / P0-P1 审计，不代表 READY，不代表 1009 / 811 full-official。

覆盖 FU：

- Savage Jawfish / 《凶残颚鱼》（`CATALOG` UNL-129/219，`FU-bd94334cc5`）。

4C-14 关闭的 P0 子项：

- true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 两条路径均可让 visible surviving friendly Savage Jawfish source 进入 `TriggerQueue`。
- 多触发走 `ORDER_TRIGGERS`；单触发走 auto-stack；priority 双方 pass 后结算为 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- source guard：来源必须仍在场、face-up、non-standby、同 controller，且不能是被摧毁对象 / cleanup removal set。
- hidden / face-down / standby / opponent-controlled source 不 enqueue、不泄漏 prompt metadata、不加经验。
- 旧 P79 Savage Jawfish fixture 已更新为 queue / stack / priority 语义。

明确仍缺：

- P1：同一来源同一 cleanup / stack pass 多个友方被摧毁时，当前最小切片保守 cap 为每 source 每 pass 最多一次；这不是 full official trigger-count matrix。
- P0：完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent。
- P0：hidden / face-down 原始触发建模、完整 effect resolution、FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。

规则证据入口：

- Stack destruction `UNIT_DESTROYED` 触发入队：`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Savage Jawfish experience +1：`CATALOG` UNL-129/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；更精确 experience 官方页码仍为 evidence TODO。
- Source legality / visibility guard：`CORE-260330` p4-p8 rules 107-129；hidden / face-down / standby 触发建模 FAQ 页码仍为 TODO。
- Per-source per-pass cap：当前是实现保守边界；完整官方 trigger-count matrix 仍为 TODO。

验证记录：

- focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79SavageJawfish"` 通过，33/33。
- backend full：passed，3374/3374。
- frontend build：passed。
- Chrome smoke：passed。
- Stage3 preflight：passed。
- git diff --check：passed。

### 阶段 4C-15 Viktor Feasibility Blocker 审计

4C-15 证据入口：`docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`。本节只记录可行性阻断，不代表 READY，不代表 1009 / 811 full-official。

候选范围：

- Viktor destroyed non-minion token trigger / `FU-b5cb36a5c9`。
- 4C-14 Savage Jawfish 已 checkpoint：`2deef64 checkpoint: complete stage 4C savage jawfish trigger batch`。

B 只读检查结果：

- 未修改代码。
- 未新增测试。
- 当前 `CardObjectTags` 没有 `Minion` / `随从` / subtype 字段。
- 当前 `CardObjectState` 没有稳定 token family / subtype / `isMinion` 字段。
- 多个“随从”创建路径经 `CreateBaseUnitTokens` 只落成 `CARD_TYPE:UNIT`，不保留 `cardNo` / `tokenName` / `TokenFamilyName`。
- 摧毁时无法可靠区分“随从单位”和普通单位。
- Viktor fixtures 当前也描述 destroyed-listener / non-minion filtering / minion-token path deferred。

D 审计结论：

- 不建议硬编码。
- 4C-15 未实现，不关闭 `FU-b5cb36a5c9`。
- 该项作为 P0/P1 blocker 记录，需要先冻结 token subtype / family 模型或用户裁定官方解释。
- 下一步建议：先做 `CardObjectState` subtype / token-family 模型和随从 token factory 统一写入，再做 Viktor；或者用户确认跳过 Viktor，改做不依赖“非随从”分类的下一个 safe FU。

仍缺 P0/P1：

- P0：Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger。
- P0：token subtype / token-family / minion classification 模型。
- P0：完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs。
- P0：Kogmaw / Karthus / Undercover Agent。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：Viktor fixtures 的 destroyed-listener / non-minion filtering / minion-token path 仍 deferred。

### 阶段 4C-15A Minion Token Family 审计

4C-15A 证据入口：`docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_AUDIT.md`。本节只记录 token subtype / family / minion classification 最小前置模型，不代表 READY，不代表 1009 / 811 full-official。

4C-15A 已关闭的前置子项：

- 新增稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- `P6TokenFactoryCatalog` 的官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）带该 tag。
- `CoreRuleEngine.CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接创建随从路径同步带 `TOKEN_FAMILY:MINION`。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield held minion 等路径可生成带 marker 的随从 token。
- 普通单位不带 marker；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker。
- hidden face-down standby 即使内部带 marker，对手 snapshot 仍不泄漏 tags / cardNo / power。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3375/3375。
- `git diff --check` passed。

仍缺 P0/P1：

- P0：Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 本体。
- P0：destroy / cleanup 入队时 destroyed target pre-removal state 判定。
- P0：完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

### 阶段 4C-15B Viktor Trigger Baseline 审计

4C-15B 证据入口：`docs/CURRENT_STAGE4C_BATCH15B_VIKTOR_TRIGGER_AUDIT.md`。本节只记录 Viktor destroyed non-minion trigger 最小官方化代表切片，不代表 READY，不代表 1009 / 811 full-official。

4C-15B 已关闭的代表性子项：

- 目标 FU：`FU-b5cb36a5c9` / Viktor destroyed non-minion token trigger，覆盖 `ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- visible surviving friendly Viktor source 看到另一名友方非随从单位被摧毁时触发。
- destroyed target 使用 pre-removal `CardObjectState` 判定：unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily`。
- source guard：Viktor still on field、face-up、non-standby、same controller、not cleanup removal set。
- 覆盖 true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED`。
- trigger path：`TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED`，在 controller base 创建 1-power Zaun minion `OGN·273/298`，并带 `TOKEN_FAMILY:MINION`。
- minion target、hidden / face-down / standby / opponent source、source also dying 均不入队、不泄漏、不造 token。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3380/3380。
- B `git diff --check`：pending / expected passed；A 将在文档后再次复核。

仍缺 P0/P1：

- P1：same source same stack / cleanup pass multiple non-minion friendly deaths 的 full official trigger-count matrix 仍保守 one source once。
- P0：Kogmaw / Karthus / Undercover Agent 等其他 destroyed-family / friendly-destroyed FUs。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

### 阶段 4C-16 Mechanical Trickster Trigger 审计

4C-16 证据入口：`docs/CURRENT_STAGE4C_BATCH16_MECHANICAL_TRICKSTER_TRIGGER_AUDIT.md`。本节只记录 Mechanical Trickster last-breath create minions 的旧 immediate -> real trigger queue 代表迁移，不代表 READY，不代表 1009 / 811 full-official。

4C-16 已关闭的代表性子项：

- Mechanical Trickster / 《机械戏法师》 / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`。
- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。
- face-down / standby Mechanical Trickster 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` fixture 已更新为 queue / priority semantics。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3382/3382。
- A frontend build / smoke：将在 D 文档后继续运行；本 D 审计不提前记为完成。

仍缺 P0/P1：

- P1：Ironclad Vanguard 仍是旧 immediate compatibility，未迁移到 real trigger queue。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

### 阶段 4C-17 Ironclad Vanguard Trigger 审计

4C-17 证据入口：`docs/CURRENT_STAGE4C_BATCH17_IRONCLAD_VANGUARD_TRIGGER_AUDIT.md`。本节记录 Ironclad Vanguard last-breath create robots 的旧 immediate -> real trigger queue 代表迁移口径，不代表 READY，不代表 1009 / 811 full-official。

4C-17 已关闭代表子项：

- Ironclad Vanguard / 《铁甲先锋》 / `SFD·021/221` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`。
- 矩阵 FU：`FU-6d0971786b`；`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 作为 4C-17 overlay `triggerEffectKind`。
- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x2。
- face-down / standby Ironclad Vanguard 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` fixture 已更新为 queue / priority semantics。

验证：

- B focused filter：通过 42/42。
- B backend full：通过 3384/3384。
- A backend full：通过 3384/3384。
- A frontend build：通过。
- A Chrome smoke：通过。
- `git diff --check`、矩阵 JSON/断言通过。

仍缺 P0/P1：

- 已关闭 P1 子项：Ironclad Vanguard true stack destruction 旧 immediate migration 已迁移到 real trigger queue / stack / priority 代表路径。
- 仍留 P1：Ironclad Vanguard state-based cleanup last-breath route 未在本批官方化。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

### 阶段 4C-18 Mechanical + Ironclad Cleanup Trigger 审计

4C-18 证据入口：`docs/CURRENT_STAGE4C_BATCH18_MECHANICAL_IRONCLAD_CLEANUP_TRIGGER_AUDIT.md`。本节记录 cleanup route 的 verified 口径，不代表 full-official，不代表 READY，不代表 1009 / 811 full-official。

4C-18 关闭项：

- Mechanical Trickster / 《机械戏法师》 / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` 的 state-based cleanup lethal damage 路线。
- Ironclad Vanguard / 《铁甲先锋》 / `SFD·021/221` / 冻结矩阵 FU `FU-6d0971786b` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 的 state-based cleanup lethal damage 路线。
- 已验证路径：cleanup `LETHAL_DAMAGE` -> `UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Mechanical token x3 / Ironclad token x2。
- 已验证 guard：hidden / face-down / standby source 不入队、不泄漏 prompt metadata、不创建 token。

已回填证据：

- B focused filter：通过 47/47。
- B backend full 与 A backend full：通过 3388/3388。
- A frontend build 与 Chrome smoke：通过。
- `git diff --check`：通过。
- 代表 lethal damage 来源：Starfall cleanup route。
- 代表测试：`StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack`、`StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack`、`StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers`、`StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers`。

仍缺 P0/P1：

- 4C-18 已验证这两个 cleanup-route representative baseline；完整 trigger engine、multiplicity、FAQ regression 与 full-official 仍不能从 P0/P1 列表移除。
- Kogmaw / Karthus / Undercover Agent。
- full trigger engine、full effect resolution、trigger batch / optional trigger choice、完整 APNAP。
- multiplicity：same source same cleanup pass / same stack pass 多对象触发次数矩阵。
- hidden original visibility、FAQ regression、1009 / 811 full-official、正式 18-step E2E。

### 阶段 4C-19 Kogmaw Last-Breath AoE 审计

4C-19 证据入口：`docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_AUDIT.md`。本节记录 Kogmaw AoE last-breath 的 verified representative baseline，不代表 READY，不代表 1009 / 811 full-official。

4C-19 关闭项：

- Kogmaw / 克格莫 / `OGN·190/298` / `FU-af8b05c294`。
- 只覆盖 visible、face-up、field source 的 last-breath AoE damage representative baseline。
- 已验证路径：`UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> battlefield units take 4 damage -> cleanup queue stabilizes。
- AoE 使用 source pre-removal battlefield location，只伤害该 battlefield 的当前单位；其他 battlefield 单位不受伤害。
- hidden / face-down / standby source 与缺少 battlefield location 的 source 不入队、不泄漏、不造成伤害。

已回填证据：

- Focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests&FullyQualifiedName~Kogmaw"` 通过 4/4。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3392/3392。
- Frontend build 与 Chrome smoke：通过。
- `git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-19 matrix assertions：通过。
- 代表测试：`RealKogmawLastBreathDealsFourToDestroyedBattlefieldAndCleanupStabilizes`、`StateBasedCleanupKogmawLastBreathDealsFourToDestroyedBattlefield`、`StateBasedCleanupHiddenKogmawsDoNotEnqueueOrDealAoeDamage`、`RealKogmawDestroyedWithoutBattlefieldLocationDoesNotEnqueueOrDealDamage`。
- 协议 / 前端字段：本批无变更。

仍缺 P0/P1：

- Karthus 额外绝念、Undercover Agent discard / draw。
- full trigger engine、full effect resolution、trigger batch / optional trigger choice、完整 APNAP。
- multiplicity：same source same pass / simultaneous destruction / AoE damage 后多轮 cleanup 与触发交织矩阵。
- hidden original visibility、FAQ regression、1009 / 811 full-official、正式 18-step E2E。

### 阶段 4C-20B Undercover Agent Triggered Hand-Choice 审计

4C-20B 证据入口：`docs/CURRENT_STAGE4C_BATCH20B_UNDERCOVER_HAND_CHOICE_AUDIT.md`。本节记录 Undercover Agent 服务端 hand-choice prompt 微切片，不代表 READY，不代表 1009 / 811 full-official。

4C-20B 关闭项：

- Undercover Agent / 卧底特工 / `OGN·178/298` / `FU-6a52b04cb2`。
- 只覆盖 Undercover 绝念触发结算中的服务端 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` prompt。
- viewer-specific `handChoices` redaction：选择玩家可见候选手牌，对手只看到脱敏等待信息。
- wrong player、stale prompt、invalid choice、malformed / illegal payload 拒绝且 no mutation。
- `CORE-260330` p62 / rule `422.4`：1 / 0 手牌 shortfall 已裁决，弃尽可弃数量后仍抽两张。

已回填证据：

- A focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UndercoverAgentTriggerTests"` 通过 6/6。
- 协议 / 前端状态：服务端 prompt 微切片已落地；C frontend sync 已完成，前端只展示服务端候选并提交 `CHOOSE_HAND_CARDS`，不得本地裁决。

仍缺 P0/P1：

- Karthus 额外绝念 optional / multiplicity / multi-Karthus / visibility 裁决。
- 非 Undercover 的通用 discard / hand-choice engine、其它 hand-choice FUs。
- full trigger engine、full effect resolution、trigger batch、完整 APNAP。
- public/private discard event redaction 全矩阵、replay / spectator hand-choice redaction。
- hidden original visibility、FAQ regression、1009 / 811 full-official、正式 18-step E2E。

### 阶段 4C-21 Sunken Temple Trigger Payment 审计

4C-21 证据入口：`docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_AUDIT.md`。本节记录 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 征服强力单位后的服务端权威 trigger payment 代表切片，不代表 READY，不代表 1009 / 811 full-official。

4C-21 关闭项：

- 旧 immediate auto pay + draw 路径已 superseded。
- 征服此处且战场上留存强力单位时，服务端打开 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- `PAY_COST(SPEND_MANA:1)` 支付成功抽 1；`PAY_COST(DECLINE)` 拒付关闭窗口且不抽牌。
- focused 覆盖 invalid / stale / insufficient 等 no-mutation 语义；不得把该代表路径外推为完整 PaymentEngine。

已回填证据：

- `CATALOG` `SFD·218/221`；FU `FU-05ce012700`；`SOUL-OFAQ-260114` p15。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- A/B focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P79BattlefieldConquerPowerfulUnitPaysOneToDraw|FullyQualifiedName~P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws"` 通过 13/13。
- 协议 / 前端状态：本批未记录协议或前端字段变更；前端仍只提交服务端候选，不本地裁决征服、强力或抽牌。

仍缺 P0/P1：

- 完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、更多非出牌支付窗口。
- 完整 battlefield / conquer lifecycle、战场控制冻结、battle cleanup、征服 / 据守得分全规则矩阵。
- Sunken Temple full-official timing matrix：effective power / LayerEngine、temporary modifier、征服后才变强力、战场上多单位同时离场等组合。
- full trigger engine、full effect resolution、FAQ regression、1009 / 811 full-official、正式 18-step E2E。

| 流程 / P0 | 规则依据入口 | 当前实现状态 | 分类 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- | --- |
| 创建 / 加入 / 卡组 / 准备 / 开局连续链路 | `CORE-260330` rule 103；rules 107-129；工程会话契约 | 后端和 UI 有分散代表路径；缺同一双浏览器阶段 3 smoke | 阻断 smoke | B/C；D 审计 | C 补最小 Chrome smoke，B 保持服务端权威，D 记录证据 |
| 起手调整与隐藏信息 | `CORE-260330` rules 107-129；开局流程 | `MULLIGAN` 与 selection guard 有代表测试；DOM/store/WS 双窗口隐藏断言未收口 | 阻断 smoke | B/C；D 审计 | smoke 同时断言对手手牌、牌堆顺序、hidden metadata 不泄漏 |
| 第一回合 / 召符文 / 抽牌 | `CORE-260330` p20 rules 164-167；p28-p29 rule 315；rule 481.7 | `p2-preflight-turn-start-*` 已有；缺浏览器连续证明 | 阻断 smoke | B/C；D 审计 | 断言 turn、active player、phase、runePool、hand count 来自 snapshot |
| 打出卡牌 / 支付 | `CORE-260330` rules 349+、355-357、377、403-405；`JFAQ-251023` q2.5 | 代表 `PLAY_CARD` / `COST_PAID` 有；阶段 3A 已补 `PAY_COST` 最小切片，完整 PaymentEngine 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B 主；C 等 schema；E fixture；D 审计 | smoke 用简单服务端候选先闭环；B 继续完整 payment window |
| 移动 / 争夺 / 结算链 / 法术对决 | `CORE-260330` rules 187-189、307-313、333-348、344-348；`JFAQ-251023` q2.2-q5.4 | 具体战场移动、代表争夺、stack/pass/focus 有；完整 lifecycle 未关闭 | 阻断 smoke；可在阶段 3 内继续 | B/C/E；D 审计 | smoke 选择一条最短路径，不关闭完整 lifecycle P0 |
| 结束回合 / 投降或胜负 | `CORE-260330` rules 316-324、461-464；产品会话契约 | end-turn / result 代表路径有；连续浏览器 result 未收口 | 阻断 smoke | B/C；D 审计 | smoke 至少进入下一回合，并覆盖投降或服务端 winner/result |

阶段 3 B 服务端阻断关闭时，D 必须在 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md` 的记录位补齐：规则依据、实现状态、测试证据、仍缺口和 D 审计结论。

## 仍未清零阻断

- P0-S2-001 battlefield / standby / control / held / conquer lifecycle 进入 3B 最小切片；代表 snapshot/control/held/conquer 子项可候选关闭；4C-36 已补 Hostile Takeover enemy public battlefield unit gain-control + ready 与 turn-end return scheduling representative baseline，但完整 standby / reaction timing、battle-start / conquer branch、control-zone lifecycle、owner/controller matrix 与 end-turn cleanup task model 未完成。
- P0-S2-002 cleanup queue 进入 3B 最小切片；代表 task view/非法待命/致命伤害子项可候选关闭，但全触发面 central queue 未完成。
- P0-S2-003 spell duel / battle lifecycle 进入 3C 最小切片；focus/pass、battle view、battle resolution 代表子项可候选关闭，但完整 lifecycle 未完成。
- P0-S2-004 `ASSIGN_COMBAT_DAMAGE` runtime 进入 3C 最小切片；schema skeleton、最小 runtime prompt、damage assignment phase、submit/reject 和 simultaneous commit 子项已关闭，完整全规则矩阵未完成。
- P0-S2-005 `PAY_COST` 完整 PaymentEngine 与 `DECLINE_PAY_COST` / 替代费用 / 额外费用 / 非出牌支付窗口仍未完成；阶段 3A 最小 runtime 切片已关闭；4C-4 Treasure Pile、4C-21 Sunken Temple、4C-24 Vayne、4C-25 Icevale Archer 与 4C-26 Jax triggered payment 代表路径已关闭但不外推为完整 PaymentEngine；4C-28 Battle or Flight、4C-29 Gust、4C-30 Hunt the Weak、4C-31 Reprimand、4C-32 Ride the Wind、4C-35 Vengeance、4C-36 Hostile Takeover 与 4C-37 Berserk Impulse 补充普通打出费用后结算与 invalid target no-payment/no-mutation guard，也不外推为完整 PaymentEngine。
- P0-S2-006 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项已关闭；4C-1 APNAP controller-block 子集、battle initial stack 代表证据和 hidden trigger source redaction 子项已关闭；4C-2 Watchful Sentinel 多触发真实入队、经排序 / stack / priority 结算和非法排序 no mutation 子项已关闭；4C-3 Honest Broker 遗言金币真实多触发排序 / 入栈 / 结算代表路径已关闭；4C-4 Treasure Pile 触发支付 / 拒付 / 支付失败 no-mutation 代表路径已关闭；4C-5 至 4C-22 多条 trigger / cleanup / token / hand-choice 代表路径已关闭；4C-23 Lux high-cost spell temporary power representative baseline 已关闭；4C-24 Vayne conquer trigger payment recall representative baseline 已关闭；4C-25 Icevale Archer attack trigger payment representative baseline 已关闭；4C-26 Jax weapon attach trigger payment draw representative baseline 已关闭；4C-27 Treasure Hunter move Gold representative baseline 已关闭；完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs、attack-trigger family、weapon-attachment trigger family、move-trigger family、Kogmaw / Karthus / Undercover Agent、Viktor full official trigger-count matrix、Savage Jawfish full official trigger-count matrix、Ironclad Vanguard state-based cleanup route、完整“每回合首次”时序、完整同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation matrix、hidden / face-down 原始触发建模、更多 trigger payment / decline、完整 effect resolution、FAQ regression、1009/811 full-official 未完成。
- 4C-18 已关闭 Mechanical Trickster + Ironclad Vanguard state-based cleanup route 的 representative baseline；完整 trigger engine、multiplicity、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-19 已关闭 Kogmaw last-breath AoE damage representative baseline；Kogmaw FAQ adjudication、full AoE damage matrix 与 full-official 仍按 P0/P1 缺口管理。
- 4C-20B 已关闭 Undercover Agent triggered hand-choice prompt 微切片和前端安全接线；非 Undercover 通用 discard / hand-choice engine、Karthus 额外绝念、其它 hand-choice FUs、full-official 仍按 P0/P1 缺口管理。
- 4C-21 已关闭 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 征服强力单位后的 `TRIGGER_PAYMENT` / `PAY_COST` 代表路径；完整 PaymentEngine、Sunken Temple full-official timing matrix、battlefield / conquer lifecycle、FAQ regression 仍按 P0/P1 缺口管理。
- 4C-23 已关闭 Lux / 拉克丝 `OGS·006/024` / `FU-f18a49e06d` high-cost spell temporary power representative baseline；完整 trigger engine、PaymentEngine、LayerEngine、high-cost spell family 与 full-official 仍按 P0/P1 缺口管理。
- 4C-24 已关闭 Vayne / 薇恩 `OGN·035/298` / `FU-c027639a3c` 征服后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 回手或拒付不变更代表路径；full Assault3、active-entry、完整 conquer/control-zone matrix、full PaymentEngine、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-25 已关闭 Icevale Archer / 冰谷弓箭手 `UNL-065/219` / `FU-c170628e3a` 进攻后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 令同处目标本回合 -1 或拒付不变更代表路径；full attack-trigger family、完整 target selection prompt、支付后恢复战斗时点、Spellshield target tax、LayerEngine、full PaymentEngine、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-26 已关闭 Jax / 贾克斯 `SFD·119/221` / `SFD·119a/221` / `FU-73f3be35df` 武装贴附后 `TRIGGER_PAYMENT` / `PAY_COST` 支付 1 抽 1 或拒付不变更代表路径；full Forge / 百炼 / assemble lifecycle、full equipment attachment rules、full optional trigger family / order triggers、full PaymentEngine、draw / replacement / hidden-zone matrix、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-27 已关闭 Treasure Hunter / 寻宝猎人 `SFD·130/221` / `FU-6144ab0271` successful move -> dormant Gold equipment token representative baseline；完整 movement / control-zone / roam lifecycle、move-trigger family、Gold token full resource / reaction ability、equipment token full rules、hidden / face-down trigger policy、Karthus design gate、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-28 已关闭 Battle or Flight / 战或逃 `OGN·168/298` / `FU-813144e7d4` move battlefield unit -> owner base 与 target guard hardening representative baseline；swift spell-duel timing、face-down standby reaction play、完整 movement / control-zone matrix、targeting prompt、PaymentEngine、FAQ regression 与 full-official 仍按 P0/P1 缺口管理。
- 4C-29 已关闭 Gust / 罡风 `OGN·169/298` / `FU-48662b7661` return public battlefield unit with power <= 3 to owner hand 与 target guard hardening representative baseline；swift / reaction timing、完整 return-to-hand / movement / control-zone matrix、targeting prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理。
- 4C-30 已关闭 Hunt the Weak / 狩魂 `UNL-159/219` / `FU-282b6e3149` destroy public battlefield unit with power <= 3 to owner graveyard 与 target guard hardening representative baseline；swift / reaction timing、完整 destroy / cleanup / Last Breath trigger interactions、targeting prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；replacement / prevention / cleanup / full targeting matrix 保持 P1/P2 后续项，不新增为本批 P0。
- 4C-31 已关闭 Reprimand / 责退 `OGN·172/298` / `FU-d0383ed260` return public battlefield unit to owner hand 与 target guard hardening representative baseline；swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / control-zone matrix、targeting prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；这些 Reprimand-specific breadth items 保持 P1/P2 后续项，不新增为本批 P0。
- 4C-32 已关闭 Ride the Wind / 驭风而行 `OGN·173/298` / cardId `31403` / `FU-6f84196631` ready + move friendly public battlefield unit to owner base 与 target guard hardening representative baseline；swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、targeting prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；这些 Ride-the-Wind-specific breadth items 保持 P1/P2 后续项，不新增为本批 P0。
- 4C-35 已关闭 Vengeance / 复仇 `OGN·229/298` / cardId `31467` / `FU-07104fa58a` destroy public unit target to owner graveyard 与 target guard hardening representative baseline；完整 destroy / cleanup / replacement / prevention / Last Breath interaction、attached-equipment detach / replacement breadth、destroyed-this-turn memory、targeting prompt、PaymentEngine、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；这些 Vengeance-specific breadth items 保持 P1/P2 后续项，不新增为本批 P0。
- 4C-36 已关闭 Hostile Takeover / 恶意收购 `SFD·202/221` / cardId `33301` / `FU-00ee09c2cc` gain control + ready enemy public battlefield unit 与 target guard hardening representative baseline；standby / reaction timing、battle-start / conquer branch、完整 battlefield / control-zone lifecycle、owner/controller matrix、end-turn cleanup task model、targeting prompt、PaymentEngine、FAQ regression、Berserk Impulse / Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；这些 Hostile-Takeover-specific breadth items 保持 P1/P2 后续项，不新增为本批 P0。
- 4C-37 已关闭 Berserk Impulse / 暴怒冲动 `OGN·025/298` / cardId `31231` / `FU-b05eda44ce` opponent top main-deck unit target guard representative baseline；full hidden-zone reveal / choose / recycle 仍为 P0 / design-gated，multi-opponent reveal、未选牌回收、non-unit branch、hidden-zone prompt / redaction、spell duel / reaction timing、PaymentEngine、LayerEngine、FAQ regression、Edge of Night / Karthus / Aphelios design gates 与 full-official 仍按 P0/P1 缺口管理；这些 Berserk-Impulse-specific breadth items 保持后续项，不新增为本批 P0。
- 4C-38 已关闭 Edge of Night / 夜之锋刃 `SFD·139/221` / cardId `33229` / `FU-804412488c` play-equipment / assemble-purple target guard representative baseline；Core gap none；Edge of Night face-down standby immediate attach 仍为 P0 / design-gated，full standby immediate attach、hidden redaction、equipment layer、FAQ、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-39 已关闭 Zhonya's Hourglass / 中娅沙漏 `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` ordinary hand play-equipment target guard representative baseline；Core gap none；standby / reaction timing、destroy replacement recall、完整 equipment / layer / FAQ、hidden info、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-40 已关闭 Sea Monster Hook / 海兽钓钩 `OGN·242/298` / cardId `31482` / `FU-2653af0380` ordinary hand play-equipment target guard representative baseline；Core gap none；activated ability：pay 1 + yellow + exhaust、destroy friendly unit、top-five look / choice、free play、recycle remainder、hidden / zone / payment / layer / FAQ、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-41 已关闭 Giant Arm Kato / 巨腕加藤 `SFD·112/221` / cardId `33198` / `FU-464ec8c275` ordinary hand play-unit keyword-tag target guard representative baseline；Core gap none；Spellshield target tax、move-to-battlefield trigger、friendly-unit choice / prompt、keyword grant、+power until EOT、LayerEngine / duration cleanup、movement / control matrix、FAQ、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-42 已关闭 Time Gate / 预时之门 `SFD·078/221` / cardId `33158` / `FU-081d97eb3e` ordinary hand play-equipment target guard representative baseline；Core gap none；activated / tap ability、payment `[A]`、next spell gains Echo、optional echo payment / repeat、duration cleanup、equipment exhaust / readiness lifecycle、FAQ timing、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-43 已关闭 Sfur Song / 斯弗尔尚歌 `SFD·059/221` / cardId `33139` / `FU-9a623b3185` ordinary hand play-equipment target guard representative baseline；Core unchanged；复制宿主技能文字、持续文本 / layer、完整 assemble / equipment attach lifecycle、装备控制权 / 区域移动、FAQ full behavior、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-44 已关闭 Akshan / 阿克尚 `SFD·109/221` / cardId `33194` / `FU-7419ee7d9d` ordinary hand no-optional / no-extra play-unit guard representative baseline；后端 full 3582/3582 passed，前端 build passed，Chrome smoke passed；optional assemble、orange-orange extra play、enemy equipment move / control、weapon attach、control-until-leaves cleanup、LayerEngine / continuous effects、FAQ full behavior、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-45 已关闭 Switcheroo / 换换乐 `SFD·145/221` / cardId `33237` / `FU-0b6332bbf0` representative battlefield power-swap target guard overlay；后端 full 3594/3594 passed，前端 build passed，Chrome smoke passed；true LayerEngine、later modifier ordering、duration cleanup / EOT expiry、same-battlefield precision beyond current representative model、damage / battle math、full FAQ `SOUL-JFAQ-260114 p14`、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-46 已记录 Void Burrower / 虚空遁地兽 `SFD·187/221` / `FU-6e7d0dba2c` 与 Sett / 腕豪 `OGN·269/298` / `FU-6308c2db01` legend-domain / shared-oracle design gate；B/C/D/E 只读门禁一致 NO-GO for direct runtime implementation；LegendActivePredicate、LegendOptionalTrigger、RevealChoice、ReplacementPayment、shared oracle reprint mapping、hidden redaction、`PAY_COST` / cleanup queue interactions、FAQ `SOUL-JFAQ-260114 p14` / `SOUL-OFAQ-260114 p4`、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-47 已关闭 Draven / 德莱文 `SFD·020/221` / cardId `33092` 与 `SFD·020a/221` / cardId `33093` / `FU-964b214448` ordinary hand play-unit body + guard representative slice；Core / frontend / protocol 未改；battle win dormant Gold、attack / defense optional red payment、+2 until EOT、full PaymentEngine、Layer / duration cleanup、FAQ refs、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-48 已记录 Vex / 薇古丝 `UNL-150/219` / cardId `34697` / `FU-9f7cb73dc4` / `VEX_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` test-only ordinary hand spellshield-tag play-unit + guard representative baseline；focused 35/35 passed，backend full 3607/3607 passed，frontend build passed，Chrome smoke passed；不实现 / 不宣称 opponent-unit stun runtime；opponent unit-play listener、battlefield-only condition、`STUNNED` application、cannot-move-this-turn duration、movement guard / cleanup、Spellshield full target tax、FAQ adjudication、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-49 已记录 Ezreal / 伊泽瑞尔 `SFD·082/221` / cardId `33162`、`SFD·082a/221` / cardId `33163`、`SFD·082b/221·P` / cardId `33164` / `FU-2dca1ad450` ordinary hand play-unit + guard representative baseline；focused 21/21 passed，backend full 3617/3617 passed，frontend build passed，Chrome smoke passed；不实现 / 不宣称 combat-damage / move runtime；attack / defense trigger、“此处” enemy unit target selection、damage equal to Ezreal power、cannot combat damage static、blue swift move to base、swift / reaction timing、blue payment / `PAY_COST`、movement / control-zone matrix、damage prevention / replacement / cleanup、Layer / effective power、FAQ refs、1009/811 与 final 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-50 已记录 Draven / 德莱文 `SFD·148/221` / cardId `33240` 与 `SFD·148a/221` / cardId `33241` / `FU-104211dbbc` ordinary hand play-unit + `法盾` tag guard representative baseline；focused 17/17 passed，backend full 3625/3625 passed，frontend build passed，Chrome smoke passed；不实现 / 不宣称 battle / scoring runtime；battle win scoring、destroyed-in-battle opponent scoring、Spellshield target tax、battle cleanup / score once-per-turn matrix、PaymentEngine、FAQ refs、1009/811 与 formal 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-51 已记录 Rek'Sai / 雷克塞 `SFD·170/221` / cardId `33264` 与 `SFD·170a/221` / cardId `33265` / `FU-422b450261` ordinary hand play-unit guard representative baseline；focused 25/25 passed，backend full 3633/3633 passed，frontend build passed，Chrome smoke passed；不实现 / 不宣称 attack reveal runtime 或 movement runtime；attack reveal runtime、top-2 reveal、free play、recycle remainder、unit destination to current battlefield / "here"、hidden-info redaction / reveal matrix、`ORDER_TRIGGERS`、battle lifecycle full matrix、movement / control-zone、FAQ refs、1009/811 与 formal 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-52 已记录 Rek'Sai / 雷克塞 `SFD·029/221` / cardId `33104` 与 `SFD·029a/221` / cardId `33105` / `FU-1945f6918c` ordinary hand no-optional play-unit + keyword tag guard representative baseline；focused 305/305 passed，backend full 3641/3641 passed，frontend build passed，Chrome smoke passed；不实现 / 不宣称 full-official haste / overwhelm runtime；`HASTE_READY` paid branch full matrix、red resource exactness、Overwhelm / 强攻 battle modifier、`ASSIGN_COMBAT_DAMAGE` overflow behavior、non-hand friendly unit gains haste、LayerEngine、hidden-info、FAQ refs、1009/811 与 formal 18-step E2E 仍按 P0/P1 缺口管理。
- 4C-53 已记录 Sett / 腕豪 `OGN·269/298` / cardId `31512` / `FU-6308c2db01` / `LEGEND_ACTION_DOMAIN` representative automated evidence overlay；focused 54/54 passed，backend full 3647/3647 passed，frontend build passed，Chrome smoke passed；本批只关闭 Sett representative automated evidence gap，不做 direct runtime implementation，不关闭 full-official NO-GO；LegendActivePredicate、LegendOptionalTrigger、ReplacementPayment、boon consume official semantics、dormant recall cleanup、conquest ready lifecycle full matrix、shared oracle mapping、`PAY_COST` prompt / decline、cleanup queue interactions、FAQ adjudication、1009/811 与 formal E2E 仍按 P0/P1 缺口管理。
- 4C-54 已记录 Void Burrower / 虚空遁地兽 `SFD·187/221` / cardId `33285` 与 `SFD·243/221` / cardId `33354` / `FU-6e7d0dba2c` / `LEGEND_ACTION_DOMAIN` representative automated evidence overlay；focused 32/32 passed，backend full 3650/3650 passed，frontend build passed，Chrome smoke passed；本批只关闭 Void Burrower representative automated evidence gap，不做 direct runtime implementation，不关闭 full-official NO-GO；LegendActivePredicate、LegendOptionalTrigger、RevealChoice、shared oracle mapping、hidden / reveal redaction matrix、optional trigger prompt / decline、free-play official semantics、recycle remainder official semantics、unit destination / zone ownership details、`ORDER_TRIGGERS` / battle lifecycle full matrix、FAQ adjudication、1009/811 与 formal E2E 仍按 P0/P1 缺口管理。
- 4C-15 Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger 已记录为 feasibility blocker；4C-15A 已补 `TOKEN_FAMILY:MINION` 最小前置模型并部分关闭 token classification blocker；4C-15B 已关闭 Viktor 代表性 baseline，但 same-source 多对象 full official matrix、Kogmaw / Karthus / Undercover Agent、完整 trigger engine 仍未关闭。
- 3A-P0-001 / 002 / 003 / 004 已关闭；不得把这些 3A 子项误读为完整 Stage 3 或 READY。
- 3B-CAND-001 / 002 / 003 / 004 只能作为阶段 3B 关闭候选；D/A 证据入账前不得移出 P0。
- 3C-CAND-001 / 002 / 003 / 004 只能作为阶段 3C 关闭候选；D/A 证据入账前不得移出 P0，且不得替代最终 18 步 E2E。
- 3C-P0-001 / 002 / 003 / 004 / 005 仍阻断 READY；3C-P1-001 仍需正式 DTO 冻结。
- 3D 关闭 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项和第三阶段文档收口口径；A final validation 已通过，第三阶段可判定 DONE，允许进入阶段 4 不等于 READY。
- S3-P0-001 双浏览器连续核心链路未完成；3A 基础 Chrome route smoke 已完成。
- S3-P0-002 创建 / 加入 / 卡组 / 准备 / 开局 / 起手 / 第一回合未以同一阶段 3 smoke 证明。
- S3-P0-003 打牌 / 移动 / 争夺或结算链或法术对决 / 结束回合 / 投降或胜负未以同一阶段 3 smoke 证明。
- S3-P0-004 阶段 3 隐藏信息 DOM/store/WS 断言未收口。
- LayerEngine 未统一；持续/替代/预防/层系统解释仍不能支撑正式规则助手。
- 全官方卡牌证据与执行仍未完成。
- 正式 18 步 E2E 未最终收口，尤其需要覆盖双人窗口、隐藏信息、复杂 prompt、战斗/法术对决、断线重连。

## 推荐 D/E 子 agent 任务

- D-Protocol：继续维护 `docs/CURRENT_STAGE1_PROTOCOL_BASELINE.md`，每次服务端协议改动后同步真实 DTO、命令 union、错误码与不存在 DTO 列表。
- D-P0Contract：维护 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`，每轮对齐 P0-S2-001 至 P0-S2-006 的证据链、实现状态和 owner。
- D-Stage3A：维护 `docs/CURRENT_STAGE3A_PLAN.md`，阶段 3A 只跟踪 smoke 基线、typed mapper、`PAY_COST` 最小 runtime、前端外壳安全接线，不扩大到 3B/3C。
- D-Stage3B：维护 `docs/CURRENT_STAGE3B_PLAN.md`，阶段 3B 只跟踪 battlefield / standby / control / conquer lifecycle 与 central cleanup queue 最小官方化切片，不扩大到 18 步 E2E 或 1009 全量。
- D-Stage3C：维护 `docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`，阶段 3C 只跟踪 spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的规则证据、关闭候选和仍缺口，不扩大到最终 18 步 E2E、1009 全量、完整 PaymentEngine / `ORDER_TRIGGERS` / LayerEngine。
- D-Stage3D：维护 `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`，第三阶段收口只判断阶段性关闭项、仍缺 P0/P1、阶段 4 入口和最终验收边界；不得宣称 READY。
- D-Stage3Flow：维护 `docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`，每次 B/C 报告阶段 3 smoke 或服务端阻断关闭时，补规则依据、实现状态、测试证据、仍缺口和是否仍阻断。
- D-FrontendContract：继续把正式复杂 prompt 字段草案拆成 `PAY_COST`、`ORDER_TRIGGERS`、`ASSIGN_COMBAT_DAMAGE`、`SPELL_DUEL_ACTION` 四张契约清单；标注 `PAY_COST` 已有最小 runtime、`SFD·220/221` Treasure Pile 与 `SFD·218/221` Sunken Temple `TRIGGER_PAYMENT` 代表路径，完整 PaymentEngine 未开放；`ASSIGN_COMBAT_DAMAGE` 只有最小 runtime、完整全规则矩阵未开放；`ORDER_TRIGGERS` 已到 Watchful Sentinel + Honest Broker 两条 last-breath real enqueue、visible Watchful / Honest cleanup enqueue、Warhawk explicit / cleanup enqueue、Sad / Loyal Poro cleanup enqueue、Unsung Hero cleanup enqueue、Ghostly Centaur cleanup / stack enqueue、Resonant Soul cleanup / stack enqueue、Savage Jawfish stack / cleanup enqueue、Viktor destroyed non-minion baseline、Mechanical Trickster / Ironclad Vanguard stack 与 cleanup baseline、Kogmaw AoE baseline、Undercover hand-choice prompt 代表路径；但完整 trigger engine / 其他 destroyed-family / friendly-destroyed FUs / Viktor full official trigger-count matrix / Savage Jawfish full official trigger-count matrix / 完整“每回合首次”时序 / 完整同时死亡触发次数 / effective power 或 LayerEngine / temporary modifier / battlefield objectLocation matrix / hidden 或 face-down 原始触发建模 / effect resolution / 更多 trigger payment / FAQ regression 未开放，`SPELL_DUEL_ACTION` 仍没有完整 runtime prompt，现有降级面板只能临时承接展示。
- D-Stage4C-18：已回填 Mechanical Trickster + Ironclad Vanguard state-based cleanup trigger baseline 的 focused/full/smoke/diff 结果；无协议字段变化，C 无需新增复杂交互。
- E-RuleEvidence：把五份官方规则/FAQ PDF 与 2026-04-27 官网卡牌快照继续映射到 P0-S2-001 至 P0-S2-006，尤其补 `JFAQ` q2.2/q2.3/q2.5/q5.4/q6.x 和 `SOUL-OFAQ` p21 的 fixture 锚点。
- E-Conformance：为仍未清零 P0 建最小官方场景，优先覆盖失控待命延迟移除、恶意收购非战斗法术对决、完整 APNAP 多玩家独立排序、其他 destroyed / last-breath / friendly-destroyed FUs、Viktor full official trigger-count matrix、Kogmaw / Karthus / Undercover Agent、Savage Jawfish full official trigger-count matrix、Ironclad Vanguard state-based cleanup route、完整“每回合首次”时序、完整同时死亡触发次数、hidden / face-down 原始触发建模、触发费用拒付、多单位伤害分配、战斗清理与征服/据守得分。

## A 主控验收记录

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZeroPower|FullyQualifiedName~NegativePower|FullyQualifiedName~LowercaseOfficialBattlefield|FullyQualifiedName~MoveUnitCommandMovesBaseUnitToConcreteBattlefield|FullyQualifiedName~ActionPromptOffersConcreteBattlefieldsForBaseUnitMove"`：11/11 通过。
- `git diff --check`：通过。
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore`：3304/3304 通过。
