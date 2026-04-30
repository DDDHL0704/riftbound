# 规则证据索引

更新时间：2026-04-30

## 1. 目的

本索引用来把五份官方 PDF/FAQ 映射到新项目的规则域、fixture 和实现状态。它不是规则全文摘录，而是开发时定位证据的目录。

后续每条规则能力、fixture 和卡牌行为都必须能关联到这里的证据条目，或在本文件中补充新的条目。

## 2. 文档版本

| 证据编号 | 文件 | 页数 | 当前用途 |
|---|---|---:|---|
| `CORE-260330` | `《符文战场》核心规则_260330.pdf` | 105 | 核心规则主干。 |
| `JFAQ-251023` | `裁判FAQ_251023.pdf` | 10 | 裁判 FAQ；重点补充结算链、触发、优先行动权、战场控制权、清理、战斗伤害等场景。 |
| `SOUL-JFAQ-260114` | `铸魂淬炼系列_裁判FAQ.pdf` | 25 | `铸魂淬炼` 裁判 FAQ；重点补充系列勘误、战斗得分、法盾、回响、控制权等卡牌场景。 |
| `SOUL-OFAQ-260114` | `铸魂淬炼系列_官方FAQ_260114.pdf` | 21 | `铸魂淬炼` 官方 FAQ；包含立即生效的规则阐明和勘误。 |
| `BREAK-JFAQ-260416` | `《符文战场》破限系列_裁判FAQ_260416.pdf` | 11 | `破限` 裁判 FAQ；基于 2026-03-30 核心规则后的最新裁判问题。 |

## 3. 优先级提示

- 官网卡面和核心规则黄金法则仍是最终冲突裁决入口。
- FAQ 用于澄清核心规则中不准确、不完善或未覆盖的具体场景。
- 系列 FAQ 中更具体、更新的条目优先于较旧或更泛化的裁判 FAQ。
- 旧 Java fixture 只能作为 `legacyOracle`，不能覆盖本索引中的 PDF/FAQ 裁决。

## 4. 核心规则域索引

| 规则域 | 证据 | 开发影响 |
|---|---|---|
| 黄金/白银法则 | `CORE-260330` p1, rules 000-056 | 卡面优先、无法高于可以、尽可能执行、所属者区域保护。所有 handler 和 fixture 的冲突裁决入口。 |
| 构筑与开局区 | `CORE-260330` p2-p3, rule 103 | 牌组校验、传奇、英雄、主牌堆、符文牌堆、战场数量。 |
| 区域与信息公开性 | `CORE-260330` p4-p8, rules 107-129 | PlayerSnapshot 必须区分公开、私密、隐秘信息；前端不能拿到对手手牌或牌堆顺序。 |
| 所有权/控制权 | `CORE-260330` p22-p26, rules 179, 187-189 | `ownerId`、`controllerId` 必须贯穿单位、装备、战场、指示物和技能控制权。 |
| 回合状态 | `CORE-260330` p26-p27, rules 307-310 | 普通/法术对决、开环/闭环构成四种状态，决定 action prompt。 |
| 优先行动权与焦点 | `CORE-260330` p27-p28, rules 311-313; `JFAQ-251023` p4-p5, questions 3.1-3.3 | `ActionPrompt` 必须表达唯一活跃玩家、焦点、待处理效果导致的暂时不可行动。 |
| 回合开始流程 | `CORE-260330` p28-p29, rule 315 | 唤醒、据守、召出两张符文、抽牌、清空符文池。 |
| 主阶段和结束回合 | `CORE-260330` p29-p31, rules 316-317 | 玩家无自决行动时进入回合结束；回合结束特殊清理、清空符文池、下一玩家成为回合玩家。 |
| 清理 | `CORE-260330` p31-p33, rules 318-324; `JFAQ-251023` p6-p7, questions 5.1-5.4 | 清理期间不结算合法项目；可加入待处理项目；重复清理直到状态稳定；战斗/回合结束特殊清理要独立建模。 |
| 结算链与让过 | `CORE-260330` p33-p35, rules 333-340 | 待处理项目、确认、执行、让过、结算的 FEPR 流程；`PASS` 不能只是“结束回合”的别名。 |
| 法术对决 | `CORE-260330` p35-p36, rules 341-348; `JFAQ-251023` p4-p5, questions 3.1-3.3 | 焦点传递、初始结算链、进攻/防守触发会影响谁能行动。 |
| 打出卡牌 | `CORE-260330` p36 onward, rules 349+; `JFAQ-251023` p1-p4, questions 1.1-2.5 | 打出/确认流程需要选择、费用、合法性、待处理效果；触发式技能费用可拒付。 |
| 抽牌 | `CORE-260330` p57, rule 413 | 抽牌是限定行动；牌堆不足触发燃尽。 |
| 符文池与资源 | `CORE-260330` p20, rules 164-167; p29-p31, rules 315.4, 317.2 | 法力/符能进入符文池后再支付；抽牌阶段结束和回合结束清空。 |
| 战场控制权 | `CORE-260330` p24-p26, rules 187-189; `JFAQ-251023` p5-p7, questions 4.1-5.4; `SOUL-OFAQ-260114` p21 | 战场争夺期间控制权通常冻结；“恶意收购”类场景存在官方特例，必须以 FAQ 修正 Java 行为。 |
| 战斗与得分 | `CORE-260330` p77-p78, rules 461-464; `JFAQ-251023` p7-p10, questions 5.3-6.x; `SOUL-JFAQ-260114` p4-p5 | 战斗清理、征服/据守、胜利分数、无法得分转抽牌等都需要专门 fixture。 |
| 装备/贴附/顶部卡牌 | `CORE-260330` p89, rules 718-719; `SOUL-JFAQ-260114` p22-p23 | 装备控制权、顶部卡牌、卸除顺序和区域归属必须独立建模。 |
| 关键词 | `CORE-260330` p92-p105, rules 800+; `SOUL-OFAQ-260114` p1-p4, p21; `SOUL-JFAQ-260114` p2, p19 | 法盾、回响、急速、百炼等存在 FAQ 修正；关键词实现前必须逐条查 FAQ。 |

## 5. FAQ 问题索引

| FAQ | 问题范围 | 需要优先转成 fixture 的场景 |
|---|---|---|
| `JFAQ-251023` p1-p2 | 打出卡牌、无目标、效果中打出另一张牌 | 空目标合法性、效果内打出卡牌的待处理状态。 |
| `JFAQ-251023` p2-p4 | 结算链与触发式技能 | 同时触发排序、进攻/防守触发、触发费用拒付。 |
| `JFAQ-251023` p4-p5 | 优先行动权、焦点、活跃玩家 | 初始结算链后谁获得优先行动权，获得资源技能不传递焦点。 |
| `JFAQ-251023` p5-p7 | 战场控制权、清理、特殊清理 | 战斗中控制权冻结、待命牌移除时机、清理不结算合法项目。 |
| `JFAQ-251023` p7-p10 | 战斗、伤害分配、卡牌 Q&A | 战斗清理、壁垒/伤害分配同优先级选择、牌堆为空时特定触发。 |
| `SOUL-OFAQ-260114` p1-p4 | 官方勘误、法盾、从牌堆打出 | 法盾按每次选为目标收费；从牌堆打出的卡先放逐再打出。 |
| `SOUL-OFAQ-260114` p21 | 恶意收购、回响、百炼 | 控制权导致非战斗法术对决、回响重复“指示”、百炼为可选。 |
| `SOUL-JFAQ-260114` p1-p5 | 系列勘误、战斗得分 | 星落/艾卡西亚暴雨、法盾、征服/据守与胜利分数。 |
| `SOUL-JFAQ-260114` p19 | 回响与费用 | 回响重复流程和费用判定。 |
| `SOUL-JFAQ-260114` p22-p23 | 控制权与装备相关 Q&A | 装备/控制权高风险 fixture 来源。 |
| `BREAK-JFAQ-260416` p1-p5 | 破限后规则更新相关卡牌 | 2026-03-30 规则后发生变化的答案，优先于旧系列 FAQ。 |
| `BREAK-JFAQ-260416` p5-p11 | 新增 Q&A 与其他常见卡牌 Q&A | 后续实现破限卡牌前逐条纳入 fixture。 |

## 6. 当前 Fixture 审查映射

| Fixture | 状态 | 初始证据 | 下一步 |
|---|---|---|---|
| `java-oracle-p1-pass` | `NEEDS_RULE_AUDIT` | `CORE-260330` p27 rule 310, p27-p28 rules 312-313, p34-p35 rules 335-340, p36 rules 347-348；`JFAQ-251023` p4-p5 questions 3.1-3.3；`BREAK-JFAQ-260416` p2-p5 scan | 拆清 `PASS_PRIORITY`、`PASS_FOCUS`、主阶段 `END_TURN` 的协议语义；旧 Java `PASS -> TURN_ENDED` 只能当 legacy 行为。 |
| `java-oracle-p1-end-turn` | `NEEDS_RULE_AUDIT` | `CORE-260330` p29 rules 315.3-315.4, p30 rules 316.1-316.6, p30-p31 rules 317.1-317.3, p31 rules 318-322, p57 rule 413；`JFAQ-251023` p6-p7 questions 5.1-5.2；`BREAK-JFAQ-260416` p11 scan | 明确 `END_TURN` 是主阶段结束意图；后续事件需要拆成回合结束、特殊清理、符文池清空、下一回合开始、召出和抽牌。 |
| `java-oracle-p1-duplicate-pass` | `NEEDS_RULE_AUDIT` | 工程幂等契约；`command_log(match_id, player_id, client_intent_id)` 唯一；不属于卡牌规则 PDF 裁决 | 保留为服务端权威/网络重试 fixture；不要把它当作游戏规则 fixture。 |
| `p2-preflight-turn-start-runes-and-draw` | `RULE_AUDITED` | `CORE-260330` p20 rules 164-167, p28-p29 rule 315, rule 430, rule 481.7 | 已验证 P2 非首个回合普通回合开始：召出 2 张符文、抽 1 张牌、抽牌阶段结束清空符文池、进入主阶段。 |
| `p2-preflight-turn-start-short-rune-deck` | `RULE_AUDITED` | `CORE-260330` p28-p29 rule 315.3.b.1, rule 430, rule 481.7 | 已验证符文牌堆不足两张时有多少召出多少。 |
| `p2-preflight-turn-start-first-p2-extra-rune` | `RULE_AUDITED` | `CORE-260330` p28-p29 rule 315.3, rule 430, rule 481.7 | 已验证 1v1 第二个行动玩家首个召出阶段额外召出 1 张符文。 |
| `p2-preflight-turn-start-burnout` | `RULE_AUDITED` | `CORE-260330` p28-p29 rule 315.4, p57 rule 413.4, p67 rule 431.2, rule 481.7 | 已验证抽牌阶段主牌堆为空且废牌堆有牌可回收时执行燃尽、对手得 1 分、回收后完成抽牌。 |
| `p2-preflight-turn-start-burnout-empty-graveyard-wins` | `RULE_AUDITED` | `CORE-260330` p57 rule 413.4, p67-p68 rules 431.1-431.3, p31 rule 323.1 | 已验证燃尽后主牌堆仍为空时继续燃尽；对手达到获胜分且领先时立即获胜，不等待清理。 |
| `p2-preflight-end-turn-advances-to-next-start` | `RULE_AUDITED` | `CORE-260330` p29-p31 rules 316.1-317.3, p20 rules 164-167, p28-p29 rule 315, rule 481.7；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证 P1 主阶段 `END_TURN` 会记录回合结束声明、执行无伤害/无持续效果的最小特殊清理、清空符文池、推进到 P2，并自动结算 P2 回合开始。 |
| `p2-preflight-end-turn-special-cleanup` | `RULE_AUDITED` | `CORE-260330` p30-p31 rules 317.2.a-317.2.f, p31-p33 rules 318-324；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证回合结束特殊清理会移除单位伤害、令期限为本回合内的效果失效、清空符文池，并自动结算下一回合开始。 |
| `p2-preflight-cleanup-repeats-until-stable` | `RULE_AUDITED` | `CORE-260330` p31-p33 rules 318-324；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证特殊清理导致对象状态变化后追加一次常规清理检查，并且不重复执行回合结束特殊清理步骤。 |
| `p2-preflight-pass-priority-does-not-end-turn` | `RULE_AUDITED` | `CORE-260330` p27-p28 rules 312-313, p33-p35 rules 333-340；`JFAQ-251023` p4-p5 questions 3.1-3.3 | 已验证普通主阶段没有优先行动权窗口时 `PASS_PRIORITY` 返回 `PHASE_NOT_ALLOWED`，不产生日志事件、不推进 tick、不结束回合。 |
| `p2-preflight-fepr-priority-pass-resolves-stack` | `RULE_AUDITED` | `CORE-260330` p33-p35 rules 333-340；`JFAQ-251023` p4-p5 questions 3.1-3.3 | 已验证有已确认结算链项目时，当前优先权玩家让过后优先权转移，所有玩家让过后结算最新项目并回到普通主阶段。 |
| `p2-preflight-fepr-resolves-latest-keeps-remaining-stack` | `RULE_AUDITED` | `CORE-260330` p35 rule 340.4 | 已验证最新项目结算后若结算链仍不为空且无待处理项目，则新的最新项目控制者获得优先行动权。 |
| `p2-preflight-spell-duel-pass-focus-closes-window` | `RULE_AUDITED` | `CORE-260330` p35-p36 rules 341-348；`JFAQ-251023` p4-p5 questions 3.1-3.3 | 已验证法术对决中当前焦点玩家让过后焦点传递，所有玩家让过焦点后关闭法术对决并回到普通主阶段。 |
| `p2-preflight-play-punishment-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-007/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《惩戒》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后造成 3 点伤害，并施加本回合若被摧毁则改为放逐的替代效果。 |
| `CoreRuleEngineRejectsPunishmentAgainstBaseUnit` | `RULE_AUDITED` | `CATALOG` UNL-007/219；`CORE-260330` p39-p42 rules 355-356 | 已验证官方法术《惩戒》的官网卡面限定为“战场上的一名单位”，不能指定基地单位。 |
| `p2-preflight-punishment-lethal-damage-banishes-unit` | `RULE_AUDITED` | `CATALOG` UNL-007/219；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 323-324；p62-p63 rule 428 | 已验证官方法术《惩戒》对 3 战力战场单位造成 3 点伤害后，目标因替代效果改为放逐而非移入废牌堆。 |
| `p2-preflight-punishment-banishes-if-destroyed-later` | `RULE_AUDITED` | `CATALOG` UNL-007/219、OGN·229/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 323-324；p62-p63 rule 428 | 已验证《惩戒》建立的本回合替代效果可覆盖稍后由《复仇》造成的摧毁，将目标改为放逐且不计入本回合摧毁记忆。 |
| `p2-preflight-shattered-fire-draws-after-lethal-damage` | `RULE_AUDITED` | `CATALOG` OGN·005/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《碎裂之火》对战场单位造成 3 点伤害，若该单位被此法术摧毁，则控制者抽 1 张牌。 |
| `p2-preflight-shattered-fire-does-not-draw-without-destroy` | `RULE_AUDITED` | `CATALOG` OGN·005/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《碎裂之火》未摧毁目标时不满足卡面抽牌条件。 |
| `p2-preflight-play-sinful-pleasure-discard-damage` | `RULE_AUDITED` | `CATALOG` OGN·008/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《罪恶快感》先弃置一张友方手牌，再按被弃牌法力费用对战场单位造成非致命伤害；对手手牌目标由直接测试拒绝。 |
| `p2-preflight-starfall-damages-two-units` | `RULE_AUDITED` | `CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《星落》按两次选择分别对单位造成 3 点伤害，并在同一结算后摧毁多个达到战力伤害的目标。 |
| `p2-preflight-starfall-can-damage-same-unit-twice` | `RULE_AUDITED` | `CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《星落》的两次伤害选择可指向同一单位，并累计伤害后摧毁目标。 |
| `p2-preflight-play-duel-mutual-power-damage` | `RULE_AUDITED` | `CATALOG` OGN·128/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《决斗》让一名友方单位和一名敌方单位以自身战力互相造成伤害，并在致命伤害清理中摧毁目标。 |
| `p2-preflight-play-gentleman-duel-power-then-mutual-damage` | `RULE_AUDITED` | `CATALOG` OGS·008/024；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《绅士决斗》先让友方目标本回合内战力 +3，再按两名目标当前战力互相造成伤害。 |
| `p2-preflight-play-marching-orders-echo-mutual-power-damage` | `RULE_AUDITED` | `CATALOG` SFD·114/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428；p92-p105 keyword rules 800+ | 已验证官方法术《行军号令》支付回响后重复友方单位与敌方战场单位按自身战力互相造成伤害。 |
| `p2-preflight-play-clash-of-giants-mutual-power-damage` | `RULE_AUDITED` | `CATALOG` UNL-110/219；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《巨人之战》让任意两名单位以自身战力互相造成伤害，并覆盖基地单位与战场单位互伤。 |
| `p2-preflight-icathian-rain-can-hit-same-unit-six-times` | `RULE_AUDITED` | `CATALOG` OGN·248/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《艾卡西亚暴雨》的六次伤害选择可指向同一单位，并累计伤害后摧毁目标。 |
| `p2-preflight-play-blade-whirlwind-damage-all-battlefield-units` | `RULE_AUDITED` | `CATALOG` OGN·133/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 323-324 | 已验证官方法术《剑刃飓风》0 目标入栈后对所有战场上的单位各造成 1 点伤害；当前样例锁定未致命伤害路径。 |
| `p2-preflight-blade-whirlwind-lethal-damage-destroys-units` | `RULE_AUDITED` | `CATALOG` OGN·133/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 323-324；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《剑刃飓风》造成的全战场伤害达到多个单位战力后，会逐一摧毁这些单位并移入各自拥有者废牌堆。 |
| `p2-preflight-play-cannon-barrage-damage-enemy-combat-units` | `RULE_AUDITED` | `CATALOG` OGN·127/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 323-324；p39-p42 rules 355-356 | 已验证官方法术《加农炮幕》0 目标入栈后只对战斗中的敌方单位各造成 2 点伤害；当前最小模型以 `isAttacking` / `isDefending` 识别战斗中的单位。 |
| `p2-preflight-play-production-surge-create-robot-draw` | `RULE_AUDITED` | `CATALOG` SFD·076/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《产量激增》全额费用路径下打出一名 3 战力“机器人”单位指示物到控制者基地，然后抽 1 张牌。 |
| `p2-preflight-play-production-surge-reduced-by-mechanical` | `RULE_AUDITED` | `CATALOG` SFD·076/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4 | 已验证《产量激增》在控制者控制带 `机械` 标签的单位时费用减少 2，并继续按卡面打出“机器人”后抽牌。 |
| `p2-preflight-play-common-cause-create-four-minions-base` | `RULE_AUDITED` | `CATALOG` OGS·015/024；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《共同献身》选择基地目的地后打出四名 1 战力“随从”单位指示物到控制者基地，并锁定同源 token ID 顺序。 |
| `p2-preflight-play-featherstorm-create-warhawks` | `RULE_AUDITED` | `CATALOG` UNL-044/219；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《羽毛旋风》选择战鹰模式后打出四名 1 战力“战鹰”单位指示物到控制者基地，并以对象标签记录 `法盾`；无效化法术模式和法盾额外选取费用模型暂缓。 |
| `p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base` | `RULE_AUDITED` | `CATALOG` SFD·031/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《点沙成兵》支付回响后重复打出单位指示物效果，共打出两名 2 战力“黄沙士兵”到控制者基地，并锁定同源 token ID 顺序。 |
| `p2-preflight-play-protect-the-emperor-create-sand-soldier` | `RULE_AUDITED` | `CATALOG` SFD·154/221；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《护驾！》不支付黄色可选费用时打出一名 2 战力“黄沙士兵”单位指示物到控制者基地；待命和支付黄色让其变为活跃状态的分支暂缓。 |
| `p2-preflight-play-sand-soldiers-rise-ready-two` | `RULE_AUDITED` | `CATALOG` SFD·198/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方专属法术《沙兵现身》在控制者控制 0 件武装时不创建新单位，并让两名带 `黄沙士兵` 标签的友方单位变为活跃状态；按武装数量创建黄沙士兵的动态分支暂缓。 |
| `p2-preflight-play-stay-away-stun-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-042/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p92-p105 keyword rules 800+ | 已验证官方法术《走开》从手牌打出时眩晕一名单位，然后抽 1 张牌；待命路径暂缓。 |
| `p2-preflight-play-disposal-order-draw-mode` | `RULE_AUDITED` | `CATALOG` UNL-103/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《处置命令》的抽牌模式。 |
| `p2-preflight-play-disposal-order-recycle-opponent-graveyard` | `RULE_AUDITED` | `CATALOG` UNL-103/219；`CORE-260330` p39-p42 rules 355-356；p58-p59 rule 416 | 已验证官方法术《处置命令》选择对手废牌堆中最多三张牌，并让其拥有者回收；多张回收到主牌堆底部时使用可回放随机顺序。 |
| `p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card` | `RULE_AUDITED` | `CATALOG` OGN·156/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；p58-p59 rule 416 | 已验证官方法术《暗中破坏》选择对手手牌中的非单位牌，并让该牌拥有者回收；对手手牌单位牌目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-predictive-offensive-draw-one-recycle-other` | `RULE_AUDITED` | `CATALOG` SFD·122/221；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p58-p59 rule 416 | 已验证官方法术《预判攻势》不支付回响时，选择己方主牌堆顶部两张中的一张加入手牌，并将另一张回收到主牌堆底部；顶部两张以外目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-card-trick-draw-one-recycle-rest` | `RULE_AUDITED` | `CATALOG` OGN·183/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p58-p59 rule 416 | 已验证官方法术《卡牌骗术》选择己方主牌堆顶部三张中的一张加入手牌，并将其余两张回收到主牌堆底部；顶部三张以外目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-dragon-tiger-draw-unit-recycle-rest` | `RULE_AUDITED` | `CATALOG` UNL-032/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p58-p59 rule 416 | 已验证官方法术《龙虎双雄》不支付回响时，选择己方主牌堆顶部三张中带 `CARD_TYPE:UNIT` 对象标签的一张单位牌加入手牌，并将其余两张回收到主牌堆底部；非单位目标和顶部三张以外目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all` | `RULE_AUDITED` | `CATALOG` UNL-032/219；`CORE-260330` p39-p42 rules 355-356；p58-p59 rule 416 | 已验证官方法术《龙虎双雄》不支付回响且不选择单位牌时，不抽牌并回收已查看的主牌堆顶部三张牌。 |
| `p2-preflight-play-reinforcements-no-selection-recycle-top-five` | `RULE_AUDITED` | `CATALOG` OGN·062/298；`CORE-260330` p39-p42 rules 355-356；p58-p59 rule 416 | 已验证官方法术《增援》不选择单位牌时，不抽牌并回收已查看的主牌堆顶部五张牌；从牌堆打出单位并减费 5 的分支暂缓。 |
| `p2-preflight-play-meditation-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·048/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《冥想》的基础抽牌路径。 |
| `p2-preflight-play-salvage-draw-no-equipment` | `RULE_AUDITED` | `CATALOG` OGN·224/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《废物利用》不选择装备目标的合法分支会跳过可选装备摧毁，并继续抽 1 张牌。 |
| `p2-preflight-play-salvage-destroy-equipment-draw` | `RULE_AUDITED` | `CATALOG` OGN·224/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《废物利用》选择一件装备时，在双方让过后先摧毁该装备，再由来源控制者抽 1 张牌；单位目标由直接测试拒绝。 |
| `p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields` | `RULE_AUDITED` | `CATALOG` UNL-015/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《占山为王》在没有已控制战场时只抽基础 1 张牌；按战场控制数量额外抽牌暂缓至战场控制模型落地。 |
| `p2-preflight-play-meditation-exhaust-friendly-extra-draw` | `RULE_AUDITED` | `CATALOG` OGN·048/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《冥想》让活跃友方单位休眠作为额外费用，并额外抽 1 张牌。 |
| `p2-preflight-play-moonsilver-gift-discard-draw` | `RULE_AUDITED` | `CATALOG` UNL-125/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《月神恩赐》弃置另一张友方手牌到废牌堆后抽 2 张牌。 |
| `p2-preflight-play-revive-return-graveyard-unit` | `RULE_AUDITED` | `CATALOG` OGN·170/298；`CORE-260330` p39-p42 rules 355-356；p4-p8 rules 107-129 | 已验证官方法术《亡者复生》选择己方废牌堆单位牌并返回手牌，且拒绝对手废牌堆目标。 |
| `p2-preflight-play-guerrilla-warfare-return-standby-graveyard` | `RULE_AUDITED` | `CATALOG` OGN·264/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方专属法术《游击战》选择己方废牌堆最多两张带 `待命` 标签的牌并返回手牌；非待命目标由直接测试拒绝，免费正面朝下布置待命牌权限暂缓到待命布置模型。 |
| `p2-preflight-play-call-of-the-shadows-give-ephemeral-draw` | `RULE_AUDITED` | `CATALOG` UNL-165/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p92-p105 keyword rules 800+ | 已验证官方法术《暗影的召唤》让未拥有 `瞬息` 的友方单位获得 `瞬息` 对象标签并抽 2 张牌；已有 `瞬息` 目标由直接测试拒绝，开始阶段摧毁瞬息单位暂缓。 |
| `p2-preflight-play-deadly-flourish-enemy-unit-damage` | `RULE_AUDITED` | `CATALOG` UNL-073/219；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《致命华彩》选择一名敌方单位并造成 3 点非致命伤害；友方目标由直接测试拒绝，本回合摧毁后的休眠“金币”装备指示物触发暂缓。 |
| `p2-preflight-play-flowing-time-mirror-battlefield-unit-ephemeral` | `RULE_AUDITED` | `CATALOG` OGN·180/298；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《逝水如镜》选择一名战场单位并给予 `瞬息` 对象标签；基地单位目标由直接测试拒绝，下个回合开始阶段摧毁瞬息对象暂缓。 |
| `p2-preflight-play-flowing-time-mirror-equipment-ephemeral` | `RULE_AUDITED` | `CATALOG` OGN·180/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《逝水如镜》选择一件装备并给予 `瞬息` 对象标签。 |
| `p2-preflight-play-ashes-to-ashes-equipment-ephemeral` | `RULE_AUDITED` | `CATALOG` UNL-070/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《化为灰烬》选择一件场上装备并给予 `瞬息` 对象标签；单位目标由直接测试拒绝，开始阶段摧毁瞬息对象暂缓。 |
| `p2-preflight-play-sigil-burst-destroy-equipment-draw` | `RULE_AUDITED` | `CATALOG` SFD·005/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《印爆术》选择一件场上装备并摧毁，然后让该装备控制者抽 2 张牌；单位目标由直接测试拒绝，装备摧毁不会写入本回合单位摧毁记忆。 |
| `p2-preflight-play-emergency-recall-return-equipment` | `RULE_AUDITED` | `CATALOG` SFD·135/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《紧急召回》选择一件场上装备并让其返回拥有者手牌；单位目标由直接测试拒绝，返回手牌后移除公开对象状态。 |
| `p2-preflight-play-thermogenic-beam-destroy-all-equipment` | `RULE_AUDITED` | `CATALOG` OGN·022/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《热电光束》0 目标摧毁双方场上所有装备；非装备单位不受影响，装备摧毁不会写入本回合单位摧毁记忆。 |
| `p2-preflight-play-broken-blades-rematch-destroy-each-player-equipment` | `RULE_AUDITED` | `CATALOG` OGN·179/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《折戟再战》在当前 2P preflight 中按目标顺序记录双方各自选择一件自己的装备，并在双方让过后分别摧毁；单位目标由直接测试拒绝，逐玩家 prompt 暂缓。 |
| `p2-preflight-play-back-against-wall-double-power-ephemeral` | `RULE_AUDITED` | `CATALOG` OGN·069/298；`CORE-260330` p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《背水一战》选择一名友方单位，按其当前战力翻倍并给予 `瞬息` 对象标签；敌方目标由直接测试拒绝，迅捷时机和下个回合开始阶段摧毁瞬息单位暂缓。 |
| `p2-preflight-play-painful-payoff-damage-create-gold` | `RULE_AUDITED` | `CATALOG` SFD·070/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p62-p63 rule 428；p89 rules 718-719 | 已验证官方法术《痛苦之酬》选择一名战场单位造成 3 点伤害，并打出一枚休眠“金币”装备指示物到来源控制者基地，带 `CARD_TYPE:EQUIPMENT` 标签；基地目标由直接测试拒绝，待命时机和金币资源技能暂缓。 |
| `p2-preflight-play-jungle-ambush-create-gold` | `RULE_AUDITED` | `CATALOG` SFD·004/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p89 rules 718-719 | 已验证官方法术《丛林伏击》从手牌打出时支付 2 点费用、0 目标入栈，双方让过后在来源控制者基地打出一枚休眠“金币”装备指示物，带 `CARD_TYPE:EQUIPMENT` 标签；本回合友方单位活跃进场的全局效果暂缓。 |
| `p2-preflight-play-blood-money-destroy-enemy-small-unit-create-gold` | `RULE_AUDITED` | `CATALOG` SFD·162/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428；p89 rules 718-719 | 已验证官方法术《血钱》摧毁敌方不高于 2 战力的战场单位后，在来源控制者基地打出一枚休眠“金币”装备指示物；3 战力目标由直接测试拒绝。 |
| `p2-preflight-play-blood-money-destroy-friendly-small-unit-create-two-gold` | `RULE_AUDITED` | `CATALOG` SFD·162/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428；p89 rules 718-719 | 已验证官方法术《血钱》摧毁友方不高于 2 战力的战场单位后，在来源控制者基地打出两枚休眠“金币”装备指示物。 |
| `p2-preflight-play-rewind-timeline-discard-hands-draw-four` | `RULE_AUDITED` | `CATALOG` OGN·201/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《反转时间线》让每名玩家弃置自己的所有手牌，然后各抽 4 张牌。 |
| `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune` | `RULE_AUDITED` | `CATALOG` UNL-173/219；`CORE-260330` p14-p15 rules 142-143；p20 rules 164-167；p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《牺牲》摧毁一名友方强力单位作为强制额外费用，然后先抽 2 张牌、再召出一枚休眠符文。 |
| `p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw` | `RULE_AUDITED` | `CATALOG` SFD·163/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《断魂一扼》摧毁一名友方单位后，按其当前战力临时增益另一名友方单位并抽 1 张牌。 |
| `p2-preflight-play-center-your-mind-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-091/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《聚心凝神》未满足等级减费条件时全额支付 5 点费用并抽 2 张牌；等级 6/11 减费路径暂缓。 |
| `p2-preflight-play-might-makes-right-draw-powerful-units` | `RULE_AUDITED` | `CATALOG` SFD·106/221；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《实力至上》按控制者当前强力单位数量抽牌，且强力单位按战力达到 5 或以上统计。 |
| `p2-preflight-play-borrowed-history-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·083/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《借鉴历史》从手牌打出时支付 4 点费用并抽 2 张牌；待命/反应时机路径暂缓。 |
| `p2-preflight-play-assemble-the-ranks-draw` | `RULE_AUDITED` | `CATALOG` SFD·166/221；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《集结部队》从手牌打出时支付 2 点费用、0 目标加入结算链，双方让过后抽 1 张牌；友方单位进场给予增益的全局触发暂缓。 |
| `p2-preflight-play-call-to-action-draw` | `RULE_AUDITED` | `CATALOG` OGN·129/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《迎敌号令》从手牌打出时支付 2 点费用、0 目标加入结算链，双方让过后抽 1 张牌；本回合单位活跃进场的全局效果暂缓。 |
| `p2-preflight-play-secret-art-mercy-grant-boon` | `RULE_AUDITED` | `CATALOG` OGN·053/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《秘奥义！慈悲度魂落》选择未拥有增益的友方单位，双方让过后给予目标 `增益` 标签并让其基础战力永久 +1；本回合所有增益额外 +1 的全局效果暂缓。 |
| `p2-preflight-play-stunning-display-boon-move-base-unit` | `RULE_AUDITED` | `CATALOG` OGN·270/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方专属法术《叹为观止》选择基地中的一名未拥有增益的友方单位，双方让过后给予目标 `增益` 标签、让其基础战力永久 +1，并移动到当前单战场区域。 |
| `p2-preflight-play-void-rush-draw-no-free-play` | `RULE_AUDITED` | `CATALOG` SFD·188/221；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方专属法术《虚空猛冲》在当前 preflight 中不选择免费打出展示牌，双方让过后抽取两张主牌堆顶部展示牌；免费打出分支暂缓。 |
| `p2-preflight-play-open-action-grant-all-boons` | `RULE_AUDITED` | `CATALOG` OGN·153/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《公开行动》在当前没有既有增益可消耗时，双方让过后给予所有友方单位 `增益` 标签，并让未拥有增益的友方单位基础战力永久 +1；消耗增益让单位活跃分支暂缓。 |
| `p2-preflight-play-reflections-swap-draw` | `RULE_AUDITED` | `CATALOG` UNL-083/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p57 rule 413.4；p92-p105 keyword rules 800+ | 已验证官方法术《镜中幻影》选择两名不同公开区域友方单位，且至少一名拥有 `瞬息`，双方让过后互换位置并抽 1 张牌；无 `瞬息` 目标组合由直接测试拒绝，精确多战场位置暂缓。 |
| `p2-preflight-play-thundering-drop-base-power-damage-move` | `RULE_AUDITED` | `CATALOG` OGN·250/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方专属法术《天声震落》选择基地中的一名友方单位，双方让过后按其当前战力对敌方战场单位造成伤害，然后将该友方单位移动到当前单战场区域。 |
| `p2-preflight-play-battle-command-move-friendly-and-opponent-unit` | `RULE_AUDITED` | `CATALOG` UNL-101/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《战斗号令》当前 2P preflight 以目标顺序记录友方单位和对手所选单位，双方让过后将两名基地单位移动到当前粗粒度战场区域；完整对手选择 prompt 和多战场精确位置暂缓。 |
| `p2-preflight-play-bullet-time-power-damage-enemy-battlefield` | `RULE_AUDITED` | `CATALOG` OGN·268/298；`CORE-260330` p20 rules 164-167；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方专属法术《弹幕时间》用 `SPEND_POWER:3` 支付 3 点符能，并按支付数值对敌方战场单位造成伤害；符能不足由直接测试拒绝。 |
| `p2-preflight-play-portalpal-rescue-banish-play-base` | `RULE_AUDITED` | `CATALOG` OGN·102/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《传送门大营救》选择友方单位，双方让过后先放逐该单位，再将其重新打出到所属基地，并清除场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接测试拒绝。 |
| `p2-preflight-play-hunting-rhythm-banish-play-battlefield` | `RULE_AUDITED` | `CATALOG` UNL-184/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方专属法术《狩猎律动》选择友方单位，双方让过后先放逐该单位，再将其重新打出到当前粗粒度战场区域，并清除场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接测试拒绝。 |
| `p2-preflight-play-mobilize-call-rune` | `RULE_AUDITED` | `CATALOG` OGN·134/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356 | 已验证官方法术《动员》支付 2 点费用、0 目标加入结算链、双方让过后从控制者符文牌堆顶召出一枚休眠符文到基地，并记录该符文对象 `isExhausted = true`。 |
| `p2-preflight-play-mobilize-draws-if-rune-call-fails` | `RULE_AUDITED` | `CATALOG` OGN·134/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《动员》在控制者无法召出符文时改为抽 1 张牌。 |
| `p2-preflight-play-catalyst-of-aeons-call-two-runes` | `RULE_AUDITED` | `CATALOG` OGN·138/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356 | 已验证官方法术《万世催化石》支付 4 点费用、0 目标加入结算链、双方让过后从控制者符文牌堆顶召出两枚休眠符文到基地，并记录符文对象 `isExhausted = true`。 |
| `p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short` | `RULE_AUDITED` | `CATALOG` OGN·138/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《万世催化石》只能召出一枚休眠符文时尽可能召出并记录休眠对象状态，随后因无法完整召出两枚符文而抽 1 张牌。 |
| `p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune` | `RULE_AUDITED` | `CATALOG` OGN·047/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《御衡守念》在对手距离胜利得分不超过 3 分时费用减少 2，结算时先抽 1 张牌，再召出 1 枚休眠符文并记录 `isExhausted = true`；未满足减费条件的费用不足由直接测试覆盖。 |
| `p2-preflight-play-portalpalooza-other-chooses-cards` | `RULE_AUDITED` | `CATALOG` OGN·071/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《次元门狂欢》在当前 2P preflight 中用 `PLAY_CARD.mode = OTHER_PLAYERS_CHOOSE_CARDS` 记录对手选择卡牌，双方让过后 P1 与 P2 各抽 1 张牌；多人逐玩家选择暂缓。 |
| `p2-preflight-play-portalpalooza-other-chooses-runes` | `RULE_AUDITED` | `CATALOG` OGN·071/298；`CORE-260330` p20 rules 164-167；p39-p42 rules 355-356 | 已验证官方法术《次元门狂欢》在当前 2P preflight 中用 `PLAY_CARD.mode = OTHER_PLAYERS_CHOOSE_RUNES` 记录对手选择符文，双方让过后 P1 与 P2 各召出 1 枚休眠符文并记录 `isExhausted = true`；多人逐玩家选择暂缓。 |
| `p2-preflight-play-spoils-of-war-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·144/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《以战养战》在未满足减费条件时全额支付 4 点费用并抽 2 张牌。 |
| `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed` | `RULE_AUDITED` | `CATALOG` OGN·229/298、OGN·144/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证本回合先由《复仇》摧毁敌方单位后，《以战养战》费用减少 2 并抽 2 张牌。 |
| `p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit` | `RULE_AUDITED` | `CATALOG` UNL-186/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p62-p63 rule 428 | 已验证官方专属法术《涌泉之恨》支付 4 点费用、指定 4 战力战场单位、加入结算链、双方让过后摧毁目标；不高于 3 战力后的符能再打出分支暂缓。 |
| `p2-preflight-play-abyssal-hunt-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-014/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《渊海狩咒》在未控制正面朝下卡牌时支付 1 点费用、选择战场单位目标、加入结算链、双方让过后造成 2 点伤害并进入废牌堆。 |
| `p2-preflight-play-abyssal-hunt-face-down-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-014/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《渊海狩咒》在控制者控制正面朝下战场牌时改为造成 4 点伤害。 |
| `p2-preflight-play-dancing-grenade-base-unit-damage` | `RULE_AUDITED` | `CATALOG` UNL-020/219；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356 | 已验证官方法术《曼舞手雷》不进入再次打出分支时支付 2 点费用、可指定基地单位、加入结算链、双方让过后造成 2 点伤害；支付 `A` 再次打出并按伤害次数递增的分支暂缓。 |
| `p2-preflight-play-incinerate-damage-stack` | `RULE_AUDITED` | `CATALOG` OGS·003/024；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《焚烧》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后造成 2 点伤害并进入废牌堆。 |
| `p2-preflight-play-lotus-trap-doubles-next-damage` | `RULE_AUDITED` | `CATALOG` UNL-013/219、OGS·003/024；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《莲花陷阱》令目标本回合受到的后续伤害翻倍；同回合《焚烧》的 2 点伤害会翻倍为 4 点。 |
| `p2-preflight-play-counterstorm-prevent-next-damage` | `RULE_AUDITED` | `CATALOG` SFD·194/221、OGS·003/024；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《反击风暴》对目标施加本回合下一次伤害抵挡效果并抽 1 张牌；同回合《焚烧》的 2 点伤害被抵挡为 0，且抵挡效果被消耗。 |
| `p2-preflight-play-noxian-guillotine-next-damage-destroys` | `RULE_AUDITED` | `CATALOG` OGN·254/298、OGS·003/024；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《诺克萨斯断头台》对目标施加本回合下次受到伤害时摧毁的效果；同回合《焚烧》的 2 点非致命伤害会触发摧毁并将单位移入拥有者废牌堆，鼓舞立即摧毁分支暂缓。 |
| `p2-preflight-play-imperial-decree-damage-destroys-unit` | `RULE_AUDITED` | `CATALOG` OGN·221/298、OGS·003/024；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《帝国谕令》支付 5 点费用、给当前场上单位施加本回合受到伤害即摧毁的效果；同回合《焚烧》的 2 点非致命伤害会触发摧毁。后续新进场单位暂缓到全局持续效果模型。 |
| `p2-preflight-play-sprite-summon-create-sprite-base` | `RULE_AUDITED` | `CATALOG` OGN·094/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《精灵召唤》支付 3 点费用后，在当前目的地受限代表路径下打出一名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。 |
| `p2-preflight-play-sprite-burst-create-two-sprites-base` | `RULE_AUDITED` | `CATALOG` UNL-069/219；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《精灵迸发》支付 5 点费用后，在当前目的地受限代表路径下打出两名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。 |
| `p2-preflight-play-mirror-image-copy-ephemeral-base` | `RULE_AUDITED` | `CATALOG` UNL-200/219、UNL·T06；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方专属法术《镜花水月》选择一名单位，并在当前最小对象模型中于控制者基地打出一名活跃“映像”；该映像复制目标当前战力和对象标签，并额外获得 `瞬息`。完整复制牌面、忽略打出效果和瞬息到期摧毁暂缓。 |
| `p2-preflight-play-hextech-ray-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·009/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《海克斯射线》支付 1 点费用、选择战场单位目标、加入结算链、双方让过后造成 3 点伤害并进入废牌堆。 |
| `p2-preflight-hextech-ray-damage-clears-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·009/298；`CORE-260330` p30-p33 rules 317-324, p39-p42 rules 355-356；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证官方法术《海克斯射线》造成的真实伤害会在随后 `END_TURN` 特殊清理中移除，并自动推进到下一回合开始。 |
| `p2-preflight-play-thundering-drop-attacking-damage-stack` | `RULE_AUDITED` | `CATALOG` SFD·017/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《雷霆突降》从手牌打出时支付 3 点费用、指定战场单位目标、加入结算链、双方让过后造成伤害；目标为进攻方单位时伤害从 2 点改为 4 点，非进攻方目标基础 2 点伤害由直接测试覆盖。 |
| `p2-preflight-play-piercing-light-two-target-damage-stack` | `RULE_AUDITED` | `CATALOG` SFD·023/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《透体圣光》不支付回响时支付 2 点费用、选择 1-2 个不同战场单位、加入结算链、双方让过后对每名目标各造成 2 点伤害；有色回响费用路径暂缓。 |
| `p2-preflight-play-thundering-sky-cost-reduced-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·014/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《霹天雳地》按控制单位最高战力降低法力费用，支付降低后的费用、加入结算链、双方让过后对战场单位造成 5 点伤害；未满足降低后费用由直接拒绝测试覆盖。 |
| `p2-preflight-play-comet-strike-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·085/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《彗星坠击》支付 5 点费用、选择战场单位目标、加入结算链、双方让过后造成 6 点伤害并进入废牌堆。 |
| `p2-preflight-play-final-spark-damage-stack` | `RULE_AUDITED` | `CATALOG` OGS·022/024；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《终极闪光》支付 8 点费用、选择一名单位、加入结算链、双方让过后造成 8 点伤害并进入废牌堆。 |
| `p2-preflight-play-super-mega-death-rocket-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·252/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方专属法术《超究极死神飞弹！》支付 4 点费用、选择一名单位、加入结算链、双方让过后造成 5 点伤害；征服后从废牌堆返回手牌的触发能力暂缓。 |
| `p2-preflight-play-center-stage-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-061/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《台前作秀》不支付回响时的 0 目标基础路径：支付 2 点费用、加入结算链、双方让过后抽 1 张牌并进入废牌堆。 |
| `p2-preflight-play-center-stage-echo-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-061/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p92-p105 keyword rules 800+ | 已验证官方法术《台前作秀》支付回响 2 额外费用时重复基础抽牌效果一次，共抽 2 张牌。 |
| `p2-preflight-play-prophets-omen-draw-stack` | `RULE_AUDITED` | `CATALOG` SFD·087/221；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《先知之兆》支付 2 点费用、0 目标入栈、双方让过后抽 3 张牌并进入废牌堆。 |
| `p2-preflight-play-evolution-day-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·114/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《进化日》支付 6 点费用、0 目标入栈、双方让过后抽 4 张牌并进入废牌堆。 |
| `p2-preflight-play-vengeance-destroy-unit-stack` | `RULE_AUDITED` | `CATALOG` OGN·229/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p62-p63 rule 428 | 已验证官方法术《复仇》支付 4 点费用、指定一名单位、加入结算链、双方让过后将目标从场上移入拥有者废牌堆。 |
| `p2-preflight-play-detonation-destroy-battlefield-unit-stack` | `RULE_AUDITED` | `CATALOG` OGS·012/024；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p62-p63 rule 428 | 已验证官方法术《爆能术》支付 6 点费用、指定战场上的一名单位、加入结算链、双方让过后将目标从战场移入拥有者废牌堆。 |
| `p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit` | `RULE_AUDITED` | `CATALOG` UNL-159/219；`CORE-260330` p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《狩魂》支付 2 点费用、指定战场上不高于 3 战力的一名单位、加入结算链、双方让过后将目标从战场移入拥有者废牌堆；4 战力目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-darkin-blade-destroy-target-controller-draw` | `RULE_AUDITED` | `CATALOG` OGN·213/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p62-p63 rule 428 | 已验证官方法术《暗刃》从手牌打出时支付 2 点费用、指定战场中的一名单位、加入结算链、双方让过后摧毁目标，并让该单位控制者抽 2 张牌；待命路径暂缓。 |
| `p2-preflight-play-housecleaning-destroy-each-player-unit` | `RULE_AUDITED` | `CATALOG` OGN·209/298；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《清理门户》支付 2 点费用、按顺序记录 P1 自选友方单位和 P2 自选敌方单位、加入结算链、双方让过后各摧毁一名自己的单位；反向顺序和两个友方目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack` | `RULE_AUDITED` | `CATALOG` SFD·164/221；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p62-p63 rule 428 | 已验证官方法术《流沙陷坑》从手牌打出时支付 5 点费用、指定战场上的一名单位、加入结算链、双方让过后将目标从战场移入拥有者废牌堆；非手牌位置打出减费路径暂缓。 |
| `p2-preflight-play-ruination-destroy-all-units` | `RULE_AUDITED` | `CATALOG` UNL-180/219；`CORE-260330` p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《破败之咒》从手牌打出时支付 9 点费用、0 目标加入结算链、双方让过后摧毁所有当前场上单位；当前最小模型覆盖基地和战场区域中的单位。 |
| `p2-preflight-play-undertow-return-all-units` | `RULE_AUDITED` | `CATALOG` SFD·147/221；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《坠渊之流》支付 8 点费用、0 目标加入结算链、双方让过后让所有当前场上单位和装备返回所属者手牌，并移除公开对象状态。 |
| `p2-preflight-play-reprimand-return-battlefield-unit` | `RULE_AUDITED` | `CATALOG` OGN·172/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《责退》从手牌打出时支付 2 点费用、指定战场上的一名单位、加入结算链、双方让过后将目标返回所属者手牌，并移除场上对象状态。 |
| `p2-preflight-play-gust-return-small-battlefield-unit` | `RULE_AUDITED` | `CATALOG` OGN·169/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《罡风》从手牌打出时支付 1 点费用、指定战场上不高于 3 战力的一名单位、加入结算链、双方让过后将目标返回所属者手牌；4 战力目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-reconsider-return-friendly-call-rune` | `RULE_AUDITED` | `CATALOG` OGN·104/298；`CORE-260330` p4-p8 rules 107-129；p20 rules 164-167；p39-p42 rules 355-356 | 已验证官方法术《择日再战》支付 1 点费用、指定一名友方单位、加入结算链、双方让过后让目标返回所属者手牌，再让其拥有者召出 1 枚休眠符文并记录 `isExhausted = true`；敌方目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-happenstance-return-friendly-and-enemy` | `RULE_AUDITED` | `CATALOG` UNL-128/219；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《造化弄人》支付 3 点费用、按顺序指定一名友方单位和一名敌方单位、加入结算链、双方让过后让两个目标分别返回所属者手牌；目标顺序反转由直接测试覆盖拒绝。 |
| `p2-preflight-play-hurricane-sweep-each-player-return-unit` | `RULE_AUDITED` | `CATALOG` OGN·187/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356 | 已验证官方法术《飓风席卷》支付 4 点费用、在当前 2P preflight 中用 0-2 个单位目标按座位选择顺序记录玩家选择、加入结算链、双方让过后让所选单位返回所属者手牌；重复目标拒绝由直接测试覆盖，逐玩家 prompt/多人选择暂缓。 |
| `p2-preflight-play-custodian-judgment-unit-to-deck-top` | `RULE_AUDITED` | `CATALOG` UNL-204/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356 | 已验证官方法术《持卫的裁决》支付 2 点费用、指定敌方战场单位、用 `PLAY_CARD.mode = OWNER_MAIN_DECK_TOP` 记录拥有者选择顶部、加入结算链、双方让过后将目标放到拥有者主牌堆顶部并移除公开对象状态。 |
| `p2-preflight-play-custodian-judgment-unit-to-deck-bottom` | `RULE_AUDITED` | `CATALOG` UNL-204/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356 | 已验证官方法术《持卫的裁决》支付 2 点费用、指定敌方战场单位、用 `PLAY_CARD.mode = OWNER_MAIN_DECK_BOTTOM` 记录拥有者选择底部、加入结算链、双方让过后将目标放到拥有者主牌堆底部并移除公开对象状态；缺失模式、友方单位和基地单位目标由直接测试覆盖。 |
| `p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base` | `RULE_AUDITED` | `CATALOG` OGN·168/298；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《战或逃》支付 2 点费用、指定战场上的一名单位、加入结算链、双方让过后将目标移动到所属者基地，并保留对象状态。 |
| `p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready` | `RULE_AUDITED` | `CATALOG` OGN·173/298；`CORE-260330` p4-p8 rules 107-129；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《驭风而行》支付 2 点费用、指定友方战场单位、加入结算链、双方让过后在当前目的地受限模型下将目标移动到所属者基地并变为活跃状态；完整目的地选择暂缓到多位置移动模型。 |
| `p2-preflight-play-charm-move-enemy-battlefield-unit-to-base` | `RULE_AUDITED` | `CATALOG` OGN·043/298；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《魅惑妖术》支付 1 点费用、指定敌方战场单位、加入结算链、双方让过后将目标移动到所属者基地；完整目的地选择暂缓到多位置移动模型。 |
| `p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base` | `RULE_AUDITED` | `CATALOG` UNL-038/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《升龙踢》未启用等级 6 时支付 2 点费用、指定敌方战场单位、加入结算链、双方让过后将目标移动到所属者基地；完整目的地选择和等级 6 眩晕暂缓。 |
| `p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base` | `RULE_AUDITED` | `CATALOG` UNL-124/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《隔绝》支付 2 点费用、指定敌方战场单位、加入结算链、双方让过后将目标移动到所属者基地；当前 fixture 锁定移动后无落单敌方单位残留，因此不抽牌，落单抽牌分支暂缓到多战场位置/孤立判定模型。 |
| `p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base` | `RULE_AUDITED` | `CATALOG` OGS·011/024；`CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已验证官方法术《闪现》支付 2 点费用、指定最多两名友方战场单位、加入结算链、双方让过后将目标移动到基地，并保留对象状态；敌方单位和友方基地单位目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base` | `RULE_AUDITED` | `CATALOG` SFD·043/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《禁军之墙》从手牌打出时支付 2 点费用、按当前己方战场单位数动态选择任意数量友方战场单位、加入结算链、双方让过后将所选目标移动到基地，并保留对象状态；敌方单位、友方基地单位和重复目标拒绝由直接测试覆盖，待命/迅捷窗口细节暂缓。 |
| `p2-preflight-play-playful-tentacles-move-total-power-eight` | `RULE_AUDITED` | `CATALOG` UNL-054/219；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已验证官方法术《顽皮触手》从手牌打出时支付 4 点费用、选择总战力不高于 8 的敌方战场单位、加入结算链、双方让过后将所选目标移动到所属者基地，并保留对象状态；总战力超过 8 的组合拒绝由直接测试覆盖，同一位置目的地和多控制者约束暂缓。 |
| `p2-preflight-play-bait-move-enemy-unit-to-another-location` | `RULE_AUDITED` | `CATALOG` SFD·129/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《诱饵》不支付回响时支付 2 点费用、按顺序指定一名敌方单位和另一名敌方单位、加入结算链、双方让过后将第一目标移动到第二目标所在公开区域位置，并保留对象状态；友方目标和重复目标拒绝由直接测试覆盖，回响与多战场精确位置暂缓。 |
| `p2-preflight-play-the-curtain-rises-echo-ready-unit` | `RULE_AUDITED` | `CATALOG` UNL-009/219；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《大幕渐起》支付基础 2 点和回响 2 点费用、指定一名单位、加入结算链、双方让过后重复“变为活跃状态”效果一次。 |
| `p2-preflight-play-beatdown-ready-unit` | `RULE_AUDITED` | `CATALOG` OGN·146/298；`CORE-260330` p39-p42 rules 355-356 | 已验证官方法术《痛殴》不支付消耗增益额外费用时支付 2 点费用、指定一名单位、加入结算链、双方让过后令目标变为活跃状态；消耗增益无视费用路径暂缓。 |
| `p2-preflight-play-hunt-ready-all-friendly-units` | `RULE_AUDITED` | `CATALOG` SFD·204/221；`CORE-260330` p39-p42 rules 355-356 | 已验证官方专属法术《狩猎》支付 1 点费用、0 目标加入结算链、双方让过后令控制者所有场上单位变为活跃状态，且不影响对手单位。 |
| `p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield` | `RULE_AUDITED` | `CATALOG` OGN·123/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 323-324；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《过载能量》支付 7 点费用、0 目标加入结算链、双方让过后先令控制者所有场上单位变为休眠状态，再对所有战场上的单位各造成 12 点伤害，并在结算后执行致命伤害清理。 |
| `p2-preflight-play-practical-experience-power-plus-1` | `RULE_AUDITED` | `CATALOG` UNL-031/219；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《实战经验》未启用等级 6 升级时支付 1 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 +1；等级 6 改为 +3 的路径暂缓。 |
| `p2-preflight-play-dueling-stance-friendly-power-plus-1` | `RULE_AUDITED` | `CATALOG` OGN·046/298；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《决斗架势》支付 1 点费用、指定一名友方单位、加入结算链、双方让过后令目标本回合内战力 +1；“该处唯一控制单位”额外 +1 分支暂缓到更细位置模型。 |
| `p2-preflight-play-animal-friends-power-per-controlled-tag` | `RULE_AUDITED` | `CATALOG` UNL-046/219；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《动物之友》支付 1 点费用、指定一名单位、按控制者场上/基地单位中的“鸟类、猫科、犬形、魄罗”不同标签种类数动态计算本回合内战力修正；样例中三种标签令目标 +3。 |
| `p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit` | `RULE_AUDITED` | `CATALOG` SFD·001/221；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《矢志不退》支付 2 点费用、指定一名友方战场单位、按敌方战场单位数量动态计算本回合内战力修正；当前单战场区域模型中两个敌方战场单位令目标 +4，同一战场位置约束暂缓。 |
| `p2-preflight-play-well-trained-power-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·058/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p31-p33 rules 318-324 | 已验证官方法术《训练有素》支付 2 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 +2，然后抽 1 张牌。 |
| `p2-preflight-well-trained-power-expires-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·058/298；`CORE-260330` p31-p33 rules 318-324；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证《训练有素》式本回合内战力修正会在 `END_TURN` 特殊清理中移除，并自动推进到下一回合开始。 |
| `p2-preflight-play-savage-strength-echo-power-stack` | `RULE_AUDITED` | `CATALOG` SFD·034/221；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+；p31-p33 rules 318-324 | 已验证官方法术《蛮荒之力》支付回响 2 后重复本回合内战力 +2 修正一次，目标共获得 +4。 |
| `p2-preflight-play-freeze-echo-power-minus-2` | `RULE_AUDITED` | `CATALOG` SFD·066/221；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+；p31-p33 rules 318-324 | 已验证官方法术《封冻》支付回响 2 后重复本回合内战力 -2 修正一次，目标共获得 -4。 |
| `p2-preflight-play-distance-break-dance-split-power-modifiers` | `RULE_AUDITED` | `CATALOG` SFD·196/221；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《距破之舞》支付 1 点费用、指定两名不同单位、加入结算链、双方让过后令第一名目标本回合内战力 +2、第二名目标本回合内战力 -2。 |
| `p2-preflight-play-switcheroo-swap-battlefield-unit-powers` | `RULE_AUDITED` | `CATALOG` SFD·145/221；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《换换乐》支付 2 点费用、指定两名不同战场单位、加入结算链、双方让过后用本回合内战力修正互换两名目标当前战力；基地单位和重复目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-cleave-overwhelm-attacking-power` | `RULE_AUDITED` | `CATALOG` OGN·004/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《顺劈》授予本回合 `OVERWHELM_3` 标记，并在目标为进攻方时给予本回合内战力 +3。 |
| `p2-preflight-play-blood-rush-echo-overwhelm-attacking-power` | `RULE_AUDITED` | `CATALOG` SFD·003/221；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《血性冲刺》支付回响 1 后重复授予本回合 `OVERWHELM_2`，并对进攻方目标重复给予本回合内战力 +2。 |
| `p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power` | `RULE_AUDITED` | `CATALOG` UNL-017/219；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《怒吼清算》弃置一张手牌作为回响额外费用后重复授予本回合 `OVERWHELM_4`，并对进攻方目标重复给予本回合内战力 +4。 |
| `p2-preflight-play-power-punch-overwhelm-roam-attacking-power` | `RULE_AUDITED` | `CATALOG` UNL-010/219；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《强能冲拳》授予本回合 `OVERWHELM_2` 与 `ROAM` 标记，并在目标为进攻方时给予本回合内战力 +2；非进攻方只获得关键词标记。 |
| `p2-preflight-play-parry-steadfast-barrier-defending-power` | `RULE_AUDITED` | `CATALOG` OGN·057/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《格挡》授予本回合 `STEADFAST_3` 与 `BARRIER` 标记，并在目标为防守方时给予本回合内战力 +3；非防守方只获得关键词标记。 |
| `p2-preflight-play-shoot-first-power-plus-5-stack` | `RULE_AUDITED` | `CATALOG` SFD·097/221；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《先打再问》支付 1 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 +5。 |
| `p2-preflight-play-tremendous-strength-power-plus-7` | `RULE_AUDITED` | `CATALOG` OGN·154/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《洪荒巨力》支付 4 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 +7。 |
| `p2-preflight-play-eclipse-power-minus-4` | `RULE_AUDITED` | `CATALOG` UNL-063/219；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《月蚀》支付 3 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 -4；只选择单位目标表示洞察时不回收顶部牌。 |
| `p2-preflight-play-eclipse-power-minus-4-insight-recycle` | `RULE_AUDITED` | `CATALOG` UNL-063/219；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356；p58-p59 rule 416 | 已验证官方法术《月蚀》先令一名单位本回合内战力 -4，再通过洞察选择己方主牌堆顶部一张牌并回收到主牌堆底部；顶部一张以外目标由直接测试覆盖拒绝。 |
| `p2-preflight-play-moonfall-power-minus-10` | `RULE_AUDITED` | `CATALOG` UNL-066/219；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《月光之殇》支付 7 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 -10。 |
| `p2-preflight-play-glory-call-power-plus-3` | `RULE_AUDITED` | `CATALOG` OGN·207/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《荣耀召唤》不支付消耗增益额外费用时支付 3 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 +3；消耗增益以无视费用的路径暂缓。 |
| `p2-preflight-play-last-stand-friendly-power-plus-3` | `RULE_AUDITED` | `CATALOG` UNL-095/219；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《视死如归》支付 2 点费用、指定一名友方单位、加入结算链、双方让过后令目标本回合内战力 +3；本回合赢得战斗时获得 2 经验的触发路径暂缓。 |
| `p2-preflight-play-decisive-strike-all-friendly-power-plus-2` | `RULE_AUDITED` | `CATALOG` OGS·024/024；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《致命打击》支付 5 点费用、0 目标加入结算链、双方让过后令控制者所有场上友方单位本回合内战力 +2，且不影响对手单位。 |
| `p2-preflight-play-grand-strategy-all-friendly-power-plus-5` | `RULE_AUDITED` | `CATALOG` OGN·233/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《宏伟战略》支付 6 点费用、0 目标加入结算链、双方让过后令控制者所有场上友方单位本回合内战力 +5，且不影响对手单位。 |
| `p2-preflight-play-back-to-back-two-friendly-power-plus-2` | `RULE_AUDITED` | `CATALOG` OGN·206/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《背靠背》支付 3 点费用、指定两名友方单位、加入结算链、双方让过后令两个目标本回合内战力 +2；敌方单位目标由直接测试拒绝。 |
| `p2-preflight-play-power-bind-echo-two-friendly-power-plus-1` | `RULE_AUDITED` | `CATALOG` SFD·151/221；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+；p31-p33 rules 318-324 | 已验证官方法术《力量之缚》支付基础 2 点和回响 2 点费用、指定两名友方单位、加入结算链、双方让过后重复本回合内战力 +1 效果一次，两个目标各获得 +2。 |
| `p2-preflight-play-danger-temperature-mechanical-power-plus-1` | `RULE_AUDITED` | `CATALOG` SFD·182/221；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《危险温度》未支付混合资源回响时，只让控制者自己的“机械”属性单位本回合内战力 +1；己方非机械和对手机械单位不受影响。 |
| `p2-preflight-play-siphon-energy-battlefield-power-split` | `RULE_AUDITED` | `CATALOG` OGN·266/298；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《虹吸能量》在当前单战场区域模型下让友方战场单位本回合内战力 +1、敌方战场单位本回合内战力 -1 且不得低于 1；双方基地单位不受影响。 |
| `p2-preflight-play-moonrise-enemy-battlefield-power-minus-2` | `RULE_AUDITED` | `CATALOG` UNL-198/219；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已验证官方法术《月之降临》在当前单战场区域模型下跳过可选敌方单位移动，并让敌方战场单位本回合内战力 -2；双方基地单位和友方战场单位不受影响。 |
| `p2-preflight-play-smoke-bomb-power-floor-stack` | `RULE_AUDITED` | `CATALOG` OGN·093/298；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证官方法术《烟幕弹》支付 2 点费用、指定一名单位、加入结算链、双方让过后令目标本回合内战力 -4 且不得低于 1；被下限截断时记录实际应用的战力修正。 |
| `p2-preflight-smoke-bomb-power-floor-expires-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·093/298；`CORE-260330` p31-p33 rules 318-324；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证《烟幕弹》式被“不得低于 1”截断的负战力修正会在 `END_TURN` 特殊清理中移除，并恢复单位原战力。 |
| `p2-preflight-play-extortion-power-floor-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·095/298；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p31-p33 rules 318-324 | 已验证官方法术《“敲”诈》支付 1 点费用、指定一名单位、加入结算链、双方让过后尝试令目标本回合内战力 -1 且不得低于 1；目标已是 1 战力时实际应用 0，并继续抽 1 张牌。 |
| `p2-preflight-play-stellar-convergence-two-target-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·105/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《星芒凝汇》支付 6 点费用、选择最多两名单位、加入结算链、双方让过后对每名目标各造成 6 点伤害并进入废牌堆。 |
| `p2-preflight-play-rocket-barrage-base-unit-mode-stack` | `RULE_AUDITED` | `CATALOG` SFD·077/221；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《火箭轰击》选择基地单位伤害模式时，支付 4 点费用、指定基地中的一名单位、加入结算链、双方让过后造成 4 点伤害并进入废牌堆。 |
| `p2-preflight-play-rocket-barrage-destroy-equipment-mode` | `RULE_AUDITED` | `CATALOG` SFD·077/221；`CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《火箭轰击》选择摧毁装备模式时，支付 4 点费用、指定一件场上装备、加入结算链、双方让过后将该装备移入拥有者废牌堆；单位目标由直接测试拒绝。 |
| `p2-preflight-play-bellows-breath-up-to-three-units-damage` | `RULE_AUDITED` | `CATALOG` SFD·080/221；`CORE-260330` p14-p15 rules 142-143；p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《风箱炎息》不支付有色回响时支付 1 点费用、选择当前单战场区域模型中的 1-3 名不同单位、加入结算链、双方让过后对每名目标各造成 1 点伤害；同一位置精确约束和有色回响路径暂缓。 |
| `p2-preflight-play-firestorm-damage-enemy-battlefield-units` | `RULE_AUDITED` | `CATALOG` OGS·002/024；`CORE-260330` p14-p15 rules 142-143；p31-p33 rules 323-324；p39-p42 rules 355-356 | 已验证官方法术《烈火风暴》支付 6 点费用、0 目标加入结算链、双方让过后对当前单战场区域模型中的所有敌方战场单位各造成 3 点伤害；友方战场单位和敌方基地单位不受影响。 |
| `p2-preflight-play-crescent-strike-target-plus-splash` | `RULE_AUDITED` | `CATALOG` UNL-072/219；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《新月打击》支付 3 点费用、目标限制为敌方战场单位、加入结算链、双方让过后对主目标造成 4 点伤害，并对当前单战场区域模型中的其他敌方战场单位各造成 1 点伤害；友方战场单位和敌方基地单位不受影响，同一位置约束暂按当前单战场区域模型处理。 |
| `p2-preflight-play-perfect-finale-draw-mode` | `RULE_AUDITED` | `CATALOG` UNL-182/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《完美谢幕》未支付回响时选择抽牌模式，支付 4 点费用、0 目标加入结算链、双方让过后抽 1 张牌。 |
| `p2-preflight-play-perfect-finale-battlefield-damage-mode` | `RULE_AUDITED` | `CATALOG` UNL-182/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证《完美谢幕》未支付回响时选择战场单位伤害模式，目标必须是战场单位，结算后造成 2 点伤害。 |
| `p2-preflight-play-perfect-finale-base-damage-mode` | `RULE_AUDITED` | `CATALOG` UNL-182/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证《完美谢幕》未支付回响时选择基地单位伤害模式，目标必须是基地单位，结算后造成 3 点伤害。 |
| `p2-preflight-play-perfect-finale-battlefield-power-mode` | `RULE_AUDITED` | `CATALOG` UNL-182/219；`CORE-260330` p39-p42 rules 355-356；p31-p33 rules 318-324 | 已验证《完美谢幕》未支付回响时选择战场单位战力 -4 模式，目标必须是战场单位，结算后获得本回合内战力 -4 修正。 |
| `p2-preflight-play-highlander-bloodline-recall-if-destroyed` | `RULE_AUDITED` | `CATALOG` OGS·020/024、OGN·229/298；`CORE-260330` p39-p42 rules 355-356；p62-p63 rule 428；p31-p33 rules 318-324 | 已验证官方法术《高原血统》为友方单位施加本回合内摧毁替代效果；随后《复仇》摧毁该单位时改为以休眠状态召回拥有者基地、清除伤害、不进入废牌堆且不写入本回合摧毁记忆。 |
| `p2-preflight-play-tactical-retreat-recall-if-destroyed` | `RULE_AUDITED` | `CATALOG` UNL-175/219、OGN·229/298；`CORE-260330` p39-p42 rules 355-356；p62-p63 rule 428；p31-p33 rules 318-324 | 已验证官方法术《战术撤退》为友方单位施加本回合内下次摧毁替代效果；随后《复仇》摧毁该单位时改为移除伤害、变为休眠并召回拥有者基地，且不进入废牌堆或本回合摧毁记忆。 |
| `p2-preflight-play-highway-robbery-enemy-unit-damage` | `RULE_AUDITED` | `CATALOG` OGN·033/298；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356 | 已验证官方法术《巧取豪夺》在目标控制者不选择让你抽两张牌时支付 2 点费用、目标限制为敌方单位、加入结算链、双方让过后造成 6 点伤害；友方单位和离场敌方牌拒绝由直接测试覆盖。 |
| `p2-preflight-play-highway-robbery-target-controller-draw-choice` | `RULE_AUDITED` | `CATALOG` OGN·033/298；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p57 rule 413.4 | 已验证官方法术《巧取豪夺》在目标控制者选择让来源控制者抽两张牌时，支付 2 点费用并保留敌方目标不受伤害，结算后由来源控制者抽 2 张牌。 |
| `p2-preflight-play-kings-edict-destroy-enemy-unit` | `RULE_AUDITED` | `CATALOG` OGN·237/298；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《国王诏令》在当前 2P preflight 中支付 6 点费用、用一个敌方单位目标记录另一名玩家选择的非控制者单位、加入结算链、双方让过后摧毁该单位；友方单位目标拒绝由直接测试覆盖，多人逐玩家选择暂缓。 |
| `p2-preflight-play-spirit-fire-destroy-total-power-four` | `RULE_AUDITED` | `CATALOG` OGN·256/298；`CORE-260330` p14-p15 rules 142-143；p33-p35 rules 327-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《妖异狐火》支付 3 点费用、选择战场上总战力不高于 4 的任意数量单位、加入结算链、双方让过后摧毁所选单位；总战力超过 4 的选择由直接拒绝测试覆盖，一处战场精确位置暂缓。 |
| `p2-preflight-play-last-breath-ready-damage-enemy-battlefield` | `RULE_AUDITED` | `CATALOG` OGN·260/298；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p62-p63 rule 428 | 已验证官方法术《狂风绝息斩》支付 3 点费用、按顺序选择友方单位和敌方战场单位、加入结算链、双方让过后先让友方目标变为活跃状态，再按该友方单位当前战力对敌方战场单位造成伤害；目标顺序错误和敌方基地单位拒绝由直接测试覆盖。 |
| `p2-preflight-play-convergent-mutation-match-friendly-power` | `RULE_AUDITED` | `CATALOG` OGN·108/298；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《聚合变异》支付 2 点费用、按顺序选择两名不同友方单位、加入结算链、双方让过后若第一目标战力低于第二目标，则第一目标获得本回合内动态战力修正并变为第二目标当前战力；敌方目标和重复目标拒绝由直接测试覆盖。 |
| `p2-preflight-play-void-seeker-damage-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·024/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《虚空索敌》支付 3 点费用、选择战场单位目标、加入结算链、双方让过后造成 4 点伤害并让控制者抽 1 张牌。 |
| `p2-preflight-void-seeker-draw-burnout-stack` | `RULE_AUDITED` | `CATALOG` OGN·024/298；`CORE-260330` p57 rule 413.4；p67 rule 431.2；p39-p42 rules 355-356 | 已验证官方法术《虚空索敌》的结算抽牌会在控制者主牌堆为空时触发燃尽，对手得 1 分，控制者回收废牌堆并抽到回收牌。 |
| `p2-preflight-play-rune-prison-stun-stack` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《符文禁锢》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后施加 `STUNNED` 本回合内效果并进入废牌堆。 |
| `p2-preflight-play-rune-prison-base-unit-stun-stack` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《符文禁锢》的“眩晕一名单位”目标范围允许指定基地单位。 |
| `p2-preflight-rune-prison-stun-expires-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p30-p33 rules 317-324, p39-p42 rules 355-356；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证官方法术《符文禁锢》施加的 `STUNNED` 会在随后 `END_TURN` 特殊清理中失效，并自动推进到下一回合开始。 |
| `p2-preflight-play-kerplunk-stun-attacking-unit` | `RULE_AUDITED` | `CATALOG` SFD·040/221；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《扑咚！》不支付回响时支付 2 点费用、目标限制为进攻方单位、加入结算链、双方让过后施加 `STUNNED`；非进攻单位拒绝由直接测试覆盖。 |
| `p2-preflight-play-kerplunk-echo-stun-attacking-unit` | `RULE_AUDITED` | `CATALOG` SFD·040/221；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《扑咚！》支付基础 2 点和回响 2 点费用、目标限制为进攻方单位、加入结算链、双方让过后重复对目标施加 `STUNNED`。 |
| `p2-preflight-play-existential-dread-echo-stun-then-return` | `RULE_AUDITED` | `CATALOG` UNL-134/219；`CORE-260330` p39-p42 rules 355-356；p92-p105 keyword rules 800+ | 已验证官方法术《存在焦虑》支付基础 1 点和回响 2 点费用、目标限制为正在进攻的敌方单位、加入结算链、双方让过后重复效果先施加 `STUNNED`，再因目标已眩晕改为返回所属者手牌；友方进攻单位拒绝由直接测试覆盖。 |
| `p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit` | `RULE_AUDITED` | `CATALOG` OGN·262/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p31-p33 rules 318-324 | 已验证官方法术《天顶之刃》不选择可选移动时支付 3 点费用、目标限制为敌方战场单位、加入结算链、双方让过后施加 `STUNNED`；友方单位和敌方基地单位拒绝由直接测试覆盖，可选移动分支暂缓。 |
| `p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units` | `RULE_AUDITED` | `CATALOG` OGN·220/298；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《强手裂颅》支付 2 点费用、按顺序指定一名友方战场单位和一名敌方战场单位、加入结算链、双方让过后对两名目标施加 `STUNNED`；目标顺序、友方基地单位和敌方基地单位拒绝由直接测试覆盖。 |
| `p2-preflight-play-heroic-charge-power-plus-stun` | `RULE_AUDITED` | `CATALOG` UNL-155/219；`CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356 | 已验证官方法术《英勇冲锋》支付 3 点费用、按顺序指定一名友方战场单位和一名敌方战场单位、加入结算链、双方让过后让友方目标本回合内战力 +1 并对敌方目标施加 `STUNNED`；目标顺序和基地单位拒绝由直接测试覆盖，同一位置约束暂按当前单战场区域模型处理。 |

## 6.1 当前三条 Fixture 冲突检查结论

- `PASS`：`CORE-260330` 将“让过”分别放在 FEPR 和法术对决语境中，且优先行动权/焦点决定谁能行动。`JFAQ-251023` questions 3.1-3.3 澄清焦点、活跃玩家和初始结算链；`BREAK-JFAQ-260416` p2-p5 的普通开环/法术对决个案未发现推翻通用 `PASS` 语义的条目。当前旧 Java 把初始 `PASS` 记成 `TURN_ENDED`，这是 legacy mismatch candidate。
- `END_TURN`：`CORE-260330` rules 316.6-317.3 支持主阶段结束后进入回合结束流程，并在下一回合开始执行召出、抽牌等步骤。`JFAQ-251023` questions 5.1-5.2 补充清理和特殊清理；`BREAK-JFAQ-260416` p11 仅发现额外回合相关个案，未改变裸 `END_TURN` 通用流程。
- `duplicate-pass`：这是网络重试和服务端权威幂等 fixture，不由游戏规则 PDF 裁决。它的验收依据是 command log 唯一键、同一 `clientIntentId` 不重复推进 tick、不重复写事件。

## 7. 索引维护规则

- 新增规则能力前，先在本索引中找到证据；找不到就先补索引。
- 不能把整段 PDF/FAQ 原文复制进仓库，只记录文件、页码、规则号/问题号和非原文摘要。
- 如果 FAQ 与旧 Java 行为冲突，新增 `legacyMismatch` 记录，并以 PDF/FAQ expected 为准。
- 每次新增 PDF、FAQ 或官网快照，先更新本索引，再更新 fixture 和状态矩阵。
