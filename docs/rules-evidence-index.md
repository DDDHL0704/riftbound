# 规则证据索引

更新时间：2026-04-28

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
| `p2-preflight-play-punishment-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-007/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《惩戒》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后造成 3 点伤害并进入废牌堆。 |
| `p2-preflight-play-punishment-base-unit-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-007/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《惩戒》的“对一名单位”目标范围允许指定基地单位，而非仅限战场单位。 |
| `p2-preflight-play-abyssal-hunt-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-014/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《渊海狩咒》在未控制正面朝下卡牌时支付 1 点费用、选择战场单位目标、加入结算链、双方让过后造成 2 点伤害并进入废牌堆。 |
| `p2-preflight-play-abyssal-hunt-face-down-damage-stack` | `RULE_AUDITED` | `CATALOG` UNL-014/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《渊海狩咒》在控制者控制正面朝下战场牌时改为造成 4 点伤害。 |
| `p2-preflight-play-incinerate-damage-stack` | `RULE_AUDITED` | `CATALOG` OGS·003/024；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《焚烧》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后造成 2 点伤害并进入废牌堆。 |
| `p2-preflight-play-hextech-ray-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·009/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《海克斯射线》支付 1 点费用、选择战场单位目标、加入结算链、双方让过后造成 3 点伤害并进入废牌堆。 |
| `p2-preflight-hextech-ray-damage-clears-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·009/298；`CORE-260330` p30-p33 rules 317-324, p39-p42 rules 355-356；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证官方法术《海克斯射线》造成的真实伤害会在随后 `END_TURN` 特殊清理中移除，并自动推进到下一回合开始。 |
| `p2-preflight-play-comet-strike-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·085/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《彗星坠击》支付 5 点费用、选择战场单位目标、加入结算链、双方让过后造成 6 点伤害并进入废牌堆。 |
| `p2-preflight-play-final-spark-damage-stack` | `RULE_AUDITED` | `CATALOG` OGS·022/024；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《终极闪光》支付 8 点费用、选择一名单位、加入结算链、双方让过后造成 8 点伤害并进入废牌堆。 |
| `p2-preflight-play-center-stage-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-061/219；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《台前作秀》不支付回响时的 0 目标基础路径：支付 2 点费用、加入结算链、双方让过后抽 1 张牌并进入废牌堆。 |
| `p2-preflight-play-center-stage-echo-draw-stack` | `RULE_AUDITED` | `CATALOG` UNL-061/219；`CORE-260330` p39-p42 rules 355-356；p57 rule 413.4；p92-p105 keyword rules 800+ | 已验证官方法术《台前作秀》支付回响 2 额外费用时重复基础抽牌效果一次，共抽 2 张牌。 |
| `p2-preflight-play-prophets-omen-draw-stack` | `RULE_AUDITED` | `CATALOG` SFD·087/221；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《先知之兆》支付 2 点费用、0 目标入栈、双方让过后抽 3 张牌并进入废牌堆。 |
| `p2-preflight-play-evolution-day-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·114/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340；p57 rule 413.4 | 已验证官方法术《进化日》支付 6 点费用、0 目标入栈、双方让过后抽 4 张牌并进入废牌堆。 |
| `p2-preflight-play-stellar-convergence-two-target-damage-stack` | `RULE_AUDITED` | `CATALOG` OGN·105/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《星芒凝汇》支付 6 点费用、选择最多两名单位、加入结算链、双方让过后对每名目标各造成 6 点伤害并进入废牌堆。 |
| `p2-preflight-play-rocket-barrage-base-unit-mode-stack` | `RULE_AUDITED` | `CATALOG` SFD·077/221；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《火箭轰击》选择基地单位伤害模式时，支付 4 点费用、指定基地中的一名单位、加入结算链、双方让过后造成 4 点伤害并进入废牌堆。 |
| `p2-preflight-play-void-seeker-damage-draw-stack` | `RULE_AUDITED` | `CATALOG` OGN·024/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《虚空索敌》支付 3 点费用、选择战场单位目标、加入结算链、双方让过后造成 4 点伤害并让控制者抽 1 张牌。 |
| `p2-preflight-void-seeker-draw-burnout-stack` | `RULE_AUDITED` | `CATALOG` OGN·024/298；`CORE-260330` p57 rule 413.4；p67 rule 431.2；p39-p42 rules 355-356 | 已验证官方法术《虚空索敌》的结算抽牌会在控制者主牌堆为空时触发燃尽，对手得 1 分，控制者回收废牌堆并抽到回收牌。 |
| `p2-preflight-play-rune-prison-stun-stack` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《符文禁锢》支付 2 点费用、选择战场单位目标、加入结算链、双方让过后施加 `STUNNED` 本回合内效果并进入废牌堆。 |
| `p2-preflight-play-rune-prison-base-unit-stun-stack` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p39-p42 rules 355-356；p33-p35 rules 327-340 | 已验证官方法术《符文禁锢》的“眩晕一名单位”目标范围允许指定基地单位。 |
| `p2-preflight-rune-prison-stun-expires-end-turn` | `RULE_AUDITED` | `CATALOG` OGN·050/298；`CORE-260330` p30-p33 rules 317-324, p39-p42 rules 355-356；`JFAQ-251023` p6-p7 questions 5.1-5.2 | 已验证官方法术《符文禁锢》施加的 `STUNNED` 会在随后 `END_TURN` 特殊清理中失效，并自动推进到下一回合开始。 |

## 6.1 当前三条 Fixture 冲突检查结论

- `PASS`：`CORE-260330` 将“让过”分别放在 FEPR 和法术对决语境中，且优先行动权/焦点决定谁能行动。`JFAQ-251023` questions 3.1-3.3 澄清焦点、活跃玩家和初始结算链；`BREAK-JFAQ-260416` p2-p5 的普通开环/法术对决个案未发现推翻通用 `PASS` 语义的条目。当前旧 Java 把初始 `PASS` 记成 `TURN_ENDED`，这是 legacy mismatch candidate。
- `END_TURN`：`CORE-260330` rules 316.6-317.3 支持主阶段结束后进入回合结束流程，并在下一回合开始执行召出、抽牌等步骤。`JFAQ-251023` questions 5.1-5.2 补充清理和特殊清理；`BREAK-JFAQ-260416` p11 仅发现额外回合相关个案，未改变裸 `END_TURN` 通用流程。
- `duplicate-pass`：这是网络重试和服务端权威幂等 fixture，不由游戏规则 PDF 裁决。它的验收依据是 command log 唯一键、同一 `clientIntentId` 不重复推进 tick、不重复写事件。

## 7. 索引维护规则

- 新增规则能力前，先在本索引中找到证据；找不到就先补索引。
- 不能把整段 PDF/FAQ 原文复制进仓库，只记录文件、页码、规则号/问题号和非原文摘要。
- 如果 FAQ 与旧 Java 行为冲突，新增 `legacyMismatch` 记录，并以 PDF/FAQ expected 为准。
- 每次新增 PDF、FAQ 或官网快照，先更新本索引，再更新 fixture 和状态矩阵。
