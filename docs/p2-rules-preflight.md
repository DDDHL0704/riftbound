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
- `p2-preflight-play-punishment-damage-stack.fixture.json` 已验证官方法术 `UNL-007/219 惩戒` 的最小 `PLAY_CARD` 通道：验证手牌、费用和战场单位目标，支付费用，加入结算链，双方让过后结算 3 点伤害，并对目标施加本回合若被摧毁则改为放逐的替代效果。
- `CoreRuleEngineRejectsPunishmentAgainstBaseUnit` 已验证《惩戒》的官网卡面限定为“战场上的一名单位”，不能指定基地单位。
- `p2-preflight-play-abyssal-hunt-damage-stack.fixture.json` 已验证官方法术 `UNL-014/219 渊海狩咒` 在未控制正面朝下卡牌时的最小 `PLAY_CARD` 通道：支付 1 点费用，加入结算链，双方让过后对目标单位造成 2 点伤害并进入废牌堆。
- `p2-preflight-play-abyssal-hunt-face-down-damage-stack.fixture.json` 已验证 `UNL-014/219 渊海狩咒` 在控制者控制正面朝下战场牌时改为造成 4 点伤害。
- `p2-preflight-play-dancing-grenade-base-unit-damage.fixture.json` 已验证官方法术 `UNL-020/219 曼舞手雷` 不进入再次打出分支时，对一名基地单位造成 2 点伤害；支付 `A` 再次打出并按次数递增伤害的分支暂缓。
- `p2-preflight-play-incinerate-damage-stack.fixture.json` 已验证官方法术 `OGS·003/024 焚烧` 的最小 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后对目标单位造成 2 点伤害并进入废牌堆。
- `p2-preflight-play-hextech-ray-damage-stack.fixture.json` 已验证官方法术 `OGN·009/298 海克斯射线` 的最小 `PLAY_CARD` 通道：支付 1 点费用，加入结算链，双方让过后对目标单位造成 3 点伤害并进入废牌堆。
- `p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json` 已验证《海克斯射线》造成的真实伤害会在随后 `END_TURN` 的特殊清理中通过 `DAMAGE_REMOVED` 移除，并自动推进到下一回合开始。
- `p2-preflight-play-comet-strike-damage-stack.fixture.json` 已验证官方法术 `OGN·085/298 彗星坠击` 的最小 `PLAY_CARD` 通道：支付 5 点费用，加入结算链，双方让过后对目标单位造成 6 点伤害并进入废牌堆。
- `p2-preflight-play-final-spark-damage-stack.fixture.json` 已验证官方法术 `OGS·022/024 终极闪光` 的最小 `PLAY_CARD` 通道：支付 8 点费用，加入结算链，双方让过后对一名单位造成 8 点伤害并进入废牌堆。
- `p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json` 已验证官方专属法术 `OGN·252/298 超究极死神飞弹！` 的最小 `PLAY_CARD` 通道：支付 4 点费用，加入结算链，双方让过后对一名单位造成 5 点伤害；征服后从废牌堆返回手牌的触发能力暂缓。
- `p2-preflight-play-center-stage-draw-stack.fixture.json` 已验证官方法术 `UNL-061/219 台前作秀` 不支付回响时的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 1 张牌并进入废牌堆。
- `p2-preflight-play-center-stage-echo-draw-stack.fixture.json` 已验证《台前作秀》支付 `optionalCosts = ["ECHO"]` 时额外支付 2 点费用，并重复基础抽牌效果一次。
- `p2-preflight-play-prophets-omen-draw-stack.fixture.json` 已验证官方法术 `SFD·087/221 先知之兆` 的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 3 张牌并进入废牌堆。
- `p2-preflight-play-might-makes-right-draw-powerful-units.fixture.json` 已验证官方法术 `SFD·106/221 实力至上` 的动态抽牌路径：支付 2 点费用，按控制者当前控制的强力单位数量抽牌，且只统计战力达到 5 或以上的己方单位。
- `p2-preflight-play-evolution-day-draw-stack.fixture.json` 已验证官方法术 `OGN·114/298 进化日` 的 0 目标 `PLAY_CARD` 通道：支付 6 点费用，加入结算链，双方让过后抽 4 张牌并进入废牌堆。
- `p2-preflight-play-mobilize-call-rune.fixture.json` 已验证官方法术 `OGN·134/298 动员` 的召出休眠符文路径：支付 2 点费用，0 目标入栈，双方让过后从控制者符文牌堆顶召出 1 枚符文到基地，并在对象状态中标记 `isExhausted = true`。
- `p2-preflight-play-mobilize-draws-if-rune-call-fails.fixture.json` 已验证《动员》无法召出符文时转入抽 1 张牌分支。
- `p2-preflight-play-catalyst-of-aeons-call-two-runes.fixture.json` 已验证官方法术 `OGN·138/298 万世催化石` 的完整召出休眠符文路径：支付 4 点费用，0 目标入栈，双方让过后从控制者符文牌堆顶召出 2 枚符文到基地，并在对象状态中标记 `isExhausted = true`。
- `p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short.fixture.json` 已验证《万世催化石》只能召出 1 枚休眠符文时仍尽可能召出并标记休眠状态，随后因无法完整召出 2 枚符文而抽 1 张牌。
- `p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune.fixture.json` 已验证官方法术 `OGN·047/298 御衡守念` 在对手距胜利得分不超过 3 分时费用减少 2，并按卡面顺序先抽 1 张牌，再召出 1 枚休眠符文到基地。
- `p2-preflight-play-vengeance-destroy-unit-stack.fixture.json` 已验证官方法术 `OGN·229/298 复仇` 的最小 `PLAY_CARD` 通道：支付 4 点费用，加入结算链，双方让过后摧毁目标单位，将其移入拥有者废牌堆并移除场上对象状态。
- `p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit.fixture.json` 已验证官方专属法术 `UNL-186/219 涌泉之恨` 的基础摧毁路径：支付 4 点费用，选择战场上的一名 4 战力单位，双方让过后摧毁目标并移入拥有者废牌堆；不高于 3 战力后的符能再打出分支暂缓。
- `p2-preflight-play-detonation-destroy-battlefield-unit-stack.fixture.json` 已验证官方法术 `OGS·012/024 爆能术` 的最小 `PLAY_CARD` 通道：支付 6 点费用，选择战场上的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆。
- `p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit.fixture.json` 已验证官方法术 `UNL-159/219 狩魂` 的最小 `PLAY_CARD` 通道：支付 2 点费用，选择战场上不高于 3 战力的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆；4 战力目标由直接拒绝测试覆盖。
- `p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json` 已验证官方法术 `OGN·213/298 暗刃` 从手牌打出的基础路径：支付 2 点费用，摧毁战场中的一名单位，然后让该单位当前控制者抽 2 张牌；待命路径暂缓。
- `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json` 已验证官方法术 `SFD·164/221 流沙陷坑` 从手牌打出的全额费用路径：支付 5 点费用，选择战场上的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆；从手牌以外位置打出的减费路径暂缓。
- `p2-preflight-play-ruination-destroy-all-units.fixture.json` 已验证官方法术 `UNL-180/219 破败之咒` 的全局摧毁路径：支付 9 点费用，0 目标入栈，双方让过后摧毁当前所有场上单位，即各玩家基地和战场区域中的单位。
- `p2-preflight-play-undertow-return-all-units.fixture.json` 已验证官方法术 `SFD·147/221 坠渊之流` 的全局回手路径：支付 8 点费用，0 目标入栈，双方让过后让当前所有场上单位返回所属者手牌；装备返回待装备对象模型落地后补。
- `p2-preflight-play-reprimand-return-battlefield-unit.fixture.json` 已验证官方法术 `OGN·172/298 责退` 的回手路径：支付 2 点费用，选择战场上的一名单位，双方让过后将其从战场移入所属者手牌，并移除场上对象状态。
- `p2-preflight-play-gust-return-small-battlefield-unit.fixture.json` 已验证官方法术 `OGN·169/298 罡风` 的小单位回手路径：支付 1 点费用，选择战场上不高于 3 战力的一名单位，双方让过后将其返回所属者手牌；4 战力目标由直接拒绝测试覆盖。
- `p2-preflight-play-reconsider-return-friendly-call-rune.fixture.json` 已验证官方法术 `OGN·104/298 择日再战` 的友方单位回手后召出休眠符文路径：支付 1 点费用，选择一名友方单位，双方让过后将其返回所属者手牌，再让其拥有者召出 1 枚休眠符文。
- `p2-preflight-play-happenstance-return-friendly-and-enemy.fixture.json` 已验证官方法术 `UNL-128/219 造化弄人` 的按序双目标回手路径：支付 3 点费用，第一目标必须是友方单位、第二目标必须是敌方单位，双方让过后分别返回所属者手牌；目标顺序反转由直接拒绝测试覆盖。
- `p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base.fixture.json` 已验证官方法术 `OGN·168/298 战或逃` 的战场单位移动到所属基地路径：支付 2 点费用，选择战场上的一名单位，双方让过后将其移动到所属者基地，并保留对象状态。
- `p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base.fixture.json` 已验证官方法术 `OGS·011/024 闪现` 的最多两名友方战场单位移动到基地路径；敌方单位和友方基地单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json` 已验证官方法术 `UNL-009/219 大幕渐起` 支付 `optionalCosts = ["ECHO"]` 后重复“让一名单位变为活跃状态”效果一次。
- `p2-preflight-play-beatdown-ready-unit.fixture.json` 已验证官方法术 `OGN·146/298 痛殴` 不支付消耗增益额外费用时，让一名单位变为活跃状态的基础路径。
- `p2-preflight-play-hunt-ready-all-friendly-units.fixture.json` 已验证官方专属法术 `SFD·204/221 狩猎` 让控制者所有场上单位变为活跃状态，且不会影响对手单位。
- `p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json` 已验证官方法术 `OGN·123/298 过载能量` 先让所有友方单位变为休眠状态，再对所有战场上的单位各造成 12 点伤害，并在结算后执行致命伤害清理。
- `p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json` 已验证官方法术 `OGN·105/298 星芒凝汇` 的 `PLAY_CARD` 通道：支付 6 点费用，选择 1-2 名单位，双方让过后对每名目标各造成 6 点伤害。
- `p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json` 已验证官方法术 `SFD·077/221 火箭轰击` 的 `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4` 基础模式：支付 4 点费用，选择基地中的一名单位，双方让过后造成 4 点伤害。`回响`和摧毁装备模式暂缓；缺失模式有直接拒绝测试覆盖。
- `p2-preflight-play-production-surge-create-robot-draw.fixture.json` 已验证官方法术 `SFD·076/221 产量激增` 的全额费用路径：支付 4 点费用，0 目标入栈，双方让过后打出一名 3 战力“机器人”单位指示物到控制者基地，然后抽 1 张牌。
- `p2-preflight-play-production-surge-reduced-by-mechanical.fixture.json` 已验证《产量激增》在控制者控制“机械”属性单位时费用减少 2：通过对象状态 `tags = ["机械"]` 触发减费，支付 2 点费用后仍按卡面打出“机器人”到基地并抽 1 张牌。
- `p2-preflight-play-common-cause-create-four-minions-base.fixture.json` 已验证官方法术 `OGS·015/024 共同献身` 的基地目的地路径：`PLAY_CARD.mode = "BASE"`，支付 6 点费用，0 目标入栈，双方让过后打出四名 1 战力“随从”单位指示物到控制者基地。
- `p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json` 已验证官方法术 `SFD·031/221 点沙成兵` 支付回响 2 后重复单位指示物创建效果：支付基础 2 点和回响 2 点费用，双方让过后打出两名 2 战力“黄沙士兵”单位指示物到控制者基地。
- `p2-preflight-play-thundering-drop-attacking-damage-stack.fixture.json` 已验证官方法术 `SFD·017/221 雷霆突降` 从手牌打出的进攻方目标路径：基础 2 点伤害在目标为进攻方单位时改为 4 点；非进攻方目标的基础 2 点伤害由直接测试覆盖，待命路径暂缓。
- `p2-preflight-play-piercing-light-two-target-damage-stack.fixture.json` 已验证官方法术 `SFD·023/221 透体圣光` 不支付回响的基础路径：支付 2 点费用，选择 1-2 个不同的战场单位，双方让过后对每名目标各造成 2 点伤害；有色回响 `2红色` 路径暂缓，避免在符文颜色成本模型落地前误按通用费用处理。
- `p2-preflight-play-thundering-sky-cost-reduced-damage-stack.fixture.json` 已验证官方法术 `OGN·014/298 霹天雳地` 按控制者所控制单位中的最高战力降低法力费用：P1 控制 3 战力单位时，费用从 8 降到 5，并对战场单位造成 5 点伤害；没有足够降低后的费用时由直接拒绝测试覆盖。
- `p2-preflight-play-void-seeker-damage-draw-stack.fixture.json` 已验证官方法术 `OGN·024/298 虚空索敌` 的最小 `PLAY_CARD` 通道：对目标单位造成 4 点伤害，然后控制者抽 1 张牌。
- `p2-preflight-void-seeker-draw-burnout-stack.fixture.json` 已验证《虚空索敌》结算抽牌时若控制者主牌堆为空，会先触发燃尽、对手得 1 分、回收废牌堆，再完成抽牌。
- `p2-preflight-play-rune-prison-stun-stack.fixture.json` 已验证官方法术 `OGN·050/298 符文禁锢` 的最小 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后对目标单位施加 `STUNNED` 本回合内效果并进入废牌堆。
- `p2-preflight-play-rune-prison-base-unit-stun-stack.fixture.json` 已验证《符文禁锢》的“眩晕一名单位”目标范围可指定基地中的单位。
- `p2-preflight-rune-prison-stun-expires-end-turn.fixture.json` 已验证《符文禁锢》施加的 `STUNNED` 会在随后 `END_TURN` 的特殊清理中通过 `UNTIL_END_OF_TURN_EXPIRED` 失效，并自动推进到下一回合开始。
- `p2-preflight-play-kerplunk-stun-attacking-unit.fixture.json` 已验证官方法术 `SFD·040/221 扑咚！` 不支付回响的基础路径：目标必须是进攻方单位，双方让过后对目标施加 `STUNNED`；非进攻单位由直接拒绝测试覆盖。
- `p2-preflight-play-kerplunk-echo-stun-attacking-unit.fixture.json` 已验证《扑咚！》支付 `optionalCosts = ["ECHO"]` 后，基础费用和回响费用共支付 4 点，并重复眩晕同一进攻方单位。
- `p2-preflight-play-existential-dread-echo-stun-then-return.fixture.json` 已验证官方法术 `UNL-134/219 存在焦虑` 支付回响后的条件路径：目标必须是正在进攻的敌方单位，第一次结算施加 `STUNNED`，重复效果看到目标已被眩晕后改为让其返回所属者手牌；友方进攻单位目标由直接拒绝测试覆盖。
- `p2-preflight-punishment-lethal-damage-banishes-unit.fixture.json` 已验证《惩戒》造成的 3 点伤害达到目标 3 点战力时，目标因本回合替代效果改为记录 `UNIT_BANISHED`，并移入拥有者放逐区而非废牌堆。
- `p2-preflight-punishment-banishes-if-destroyed-later.fixture.json` 已验证《惩戒》目标若在同一回合稍后被《复仇》摧毁，也会由替代效果改为放逐，且不写入本回合摧毁记忆。
- `p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json` 已验证《碎裂之火》对战场单位造成致命伤害后，先摧毁目标，再按卡面条件抽 1 张牌。
- `p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json` 已验证《碎裂之火》未摧毁目标时不会抽牌，并保留目标的伤害状态。
- `p2-preflight-starfall-damages-two-units.fixture.json` 已验证《星落》的两次单位伤害选择可以指向不同位置的单位，并在同一结算后摧毁多个达到战力伤害的目标。
- `p2-preflight-starfall-can-damage-same-unit-twice.fixture.json` 已验证《星落》可以两次选择同一个单位，目标不去重，并在伤害累积达到战力后摧毁该单位。
- `p2-preflight-play-duel-mutual-power-damage.fixture.json` 已验证《决斗》按“友方单位、敌方单位”的目标顺序，让两名单位以自身当前战力互相造成伤害，并在致命伤害清理中摧毁达到战力伤害的敌方单位；直接测试另覆盖反向目标顺序会被拒绝。
- `p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json` 已验证《绅士决斗》先让友方目标本回合内战力 +3，再按两名目标当前战力互相造成伤害；样例覆盖战力修正后的互伤与致命伤害清理。
- `p2-preflight-play-marching-orders-echo-mutual-power-damage.fixture.json` 已验证《行军号令》支付回响后重复友方单位与敌方战场单位按自身战力互相造成伤害；样例覆盖友方基地单位与敌方战场单位互伤。
- `p2-preflight-play-clash-of-giants-mutual-power-damage.fixture.json` 已验证《巨人之战》可选择任意两名单位，让两名目标以自身当前战力互相造成伤害；样例覆盖基地单位和战场单位互伤后摧毁达到战力伤害的战场单位。
- `p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json` 已验证《艾卡西亚暴雨》的六次单位伤害选择可以重复命中同一单位，并在累计 12 点伤害后摧毁目标。
- `p2-preflight-play-blade-whirlwind-damage-all-battlefield-units.fixture.json` 已验证《剑刃飓风》0 目标入栈后，对所有战场上的单位各造成 1 点伤害，不分敌我；当前样例锁定未致命伤害路径。
- `p2-preflight-blade-whirlwind-lethal-damage-destroys-units.fixture.json` 已验证《剑刃飓风》全战场单位伤害达到多个单位战力时，会逐一记录 `UNIT_DESTROYED` 并将单位移入各自拥有者废牌堆。
- `p2-preflight-play-cannon-barrage-damage-enemy-combat-units.fixture.json` 已验证《加农炮幕》0 目标入栈后，只对战斗中的敌方单位各造成 2 点伤害；当前最小模型以 `isAttacking` / `isDefending` 表达战斗中单位，并确认友方战斗中单位和敌方非战斗单位不受影响。
- `p2-preflight-play-stay-away-stun-draw-stack.fixture.json` 已验证《走开》从手牌打出时对目标施加 `STUNNED`，然后抽 1 张牌；待命路径暂缓。
- `p2-preflight-play-disposal-order-draw-mode.fixture.json` 已验证《处置命令》的 `DRAW_1` 模式，支付费用后 0 目标入栈并抽 1 张牌。
- `p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json` 已验证《处置命令》的 `RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3` 模式，选择对手废牌堆中最多三张牌并让其拥有者回收。
- `p2-preflight-play-meditation-draw-stack.fixture.json` 已验证《冥想》的基础抽牌路径，支付费用后 0 目标入栈并抽 1 张牌。
- `p2-preflight-play-salvage-draw-no-equipment.fixture.json` 已验证《废物利用》不选择装备目标的合法分支：支付费用后 0 目标入栈，双方让过后直接抽 1 张牌；摧毁装备分支暂缓至装备对象模型落地。
- `p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields.fixture.json` 已验证《占山为王》在当前没有已控制战场时的基础路径：支付费用后 0 目标入栈，双方让过后只抽基础 1 张牌；按战场控制数量额外抽牌暂缓至战场控制模型落地。
- `p2-preflight-play-meditation-exhaust-friendly-extra-draw.fixture.json` 已验证《冥想》可用 `optionalCosts = ["EXHAUST_FRIENDLY_UNIT:<objectId>"]` 让一名活跃友方单位变为休眠状态作为额外费用，并额外抽 1 张牌；直接测试另覆盖敌方单位不能支付该额外费用。
- `p2-preflight-play-moonsilver-gift-discard-draw.fixture.json` 已验证《月神恩赐》选择另一张友方手牌，结算时先弃置该牌到废牌堆，再抽 2 张牌；直接测试另覆盖不能把正在打出的源牌作为弃置对象。
- `p2-preflight-play-revive-return-graveyard-unit.fixture.json` 已验证《亡者复生》选择己方废牌堆里的单位牌，结算时将该目标移回手牌；直接测试另覆盖不能选择对手废牌堆目标。
- `p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json` 已验证《反转时间线》0 目标入栈后，每名玩家先弃置自己的所有手牌，再各抽 4 张牌。
- `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune.fixture.json` 已验证《牺牲》必须用 `optionalCosts = ["DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"]` 摧毁一名友方强力单位作为额外费用，然后先抽 2 张牌、再召出 1 枚休眠符文；直接测试另覆盖缺少该额外费用或目标未达到 5 战力时拒绝。
- `p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw.fixture.json` 已验证《断魂一扼》先摧毁第一名友方目标，再按被摧毁单位当前战力让第二名友方目标本回合内获得等量 +战力加成，最后抽 1 张牌；直接测试另覆盖敌方单位不能作为第二目标。
- `p2-preflight-play-center-your-mind-draw-stack.fixture.json` 已验证《聚心凝神》未满足等级减费条件时的全额支付基础路径，支付费用后 0 目标入栈并抽 2 张牌；等级 6/11 减费路径暂缓。
- `p2-preflight-play-borrowed-history-draw-stack.fixture.json` 已验证《借鉴历史》从手牌打出的基础抽牌路径，支付费用后 0 目标入栈并抽 2 张牌；待命/反应时机路径暂缓。
- `p2-preflight-play-spoils-of-war-draw-stack.fixture.json` 已验证《以战养战》未满足减费条件时的全额支付基础路径，支付 4 点费用后 0 目标入栈并抽 2 张牌。
- `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json` 已验证本回合先由《复仇》摧毁敌方单位后，《以战养战》读取 `destroyedUnitOwnerIdsThisTurn` 并减少 2 点费用，再抽 2 张牌。
- `p2-preflight-play-practical-experience-power-plus-1.fixture.json` 已验证《实战经验》在未启用等级 6 升级时对一名单位施加本回合内战力 +1；等级 6 改为 +3 的路径暂缓。
- `p2-preflight-play-dueling-stance-friendly-power-plus-1.fixture.json` 已验证《决斗架势》基础分支对一名友方单位施加本回合内战力 +1；“该处唯一控制单位”额外 +1 分支暂缓到更细位置模型。
- `p2-preflight-play-animal-friends-power-per-controlled-tag.fixture.json` 已验证《动物之友》按控制者场上/基地单位中“鸟类、猫科、犬形、魄罗”属性标签的不同种类数动态计算本回合内战力修正；样例中三种标签使目标 +3。
- `p2-preflight-play-well-trained-power-draw-stack.fixture.json` 已验证《训练有素》对一名单位施加本回合内战力 +2，然后抽 1 张牌。
- `p2-preflight-well-trained-power-expires-end-turn.fixture.json` 已验证本回合内战力修正会在 `END_TURN` 特殊清理中移除，并追加常规清理检查。
- `p2-preflight-play-savage-strength-echo-power-stack.fixture.json` 已验证《蛮荒之力》支付 `optionalCosts = ["ECHO"]` 后，重复本回合内战力 +2 修正一次。
- `p2-preflight-play-freeze-echo-power-minus-2.fixture.json` 已验证《封冻》支付 `optionalCosts = ["ECHO"]` 后，重复本回合内战力 -2 修正一次。
- `p2-preflight-play-distance-break-dance-split-power-modifiers.fixture.json` 已验证《距破之舞》对两名不同单位分别施加本回合内战力 +2 和 -2 的分裂修正路径。
- `p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json` 已验证《顺劈》让一名单位本回合内获得 `OVERWHELM_3` 标记，且目标是进攻方时本回合内战力 +3；直接测试另覆盖非进攻方只获得关键词标记、不获得战力修正。
- `p2-preflight-play-blood-rush-echo-overwhelm-attacking-power.fixture.json` 已验证《血性冲刺》支付 `optionalCosts = ["ECHO"]` 后重复授予 `OVERWHELM_2` 并重复进攻方本回合内战力 +2，最终目标累计 +4。
- `p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power.fixture.json` 已验证《怒吼清算》支付 `optionalCosts = ["DISCARD_HAND_CARD:<objectId>"]` 弃置一张手牌作为回响额外费用后，重复授予 `OVERWHELM_4` 并重复进攻方本回合内战力 +4；直接测试另覆盖不能把源牌自己作为弃牌费用。
- `p2-preflight-play-power-punch-overwhelm-roam-attacking-power.fixture.json` 已验证《强能冲拳》让一名单位本回合内同时获得 `OVERWHELM_2` 和 `ROAM` 标记，且目标是进攻方时本回合内战力 +2；直接测试另覆盖非进攻方只获得关键词标记、不获得战力修正。
- `p2-preflight-play-parry-steadfast-barrier-defending-power.fixture.json` 已验证《格挡》让一名单位本回合内同时获得 `STEADFAST_3` 和 `BARRIER` 标记，且目标是防守方时本回合内战力 +3；直接测试另覆盖非防守方只获得关键词标记、不获得战力修正。
- `p2-preflight-play-shoot-first-power-plus-5-stack.fixture.json` 已验证《先打再问》对一名单位施加本回合内战力 +5，并记录待回合结束清理移除的实际修正。
- `p2-preflight-play-tremendous-strength-power-plus-7.fixture.json` 已验证《洪荒巨力》对一名单位施加本回合内战力 +7，并记录待回合结束清理移除的实际修正。
- `p2-preflight-play-eclipse-power-minus-4.fixture.json` 已验证《月蚀》对一名单位施加本回合内战力 -4；洞察路径暂缓到牌堆顶部查看/回收选择模型。
- `p2-preflight-play-moonfall-power-minus-10.fixture.json` 已验证《月光之殇》对一名单位施加本回合内战力 -10，并记录待回合结束清理移除的实际修正。
- `p2-preflight-play-glory-call-power-plus-3.fixture.json` 已验证《荣耀召唤》未支付消耗增益额外费用时，对一名单位施加本回合内战力 +3；消耗增益以无视费用的路径暂缓。
- `p2-preflight-play-last-stand-friendly-power-plus-3.fixture.json` 已验证《视死如归》对一名友方单位施加本回合内战力 +3；该单位本回合赢得战斗时获得 2 经验的触发路径暂缓到战斗胜负/经验模型。
- `p2-preflight-play-decisive-strike-all-friendly-power-plus-2.fixture.json` 已验证《致命打击》对控制者所有场上友方单位施加本回合内战力 +2，且不会影响对手单位。
- `p2-preflight-play-grand-strategy-all-friendly-power-plus-5.fixture.json` 已验证《宏伟战略》对控制者所有场上友方单位施加本回合内战力 +5，且不会影响对手单位。
- `p2-preflight-play-back-to-back-two-friendly-power-plus-2.fixture.json` 已验证《背靠背》只能选择两名友方单位并分别施加本回合内战力 +2；直接测试另覆盖选择敌方单位会被 `INVALID_TARGET` 拒绝。
- `p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json` 已验证《力量之缚》支付 `optionalCosts = ["ECHO"]` 后，重复“两名友方单位本回合内战力 +1”效果一次，两个目标各累计 +2。
- `p2-preflight-play-danger-temperature-mechanical-power-plus-1.fixture.json` 已验证《危险温度》未支付混合资源回响时，只让控制者自己的“机械”属性单位本回合内战力 +1，不影响己方非机械单位或对手机械单位。
- `p2-preflight-play-smoke-bomb-power-floor-stack.fixture.json` 已验证《烟幕弹》施加本回合内战力 -4，且结果不得低于 1；实际应用的战力修正会用于后续清理恢复。
- `p2-preflight-smoke-bomb-power-floor-expires-end-turn.fixture.json` 已验证被“不得低于 1”截断后的负战力修正会在 `END_TURN` 特殊清理中移除，并恢复单位原战力。
- `p2-preflight-play-extortion-power-floor-draw-stack.fixture.json` 已验证《“敲”诈》尝试施加本回合内战力 -1 时若目标已是 1 战力，则实际应用 0 且不低于 1，并继续抽 1 张牌。
- `p2-preflight-play-perfect-finale-*.fixture.json` 已验证《完美谢幕》未支付有色/多次回响时的四个模式：抽 1 张牌、对战场单位造成 2 点伤害、对基地单位造成 3 点伤害、让战场单位本回合内战力 -4；回响重复且不能重复选择同一效果的完整路径暂缓。
- `p2-preflight-play-highlander-bloodline-recall-if-destroyed.fixture.json` 已验证《高原血统》给予一名友方单位本回合内摧毁替代效果；随后《复仇》摧毁该单位时改为记录 `UNIT_RECALLED_TO_BASE`，清除伤害、以休眠状态返回拥有者基地，且不写入本回合摧毁记忆。
- `p2-preflight-play-tactical-retreat-recall-if-destroyed.fixture.json` 已验证《战术撤退》给予一名友方单位本回合内“下次被摧毁”替代效果；随后《复仇》摧毁该单位时改为清除伤害、变为休眠、召回拥有者基地，且不写入本回合摧毁记忆。

尚未完成：

- `PLAY_CARD` 已通过最小 card behavior registry 支撑二十张官方伤害法术和《完美谢幕》两条模式化伤害路径、四张眩晕法术、一条回响重复眩晕路径、十三条 0 目标抽牌/手牌重置路径、七条主动摧毁单位路径、一个战力上限目标限制、一个进攻方单位目标限制、一个敌方进攻单位目标限制、一个友方后敌方的按序双目标限制、伤害达到战力后的致命摧毁、《惩戒》放逐替代路径和《高原血统》/《战术撤退》休眠召回基地替代路径、一个结算后抽牌路径、一个“目标被此法术摧毁则抽牌”的条件抽牌路径、一条目标控制者抽牌路径、一条按强力单位数量动态抽牌路径、一条眩晕后抽牌路径、一条已眩晕进攻敌方单位改为回手路径、一条所有玩家弃置手牌后各抽四张路径、一条本回合内正战力修正后抽牌路径、六条单纯本回合内正战力修正路径、两条所有友方单位本回合内正战力修正路径、一条按对象标签筛选的所有友方单位正战力修正路径、一条按己方单位属性标签种类动态计算的正战力修正路径、一条两名友方单位本回合内正战力修正路径、一条多目标回响重复正战力修正路径、一条回响重复正战力修正路径、一条分裂正负战力修正路径、五条授予本回合关键词并按战斗状态条件加战力路径（其中两条支付回响重复，一条以弃手牌作为回响费用，两条同时授予两个关键词，一条按防守状态加战力）、两条单纯负战力修正路径、一条回响重复负战力修正路径、一条不得低于 1 的负战力修正路径、一条不得低于 1 的负战力修正后抽牌路径、一条摧毁友方单位后按其当前战力临时增益另一名友方单位并抽牌路径、一条先正战力修正再按当前战力互伤路径、一条回响重复互伤路径、三条变为活跃状态路径、一条友方单位休眠后全战场单位伤害路径、一条敌方战斗中单位范围伤害路径、一条单位指示物打出到基地后抽牌路径、一条多枚单位指示物打出到基地路径、一条回响重复单位指示物打出到基地路径、一条可选装备摧毁被跳过后的抽牌路径、一条无已控制战场时的基础抽牌路径、结算抽牌燃尽分支、四条费用修正路径（本回合敌方单位被摧毁、控制单位最高战力、对手距胜利不超过 3 分、控制指定属性单位）、两条召出休眠符文/失败抽牌路径、两条先抽牌再召出休眠符文路径（一条包含摧毁友方强力单位作为强制额外费用）、一条友方单位回手后召出休眠符文路径、一条从对手废牌堆回收所选牌路径、一条己方废牌堆目标回手路径、一条全战场单位伤害路径、一条全场单位返回所属者手牌路径、两条战场单位返回所属者手牌路径、一条友方和敌方单位按序返回所属者手牌路径、一条战场单位移动到所属基地路径、一条最多两名友方战场单位移动到基地路径、一条不支付有色回响的 1-2 个不同战场单位伤害路径、《渊海狩咒》的控制正面朝下卡牌条件伤害、《雷霆突降》的进攻方目标条件伤害、`ANY_UNIT` / `BATTLEFIELD_UNIT` / `BASE_UNIT` / `FRIENDLY_UNIT` / `FRIENDLY_THEN_ENEMY_UNITS` / `FRIENDLY_THEN_ENEMY_BATTLEFIELD_UNITS` / `FRIENDLY_BATTLEFIELD_UNIT` / `FRIENDLY_HAND_CARD` / `FRIENDLY_GRAVEYARD_CARD` / `ATTACKING_UNIT` / `ENEMY_ATTACKING_UNIT` / `OPPONENT_GRAVEYARD_CARD` 目标范围分流、`1-2` 目标数量范围、两次/六次单位伤害选择、一条友方和敌方单位按自身战力互相造成伤害路径、一条任意两名单位按自身战力互相造成伤害路径、允许重复选择同一目标的多次执行法术、`PLAY_CARD.mode` 模式选择，以及 `optionalCosts = ["ECHO"]` 和 `optionalCosts = ["DISCARD_HAND_CARD:<objectId>"]` 的单次回响额外费用；richer expected diff 已开始覆盖出牌 fixture 的事件 tick/sequence/payload 局部字段、最终玩家区域、对象状态、对象标签和结算链；下一步逐批迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。
- 额外费用协议已扩展到 `optionalCosts = ["EXHAUST_FRIENDLY_UNIT:<objectId>"]`、`optionalCosts = ["DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"]` 和 `optionalCosts = ["DISCARD_HAND_CARD:<objectId>"]`，当前分别用于《冥想》让活跃友方单位休眠后额外抽牌、《牺牲》摧毁友方强力单位后抽牌并召休眠符文，以及《怒吼清算》弃置一张手牌后重复回响效果；非法目标会由服务端以 `INVALID_TARGET` 拒绝。
- 手牌对象目标已扩展到 `FRIENDLY_HAND_CARD`，当前用于《月神恩赐》的弃置后抽牌路径；源牌自身不能作为弃置目标。
- 己方废牌堆对象目标已扩展到 `FRIENDLY_GRAVEYARD_CARD`，当前用于《亡者复生》的废牌堆目标回手路径；完整“单位牌”类型校验待卡牌对象身份元数据补齐后细化。

## 3. P2 最小状态模型

在扩展 `MatchState` 时，优先加入以下服务端权威字段。玩家视角 snapshot 只投影允许看到的部分。

| 状态域 | 最小字段 | 用途 |
|---|---|---|
| 回合 | `turnNumber`, `turnPlayerId`, `phase`, `timingState` | 区分回合开始、主阶段、回合结束、普通/法术对决、开环/闭环。 |
| 行动权 | `priorityPlayerId`, `focusPlayerId`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` | 表达谁能自决行动，以及连续让过何时推进。 |
| 符文资源 | `runePools[playerId].mana`, `runePools[playerId].power` | 费用支付、抽牌阶段结束清空、回合结束清空。 |
| 区域 | `mainDeck`, `runeDeck`, `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone`, `championZone` | 后续打出、召出、抽牌、公开/私密/隐秘信息边界。 |
| 对象状态 | `cardObjects[objectId].damage`, `cardObjects[objectId].power`, `cardObjects[objectId].untilEndOfTurnPowerModifier`, `cardObjects[objectId].untilEndOfTurnEffects`, `cardObjects[objectId].isFaceDown`, `cardObjects[objectId].isAttacking`, `cardObjects[objectId].isDefending`, `cardObjects[objectId].isExhausted` | 回合结束特殊清理移除伤害、令本回合内效果和战力修正失效；`power` 先服务伤害达到战力后的摧毁判定；`isFaceDown` 先服务待命/隐藏信息条件效果的规则前提；`isAttacking` 先服务进攻方单位目标限制；`isDefending` 先服务防守方条件战力修正；`isExhausted` 先服务“变为活跃/休眠状态”和后续横置成本，后续扩展控制者、附属关系、战斗状态等完整对象字段。 |
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
| `UNIT_READIED` | 法术或技能让指定/友方单位变为活跃状态。 |
| `UNIT_EXHAUSTED` | 法术或技能让指定/友方单位变为休眠状态。 |
| `BATTLEFIELDS_SECURED` | 得分计算步骤据守战场。 |
| `RUNES_CALLED` | 召出阶段从符文牌堆召出符文；法术卡面写明“休眠的符文”时，同时在对象状态中标记 `isExhausted = true`。 |
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
| `DESTROY_REPLACEMENT_EFFECT_APPLIED` | 法术或技能为对象施加本回合内摧毁替代效果。 |
| `UNIT_DESTROYED` | 法术/技能效果或致命伤害摧毁单位，并将其从场上移入拥有者废牌堆。 |
| `UNIT_BANISHED` | 摧毁被替代效果改写时，将单位从场上移入拥有者放逐区。 |
| `UNIT_RECALLED_TO_BASE` | 摧毁被替代效果改写时，将单位以休眠状态召回拥有者基地。 |
| `UNIT_TOKEN_CREATED` | 法术或技能打出/生成单位指示物到指定区域。 |
| `CARDS_RECYCLED` | 法术或技能让所选卡牌从废牌堆回到拥有者主牌堆。 |
| `CARD_DISCARDED` | 法术或技能让玩家从手牌弃置所选牌，并将其移入该玩家废牌堆。 |
| `CARDS_DISCARDED` | 法术或技能让玩家批量弃置多张手牌，并将它们移入该玩家废牌堆。 |
| `CARD_RETURNED_TO_HAND` | 法术或技能让所选非场上卡牌从指定区域返回其拥有者手牌。 |
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
| `p2-play-punishment-damage-stack` | P1 打出官方法术《惩戒》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标造成 3 点伤害，并施加本回合若被摧毁则改为放逐的替代效果。 | `CATALOG` UNL-007/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-punishment-rejects-base-unit-target` | P1 尝试用《惩戒》指定基地中的单位 | 按官网卡面“战场上的一名单位”拒绝基地单位目标，返回 `INVALID_TARGET`。 | `CATALOG` UNL-007/219; `CORE-260330` p39-p42 rules 355-356 |
| `p2-play-abyssal-hunt-damage-stack` | P1 打出官方法术《渊海狩咒》，目标为战场上的单位，且 P1 未控制正面朝下卡牌 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 2 点伤害并进入废牌堆。 | `CATALOG` UNL-014/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-abyssal-hunt-face-down-damage-stack` | P1 打出官方法术《渊海狩咒》，目标为战场上的单位，且 P1 控制正面朝下战场牌 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 4 点伤害并进入废牌堆。 | `CATALOG` UNL-014/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-dancing-grenade-base-unit-damage` | P1 打出官方法术《曼舞手雷》，目标为基地单位，不进入再次打出分支 | 支付 2 点费用，目标可为基地中的单位；双方让过后对目标造成 2 点伤害并保留对象状态。 | `CATALOG` UNL-020/219; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-play-incinerate-damage-stack` | P1 打出官方法术《焚烧》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标造成 2 点伤害并进入废牌堆。 | `CATALOG` OGS·003/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-hextech-ray-damage-stack` | P1 打出官方法术《海克斯射线》，目标为战场上的单位 | 支付 1 点费用，卡牌入栈，双方让过后对目标造成 3 点伤害并进入废牌堆。 | `CATALOG` OGN·009/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-hextech-ray-damage-clears-end-turn` | P1 打出并结算《海克斯射线》后结束回合 | 真实卡牌造成的伤害被回合结束特殊清理移除，记录 `DAMAGE_REMOVED` 和常规清理检查，然后推进到 P2 回合开始。 | `CATALOG` OGN·009/298; `CORE-260330` p30-p33 rules 317-324; p39-p42 rules 355-356; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-preflight-play-thundering-drop-attacking-damage-stack` | P1 打出官方法术《雷霆突降》，目标为进攻方战场单位 | 支付 3 点费用，目标为进攻方单位时基础 2 点伤害改为 4 点；非进攻方目标基础 2 点伤害由直接测试覆盖。 | `CATALOG` SFD·017/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-piercing-light-two-target-damage-stack` | P1 打出官方法术《透体圣光》，不支付回响 | 支付 2 点费用，选择 1-2 个不同战场单位，双方让过后对每名目标各造成 2 点伤害；重复同一目标由直接拒绝测试覆盖，有色回响路径暂缓。 | `CATALOG` SFD·023/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-thundering-sky-cost-reduced-damage-stack` | P1 打出官方法术《霹天雳地》，并控制一名 3 战力单位 | 将法力费用从 8 减去控制单位最高战力 3，支付 5 点费用后入栈，双方让过后对战场单位造成 5 点伤害；未满足降低后费用有直接拒绝测试覆盖。 | `CATALOG` OGN·014/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-play-comet-strike-damage-stack` | P1 打出官方法术《彗星坠击》，目标为战场上的单位 | 支付 5 点费用，卡牌入栈，双方让过后对目标造成 6 点伤害并进入废牌堆。 | `CATALOG` OGN·085/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-final-spark-damage-stack` | P1 打出官方法术《终极闪光》，目标为一名战场单位 | 支付 8 点费用，卡牌入栈，双方让过后对一名单位造成 8 点伤害并进入废牌堆。 | `CATALOG` OGS·022/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-super-mega-death-rocket-damage-stack` | P1 打出官方专属法术《超究极死神飞弹！》，目标为一名战场单位 | 支付 4 点费用，卡牌入栈，双方让过后对一名单位造成 5 点伤害；废牌堆返回手牌触发能力暂缓。 | `CATALOG` OGN·252/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-center-stage-draw-stack` | P1 打出官方法术《台前作秀》，不支付回响 | 支付 2 点费用，0 目标卡牌入栈，双方让过后抽 1 张牌并进入废牌堆。 | `CATALOG` UNL-061/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-play-center-stage-echo-draw-stack` | P1 打出官方法术《台前作秀》，支付回响 | 支付基础 2 点和回响 2 点费用，0 目标卡牌入栈，双方让过后重复抽牌效果一次，共抽 2 张牌。 | `CATALOG` UNL-061/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-play-prophets-omen-draw-stack` | P1 打出官方法术《先知之兆》 | 支付 2 点费用，0 目标卡牌入栈，双方让过后抽 3 张牌并进入废牌堆。 | `CATALOG` SFD·087/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-play-might-makes-right-draw-powerful-units` | P1 打出官方法术《实力至上》，并控制两名强力单位 | 支付 2 点费用，0 目标卡牌入栈，双方让过后按当前控制的强力单位数量抽 2 张牌。 | `CATALOG` SFD·106/221; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-play-evolution-day-draw-stack` | P1 打出官方法术《进化日》 | 支付 6 点费用，0 目标卡牌入栈，双方让过后抽 4 张牌并进入废牌堆。 | `CATALOG` OGN·114/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p57 rule 413.4 |
| `p2-preflight-play-mobilize-call-rune` | P1 打出官方法术《动员》，符文牌堆有符文 | 支付 2 点费用，0 目标卡牌入栈，双方让过后从符文牌堆顶召出一枚休眠符文到基地，并记录 `cardObjects[P1-RUNE-001].isExhausted = true`。 | `CATALOG` OGN·134/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356 |
| `p2-preflight-play-mobilize-draws-if-rune-call-fails` | P1 打出官方法术《动员》，符文牌堆为空 | 无法召出符文时按卡面改为抽 1 张牌。 | `CATALOG` OGN·134/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-catalyst-of-aeons-call-two-runes` | P1 打出官方法术《万世催化石》，符文牌堆至少两枚符文 | 支付 4 点费用，0 目标卡牌入栈，双方让过后从符文牌堆顶召出两枚休眠符文到基地，并记录两个符文对象 `isExhausted = true`。 | `CATALOG` OGN·138/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356 |
| `p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short` | P1 打出官方法术《万世催化石》，符文牌堆仅一枚符文 | 尽可能召出 1 枚休眠符文并标记 `isExhausted = true`；因无法完整召出两枚符文，随后按卡面抽 1 张牌。 | `CATALOG` OGN·138/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune` | P1 打出官方法术《御衡守念》，P2 得分距离胜利得分为 3 | 费用从 3 减到 1，支付后入栈；双方让过后先抽 1 张牌，再召出一枚休眠符文并记录 `isExhausted = true`；对手距离胜利超过 3 分时由直接拒绝测试覆盖未减费。 | `CATALOG` OGN·047/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune` | P1 打出官方法术《牺牲》，并摧毁一名友方强力单位作为额外费用 | 支付 1 点费用，先摧毁战力至少 5 的友方单位并计入本回合摧毁记忆，再入栈；双方让过后先抽 2 张牌，再召出一枚休眠符文。 | `CATALOG` UNL-173/219; `CORE-260330` p14-p15 rules 142-143; p20 rules 164-167; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw` | P1 打出官方法术《断魂一扼》，目标为两名友方单位 | 支付 2 点费用，双方让过后按目标顺序先摧毁第一名友方单位，再让第二名友方单位本回合内获得等同于被摧毁单位当前战力的 +战力加成，最后抽 1 张牌。 | `CATALOG` SFD·163/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-play-vengeance-destroy-unit-stack` | P1 打出官方法术《复仇》，目标为战场上的一名单位 | 支付 4 点费用，卡牌入栈，双方让过后摧毁目标单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit` | P1 打出官方专属法术《涌泉之恨》，目标为 4 战力战场单位 | 支付 4 点费用，卡牌入栈，双方让过后摧毁目标单位；目标高于 3 战力，因此不进入符能再打出分支。 | `CATALOG` UNL-186/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-detonation-destroy-battlefield-unit-stack` | P1 打出官方法术《爆能术》，目标为战场上的一名单位 | 支付 6 点费用，卡牌入栈，双方让过后摧毁战场单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGS·012/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit` | P1 打出官方法术《狩魂》，目标为 3 战力战场单位 | 支付 2 点费用，目标必须是战场上不高于 3 战力的单位，双方让过后摧毁目标；4 战力目标有直接拒绝测试覆盖。 | `CATALOG` UNL-159/219; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-darkin-blade-destroy-target-controller-draw` | P1 打出官方法术《暗刃》，目标为 P2 战场单位 | 支付 2 点费用，双方让过后摧毁目标单位并让该单位控制者 P2 抽 2 张牌；待命路径暂缓。 | `CATALOG` OGN·213/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack` | P1 从手牌打出官方法术《流沙陷坑》，目标为战场上的一名单位 | 支付 5 点费用，卡牌入栈，双方让过后摧毁战场单位；从非手牌位置打出的减费路径暂缓。 | `CATALOG` SFD·164/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-ruination-destroy-all-units` | P1 打出官方法术《破败之咒》 | 支付 9 点费用，0 目标入栈，双方让过后摧毁所有当前场上单位；当前最小模型覆盖基地和战场区域中的单位。 | `CATALOG` UNL-180/219; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-undertow-return-all-units` | P1 打出官方法术《坠渊之流》 | 支付 8 点费用，0 目标入栈，双方让过后让所有当前场上单位返回所属者手牌；装备返回待装备对象模型落地后补。 | `CATALOG` SFD·147/221; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-reprimand-return-battlefield-unit` | P1 打出官方法术《责退》 | 支付 2 点费用，选择战场上的一名单位，双方让过后让目标返回所属者手牌，并移除场上对象状态。 | `CATALOG` OGN·172/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-gust-return-small-battlefield-unit` | P1 打出官方法术《罡风》 | 支付 1 点费用，目标必须是战场上不高于 3 战力的单位，双方让过后让目标返回所属者手牌；4 战力目标由直接拒绝测试覆盖。 | `CATALOG` OGN·169/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-reconsider-return-friendly-call-rune` | P1 打出官方法术《择日再战》 | 支付 1 点费用，目标必须是一名友方单位；双方让过后让目标返回所属者手牌，再让其拥有者召出一枚休眠符文并记录 `isExhausted = true`；敌方单位目标由直接测试拒绝。 | `CATALOG` OGN·104/298; `CORE-260330` p4-p8 rules 107-129; p20 rules 164-167; p39-p42 rules 355-356 |
| `p2-preflight-play-happenstance-return-friendly-and-enemy` | P1 打出官方法术《造化弄人》 | 支付 3 点费用，目标顺序必须是一名友方单位再一名敌方单位；双方让过后两个目标分别返回所属者手牌；目标顺序反转由直接拒绝测试覆盖。 | `CATALOG` UNL-128/219; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base` | P1 打出官方法术《战或逃》 | 支付 2 点费用，目标必须是战场上的单位，双方让过后让目标移动到所属者基地，并保留对象状态。 | `CATALOG` OGN·168/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base` | P1 打出官方法术《闪现》 | 支付 2 点费用，选择最多两名友方战场单位，双方让过后让这些目标移动到基地，并保留对象状态；敌方单位和友方基地单位目标由直接拒绝测试覆盖。 | `CATALOG` OGS·011/024; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-the-curtain-rises-echo-ready-unit` | P1 打出官方法术《大幕渐起》并支付回响 | 支付基础 2 点和回响 2 点费用，选择一名单位，加入结算链，双方让过后重复“变为活跃状态”效果一次。 | `CATALOG` UNL-009/219; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-beatdown-ready-unit` | P1 打出官方法术《痛殴》 | 支付 2 点费用，选择一名单位，加入结算链，双方让过后让目标变为活跃状态；消耗增益无视费用暂缓。 | `CATALOG` OGN·146/298; `CORE-260330` p39-p42 rules 355-356 |
| `p2-preflight-play-hunt-ready-all-friendly-units` | P1 打出官方专属法术《狩猎》 | 支付 1 点费用，0 目标加入结算链，双方让过后让 P1 所有场上单位变为活跃状态，且不影响对手单位。 | `CATALOG` SFD·204/221; `CORE-260330` p39-p42 rules 355-356 |
| `p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield` | P1 打出官方法术《过载能量》 | 支付 7 点费用，0 目标加入结算链，双方让过后先让 P1 所有场上单位变为休眠状态，然后对所有战场上的单位各造成 12 点伤害并执行致命伤害清理。 | `CATALOG` OGN·123/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-punishment-lethal-damage-banishes-unit` | P1 打出官方法术《惩戒》，目标为 3 战力战场单位 | 造成 3 点伤害后本应因伤害达到战力而被摧毁，但《惩戒》的本回合替代效果将目标改为放逐。 | `CATALOG` UNL-007/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p62-p63 rule 428 |
| `p2-preflight-punishment-banishes-if-destroyed-later` | P1 先用《惩戒》伤害 5 战力战场单位，再同回合用《复仇》摧毁该单位 | 《惩戒》建立的本回合替代效果持续到回合结束前；稍后的摧毁改为放逐，且不计入本回合摧毁记忆。 | `CATALOG` UNL-007/219, OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 323-324; p62-p63 rule 428 |
| `p2-preflight-shattered-fire-draws-after-lethal-damage` | P1 打出官方法术《碎裂之火》，目标为战场上的 3 战力单位 | 支付 4 点费用，造成 3 点伤害并摧毁目标后，按“如果该单位被此法术摧毁”条件抽 1 张牌。 | `CATALOG` OGN·005/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-shattered-fire-does-not-draw-without-destroy` | P1 打出官方法术《碎裂之火》，目标为战场上的 4 战力单位 | 造成 3 点伤害但目标存活，不触发卡面抽牌条件，目标保留 3 点伤害。 | `CATALOG` OGN·005/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-starfall-damages-two-units` | P1 打出官方法术《星落》，两次伤害分别选择战场单位和基地单位 | 支付 2 点费用，进行两次 3 点伤害选择；两个 3 战力目标均被摧毁并进入拥有者废牌堆。 | `CATALOG` OGN·029/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-starfall-can-damage-same-unit-twice` | P1 打出官方法术《星落》，两次伤害均选择同一战场单位 | 同一目标保留两次选择，累计受到 6 点伤害后被摧毁。 | `CATALOG` OGN·029/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-duel-mutual-power-damage` | P1 打出官方法术《决斗》，选择一名友方单位和一名敌方单位 | 支付 2 点费用，双方让过后两名目标以自身战力互相造成伤害；敌方 2 战力单位受到 4 点伤害后被摧毁。 | `CATALOG` OGN·128/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-gentleman-duel-power-then-mutual-damage` | P1 打出官方法术《绅士决斗》，选择一名友方单位和一名敌方单位 | 支付 6 点费用，先让友方目标本回合内战力 +3，再让两名目标以当前战力互相造成伤害；敌方 3 战力单位受到 5 点伤害后被摧毁。 | `CATALOG` OGS·008/024; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-marching-orders-echo-mutual-power-damage` | P1 打出官方法术《行军号令》，支付回响并选择一名友方单位和一名敌方战场单位 | 支付 3 点基础费用和 3 点回响额外费用，互伤效果重复一次；敌方战场单位累计受到 14 点伤害后被摧毁。 | `CATALOG` SFD·114/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428; p92-p105 keyword rules 800+ |
| `p2-preflight-play-clash-of-giants-mutual-power-damage` | P1 打出官方法术《巨人之战》，选择任意两名单位 | 支付 6 点费用，双方让过后基地单位和战场单位以自身战力互相造成伤害；敌方 3 战力单位受到 5 点伤害后被摧毁。 | `CATALOG` UNL-110/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-icathian-rain-can-hit-same-unit-six-times` | P1 打出官方法术《艾卡西亚暴雨》，六次伤害均选择同一战场单位 | 支付 7 点费用，同一目标保留六次选择，累计受到 12 点伤害后被摧毁。 | `CATALOG` OGN·248/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-blade-whirlwind-damage-all-battlefield-units` | P1 打出官方法术《剑刃飓风》 | 支付 1 点费用，0 目标入栈，双方让过后对所有战场上的单位各造成 1 点伤害，不分敌我；当前样例锁定未致命伤害路径。 | `CATALOG` OGN·133/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 323-324 |
| `p2-preflight-blade-whirlwind-lethal-damage-destroys-units` | P1 打出《剑刃飓风》，双方战场各有 1 战力单位 | 全战场 1 点伤害同时达到多个单位战力，结算后按致命伤害摧毁这些单位，并移入各自拥有者废牌堆。 | `CATALOG` OGN·133/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-cannon-barrage-damage-enemy-combat-units` | P1 打出官方法术《加农炮幕》 | 支付 2 点费用，0 目标入栈，双方让过后仅对战斗中的敌方单位各造成 2 点伤害；友方战斗中单位和敌方非战斗单位不受影响。 | `CATALOG` OGN·127/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 323-324 |
| `p2-preflight-play-production-surge-create-robot-draw` | P1 打出官方法术《产量激增》，未控制“机械”属性单位 | 支付 4 点费用，0 目标入栈，双方让过后打出一名 3 战力“机器人”单位指示物到 P1 基地，然后抽 1 张牌。 | `CATALOG` SFD·076/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-production-surge-reduced-by-mechanical` | P1 控制一名带 `tags = ["机械"]` 的单位后打出《产量激增》 | 费用从 4 减到 2；支付 2 点费用后仍打出 3 战力“机器人”到基地并抽 1 张牌。 | `CATALOG` SFD·076/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-common-cause-create-four-minions-base` | P1 打出官方法术《共同献身》，选择基地目的地 | `PLAY_CARD.mode = "BASE"`，支付 6 点费用，0 目标入栈，双方让过后打出四名 1 战力“随从”到 P1 基地；已锁定同一源牌的 token ID 顺序。 | `CATALOG` OGS·015/024; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base` | P1 打出官方法术《点沙成兵》并支付回响 | 支付基础 2 点和回响 2 点费用，0 目标入栈，双方让过后重复打出单位指示物效果，共打出两名 2 战力“黄沙士兵”到 P1 基地；已锁定同一源牌的 token ID 顺序。 | `CATALOG` SFD·031/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-stay-away-stun-draw-stack` | P1 从手牌打出官方法术《走开》 | 支付 3 点费用，眩晕一名单位，然后因从手牌打出而抽 1 张牌。 | `CATALOG` UNL-042/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-disposal-order-draw-mode` | P1 打出官方法术《处置命令》并选择抽牌模式 | `PLAY_CARD.mode = DRAW_1`，支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-disposal-order-recycle-opponent-graveyard` | P1 打出官方法术《处置命令》并选择对手废牌堆回收模式 | `PLAY_CARD.mode = RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3`，选择对手废牌堆中最多三张牌，双方让过后让其拥有者回收；多张回收到主牌堆底部时用 seed/rngCursor 生成可回放随机顺序。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-meditation-draw-stack` | P1 打出官方法术《冥想》，不支付休眠友方单位的额外费用 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` OGN·048/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-salvage-draw-no-equipment` | P1 打出官方法术《废物利用》，不选择装备目标 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌；可选摧毁装备路径暂缓至装备对象模型落地。 | `CATALOG` OGN·224/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields` | P1 打出官方法术《占山为王》，当前没有已控制战场 | 支付 3 点费用，0 目标入栈，双方让过后只抽基础 1 张牌；按控制战场数量额外抽牌暂缓至战场控制模型落地。 | `CATALOG` UNL-015/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-meditation-exhaust-friendly-extra-draw` | P1 打出官方法术《冥想》，让一名活跃友方单位休眠作为额外费用 | 支付 2 点费用，先记录友方单位变为休眠，再入栈；双方让过后共抽 2 张牌。 | `CATALOG` OGN·048/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-moonsilver-gift-discard-draw` | P1 打出官方法术《月神恩赐》，选择另一张友方手牌弃置 | 支付 3 点费用，选择友方手牌目标；双方让过后先弃置目标到废牌堆，再抽 2 张牌。 | `CATALOG` UNL-125/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-revive-return-graveyard-unit` | P1 打出官方法术《亡者复生》，选择己方废牌堆单位牌 | 支付 2 点费用，选择己方废牌堆目标；双方让过后将该牌返回手牌，并拒绝对手废牌堆目标。 | `CATALOG` OGN·170/298; `CORE-260330` p39-p42 rules 355-356; p4-p8 rules 107-129 |
| `p2-preflight-play-rewind-timeline-discard-hands-draw-four` | P1 打出官方法术《反转时间线》 | 支付 3 点费用，0 目标入栈；双方让过后每名玩家弃置当前手牌，然后各抽 4 张牌。 | `CATALOG` OGN·201/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-center-your-mind-draw-stack` | P1 打出官方法术《聚心凝神》，不满足等级减费 | 支付 5 点费用，0 目标入栈，双方让过后抽 2 张牌；等级 6/11 减费路径暂缓。 | `CATALOG` UNL-091/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-borrowed-history-draw-stack` | P1 从手牌打出官方法术《借鉴历史》 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌；待命/反应时机路径暂缓。 | `CATALOG` OGN·083/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-spoils-of-war-draw-stack` | P1 打出官方法术《以战养战》，本回合未摧毁敌方单位 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌。 | `CATALOG` OGN·144/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed` | P1 本回合先用《复仇》摧毁 P2 单位，再打出《以战养战》 | 记录敌方单位拥有者被摧毁的本回合记忆；《以战养战》费用从 4 降到 2，并抽 2 张牌。 | `CATALOG` OGN·229/298, OGN·144/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-practical-experience-power-plus-1` | P1 打出官方法术《实战经验》，未启用等级 6 升级 | 支付 1 点费用，加入结算链，双方让过后让一名单位本回合内战力 +1；等级 6 改为 +3 的路径暂缓。 | `CATALOG` UNL-031/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-dueling-stance-friendly-power-plus-1` | P1 打出官方法术《决斗架势》，目标为一名友方单位 | 支付 1 点费用，加入结算链，双方让过后让该友方单位本回合内战力 +1；“该处唯一控制单位”额外 +1 分支暂缓。 | `CATALOG` OGN·046/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-animal-friends-power-per-controlled-tag` | P1 打出官方法术《动物之友》，指定一名单位 | 支付 1 点费用，加入结算链，双方让过后按 P1 控制单位中“鸟类、猫科、犬形、魄罗”的不同标签种类数动态修正目标战力；样例中三种标签使目标本回合内战力 +3。 | `CATALOG` UNL-046/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-well-trained-power-draw-stack` | P1 打出官方法术《训练有素》，目标为一名单位 | 支付 2 点费用，加入结算链，双方让过后目标战力本回合内 +2，然后 P1 抽 1 张牌。 | `CATALOG` OGN·058/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p31-p33 rules 318-324 |
| `p2-preflight-well-trained-power-expires-end-turn` | 已存在本回合内战力 +2 修正的单位，随后 P1 结束回合 | `END_TURN` 特殊清理移除本回合内战力修正，记录 `POWER_MODIFIER_EXPIRED`，再推进到下一回合开始。 | `CATALOG` OGN·058/298; `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-preflight-play-savage-strength-echo-power-stack` | P1 打出官方法术《蛮荒之力》并支付回响 | 支付基础 2 点和回响 2 点费用，加入结算链，双方让过后重复“本回合内战力 +2”效果一次，目标共 +4。 | `CATALOG` SFD·034/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+; p31-p33 rules 318-324 |
| `p2-preflight-play-freeze-echo-power-minus-2` | P1 打出官方法术《封冻》并支付回响 | 支付基础 2 点和回响 2 点费用，加入结算链，双方让过后重复“本回合内战力 -2”效果一次，目标共 -4。 | `CATALOG` SFD·066/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+; p31-p33 rules 318-324 |
| `p2-preflight-play-distance-break-dance-split-power-modifiers` | P1 打出官方法术《距破之舞》 | 支付 1 点费用，选择两名不同单位，加入结算链，双方让过后让第一名目标本回合内战力 +2、第二名目标本回合内战力 -2。 | `CATALOG` SFD·196/221; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-cleave-overwhelm-attacking-power` | P1 打出官方法术《顺劈》，目标为进攻方单位 | 支付 1 点费用，双方让过后目标本回合内获得 `OVERWHELM_3` 标记；由于目标正在进攻，同时本回合内战力 +3。 | `CATALOG` OGN·004/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-blood-rush-echo-overwhelm-attacking-power` | P1 打出官方法术《血性冲刺》，目标为进攻方单位并支付回响 | 支付基础 1 点和回响 1 点费用，双方让过后重复授予 `OVERWHELM_2` 与进攻方本回合内战力 +2，目标累计 +4。 | `CATALOG` SFD·003/221; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power` | P1 打出官方法术《怒吼清算》，目标为进攻方单位并弃一张手牌支付回响 | 支付基础 4 点费用，额外弃置一张手牌，双方让过后重复授予 `OVERWHELM_4` 与进攻方本回合内战力 +4，目标累计 +8。 | `CATALOG` UNL-017/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-power-punch-overwhelm-roam-attacking-power` | P1 打出官方法术《强能冲拳》，目标为进攻方单位 | 支付 1 点费用，双方让过后目标本回合内获得 `OVERWHELM_2` 和 `ROAM` 标记；由于目标正在进攻，同时本回合内战力 +2。 | `CATALOG` UNL-010/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-parry-steadfast-barrier-defending-power` | P1 打出官方法术《格挡》，目标为防守方单位 | 支付 2 点费用，双方让过后目标本回合内获得 `STEADFAST_3` 和 `BARRIER` 标记；由于目标正在防守，同时本回合内战力 +3。 | `CATALOG` OGN·057/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-shoot-first-power-plus-5-stack` | P1 打出官方法术《先打再问》 | 支付 1 点费用，加入结算链，双方让过后让一名单位本回合内战力 +5。 | `CATALOG` SFD·097/221; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-tremendous-strength-power-plus-7` | P1 打出官方法术《洪荒巨力》 | 支付 4 点费用，加入结算链，双方让过后让一名单位本回合内战力 +7。 | `CATALOG` OGN·154/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-eclipse-power-minus-4` | P1 打出官方法术《月蚀》 | 支付 3 点费用，加入结算链，双方让过后让一名单位本回合内战力 -4；洞察路径暂缓。 | `CATALOG` UNL-063/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-moonfall-power-minus-10` | P1 打出官方法术《月光之殇》 | 支付 7 点费用，加入结算链，双方让过后让一名单位本回合内战力 -10。 | `CATALOG` UNL-066/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-glory-call-power-plus-3` | P1 打出官方法术《荣耀召唤》，不支付消耗增益额外费用 | 支付 3 点费用，加入结算链，双方让过后让一名单位本回合内战力 +3；消耗增益以无视费用的路径暂缓。 | `CATALOG` OGN·207/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-last-stand-friendly-power-plus-3` | P1 打出官方法术《视死如归》指定友方单位 | 支付 2 点费用，加入结算链，双方让过后让目标友方单位本回合内战力 +3；本回合赢得战斗时获得 2 经验的触发路径暂缓。 | `CATALOG` UNL-095/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-decisive-strike-all-friendly-power-plus-2` | P1 打出官方法术《致命打击》 | 支付 5 点费用，0 目标入栈，双方让过后让 P1 所有场上友方单位本回合内战力 +2；对手单位不受影响。 | `CATALOG` OGS·024/024; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-grand-strategy-all-friendly-power-plus-5` | P1 打出官方法术《宏伟战略》 | 支付 6 点费用，0 目标入栈，双方让过后让 P1 所有场上友方单位本回合内战力 +5；对手单位不受影响。 | `CATALOG` OGN·233/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-back-to-back-two-friendly-power-plus-2` | P1 打出官方法术《背靠背》 | 支付 3 点费用，选择两名友方单位，加入结算链，双方让过后分别让目标本回合内战力 +2；敌方单位目标由直接测试拒绝。 | `CATALOG` OGN·206/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-power-bind-echo-two-friendly-power-plus-1` | P1 打出官方法术《力量之缚》并支付回响 | 支付基础 2 点和回响 2 点费用，选择两名友方单位，加入结算链，双方让过后重复“两名友方单位本回合内战力 +1”效果一次，两个目标各 +2。 | `CATALOG` SFD·151/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+; p31-p33 rules 318-324 |
| `p2-preflight-play-danger-temperature-mechanical-power-plus-1` | P1 打出官方法术《危险温度》，不支付混合资源回响 | 支付 1 点费用，0 目标入栈，双方让过后只让 P1 场上带 `tags = ["机械"]` 的单位本回合内战力 +1；己方非机械和对手机械单位不受影响。 | `CATALOG` SFD·182/221; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-smoke-bomb-power-floor-stack` | P1 打出官方法术《烟幕弹》，目标为 3 战力单位 | 支付 2 点费用，加入结算链，双方让过后尝试让目标本回合内战力 -4；因不得低于 1，实际应用 -2，目标战力变为 1。 | `CATALOG` OGN·093/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-smoke-bomb-power-floor-expires-end-turn` | 已存在《烟幕弹》式被下限截断的负战力修正，随后 P1 结束回合 | `END_TURN` 特殊清理移除实际应用的负战力修正，记录 `POWER_MODIFIER_EXPIRED`，目标恢复原战力。 | `CATALOG` OGN·093/298; `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-preflight-play-extortion-power-floor-draw-stack` | P1 打出官方法术《“敲”诈》，目标为 1 战力单位 | 支付 1 点费用，加入结算链，双方让过后尝试让目标本回合内战力 -1；因不得低于 1，实际应用 0，随后 P1 抽 1 张牌。 | `CATALOG` OGN·095/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p31-p33 rules 318-324 |
| `p2-play-stellar-convergence-two-target-damage-stack` | P1 打出官方法术《星芒凝汇》，目标为一名战场单位和一名基地单位 | 支付 6 点费用，卡牌入栈，双方让过后对每名目标各造成 6 点伤害并进入废牌堆；一目标路径有直接测试覆盖。 | `CATALOG` OGN·105/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-rocket-barrage-base-unit-mode-stack` | P1 打出官方法术《火箭轰击》，选择基地单位伤害模式 | `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4`，支付 4 点费用，目标必须是基地中的单位，双方让过后造成 4 点伤害；`回响`和摧毁装备模式暂缓。 | `CATALOG` SFD·077/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-perfect-finale-draw-mode` | P1 打出官方法术《完美谢幕》，选择抽牌模式 | `PLAY_CARD.mode = DRAW_1`，支付 4 点费用，0 目标入栈，双方让过后抽 1 张牌；回响重复不同模式路径暂缓。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-perfect-finale-battlefield-damage-mode` | P1 打出官方法术《完美谢幕》，选择战场单位伤害模式 | `PLAY_CARD.mode = BATTLEFIELD_UNIT_DAMAGE_2`，支付 4 点费用，目标必须是战场单位，双方让过后造成 2 点伤害。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-perfect-finale-base-damage-mode` | P1 打出官方法术《完美谢幕》，选择基地单位伤害模式 | `PLAY_CARD.mode = BASE_UNIT_DAMAGE_3`，支付 4 点费用，目标必须是基地单位，双方让过后造成 3 点伤害。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-perfect-finale-battlefield-power-mode` | P1 打出官方法术《完美谢幕》，选择战场单位战力 -4 模式 | `PLAY_CARD.mode = BATTLEFIELD_UNIT_POWER_MINUS_4`，支付 4 点费用，目标必须是战场单位，双方让过后目标本回合内战力 -4。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-highlander-bloodline-recall-if-destroyed` | P1 打出官方法术《高原血统》指定友方战场单位，随后用《复仇》触发摧毁 | 《高原血统》结算后施加 `RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN`；后续摧毁被替代为 `UNIT_RECALLED_TO_BASE`，目标清除伤害、以休眠状态返回拥有者基地，不进入废牌堆且不写入本回合摧毁记忆。 | `CATALOG` OGS·020/024, OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428; p31-p33 rules 318-324 |
| `p2-preflight-play-tactical-retreat-recall-if-destroyed` | P1 打出官方法术《战术撤退》指定友方战场单位，随后用《复仇》触发摧毁 | 《战术撤退》结算后施加本回合内下次摧毁替代效果；后续摧毁被替代为 `UNIT_RECALLED_TO_BASE`，目标清除伤害、变为休眠并返回拥有者基地，不进入废牌堆且不写入本回合摧毁记忆。 | `CATALOG` UNL-175/219, OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428; p31-p33 rules 318-324 |
| `p2-play-void-seeker-damage-draw-stack` | P1 打出官方法术《虚空索敌》，目标为战场上的单位 | 支付 3 点费用，卡牌入栈，双方让过后对目标造成 4 点伤害，然后 P1 抽 1 张牌。 | `CATALOG` OGN·024/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-void-seeker-draw-burnout-stack` | P1 打出《虚空索敌》且 P1 主牌堆为空、废牌堆有 1 张牌 | 伤害结算后执行抽牌；抽牌先触发燃尽、P2 得 1 分、P1 回收废牌堆，再抽到回收牌。 | `CATALOG` OGN·024/298; `CORE-260330` p57 rule 413.4; p67 rule 431.2; p39-p42 rules 355-356 |
| `p2-play-rune-prison-stun-stack` | P1 打出官方法术《符文禁锢》，目标为战场上的单位 | 支付 2 点费用，卡牌入栈，双方让过后对目标施加 `STUNNED` 本回合内效果并进入废牌堆。 | `CATALOG` OGN·050/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-rune-prison-base-unit-stun-stack` | P1 打出官方法术《符文禁锢》，目标为基地中的单位 | “眩晕一名单位”允许选择基地单位；支付 2 点费用，卡牌入栈，双方让过后对目标施加 `STUNNED`。 | `CATALOG` OGN·050/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-rune-prison-stun-expires-end-turn` | P1 打出并结算《符文禁锢》后结束回合 | `STUNNED` 作为本回合内效果失效，记录 `UNTIL_END_OF_TURN_EXPIRED` 和常规清理检查，然后推进到 P2 回合开始。 | `CATALOG` OGN·050/298; `CORE-260330` p30-p33 rules 317-324; p39-p42 rules 355-356; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-preflight-play-kerplunk-stun-attacking-unit` | P1 打出官方法术《扑咚！》，目标为一名进攻方单位 | 不支付回响时支付 2 点费用，目标必须具有 `isAttacking = true`，双方让过后对目标施加 `STUNNED`；非进攻单位有直接拒绝测试覆盖。 | `CATALOG` SFD·040/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kerplunk-echo-stun-attacking-unit` | P1 打出官方法术《扑咚！》并支付回响，目标为一名进攻方单位 | 支付基础 2 点和回响 2 点费用，目标必须具有 `isAttacking = true`，双方让过后重复对目标施加 `STUNNED`。 | `CATALOG` SFD·040/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-existential-dread-echo-stun-then-return` | P1 打出官方法术《存在焦虑》并支付回响 | 支付基础 1 点和回响 2 点费用，目标必须是正在进攻的敌方单位；重复效果先施加 `STUNNED`，再因目标已被眩晕改为返回所属者手牌；友方进攻单位目标由直接拒绝测试覆盖。 | `CATALOG` UNL-134/219; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
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
29. 已完成：拆分 `ANY_UNIT` / `BATTLEFIELD_UNIT` 目标范围；《惩戒》已按官网卡面校正为战场单位目标，并保留基地单位目标拒绝测试。
30. 已完成：补《符文禁锢》指定基地单位 fixture，并补“战场单位”法术不可指定基地单位的拒绝测试。
31. 已完成：支持 0 目标 stack item 结算，并补《台前作秀》不支付回响的基础抽牌 fixture。
32. 已完成：支持 `1-2` 目标数量范围和多目标逐个结算，并补《星芒凝汇》双目标 fixture 与一目标直接测试。
33. 已完成：支持 `PLAY_CARD.mode` 模式选择和 `BASE_UNIT` 目标范围，并补《火箭轰击》基地单位伤害模式 fixture 与缺失模式拒绝测试。
34. 已完成：支持 `optionalCosts = ["ECHO"]` 和 `optionalCosts = ["DISCARD_HAND_CARD:<objectId>"]` 的单次回响额外费用和效果重复，并补《台前作秀》回响抽牌 fixture。
35. 已完成：迁移《终极闪光》《先知之兆》《进化日》三张低复杂度官方法术，覆盖任意单位 8 点伤害与 0 目标多张抽牌路径。
36. 已完成：`CompareExpected` 支持 `expected.events[].tick`、`sequence` 和 `payload` 局部匹配，并用《终极闪光》fixture 锁住费用、入栈目标和伤害 payload。
37. 已完成：实现主动摧毁单位的最小原语，并补《复仇》摧毁单位 fixture。
38. 已完成：为 `CardObjectState` 增加 `power`，实现伤害达到战力后的最小致命摧毁；《惩戒》致命伤害样例已按卡面替代效果改为放逐。
39. 已完成：支持“目标被此效果摧毁则抽牌”的最小条件抽牌，并补《碎裂之火》致命伤害后抽牌 fixture。
40. 已完成：补《碎裂之火》未摧毁目标不抽牌的反向 fixture。
41. 已完成：补《星落》两次单位伤害选择并摧毁多个目标的 fixture。
42. 已完成：支持多次执行法术允许重复选择同一目标，并补《星落》同一单位受两次伤害的 fixture。
43. 已完成：迁移《艾卡西亚暴雨》六次单位伤害路径，并补同一单位被六次命中的累计伤害 fixture。
44. 已完成：迁移《走开》从手牌打出的眩晕后抽牌基础路径；待命路径暂缓。
45. 已完成：迁移《处置命令》的 `DRAW_1` 模式和对手废牌堆最多三张回收模式。
46. 已完成：迁移《冥想》的基础抽牌路径。
47. 已完成：迁移《爆能术》的战场单位摧毁路径。
48. 已完成：迁移《以战养战》的全额支付抽 2 张基础路径。
49. 已完成：迁移《超究极死神飞弹！》的基础 5 点伤害路径；征服后从废牌堆返回手牌的触发能力暂缓。
50. 已完成：迁移《流沙陷坑》从手牌打出的全额费用摧毁战场单位路径；从非手牌位置打出的减费路径暂缓。
51. 已完成：实现最小本回合摧毁记忆，并补《以战养战》在本回合敌方单位被摧毁后的减费 fixture。
52. 已完成：补 `OPPONENT_GRAVEYARD_CARD` 目标范围、`CARDS_RECYCLED` 事件，以及 `seed/rngCursor` 的最小可回放随机顺序，服务《处置命令》回收模式和燃尽回收洗匀。
53. 已完成：迁移《借鉴历史》从手牌打出的抽 2 张基础路径；待命/反应时机路径暂缓。
54. 已完成：迁移《聚心凝神》全额支付抽 2 张基础路径；等级 6/11 减费路径暂缓。
55. 已完成：迁移《剑刃飓风》0 目标全战场单位各 1 点伤害路径；未致命 AoE 样例已锁定。
56. 已完成：补《剑刃飓风》全战场单位伤害达到战力后的多单位致命摧毁 fixture。
57. 已完成：迁移《狩魂》摧毁战场上不高于 3 战力单位路径，并补高战力目标拒绝测试。
58. 已完成：迁移《霹天雳地》按控制单位最高战力降低法力费用的 5 点伤害路径，并补未满足降低后费用的拒绝测试。
59. 已完成：迁移《暗刃》摧毁战场单位后由目标控制者抽 2 张的基础路径；待命路径暂缓。
60. 已完成：为对象状态补最小 `isAttacking`，迁移《扑咚！》不支付回响的进攻方单位眩晕路径，并补非进攻单位拒绝测试。
61. 已完成：迁移《雷霆突降》目标为进攻方单位时 2 点伤害改为 4 点的基础路径，并补非进攻方目标基础 2 点伤害测试；待命路径暂缓。
62. 已完成：迁移《透体圣光》不支付回响的 1-2 个不同战场单位各 2 点伤害路径，并补一目标允许、重复目标拒绝测试；有色回响路径暂缓。
63. 已完成：迁移《破败之咒》0 目标摧毁所有场上单位路径，补全局摧毁事件和基地/战场单位移入拥有者废牌堆 fixture。
64. 已完成：迁移《责退》战场单位返回所属者手牌路径，并补对象状态移除和手牌区域更新 fixture。
65. 已完成：迁移《罡风》不高于 3 战力战场单位返回所属者手牌路径，并补 4 战力目标拒绝测试。
66. 已完成：为对象状态补最小 `untilEndOfTurnPowerModifier`，迁移《训练有素》本回合内战力 +2 后抽 1 张路径。
67. 已完成：补《训练有素》式本回合内战力修正随 `END_TURN` 特殊清理失效的 fixture。
68. 已完成：迁移《蛮荒之力》支付回响后重复本回合内战力 +2 修正路径。
69. 已完成：迁移《烟幕弹》本回合内战力 -4、不得低于 1 的负战力修正路径，并记录实际应用 delta 以便清理恢复。
70. 已完成：迁移《“敲”诈》本回合内战力 -1、不得低于 1 后抽 1 张路径，覆盖下限截断不阻断后续指示。
71. 已完成：实现《惩戒》本回合“若被摧毁则改为放逐”的最小替代效果，覆盖立即致命伤害和同回合稍后被《复仇》摧毁两条路径。
72. 已完成：迁移《实力至上》按控制者强力单位数量动态抽牌路径，补最小 `CONTROLLER_POWERFUL_UNITS` 抽牌计数原语。
73. 已完成：迁移《涌泉之恨》高战力目标的基础摧毁战场单位路径；不高于 3 战力后的符能再打出分支暂缓。
74. 已完成：迁移《动员》召出一枚休眠符文路径，并补无法召出时抽 1 张牌的失败分支。
75. 已完成：迁移《万世催化石》召出两枚休眠符文路径，并补只能召出一枚时仍抽 1 张牌的不足分支。
76. 已完成：迁移《先打再问》本回合内战力 +5 路径，复用临时战力修正和清理原语。
77. 已完成：迁移《致命打击》所有友方单位本回合内战力 +2 路径，补己方基地/战场单位均受影响且对手单位不受影响的 fixture。
78. 已完成：迁移《背靠背》两名友方单位本回合内战力 +2 路径，补 `FRIENDLY_UNIT` 目标范围和敌方目标拒绝测试。
79. 已完成：迁移《力量之缚》支付回响后重复两名友方单位本回合内战力 +1 路径，覆盖多目标效果的回响重复。
80. 已完成：迁移《洪荒巨力》本回合内战力 +7 路径，复用单目标临时战力修正。
81. 已完成：迁移《宏伟战略》所有友方单位本回合内战力 +5 路径，复用全友方单位临时战力修正。
82. 已完成：迁移《封冻》支付回响后重复本回合内战力 -2 路径，覆盖回响重复负战力修正。
83. 已完成：迁移《月光之殇》本回合内战力 -10 路径，复用单目标临时负战力修正。
84. 已完成：迁移《实战经验》未启用等级 6 时的本回合内战力 +1 基础路径；等级 6 改为 +3 暂缓。
85. 已完成：迁移《荣耀召唤》不支付消耗增益额外费用时的本回合内战力 +3 基础路径；消耗增益无视费用暂缓。
86. 已完成：迁移《战或逃》战场单位移动到所属基地路径，并保留对象状态。
87. 已完成：迁移《距破之舞》两名不同单位分别 +2/-2 的分裂战力修正路径。
88. 已完成：迁移《闪现》最多两名友方战场单位移动到基地路径，并补敌方单位/友方基地单位目标拒绝测试。
89. 已完成：为对象状态补最小 `isExhausted`，迁移《大幕渐起》支付回响后重复变为活跃状态路径。
90. 已完成：迁移《痛殴》不支付消耗增益额外费用时让一名单位变为活跃状态路径；消耗增益无视费用暂缓。
91. 已完成：迁移《狩猎》让控制者所有场上单位变为活跃状态路径。
92. 已完成：迁移《过载能量》先让所有友方单位休眠、再对所有战场单位造成 12 点伤害的组合结算路径。
93. 已完成：将法术召出的“休眠符文”写入 `cardObjects.isExhausted = true`，并补《动员》《万世催化石》相关 fixture 期望。
94. 已完成：迁移《御衡守念》对手距胜利不超过 3 分时减费 2，并按卡面顺序先抽 1 张牌、再召出 1 枚休眠符文路径。
95. 已完成：迁移《择日再战》友方单位返回所属者手牌后，其拥有者召出 1 枚休眠符文路径。
96. 已完成：迁移《坠渊之流》让所有当前场上单位返回所属者手牌路径；装备返回待装备模型补齐后继续。
97. 已完成：迁移《造化弄人》一名友方单位和一名敌方单位按序返回所属者手牌路径，并补目标顺序反转拒绝测试。
98. 已完成：迁移《存在焦虑》正在进攻的敌方单位目标、回响重复先眩晕再因已眩晕改为回手路径，并补友方进攻单位拒绝测试。
99. 已完成：补《冥想》让活跃友方单位变为休眠状态作为额外费用后额外抽 1 张牌的路径，并补敌方单位不能支付该额外费用的拒绝测试。
100. 已完成：迁移《月神恩赐》弃置另一张友方手牌后抽 2 张牌的路径，补 `FRIENDLY_HAND_CARD` 目标范围和 `CARD_DISCARDED` 事件。
101. 已完成：迁移《亡者复生》从己方废牌堆选择目标返回手牌路径，补 `FRIENDLY_GRAVEYARD_CARD` 目标范围和 `CARD_RETURNED_TO_HAND` 事件。
102. 已完成：迁移《决斗》友方单位与敌方单位按自身战力互相造成伤害路径，复用 `FRIENDLY_THEN_ENEMY_UNITS` 目标顺序并补反向顺序拒绝测试。
103. 已完成：迁移《巨人之战》任意两名单位按自身战力互相造成伤害路径，复用 `ANY_UNIT` 目标范围覆盖基地/战场单位互伤。
104. 已完成：迁移《反转时间线》每名玩家弃置自己的所有手牌后各抽 4 张牌路径，补 `CARDS_DISCARDED` 批量弃置事件。
105. 已完成：迁移《绅士决斗》友方单位先获得本回合内战力 +3，再与敌方单位按当前战力互相造成伤害路径。
106. 已完成：迁移《行军号令》支付回响后重复友方单位与敌方战场单位按自身战力互相造成伤害路径，补 `FRIENDLY_THEN_ENEMY_BATTLEFIELD_UNITS` 目标范围。
107. 已完成：迁移《牺牲》摧毁友方强力单位作为强制额外费用后，先抽 2 张牌再召出 1 枚休眠符文路径，并补缺少额外费用/弱单位拒绝测试。
108. 已完成：迁移《断魂一扼》摧毁一名友方单位后，按其当前战力让另一名友方单位本回合内获得等量 +战力加成，并抽 1 张牌路径；补敌方第二目标拒绝测试。
109. 已完成：迁移《顺劈》本回合内授予 `OVERWHELM_3`，并在目标为进攻方时施加本回合内战力 +3；补非进攻方不加战力测试。
110. 已完成：迁移《血性冲刺》支付回响 1 后重复授予 `OVERWHELM_2`，并对进攻方目标重复施加本回合内战力 +2。
111. 已完成：迁移《强能冲拳》本回合内同时授予 `OVERWHELM_2` 与 `ROAM`，并在目标为进攻方时施加本回合内战力 +2；补非进攻方不加战力测试。
112. 已完成：迁移《格挡》本回合内同时授予 `STEADFAST_3` 与 `BARRIER`，并在目标为防守方时施加本回合内战力 +3；补非防守方不加战力测试。
113. 已完成：迁移《怒吼清算》弃置一张手牌作为回响额外费用后重复授予 `OVERWHELM_4`，并对进攻方目标重复施加本回合内战力 +4；补源牌不能作为弃牌费用测试。
114. 已完成：迁移《加农炮幕》对战斗中的所有敌方单位各造成 2 点伤害路径，补最小 `isAttacking` / `isDefending` 战斗状态筛选原语。
115. 已完成：迁移《产量激增》全额费用路径，打出 3 战力“机器人”单位指示物到控制者基地后抽 1 张牌。
116. 已完成：为对象状态补最小 `tags`，并覆盖《产量激增》控制“机械”属性单位时费用减少 2 的路径。
117. 已完成：迁移《共同献身》选择基地目的地后打出四名 1 战力“随从”到基地的路径，锁定多枚同源 token ID 顺序。
118. 已完成：迁移《危险温度》未支付混合资源回响时只让己方“机械”属性单位本回合内战力 +1 的路径。
119. 已完成：迁移《点沙成兵》支付回响后重复打出 2 战力“黄沙士兵”到基地的路径，覆盖单位指示物创建效果的回响重复。
120. 已完成：迁移《废物利用》不选择装备目标的合法分支，覆盖可选装备摧毁被跳过后继续抽 1 张牌的路径。
121. 已完成：迁移《占山为王》没有已控制战场时只抽基础 1 张牌的路径，战场控制额外抽牌暂缓到战场控制模型。
122. 已完成：迁移《完美谢幕》未支付有色/多次回响时的四个模式，覆盖抽牌、战场单位伤害、基地单位伤害和战场单位负战力修正。
123. 已完成：迁移《高原血统》本回合内摧毁替代效果，覆盖被摧毁时改为清除伤害、休眠召回拥有者基地，且不计入本回合摧毁记忆。
124. 已完成：迁移《战术撤退》本回合内下次摧毁替代效果，覆盖移除伤害、变为休眠并召回拥有者基地，且不计入本回合摧毁记忆。
125. 已完成：迁移《视死如归》友方单位本回合内战力 +3 基础路径；赢得战斗获得经验的触发路径暂缓到战斗胜负/经验模型。
126. 已完成：迁移《动物之友》按己方单位属性标签种类动态计算本回合内战力修正的路径。
127. 已完成：补《扑咚！》支付回响后重复眩晕进攻方单位的路径。
128. 已完成：迁移《曼舞手雷》不进入再次打出分支时对任意单位造成 2 点伤害的基础路径，覆盖基地单位伤害。
129. 已完成：迁移《决斗架势》友方单位本回合内战力 +1 基础路径；“该处唯一控制单位”额外 +1 分支暂缓到更细位置模型。
130. 已完成：迁移《月蚀》本回合内战力 -4 基础路径；洞察路径暂缓到牌堆顶部查看/回收选择模型。
131. 下一步：逐批迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。

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
