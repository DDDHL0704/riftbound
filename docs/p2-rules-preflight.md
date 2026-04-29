# P2 核心规则前置审查

更新时间：2026-04-29

## 1. 目的

本文件是进入 P2 核心规则实现前的执行清单。它把符文资源、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS` 和清理流程先从五份 PDF/FAQ 中拆成可实现的状态、事件和 fixture，避免继续继承旧 Java 的 `PASS -> TURN_ENDED` 混淆。

本文件不是规则全文摘录。实现时仍必须回到本地五份 PDF/FAQ 和官网卡牌快照核对。

## 2. 当前裁决

| 能力 | 裁决 | 证据 | 对旧 Java 的态度 |
|---|---|---|---|
| 符文池 | 法力/符能先进入玩家符文池，再用于支付费用；抽牌阶段结束和回合结束都会清空所有玩家符文池。 | `CORE-260330` p20 rules 164-167, p28-p31 rules 315.4, 317.2 | Java snapshot 可作对照，但 P2 必须建服务端权威资源状态。 |
| `END_TURN` | 表示主阶段没有要执行的自决行动，随后进入回合结束阶段。 | `CORE-260330` p29-p31 rules 316.1-317.3; `JFAQ-251023` p6-p7 questions 5.1-5.2 | Java 粗粒度事件可临时对照，最终事件要拆细。 |
| `PASS_PRIORITY` | 只表示 FEPR 流程中让过优先行动权；不能等同结束回合。 | `CORE-260330` p27-p28 rules 312-313, p33-p35 rules 333-340 | `java-oracle-p1-pass` 的 `TURN_ENDED` 是 legacy mismatch candidate。 |
| `PASS_FOCUS` | 只表示法术对决中让过焦点；所有玩家依次让过才关闭法术对决。 | `CORE-260330` p35-p36 rules 341-348; `JFAQ-251023` p4-p5 questions 3.1-3.3 | 已补独立 fixture，不能从裸 `PASS` 推断。 |
| 清理/特殊清理 | 清理期间不结算合法项目，不授予/传递优先行动权或焦点；若清理改变状态导致再次满足清理条件，继续清理至稳定。回合结束和战斗清理是特殊清理。 | `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.4 | P2 事件和状态机必须显式支持重复清理。 |

## 2.1 当前实现状态

已完成第一批代码地基：

- `ConformanceFixture` 已支持 schema v2 的 `initialState`、`expected.finalState`、`expected.events`、`expected.snapshots`、`expected.prompts` 和 `cardObjects`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start.fixture.json` 已记录回合开始、召出符文、抽牌、清空符文池、进入主阶段的规则审查样例。
- `MatchState` 已加入 `turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`、`cardObjects`、`priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`、`winnerPlayerId`、`destroyedUnitOwnerIdsThisTurn`，snapshot timing 已投影 timing、结算链、焦点、赢家字段和最小本回合摧毁记忆，玩家 `handSize` 和 `score` 来自权威状态。
- runner 已能把 P2 `initialState` 应用为真实初始局面，包含 turn/phase/timing、符文池和玩家区域。
- `CoreRuleEngine` 已实现普通回合开始最小流程，并通过 `p2-preflight-turn-start.fixture.json` 验证：召出 2 张符文、抽 1 张牌、清空所有玩家符文池、进入主阶段。
- `p2-preflight-turn-start-short-rune-deck.fixture.json` 已验证符文牌堆不足 2 张时有多少召出多少。
- `p2-preflight-turn-start-first-p2-extra-rune.fixture.json` 已验证 1v1 第二个行动玩家在自己首个召出阶段额外召出 1 张符文。
- `p2-preflight-turn-start-burnout.fixture.json` 已验证抽牌阶段主牌堆为空且废牌堆有牌可回收时：执行燃尽、对手得 1 分、回收废牌堆并完成抽牌。
- `p2-preflight-turn-start-burnout-empty-graveyard-wins.fixture.json` 已验证燃尽后主牌堆仍为空时会继续燃尽，若对手达到获胜分且领先则立即获胜。
- `p2-preflight-end-turn-advances-to-next-start.fixture.json` 已验证 P1 主阶段 `END_TURN` 会记录回合结束声明、执行无伤害/无持续效果的最小特殊清理、清空符文池、推进到 P2，并自动结算 P2 回合开始。
- `p2-preflight-end-turn-special-cleanup.fixture.json` 已验证回合结束特殊清理会移除单位伤害、令期限为本回合内的效果同时失效、清空符文池并进入下一回合开始流程。
- `p2-preflight-cleanup-repeats-until-stable.fixture.json` 已验证特殊清理造成对象状态变化后追加一次常规清理检查，并且不重复执行回合结束特殊步骤。
- `p2-preflight-pass-priority-does-not-end-turn.fixture.json` 已验证普通主阶段误提交 `PASS_PRIORITY` 会以 `PHASE_NOT_ALLOWED` 拒绝，不产生 `TURN_END_DECLARED`，不推进 tick，也不结束回合。
- `p2-preflight-fepr-priority-pass-resolves-stack.fixture.json` 已验证有结算链项目时，当前优先权玩家让过后优先权转移，所有玩家让过后结算最新项目并回到普通主阶段。
- `p2-preflight-fepr-resolves-latest-keeps-remaining-stack.fixture.json` 已验证最新项目结算后若结算链仍不为空，则新的最新项目控制者获得优先行动权并维持闭环。
- `p2-preflight-spell-duel-pass-focus-closes-window.fixture.json` 已验证法术对决开环时焦点让过、焦点转移，以及所有玩家让过焦点后关闭法术对决并回到普通主阶段。
- `p2-preflight-play-punishment-damage-stack.fixture.json` 已验证官方法术 `UNL-007/219 惩戒` 的最小 `PLAY_CARD` 通道：验证手牌、费用和目标，支付费用，加入结算链，双方让过后结算 3 点伤害并进入废牌堆。
- `p2-preflight-play-punishment-base-unit-damage-stack.fixture.json` 已验证《惩戒》的“对一名单位”目标范围可指定基地中的单位，而非仅限战场单位。
- `p2-preflight-play-abyssal-hunt-damage-stack.fixture.json` 已验证官方法术 `UNL-014/219 渊海狩咒` 在未控制正面朝下卡牌时的最小 `PLAY_CARD` 通道：支付 1 点费用，加入结算链，双方让过后对目标单位造成 2 点伤害并进入废牌堆。
- `p2-preflight-play-abyssal-hunt-face-down-damage-stack.fixture.json` 已验证 `UNL-014/219 渊海狩咒` 在控制者控制正面朝下战场牌时改为造成 4 点伤害。
- `p2-preflight-play-incinerate-damage-stack.fixture.json` 已验证官方法术 `OGS·003/024 焚烧` 的最小 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后对目标单位造成 2 点伤害并进入废牌堆。
- `p2-preflight-play-hextech-ray-damage-stack.fixture.json` 已验证官方法术 `OGN·009/298 海克斯射线` 的最小 `PLAY_CARD` 通道：支付 1 点费用，加入结算链，双方让过后对目标单位造成 3 点伤害并进入废牌堆。
- `p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json` 已验证《海克斯射线》造成的真实伤害会在随后 `END_TURN` 的特殊清理中通过 `DAMAGE_REMOVED` 移除，并自动推进到下一回合开始。
- `p2-preflight-play-comet-strike-damage-stack.fixture.json` 已验证官方法术 `OGN·085/298 彗星坠击` 的最小 `PLAY_CARD` 通道：支付 5 点费用，加入结算链，双方让过后对目标单位造成 6 点伤害并进入废牌堆。
- `p2-preflight-play-final-spark-damage-stack.fixture.json` 已验证官方法术 `OGS·022/024 终极闪光` 的最小 `PLAY_CARD` 通道：支付 8 点费用，加入结算链，双方让过后对一名单位造成 8 点伤害并进入废牌堆。
- `p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json` 已验证官方专属法术 `OGN·252/298 超究极死神飞弹！` 的最小 `PLAY_CARD` 通道：支付 4 点费用，加入结算链，双方让过后对一名单位造成 5 点伤害；征服后从废牌堆返回手牌的触发能力暂缓。
- `p2-preflight-play-center-stage-draw-stack.fixture.json` 已验证官方法术 `UNL-061/219 台前作秀` 不支付回响时的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 1 张牌并进入废牌堆。
- `p2-preflight-play-center-stage-echo-draw-stack.fixture.json` 已验证《台前作秀》支付 `optionalCosts = ["ECHO"]` 时额外支付 2 点费用，并重复基础抽牌效果一次。
- `p2-preflight-play-prophets-omen-draw-stack.fixture.json` 已验证官方法术 `SFD·087/221 先知之兆` 的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 3 张牌并进入废牌堆。
- `p2-preflight-play-evolution-day-draw-stack.fixture.json` 已验证官方法术 `OGN·114/298 进化日` 的 0 目标 `PLAY_CARD` 通道：支付 6 点费用，加入结算链，双方让过后抽 4 张牌并进入废牌堆。
- `p2-preflight-play-vengeance-destroy-unit-stack.fixture.json` 已验证官方法术 `OGN·229/298 复仇` 的最小 `PLAY_CARD` 通道：支付 4 点费用，加入结算链，双方让过后摧毁目标单位，将其移入拥有者废牌堆并移除场上对象状态。
- `p2-preflight-play-detonation-destroy-battlefield-unit-stack.fixture.json` 已验证官方法术 `OGS·012/024 爆能术` 的最小 `PLAY_CARD` 通道：支付 6 点费用，选择战场上的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆。
- `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json` 已验证官方法术 `SFD·164/221 流沙陷坑` 从手牌打出的全额费用路径：支付 5 点费用，选择战场上的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆；从手牌以外位置打出的减费路径暂缓。
- `p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json` 已验证官方法术 `OGN·105/298 星芒凝汇` 的 `PLAY_CARD` 通道：支付 6 点费用，选择 1-2 名单位，双方让过后对每名目标各造成 6 点伤害。
- `p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json` 已验证官方法术 `SFD·077/221 火箭轰击` 的 `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4` 基础模式：支付 4 点费用，选择基地中的一名单位，双方让过后造成 4 点伤害。`回响`和摧毁装备模式暂缓；缺失模式有直接拒绝测试覆盖。
- `p2-preflight-play-void-seeker-damage-draw-stack.fixture.json` 已验证官方法术 `OGN·024/298 虚空索敌` 的最小 `PLAY_CARD` 通道：对目标单位造成 4 点伤害，然后控制者抽 1 张牌。
- `p2-preflight-void-seeker-draw-burnout-stack.fixture.json` 已验证《虚空索敌》结算抽牌时若控制者主牌堆为空，会先触发燃尽、对手得 1 分、回收废牌堆，再完成抽牌。
- `p2-preflight-play-rune-prison-stun-stack.fixture.json` 已验证官方法术 `OGN·050/298 符文禁锢` 的最小 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后对目标单位施加 `STUNNED` 本回合内效果并进入废牌堆。
- `p2-preflight-play-rune-prison-base-unit-stun-stack.fixture.json` 已验证《符文禁锢》的“眩晕一名单位”目标范围可指定基地中的单位。
- `p2-preflight-rune-prison-stun-expires-end-turn.fixture.json` 已验证《符文禁锢》施加的 `STUNNED` 会在随后 `END_TURN` 的特殊清理中通过 `UNTIL_END_OF_TURN_EXPIRED` 失效，并自动推进到下一回合开始。
- `p2-preflight-punishment-lethal-damage-destroys-unit.fixture.json` 已验证《惩戒》造成的 3 点伤害达到目标 3 点战力时，结算后立即记录 `UNIT_DESTROYED`，并将目标单位移入拥有者废牌堆。
- `p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json` 已验证《碎裂之火》对战场单位造成致命伤害后，先摧毁目标，再按卡面条件抽 1 张牌。
- `p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json` 已验证《碎裂之火》未摧毁目标时不会抽牌，并保留目标的伤害状态。
- `p2-preflight-starfall-damages-two-units.fixture.json` 已验证《星落》的两次单位伤害选择可以指向不同位置的单位，并在同一结算后摧毁多个达到战力伤害的目标。
- `p2-preflight-starfall-can-damage-same-unit-twice.fixture.json` 已验证《星落》可以两次选择同一个单位，目标不去重，并在伤害累积达到战力后摧毁该单位。
- `p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json` 已验证《艾卡西亚暴雨》的六次单位伤害选择可以重复命中同一单位，并在累计 12 点伤害后摧毁目标。
- `p2-preflight-play-stay-away-stun-draw-stack.fixture.json` 已验证《走开》从手牌打出时对目标施加 `STUNNED`，然后抽 1 张牌；待命路径暂缓。
- `p2-preflight-play-disposal-order-draw-mode.fixture.json` 已验证《处置命令》的 `DRAW_1` 模式，支付费用后 0 目标入栈并抽 1 张牌。
- `p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json` 已验证《处置命令》的 `RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3` 模式，选择对手废牌堆中最多三张牌并让其拥有者回收。
- `p2-preflight-play-meditation-draw-stack.fixture.json` 已验证《冥想》的基础抽牌路径，支付费用后 0 目标入栈并抽 1 张牌；让友方单位休眠作为额外费用再抽 1 张的路径暂缓。
- `p2-preflight-play-center-your-mind-draw-stack.fixture.json` 已验证《聚心凝神》未满足等级减费条件时的全额支付基础路径，支付费用后 0 目标入栈并抽 2 张牌；等级 6/11 减费路径暂缓。
- `p2-preflight-play-borrowed-history-draw-stack.fixture.json` 已验证《借鉴历史》从手牌打出的基础抽牌路径，支付费用后 0 目标入栈并抽 2 张牌；待命/反应时机路径暂缓。
- `p2-preflight-play-spoils-of-war-draw-stack.fixture.json` 已验证《以战养战》未满足减费条件时的全额支付基础路径，支付 4 点费用后 0 目标入栈并抽 2 张牌。
- `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json` 已验证本回合先由《惩戒》摧毁敌方单位后，《以战养战》读取 `destroyedUnitOwnerIdsThisTurn` 并减少 2 点费用，再抽 2 张牌。

尚未完成：

- `PLAY_CARD` 已通过最小 card behavior registry 支撑十三张官方伤害法术、两张眩晕法术、八条 0 目标抽牌路径、三条主动摧毁单位路径、一个伤害达到战力后的致命摧毁路径、一个结算后抽牌路径、一个“目标被此法术摧毁则抽牌”的条件抽牌路径、一条眩晕后抽牌路径、结算抽牌燃尽分支、一条本回合敌方单位被摧毁后的费用修正路径、一条从对手废牌堆回收所选牌路径、《渊海狩咒》的条件伤害、`ANY_UNIT` / `BATTLEFIELD_UNIT` / `BASE_UNIT` / `OPPONENT_GRAVEYARD_CARD` 目标范围分流、`1-2` 目标数量范围、两次/六次单位伤害选择、允许重复选择同一目标的多次执行法术、`PLAY_CARD.mode` 模式选择，以及 `optionalCosts = ["ECHO"]` 的单次回响额外费用；richer expected diff 已开始覆盖出牌 fixture 的事件 tick/sequence/payload 局部字段、最终玩家区域、对象状态和结算链；下一步逐批迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。

## 3. P2 最小状态模型

在扩展 `MatchState` 时，优先加入以下服务端权威字段。玩家视角 snapshot 只投影允许看到的部分。

| 状态域 | 最小字段 | 用途 |
|---|---|---|
| 回合 | `turnNumber`, `turnPlayerId`, `phase`, `timingState` | 区分回合开始、主阶段、回合结束、普通/法术对决、开环/闭环。 |
| 行动权 | `priorityPlayerId`, `focusPlayerId`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` | 表达谁能自决行动，以及连续让过何时推进。 |
| 符文资源 | `runePools[playerId].mana`, `runePools[playerId].power` | 费用支付、抽牌阶段结束清空、回合结束清空。 |
| 区域 | `mainDeck`, `runeDeck`, `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone`, `championZone` | 后续打出、召出、抽牌、公开/私密/隐秘信息边界。 |
| 对象状态 | `cardObjects[objectId].damage`, `cardObjects[objectId].power`, `cardObjects[objectId].untilEndOfTurnEffects`, `cardObjects[objectId].isFaceDown` | 回合结束特殊清理移除伤害、令本回合内效果失效；`power` 先服务伤害达到战力后的摧毁判定；`isFaceDown` 先服务待命/隐藏信息条件效果的规则前提，后续扩展控制者、附属关系等完整对象字段。 |
| 本回合事件记忆 | `destroyedUnitOwnerIdsThisTurn` | 先服务“本回合内有敌方单位被摧毁”类费用修正，后续再扩展为通用事件记忆/触发队列。 |
| 结算链 | `stackItems` 已落地；`pendingTasks`, `resolvingItemId` 后续补 | HOT FEPR、确认、执行、让过、结算。 |
| 清理 | `cleanupKind`, `cleanupIteration`, `pendingCleanupReasons` | 普通清理、回合结束特殊清理、战斗特殊清理和重复清理。 |
| 随机 | `seed`, `rngCursor` | 洗牌、抽牌和随机选择可重放；当前先服务多张卡牌同时回收到主牌堆底部的随机顺序，以及燃尽回收废牌堆时的洗匀顺序。 |

## 4. P2 事件词表先行

进入实现前先稳定事件名，后续 fixture 和 UI 都依赖这些名称。

| 事件 | 触发时机 |
|---|---|
| `TURN_START_BEGAN` | 新回合玩家成为回合玩家后，开始回合开始流程。 |
| `OBJECTS_READIED` | 唤醒阶段使可活跃对象变为活跃。 |
| `BATTLEFIELDS_SECURED` | 得分计算步骤据守战场。 |
| `RUNES_CALLED` | 召出阶段从符文牌堆召出符文。 |
| `CARD_DRAWN` / `BURNOUT_APPLIED` | 抽牌阶段抽牌或牌堆不足时燃尽。 |
| `MATCH_WON` | 玩家达到获胜分且高于对手，比赛立即结束。 |
| `RUNE_POOL_CLEARED` | 抽牌阶段结束或回合结束清空符文池。 |
| `MAIN_PHASE_BEGAN` | 主阶段开始，回合玩家获得普通开环行动窗口。 |
| `TURN_END_DECLARED` | 回合玩家提交 `END_TURN`。 |
| `TURN_END_CLEANUP_STARTED` | 回合结束特殊清理开始。 |
| `DAMAGE_REMOVED` | 回合结束特殊清理移除单位伤害。 |
| `UNTIL_END_OF_TURN_EXPIRED` | 本回合内期限效果失效。 |
| `TURN_PLAYER_ADVANCED` | 回合队列推进到下一位玩家。 |
| `CARD_PLAYED` | 玩家从手牌打出卡牌。 |
| `COST_PAID` | 打出卡牌或激活技能时支付费用。 |
| `STACK_ITEM_ADDED` | 卡牌或技能作为结算链项目入栈。 |
| `PRIORITY_PASSED` | `PASS_PRIORITY` 成功让过优先行动权。 |
| `FOCUS_PASSED` | `PASS_FOCUS` 成功让过焦点。 |
| `STACK_ITEM_RESOLVED` | 结算链最新项目完成结算。 |
| `DAMAGE_APPLIED` | 法术或技能对对象造成伤害。 |
| `UNIT_DESTROYED` | 法术/技能效果或致命伤害摧毁单位，并将其从场上移入拥有者废牌堆。 |
| `CARDS_RECYCLED` | 法术或技能让所选卡牌从废牌堆回到拥有者主牌堆。 |
| `SPELL_DUEL_CLOSED` | 所有玩家让过焦点后关闭法术对决。 |
| `CLEANUP_REPEATED` | 清理造成状态变化后再次启动清理。 |

P1 的 `TURN_ENDED`、`TURN_BEGAN`、`RUNE_CHANNELLED`、`CARD_DRAWN` 仍保留为 legacy placeholder，不应作为 P2 正式事件设计。

## 5. 第一批 P2 Fixture

这些 fixture 必须先成为 `RULE_AUDITED`，再实现对应规则。

| Fixture ID | 输入 | 期望重点 | 依据 |
|---|---|---|---|
| `p2-turn-start-runes-and-draw` | P2 非首个回合成为回合玩家，符文牌堆至少 2 张，主牌堆至少 1 张 | 召出 2 张符文，抽 1 张牌，抽牌阶段结束清空符文池，进入主阶段。 | `CORE-260330` p28-p29 rule 315; p20 rules 164-167; rule 481.7 |
| `p2-turn-start-short-rune-deck` | P2 非首个回合，符文牌堆不足 2 张 | 有多少召出多少，不越界，不报错。 | `CORE-260330` p28-p29 rule 315.3; rule 481.7 |
| `p2-turn-start-first-p2-extra-rune` | P2 作为第二个行动玩家的首个回合，符文牌堆至少 3 张 | 召出 3 张符文，抽 1 张牌，清空符文池，进入主阶段。 | `CORE-260330` p28-p29 rule 315.3; rule 481.7 |
| `p2-turn-start-burnout` | P2 非首个回合，主牌堆为空，废牌堆有 1 张牌 | 执行燃尽，对手得 1 分，废牌堆回收后抽 1 张牌，进入主阶段。 | `CORE-260330` p28-p29 rule 315.4; p57 rule 413.4; p67 rule 431.2 |
| `p2-turn-start-burnout-empty-graveyard-wins` | P2 抽牌时主牌堆和废牌堆均为空，对手 7 分 | 燃尽使对手到达 8 分并立即获胜，不继续进入主阶段。 | `CORE-260330` p57 rule 413.4; p67-p68 rules 431.1-431.3 |
| `p2-end-turn-advances-to-next-start` | P1 主阶段没有伤害和本回合内持续效果，P2 牌堆足够 | 记录回合结束声明，执行最小特殊清理，清空符文池，推进回合玩家，并自动结算 P2 回合开始。 | `CORE-260330` p29-p31 rules 316.1-317.3; p20 rules 164-167; p28-p29 rule 315; rule 481.7 |
| `p2-end-turn-special-cleanup` | 主阶段有未消耗资源、单位伤害和本回合内效果 | 记录回合结束声明，移除伤害，本回合内效果失效，清空符文池，推进回合玩家，并自动进入下一回合开始。 | `CORE-260330` p30-p31 rules 317.2.a-317.2.f; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-pass-priority-does-not-end-turn` | 普通主阶段没有结算链时误提交 `PASS_PRIORITY` | 不产生 `TURN_END_DECLARED`，不推进 tick，不结束回合，并返回 `PHASE_NOT_ALLOWED`。 | `CORE-260330` p33-p35 rules 333-340 |
| `p2-fepr-priority-pass-resolves-stack` | 有已确认结算链项目，双方依次让过优先行动权 | 让过转移优先行动权，所有玩家让过后结算最新项目。 | `CORE-260330` p33-p35 rules 333-340 |
| `p2-fepr-resolves-latest-keeps-remaining-stack` | 最新结算链项目结算后仍有较早项目留存 | 维持闭环，并由新的最新项目控制者获得优先行动权。 | `CORE-260330` p35 rule 340.4 |
| `p2-spell-duel-pass-focus-closes-window` | 非战斗法术对决开环，双方没有新增法术 | 焦点传递；所有玩家让过焦点后关闭法术对决并进行清理。 | `CORE-260330` p35-p36 rules 341-348; `JFAQ-251023` p4-p5 questions 3.1-3.3 |
| `p2-play-punishment-damage-stack` | P1 打出官方法术《惩戒》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标造成 3 点伤害并进入废牌堆。 | `CATALOG` UNL-007/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-punishment-base-unit-damage-stack` | P1 打出官方法术《惩戒》，目标为基地中的单位 | “对一名单位”允许选择基地单位；支付 2 点费用，卡牌入栈，双方让过后对目标造成 3 点伤害并进入废牌堆。 | `CATALOG` UNL-007/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-abyssal-hunt-damage-stack` | P1 打出官方法术《渊海狩咒》，目标为战场上的单位，且 P1 未控制正面朝下卡牌 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 2 点伤害并进入废牌堆。 | `CATALOG` UNL-014/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-abyssal-hunt-face-down-damage-stack` | P1 打出官方法术《渊海狩咒》，目标为战场上的单位，且 P1 控制正面朝下战场牌 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 4 点伤害并进入废牌堆。 | `CATALOG` UNL-014/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-incinerate-damage-stack` | P1 打出官方法术《焚烧》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标造成 2 点伤害并进入废牌堆。 | `CATALOG` OGS·003/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-hextech-ray-damage-stack` | P1 打出官方法术《海克斯射线》，目标为战场上的单位 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 3 点伤害并进入废牌堆。 | `CATALOG` OGN·009/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-hextech-ray-damage-clears-end-turn` | P1 打出并结算《海克斯射线》后结束回合 | 真实卡牌造成的伤害被回合结束特殊清理移除，记录 `DAMAGE_REMOVED` 和常规清理检查，然后推进到 P2 回合开始。 | `CATALOG` OGN·009/298; `CORE-260330` p30-p33 rules 317-324; p39-p42 rules 355-356; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-play-comet-strike-damage-stack` | P1 打出官方法术《彗星坠击》，目标为战场上的单位 | 支付 5 点费用，卡牌入栈，双方让过后对目标造成 6 点伤害并进入废牌堆。 | `CATALOG` OGN·085/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-final-spark-damage-stack` | P1 打出官方法术《终极闪光》，目标为一名战场单位 | 支付 8 点费用，卡牌入栈，双方让过后对一名单位造成 8 点伤害并进入废牌堆。 | `CATALOG` OGS·022/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-super-mega-death-rocket-damage-stack` | P1 打出官方专属法术《超究极死神飞弹！》，目标为一名战场单位 | 支付 4 点费用，卡牌入栈，双方让过后对一名单位造成 5 点伤害；废牌堆返回手牌触发能力暂缓。 | `CATALOG` OGN·252/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-center-stage-draw-stack` | P1 打出官方法术《台前作秀》，不支付回响 | 支付 2 点费用，0 目标卡牌入栈，双方让过后抽 1 张牌并进入废牌堆。 | `CATALOG` UNL-061/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-play-center-stage-echo-draw-stack` | P1 打出官方法术《台前作秀》，支付回响 | 支付基础 2 点和回响 2 点费用，0 目标卡牌入栈，双方让过后重复抽牌效果一次，共抽 2 张牌。 | `CATALOG` UNL-061/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-play-prophets-omen-draw-stack` | P1 打出官方法术《先知之兆》 | 支付 2 点费用，0 目标卡牌入栈，双方让过后抽 3 张牌并进入废牌堆。 | `CATALOG` SFD·087/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-play-evolution-day-draw-stack` | P1 打出官方法术《进化日》 | 支付 6 点费用，0 目标卡牌入栈，双方让过后抽 4 张牌并进入废牌堆。 | `CATALOG` OGN·114/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-play-vengeance-destroy-unit-stack` | P1 打出官方法术《复仇》，目标为战场上的一名单位 | 支付 4 点费用，卡牌入栈，双方让过后摧毁目标单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-detonation-destroy-battlefield-unit-stack` | P1 打出官方法术《爆能术》，目标为战场上的一名单位 | 支付 6 点费用，卡牌入栈，双方让过后摧毁战场单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGS·012/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack` | P1 从手牌打出官方法术《流沙陷坑》，目标为战场上的一名单位 | 支付 5 点费用，卡牌入栈，双方让过后摧毁战场单位；从非手牌位置打出的减费路径暂缓。 | `CATALOG` SFD·164/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-punishment-lethal-damage-destroys-unit` | P1 打出官方法术《惩戒》，目标为 3 战力单位 | 造成 3 点伤害后立即因伤害达到战力而摧毁目标，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` UNL-007/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p62-p63 rule 428 |
| `p2-preflight-shattered-fire-draws-after-lethal-damage` | P1 打出官方法术《碎裂之火》，目标为战场上的 3 战力单位 | 支付 4 点费用，造成 3 点伤害并摧毁目标后，按“如果该单位被此法术摧毁”条件抽 1 张牌。 | `CATALOG` OGN·005/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-shattered-fire-does-not-draw-without-destroy` | P1 打出官方法术《碎裂之火》，目标为战场上的 4 战力单位 | 造成 3 点伤害但目标存活，不触发卡面抽牌条件，目标保留 3 点伤害。 | `CATALOG` OGN·005/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-starfall-damages-two-units` | P1 打出官方法术《星落》，两次伤害分别选择战场单位和基地单位 | 支付 2 点费用，进行两次 3 点伤害选择；两个 3 战力目标均被摧毁并进入拥有者废牌堆。 | `CATALOG` OGN·029/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-starfall-can-damage-same-unit-twice` | P1 打出官方法术《星落》，两次伤害均选择同一战场单位 | 同一目标保留两次选择，累计受到 6 点伤害后被摧毁。 | `CATALOG` OGN·029/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-icathian-rain-can-hit-same-unit-six-times` | P1 打出官方法术《艾卡西亚暴雨》，六次伤害均选择同一战场单位 | 支付 7 点费用，同一目标保留六次选择，累计受到 12 点伤害后被摧毁。 | `CATALOG` OGN·248/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-stay-away-stun-draw-stack` | P1 从手牌打出官方法术《走开》 | 支付 3 点费用，眩晕一名单位，然后因从手牌打出而抽 1 张牌。 | `CATALOG` UNL-042/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-disposal-order-draw-mode` | P1 打出官方法术《处置命令》并选择抽牌模式 | `PLAY_CARD.mode = DRAW_1`，支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-disposal-order-recycle-opponent-graveyard` | P1 打出官方法术《处置命令》并选择对手废牌堆回收模式 | `PLAY_CARD.mode = RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3`，选择对手废牌堆中最多三张牌，双方让过后让其拥有者回收；多张回收到主牌堆底部时用 seed/rngCursor 生成可回放随机顺序。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-meditation-draw-stack` | P1 打出官方法术《冥想》，不支付休眠友方单位的额外费用 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌；休眠额外费用再抽 1 张路径暂缓。 | `CATALOG` OGN·048/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-center-your-mind-draw-stack` | P1 打出官方法术《聚心凝神》，不满足等级减费 | 支付 5 点费用，0 目标入栈，双方让过后抽 2 张牌；等级 6/11 减费路径暂缓。 | `CATALOG` UNL-091/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-borrowed-history-draw-stack` | P1 从手牌打出官方法术《借鉴历史》 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌；待命/反应时机路径暂缓。 | `CATALOG` OGN·083/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-spoils-of-war-draw-stack` | P1 打出官方法术《以战养战》，本回合未摧毁敌方单位 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌。 | `CATALOG` OGN·144/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed` | P1 本回合先用《惩戒》摧毁 P2 单位，再打出《以战养战》 | 记录敌方单位拥有者被摧毁的本回合记忆；《以战养战》费用从 4 降到 2，并抽 2 张牌。 | `CATALOG` UNL-007/219, OGN·144/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-play-stellar-convergence-two-target-damage-stack` | P1 打出官方法术《星芒凝汇》，目标为一名战场单位和一名基地单位 | 支付 6 点费用，卡牌入栈，双方让过后对每名目标各造成 6 点伤害并进入废牌堆；一目标路径有直接测试覆盖。 | `CATALOG` OGN·105/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-rocket-barrage-base-unit-mode-stack` | P1 打出官方法术《火箭轰击》，选择基地单位伤害模式 | `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4`，支付 4 点费用，目标必须是基地中的单位，双方让过后造成 4 点伤害；`回响`和摧毁装备模式暂缓。 | `CATALOG` SFD·077/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-void-seeker-damage-draw-stack` | P1 打出官方法术《虚空索敌》，目标为战场上的单位 | 支付 3 点费用，卡牌入栈，双方让过后对目标造成 4 点伤害，然后 P1 抽 1 张牌。 | `CATALOG` OGN·024/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-void-seeker-draw-burnout-stack` | P1 打出《虚空索敌》且 P1 主牌堆为空、废牌堆有 1 张牌 | 伤害结算后执行抽牌；抽牌先触发燃尽、P2 得 1 分、P1 回收废牌堆，再抽到回收牌。 | `CATALOG` OGN·024/298; `CORE-260330` p57 rule 413.4; p67 rule 431.2; p39-p42 rules 355-356 |
| `p2-play-rune-prison-stun-stack` | P1 打出官方法术《符文禁锢》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标施加 `STUNNED` 本回合内效果并进入废牌堆。 | `CATALOG` OGN·050/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-rune-prison-base-unit-stun-stack` | P1 打出官方法术《符文禁锢》，目标为基地中的单位 | “眩晕一名单位”允许选择基地单位；支付 2 点费用，卡牌入栈，双方让过后对目标施加 `STUNNED`。 | `CATALOG` OGN·050/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-rune-prison-stun-expires-end-turn` | P1 打出并结算《符文禁锢》后结束回合 | `STUNNED` 作为本回合内效果失效，记录 `UNTIL_END_OF_TURN_EXPIRED` 和常规清理检查，然后推进到 P2 回合开始。 | `CATALOG` OGN·050/298; `CORE-260330` p30-p33 rules 317-324; p39-p42 rules 355-356; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-cleanup-repeats-until-stable` | 特殊清理造成对象状态变化 | 不重复特殊清理步骤；追加一次常规清理检查并稳定后推进回合。 | `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.4 |

## 6. 实现顺序

1. 已完成：扩展 fixture schema，加入 `initialState`、`expected.finalState`、`expected.events`、`expected.snapshots`、`expected.prompts` 的 P2 必要字段。
2. 已完成：扩展 `MatchState`，先建 `turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones` 等权威状态。
3. 已完成：让 runner 能把 `initialState` 应用到权威 `MatchState`。
4. 进行中：调整 snapshot 投影，确保当前权威 timing 可见；后续扩展区域时必须确保对手手牌、牌堆顺序、隐秘信息不可见。
5. 已完成：实现普通回合开始流程的最小规则引擎和行为 fixture。
6. 已完成：补符文牌堆不足 fixture。
7. 已完成：补 1v1 首回合额外符文 fixture。
8. 已完成：补主牌堆为空/燃尽 fixture。
9. 已完成：实现 `END_TURN` 的最小回合结束清理、回合推进，并接入回合开始自动结算。
10. 已完成：补 `END_TURN` 特殊清理中的伤害移除和本回合内效果失效 fixture。
11. 已完成：补清理重复/常规清理最小闭环 fixture。
12. 已完成：补普通主阶段误提交 `PASS_PRIORITY` 不结束回合 fixture。
13. 已完成：实现有结算链项目时的 `PASS_PRIORITY` / FEPR 最小流程。
14. 已完成：实现 `PASS_FOCUS` 和法术对决最小流程。
15. 已完成：补废牌堆也为空导致连续燃尽/胜利判定 fixture。
16. 已完成：以官方法术《惩戒》固定真实卡牌进入 FEPR 栈项目的最小通道。
17. 已完成：把《惩戒》逻辑抽成可扩展 card behavior registry。
18. 已完成：迁移第二张低复杂度官方伤害法术《渊海狩咒》的基础 2 点伤害路径。
19. 已完成：抽出 `PLAY_CARD` 的基础费用/目标/结算计划校验，并补《渊海狩咒》的正面朝下卡牌条件效果。
20. 已完成：`ConformanceFixtureRunner.CompareExpected` 开始通用比较 final tick、event kinds、prompt actions、最终 timing、符文池、分数、玩家区域、对象状态和结算链，并已接入三条出牌 fixture。
21. 已完成：迁移第三张低复杂度官方伤害法术《焚烧》，并复用通用 expected diff。
22. 已完成：迁移第一张低复杂度眩晕法术《符文禁锢》，结算时写入 `cardObjects.untilEndOfTurnEffects`。
23. 已完成：补《符文禁锢》眩晕效果随 `END_TURN` 特殊清理失效的组合 fixture。
24. 已完成：迁移第四张低复杂度官方伤害法术《海克斯射线》。
25. 已完成：补《海克斯射线》真实伤害随 `END_TURN` 特殊清理移除的组合 fixture。
26. 已完成：迁移第一张“伤害后抽牌”法术《虚空索敌》，结算时更新控制者主牌堆、手牌和废牌堆。
27. 已完成：补《虚空索敌》结算抽牌触发燃尽/回收/抽牌 fixture。
28. 已完成：迁移第六张低复杂度官方伤害法术《彗星坠击》。
29. 已完成：拆分 `ANY_UNIT` / `BATTLEFIELD_UNIT` 目标范围，并补《惩戒》指定基地单位 fixture。
30. 已完成：补《符文禁锢》指定基地单位 fixture，并补“战场单位”法术不可指定基地单位的拒绝测试。
31. 已完成：支持 0 目标 stack item 结算，并补《台前作秀》不支付回响的基础抽牌 fixture。
32. 已完成：支持 `1-2` 目标数量范围和多目标逐个结算，并补《星芒凝汇》双目标 fixture 与一目标直接测试。
33. 已完成：支持 `PLAY_CARD.mode` 模式选择和 `BASE_UNIT` 目标范围，并补《火箭轰击》基地单位伤害模式 fixture 与缺失模式拒绝测试。
34. 已完成：支持 `optionalCosts = ["ECHO"]` 的单次回响额外费用和效果重复，并补《台前作秀》回响抽牌 fixture。
35. 已完成：迁移《终极闪光》《先知之兆》《进化日》三张低复杂度官方法术，覆盖任意单位 8 点伤害与 0 目标多张抽牌路径。
36. 已完成：`CompareExpected` 支持 `expected.events[].tick`、`sequence` 和 `payload` 局部匹配，并用《终极闪光》fixture 锁住费用、入栈目标和伤害 payload。
37. 已完成：实现主动摧毁单位的最小原语，并补《复仇》摧毁单位 fixture。
38. 已完成：为 `CardObjectState` 增加 `power`，实现伤害达到战力后的最小致命摧毁，并补《惩戒》致命伤害 fixture。
39. 已完成：支持“目标被此效果摧毁则抽牌”的最小条件抽牌，并补《碎裂之火》致命伤害后抽牌 fixture。
40. 已完成：补《碎裂之火》未摧毁目标不抽牌的反向 fixture。
41. 已完成：补《星落》两次单位伤害选择并摧毁多个目标的 fixture。
42. 已完成：支持多次执行法术允许重复选择同一目标，并补《星落》同一单位受两次伤害的 fixture。
43. 已完成：迁移《艾卡西亚暴雨》六次单位伤害路径，并补同一单位被六次命中的累计伤害 fixture。
44. 已完成：迁移《走开》从手牌打出的眩晕后抽牌基础路径；待命路径暂缓。
45. 已完成：迁移《处置命令》的 `DRAW_1` 模式和对手废牌堆最多三张回收模式。
46. 已完成：迁移《冥想》的基础抽牌路径；休眠友方单位的额外费用路径暂缓。
47. 已完成：迁移《爆能术》的战场单位摧毁路径。
48. 已完成：迁移《以战养战》的全额支付抽 2 张基础路径。
49. 已完成：迁移《超究极死神飞弹！》的基础 5 点伤害路径；征服后从废牌堆返回手牌的触发能力暂缓。
50. 已完成：迁移《流沙陷坑》从手牌打出的全额费用摧毁战场单位路径；从非手牌位置打出的减费路径暂缓。
51. 已完成：实现最小本回合摧毁记忆，并补《以战养战》在本回合敌方单位被摧毁后的减费 fixture。
52. 已完成：补 `OPPONENT_GRAVEYARD_CARD` 目标范围、`CARDS_RECYCLED` 事件，以及 `seed/rngCursor` 的最小可回放随机顺序，服务《处置命令》回收模式和燃尽回收洗匀。
53. 已完成：迁移《借鉴历史》从手牌打出的抽 2 张基础路径；待命/反应时机路径暂缓。
54. 已完成：迁移《聚心凝神》全额支付抽 2 张基础路径；等级 6/11 减费路径暂缓。
55. 下一步：逐批迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。

## 7. 暂缓项

- 不实现完整打出卡牌流程。
- 不实现全部战斗、得分、移动。
- 不实现复杂 AI。
- 不做产品级 UI。
- 不迁移全部卡牌。

## 8. 完成门禁

- 每个 P2 fixture 都有 `rulesEvidence`，且 `auditStatus = RULE_AUDITED`。
- `expected` 以 PDF/FAQ 和官网卡面裁决为准，不以 Java 输出为准。
- C# conformance runner 能输出稳定 diff。
- `state_snapshots.payload` 能保存完整权威状态。
- 玩家 snapshot 通过公开/私密/隐秘信息检查。
- solution 级 build/test 通过。
