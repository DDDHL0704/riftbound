# P2 核心规则前置审查

更新时间：2026-05-01

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
- `p2-preflight-play-highway-robbery-enemy-unit-damage.fixture.json` 已验证官方法术 `OGN·033/298 巧取豪夺` 在目标控制者不选择让你抽两张牌时，对敌方单位造成 6 点伤害；目标范围覆盖敌方基地/战场单位，友方单位和离场敌方牌由直接拒绝测试覆盖。
- `p2-preflight-play-highway-robbery-target-controller-draw-choice.fixture.json` 已验证《巧取豪夺》目标控制者选择让来源控制者抽两张牌以避免伤害的分支，通过 `PLAY_CARD.mode = TARGET_CONTROLLER_CHOOSES_DRAW_2` 记录当前 2P preflight 选择。
- `p2-preflight-play-incinerate-damage-stack.fixture.json` 已验证官方法术 `OGS·003/024 焚烧` 的最小 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后对目标单位造成 2 点伤害并进入废牌堆。
- `p2-preflight-play-lotus-trap-doubles-next-damage.fixture.json` 已验证官方法术 `UNL-013/219 莲花陷阱` 先对目标施加 `DAMAGE_RECEIVED_DOUBLED_THIS_TURN`，随后同回合《焚烧》对该目标的 2 点伤害会按卡面翻倍为 4 点；本回合内效果清理由既有 `END_TURN` 特殊清理覆盖。
- `p2-preflight-play-counterstorm-prevent-next-damage.fixture.json` 已验证官方法术 `SFD·194/221 反击风暴` 先对目标施加 `PREVENT_NEXT_DAMAGE_THIS_TURN` 并抽 1 张牌，随后同回合《焚烧》对该目标的 2 点伤害被抵挡为 0，且抵挡效果被消耗。
- `p2-preflight-play-noxian-guillotine-next-damage-destroys.fixture.json` 已验证官方法术 `OGN·254/298 诺克萨斯断头台` 先对目标施加 `DESTROY_ON_NEXT_DAMAGE_THIS_TURN`，随后同回合《焚烧》的 2 点非致命伤害会触发摧毁；`鼓舞` 立即摧毁分支暂缓。
- `p2-preflight-play-imperial-decree-damage-destroys-unit.fixture.json` 已验证官方法术 `OGN·221/298 帝国谕令` 给当前场上单位施加本回合受伤即摧毁效果；随后同回合《焚烧》的 2 点非致命伤害会触发摧毁。后续新进场单位暂缓到全局持续效果模型。
- `p2-preflight-play-sprite-summon-create-sprite-base.fixture.json` 已验证官方法术 `OGN·094/298 精灵召唤` 在当前目的地受限代表路径下打出一名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。
- `p2-preflight-play-sprite-burst-create-two-sprites-base.fixture.json` 已验证官方法术 `UNL-069/219 精灵迸发` 在当前目的地受限代表路径下打出两名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。
- `p2-preflight-play-mirror-image-copy-ephemeral-base.fixture.json` 已验证官方专属法术 `UNL-200/219 镜花水月` 选择一名单位后，在当前最小对象模型中打出一名活跃“映像”到控制者基地，复制目标当前战力与对象标签，并追加 `瞬息` 标签；完整复制牌面和不触发打出效果暂缓。
- `p2-preflight-play-sinful-pleasure-discard-damage.fixture.json` 已验证官方法术 `OGN·008/298 罪恶快感` 选择一张友方手牌和一名战场单位，结算时先弃置手牌，再按该弃牌的 `manaCost` 对战场单位造成伤害；对手手牌目标由直接拒绝测试覆盖。
- `p2-preflight-play-hextech-ray-damage-stack.fixture.json` 已验证官方法术 `OGN·009/298 海克斯射线` 的最小 `PLAY_CARD` 通道：支付 1 点费用，加入结算链，双方让过后对目标单位造成 3 点伤害并进入废牌堆。
- `p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json` 已验证《海克斯射线》造成的真实伤害会在随后 `END_TURN` 的特殊清理中通过 `DAMAGE_REMOVED` 移除，并自动推进到下一回合开始。
- `p2-preflight-play-comet-strike-damage-stack.fixture.json` 已验证官方法术 `OGN·085/298 彗星坠击` 的最小 `PLAY_CARD` 通道：支付 5 点费用，加入结算链，双方让过后对目标单位造成 6 点伤害并进入废牌堆。
- `p2-preflight-play-final-spark-damage-stack.fixture.json` 已验证官方法术 `OGS·022/024 终极闪光` 的最小 `PLAY_CARD` 通道：支付 8 点费用，加入结算链，双方让过后对一名单位造成 8 点伤害并进入废牌堆。
- `p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json` 已验证官方专属法术 `OGN·252/298 超究极死神飞弹！` 的最小 `PLAY_CARD` 通道：支付 4 点费用，加入结算链，双方让过后对一名单位造成 5 点伤害；征服后从废牌堆返回手牌的触发能力暂缓。
- `p2-preflight-play-center-stage-draw-stack.fixture.json` 已验证官方法术 `UNL-061/219 台前作秀` 不支付回响时的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 1 张牌并进入废牌堆。
- `p2-preflight-play-center-stage-echo-draw-stack.fixture.json` 已验证《台前作秀》支付 `optionalCosts = ["ECHO"]` 时额外支付 2 点费用，并重复基础抽牌效果一次。
- `p2-preflight-play-prophets-omen-draw-stack.fixture.json` 已验证官方法术 `SFD·087/221 先知之兆` 的 0 目标 `PLAY_CARD` 通道：支付 2 点费用，加入结算链，双方让过后抽 3 张牌并进入废牌堆。
- `p2-preflight-play-portalpalooza-other-chooses-cards.fixture.json` 与 `p2-preflight-play-portalpalooza-other-chooses-runes.fixture.json` 已验证官方法术 `OGN·071/298 次元门狂欢` 在当前 2P preflight 中用 `PLAY_CARD.mode` 记录对手选择“卡牌”或“符文”，并分别让双方各抽 1 张牌或各召出 1 枚休眠符文；多人逐玩家选择暂缓。
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
- `p2-preflight-play-housecleaning-destroy-each-player-unit.fixture.json` 已验证官方法术 `OGN·209/298 清理门户` 支付 2 点费用，按顺序记录 P1 自选友方单位和 P2 自选敌方单位，双方让过后各摧毁一名自己的单位；反向顺序和两个友方目标由直接拒绝测试覆盖。
- `p2-preflight-play-kings-edict-destroy-enemy-unit.fixture.json` 已验证官方法术 `OGN·237/298 国王诏令` 在当前 2P preflight 中用一名敌方单位目标记录另一名玩家选择的非控制者单位，双方让过后摧毁该单位；友方单位目标由直接拒绝测试覆盖，多人逐玩家选择暂缓到 prompt/多席位选择模型。
- `p2-preflight-play-spirit-fire-destroy-total-power-four.fixture.json` 已验证官方法术 `OGN·256/298 妖异狐火` 选择战场上总战力不高于 4 的任意数量单位，双方让过后摧毁所选单位；总战力超过 4 的目标组合由直接拒绝测试覆盖，一处战场精确位置暂缓到多战场位置模型。
- `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json` 已验证官方法术 `SFD·164/221 流沙陷坑` 从手牌打出的全额费用路径：支付 5 点费用，选择战场上的一名单位，双方让过后摧毁目标单位并移入拥有者废牌堆；从手牌以外位置打出的减费路径暂缓。
- `p2-preflight-play-ruination-destroy-all-units.fixture.json` 已验证官方法术 `UNL-180/219 破败之咒` 的全局摧毁路径：支付 9 点费用，0 目标入栈，双方让过后摧毁当前所有场上单位，即各玩家基地和战场区域中的单位。
- `p2-preflight-play-undertow-return-all-units.fixture.json` 已验证官方法术 `SFD·147/221 坠渊之流` 的全局回手路径：支付 8 点费用，0 目标入栈，双方让过后让当前所有场上单位和装备返回所属者手牌。
- `p2-preflight-play-reprimand-return-battlefield-unit.fixture.json` 已验证官方法术 `OGN·172/298 责退` 的回手路径：支付 2 点费用，选择战场上的一名单位，双方让过后将其从战场移入所属者手牌，并移除场上对象状态。
- `p2-preflight-play-gust-return-small-battlefield-unit.fixture.json` 已验证官方法术 `OGN·169/298 罡风` 的小单位回手路径：支付 1 点费用，选择战场上不高于 3 战力的一名单位，双方让过后将其返回所属者手牌；4 战力目标由直接拒绝测试覆盖。
- `p2-preflight-play-reconsider-return-friendly-call-rune.fixture.json` 已验证官方法术 `OGN·104/298 择日再战` 的友方单位回手后召出休眠符文路径：支付 1 点费用，选择一名友方单位，双方让过后将其返回所属者手牌，再让其拥有者召出 1 枚休眠符文。
- `p2-preflight-play-happenstance-return-friendly-and-enemy.fixture.json` 已验证官方法术 `UNL-128/219 造化弄人` 的按序双目标回手路径：支付 3 点费用，第一目标必须是友方单位、第二目标必须是敌方单位，双方让过后分别返回所属者手牌；目标顺序反转由直接拒绝测试覆盖。
- `p2-preflight-play-hurricane-sweep-each-player-return-unit.fixture.json` 已验证官方法术 `OGN·187/298 飓风席卷` 在当前 2P preflight 中按座位选择顺序记录 P2 和 P1 各自选择的单位，双方让过后分别返回所属者手牌；重复目标由直接拒绝测试覆盖，逐玩家 prompt/多人选择暂缓。
- `p2-preflight-play-custodian-judgment-unit-to-deck-top.fixture.json` 与 `p2-preflight-play-custodian-judgment-unit-to-deck-bottom.fixture.json` 已验证官方法术 `UNL-204/219 持卫的裁决` 的敌方战场单位回到拥有者主牌堆路径：支付 2 点费用，选择敌方战场单位，当前 preflight 用 `PLAY_CARD.mode` 记录拥有者选择顶部或底部，双方让过后将目标放到拥有者主牌堆对应位置；缺失模式、友方单位和基地单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base.fixture.json` 已验证官方法术 `OGN·168/298 战或逃` 的战场单位移动到所属基地路径：支付 2 点费用，选择战场上的一名单位，双方让过后将其移动到所属者基地，并保留对象状态。
- `p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready.fixture.json` 已验证官方法术 `OGN·173/298 驭风而行` 的当前代表移动路径：支付 2 点费用，选择友方战场单位，双方让过后将其移动到所属者基地并变为活跃状态；完整目的地选择暂缓到多位置移动模型。
- `p2-preflight-play-charm-move-enemy-battlefield-unit-to-base.fixture.json` 已验证官方法术 `OGN·043/298 魅惑妖术` 的当前代表移动路径：支付 1 点费用，选择敌方战场单位，双方让过后将其移动到所属者基地；完整目的地选择暂缓到多位置移动模型。
- `p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base.fixture.json` 已验证官方法术 `UNL-038/219 升龙踢` 未启用等级 6 时的当前代表移动路径：支付 2 点费用，选择敌方战场单位，双方让过后将其移动到所属者基地；完整目的地选择和等级 6 眩晕暂缓。
- `p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base.fixture.json` 已验证官方法术 `UNL-124/219 隔绝` 的基础移动路径：支付 2 点费用，选择敌方战场单位，双方让过后将其移动到所属者基地；当前 fixture 锁定移动后无落单敌方单位残留的不抽牌分支，落单抽牌待多战场位置/孤立判定模型补齐。
- `p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base.fixture.json` 已验证官方法术 `OGS·011/024 闪现` 的最多两名友方战场单位移动到基地路径；敌方单位和友方基地单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json` 已验证官方法术 `SFD·043/221 禁军之墙` 从手牌打出时按当前己方战场单位数动态允许选择任意数量友方战场单位，并将所选单位移动到基地；敌方单位、友方基地单位和重复目标由直接拒绝测试覆盖，待命/迅捷窗口细节暂缓。
- `p2-preflight-play-playful-tentacles-move-total-power-eight.fixture.json` 已验证官方法术 `UNL-054/219 顽皮触手` 选择总战力不高于 8 的敌方战场单位，双方让过后将所选目标移动到所属者基地；总战力超过 8 的组合由直接拒绝测试覆盖，同一位置目的地和多控制者约束暂缓到多战场/多席位位置模型。
- `p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json` 已验证官方法术 `SFD·129/221 诱饵` 不支付回响时，选择一名敌方单位和另一名敌方单位，双方让过后将第一目标移动到第二目标所在位置；友方目标和重复目标由直接拒绝测试覆盖，回响与多战场精确位置暂缓。
- `p2-preflight-play-dragons-rage-move-then-mutual-damage.fixture.json` 已验证官方专属法术 `OGN·258/298 猛龙摆尾` 选择一名敌方单位和另一名敌方单位，双方让过后先将第一目标移动到第二目标所在位置，再让两名目标以自身当前战力互相造成伤害；友方目标和重复目标由直接拒绝测试覆盖，多战场精确目的地暂缓。
- `p2-preflight-play-ruthless-pursuit-move-friendly-unit-recall-mark.fixture.json` 已验证官方法术 `SFD·184/221 冷酷追击` 选择一名友方单位，双方让过后在当前目的地受限模型下将其移动到所属基地，并给予本回合征服战场后可召回的状态标记；敌方目标和非单位友方对象由直接拒绝测试覆盖，可选贴附武装和征服后触发结算暂缓。
- `p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json` 已验证官方法术 `UNL-009/219 大幕渐起` 支付 `optionalCosts = ["ECHO"]` 后重复“让一名单位变为活跃状态”效果一次。
- `p2-preflight-play-the-curtain-rises-ready-unit.fixture.json` 已验证《大幕渐起》未支付回响时只让目标单位变为活跃状态一次。
- `p2-preflight-play-beatdown-ready-unit.fixture.json` 已验证官方法术 `OGN·146/298 痛殴` 不支付消耗增益额外费用时，让一名单位变为活跃状态的基础路径。
- `p2-preflight-play-hunt-ready-all-friendly-units.fixture.json` 已验证官方专属法术 `SFD·204/221 狩猎` 让控制者所有场上单位变为活跃状态，且不会影响对手单位。
- `p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json` 已验证官方法术 `OGN·123/298 过载能量` 先让所有友方单位变为休眠状态，再对所有战场上的单位各造成 12 点伤害，并在结算后执行致命伤害清理。
- `p2-preflight-play-stellar-convergence-two-target-damage-stack.fixture.json` 已验证官方法术 `OGN·105/298 星芒凝汇` 的 `PLAY_CARD` 通道：支付 6 点费用，选择 1-2 名单位，双方让过后对每名目标各造成 6 点伤害。
- `p2-preflight-play-rocket-barrage-base-unit-mode-stack.fixture.json` 已验证官方法术 `SFD·077/221 火箭轰击` 的 `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4` 基础模式：支付 4 点费用，选择基地中的一名单位，双方让过后造成 4 点伤害。`回响`暂缓；缺失模式有直接拒绝测试覆盖。
- `p2-preflight-play-rocket-barrage-destroy-equipment-mode.fixture.json` 已验证《火箭轰击》的 `PLAY_CARD.mode = DESTROY_EQUIPMENT` 模式：支付 4 点费用，选择一件装备，双方让过后摧毁该装备并移入拥有者废牌堆；单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-emergency-recall-return-equipment.fixture.json` 已验证《紧急召回》支付 1 点费用，选择一件装备，双方让过后让该装备返回其拥有者手牌；单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-poro-snax-equipment-draw.fixture.json` 已验证官方装备 `SFD·046/221 魄罗佳肴` 从手牌打出后，双方让过结算时进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象，并抽 1 张牌；带目标打出由直接拒绝测试覆盖，自毁激活抽牌技能暂缓。
- `p2-preflight-play-shurelyas-requiem-equipment-ready-all.fixture.json` 已验证官方专属装备 `SFD·192/221 舒瑞娅的安魂曲` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象，并让控制者所有单位变为活跃状态；带目标打出由直接拒绝测试覆盖，唯我和装配技能暂缓。
- `p2-preflight-play-future-forge-equipment-create-minion.fixture.json` 已验证官方装备 `OGN·212/298 未来熔炉` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象，并打出一名带 `CARD_TYPE:UNIT` 标签的 1 战力“随从”到控制者基地；带目标打出由直接拒绝测试覆盖，摧毁装备回收废牌堆分支暂缓。
- `p2-preflight-play-treasure-golem-create-four-gold.fixture.json` 已验证官方单位 `SFD·174/221 宝藏魔像` 从手牌打出后进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，并打出四个休眠的 `CARD_TYPE:EQUIPMENT` “金币”装备指示物；带目标打出由直接拒绝测试覆盖，完整目的地选择暂缓。
- `p2-preflight-play-faithful-craftsman-create-minion.fixture.json` 已验证官方单位 `OGN·211/298 忠实的工坊主` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 1 战力“随从”单位指示物；带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。
- `p2-preflight-play-royal-guard-create-sand-soldier.fixture.json` 已验证官方单位 `SFD·157/221 皇家守卫` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 2 战力且带 `黄沙士兵` 标签的单位指示物；带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。
- `p2-preflight-play-blueflame-guardian-power-plus-8.fixture.json` 已验证官方单位 `OGN·082/298 苍炎守护者` 从手牌打出后进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，并让一名单位本回合内战力 +8；非单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-blastcone-sprout-power-minus-2-floor.fixture.json` 已验证官方单位 `OGN·097/298 爆裂球果仙灵` 从手牌普通打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让一名单位本回合内战力 -2 且不得低于 1；非单位目标由直接拒绝测试覆盖，待命/反应路径暂缓。
- `p2-preflight-play-prowling-hunter-create-warhawk.fixture.json` 已验证官方单位 `UNL-033/219 调皮猎手` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 1 战力且带 `法盾` 标签的“战鹰”单位指示物；带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。
- `p2-preflight-play-apprentice-engineer-return-graveyard-equipment.fixture.json` 已验证官方单位 `SFD·061/221 见习工程师` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让己方废牌堆一件装备返回手牌；非装备废牌堆目标由直接拒绝测试覆盖。
- `p2-preflight-play-darkened-lurker-discard-draw.fixture.json` 已验证官方单位 `UNL-123/219 永黯潜伏者` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，弃置另一张手牌并抽 1 张牌；以源牌自身作为弃置目标由直接拒绝测试覆盖。
- `p2-preflight-play-shepherd-dog-return-graveyard-unit.fixture.json` 已验证官方单位 `OGN·165/298 牧灵犬` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让己方废牌堆一张单位牌返回手牌；非单位废牌堆目标由直接拒绝测试覆盖。
- `p2-preflight-play-annie-return-graveyard-spell.fixture.json` 已验证官方单位 `OGS·010/024 安妮` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让己方废牌堆一张法术牌返回手牌；非法术废牌堆目标由直接拒绝测试覆盖。
- `p2-preflight-play-scuttle-crab-draw.fixture.json` 已验证官方单位 `UNL-053/219 迅捷蟹` 从手牌打出后进入控制者基地成为 0 战力 `CARD_TYPE:UNIT` 单位对象，并抽 1 张牌；带目标打出由直接拒绝测试覆盖，绝念分支暂缓。
- `p2-preflight-play-yordle-instructor-draw.fixture.json` 已验证官方单位 `OGN·087/298 约德尔教官` 从手牌打出后进入控制者基地成为 2 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并抽 1 张牌；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sprite-mother-create-sprite.fixture.json` 已验证官方单位 `OGN·106/298 精灵之母` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 3 战力且带 `瞬息` 标签的“精灵”单位指示物；带目标打出由直接拒绝测试覆盖，精确“此处”和瞬息到期暂缓。
- `p2-preflight-play-megashark-cannon-damage-enemy-battlefield.fixture.json` 已验证官方单位 `OGN·092/298 怒海大鲨炮` 从手牌打出后进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并对敌方战场单位造成 6 点非致命伤害；友方目标由直接拒绝测试覆盖。
- `p2-preflight-play-quicksand-mage-destroy-small-enemy.fixture.json` 已验证官方单位 `SFD·158/221 流沙术士` 从手牌打出后进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并摧毁不高于 3 战力的敌方单位；4 战力目标由直接拒绝测试覆盖。
- `p2-preflight-play-zaun-bodyguard-return-battlefield-unit.fixture.json` 已验证官方单位 `OGN·188/298 祖安保镖` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让一名战场单位返回其拥有者手牌；基地目标由直接拒绝测试覆盖。
- `p2-preflight-play-dragon-cavalry-destroy-enemy-unit.fixture.json` 已验证官方单位 `OGN·234/298 龙骑兵` 从手牌打出后进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并摧毁一名敌方单位；友方目标由直接拒绝测试覆盖。
- `p2-preflight-play-first-mate-ready-unit.fixture.json` 已验证官方单位 `OGN·132/298 大副` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让另一名单位变为活跃状态；非单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-arena-rookie-grant-boon.fixture.json` 已验证官方单位 `OGN·136/298 竞技场新人` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并给予另一名友方单位 `增益` 标签和永久 +1 战力；敌方目标由直接拒绝测试覆盖。
- `p2-preflight-play-sword-vagrant-destroy-equipment.fixture.json` 已验证官方单位 `SFD·032/221 斩剑浪客` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并可选摧毁一件装备；不选择装备可正常入栈，单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-sun-shieldguard-stun-unit.fixture.json` 已验证官方单位 `OGN·051/298 烈阳盾卫` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并眩晕目标单位；非单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-thunderclaw-ursine-call-rune.fixture.json` 已验证官方单位 `OGN·137/298 雷爪氏族熊人` 从手牌打出后进入控制者基地成为 6 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并召出一枚休眠符文；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-kinkou-monk-grant-two-boons.fixture.json` 已验证官方单位 `OGN·141/298 均衡僧侣` 从手牌打出后进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并给予两名友方单位 `增益` 标签和永久 +1 战力；敌方目标由直接拒绝测试覆盖。
- `p2-preflight-play-gloomy-apothecary-return-friendly-battlefield.fixture.json` 已验证官方单位 `UNL-021/219 阴森药剂师` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并可选让友方战场单位返回手牌；不选择目标可正常入栈，敌方战场目标由直接拒绝测试覆盖，伏击路径暂缓。
- `p2-preflight-play-windsong-wing-return-small-battlefield.fixture.json` 已验证官方单位 `SFD·138/221 吟风翼` 从手牌打出后进入控制者基地成为 1 战力、带 `待命` 标签的 `CARD_TYPE:UNIT` 单位对象，并可选让不高于 3 战力的战场单位返回手牌；不选择目标可正常入栈，4 战力目标由直接拒绝测试覆盖，待命/反应路径暂缓。
- `p2-preflight-play-hexcore-disruptor-roam-unit.fixture.json` 已验证官方单位 `SFD·007/221 晶能阻断器` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让目标单位本回合内获得 `ROAM`；装备目标由直接拒绝测试覆盖。
- `p2-preflight-play-kadregrin-draw-powerful-units.fixture.json` 已验证官方单位 `OGN·038/298 邪焰巨龙 卡德雷格林` 从手牌打出后进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，并按控制者场上强力单位数量抽牌；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-frenzied-raider-move-battlefield-unit-to-base.fixture.json` 已验证官方单位 `OGN·191/298 疯狂海寇` 从手牌打出后进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并将战场单位移动到其所属基地；基地目标由直接拒绝测试覆盖。
- `p2-preflight-play-abyssal-behemoth-return-friendly-and-enemy.fixture.json` 已验证官方单位 `SFD·132/221 海渊巨兽` 从手牌打出后进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，并按顺序让另一名友方单位和一名敌方单位返回所属者手牌；目标顺序反转由直接拒绝测试覆盖。
- `p2-preflight-play-bubblebot-ready-friendly-mechanical.fixture.json` 已验证官方单位 `SFD·062/221 泡泡机` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让另一名友方“机械”属性单位变为活跃状态；非机械单位由直接拒绝测试覆盖。
- `p2-preflight-play-sprite-queen-create-sprite.fixture.json` 已验证官方单位 `UNL-084/219 精灵女王` 从手牌打出后进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 3 战力且带 `瞬息` 标签的“精灵”单位指示物；带目标打出由直接拒绝测试覆盖，开始阶段重复创建和瞬息到期暂缓。
- `p2-preflight-play-faerie-dragon-grant-four-boons.fixture.json` 已验证官方单位 `SFD·101/221 仙灵龙` 从手牌打出后进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，并给予最多四名友方单位 `增益` 标签和永久 +1 战力；不选择目标可正常入栈，敌方目标由直接拒绝测试覆盖，消耗增益打出金币分支暂缓。
- `p2-preflight-play-ezreal-discard-draw-two.fixture.json` / `p2-preflight-play-ezreal-alt-a-discard-draw-two.fixture.json` 已验证官方单位 `SFD·149/221` / `SFD·149a/221` 《伊泽瑞尔》从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，弃置另一张手牌并抽 2 张牌；以源牌自身作为弃置目标由直接拒绝测试覆盖，其他卡牌的可选额外费用减免暂缓。
- `p2-preflight-play-solari-leader-stun-enemy-unit.fixture.json` 已验证官方单位 `OGN·225/298 烈阳首领` 从手牌打出后进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并让未眩晕敌方单位获得本回合内 `STUNNED`。
- `p2-preflight-play-solari-leader-destroy-stunned-enemy.fixture.json` 已验证官方单位 `OGN·225/298 烈阳首领` 对已眩晕敌方单位的分支：结算后源牌入场，目标改为被摧毁并进入拥有者废牌堆；友方单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-buhru-captain-draw-mode.fixture.json` 已验证官方单位 `SFD·091/221 芭茹队长` 从手牌打出后进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并通过 `DRAW_1` 模式抽 1 张牌；缺失模式由直接拒绝测试覆盖，自身增益分支暂缓。
- `p2-preflight-play-chempunk-tough-discard-hand.fixture.json` 已验证官方单位 `OGN·003/298 炼金太保` 从手牌打出后进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并弃置另一张手牌；以源牌自身作为弃置目标由直接拒绝测试覆盖，强攻战斗关键词暂缓。
- `p2-preflight-play-jinx-discard-two-hand.fixture.json` / `p2-preflight-play-jinx-alt-a-discard-two-hand.fixture.json` 已验证官方单位 `OGN·030/298` / `OGN·030a/298` 《金克丝》从手牌打出后进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并弃置另外两张手牌；目标包含源牌自身由直接拒绝测试覆盖，急速和强攻战斗关键词暂缓。
- `p2-preflight-play-blast-crew-apprentice-no-optional-damage.fixture.json` / `p2-preflight-play-frostcoat-cub-no-optional-power-minus-two.fixture.json` / `p2-preflight-play-ship-monkey-no-optional-boon.fixture.json` 已验证官方单位 `SFD·013/221` 《爆破队学员》、`SFD·067/221` 《霜衣幼崽》和 `SFD·098/221` 《船猿》不支付可选额外费用时的单位源牌入场路径；有色/额外费用和对应伤害、战力修正、自身增益分支暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-pyke-no-optional-ready-power.fixture.json` / `p2-preflight-play-pyke-alt-a-no-optional-ready-power.fixture.json` / `p2-preflight-play-tiny-guardian-no-optional-draw.fixture.json` 已验证官方单位 `UNL-028/219` / `UNL-028a/219` 《派克》和 `OGN·044/298` 《小小守护者》不支付可选额外费用时的单位源牌入场路径；《派克》记录 `待命` 与 `游走` 标签，红色额外费用的活跃/+2 分支和绿色额外费用的抽牌分支暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-blazing-drake-no-optional-haste.fixture.json` / `p2-preflight-play-legion-rearguard-no-optional-haste.fixture.json` / `p2-preflight-play-baby-shark-no-optional-haste.fixture.json` 已验证官方单位 `OGN·001/298` 《灼焰飞龙》、`OGN·010/298` 《军团后卫》和 `UNL-006/219` 《小鲨鱼》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场和强攻战斗修正暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-reksai-no-optional-haste.fixture.json` / `p2-preflight-play-reksai-alt-a-no-optional-haste.fixture.json` / `p2-preflight-play-kaisa-no-optional-haste.fixture.json` / `p2-preflight-play-kaisa-alt-a-no-optional-haste.fixture.json` 已验证官方英雄单位 `SFD·029/221` / `SFD·029a/221` 《雷克塞》和 `OGN·039/298` / `OGN·039a/298` 《卡莎》不支付 `急速` 可选额外费用时的单位源牌入场路径；急速活跃进场、强攻战斗修正、非手牌打出授予急速和征服抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-rengar-no-optional-haste.fixture.json` / `p2-preflight-play-rengar-alt-a-no-optional-haste.fixture.json` / `p2-preflight-play-nilah-no-optional-haste.fixture.json` 已验证官方英雄单位 `UNL-024/219` / `UNL-024a/219` 《雷恩加尔》和 `UNL-115/219` 《尼菈》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、强攻战斗修正、法盾目标税、游走移动和移动触发暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-miss-fortune-no-optional-haste.fixture.json` / `p2-preflight-play-miss-fortune-alt-a-no-optional-haste.fixture.json` / `p2-preflight-play-sivir-no-optional-haste.fixture.json` / `p2-preflight-play-sivir-alt-a-no-optional-haste.fixture.json` 已验证官方英雄单位 `OGN·162/298` / `OGN·162a/298` 《厄运小姐》和 `SFD·143/221` / `SFD·143a/221` 《希维尔》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、游走移动、移动触发和万能符能支付条件战力/关键词修正暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-lillia-no-optional-haste.fixture.json` / `p2-preflight-play-lillia-alt-a-no-optional-haste.fixture.json` / `p2-preflight-play-azir-no-optional-haste.fixture.json` / `p2-preflight-play-azir-alt-a-no-optional-haste.fixture.json` 已验证官方英雄单位 `UNL-082/219` / `UNL-082a/219` 《莉莉娅》和 `SFD·177/221` / `SFD·177a/221` 《阿兹尔》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、移动/进攻触发、精灵指示物和指示物移动暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-mr-root-no-optional-haste.fixture.json` / `p2-preflight-play-mech-maniac-no-optional-haste.fixture.json` / `p2-preflight-play-xersai-fish-no-optional-haste.fixture.json` / `p2-preflight-play-karina-veraze-no-optional-haste.fixture.json` 已验证官方单位 `UNL-127/219` 《树根先生》、`SFD·068/221` 《机械迷》、`SFD·103/221` 《琢珥鱼》和 `SFD·179/221` 《卡银娜·薇蕊泽》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、移动触发、经验、武装静态修正、费用减少和随从指示物创建路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-crimson-signet-treant-no-optional-haste.fixture.json` / `p2-preflight-play-crimson-signet-treant-alt-a-no-optional-haste.fixture.json` / `p2-preflight-play-tasty-faerie-no-optional-haste.fixture.json` / `p2-preflight-play-ekko-no-optional-haste.fixture.json` 已验证官方单位 `UNL-029/219` / `UNL-029a/219` 《绯红印记树怪》、`OGN·075/298` 《美味仙灵》和英雄单位 `OGN·110/298` 《艾克》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、征服、增益、绝念、回收和符文活跃化路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-armed-assaulter-no-optional-haste.fixture.json` / `p2-preflight-play-ancient-berserker-no-optional-haste.fixture.json` / `p2-preflight-play-kraken-hunter-no-optional-haste.fixture.json` / `p2-preflight-play-lee-sin-no-optional-haste.fixture.json` / `p2-preflight-play-lee-sin-alt-a-no-optional-haste.fixture.json` 已验证官方单位 `SFD·002/221` 《武装强袭者》、`SFD·131/221` 《远古战狂》、`OGN·150/298` 《海妖猎手》和英雄单位 `OGN·151/298` / `OGN·151a/298` 《李青》不支付 `急速` 可选额外费用时的单位源牌入场路径；属性/关键词标签记录为对象标签，急速活跃进场、百炼装配、动态强攻、消耗增益减费和战场静态战力修正路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3.fixture.json` 已验证官方单位 `OGN·116/298` 《千尾监视者》不支付 `急速` 可选额外费用时的单位源牌入场路径，并在结算后让所有敌方单位本回合内战力 -3、不得低于 1；fixture 覆盖敌方基地/战场单位、下限截断和友方单位不受影响，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-plucky-poro-keyword-unit.fixture.json` / `p2-preflight-play-mighty-poro-keyword-unit.fixture.json` / `p2-preflight-play-assault-poro-keyword-unit.fixture.json` / `p2-preflight-play-fierce-first-mate-keyword-unit.fixture.json` / `p2-preflight-play-zephyr-sage-keyword-unit.fixture.json` 已验证官方单位 `OGN·013/298` 《呸呸魄罗》、`OGN·052/298` 《强强魄罗》、`OGN·210/298` 《莽莽魄罗》、`OGN·215/298` 《躁烈的副官》和 `OGS·005/024` 《和风贤者》的单位源牌入场路径；属性/关键词标签记录为对象标签，法盾目标税、坚守/强攻战斗修正暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-pakaa-cub-keyword-unit.fixture.json` / `p2-preflight-play-navori-scout-keyword-unit.fixture.json` / `p2-preflight-play-laurent-swordsman-keyword-unit.fixture.json` / `p2-preflight-play-gluttonous-toadfrog-keyword-unit.fixture.json` / `p2-preflight-play-sentinel-adept-no-optional-assemble.fixture.json` 已验证官方单位 `OGN·135/298` 《帕卡幼崽》、`SFD·037/221` 《纳沃利侦察兵》、`SFD·156/221` 《劳伦特剑使》、`UNL-100/219` 《贪食魔沼蛙》和 `SFD·008/221` 《哨兵好手》的单位源牌入场路径；属性/关键词标签记录为对象标签，待命、法盾、强攻、狩猎和百炼装配路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-battle-chef-no-optional-assemble.fixture.json` / `p2-preflight-play-stout-poro-no-optional-assemble.fixture.json` / `p2-preflight-play-master-bingwen-no-optional-assemble.fixture.json` 已验证官方单位 `SFD·092/221` 《战斗厨神》、`SFD·099/221` 《壮壮魄罗》和 `SFD·127/221` 《炳文大师》不选择百炼装配时的单位源牌入场路径；关键词/属性标签记录为对象标签，百炼装配路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-unl-plucky-poro-keyword-unit.fixture.json` / `p2-preflight-play-unl-stout-poro-keyword-unit.fixture.json` / `p2-preflight-play-unl-assault-poro-keyword-unit.fixture.json` 已验证官方单位 `UNL-220/219` 《呸呸魄罗》、`UNL-223/219` 《壮壮魄罗》和 `UNL-225/219` 《莽莽魄罗》的单位源牌入场路径；属性/关键词标签记录为对象标签，法盾、百炼装配和强攻战斗修正暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-mutant-kitten-keyword-unit.fixture.json` / `p2-preflight-play-burly-brawler-keyword-unit.fixture.json` / `p2-preflight-play-laurent-bladeguard-keyword-unit.fixture.json` / `p2-preflight-play-garen-keyword-unit.fixture.json` / `p2-preflight-play-solari-guard-keyword-unit.fixture.json` 已验证官方单位/英雄单位 `UNL-036/219` 《变异猫咪》、`UNL-099/219` 《魁梧斗士》、`SFD·096/221` 《劳伦特护刃者》、`OGS·007/024` 《盖伦》和 `OGN·054/298` 《日耀卫队》的单位源牌入场路径；属性/关键词标签记录为对象标签，坚守/强攻战斗修正、壁垒承伤顺序和游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-aerie-head-fan-keyword-unit.fixture.json` / `p2-preflight-play-vex-keyword-unit.fixture.json` / `p2-preflight-play-wildclaw-beastmaster-keyword-unit.fixture.json` / `p2-preflight-play-huge-yordle-keyword-unit.fixture.json` / `p2-preflight-play-tianna-crownguard-keyword-unit.fixture.json` 已验证官方单位/英雄单位 `UNL-041/219` 《艾蕾，头号拥趸》、`UNL-055/219` 《薇古丝》、`UNL-057/219` 《野爪兽王》、`SFD·055/221` 《超大型约德尔人》和 `SFD·060/221` 《缇亚娜·冕卫》的单位源牌入场路径；属性/关键词标签记录为对象标签，法盾静态授予、眩晕后移动、低战力目标限制、费用减少和阻止得分静态效果暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-jhin-spellshield-roam-keyword-unit.fixture.json` / `p2-preflight-play-jhin-alt-a-spellshield-roam-keyword-unit.fixture.json` / `p2-preflight-play-vi-keyword-unit.fixture.json` / `p2-preflight-play-vi-alt-a-keyword-unit.fixture.json` / `p2-preflight-play-leblanc-keyword-unit.fixture.json` 已验证官方英雄单位 `UNL-022/219` / `UNL-022a/219` 《烬》、`UNL-030/219` / `UNL-030a/219` 《蔚》和 `UNL-090/219` 《乐芙兰》的单位源牌入场路径；关键词标签记录为对象标签，移动获得资源、支付资源战力翻倍和瞬息静态免触发路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-enthusiastic-announcer-keyword-unit.fixture.json` / `p2-preflight-play-moss-stepper-keyword-unit.fixture.json` / `p2-preflight-play-trevor-duttonel-keyword-unit.fixture.json` / `p2-preflight-play-windrunner-fox-keyword-unit.fixture.json` / `p2-preflight-play-crystalhand-hunter-keyword-unit.fixture.json` 已验证官方单位 `UNL-043/219` 《热情的播报员》、`UNL-047/219` 《踏苔蜥》、`UNL-048/219` 《特雷弗·达顿尔》、`UNL-075/219` 《风行狐》和 `UNL-094/219` 《晶手猎人》的单位源牌入场路径；属性/关键词标签记录为对象标签，据守群体增益/精灵生成、等级 3/6 以上静态修正、法盾/游走升级路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-flameclaw-keyword-unit.fixture.json` / `p2-preflight-play-wuji-apprentice-keyword-unit.fixture.json` / `p2-preflight-play-arena-crowd-favorite-keyword-unit.fixture.json` / `p2-preflight-play-unl-yi-hunt-keyword-unit.fixture.json` / `p2-preflight-play-unl-yi-alt-a-hunt-keyword-unit.fixture.json` 已验证官方单位/英雄单位 `UNL-016/219` 《焰爪》、`UNL-040/219` 《无极学徒》、`UNL-102/219` 《竞技场人气王》和 `UNL-113/219` / `UNL-113a/219` 《易》的单位源牌入场路径；属性/关键词标签记录为对象标签，等级 3/6 以上战力修正、活跃进场、抽牌、法盾/游走升级和经验消耗增益路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-khazix-hunt-keyword-unit.fixture.json` / `p2-preflight-play-khazix-alt-a-hunt-keyword-unit.fixture.json` / `p2-preflight-play-black-rose-agent-keyword-unit.fixture.json` / `p2-preflight-play-stunning-guardian-keyword-unit.fixture.json` / `p2-preflight-play-galio-keyword-unit.fixture.json` 已验证官方单位/英雄单位 `UNL-119/219` / `UNL-119a/219` 《卡兹克》、`UNL-152/219` 《黑色玫瑰要员》、`UNL-162/219` 《惊艳守护者》和 `UNL-171/219` 《加里奥》的单位源牌入场路径；属性/关键词标签记录为对象标签，进攻触发、经验消耗、伤害、绝念召符文、强攻战斗修正、法盾目标税、壁垒承伤顺序和无法造成战斗伤害路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-rell-keyword-unit.fixture.json` / `p2-preflight-play-sfd-jax-keyword-unit.fixture.json` / `p2-preflight-play-sfd-jax-alt-a-keyword-unit.fixture.json` / `p2-preflight-play-giant-arm-kato-keyword-unit.fixture.json` / `p2-preflight-play-xin-zhao-keyword-unit.fixture.json` 已验证官方单位/英雄单位 `SFD·024/221` 《芮尔》、`SFD·054/221` / `SFD·054a/221` 《贾克斯》、`SFD·112/221` 《巨腕加藤》和 `SFD·176/221` 《赵信》的单位源牌入场路径；关键词标签记录为对象标签，壁垒承伤顺序、法盾目标税、进攻触发、武装免费打出/贴附、手牌武装灵便、移动触发关键词/战力授予和条件活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sfd-sivir-spellshield2-keyword-unit.fixture.json` / `p2-preflight-play-sfd-sivir-alt-a-spellshield2-keyword-unit.fixture.json` / `p2-preflight-play-sfd-draven-keyword-unit.fixture.json` / `p2-preflight-play-sfd-draven-alt-a-keyword-unit.fixture.json` / `p2-preflight-play-sfd-vayne-keyword-unit.fixture.json` 已验证官方英雄单位 `SFD·120/221` / `SFD·120a/221` 《希维尔》、`SFD·148/221` / `SFD·148a/221` 《德莱文》和 `SFD·223/221` 《薇恩》的单位源牌入场路径；属性/关键词标签记录为对象标签，法盾目标税、强攻战斗修正、进攻征服、过量伤害、战斗胜利/被摧毁得分、条件活跃进场、征服支付1与回手路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sfd-vayne-promo-keyword-unit.fixture.json` / `p2-preflight-play-sfd-irelia-keyword-unit.fixture.json` / `p2-preflight-play-sfd-irelia-promo-keyword-unit.fixture.json` / `p2-preflight-play-sfd-yasuo-keyword-unit.fixture.json` / `p2-preflight-play-sfd-yasuo-promo-keyword-unit.fixture.json` 已验证官方英雄单位 `SFD·223*/221` 《薇恩》、`SFD·225/221` / `SFD·225*/221` 《艾瑞莉娅》和 `SFD·235/221` / `SFD·235*/221` 《亚索》的单位源牌入场路径；属性/关键词标签记录为对象标签，强攻战斗修正、条件活跃进场、征服支付1与回手、法盾目标税、被选择或准备时本回合 +1、游走移动和单回合第三次移动得分路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sfd-aphelios-vanilla-unit.fixture.json` / `p2-preflight-play-sfd-aphelios-promo-vanilla-unit.fixture.json` / `p2-preflight-play-sfd-ahri-vanilla-unit.fixture.json` / `p2-preflight-play-sfd-ahri-promo-vanilla-unit.fixture.json` 已验证官方英雄单位 `SFD·224/221` / `SFD·224*/221` 《厄斐琉斯》和 `SFD·227/221` / `SFD·227*/221` 《阿狸》的单位源牌入场路径；源牌进入控制者基地成为无额外标签的 `CARD_TYPE:UNIT` 单位对象，武装贴附后三选一触发和进攻/防守时敌方单位本回合 -2 且不得低于 1 的触发路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sfd-yone-no-optional-assemble.fixture.json` / `p2-preflight-play-sfd-yone-promo-no-optional-assemble.fixture.json` / `p2-preflight-play-sfd-darius-trifarian-unit.fixture.json` / `p2-preflight-play-sfd-darius-promo-trifarian-unit.fixture.json` 已验证官方英雄单位 `SFD·233/221` / `SFD·233*/221` 《永恩》和 `SFD·236/221` / `SFD·236*/221` 《德莱厄斯》的单位源牌入场路径；永恩不选择百炼装配并记录 `恶魔` / `百炼` 标签，德莱厄斯记录 `崔法利` 属性标签；百炼装配/武装贴附、征服开放战场后的基地伤害、鼓舞活跃进场和同处友方单位静态 +1 路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-arena-councilor-active-unit.fixture.json` / `p2-preflight-play-immortal-phoenix-keyword-unit.fixture.json` / `p2-preflight-play-corpse-flower-predator-keyword-unit.fixture.json` / `p2-preflight-play-revna-roam-keyword-unit.fixture.json` / `p2-preflight-play-jungle-elephant-keyword-unit.fixture.json` 已验证官方单位 `UNL-001/219` 《竞技场理事》、`OGN·037/298` 《不朽凤凰》、`OGN·161/298` 《亡花掠食者》、`UNL-005/219` 《传承者雷芙纳》和 `UNL-008/219` 《莽林巨象》的单位源牌入场路径；《竞技场理事》验证未休眠入场并记录 `约德尔人` 属性，其余四张记录属性/关键词标签；横置战力修正、强攻战斗修正、法盾目标税、游走移动、废牌堆打出、敌方控制战场打出、法术触发活跃和条件活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-sad-poro-keyword-unit.fixture.json` / `p2-preflight-play-unl-sad-poro-keyword-unit.fixture.json` / `p2-preflight-play-watchful-sentinel-vanilla-unit.fixture.json` / `p2-preflight-play-scouting-warhawk-keyword-unit.fixture.json` / `p2-preflight-play-mechanical-trickster-vanilla-unit.fixture.json` 已验证官方单位 `SFD·036/221` / `UNL-221/219` 《哀哀魄罗》、`OGN·096/298` 《警觉的哨兵》、`OGN·216/298` 《侦察飞鹰》和 `OGN·239/298` 《机械戏法师》的单位源牌入场路径；属性标签记录为对象标签，无属性者成为无额外标签单位对象；绝念抽牌、召符文和创建随从路径暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-unl-babbling-poro-predict-recycle.fixture.json` / `p2-preflight-play-babbling-poro-predict-recycle.fixture.json` / `p2-preflight-play-gemstone-golem-predict-recycle.fixture.json` / `p2-preflight-play-dase-scout-predict-recycle.fixture.json` / `p2-preflight-play-jhin-predict-recycle.fixture.json` 已验证官方单位/英雄单位 `UNL-224/219` 《叨叨魄罗》、`OGN·171/298` 《叨叨魄罗》、`OGN·086/298` 《宝石巨像》、`OGN·174/298` 《大塞斥候》和 `UNL-089/219` 《烬》的单位源牌入场后预知回收路径；源牌进入控制者基地成为单位对象，主牌堆顶部目标回收到牌堆底部，选择非顶部牌由直接拒绝测试覆盖。
- `p2-preflight-play-aggressive-dragonhound-active-unit.fixture.json` / `p2-preflight-play-yi-active-unit.fixture.json` / `p2-preflight-play-vanguard-squire-active-unit.fixture.json` / `p2-preflight-play-warwick-active-unit.fixture.json` / `p2-preflight-play-arc-warwick-active-unit.fixture.json` 已验证官方单位/英雄单位 `SFD·006/221` 《好斗的龙犬》、`OGS·009/024` 《易》、`OGS·016/024` 《先锋扈从》、`OGN·159/298` 《沃里克》和 `ARC-004/006` 《沃里克》的活跃入场路径；源牌进入控制者基地成为 `isExhausted = false` 的单位对象并记录属性/关键词标签，游走移动、横置技能和进攻触发暂缓，带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-dockside-lurker-vanilla-unit.fixture.json` 已验证官方单位 `OGN·175/298 船坞潜伏者` 的无卡面效果单位源牌入场路径，支付 3 点费用并结算为 3 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-vanguard-sergeant-vanilla-unit.fixture.json` 已验证官方单位 `OGN·219/298 先锋中士` 的无卡面效果单位源牌入场路径，支付 4 点费用并结算为 4 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-playful-imp-vanilla-unit.fixture.json` 已验证官方单位 `OGN·049/298 贪玩的小鬼` 的无卡面效果单位源牌入场路径，支付 5 点费用并结算为 5 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-super-mech-vanilla-unit.fixture.json` 已验证官方单位 `OGN·088/298 超能机甲` 的无卡面效果单位源牌入场路径，支付 7 点费用并结算为 8 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-mountain-drake-vanilla-unit.fixture.json` 已验证官方单位 `OGN·142/298 山脉亚龙` 的无卡面效果单位源牌入场路径，支付 9 点费用并结算为 10 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接拒绝测试覆盖。
- `p2-preflight-play-heartsplit-dragon-discard-opponent-hand.fixture.json` 已验证官方单位 `OGN·192/298 辟心玄龙` 从手牌打出后进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，并弃置一张已展示后选择的对手手牌；友方手牌目标由直接拒绝测试覆盖。
- `p2-preflight-play-charming-spirit-discard-chosen-player-hand.fixture.json` 已验证官方单位 `UNL-121/219 魅惑之灵` 从手牌打出后进入控制者基地成为 2 战力、带 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象，并让被选择玩家弃置一张手牌；当前 fixture 用对手手牌记录被选择玩家实际弃置结果，直接测试另覆盖己方手牌同样可作为弃置结果、非手牌目标拒绝。
- `p2-preflight-play-teemo-self-power-plus-three.fixture.json` / `p2-preflight-play-teemo-alt-a-self-power-plus-three.fixture.json` / `p2-preflight-play-teemo-alt-b-self-power-plus-three.fixture.json` / `p2-preflight-play-fnd-teemo-self-power-plus-three.fixture.json` 已验证官方英雄单位 `OGN·197/298` / `OGN·197a/298` / `OGN·197b/298` / `FND-196/298` 《提莫》从手牌打出后进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，并让自身本回合内战力 +3；带目标打出由直接拒绝测试覆盖，待命/反应路径暂缓。
- `p2-preflight-play-sett-self-boon.fixture.json` / `p2-preflight-play-sett-promo-self-boon.fixture.json` / `p2-preflight-play-ogn-sett-self-boon.fixture.json` / `p2-preflight-play-ogn-sett-alt-a-self-boon.fixture.json` 已验证官方英雄单位 `SFD·232/221` / `SFD·232*/221` / `OGN·164/298` / `OGN·164a/298` 《瑟提》从手牌打出后进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并给予自身 `增益` 标签和永久 +1 战力；带目标打出由直接拒绝测试覆盖，征服触发与消耗增益激活暂缓。
- `p2-preflight-play-scrap-heap-equipment-draw.fixture.json` 已验证官方装备 `OGN·182/298 废料堆` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象，并抽 1 张牌；带目标打出由直接拒绝测试覆盖，弃置和摧毁触发抽牌分支暂缓。
- `p2-preflight-play-sprite-lantern-equipment-create-sprite.fixture.json` 已验证官方装备 `UNL-078/219 精灵提灯` 从手牌打出后进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象，并打出一名带 `瞬息` 标签的 3 战力“精灵”到控制者基地；带目标打出由直接拒绝测试覆盖，绝念和开始阶段瞬息摧毁暂缓。
- `p2-preflight-play-sumpworks-map-equipment-ephemeral.fixture.json` 已验证官方装备 `UNL-085/219 地沟区地图` 从手牌打出后进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象；带目标打出由直接拒绝测试覆盖，对手得分触发抽牌和开始阶段瞬息摧毁暂缓。
- `p2-preflight-play-scrying-blossom-equipment-exhausted.fixture.json` 已验证官方装备 `UNL-136/219 占卜花朵` 从手牌打出后进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，洞察/抽牌/经验激活技能暂缓。
- `p2-preflight-play-magic-beans-equipment.fixture.json` 已验证官方装备 `UNL-011/219 魔法鲜豆` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，法术对决期间单位打出触发抽牌分支暂缓。
- `p2-preflight-play-steel-ballista-equipment-exhausted.fixture.json` 已验证官方装备 `OGN·017/298 钢铁弩炮` 从手牌打出后进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置伤害技能暂缓。
- `p2-preflight-play-heart-of-ice-equipment.fixture.json` 已验证官方装备 `SFD·052/221 玄冰之心` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置战力修正技能暂缓。
- `p2-preflight-play-remorse-orb-equipment.fixture.json` 已验证官方装备 `OGN·090/298 懊悔法球` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置负战力修正技能暂缓。
- `p2-preflight-play-soul-sword-equipment.fixture.json` 已验证官方装备 `UNL-039/219 灵魂之剑` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。
- `p2-preflight-play-jagged-dirk-equipment.fixture.json` 已验证官方装备 `SFD·009/221 锯齿短匕` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配红色贴附分支暂缓。
- `p2-preflight-play-dorans-shield-equipment.fixture.json` 已验证官方装备 `SFD·033/221 多兰之盾` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。
- `p2-preflight-play-hextech-infused-bulwark-equipment.fixture.json` 已验证官方装备 `SFD·073/221 海克斯注力刚壁` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配蓝色贴附分支暂缓。
- `p2-preflight-play-dorans-blade-equipment.fixture.json` 已验证官方装备 `SFD·095/221 多兰之刃` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。
- `p2-preflight-play-dorans-ring-equipment.fixture.json` 已验证官方装备 `SFD·124/221 多兰之戒` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。
- `p2-preflight-play-vanguards-eye-equipment.fixture.json` 已验证官方装备 `SFD·153/221 先锋之眼` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。
- `p2-preflight-play-recurve-bow-equipment.fixture.json` 已验证官方装备 `SFD·016/221 反曲之弓` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配红色贴附分支暂缓。
- `p2-preflight-play-brutalizer-equipment.fixture.json` 已验证官方装备 `SFD·042/221 残暴之力` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。
- `p2-preflight-play-guardian-angel-equipment.fixture.json` 已验证官方装备 `SFD·051/221 守护天使` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。
- `p2-preflight-play-hexdrinker-equipment.fixture.json` 已验证官方装备 `SFD·102/221 海克斯饮魔刀` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。
- `p2-preflight-play-warmogs-armor-equipment.fixture.json` 已验证官方装备 `SFD·108/221 狂徒铠甲` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。
- `p2-preflight-play-trinity-force-equipment.fixture.json` 已验证官方装备 `SFD·115/221 三相之力` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。
- `p2-preflight-play-boots-of-swiftness-equipment.fixture.json` 已验证官方装备 `SFD·133/221 轻灵之靴` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。
- `p2-preflight-play-cull-equipment.fixture.json` 已验证官方装备 `SFD·134/221 萃取` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。
- `p2-preflight-play-sacred-shears-equipment.fixture.json` 已验证官方装备 `SFD·172/221 神圣剪刀` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。
- `p2-preflight-play-bf-sword-equipment.fixture.json` 已验证官方装备 `SFD·161/221 暴风大剑` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。
- `p2-preflight-play-wanderers-guidebook-equipment.fixture.json` 已验证官方装备 `SFD·086/221 云游图鉴` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配蓝色贴附分支暂缓。
- `p2-preflight-play-arions-fall-equipment.fixture.json` 已验证官方装备 `SFD·030/221 阿瑞昂的陨落` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配 1 红色贴附分支暂缓。
- `p2-preflight-play-hunters-machete-equipment.fixture.json` 已验证官方装备 `UNL-096/219 猎人的宽刃刀` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。
- `p2-preflight-play-withered-battleaxe-equipment.fixture.json` 已验证官方装备 `UNL-019/219 枯萎战斧` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配 1 红色贴附分支暂缓。
- `p2-preflight-play-bone-club-equipment.fixture.json` 已验证官方装备 `SFD·118/221 碎骨棒` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配 1 橙色贴附分支暂缓。
- `p2-preflight-play-ancient-stele-equipment.fixture.json` 已验证官方装备 `SFD·117/221 远古簇碑` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。
- `p2-preflight-play-hextech-anomaly-equipment.fixture.json` 已验证官方装备 `SFD·083/221 海克斯异常体` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。
- `p2-preflight-play-energy-channel-equipment.fixture.json` 已验证官方装备 `OGN·098/298 能量通道` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。
- `p2-preflight-play-time-gate-equipment.fixture.json` 已验证官方装备 `SFD·078/221 预时之门` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置回响授予技能暂缓。
- `p2-preflight-play-raven-tome-equipment.fixture.json` 已验证官方装备 `OGN·032/298 邪鸦魔典` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置法术伤害修正技能暂缓。
- `p2-preflight-play-sun-disc-equipment.fixture.json` 已验证官方装备 `OGN·021/298 太阳圆盘` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置鼓舞技能暂缓。
- `p2-preflight-play-foresight-mask-equipment.fixture.json` 已验证官方装备 `OGN·060/298 远见面具` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，战斗触发暂缓。
- `p2-preflight-play-solari-altar-equipment.fixture.json` 已验证官方装备 `OGN·072/298 烈阳圣坛` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，摧毁触发抽牌分支暂缓。
- `p2-preflight-play-chemtech-barrel-equipment.fixture.json` 已验证官方装备 `SFD·063/221 炼金科技桶` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，对手回合法术触发打出金币分支暂缓。
- `p2-preflight-play-soul-wheel-equipment.fixture.json` 已验证官方装备 `SFD·144/221 灵魂之轮` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，友方单位目标触发支付并抽牌分支暂缓。
- `p2-preflight-play-mushroom-bag-equipment.fixture.json` 已验证官方装备 `OGN·101/298 蘑菇袋` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，开始阶段正面朝下待命牌抽牌触发暂缓。
- `p2-preflight-play-arena-bar-equipment.fixture.json` 已验证官方装备 `OGN·124/298 竞技场酒吧` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置给予休眠友方单位增益技能暂缓。
- `p2-preflight-play-pirate-hideout-equipment.fixture.json` 已验证官方装备 `OGN·143/298 海盗避风港` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，友方单位变为活跃时战力修正触发暂缓。
- `p2-preflight-play-forgotten-signpost-equipment.fixture.json` 已验证官方装备 `UNL-045/219 被遗忘的路标` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，迅捷横置移动技能暂缓。
- `p2-preflight-play-frozen-gem-equipment.fixture.json` 已验证官方装备 `UNL-074/219 冰封宝石` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，抽第二张牌触发战力修正分支暂缓。
- `p2-preflight-play-crumbling-palace-equipment.fixture.json` 已验证官方装备 `UNL-088/219 倾颓宫殿` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，开始阶段胜利条件和横置创建战鹰分支暂缓。
- `p2-preflight-play-scarlet-rose-equipment.fixture.json` 已验证官方装备 `UNL-109/219 猩红玫瑰` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，单位打出触发经验和横置活跃单位技能暂缓。
- `p2-preflight-play-reversal-shard-equipment.fixture.json` 已验证官方装备 `UNL-174/219 逆转碎片` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，开始阶段摧毁触发分支暂缓。
- `p2-preflight-play-assembly-rack-equipment.fixture.json` 已验证官方装备 `SFD·019/221 装配架` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置创建机器人技能暂缓。
- `p2-preflight-play-sfur-song-equipment.fixture.json` 已验证官方装备 `SFD·059/221 斯弗尔尚歌` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配贴附和复制技能文字分支暂缓。
- `p2-preflight-play-z-drive-equipment.fixture.json` 已验证官方装备 `SFD·090/221 Z型驱动` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配和放逐打出分支暂缓。
- `p2-preflight-play-vanguard-armory-equipment.fixture.json` 已验证官方装备 `SFD·168/221 先锋军备` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置创建多个随从分支暂缓。
- `p2-preflight-play-remembrance-altar-equipment.fixture.json` 已验证官方装备 `SFD·169/221 追忆祭坛` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，友方单位摧毁触发和牌堆放置选择暂缓。
- `p2-preflight-play-rage-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·222/221 暴怒之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得红色符能技能暂缓。
- `p2-preflight-play-focus-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·226/221 专注之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得绿色符能技能暂缓。
- `p2-preflight-play-insight-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·229/221 洞察之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得蓝色符能技能暂缓。
- `p2-preflight-play-power-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·231/221 力量之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得橙色符能技能暂缓。
- `p2-preflight-play-discord-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·234/221 不和之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得紫色符能技能暂缓。
- `p2-preflight-play-unity-sigil-equipment.fixture.json` 已验证官方 0 费装备 `SFD·238/221 团结之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得黄色符能技能暂缓。
- `p2-preflight-play-ogn-rage-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·040/298 暴怒之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得红色符能技能暂缓。
- `p2-preflight-play-ogn-focus-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·081/298 专注之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得绿色符能技能暂缓。
- `p2-preflight-play-ogn-insight-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·120/298 洞察之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得蓝色符能技能暂缓。
- `p2-preflight-play-ogn-power-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·163/298 力量之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得橙色符能技能暂缓。
- `p2-preflight-play-ogn-discord-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·204/298 不和之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得紫色符能技能暂缓。
- `p2-preflight-play-ogn-unity-sigil-equipment.fixture.json` 已验证官方 0 费装备 `OGN·245/298 团结之印` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置获得黄色符能技能暂缓。
- `p2-preflight-play-wondrous-pack-equipment.fixture.json` 已验证官方装备 `OGN·181/298 奇妙行囊` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置回手技能暂缓。
- `p2-preflight-play-siren-equipment.fixture.json` 已验证官方装备 `OGN·184/298 塞壬号` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，支付并横置移动技能暂缓。
- `p2-preflight-play-ownerless-treasure-equipment.fixture.json` 已验证官方装备 `OGN·186/298 无主宝藏` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，离场触发和自毁激活技能暂缓。
- `p2-preflight-play-scavenging-whiz-equipment.fixture.json` 已验证官方装备 `OGN·099/298 拾荒小能手` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，回收/支付/横置抽牌技能暂缓。
- `p2-preflight-play-mistfall-bladeyard-equipment.fixture.json` 已验证官方装备 `OGN·152/298 雾临剑冢` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，增益触发和支付休眠分支暂缓。
- `p2-preflight-play-shimmering-aurora-equipment.fixture.json` 已验证官方装备 `OGN·160/298 闪耀极光` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，回合结束展示与免费打出分支暂缓。
- `p2-preflight-play-solari-emblem-equipment.fixture.json` 已验证官方装备 `OGN·227/298 烈阳徽记` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，战斗平局触发和全体召回分支暂缓。
- `p2-preflight-play-vanguard-helm-equipment.fixture.json` 已验证官方装备 `OGN·228/298 先锋之盔` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，增益单位摧毁触发和增益分配暂缓。
- `p2-preflight-play-honeyfruit-equipment.fixture.json` 已验证官方装备 `UNL-049/219 蜜糖果实` 从手牌打出后以休眠状态进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，横置资源技能和等级 6 分支暂缓。
- `p2-preflight-play-last-rites-equipment.fixture.json` 已验证官方装备 `SFD·150/221 临终仪式` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配和废牌堆回收分支暂缓。
- `p2-preflight-play-blade-of-ruined-king-equipment.fixture.json` 已验证官方装备 `SFD·178/221 破败王者之刃` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，装配、额外费用和贴附分支暂缓。
- `p2-preflight-play-mysterious-weapon-equipment.fixture.json` 已验证官方装备 `OGN·023/298 来路不明的武器` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，弃牌横置和摧毁替代效果暂缓。
- `p2-preflight-play-sea-monster-hook-equipment.fixture.json` 已验证官方装备 `OGN·242/298 海兽钓钩` 从手牌打出后进入控制者基地成为 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接拒绝测试覆盖，激活、摧毁、查看和免费打出分支暂缓。
- `p2-preflight-play-thermogenic-beam-destroy-all-equipment.fixture.json` 已验证《热电光束》支付 5 点费用，0 目标入栈，双方让过后摧毁双方场上所有装备，非装备单位不受影响。
- `p2-preflight-play-blood-money-destroy-enemy-small-unit-create-gold.fixture.json` 与 `p2-preflight-play-blood-money-destroy-friendly-small-unit-create-two-gold.fixture.json` 已验证《血钱》摧毁战场上不高于 2 战力单位后，按敌方/友方分支分别打出 1/2 枚休眠“金币”装备指示物；3 战力目标由直接拒绝测试覆盖。
- `p2-preflight-play-broken-blades-rematch-destroy-each-player-equipment.fixture.json` 已验证《折戟再战》在当前 2P preflight 中按目标顺序记录双方各自选择一件自己的装备，双方让过后分别摧毁；单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-bellows-breath-up-to-three-units-damage.fixture.json` 已验证官方法术 `SFD·080/221 风箱炎息` 不支付有色回响时，对当前单战场区域模型中的最多三名单位各造成 1 点伤害；重复目标和第四目标由直接拒绝测试覆盖，同一位置精确约束与有色回响路径暂缓。
- `p2-preflight-play-firestorm-damage-enemy-battlefield-units.fixture.json` 已验证官方法术 `OGS·002/024 烈火风暴` 对当前单战场区域模型中的所有敌方战场单位各造成 3 点伤害；友方战场单位和敌方基地单位不受影响，显式单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-crescent-strike-target-plus-splash.fixture.json` 已验证官方法术 `UNL-072/219 新月打击` 在当前单战场区域模型中选择一名敌方战场单位，对主目标造成 4 点伤害，并对其他敌方战场单位各造成 1 点伤害；友方战场单位和敌方基地单位不受影响，非法目标由直接拒绝测试覆盖，同一位置精确约束暂缓。
- `p2-preflight-play-production-surge-create-robot-draw.fixture.json` 已验证官方法术 `SFD·076/221 产量激增` 的全额费用路径：支付 4 点费用，0 目标入栈，双方让过后打出一名 3 战力“机器人”单位指示物到控制者基地，然后抽 1 张牌。
- `p2-preflight-play-production-surge-reduced-by-mechanical.fixture.json` 已验证《产量激增》在控制者控制“机械”属性单位时费用减少 2：通过对象状态 `tags = ["机械"]` 触发减费，支付 2 点费用后仍按卡面打出“机器人”到基地并抽 1 张牌。
- `p2-preflight-play-common-cause-create-four-minions-base.fixture.json` 已验证官方法术 `OGS·015/024 共同献身` 的基地目的地路径：`PLAY_CARD.mode = "BASE"`，支付 6 点费用，0 目标入栈，双方让过后打出四名 1 战力“随从”单位指示物到控制者基地。
- `p2-preflight-play-featherstorm-create-warhawks.fixture.json` 已验证官方法术 `UNL-044/219 羽毛旋风` 的单位指示物模式：`PLAY_CARD.mode = "CREATE_WARHAWKS"`，支付 4 点费用，0 目标入栈，双方让过后打出四名 1 战力“战鹰”单位指示物到控制者基地，并以对象 `tags = ["法盾"]` 记录其法盾标记；无效化法术模式和法盾额外选取费用模型暂缓。
- `p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json` 已验证官方法术 `SFD·031/221 点沙成兵` 支付回响 2 后重复单位指示物创建效果：支付基础 2 点和回响 2 点费用，双方让过后打出两名 2 战力“黄沙士兵”单位指示物到控制者基地。
- `p2-preflight-play-sandcraft-create-one-sand-soldier-base.fixture.json` 已验证《点沙成兵》未支付回响时只打出一名 2 战力“黄沙士兵”单位指示物到控制者基地。
- `p2-preflight-play-protect-the-emperor-create-sand-soldier.fixture.json` 已验证官方法术 `SFD·154/221 护驾！` 不支付有色可选费用时，支付 3 点费用并打出一名 2 战力“黄沙士兵”单位指示物到控制者基地；待命和支付黄色让其变为活跃状态的分支暂缓。
- `p2-preflight-play-sand-soldiers-rise-ready-two.fixture.json` 已验证官方专属法术 `SFD·198/221 沙兵现身` 在控制 0 件武装时不创建新单位，并让两名既有“黄沙士兵”变为活跃状态；按武装数量创建黄沙士兵的动态分支暂缓。
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
- `p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json` 已验证官方法术 `OGN·262/298 天顶之刃` 不选择可选移动时的基础路径：目标必须是敌方战场单位，双方让过后对目标施加 `STUNNED`；友方单位和敌方基地单位目标由直接拒绝测试覆盖，可选移动分支暂缓。
- `p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units.fixture.json` 已验证官方法术 `OGN·220/298 强手裂颅` 从手牌打出时按顺序选择一名友方战场单位和一名敌方战场单位，并对两名目标施加 `STUNNED`；目标顺序、友方基地单位和敌方基地单位由直接拒绝测试覆盖，待命/迅捷窗口细节暂缓。
- `p2-preflight-play-heroic-charge-power-plus-stun.fixture.json` 已验证官方法术 `UNL-155/219 英勇冲锋` 在当前单战场区域模型中按顺序选择一名友方战场单位和一名敌方战场单位，先让友方目标本回合内战力 +1，再眩晕敌方目标；目标顺序和基地单位目标由直接拒绝测试覆盖，同一位置精确约束暂缓。
- `p2-preflight-punishment-lethal-damage-banishes-unit.fixture.json` 已验证《惩戒》造成的 3 点伤害达到目标 3 点战力时，目标因本回合替代效果改为记录 `UNIT_BANISHED`，并移入拥有者放逐区而非废牌堆。
- `p2-preflight-punishment-banishes-if-destroyed-later.fixture.json` 已验证《惩戒》目标若在同一回合稍后被《复仇》摧毁，也会由替代效果改为放逐，且不写入本回合摧毁记忆。
- `p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json` 已验证《碎裂之火》对战场单位造成致命伤害后，先摧毁目标，再按卡面条件抽 1 张牌。
- `p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json` 已验证《碎裂之火》未摧毁目标时不会抽牌，并保留目标的伤害状态。
- `p2-preflight-starfall-damages-two-units.fixture.json` 已验证《星落》的两次单位伤害选择可以指向不同位置的单位，并在同一结算后摧毁多个达到战力伤害的目标。
- `p2-preflight-starfall-can-damage-same-unit-twice.fixture.json` 已验证《星落》可以两次选择同一个单位，目标不去重，并在伤害累积达到战力后摧毁该单位。
- `p2-preflight-play-duel-mutual-power-damage.fixture.json` 已验证《决斗》按“友方单位、敌方单位”的目标顺序，让两名单位以自身当前战力互相造成伤害，并在致命伤害清理中摧毁达到战力伤害的敌方单位；直接测试另覆盖反向目标顺序会被拒绝。
- `p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json` 已验证《绅士决斗》先让友方目标本回合内战力 +3，再按两名目标当前战力互相造成伤害；样例覆盖战力修正后的互伤与致命伤害清理。
- `p2-preflight-play-marching-orders-echo-mutual-power-damage.fixture.json` 已验证《行军号令》支付回响后重复友方单位与敌方战场单位按自身战力互相造成伤害；样例覆盖友方基地单位与敌方战场单位互伤。
- `p2-preflight-play-last-breath-ready-damage-enemy-battlefield.fixture.json` 已验证《狂风绝息斩》先让友方单位变为活跃状态，再对敌方战场单位造成等同于该友方单位当前战力的伤害；目标顺序错误和敌方基地单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-convergent-mutation-match-friendly-power.fixture.json` 已验证《聚合变异》选择两名不同友方单位，若第一目标战力低于第二目标，则第一目标本回合内变为第二目标当前战力；敌方目标和重复目标由直接拒绝测试覆盖。
- `p2-preflight-play-switcheroo-swap-battlefield-unit-powers.fixture.json` 已验证《换换乐》在当前单战场区域模型中选择两名不同战场单位，并用本回合内战力修正互换两者战力；重复目标和基地单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-clash-of-giants-mutual-power-damage.fixture.json` 已验证《巨人之战》可选择任意两名单位，让两名目标以自身当前战力互相造成伤害；样例覆盖基地单位和战场单位互伤后摧毁达到战力伤害的战场单位。
- `p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json` 已验证《艾卡西亚暴雨》的六次单位伤害选择可以重复命中同一单位，并在累计 12 点伤害后摧毁目标。
- `p2-preflight-play-blade-whirlwind-damage-all-battlefield-units.fixture.json` 已验证《剑刃飓风》0 目标入栈后，对所有战场上的单位各造成 1 点伤害，不分敌我；当前样例锁定未致命伤害路径。
- `p2-preflight-blade-whirlwind-lethal-damage-destroys-units.fixture.json` 已验证《剑刃飓风》全战场单位伤害达到多个单位战力时，会逐一记录 `UNIT_DESTROYED` 并将单位移入各自拥有者废牌堆。
- `p2-preflight-play-cannon-barrage-damage-enemy-combat-units.fixture.json` 已验证《加农炮幕》0 目标入栈后，只对战斗中的敌方单位各造成 2 点伤害；当前最小模型以 `isAttacking` / `isDefending` 表达战斗中单位，并确认友方战斗中单位和敌方非战斗单位不受影响。
- `p2-preflight-play-stay-away-stun-draw-stack.fixture.json` 已验证《走开》从手牌打出时对目标施加 `STUNNED`，然后抽 1 张牌；待命路径暂缓。
- `p2-preflight-play-disposal-order-draw-mode.fixture.json` 已验证《处置命令》的 `DRAW_1` 模式，支付费用后 0 目标入栈并抽 1 张牌。
- `p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json` 已验证《处置命令》的 `RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3` 模式，选择对手废牌堆中最多三张牌并让其拥有者回收。
- `p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card.fixture.json` 已验证《暗中破坏》选择对手手牌中的非单位牌并让其拥有者回收；带 `CARD_TYPE:UNIT` 的对手手牌目标由直接拒绝测试覆盖。
- `p2-preflight-play-predictive-offensive-draw-one-recycle-other.fixture.json` 已验证《预判攻势》不支付回响时，选择己方主牌堆顶部两张中的一张抽取，并将另一张回收到主牌堆底部；选择顶部两张以外的卡牌由直接拒绝测试覆盖。
- `p2-preflight-play-card-trick-draw-one-recycle-rest.fixture.json` 已验证《卡牌骗术》选择己方主牌堆顶部三张中的一张加入手牌，并将其余两张回收到主牌堆底部；选择顶部三张以外的卡牌由直接拒绝测试覆盖。
- `p2-preflight-play-dragon-tiger-draw-unit-recycle-rest.fixture.json` 已验证《龙虎双雄》不支付回响时，选择己方主牌堆顶部三张中带 `CARD_TYPE:UNIT` 对象标签的一张单位牌抽取，并将其余两张回收到主牌堆底部；非单位目标和顶部三张以外目标由直接拒绝测试覆盖。
- `p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all.fixture.json` 已验证《龙虎双雄》不支付回响且不选择单位牌时，回收已查看的主牌堆顶部三张牌，不产生 `CARD_DRAWN` 事件。
- `p2-preflight-play-reinforcements-no-selection-recycle-top-five.fixture.json` 已验证《增援》不选择单位牌时，回收已查看的主牌堆顶部五张牌，不产生 `CARD_DRAWN` 事件；从牌堆打出并减费路径暂缓。
- `p2-preflight-play-meditation-draw-stack.fixture.json` 已验证《冥想》的基础抽牌路径，支付费用后 0 目标入栈并抽 1 张牌。
- `p2-preflight-play-salvage-draw-no-equipment.fixture.json` 与 `p2-preflight-play-salvage-destroy-equipment-draw.fixture.json` 已验证《废物利用》的可选装备分支：不选择装备时直接抽 1 张牌；选择装备时先摧毁该装备，再抽 1 张牌。单位目标由直接拒绝测试覆盖。
- `p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields.fixture.json` 已验证《占山为王》在当前没有已控制战场时的基础路径：支付费用后 0 目标入栈，双方让过后只抽基础 1 张牌；按战场控制数量额外抽牌暂缓至战场控制模型落地。
- `p2-preflight-play-meditation-exhaust-friendly-extra-draw.fixture.json` 已验证《冥想》可用 `optionalCosts = ["EXHAUST_FRIENDLY_UNIT:<objectId>"]` 让一名活跃友方单位变为休眠状态作为额外费用，并额外抽 1 张牌；直接测试另覆盖敌方单位不能支付该额外费用。
- `p2-preflight-play-moonsilver-gift-discard-draw.fixture.json` 已验证《月神恩赐》选择另一张友方手牌，结算时先弃置该牌到废牌堆，再抽 2 张牌；直接测试另覆盖不能把正在打出的源牌作为弃置对象。
- `p2-preflight-play-revive-return-graveyard-unit.fixture.json` 已验证《亡者复生》选择己方废牌堆里的单位牌，结算时将该目标移回手牌；直接测试另覆盖不能选择对手废牌堆目标。
- `p2-preflight-play-harrowing-play-graveyard-unit-base.fixture.json` 已验证《蚀魂夜》选择己方废牌堆里的单位牌，结算时将该目标打出到基地并变为活跃状态；非单位废牌堆目标由直接拒绝测试覆盖，完整目的地选择暂缓。
- `p2-preflight-play-guerrilla-warfare-return-standby-graveyard.fixture.json` 已验证《游击战》选择己方废牌堆最多两张带 `待命` 对象标签的牌，结算时将这些目标移回手牌；非待命目标由直接拒绝测试覆盖，免费正面朝下布置待命牌的本回合权限暂缓到待命布置模型。
- `p2-preflight-play-call-of-the-shadows-give-ephemeral-draw.fixture.json` 已验证《暗影的召唤》选择未拥有 `瞬息` 标签的友方单位，结算时给予该单位 `瞬息` 对象标签并抽 2 张牌；已有 `瞬息` 目标由直接拒绝测试覆盖，开始阶段摧毁瞬息单位暂缓到关键词清理模型。
- `p2-preflight-play-deadly-flourish-enemy-unit-damage.fixture.json` 已验证《致命华彩》选择一名敌方单位并造成 3 点非致命伤害；友方单位目标由直接拒绝测试覆盖，本回合摧毁后的休眠“金币”装备指示物触发暂缓到装备指示物和延迟触发模型。
- `p2-preflight-play-flowing-time-mirror-battlefield-unit-ephemeral.fixture.json` 与 `p2-preflight-play-flowing-time-mirror-equipment-ephemeral.fixture.json` 已验证《逝水如镜》选择一名战场单位或一件装备并给予 `瞬息` 对象标签；基地单位目标由直接拒绝测试覆盖，下个回合开始阶段摧毁瞬息对象暂缓到关键词清理模型。
- `p2-preflight-play-ashes-to-ashes-equipment-ephemeral.fixture.json` 已验证《化为灰烬》选择一件场上装备并给予 `瞬息` 对象标签；单位目标由直接拒绝测试覆盖，开始阶段摧毁瞬息对象暂缓到关键词清理模型。
- `p2-preflight-play-sigil-burst-destroy-equipment-draw.fixture.json` 已验证《印爆术》选择一件场上装备并摧毁，然后让该装备控制者抽 2 张牌；单位目标由直接拒绝测试覆盖，装备摧毁不会写入本回合单位摧毁记忆。
- `p2-preflight-play-back-against-wall-double-power-ephemeral.fixture.json` 已验证《背水一战》选择一名友方单位，使其本回合内按当前战力翻倍并获得 `瞬息` 对象标签；敌方单位目标由直接拒绝测试覆盖，迅捷时机和下个回合开始阶段摧毁瞬息单位暂缓。
- `p2-preflight-play-painful-payoff-damage-create-gold.fixture.json` 已验证《痛苦之酬》选择一名战场单位造成 3 点伤害，并在来源控制者基地打出一枚休眠的“金币”装备指示物；基地单位目标由直接拒绝测试覆盖，待命时机和金币资源技能暂缓。
- `p2-preflight-play-jungle-ambush-create-gold.fixture.json` 已验证《丛林伏击》从手牌打出时支付 2 点费用，并在来源控制者基地打出一枚休眠的“金币”装备指示物；本回合友方单位活跃进场的全局效果暂缓到单位打出模型。
- `p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json` 已验证《反转时间线》0 目标入栈后，每名玩家先弃置自己的所有手牌，再各抽 4 张牌。
- `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune.fixture.json` 已验证《牺牲》必须用 `optionalCosts = ["DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"]` 摧毁一名友方强力单位作为额外费用，然后先抽 2 张牌、再召出 1 枚休眠符文；直接测试另覆盖缺少该额外费用或目标未达到 5 战力时拒绝。
- `p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw.fixture.json` 已验证《断魂一扼》先摧毁第一名友方目标，再按被摧毁单位当前战力让第二名友方目标本回合内获得等量 +战力加成，最后抽 1 张牌；直接测试另覆盖敌方单位不能作为第二目标。
- `p2-preflight-play-center-your-mind-draw-stack.fixture.json` 已验证《聚心凝神》未满足等级减费条件时的全额支付基础路径，支付费用后 0 目标入栈并抽 2 张牌；等级 6/11 减费路径暂缓。
- `p2-preflight-play-borrowed-history-draw-stack.fixture.json` 已验证《借鉴历史》从手牌打出的基础抽牌路径，支付费用后 0 目标入栈并抽 2 张牌；待命/反应时机路径暂缓。
- `p2-preflight-play-spoils-of-war-draw-stack.fixture.json` 已验证《以战养战》未满足减费条件时的全额支付基础路径，支付 4 点费用后 0 目标入栈并抽 2 张牌。
- `p2-preflight-play-assemble-the-ranks-draw.fixture.json` 已验证《集结部队》从手牌打出时支付 2 点费用，0 目标入栈并抽 1 张牌；友方单位进场给予增益的全局触发暂缓到单位打出/延迟触发模型。
- `p2-preflight-play-call-to-action-draw.fixture.json` 已验证《迎敌号令》从手牌打出时支付 2 点费用，0 目标入栈并抽 1 张牌；本回合单位活跃进场的全局效果暂缓到单位打出模型。
- `p2-preflight-play-secret-art-mercy-grant-boon.fixture.json` 已验证《秘奥义！慈悲度魂落》从手牌打出时支付 3 点费用，选择一名友方单位并给予 `增益` 标签和永久 +1 战力；本回合所有增益额外 +1 的全局效果暂缓。
- `p2-preflight-play-stunning-display-boon-move-base-unit.fixture.json` 已验证《叹为观止》从手牌打出时支付 1 点费用，选择基地中的一名友方单位，给予 `增益` 标签和永久 +1 战力后移动到当前单战场区域。
- `p2-preflight-play-void-rush-draw-no-free-play.fixture.json` 已验证《虚空猛冲》从手牌打出时支付 2 点费用，当前 preflight 选择不免费打出顶部牌，因此抽取两张展示牌；免费打出分支暂缓。
- `p2-preflight-play-open-action-grant-all-boons.fixture.json` 已验证《公开行动》从手牌打出时支付 5 点费用，在当前没有既有增益可消耗时，给予所有友方单位 `增益` 标签和永久 +1 战力。
- `p2-preflight-play-reflections-swap-draw.fixture.json` 已验证《镜中幻影》从手牌打出时支付 2 点费用，选择两名不同公开区域友方单位，且其中一名拥有 `瞬息`，双方让过后互换位置并抽 1 张牌；无 `瞬息` 目标组合由直接拒绝测试覆盖。
- `p2-preflight-play-thundering-drop-base-power-damage-move.fixture.json` 已验证《天声震落》从手牌打出时支付 6 点费用，选择基地中的一名友方单位，按其当前战力对敌方战场单位造成伤害，然后将该友方单位移动到当前单战场区域。
- `p2-preflight-play-battle-command-move-friendly-and-opponent-unit.fixture.json` 已验证《战斗号令》当前 2P preflight 以目标顺序记录友方单位和对手所选单位，双方让过后将两名基地单位移动到当前粗粒度战场区域；完整对手选择 prompt 和多战场精确位置暂缓。
- `p2-preflight-play-void-assault-move-friendly-and-enemy-unit.fixture.json` 已验证《虚空来袭》当前 preflight 按顺序选择一名友方单位和一名敌方单位，双方让过后将两名基地单位移动到当前粗粒度战场区域；目标顺序反转由直接拒绝测试覆盖，战场控制/进攻方细节暂缓。
- `p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json` 已验证《弹幕时间》用 `optionalCosts = ["SPEND_POWER:3"]` 支付 3 点符能，并按支付数值对当前单战场区域中的敌方战场单位造成 3 点伤害；符能不足由直接拒绝测试覆盖。
- `p2-preflight-play-portalpal-rescue-banish-play-base.fixture.json` 已验证《传送门大营救》选择一名友方单位，双方让过后先放逐该单位，再将其重新打出到所属基地，并清除该单位场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接拒绝测试覆盖。
- `p2-preflight-play-hunting-rhythm-banish-play-battlefield.fixture.json` 已验证《狩猎律动》选择一名友方单位，双方让过后先放逐该单位，再将其重新打出到当前粗粒度战场，并清除该单位场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接拒绝测试覆盖。
- `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json` 已验证本回合先由《复仇》摧毁敌方单位后，《以战养战》读取 `destroyedUnitOwnerIdsThisTurn` 并减少 2 点费用，再抽 2 张牌。
- `p2-preflight-play-practical-experience-power-plus-1.fixture.json` 已验证《实战经验》在未启用等级 6 升级时对一名单位施加本回合内战力 +1；等级 6 改为 +3 的路径暂缓。
- `p2-preflight-play-dueling-stance-friendly-power-plus-1.fixture.json` 已验证《决斗架势》基础分支对一名友方单位施加本回合内战力 +1；“该处唯一控制单位”额外 +1 分支暂缓到更细位置模型。
- `p2-preflight-play-animal-friends-power-per-controlled-tag.fixture.json` 已验证《动物之友》按控制者场上/基地单位中“鸟类、猫科、犬形、魄罗”属性标签的不同种类数动态计算本回合内战力修正；样例中三种标签使目标 +3。
- `p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit.fixture.json` 已验证《矢志不退》选择一名友方战场单位，并按敌方战场单位数量动态计算本回合内战力修正；当前 P2 preflight 单战场区域模型中两个敌方战场单位使目标 +4，同一战场精确位置暂缓到多战场位置模型。
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
- `p2-preflight-play-eclipse-power-minus-4.fixture.json` 已验证《月蚀》对一名单位施加本回合内战力 -4，且洞察时不选择回收顶部牌。
- `p2-preflight-play-eclipse-power-minus-4-insight-recycle.fixture.json` 已验证《月蚀》先对一名单位施加本回合内战力 -4，再通过洞察选择己方主牌堆顶部一张牌并回收到主牌堆底部；选择顶部一张以外的卡牌由直接拒绝测试覆盖。
- `p2-preflight-play-moonfall-power-minus-10.fixture.json` 已验证《月光之殇》对一名单位施加本回合内战力 -10，并记录待回合结束清理移除的实际修正。
- `p2-preflight-play-glory-call-power-plus-3.fixture.json` 已验证《荣耀召唤》未支付消耗增益额外费用时，对一名单位施加本回合内战力 +3；消耗增益以无视费用的路径暂缓。
- `p2-preflight-play-last-stand-friendly-power-plus-3.fixture.json` 已验证《视死如归》对一名友方单位施加本回合内战力 +3；该单位本回合赢得战斗时获得 2 经验的触发路径暂缓到战斗胜负/经验模型。
- `p2-preflight-play-decisive-strike-all-friendly-power-plus-2.fixture.json` 已验证《致命打击》对控制者所有场上友方单位施加本回合内战力 +2，且不会影响对手单位。
- `p2-preflight-play-grand-strategy-all-friendly-power-plus-5.fixture.json` 已验证《宏伟战略》对控制者所有场上友方单位施加本回合内战力 +5，且不会影响对手单位。
- `p2-preflight-play-back-to-back-two-friendly-power-plus-2.fixture.json` 已验证《背靠背》只能选择两名友方单位并分别施加本回合内战力 +2；直接测试另覆盖选择敌方单位会被 `INVALID_TARGET` 拒绝。
- `p2-preflight-play-power-bind-echo-two-friendly-power-plus-1.fixture.json` 已验证《力量之缚》支付 `optionalCosts = ["ECHO"]` 后，重复“两名友方单位本回合内战力 +1”效果一次，两个目标各累计 +2。
- `p2-preflight-play-danger-temperature-mechanical-power-plus-1.fixture.json` 已验证《危险温度》未支付混合资源回响时，只让控制者自己的“机械”属性单位本回合内战力 +1，不影响己方非机械单位或对手机械单位。
- `p2-preflight-play-siphon-energy-battlefield-power-split.fixture.json` 已验证《虹吸能量》在当前单战场区域模型中令友方战场单位本回合内战力 +1、敌方战场单位本回合内战力 -1 且不得低于 1；基地单位不受影响。
- `p2-preflight-play-moonrise-enemy-battlefield-power-minus-2.fixture.json` 已验证《月之降临》在当前单战场区域模型中跳过可选敌方单位移动，并令敌方战场单位本回合内战力 -2；友方战场单位和双方基地单位不受影响。
- `p2-preflight-play-smoke-bomb-power-floor-stack.fixture.json` 已验证《烟幕弹》施加本回合内战力 -4，且结果不得低于 1；实际应用的战力修正会用于后续清理恢复。
- `p2-preflight-smoke-bomb-power-floor-expires-end-turn.fixture.json` 已验证被“不得低于 1”截断后的负战力修正会在 `END_TURN` 特殊清理中移除，并恢复单位原战力。
- `p2-preflight-play-extortion-power-floor-draw-stack.fixture.json` 已验证《“敲”诈》尝试施加本回合内战力 -1 时若目标已是 1 战力，则实际应用 0 且不低于 1，并继续抽 1 张牌。
- `p2-preflight-play-perfect-finale-*.fixture.json` 已验证《完美谢幕》未支付有色/多次回响时的四个模式：抽 1 张牌、对战场单位造成 2 点伤害、对基地单位造成 3 点伤害、让战场单位本回合内战力 -4；回响重复且不能重复选择同一效果的完整路径暂缓。
- `p2-preflight-play-highlander-bloodline-recall-if-destroyed.fixture.json` 已验证《高原血统》给予一名友方单位本回合内摧毁替代效果；随后《复仇》摧毁该单位时改为记录 `UNIT_RECALLED_TO_BASE`，清除伤害、以休眠状态返回拥有者基地，且不写入本回合摧毁记忆。
- `p2-preflight-play-tactical-retreat-recall-if-destroyed.fixture.json` 已验证《战术撤退》给予一名友方单位本回合内“下次被摧毁”替代效果；随后《复仇》摧毁该单位时改为清除伤害、变为休眠、召回拥有者基地，且不写入本回合摧毁记忆。

尚未完成：

- `PLAY_CARD` 已通过最小 card behavior registry 支撑 P2 preflight 的代表性官方卡牌/模式路径，当前为 `451/811 = 55.6%`。已覆盖能力族包括费用、目标范围、模式选择、伤害、抽牌/燃尽、摧毁/回手/移动、战力修正、关键词/对象标签、装备对象、单位源牌入场、指示物、符文召出、可选/强制额外费用和部分替代效果；完整 fixture 列表、事件词表和逐项进度以本文件的 fixture 表与第 6 节进度清单为准，规则证据以 `docs/rules-evidence-index.md` 和 fixture 内 `rulesEvidence` 为准。
- 额外费用协议已扩展到 `optionalCosts = ["EXHAUST_FRIENDLY_UNIT:<objectId>"]`、`optionalCosts = ["DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"]` 和 `optionalCosts = ["DISCARD_HAND_CARD:<objectId>"]`，当前分别用于《冥想》让活跃友方单位休眠后额外抽牌、《牺牲》摧毁友方强力单位后抽牌并召休眠符文，以及《怒吼清算》弃置一张手牌后重复回响效果；非法目标会由服务端以 `INVALID_TARGET` 拒绝。
- 手牌对象目标已扩展到 `FRIENDLY_HAND_CARD` 与 `FRIENDLY_HAND_CARD_THEN_BATTLEFIELD_UNIT`，当前用于《月神恩赐》的弃置后抽牌路径，以及《罪恶快感》的弃牌后按法力费用伤害路径；源牌自身不能作为弃置目标。
- 对手手牌对象目标已扩展到 `OPPONENT_HAND_CARD`，当前用于《暗中破坏》的非单位牌回收路径；带 `CARD_TYPE:UNIT` 标签的对手手牌目标会被拒绝。
- 己方主牌堆对象目标已扩展到 `FRIENDLY_MAIN_DECK_CARD`，当前用于《预判攻势》《卡牌骗术》和《龙虎双雄》的主牌堆顶部选择抽取路径、《占卜贝壳》的预知顶部一张可选回收路径，以及《增援》不选择单位牌时回收顶部五张路径；目标必须位于本次查看窗口内；《龙虎双雄》暂以对象标签 `CARD_TYPE:UNIT` 表达“单位牌”类型校验。
- 单位后接己方主牌堆对象目标已扩展到 `ANY_UNIT_THEN_FRIENDLY_MAIN_DECK_CARD`，当前用于《月蚀》的单位战力修正后洞察回收路径；第二目标必须位于本次查看窗口内。
- 己方废牌堆对象目标已扩展到 `FRIENDLY_GRAVEYARD_CARD`，当前用于《亡者复生》的废牌堆目标回手路径、《游击战》的待命牌回手路径，以及《蚀魂夜》《忠诚不渝》的废牌堆单位打出到基地路径；《游击战》先以对象标签 `待命` 表达待命卡牌校验，《蚀魂夜》先以对象标签 `CARD_TYPE:UNIT` 表达单位牌校验，《忠诚不渝》先用对象 `manaCost` 与 `MaxTargetManaCost` 表达 2 费上限，完整卡牌对象身份元数据补齐后可进一步细化。
- 控制权获得已用 `UNIT_CONTROL_GAINED` 事件表达，当前用于《强制征召》未支付经验额外费用时获得不高于 3 战力敌方战场单位控制权、使其休眠并召回，以及《据为己有》获得敌方战场单位控制权并召回；当前 2P preflight 用区域归属表达控制权，完整 owner/controller 分离模型暂缓。
- 对象持久标签添加已用 `OBJECT_TAG_ADDED` 事件表达，当前用于《暗影的召唤》让友方非瞬息单位获得 `瞬息` 标签、《逝水如镜》让战场单位/装备获得 `瞬息` 标签、《化为灰烬》让装备获得 `瞬息` 标签、《背水一战》让友方单位获得 `瞬息` 标签，以及《秘奥义！慈悲度魂落》《奥义！魂佑》让友方单位获得 `增益` 标签；`BOON_GRANTED` 记录增益带来的永久 +1 基础战力；`CARD_TYPE:EQUIPMENT` 暂用于《魄罗佳肴》《舒瑞娅的安魂曲》《未来熔炉》《废料堆》《精灵提灯》《地沟区地图》《占卜花朵》《魔法鲜豆》《钢铁弩炮》《玄冰之心》《懊悔法球》《灵魂之剑》《锯齿短匕》《多兰之盾》《海克斯注力刚壁》《多兰之刃》《多兰之戒》《先锋之眼》《反曲之弓》《残暴之力》《守护天使》《海克斯饮魔刀》《狂徒铠甲》《三相之力》《轻灵之靴》《萃取》《神圣剪刀》《暴风大剑》《云游图鉴》《阿瑞昂的陨落》《猎人的宽刃刀》《枯萎战斧》《碎骨棒》《远古簇碑》《海克斯异常体》《能量通道》《预时之门》《邪鸦魔典》《太阳圆盘》《远见面具》《烈阳圣坛》《炼金科技桶》《灵魂之轮》《蘑菇袋》《竞技场酒吧》《海盗避风港》《被遗忘的路标》《冰封宝石》《倾颓宫殿》《猩红玫瑰》《逆转碎片》《装配架》《斯弗尔尚歌》《Z型驱动》《先锋军备》《追忆祭坛》《暴怒之印》《专注之印》《洞察之印》《力量之印》《不和之印》《团结之印》以及 OGN 版《暴怒之印》《专注之印》《洞察之印》《力量之印》《不和之印》和《团结之印》、《奇妙行囊》《塞壬号》《无主宝藏》《拾荒小能手》《雾临剑冢》《闪耀极光》《烈阳徽记》《先锋之盔》《蜜糖果实》《临终仪式》《破败王者之刃》《来路不明的武器》《海兽钓钩》《禁魔石丰碑》《中娅沙漏》《夜之锋刃》《炉火斗篷》《灭世者的死亡之冠》《喷射球果》《奥义！魂佑》《夺命名单》《受诅咒的石棺》《占卜贝壳》、promo 编号《碎骨棒》和《海克斯科技护手》源牌入场装备对象、《痛苦之酬》和《血钱》的“金币”装备指示物对象标签，以及《化为灰烬》《印爆术》《废物利用》《紧急召回》《坠渊之流》《折戟再战》《热电光束》和《火箭轰击》摧毁装备模式的装备目标/全局摧毁校验；《未来熔炉》打出的“随从”使用 `CARD_TYPE:UNIT` 标签表达单位对象，《精灵提灯》《地沟区地图》和《禁魔石丰碑》用 `瞬息` 标签表达源装备关键词，《占卜花朵》《钢铁弩炮》和《蜜糖果实》用 `isExhausted = true` 表达休眠入场；装备入场已用 `EQUIPMENT_PLAYED_TO_BASE` 事件表达，装备摧毁已用 `EQUIPMENT_DESTROYED` 事件表达，装备回手已用 `EQUIPMENT_RETURNED_TO_HAND` 事件表达，且不会写入本回合单位摧毁记忆；开始阶段摧毁瞬息单位、金币资源技能、唯我、装配、装备横置技能、装备触发技能、法盾静态层和增益全局增强暂缓到后续关键词/装备/增益模型。

### 2.1 P2 加速执行策略

为提升后续迁移速度，P2 preflight 进入“同能力族小批次”节奏。该策略只调整执行批量和验证频率，不降低每张卡的审计要求：

- 每批优先选择 `3-5` 张共享同一 engine 原语的低复杂度官方卡牌/模式；如果批内需要新增小原语，则收窄到 `1-2` 张并优先把原语打稳。
- 每张卡仍必须包含 registry/card behavior、fixture、fixture 内 `rulesEvidence`、conformance 测试、`docs/rules-evidence-index.md` 行和本文件 fixture/进度同步。
- 批内每张卡完成后先跑目标过滤测试；批末统一跑 `FullyQualifiedName~ConformanceFixtureRunnerTests`、全量 `dotnet test Riftbound.slnx --no-restore`、`git diff --check`，再提交。
- 优先连续推进这些低复杂度能力族：0 目标抽牌/召符文/创建指示物、单目标伤害、单目标回手/移动、本回合战力修正/标签添加、简单装备对象。
- 优先补能解锁多张卡的小原语：废牌堆回收 N 张、装备自毁激活、最多 N 个目标、按标签/类型计数、单位/装备 token 参数化。
- 暂不为加速引入全卡牌自动迁移、复杂 AI、最终产品 UI、移动端或未审计规则 PDF/FAQ 提交。
- 每次停下来仍必须提交完成内容，并汇报实时 `x/811` 整体和当前 Part 百分比；未跟踪的 `riftbound-dotnet.sln` 不纳入提交。

### 2.2 并行协作判断

后续需要加速时，默认优先用子代理，而不是多个窗口同时改当前工作区：

- 适合子代理的工作：筛选下一批 `10-20` 张低复杂度候选卡、整理官方文本和 `rulesEvidence`、起草 fixture、检查已有 engine 原语是否可复用、审阅某个小原语的影响范围。
- 子代理默认不直接提交；主窗口负责最终接入 `CardBehaviorRegistry`、`CoreRuleEngine`、conformance 测试和文档，并执行批末验证与提交。
- 适合多个窗口的工作：两个以上 agent 需要长期同时写代码，且任务能按能力族或文件边界隔离，例如抽牌/召符文、装备对象、单目标伤害分别推进。
- 多窗口并行必须使用独立 `git worktree` 或分支，再由主窗口逐个 merge/cherry-pick；不要多个窗口同时在当前 `/Users/dinghaolin/MyProjects/riftbound-dotnet` 工作区写入热点文件。
- 热点文件包括 `src/Riftbound.Engine/CardBehaviorRegistry.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`docs/CURRENT_P2_STATUS.md`、`docs/p2-rules-preflight.md` 和 `docs/rules-evidence-index.md`。
- 默认安全形态是 `1` 个主窗口加 `1` 个只读/调研子代理；遇到明确、窄范围的小原语时可临时增加 `1` 个 worker。超过 `3` 个写代码 agent 前必须重新评估冲突成本。

## 3. P2 最小状态模型

在扩展 `MatchState` 时，优先加入以下服务端权威字段。玩家视角 snapshot 只投影允许看到的部分。

| 状态域 | 最小字段 | 用途 |
|---|---|---|
| 回合 | `turnNumber`, `turnPlayerId`, `phase`, `timingState` | 区分回合开始、主阶段、回合结束、普通/法术对决、开环/闭环。 |
| 行动权 | `priorityPlayerId`, `focusPlayerId`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` | 表达谁能自决行动，以及连续让过何时推进。 |
| 符文资源 | `runePools[playerId].mana`, `runePools[playerId].power` | 费用支付、抽牌阶段结束清空、回合结束清空。 |
| 区域 | `mainDeck`, `runeDeck`, `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone`, `championZone` | 后续打出、召出、抽牌、公开/私密/隐秘信息边界。 |
| 对象状态 | `cardObjects[objectId].damage`, `cardObjects[objectId].power`, `cardObjects[objectId].untilEndOfTurnPowerModifier`, `cardObjects[objectId].untilEndOfTurnEffects`, `cardObjects[objectId].isFaceDown`, `cardObjects[objectId].isAttacking`, `cardObjects[objectId].isDefending`, `cardObjects[objectId].isExhausted`, `cardObjects[objectId].tags` | 回合结束特殊清理移除伤害、令本回合内效果和战力修正失效；`power` 先服务伤害达到战力后的摧毁判定；`isFaceDown` 先服务待命/隐藏信息条件效果的规则前提；`isAttacking` 先服务进攻方单位目标限制；`isDefending` 先服务防守方条件战力修正；`isExhausted` 先服务“变为活跃/休眠状态”和后续横置成本；`tags` 先服务单位牌/属性/关键词式对象校验和指示物标记，后续扩展控制者、附属关系、战斗状态等完整对象字段。 |
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
| `EQUIPMENT_DESTROYED` | 法术/技能效果摧毁装备，并将其从场上移入拥有者废牌堆。 |
| `EQUIPMENT_RETURNED_TO_HAND` | 法术或技能让场上装备返回其拥有者手牌，并移除公开对象状态。 |
| `UNIT_BANISHED` | 法术/技能效果或摧毁替代效果将单位从场上移入拥有者放逐区。 |
| `CARDS_BANISHED` | 法术或技能将非场上区域的一批卡牌移入拥有者放逐区。 |
| `UNIT_PLAYED_TO_BASE` | 卡牌结算让单位源牌或非场上单位进入/重新打出到拥有者基地。 |
| `UNIT_PLAYED_TO_BATTLEFIELD` | 法术或技能让单位从非场上区域重新打出到战场。 |
| `UNIT_RECALLED_TO_BASE` | 摧毁被替代效果改写时，将单位以休眠状态召回拥有者基地。 |
| `UNIT_CONTROL_GAINED` | 法术或技能让一个敌方单位进入来源控制者控制区域，当前以区域归属表达控制权。 |
| `UNIT_TOKEN_CREATED` | 法术或技能打出/生成单位指示物到指定区域。 |
| `EQUIPMENT_TOKEN_CREATED` | 法术或技能打出/生成装备指示物到指定区域。 |
| `OBJECT_TAG_ADDED` | 法术或技能让一个卡牌对象获得持久对象标签。 |
| `BOON_GRANTED` | 法术或技能给予未拥有增益的单位 `增益` 标签，并让其基础战力永久 +1。 |
| `CARDS_RECYCLED` | 法术或技能让所选卡牌从废牌堆或主牌堆顶部选择窗口回到拥有者主牌堆底部。 |
| `UNIT_RETURNED_TO_DECK` | 法术或技能让场上单位放回拥有者主牌堆顶部或底部，并移除公开对象状态。 |
| `UNIT_MOVED_TO_UNIT_LOCATION` | 法术或技能让一个场上单位移动到另一个场上单位所在的公开区域位置。 |
| `UNIT_MOVED_TO_BATTLEFIELD` | 法术或技能让基地单位移动到当前单战场区域。 |
| `UNIT_LOCATIONS_SWAPPED` | 法术或技能让两名场上/基地单位分别移动到对方所在的公开区域位置。 |
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
| `p2-preflight-play-lotus-trap-doubles-next-damage` | P1 打出官方法术《莲花陷阱》后同回合用《焚烧》伤害同一目标 | 《莲花陷阱》支付 2 点费用并对目标施加 `DAMAGE_RECEIVED_DOUBLED_THIS_TURN`；随后《焚烧》的 2 点伤害按卡面翻倍为 4 点。 | `CATALOG` UNL-013/219, OGS·003/024; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-counterstorm-prevent-next-damage` | P1 打出官方法术《反击风暴》后同回合用《焚烧》伤害同一目标 | 《反击风暴》支付 2 点费用并对目标施加 `PREVENT_NEXT_DAMAGE_THIS_TURN`，然后抽 1 张牌；随后《焚烧》的 2 点伤害被抵挡为 0，且抵挡效果被消耗。 | `CATALOG` SFD·194/221, OGS·003/024; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-noxian-guillotine-next-damage-destroys` | P1 打出官方法术《诺克萨斯断头台》后同回合用《焚烧》伤害同一目标 | 《诺克萨斯断头台》支付 4 点费用并对目标施加 `DESTROY_ON_NEXT_DAMAGE_THIS_TURN`；随后《焚烧》的 2 点非致命伤害触发摧毁，目标进入拥有者废牌堆。`鼓舞` 立即摧毁分支暂缓。 | `CATALOG` OGN·254/298, OGS·003/024; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-imperial-decree-damage-destroys-unit` | P1 打出官方法术《帝国谕令》后同回合用《焚烧》伤害敌方战场单位 | 《帝国谕令》支付 5 点费用并给当前场上单位施加 `DESTROY_ON_NEXT_DAMAGE_THIS_TURN`；随后《焚烧》的 2 点非致命伤害触发摧毁。后续新进场单位暂缓到全局持续效果模型。 | `CATALOG` OGN·221/298, OGS·003/024; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p62-p63 rule 428 |
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
| `p2-preflight-play-portalpalooza-other-chooses-cards` | P1 打出官方法术《次元门狂欢》，当前 2P preflight 中 P2 选择卡牌 | `PLAY_CARD.mode = OTHER_PLAYERS_CHOOSE_CARDS`，支付 3 点费用，0 目标入栈；双方让过后 P1 与 P2 各抽 1 张牌。多人逐玩家选择暂缓。 | `CATALOG` OGN·071/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-portalpalooza-other-chooses-runes` | P1 打出官方法术《次元门狂欢》，当前 2P preflight 中 P2 选择符文 | `PLAY_CARD.mode = OTHER_PLAYERS_CHOOSE_RUNES`，支付 3 点费用，0 目标入栈；双方让过后 P1 与 P2 各召出 1 枚休眠符文，并记录 `isExhausted = true`。多人逐玩家选择暂缓。 | `CATALOG` OGN·071/298; `CORE-260330` p20 rules 164-167; p39-p42 rules 355-356 |
| `p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune` | P1 打出官方法术《牺牲》，并摧毁一名友方强力单位作为额外费用 | 支付 1 点费用，先摧毁战力至少 5 的友方单位并计入本回合摧毁记忆，再入栈；双方让过后先抽 2 张牌，再召出一枚休眠符文。 | `CATALOG` UNL-173/219; `CORE-260330` p14-p15 rules 142-143; p20 rules 164-167; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw` | P1 打出官方法术《断魂一扼》，目标为两名友方单位 | 支付 2 点费用，双方让过后按目标顺序先摧毁第一名友方单位，再让第二名友方单位本回合内获得等同于被摧毁单位当前战力的 +战力加成，最后抽 1 张牌。 | `CATALOG` SFD·163/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-play-vengeance-destroy-unit-stack` | P1 打出官方法术《复仇》，目标为战场上的一名单位 | 支付 4 点费用，卡牌入栈，双方让过后摧毁目标单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGN·229/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit` | P1 打出官方专属法术《涌泉之恨》，目标为 4 战力战场单位 | 支付 4 点费用，卡牌入栈，双方让过后摧毁目标单位；目标高于 3 战力，因此不进入符能再打出分支。 | `CATALOG` UNL-186/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-detonation-destroy-battlefield-unit-stack` | P1 打出官方法术《爆能术》，目标为战场上的一名单位 | 支付 6 点费用，卡牌入栈，双方让过后摧毁战场单位，将其移入拥有者废牌堆并移除场上对象状态。 | `CATALOG` OGS·012/024; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit` | P1 打出官方法术《狩魂》，目标为 3 战力战场单位 | 支付 2 点费用，目标必须是战场上不高于 3 战力的单位，双方让过后摧毁目标；4 战力目标有直接拒绝测试覆盖。 | `CATALOG` UNL-159/219; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-darkin-blade-destroy-target-controller-draw` | P1 打出官方法术《暗刃》，目标为 P2 战场单位 | 支付 2 点费用，双方让过后摧毁目标单位并让该单位控制者 P2 抽 2 张牌；待命路径暂缓。 | `CATALOG` OGN·213/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-housecleaning-destroy-each-player-unit` | P1 打出官方法术《清理门户》，每名玩家各选择一名自己的单位 | 支付 2 点费用，目标顺序记录为 P1 友方单位和 P2 敌方单位；双方让过后按顺序摧毁两名目标并移入各自拥有者废牌堆。反向顺序和两个友方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·209/298; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-kings-edict-destroy-enemy-unit` | P1 打出官方法术《国王诏令》，P2 选择一个不受 P1 控制的单位 | 支付 6 点费用，当前 2P preflight 用一个敌方单位目标记录 P2 的选择；双方让过后摧毁该单位。友方单位目标由直接拒绝测试覆盖，多人逐玩家选择暂缓。 | `CATALOG` OGN·237/298; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-spirit-fire-destroy-total-power-four` | P1 打出官方法术《妖异狐火》，选择总战力不高于 4 的战场单位 | 支付 3 点费用，目标数量为任意数量但所选目标总战力不得高于 4；双方让过后摧毁所选单位。总战力超过 4 的组合由直接拒绝测试覆盖，一处战场精确位置暂缓。 | `CATALOG` OGN·256/298; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack` | P1 从手牌打出官方法术《流沙陷坑》，目标为战场上的一名单位 | 支付 5 点费用，卡牌入栈，双方让过后摧毁战场单位；从非手牌位置打出的减费路径暂缓。 | `CATALOG` SFD·164/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p62-p63 rule 428 |
| `p2-preflight-play-ruination-destroy-all-units` | P1 打出官方法术《破败之咒》 | 支付 9 点费用，0 目标入栈，双方让过后摧毁所有当前场上单位；当前最小模型覆盖基地和战场区域中的单位。 | `CATALOG` UNL-180/219; `CORE-260330` p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-undertow-return-all-units` | P1 打出官方法术《坠渊之流》 | 支付 8 点费用，0 目标入栈，双方让过后让所有当前场上单位和装备返回所属者手牌，并移除公开对象状态。 | `CATALOG` SFD·147/221; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-reprimand-return-battlefield-unit` | P1 打出官方法术《责退》 | 支付 2 点费用，选择战场上的一名单位，双方让过后让目标返回所属者手牌，并移除场上对象状态。 | `CATALOG` OGN·172/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-gust-return-small-battlefield-unit` | P1 打出官方法术《罡风》 | 支付 1 点费用，目标必须是战场上不高于 3 战力的单位，双方让过后让目标返回所属者手牌；4 战力目标由直接拒绝测试覆盖。 | `CATALOG` OGN·169/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-reconsider-return-friendly-call-rune` | P1 打出官方法术《择日再战》 | 支付 1 点费用，目标必须是一名友方单位；双方让过后让目标返回所属者手牌，再让其拥有者召出一枚休眠符文并记录 `isExhausted = true`；敌方单位目标由直接测试拒绝。 | `CATALOG` OGN·104/298; `CORE-260330` p4-p8 rules 107-129; p20 rules 164-167; p39-p42 rules 355-356 |
| `p2-preflight-play-happenstance-return-friendly-and-enemy` | P1 打出官方法术《造化弄人》 | 支付 3 点费用，目标顺序必须是一名友方单位再一名敌方单位；双方让过后两个目标分别返回所属者手牌；目标顺序反转由直接拒绝测试覆盖。 | `CATALOG` UNL-128/219; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-hurricane-sweep-each-player-return-unit` | P1 打出官方法术《飓风席卷》，P2 和 P1 各选择一名单位 | 支付 4 点费用，当前 2P preflight 用 0-2 个单位目标按座位选择顺序记录玩家选择；双方让过后让所选单位返回所属者手牌。重复目标由直接拒绝测试覆盖，逐玩家 prompt 暂缓。 | `CATALOG` OGN·187/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-preflight-play-custodian-judgment-unit-to-deck-top` | P1 打出官方法术《持卫的裁决》，拥有者选择牌堆顶部 | 支付 2 点费用，目标必须是敌方战场单位；`PLAY_CARD.mode = OWNER_MAIN_DECK_TOP` 记录拥有者选择顶部，双方让过后将目标放到拥有者主牌堆顶部并移除公开对象状态。 | `CATALOG` UNL-204/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-preflight-play-custodian-judgment-unit-to-deck-bottom` | P1 打出官方法术《持卫的裁决》，拥有者选择牌堆底部 | 支付 2 点费用，目标必须是敌方战场单位；`PLAY_CARD.mode = OWNER_MAIN_DECK_BOTTOM` 记录拥有者选择底部，双方让过后将目标放到拥有者主牌堆底部并移除公开对象状态。缺失模式、友方单位和基地单位目标由直接拒绝测试覆盖。 | `CATALOG` UNL-204/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base` | P1 打出官方法术《战或逃》 | 支付 2 点费用，目标必须是战场上的单位，双方让过后让目标移动到所属者基地，并保留对象状态。 | `CATALOG` OGN·168/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready` | P1 打出官方法术《驭风而行》 | 支付 2 点费用，当前代表路径要求目标为友方战场单位；双方让过后让目标移动到所属者基地并变为活跃状态，保留对象状态。完整目的地选择暂缓到多位置移动模型。 | `CATALOG` OGN·173/298; `CORE-260330` p4-p8 rules 107-129; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-charm-move-enemy-battlefield-unit-to-base` | P1 打出官方法术《魅惑妖术》 | 支付 1 点费用，当前代表路径要求目标为敌方战场单位，双方让过后让目标移动到所属者基地，并保留对象状态；完整目的地选择暂缓到多位置移动模型。 | `CATALOG` OGN·043/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base` | P1 打出官方法术《升龙踢》，未启用等级 6 | 支付 2 点费用，当前代表路径要求目标为敌方战场单位，双方让过后让目标移动到所属者基地，并保留对象状态；完整目的地选择和等级 6 眩晕暂缓。 | `CATALOG` UNL-038/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base` | P1 打出官方法术《隔绝》 | 支付 2 点费用，目标必须是敌方战场单位，双方让过后将目标移动到所属者基地；当前 fixture 锁定移动后无落单敌方单位残留，因此不抽牌。落单抽牌分支暂缓到多战场位置/孤立判定模型。 | `CATALOG` UNL-124/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base` | P1 打出官方法术《闪现》 | 支付 2 点费用，选择最多两名友方战场单位，双方让过后让这些目标移动到基地，并保留对象状态；敌方单位和友方基地单位目标由直接拒绝测试覆盖。 | `CATALOG` OGS·011/024; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base` | P1 从手牌打出官方法术《禁军之墙》 | 支付 2 点费用，当前单战场区域模型下按己方战场单位数量动态允许选择 0 到全部友方战场单位，双方让过后让所选目标移动到基地，并保留对象状态；敌方单位、友方基地单位和重复目标由直接拒绝测试覆盖，待命/迅捷窗口细节暂缓。 | `CATALOG` SFD·043/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-playful-tentacles-move-total-power-eight` | P1 从手牌打出官方法术《顽皮触手》 | 支付 4 点费用，当前 2P 单战场区域模型下选择总战力不高于 8 的敌方战场单位，双方让过后让所选目标移动到所属者基地；总战力超过 8 的组合由直接拒绝测试覆盖，同一位置目的地和多控制者约束暂缓。 | `CATALOG` UNL-054/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-bait-move-enemy-unit-to-another-location` | P1 从手牌打出官方法术《诱饵》，不支付回响 | 支付 2 点费用，当前粗粒度位置模型下按顺序选择一名敌方单位和另一名敌方单位；双方让过后将第一目标移动到第二目标所在公开区域位置，并保留对象状态；友方目标和重复目标由直接拒绝测试覆盖，回响与多战场精确位置暂缓。 | `CATALOG` SFD·129/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-dragons-rage-move-then-mutual-damage` | P1 打出官方专属法术《猛龙摆尾》，按顺序选择两名敌方单位 | 支付 4 点费用，第一目标移动到第二目标所在公开区域位置，随后两名目标按自身当前战力互相造成伤害；样例中第二目标被致命伤害摧毁。友方目标和重复目标由直接拒绝测试覆盖，多战场精确目的地暂缓。 | `CATALOG` OGN·258/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-ruthless-pursuit-move-friendly-unit-recall-mark` | P1 打出官方法术《冷酷追击》，选择一名友方单位 | 支付 2 点费用，目标必须是带 `CARD_TYPE:UNIT` 标签的友方场上单位；双方让过后将该单位移动到所属基地，并给予 `MAY_RETURN_TO_BASE_ON_CONQUER_THIS_TURN` 本回合状态标记。敌方目标和非单位友方对象由直接拒绝测试覆盖，可选贴附武装和征服后触发结算暂缓。 | `CATALOG` SFD·184/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-the-curtain-rises-echo-ready-unit` | P1 打出官方法术《大幕渐起》并支付回响 | 支付基础 2 点和回响 2 点费用，选择一名单位，加入结算链，双方让过后重复“变为活跃状态”效果一次。 | `CATALOG` UNL-009/219; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-the-curtain-rises-ready-unit` | P1 打出官方法术《大幕渐起》不支付回响 | 支付 2 点费用，选择一名单位，加入结算链，双方让过后让目标变为活跃状态一次。 | `CATALOG` UNL-009/219; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
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
| `p2-preflight-play-last-breath-ready-damage-enemy-battlefield` | P1 打出官方法术《狂风绝息斩》，选择友方单位和敌方战场单位 | 支付 3 点费用，双方让过后先让友方目标变为活跃状态，再对敌方战场单位造成等同于该友方单位当前战力的伤害；目标顺序错误和敌方基地单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·260/298; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-convergent-mutation-match-friendly-power` | P1 打出官方法术《聚合变异》，选择两名友方单位 | 支付 2 点费用，目标必须是两名不同友方单位；双方让过后若第一目标战力低于第二目标，则第一目标本回合内变为第二目标当前战力。敌方目标和重复目标由直接拒绝测试覆盖。 | `CATALOG` OGN·108/298; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-switcheroo-swap-battlefield-unit-powers` | P1 打出官方法术《换换乐》，选择两名战场单位 | 支付 2 点费用，目标必须是两名不同战场单位；双方让过后用本回合内战力修正互换两名目标的当前战力。同一处战场约束暂按当前单战场区域模型处理。 | `CATALOG` SFD·145/221; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-clash-of-giants-mutual-power-damage` | P1 打出官方法术《巨人之战》，选择任意两名单位 | 支付 6 点费用，双方让过后基地单位和战场单位以自身战力互相造成伤害；敌方 3 战力单位受到 5 点伤害后被摧毁。 | `CATALOG` UNL-110/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-icathian-rain-can-hit-same-unit-six-times` | P1 打出官方法术《艾卡西亚暴雨》，六次伤害均选择同一战场单位 | 支付 7 点费用，同一目标保留六次选择，累计受到 12 点伤害后被摧毁。 | `CATALOG` OGN·248/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-blade-whirlwind-damage-all-battlefield-units` | P1 打出官方法术《剑刃飓风》 | 支付 1 点费用，0 目标入栈，双方让过后对所有战场上的单位各造成 1 点伤害，不分敌我；当前样例锁定未致命伤害路径。 | `CATALOG` OGN·133/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 323-324 |
| `p2-preflight-blade-whirlwind-lethal-damage-destroys-units` | P1 打出《剑刃飓风》，双方战场各有 1 战力单位 | 全战场 1 点伤害同时达到多个单位战力，结算后按致命伤害摧毁这些单位，并移入各自拥有者废牌堆。 | `CATALOG` OGN·133/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-cannon-barrage-damage-enemy-combat-units` | P1 打出官方法术《加农炮幕》 | 支付 2 点费用，0 目标入栈，双方让过后仅对战斗中的敌方单位各造成 2 点伤害；友方战斗中单位和敌方非战斗单位不受影响。 | `CATALOG` OGN·127/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 323-324 |
| `p2-preflight-play-production-surge-create-robot-draw` | P1 打出官方法术《产量激增》，未控制“机械”属性单位 | 支付 4 点费用，0 目标入栈，双方让过后打出一名 3 战力“机器人”单位指示物到 P1 基地，然后抽 1 张牌。 | `CATALOG` SFD·076/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-production-surge-reduced-by-mechanical` | P1 控制一名带 `tags = ["机械"]` 的单位后打出《产量激增》 | 费用从 4 减到 2；支付 2 点费用后仍打出 3 战力“机器人”到基地并抽 1 张牌。 | `CATALOG` SFD·076/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-common-cause-create-four-minions-base` | P1 打出官方法术《共同献身》，选择基地目的地 | `PLAY_CARD.mode = "BASE"`，支付 6 点费用，0 目标入栈，双方让过后打出四名 1 战力“随从”到 P1 基地；已锁定同一源牌的 token ID 顺序。 | `CATALOG` OGS·015/024; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-featherstorm-create-warhawks` | P1 打出官方法术《羽毛旋风》，选择战鹰模式 | `PLAY_CARD.mode = "CREATE_WARHAWKS"`，支付 4 点费用，0 目标入栈，双方让过后打出四名 1 战力“战鹰”到 P1 基地，并记录 `tags = ["法盾"]`；无效化法术模式和法盾额外选取费用模型暂缓。 | `CATALOG` UNL-044/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base` | P1 打出官方法术《点沙成兵》并支付回响 | 支付基础 2 点和回响 2 点费用，0 目标入栈，双方让过后重复打出单位指示物效果，共打出两名 2 战力“黄沙士兵”到 P1 基地；已锁定同一源牌的 token ID 顺序。 | `CATALOG` SFD·031/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sandcraft-create-one-sand-soldier-base` | P1 打出官方法术《点沙成兵》不支付回响 | 支付 2 点费用，0 目标入栈，双方让过后打出一名 2 战力“黄沙士兵”到 P1 基地；支付回响重复创建路径由既有 fixture 覆盖。 | `CATALOG` SFD·031/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-protect-the-emperor-create-sand-soldier` | P1 从手牌打出官方法术《护驾！》，不支付黄色可选费用 | 支付 3 点费用，0 目标入栈，双方让过后打出一名 2 战力“黄沙士兵”到 P1 基地；待命和支付黄色让其变为活跃状态的分支暂缓。 | `CATALOG` SFD·154/221; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356 |
| `p2-preflight-play-sand-soldiers-rise-ready-two` | P1 打出官方专属法术《沙兵现身》，当前控制 0 件武装 | 支付 6 点费用，目标必须是两名带 `黄沙士兵` 标签的友方单位；双方让过后不创建新单位，并让两名目标变为活跃状态。按武装数量创建黄沙士兵暂缓。 | `CATALOG` SFD·198/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sprite-summon-create-sprite-base` | P1 从手牌打出官方法术《精灵召唤》 | 支付 3 点费用，0 目标入栈，双方让过后在当前目的地受限代表路径下打出一名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到 P1 基地；完整目的地选择和瞬息到期摧毁暂缓。 | `CATALOG` OGN·094/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sprite-burst-create-two-sprites-base` | P1 从手牌打出官方法术《精灵迸发》 | 支付 5 点费用，0 目标入栈，双方让过后在当前目的地受限代表路径下打出两名 3 战力且带 `tags = ["瞬息"]` 的“精灵”到 P1 基地；完整目的地选择和瞬息到期摧毁暂缓。 | `CATALOG` UNL-069/219; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mirror-image-copy-ephemeral-base` | P1 从手牌打出官方专属法术《镜花水月》 | 支付 3 点费用，目标必须是一名场上单位；双方让过后在 P1 基地打出一名活跃“映像”，当前对象模型下复制目标战力与标签，并额外获得 `瞬息`。完整复制牌面、不触发打出效果和瞬息到期摧毁暂缓。 | `CATALOG` UNL-200/219, UNL·T06; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-stay-away-stun-draw-stack` | P1 从手牌打出官方法术《走开》 | 支付 3 点费用，眩晕一名单位，然后因从手牌打出而抽 1 张牌。 | `CATALOG` UNL-042/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-disposal-order-draw-mode` | P1 打出官方法术《处置命令》并选择抽牌模式 | `PLAY_CARD.mode = DRAW_1`，支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-disposal-order-recycle-opponent-graveyard` | P1 打出官方法术《处置命令》并选择对手废牌堆回收模式 | `PLAY_CARD.mode = RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3`，选择对手废牌堆中最多三张牌，双方让过后让其拥有者回收；多张回收到主牌堆底部时用 seed/rngCursor 生成可回放随机顺序。 | `CATALOG` UNL-103/219; `CORE-260330` p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card` | P1 打出官方法术《暗中破坏》，选择对手手牌中的非单位牌 | 支付 1 点费用，目标必须是对手手牌且不能带 `CARD_TYPE:UNIT` 标签；双方让过后将所选牌从对手手牌回收到其主牌堆底部。单位牌目标由直接拒绝测试覆盖；展示手牌的玩家视角提示细节暂缓。 | `CATALOG` OGN·156/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-predictive-offensive-draw-one-recycle-other` | P1 打出官方法术《预判攻势》，不支付回响 | 支付 0 点费用，选择己方主牌堆顶部两张牌中的一张作为抽取目标；双方让过后该牌加入手牌，另一张回收到主牌堆底部。顶部两张以外目标由直接拒绝测试覆盖，回响紫色路径暂缓。 | `CATALOG` SFD·122/221; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p58-p59 rule 416 |
| `p2-preflight-play-card-trick-draw-one-recycle-rest` | P1 打出官方法术《卡牌骗术》 | 支付 1 点费用，选择己方主牌堆顶部三张牌中的一张作为抽取目标；双方让过后该牌加入手牌，其余两张回收到主牌堆底部。顶部三张以外目标由直接拒绝测试覆盖。 | `CATALOG` OGN·183/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p58-p59 rule 416 |
| `p2-preflight-play-dragon-tiger-draw-unit-recycle-rest` | P1 打出官方法术《龙虎双雄》，不支付回响 | 支付 2 点费用，选择己方主牌堆顶部三张牌中一张带 `CARD_TYPE:UNIT` 对象标签的单位牌；双方让过后该牌加入手牌，其余两张回收到主牌堆底部。非单位目标和顶部三张以外目标由直接拒绝测试覆盖；回响 2 路径暂缓。 | `CATALOG` UNL-032/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p58-p59 rule 416 |
| `p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all` | P1 打出官方法术《龙虎双雄》，不支付回响且不选择单位牌 | 支付 2 点费用，目标列表为空；双方让过后不抽牌，并将已查看的主牌堆顶部三张牌回收到主牌堆底部。 | `CATALOG` UNL-032/219; `CORE-260330` p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-reinforcements-no-selection-recycle-top-five` | P1 打出官方法术《增援》，不选择单位牌 | 支付 5 点费用，目标列表为空；双方让过后不抽牌，并将已查看的主牌堆顶部五张牌回收到主牌堆底部。从牌堆打出单位并减费 5 的分支暂缓。 | `CATALOG` OGN·062/298; `CORE-260330` p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-meditation-draw-stack` | P1 打出官方法术《冥想》，不支付休眠友方单位的额外费用 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` OGN·048/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-salvage-draw-no-equipment` | P1 打出官方法术《废物利用》，不选择装备目标 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌。 | `CATALOG` OGN·224/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-salvage-destroy-equipment-draw` | P1 打出官方法术《废物利用》，选择一件装备 | 支付 2 点费用，目标必须是场上或基地中带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；双方让过后先摧毁该装备，再由来源控制者抽 1 张牌。单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·224/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields` | P1 打出官方法术《占山为王》，当前没有已控制战场 | 支付 3 点费用，0 目标入栈，双方让过后只抽基础 1 张牌；按控制战场数量额外抽牌暂缓至战场控制模型落地。 | `CATALOG` UNL-015/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-meditation-exhaust-friendly-extra-draw` | P1 打出官方法术《冥想》，让一名活跃友方单位休眠作为额外费用 | 支付 2 点费用，先记录友方单位变为休眠，再入栈；双方让过后共抽 2 张牌。 | `CATALOG` OGN·048/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-moonsilver-gift-discard-draw` | P1 打出官方法术《月神恩赐》，选择另一张友方手牌弃置 | 支付 3 点费用，选择友方手牌目标；双方让过后先弃置目标到废牌堆，再抽 2 张牌。 | `CATALOG` UNL-125/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-sinful-pleasure-discard-damage` | P1 打出官方法术《罪恶快感》，选择一张友方手牌和战场单位 | 支付 2 点费用，第一目标必须是另一张友方手牌，第二目标必须是战场单位；双方让过后先弃置手牌，再按被弃牌法力费用造成对应伤害。 | `CATALOG` OGN·008/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-revive-return-graveyard-unit` | P1 打出官方法术《亡者复生》，选择己方废牌堆单位牌 | 支付 2 点费用，选择己方废牌堆目标；双方让过后将该牌返回手牌，并拒绝对手废牌堆目标。 | `CATALOG` OGN·170/298; `CORE-260330` p39-p42 rules 355-356; p4-p8 rules 107-129 |
| `p2-preflight-play-harrowing-play-graveyard-unit-base` | P1 打出官方法术《蚀魂夜》，选择己方废牌堆单位牌 | 支付 6 点费用，目标必须是己方废牌堆中带 `CARD_TYPE:UNIT` 的单位牌；双方让过后将该牌打出到基地并变为活跃状态。非单位目标由直接拒绝测试覆盖，完整目的地选择暂缓。 | `CATALOG` OGN·198/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-steadfast-loyalty-graveyard-unit-base` | P1 打出官方法术《忠诚不渝》，选择己方废牌堆 2 费单位牌 | 支付 2 点费用，目标必须是己方废牌堆中带 `CARD_TYPE:UNIT` 且 `manaCost <= 2` 的单位牌；双方让过后将该牌打出到基地并变为活跃状态。非单位和费用过高目标由直接拒绝测试覆盖，动物属性减费和完整目的地选择暂缓。 | `CATALOG` UNL-168/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-guerrilla-warfare-return-standby-graveyard` | P1 打出官方专属法术《游击战》，选择己方废牌堆两张待命牌 | 支付 2 点费用，选择 0-2 张己方废牌堆中带 `待命` 标签的牌；双方让过后将所选牌返回手牌。非待命目标由直接拒绝测试覆盖，本回合免费正面朝下布置待命牌权限暂缓。 | `CATALOG` OGN·264/298; `CORE-260330` p4-p8 rules 107-129; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-call-of-the-shadows-give-ephemeral-draw` | P1 打出官方法术《暗影的召唤》，选择友方非瞬息单位 | 支付 2 点费用，目标必须是未带 `瞬息` 标签的友方单位；双方让过后给予该单位 `瞬息` 对象标签并抽 2 张牌。已有 `瞬息` 目标由直接拒绝测试覆盖，开始阶段摧毁瞬息单位暂缓。 | `CATALOG` UNL-165/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-deadly-flourish-enemy-unit-damage` | P1 打出官方法术《致命华彩》，选择敌方单位 | 支付 4 点费用，目标必须是一名敌方单位；双方让过后造成 3 点非致命伤害。友方单位目标由直接拒绝测试覆盖，本回合摧毁后的休眠“金币”装备指示物触发暂缓。 | `CATALOG` UNL-073/219; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-flowing-time-mirror-battlefield-unit-ephemeral` | P1 打出官方法术《逝水如镜》，选择战场单位 | 支付 4 点费用，目标必须是一名战场单位；双方让过后给予该单位 `瞬息` 对象标签。基地单位目标由直接拒绝测试覆盖，下个回合开始阶段摧毁瞬息对象暂缓。 | `CATALOG` OGN·180/298; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-flowing-time-mirror-equipment-ephemeral` | P1 打出官方法术《逝水如镜》，选择一件装备 | 支付 4 点费用，目标必须是场上或基地中带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；双方让过后给予该装备 `瞬息` 对象标签。 | `CATALOG` OGN·180/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-ashes-to-ashes-equipment-ephemeral` | P1 打出官方法术《化为灰烬》，选择一件装备 | 支付 2 点费用，目标必须是场上或基地中带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；双方让过后给予该装备 `瞬息` 对象标签。单位目标由直接拒绝测试覆盖，开始阶段摧毁瞬息对象暂缓。 | `CATALOG` UNL-070/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sigil-burst-destroy-equipment-draw` | P1 打出官方法术《印爆术》，选择一件装备 | 支付 1 点费用，目标必须是场上或基地中带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；双方让过后摧毁该装备并让其控制者抽 2 张牌。单位目标由直接拒绝测试覆盖，装备摧毁不会写入本回合单位摧毁记忆。 | `CATALOG` SFD·005/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-emergency-recall-return-equipment` | P1 打出官方法术《紧急召回》，选择一件装备 | 支付 1 点费用，目标必须是场上或基地中带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；双方让过后让该装备返回其拥有者手牌，并移除公开对象状态。单位目标由直接拒绝测试覆盖。 | `CATALOG` SFD·135/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-thermogenic-beam-destroy-all-equipment` | P1 打出官方法术《热电光束》 | 支付 5 点费用，0 目标入栈；双方让过后摧毁当前场上所有装备，双方非装备单位不受影响，装备摧毁不写入本回合单位摧毁记忆。 | `CATALOG` OGN·022/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-broken-blades-rematch-destroy-each-player-equipment` | P1 打出官方法术《折戟再战》 | 支付 1 点费用，当前 2P preflight 用目标顺序记录 P1 自己的一件装备和 P2 自己的一件装备；双方让过后分别摧毁。单位目标由直接拒绝测试覆盖，逐玩家 prompt 暂缓。 | `CATALOG` OGN·179/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-back-against-wall-double-power-ephemeral` | P1 打出官方法术《背水一战》，选择友方单位 | 支付 3 点费用，目标必须是一名友方单位；双方让过后按目标当前战力施加等量本回合内 +战力修正，并给予 `瞬息` 对象标签。敌方单位目标由直接拒绝测试覆盖，迅捷时机和下个回合开始阶段摧毁瞬息单位暂缓。 | `CATALOG` OGN·069/298; `CORE-260330` p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-painful-payoff-damage-create-gold` | P1 打出官方法术《痛苦之酬》，选择战场单位 | 支付 3 点费用，目标必须是一名战场单位；双方让过后造成 3 点伤害，并在 P1 基地打出一枚休眠“金币”装备指示物，带 `CARD_TYPE:EQUIPMENT` 标签。基地单位目标由直接拒绝测试覆盖，待命时机和金币资源技能暂缓。 | `CATALOG` SFD·070/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p62-p63 rule 428; p89 rules 718-719 |
| `p2-preflight-play-jungle-ambush-create-gold` | P1 打出官方法术《丛林伏击》 | 支付 2 点费用，0 目标入栈；双方让过后在 P1 基地打出一枚休眠“金币”装备指示物，带 `CARD_TYPE:EQUIPMENT` 标签。本回合友方单位活跃进场的全局效果暂缓。 | `CATALOG` SFD·004/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p89 rules 718-719 |
| `p2-preflight-play-blood-money-destroy-enemy-small-unit-create-gold` | P1 打出官方法术《血钱》，目标为敌方 2 战力战场单位 | 支付 2 点费用，目标必须是战场上不高于 2 战力的单位；双方让过后摧毁敌方目标并在来源控制者基地打出一枚休眠“金币”装备指示物。3 战力目标由直接拒绝测试覆盖。 | `CATALOG` SFD·162/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428; p89 rules 718-719 |
| `p2-preflight-play-blood-money-destroy-friendly-small-unit-create-two-gold` | P1 打出官方法术《血钱》，目标为友方 1 战力战场单位 | 支付 2 点费用，双方让过后摧毁友方目标并在来源控制者基地打出两枚休眠“金币”装备指示物；装备指示物带 `CARD_TYPE:EQUIPMENT` 标签。 | `CATALOG` SFD·162/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428; p89 rules 718-719 |
| `p2-preflight-play-rewind-timeline-discard-hands-draw-four` | P1 打出官方法术《反转时间线》 | 支付 3 点费用，0 目标入栈；双方让过后每名玩家弃置当前手牌，然后各抽 4 张牌。 | `CATALOG` OGN·201/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-center-your-mind-draw-stack` | P1 打出官方法术《聚心凝神》，不满足等级减费 | 支付 5 点费用，0 目标入栈，双方让过后抽 2 张牌；等级 6/11 减费路径暂缓。 | `CATALOG` UNL-091/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-borrowed-history-draw-stack` | P1 从手牌打出官方法术《借鉴历史》 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌；待命/反应时机路径暂缓。 | `CATALOG` OGN·083/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-spoils-of-war-draw-stack` | P1 打出官方法术《以战养战》，本回合未摧毁敌方单位 | 支付 4 点费用，0 目标入栈，双方让过后抽 2 张牌。 | `CATALOG` OGN·144/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-assemble-the-ranks-draw` | P1 打出官方法术《集结部队》 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌；友方单位进场给予增益的全局触发暂缓。 | `CATALOG` SFD·166/221; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-call-to-action-draw` | P1 打出官方法术《迎敌号令》 | 支付 2 点费用，0 目标入栈，双方让过后抽 1 张牌；本回合单位活跃进场的全局效果暂缓。 | `CATALOG` OGN·129/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-secret-art-mercy-grant-boon` | P1 打出官方法术《秘奥义！慈悲度魂落》，选择一名未拥有增益的友方单位 | 支付 3 点费用，目标必须是友方单位；双方让过后给予目标 `增益` 标签并让其基础战力永久 +1。本回合所有增益额外 +1 的全局效果暂缓。 | `CATALOG` OGN·053/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-stunning-display-boon-move-base-unit` | P1 打出官方专属法术《叹为观止》，选择基地中的一名未拥有增益的友方单位 | 支付 1 点费用，目标必须是己方基地单位；双方让过后给予目标 `增益` 标签、让其基础战力永久 +1，并移动到当前单战场区域。 | `CATALOG` OGN·270/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-void-rush-draw-no-free-play` | P1 打出官方专属法术《虚空猛冲》，不选择免费打出展示牌 | 支付 2 点费用，0 目标入栈，双方让过后抽取两张主牌堆顶部展示牌；免费打出其中一张的分支暂缓。 | `CATALOG` SFD·188/221; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-open-action-grant-all-boons` | P1 打出官方法术《公开行动》，当前没有既有增益可消耗 | 支付 5 点费用，0 目标入栈；双方让过后给予所有友方单位 `增益` 标签，并让未拥有增益的友方单位基础战力永久 +1。消耗增益让单位活跃的分支暂缓。 | `CATALOG` OGN·153/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-reflections-swap-draw` | P1 打出官方法术《镜中幻影》，选择基地和战场中的两名友方单位，其中一名拥有 `瞬息` | 支付 2 点费用，目标必须是两名友方单位且至少一名拥有 `瞬息`；双方让过后两名单位互换公开区域位置，然后抽 1 张牌。无 `瞬息` 目标组合由直接拒绝测试覆盖，精确多战场位置暂缓。 | `CATALOG` UNL-083/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-thundering-drop-base-power-damage-move` | P1 打出官方专属法术《天声震落》，选择基地中的一名友方单位 | 支付 6 点费用，目标必须是己方基地单位；双方让过后按目标当前战力对当前单战场区域中的敌方战场单位造成伤害，然后将目标移动到该战场。 | `CATALOG` OGN·250/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-battle-command-move-friendly-and-opponent-unit` | P1 打出官方法术《战斗号令》，目标顺序记录友方单位和对手所选单位 | 支付 3 点费用，第一目标必须是友方单位，第二目标必须是敌方单位；双方让过后将两名基地单位移动到当前粗粒度战场区域。完整对手选择 prompt 和多战场精确位置暂缓。 | `CATALOG` UNL-101/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-void-assault-move-friendly-and-enemy-unit` | P1 打出官方专属法术《虚空来袭》，目标顺序记录友方单位和敌方单位 | 支付 2 点费用，第一目标必须是友方单位，第二目标必须是敌方单位，且两者都带 `CARD_TYPE:UNIT` 标签；双方让过后将两名基地单位移动到当前粗粒度战场区域。目标顺序反转由直接拒绝测试覆盖，战场控制/进攻方细节暂缓。 | `CATALOG` UNL-202/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-bullet-time-power-damage-enemy-battlefield` | P1 打出官方专属法术《弹幕时间》，支付 3 点符能 | 支付 1 点法力与 `SPEND_POWER:3`，0 目标入栈；双方让过后对当前单战场区域中的敌方战场单位各造成 3 点伤害。符能不足由直接拒绝测试覆盖。 | `CATALOG` OGN·268/298; `CORE-260330` p20 rules 164-167; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-portalpal-rescue-banish-play-base` | P1 打出官方法术《传送门大营救》，选择一名友方战场单位 | 支付 3 点费用，目标必须是友方单位；双方让过后放逐目标，再将其打出到所属基地，并清除场上伤害、本回合内战力修正和本回合内效果。敌方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·102/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-hunting-rhythm-banish-play-battlefield` | P1 打出官方专属法术《狩猎律动》，选择一名友方单位 | 支付 2 点费用，目标必须是友方单位；双方让过后放逐目标，再将其打出到当前粗粒度战场，并清除场上伤害、本回合内战力修正和本回合内效果。敌方目标由直接拒绝测试覆盖。 | `CATALOG` UNL-184/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed` | P1 本回合先用《复仇》摧毁 P2 单位，再打出《以战养战》 | 记录敌方单位拥有者被摧毁的本回合记忆；《以战养战》费用从 4 降到 2，并抽 2 张牌。 | `CATALOG` OGN·229/298, OGN·144/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p62-p63 rule 428 |
| `p2-preflight-play-practical-experience-power-plus-1` | P1 打出官方法术《实战经验》，未启用等级 6 升级 | 支付 1 点费用，加入结算链，双方让过后让一名单位本回合内战力 +1；等级 6 改为 +3 的路径暂缓。 | `CATALOG` UNL-031/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-dueling-stance-friendly-power-plus-1` | P1 打出官方法术《决斗架势》，目标为一名友方单位 | 支付 1 点费用，加入结算链，双方让过后让该友方单位本回合内战力 +1；“该处唯一控制单位”额外 +1 分支暂缓。 | `CATALOG` OGN·046/298; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-animal-friends-power-per-controlled-tag` | P1 打出官方法术《动物之友》，指定一名单位 | 支付 1 点费用，加入结算链，双方让过后按 P1 控制单位中“鸟类、猫科、犬形、魄罗”的不同标签种类数动态修正目标战力；样例中三种标签使目标本回合内战力 +3。 | `CATALOG` UNL-046/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit` | P1 打出官方法术《矢志不退》，指定一名友方战场单位 | 支付 2 点费用，目标必须是友方战场单位；双方让过后按敌方战场单位数量动态修正目标战力，样例中两个敌方战场单位使目标本回合内战力 +4。同一战场位置约束暂按当前单战场区域模型处理。 | `CATALOG` SFD·001/221; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
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
| `p2-preflight-play-eclipse-power-minus-4` | P1 打出官方法术《月蚀》，洞察时不回收顶部牌 | 支付 3 点费用，加入结算链，双方让过后让一名单位本回合内战力 -4；只选择单位目标表示洞察不回收顶部牌。 | `CATALOG` UNL-063/219; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-eclipse-power-minus-4-insight-recycle` | P1 打出官方法术《月蚀》，洞察时回收顶部牌 | 支付 3 点费用，按顺序选择一名单位和己方主牌堆顶部一张牌；双方让过后先让单位本回合内战力 -4，再将洞察选择的顶部牌回收到主牌堆底部。顶部一张以外目标由直接拒绝测试覆盖。 | `CATALOG` UNL-063/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-moonfall-power-minus-10` | P1 打出官方法术《月光之殇》 | 支付 7 点费用，加入结算链，双方让过后让一名单位本回合内战力 -10。 | `CATALOG` UNL-066/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-glory-call-power-plus-3` | P1 打出官方法术《荣耀召唤》，不支付消耗增益额外费用 | 支付 3 点费用，加入结算链，双方让过后让一名单位本回合内战力 +3；消耗增益以无视费用的路径暂缓。 | `CATALOG` OGN·207/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-last-stand-friendly-power-plus-3` | P1 打出官方法术《视死如归》指定友方单位 | 支付 2 点费用，加入结算链，双方让过后让目标友方单位本回合内战力 +3；本回合赢得战斗时获得 2 经验的触发路径暂缓。 | `CATALOG` UNL-095/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-decisive-strike-all-friendly-power-plus-2` | P1 打出官方法术《致命打击》 | 支付 5 点费用，0 目标入栈，双方让过后让 P1 所有场上友方单位本回合内战力 +2；对手单位不受影响。 | `CATALOG` OGS·024/024; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-grand-strategy-all-friendly-power-plus-5` | P1 打出官方法术《宏伟战略》 | 支付 6 点费用，0 目标入栈，双方让过后让 P1 所有场上友方单位本回合内战力 +5；对手单位不受影响。 | `CATALOG` OGN·233/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-back-to-back-two-friendly-power-plus-2` | P1 打出官方法术《背靠背》 | 支付 3 点费用，选择两名友方单位，加入结算链，双方让过后分别让目标本回合内战力 +2；敌方单位目标由直接测试拒绝。 | `CATALOG` OGN·206/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-power-bind-echo-two-friendly-power-plus-1` | P1 打出官方法术《力量之缚》并支付回响 | 支付基础 2 点和回响 2 点费用，选择两名友方单位，加入结算链，双方让过后重复“两名友方单位本回合内战力 +1”效果一次，两个目标各 +2。 | `CATALOG` SFD·151/221; `CORE-260330` p39-p42 rules 355-356; p92-p105 keyword rules 800+; p31-p33 rules 318-324 |
| `p2-preflight-play-danger-temperature-mechanical-power-plus-1` | P1 打出官方法术《危险温度》，不支付混合资源回响 | 支付 1 点费用，0 目标入栈，双方让过后只让 P1 场上带 `tags = ["机械"]` 的单位本回合内战力 +1；己方非机械和对手机械单位不受影响。 | `CATALOG` SFD·182/221; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-siphon-energy-battlefield-power-split` | P1 打出官方法术《虹吸能量》 | 支付 2 点费用，0 目标入栈，当前单战场区域模型下让友方战场单位本回合内战力 +1，敌方战场单位本回合内战力 -1 且不得低于 1；双方基地单位不受影响。 | `CATALOG` OGN·266/298; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-moonrise-enemy-battlefield-power-minus-2` | P1 打出官方法术《月之降临》 | 支付 3 点费用，0 目标入栈，当前单战场区域模型下跳过可选敌方单位移动，并让敌方战场单位本回合内战力 -2；友方战场单位和双方基地单位不受影响。 | `CATALOG` UNL-198/219; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-smoke-bomb-power-floor-stack` | P1 打出官方法术《烟幕弹》，目标为 3 战力单位 | 支付 2 点费用，加入结算链，双方让过后尝试让目标本回合内战力 -4；因不得低于 1，实际应用 -2，目标战力变为 1。 | `CATALOG` OGN·093/298; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-smoke-bomb-power-floor-expires-end-turn` | 已存在《烟幕弹》式被下限截断的负战力修正，随后 P1 结束回合 | `END_TURN` 特殊清理移除实际应用的负战力修正，记录 `POWER_MODIFIER_EXPIRED`，目标恢复原战力。 | `CATALOG` OGN·093/298; `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-preflight-play-extortion-power-floor-draw-stack` | P1 打出官方法术《“敲”诈》，目标为 1 战力单位 | 支付 1 点费用，加入结算链，双方让过后尝试让目标本回合内战力 -1；因不得低于 1，实际应用 0，随后 P1 抽 1 张牌。 | `CATALOG` OGN·095/298; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4; p31-p33 rules 318-324 |
| `p2-play-stellar-convergence-two-target-damage-stack` | P1 打出官方法术《星芒凝汇》，目标为一名战场单位和一名基地单位 | 支付 6 点费用，卡牌入栈，双方让过后对每名目标各造成 6 点伤害并进入废牌堆；一目标路径有直接测试覆盖。 | `CATALOG` OGN·105/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-play-rocket-barrage-base-unit-mode-stack` | P1 打出官方法术《火箭轰击》，选择基地单位伤害模式 | `PLAY_CARD.mode = BASE_UNIT_DAMAGE_4`，支付 4 点费用，目标必须是基地中的单位，双方让过后造成 4 点伤害；`回响`暂缓。 | `CATALOG` SFD·077/221; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-rocket-barrage-destroy-equipment-mode` | P1 打出官方法术《火箭轰击》，选择摧毁装备模式 | `PLAY_CARD.mode = DESTROY_EQUIPMENT`，支付 4 点费用，目标必须是带 `CARD_TYPE:EQUIPMENT` 标签的场上装备，双方让过后摧毁该装备并移入拥有者废牌堆；单位目标由直接拒绝测试覆盖，`回响`暂缓。 | `CATALOG` SFD·077/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-poro-snax-equipment-draw` | P1 打出官方装备《魄罗佳肴》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，然后 P1 抽 1 张牌。带目标打出由直接拒绝测试覆盖，自毁激活抽牌技能暂缓。 | `CATALOG` SFD·046/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-shurelyas-requiem-equipment-ready-all` | P1 打出官方专属装备《舒瑞娅的安魂曲》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，然后让控制者所有单位变为活跃状态。带目标打出由直接拒绝测试覆盖，唯我和装配技能暂缓。 | `CATALOG` SFD·192/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-future-forge-equipment-create-minion` | P1 打出官方装备《未来熔炉》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，然后打出一名带 `CARD_TYPE:UNIT` 标签的 1 战力“随从”到控制者基地。带目标打出由直接拒绝测试覆盖，摧毁装备回收废牌堆分支暂缓。 | `CATALOG` OGN·212/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-treasure-golem-create-four-gold` | P1 打出官方单位《宝藏魔像》 | 支付 8 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，然后打出四个休眠的 `CARD_TYPE:EQUIPMENT` “金币”装备指示物。带目标打出由直接拒绝测试覆盖，完整目的地选择暂缓。 | `CATALOG` SFD·174/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-faithful-craftsman-create-minion` | P1 打出官方单位《忠实的工坊主》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后打出一名带 `CARD_TYPE:UNIT` 标签的 1 战力“随从”到控制者基地。带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。 | `CATALOG` OGN·211/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-royal-guard-create-sand-soldier` | P1 打出官方单位《皇家守卫》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后打出一名带 `CARD_TYPE:UNIT` 与 `黄沙士兵` 标签的 2 战力“黄沙士兵”到控制者基地。带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。 | `CATALOG` SFD·157/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-blueflame-guardian-power-plus-8` | P1 打出官方单位《苍炎守护者》 | 支付 8 点费用，选择一名带 `CARD_TYPE:UNIT` 标签的单位；双方让过后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，然后令目标本回合内战力 +8。非单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·082/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-blastcone-sprout-power-minus-2-floor` | P1 普通打出官方单位《爆裂球果仙灵》 | 支付 2 点费用，选择一名带 `CARD_TYPE:UNIT` 标签的单位；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后令目标本回合内战力 -2 且不得低于 1。非单位目标由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` OGN·097/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-prowling-hunter-create-warhawk` | P1 打出官方单位《调皮猎手》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后打出一名带 `CARD_TYPE:UNIT` 与 `法盾` 标签的 1 战力“战鹰”到控制者基地。带目标打出由直接拒绝测试覆盖，精确“此处”目的地暂缓。 | `CATALOG` UNL-033/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-apprentice-engineer-return-graveyard-equipment` | P1 打出官方单位《见习工程师》 | 支付 3 点费用，选择己方废牌堆一件带 `CARD_TYPE:EQUIPMENT` 标签的装备；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标装备返回手牌。非装备废牌堆目标由直接拒绝测试覆盖。 | `CATALOG` SFD·061/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-darkened-lurker-discard-draw` | P1 打出官方单位《永黯潜伏者》 | 支付 3 点费用，选择另一张己方手牌；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标手牌并抽 1 张牌。以源牌自身作为弃置目标由直接拒绝测试覆盖。 | `CATALOG` UNL-123/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-shepherd-dog-return-graveyard-unit` | P1 打出官方单位《牧灵犬》 | 支付 3 点费用，选择己方废牌堆一张带 `CARD_TYPE:UNIT` 标签的单位牌；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标单位牌返回手牌。非单位废牌堆目标由直接拒绝测试覆盖。 | `CATALOG` OGN·165/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-annie-return-graveyard-spell` | P1 打出官方单位《安妮》 | 支付 4 点费用，选择己方废牌堆一张带 `CARD_TYPE:SPELL` 标签的法术牌；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标法术牌返回手牌。非法术废牌堆目标由直接拒绝测试覆盖。 | `CATALOG` OGS·010/024; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-scuttle-crab-draw` | P1 打出官方单位《迅捷蟹》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 0 战力 `CARD_TYPE:UNIT` 单位对象，然后 P1 抽 1 张牌。带目标打出由直接拒绝测试覆盖，绝念分支暂缓。 | `CATALOG` UNL-053/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-yordle-instructor-draw` | P1 打出官方单位《约德尔教官》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 2 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，然后 P1 抽 1 张牌。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·087/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sprite-mother-create-sprite` | P1 打出官方单位《精灵之母》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后打出一名带 `瞬息` 标签的 3 战力“精灵”到控制者基地。带目标打出由直接拒绝测试覆盖，精确“此处”和瞬息到期暂缓。 | `CATALOG` OGN·106/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-megashark-cannon-damage-enemy-battlefield` | P1 打出官方单位《怒海大鲨炮》 | 支付 6 点费用，选择敌方战场单位；双方让过后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，然后对目标造成 6 点非致命伤害。友方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·092/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-preflight-play-quicksand-mage-destroy-small-enemy` | P1 打出官方单位《流沙术士》 | 支付 5 点费用，选择一名不高于 3 战力且带 `CARD_TYPE:UNIT` 标签的敌方单位；双方让过后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，然后摧毁目标。4 战力目标由直接拒绝测试覆盖。 | `CATALOG` SFD·158/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-zaun-bodyguard-return-battlefield-unit` | P1 打出官方单位《祖安保镖》 | 支付 4 点费用，选择战场上一名带 `CARD_TYPE:UNIT` 标签的单位；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标返回其拥有者手牌。基地目标由直接拒绝测试覆盖。 | `CATALOG` OGN·188/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-dragon-cavalry-destroy-enemy-unit` | P1 打出官方单位《龙骑兵》 | 支付 8 点费用，选择一名带 `CARD_TYPE:UNIT` 标签的敌方单位；双方让过后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，然后摧毁目标。友方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·234/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-first-mate-ready-unit` | P1 打出官方单位《大副》 | 支付 3 点费用，选择一名带 `CARD_TYPE:UNIT` 标签的单位；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后令目标变为活跃状态。非单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·132/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-arena-rookie-grant-boon` | P1 打出官方单位《竞技场新人》 | 支付 2 点费用，选择另一名友方单位；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后给予目标 `增益` 标签和永久 +1 战力。敌方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·136/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-sword-vagrant-destroy-equipment` | P1 打出官方单位《斩剑浪客》 | 支付 3 点费用，可选择一件装备；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后摧毁所选装备。不选择装备可正常入栈，单位目标由直接拒绝测试覆盖。 | `CATALOG` SFD·032/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-sun-shieldguard-stun-unit` | P1 打出官方单位《烈阳盾卫》 | 支付 3 点费用，选择一名带 `CARD_TYPE:UNIT` 标签的单位；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后对目标施加本回合内 `STUNNED`。非单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·051/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-thunderclaw-ursine-call-rune` | P1 打出官方单位《雷爪氏族熊人》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，然后从符文牌堆顶召出一枚休眠符文。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·137/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p20 rules 164-167; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kinkou-monk-grant-two-boons` | P1 打出官方单位《均衡僧侣》 | 支付 4 点费用，选择两名友方单位；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后分别给予两个目标 `增益` 标签和永久 +1 战力。敌方目标由直接拒绝测试覆盖。 | `CATALOG` OGN·141/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-gloomy-apothecary-return-friendly-battlefield` | P1 打出官方单位《阴森药剂师》 | 支付 3 点费用，可选择一名友方战场单位；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标返回手牌。不选择目标可正常入栈，敌方战场目标由直接拒绝测试覆盖，伏击路径暂缓。 | `CATALOG` UNL-021/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-windsong-wing-return-small-battlefield` | P1 打出官方单位《吟风翼》 | 支付 2 点费用，可选择一名不高于 3 战力的战场单位；双方让过后源牌进入控制者基地成为 1 战力、带 `待命` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让目标返回手牌。不选择目标可正常入栈，4 战力目标由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` SFD·138/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hexcore-disruptor-roam-unit` | P1 打出官方单位《晶能阻断器》 | 支付 2 点费用，选择一名单位；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标本回合内获得 `ROAM`。装备目标由直接拒绝测试覆盖。 | `CATALOG` SFD·007/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kadregrin-draw-powerful-units` | P1 打出官方单位《邪焰巨龙 卡德雷格林》 | 支付 9 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，然后按控制者场上强力单位数量抽牌。当前 fixture 中既有强力单位加自身共抽 2；带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·038/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-frenzied-raider-move-battlefield-unit-to-base` | P1 打出官方单位《疯狂海寇》 | 支付 5 点费用，选择一名战场单位；双方让过后源牌进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，然后将目标移动到其所属基地。基地目标由直接拒绝测试覆盖。 | `CATALOG` OGN·191/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-abyssal-behemoth-return-friendly-and-enemy` | P1 打出官方单位《海渊巨兽》 | 支付 7 点费用，按顺序选择另一名友方单位和一名敌方单位；双方让过后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，然后让两个目标分别返回所属者手牌。目标顺序反转由直接拒绝测试覆盖。 | `CATALOG` SFD·132/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-bubblebot-ready-friendly-mechanical` | P1 打出官方单位《泡泡机》 | 支付 3 点费用，选择另一名带 `CARD_TYPE:UNIT` 与 `机械` 标签的友方单位；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后令目标变为活跃状态。非机械单位由直接拒绝测试覆盖。 | `CATALOG` SFD·062/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-sprite-queen-create-sprite` | P1 打出官方单位《精灵女王》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，然后打出一名 3 战力且带 `瞬息` 标签的“精灵”到控制者基地。带目标打出由直接拒绝测试覆盖，开始阶段重复创建和瞬息到期暂缓。 | `CATALOG` UNL-084/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-faerie-dragon-grant-four-boons` | P1 打出官方单位《仙灵龙》 | 支付 7 点费用，选择最多四名友方单位；双方让过后源牌进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，然后分别给予目标 `增益` 标签和永久 +1 战力。不选择目标可正常入栈，敌方目标由直接拒绝测试覆盖，消耗增益打出金币分支暂缓。 | `CATALOG` SFD·101/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-ezreal-discard-draw-two` | P1 打出官方单位《伊泽瑞尔》 | 支付 3 点费用，选择另一张己方手牌；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标并抽 2 张牌。以源牌自身作为弃置目标由直接拒绝测试覆盖，其他卡牌的可选减费分支暂缓。 | `CATALOG` SFD·149/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-ezreal-alt-a-discard-draw-two` | P1 打出官方单位《伊泽瑞尔》A 版本 | 支付 3 点费用，选择另一张己方手牌；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标并抽 2 张牌。以源牌自身作为弃置目标由直接拒绝测试覆盖，其他卡牌的可选额外费用减免暂缓。 | `CATALOG` SFD·149a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-solari-leader-stun-enemy-unit` | P1 打出官方单位《烈阳首领》，目标为未眩晕敌方单位 | 支付 5 点费用，选择一名敌方单位；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后让目标获得本回合内 `STUNNED`。 | `CATALOG` OGN·225/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-solari-leader-destroy-stunned-enemy` | P1 打出官方单位《烈阳首领》，目标为已眩晕敌方单位 | 支付 5 点费用，选择一名已带 `STUNNED` 的敌方单位；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，目标改为被摧毁并进入拥有者废牌堆。友方单位目标由直接拒绝测试覆盖。 | `CATALOG` OGN·225/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p62-p63 rule 428 |
| `p2-preflight-play-buhru-captain-draw-mode` | P1 打出官方单位《芭茹队长》并选择抽牌模式 | 支付 3 点费用，选择 `DRAW_1` 模式且 0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后抽 1 张牌。缺失模式由直接拒绝测试覆盖，自身增益分支暂缓。 | `CATALOG` SFD·091/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-chempunk-tough-discard-hand` | P1 打出官方单位《炼金太保》 | 支付 2 点费用，选择另一张己方手牌；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标。以源牌自身作为弃置目标由直接拒绝测试覆盖，强攻战斗关键词暂缓。 | `CATALOG` OGN·003/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-jinx-discard-two-hand` | P1 打出官方单位《金克丝》 | 支付 3 点费用，选择另外两张己方手牌；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后按目标顺序弃置两张牌。目标包含源牌自身由直接拒绝测试覆盖，急速和强攻战斗关键词暂缓。 | `CATALOG` OGN·030/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-jinx-alt-a-discard-two-hand` | P1 打出官方单位《金克丝》A 版本 | 支付 3 点费用，选择另外两张己方手牌；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后按目标顺序弃置两张牌。目标包含源牌自身由直接拒绝测试覆盖，急速和强攻战斗关键词暂缓。 | `CATALOG` OGN·030a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-blast-crew-apprentice-no-optional-damage` | P1 打出官方单位《爆破队学员》，不支付可选额外费用 | 支付基础 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不造成伤害；有色额外费用与伤害分支暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·013/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-frostcoat-cub-no-optional-power-minus-two` | P1 打出官方单位《霜衣幼崽》，不支付可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付蓝色额外费用时不进行战力修正；有色额外费用与战力 -2 分支暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·067/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ship-monkey-no-optional-boon` | P1 打出官方单位《船猿》，不支付可选额外费用 | 支付基础 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 费用时不获得增益；额外费用与自身增益分支暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·098/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-pyke-no-optional-ready-power` | P1 打出官方单位《派克》，不支付红色可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `待命` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付红色额外费用时不执行条件活跃或战力 +2；红色额外费用、待命反应时机和游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-028/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-pyke-alt-a-no-optional-ready-power` | P1 打出官方单位《派克》A 版本，不支付红色可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `待命` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付红色额外费用时不执行条件活跃或战力 +2；红色额外费用、待命反应时机和游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-028a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-tiny-guardian-no-optional-draw` | P1 打出官方单位《小小守护者》，不支付绿色可选额外费用 | 支付基础 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象。不支付绿色额外费用时不抽牌；绿色额外费用与抽 1 分支暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·044/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-blazing-drake-no-optional-haste` | P1 打出官方单位《灼焰飞龙》，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `龙` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·001/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-legion-rearguard-no-optional-haste` | P1 打出官方单位《军团后卫》，不支付急速可选额外费用 | 支付基础 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `崔法利` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·010/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-baby-shark-no-optional-haste` | P1 打出官方单位《小鲨鱼》，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `急速` 与 `强攻4` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场和强攻战斗修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-006/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-reksai-no-optional-haste` | P1 打出官方英雄单位《雷克塞》，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `强攻` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场、强攻战斗修正和从手牌以外打出的友方单位获得急速路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·029/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-reksai-alt-a-no-optional-haste` | P1 打出官方英雄单位《雷克塞》A 版本，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `强攻` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场、强攻战斗修正和从手牌以外打出的友方单位获得急速路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·029a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kaisa-no-optional-haste` | P1 打出官方英雄单位《卡莎》，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场和征服抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·039/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kaisa-alt-a-no-optional-haste` | P1 打出官方英雄单位《卡莎》A 版本，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；急速活跃进场和征服抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·039a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-rengar-no-optional-haste` | P1 打出官方英雄单位《雷恩加尔》，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `强攻2`、`急速`、`法盾`、`游走` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；强攻战斗修正、法盾目标税和游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-024/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-rengar-alt-a-no-optional-haste` | P1 打出官方英雄单位《雷恩加尔》A 版本，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `强攻2`、`急速`、`法盾`、`游走` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；强攻战斗修正、法盾目标税和游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-024a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-nilah-no-optional-haste` | P1 打出官方英雄单位《尼菈》，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速`、`恶魔` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；游走移动和移动获得经验触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-115/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-miss-fortune-no-optional-haste` | P1 打出官方英雄单位《厄运小姐》，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `急速`、`海盗` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；游走移动和移动触发变为活跃路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·162/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-miss-fortune-alt-a-no-optional-haste` | P1 打出官方英雄单位《厄运小姐》A 版本，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `急速`、`海盗` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；游走移动和移动触发变为活跃路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·162a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sivir-no-optional-haste` | P1 打出官方英雄单位《希维尔》，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和紫色费用时不执行急速活跃进场；本回合内至少支付两个万能符能后获得战力 +2 与游走的路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·143/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sivir-alt-a-no-optional-haste` | P1 打出官方英雄单位《希维尔》A 版本，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和紫色费用时不执行急速活跃进场；本回合内至少支付两个万能符能后获得战力 +2 与游走的路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·143a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-lillia-no-optional-haste` | P1 打出官方英雄单位《莉莉娅》，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和蓝色费用时不执行急速活跃进场；移动触发打出瞬息精灵和瞬息到期暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-082/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-lillia-alt-a-no-optional-haste` | P1 打出官方英雄单位《莉莉娅》A 版本，不支付急速可选额外费用 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和蓝色费用时不执行急速活跃进场；移动触发打出瞬息精灵和瞬息到期暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-082a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-azir-no-optional-haste` | P1 打出官方英雄单位《阿兹尔》，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 和 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和黄色费用时不执行急速活跃进场；进攻触发移动指示物单位路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·177/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-azir-alt-a-no-optional-haste` | P1 打出官方英雄单位《阿兹尔》A 版本，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 和 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和黄色费用时不执行急速活跃进场；进攻触发移动指示物单位路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·177a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mr-root-no-optional-haste` | P1 打出官方单位《树根先生》，不支付急速可选额外费用 | 支付基础 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和紫色费用时不执行急速活跃进场；移动和获得经验触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-127/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mech-maniac-no-optional-haste` | P1 打出官方单位《机械迷》，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和蓝色费用时不执行急速活跃进场；武装贴附和双倍基础战力加成路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·068/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-xersai-fish-no-optional-haste` | P1 打出官方单位《琢珥鱼》，不支付急速可选额外费用 | 支付基础 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；按强力单位减少费用路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·103/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-karina-veraze-no-optional-haste` | P1 打出官方单位《卡银娜·薇蕊泽》，不支付急速可选额外费用 | 支付基础 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和黄色费用时不执行急速活跃进场；移动触发打出三名随从指示物暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·179/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-crimson-signet-treant-no-optional-haste` | P1 打出官方单位《绯红印记树怪》，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；征服额外触发和征服后增益路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-029/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-crimson-signet-treant-alt-a-no-optional-haste` | P1 打出官方单位《绯红印记树怪》A 版本，不支付急速可选额外费用 | 支付基础 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；征服额外触发和征服后增益路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-029a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-tasty-faerie-no-optional-haste` | P1 打出官方单位《美味仙灵》，不支付急速可选额外费用 | 支付基础 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和绿色费用时不执行急速活跃进场；绝念召符文和抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·075/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-ekko-no-optional-haste` | P1 打出官方英雄单位《艾克》，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和蓝色费用时不执行急速活跃进场；绝念回收和符文活跃化路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·110/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-armed-assaulter-no-optional-haste` | P1 打出官方单位《武装强袭者》，不支付急速可选额外费用且不选择百炼装配 | 支付基础 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `急速` 和 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和红色费用时不执行急速活跃进场；百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·002/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-ancient-berserker-no-optional-haste` | P1 打出官方单位《远古战狂》，不支付急速可选额外费用 | 支付基础 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `急速` 和 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和紫色费用时不执行急速活跃进场；动态强攻数值路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·131/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-kraken-hunter-no-optional-haste` | P1 打出官方单位《海妖猎手》，不支付急速可选额外费用且不消耗增益 | 支付基础 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `强攻`、`急速` 和 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；强攻战斗修正和消耗增益减费路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·150/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-lee-sin-no-optional-haste` | P1 打出官方英雄单位《李青》，不支付急速可选额外费用 | 支付基础 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；战场静态战力修正路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·151/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-lee-sin-alt-a-no-optional-haste` | P1 打出官方英雄单位《李青》A 版本，不支付急速可选额外费用 | 支付基础 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象。不支付额外 1 和橙色费用时不执行急速活跃进场；战场静态战力修正路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·151a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3` | P1 打出官方单位《千尾监视者》，不支付急速可选额外费用 | 支付基础 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 7 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象，并让所有敌方单位本回合内战力 -3、不得低于 1。fixture 覆盖敌方基地单位、敌方战场单位、下限截断和友方单位不受影响；不支付额外 1 和蓝色费用时不执行急速活跃进场，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·116/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-plucky-poro-keyword-unit` | P1 打出官方单位《呸呸魄罗》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `法盾` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·013/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mighty-poro-keyword-unit` | P1 打出官方单位《强强魄罗》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `坚守` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。防守战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·052/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-assault-poro-keyword-unit` | P1 打出官方单位《莽莽魄罗》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `强攻` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻方战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·210/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-fierce-first-mate-keyword-unit` | P1 打出官方单位《躁烈的副官》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `强攻` 与 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻方战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·215/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-zephyr-sage-keyword-unit` | P1 打出官方单位《和风贤者》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `坚守` 与 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象。防守战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGS·005/024; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-pakaa-cub-keyword-unit` | P1 打出官方单位《帕卡幼崽》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `待命` 与 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象。待命正面朝下与反应打出路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·135/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-navori-scout-keyword-unit` | P1 打出官方单位《纳沃利侦察兵》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·037/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-laurent-swordsman-keyword-unit` | P1 打出官方单位《劳伦特剑使》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `强攻2` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻方战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·156/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-gluttonous-toadfrog-keyword-unit` | P1 打出官方单位《贪食魔沼蛙》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `狩猎3` 标签的 `CARD_TYPE:UNIT` 单位对象。征服/据守获得经验路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-100/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sentinel-adept-no-optional-assemble` | P1 打出官方单位《哨兵好手》，不选择百炼装配 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `哨兵` 与 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·008/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-battle-chef-no-optional-assemble` | P1 打出官方单位《战斗厨神》，不选择百炼装配 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·092/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-stout-poro-no-optional-assemble` | P1 打出官方单位《壮壮魄罗》，不选择百炼装配 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `百炼` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·099/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-master-bingwen-no-optional-assemble` | P1 打出官方单位《炳文大师》，不选择百炼装配 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·127/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-unl-plucky-poro-keyword-unit` | P1 打出官方单位《呸呸魄罗》UNL 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `法盾` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-220/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-unl-stout-poro-keyword-unit` | P1 打出官方单位《壮壮魄罗》UNL 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `百炼` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配与武装贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-223/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-unl-assault-poro-keyword-unit` | P1 打出官方单位《莽莽魄罗》UNL 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `强攻` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻方战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-225/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mutant-kitten-keyword-unit` | P1 打出官方单位《变异猫咪》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `坚守2`、`壁垒` 与 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象。防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-036/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-burly-brawler-keyword-unit` | P1 打出官方单位《魁梧斗士》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `坚守2` 与 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象。防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-099/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-laurent-bladeguard-keyword-unit` | P1 打出官方单位《劳伦特护刃者》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·096/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-garen-keyword-unit` | P1 打出官方英雄单位《盖伦》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `坚守2`、`强攻2` 与 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻/防守战力修正暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGS·007/024; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-solari-guard-keyword-unit` | P1 打出官方单位《日耀卫队》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `坚守` 与 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象。防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·054/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-aerie-head-fan-keyword-unit` | P1 打出官方单位《艾蕾，头号拥趸》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `法盾` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。给予此处其他单位法盾的静态效果暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-041/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vex-keyword-unit` | P1 打出官方英雄单位《薇古丝》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `坚守`、`壁垒` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。眩晕敌方战场单位后的可选移动触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-055/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-wildclaw-beastmaster-keyword-unit` | P1 打出官方单位《野爪兽王》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 7 战力、带 `壁垒` 与 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象。此处低战力单位无法被敌方法术或技能选作目标的静态限制暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-057/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-huge-yordle-keyword-unit` | P1 打出官方单位《超大型约德尔人》 | 支付 10 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `坚守5`、`壁垒` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。本回合通过据守得分后的费用减少路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·055/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-tianna-crownguard-keyword-unit` | P1 打出官方单位《缇亚娜·冕卫》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 与 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象。位于战场时对手无法得分的静态限制暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·060/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-jhin-spellshield-roam-keyword-unit` | P1 打出官方英雄单位《烬》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 与 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。移动时获得费用资源的触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-022/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-jhin-alt-a-spellshield-roam-keyword-unit` | P1 打出官方英雄单位《烬》A 版本 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 与 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。移动时获得费用资源的触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-022a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vi-keyword-unit` | P1 打出官方英雄单位《蔚》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。支付资源令自身本回合内战力翻倍的激活路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-030/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vi-alt-a-keyword-unit` | P1 打出官方英雄单位《蔚》A 版本 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。支付资源令自身本回合内战力翻倍的激活路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-030a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-leblanc-keyword-unit` | P1 打出官方英雄单位《乐芙兰》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `后排` 标签的 `CARD_TYPE:UNIT` 单位对象。所处战场瞬息效果不会触发的静态路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-090/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-enthusiastic-announcer-keyword-unit` | P1 打出官方单位《热情的播报员》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `后排` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。据守战场后的群体增益触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-043/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-moss-stepper-keyword-unit` | P1 打出官方单位《踏苔蜥》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `犬形` 与 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 3 以上的 +1 战力与法盾路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-047/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-trevor-duttonel-keyword-unit` | P1 打出官方单位《特雷弗·达顿尔》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `坚守` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。据守战场后的活跃 `精灵` 生成触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-048/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-windrunner-fox-keyword-unit` | P1 打出官方单位《风行狐》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `犬形` 与 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 3 以上的 +1 战力与游走路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-075/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-crystalhand-hunter-keyword-unit` | P1 打出官方单位《晶手猎人》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `狩猎` 与 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 6 以上的 +1 战力路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-094/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-flameclaw-keyword-unit` | P1 打出官方单位《焰爪》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `犬形` 与 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 3 以上的 +1 战力与活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-016/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-wuji-apprentice-keyword-unit` | P1 打出官方单位《无极学徒》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 6 以上的打出抽 1 路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-040/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-arena-crowd-favorite-keyword-unit` | P1 打出官方单位《竞技场人气王》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象。经验消耗给予自身增益路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-102/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-unl-yi-hunt-keyword-unit` | P1 打出官方英雄单位《易》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 6 以上的法盾与游走路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-113/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-unl-yi-alt-a-hunt-keyword-unit` | P1 打出官方英雄单位《易》A 版本 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象。等级 6 以上的法盾与游走路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-113a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-khazix-hunt-keyword-unit` | P1 打出官方英雄单位《卡兹克》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻触发、经验消耗和伤害路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-119/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-khazix-alt-a-hunt-keyword-unit` | P1 打出官方英雄单位《卡兹克》A 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻触发、经验消耗和伤害路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-119a/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-black-rose-agent-keyword-unit` | P1 打出官方单位《黑色玫瑰要员》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `强攻` 标签的 `CARD_TYPE:UNIT` 单位对象。强攻战斗修正与绝念召符文路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-152/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-stunning-guardian-keyword-unit` | P1 打出官方单位《惊艳守护者》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `仙灵` 与 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象。经验消耗给予自身增益路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-162/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-galio-keyword-unit` | P1 打出官方英雄单位《加里奥》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `壁垒` 与 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、壁垒承伤顺序和无法造成战斗伤害路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-171/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-rell-keyword-unit` | P1 打出官方英雄单位《芮尔》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象。壁垒承伤顺序、进攻触发、免费打出武装与贴附路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·024/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-jax-keyword-unit` | P1 打出官方英雄单位《贾克斯》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税与手牌武装灵便静态授予路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·054/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-jax-alt-a-keyword-unit` | P1 打出官方英雄单位《贾克斯》A 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税与手牌武装灵便静态授予路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·054a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-giant-arm-kato-keyword-unit` | P1 打出官方单位《巨腕加藤》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、移动触发、关键词授予和本回合战力修正路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·112/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-xin-zhao-keyword-unit` | P1 打出官方英雄单位《赵信》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象。壁垒承伤顺序和条件活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·176/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-sivir-spellshield2-keyword-unit` | P1 打出官方英雄单位《希维尔》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 7 战力、带 `法盾2` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾2目标税、进攻征服、过量伤害检查和伤害触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·120/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-sivir-alt-a-spellshield2-keyword-unit` | P1 打出官方英雄单位《希维尔》A 版本 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 7 战力、带 `法盾2` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾2目标税、进攻征服、过量伤害检查和伤害触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·120a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-draven-keyword-unit` | P1 打出官方英雄单位《德莱文》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、战斗胜利得分和战斗中被摧毁的对手得分路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·148/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-draven-alt-a-keyword-unit` | P1 打出官方英雄单位《德莱文》A 版本 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、战斗胜利得分和战斗中被摧毁的对手得分路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·148a/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-vayne-keyword-unit` | P1 打出官方英雄单位《薇恩》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `哨兵` 与 `强攻3` 标签的 `CARD_TYPE:UNIT` 单位对象。强攻战斗修正、条件活跃进场、征服触发、支付1和回手路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·223/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-vayne-promo-keyword-unit` | P1 打出官方英雄单位《薇恩》promo 版本 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `哨兵` 与 `强攻3` 标签的 `CARD_TYPE:UNIT` 单位对象。强攻战斗修正、条件活跃进场、征服触发、支付1和回手路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·223*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-aphelios-vanilla-unit` | P1 打出官方英雄单位《厄斐琉斯》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。武装贴附后的三选一触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·224/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sfd-aphelios-promo-vanilla-unit` | P1 打出官方英雄单位《厄斐琉斯》promo 版本 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。武装贴附后的三选一触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·224*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sfd-irelia-keyword-unit` | P1 打出官方英雄单位《艾瑞莉娅》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、被选择为目标或准备时本回合 +1 路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·225/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-irelia-promo-keyword-unit` | P1 打出官方英雄单位《艾瑞莉娅》promo 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税、被选择为目标或准备时本回合 +1 路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·225*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-ahri-vanilla-unit` | P1 打出官方英雄单位《阿狸》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。进攻或防守时敌方单位本回合 -2 且不得低于 1 的触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·227/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sfd-ahri-promo-vanilla-unit` | P1 打出官方英雄单位《阿狸》promo 版本 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。进攻或防守时敌方单位本回合 -2 且不得低于 1 的触发路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·227*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sfd-yone-no-optional-assemble` | P1 打出官方英雄单位《永恩》，不选择百炼装配 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `恶魔` 与 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配、武装贴附和征服开放战场后的基地伤害路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·233/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-yone-promo-no-optional-assemble` | P1 打出官方英雄单位《永恩》promo 版本，不选择百炼装配 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、带 `恶魔` 与 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象。百炼装配、武装贴附和征服开放战场后的基地伤害路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·233*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-yasuo-keyword-unit` | P1 打出官方英雄单位《亚索》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。游走移动和单回合第三次移动得分路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·235/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-yasuo-promo-keyword-unit` | P1 打出官方英雄单位《亚索》promo 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。游走移动和单回合第三次移动得分路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·235*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-darius-trifarian-unit` | P1 打出官方英雄单位《德莱厄斯》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `崔法利` 标签的 `CARD_TYPE:UNIT` 单位对象。鼓舞活跃进场和同处其他友方单位静态 +1 路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·236/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sfd-darius-promo-trifarian-unit` | P1 打出官方英雄单位《德莱厄斯》promo 版本 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `崔法利` 标签的 `CARD_TYPE:UNIT` 单位对象。鼓舞活跃进场和同处其他友方单位静态 +1 路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·236*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-arena-councilor-active-unit` | P1 打出官方单位《竞技场理事》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、未休眠且带 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象。横置战力修正技能暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-001/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-immortal-phoenix-keyword-unit` | P1 打出官方单位《不朽凤凰》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、带 `强攻2` 与 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象。强攻战斗修正和废牌堆打出触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·037/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-corpse-flower-predator-keyword-unit` | P1 打出官方单位《亡花掠食者》 | 支付 8 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 8 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象。法盾目标税和敌方控制战场替代目的地路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·161/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-revna-roam-keyword-unit` | P1 打出官方单位《传承者雷芙纳》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 7 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。游走移动和法术打出触发活跃路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-005/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-jungle-elephant-keyword-unit` | P1 打出官方单位《莽林巨象》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、带 `强攻` 标签的 `CARD_TYPE:UNIT` 单位对象。强攻战斗修正和本回合有单位被摧毁时活跃进场路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-008/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sad-poro-keyword-unit` | P1 打出官方单位《哀哀魄罗》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。绝念抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·036/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-unl-sad-poro-keyword-unit` | P1 打出官方单位《哀哀魄罗》UNL 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 2 战力、带 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象。绝念抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` UNL-221/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-watchful-sentinel-vanilla-unit` | P1 打出官方单位《警觉的哨兵》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。绝念抽牌触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·096/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-scouting-warhawk-keyword-unit` | P1 打出官方单位《侦察飞鹰》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象。绝念召符文触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·216/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-mechanical-trickster-vanilla-unit` | P1 打出官方单位《机械戏法师》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象。绝念创建随从触发暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·239/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-unl-babbling-poro-predict-recycle` | P1 打出官方单位《叨叨魄罗》UNL 版本并预知回收 | 支付 2 点费用，选择己方主牌堆顶部一张牌作为预知回收目标；双方让过后源牌进入控制者基地成为 2 战力、带 `预知` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部。选择非顶部牌由直接拒绝测试覆盖。 | `CATALOG` UNL-224/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+ |
| `p2-preflight-play-babbling-poro-predict-recycle` | P1 打出官方单位《叨叨魄罗》并预知回收 | 支付 2 点费用，选择己方主牌堆顶部一张牌作为预知回收目标；双方让过后源牌进入控制者基地成为 2 战力、带 `预知` 与 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部。选择非顶部牌由直接拒绝测试覆盖。 | `CATALOG` OGN·171/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+ |
| `p2-preflight-play-gemstone-golem-predict-recycle` | P1 打出官方单位《宝石巨像》并预知回收 | 支付 5 点费用，选择己方主牌堆顶部一张牌作为预知回收目标；双方让过后源牌进入控制者基地成为 5 战力、带 `坚守` 与 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部。防守战力修正暂缓，选择非顶部牌由直接拒绝测试覆盖。 | `CATALOG` OGN·086/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+ |
| `p2-preflight-play-dase-scout-predict-recycle` | P1 打出官方单位《大塞斥候》并预知回收 | 支付 6 点费用，选择己方主牌堆顶部一张牌作为预知回收目标；双方让过后源牌进入控制者基地成为 5 战力、带 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部。开放战场打出路径暂缓，选择非顶部牌由直接拒绝测试覆盖。 | `CATALOG` OGN·174/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+ |
| `p2-preflight-play-jhin-predict-recycle` | P1 打出官方英雄单位《烬》并预知回收 | 支付 4 点费用，选择己方主牌堆顶部一张牌作为预知回收目标；双方让过后源牌进入控制者基地成为 4 战力、带 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部。替代打出费用路径暂缓，选择非顶部牌由直接拒绝测试覆盖。 | `CATALOG` UNL-089/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+ |
| `p2-preflight-play-aggressive-dragonhound-active-unit` | P1 打出官方单位《好斗的龙犬》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力、未休眠且带 `犬形` 与 `龙` 标签的 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` SFD·006/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-yi-active-unit` | P1 打出官方英雄单位《易》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 6 战力、未休眠且带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象。游走移动路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGS·009/024; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vanguard-squire-active-unit` | P1 打出官方单位《先锋扈从》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、未休眠且带 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGS·016/024; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-warwick-active-unit` | P1 打出官方英雄单位《沃里克》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、未休眠且带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻触发摧毁已受伤敌方单位路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·159/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-arc-warwick-active-unit` | P1 打出 ARC 官方英雄单位《沃里克》 | 支付 6 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力、未休眠且带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象。进攻触发摧毁已受伤敌方单位路径暂缓，带目标打出由直接拒绝测试覆盖。 | `CATALOG` ARC-004/006; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-dockside-lurker-vanilla-unit` | P1 打出官方单位《船坞潜伏者》 | 支付 3 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·175/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-vanguard-sergeant-vanilla-unit` | P1 打出官方单位《先锋中士》 | 支付 4 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·219/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-playful-imp-vanilla-unit` | P1 打出官方单位《贪玩的小鬼》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 5 战力 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·049/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-super-mech-vanilla-unit` | P1 打出官方单位《超能机甲》 | 支付 7 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·088/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-mountain-drake-vanilla-unit` | P1 打出官方单位《山脉亚龙》 | 支付 9 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 10 战力 `CARD_TYPE:UNIT` 单位对象。带目标打出由直接拒绝测试覆盖。 | `CATALOG` OGN·142/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-heartsplit-dragon-discard-opponent-hand` | P1 打出官方单位《辟心玄龙》 | 支付 7 点费用，选择一张对手手牌；双方让过后源牌进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，然后让该手牌所属对手弃置目标。友方手牌目标由直接拒绝测试覆盖。 | `CATALOG` OGN·192/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-charming-spirit-discard-chosen-player-hand` | P1 打出官方单位《魅惑之灵》 | 支付 3 点费用，选择任意玩家手牌对象代表被选择玩家实际弃置的手牌；双方让过后源牌进入控制者基地成为 2 战力、带 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让该手牌所属玩家弃置目标。己方手牌弃置和非手牌目标拒绝由直接测试覆盖。 | `CATALOG` UNL-121/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-teemo-self-power-plus-three` | P1 打出官方英雄单位《提莫》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让自身本回合内战力 +3。带目标打出由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` OGN·197/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-teemo-alt-a-self-power-plus-three` | P1 打出官方英雄单位《提莫》A 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让自身本回合内战力 +3。带目标打出由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` OGN·197a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-teemo-alt-b-self-power-plus-three` | P1 打出官方英雄单位《提莫》B 版本 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让自身本回合内战力 +3。带目标打出由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` OGN·197b/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-fnd-teemo-self-power-plus-three` | P1 打出 FND 官方英雄单位《提莫》 | 支付 2 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让自身本回合内战力 +3。带目标打出由直接拒绝测试覆盖，待命/反应路径暂缓。 | `CATALOG` FND-196/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sett-self-boon` | P1 打出官方英雄单位《瑟提》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力。带目标打出由直接拒绝测试覆盖，征服触发与消耗增益激活暂缓。 | `CATALOG` SFD·232/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-sett-promo-self-boon` | P1 打出官方英雄单位《瑟提》promo 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力。带目标打出由直接拒绝测试覆盖，征服触发与消耗增益激活暂缓。 | `CATALOG` SFD·232*/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-sett-self-boon` | P1 打出 OGN 官方英雄单位《瑟提》 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力。带目标打出由直接拒绝测试覆盖，征服触发与消耗增益激活暂缓。 | `CATALOG` OGN·164/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-sett-alt-a-self-boon` | P1 打出 OGN 官方英雄单位《瑟提》A 版本 | 支付 5 点费用，0 目标入栈；双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力。带目标打出由直接拒绝测试覆盖，征服触发与消耗增益激活暂缓。 | `CATALOG` OGN·164a/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-scrap-heap-equipment-draw` | P1 打出官方装备《废料堆》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，然后 P1 抽 1 张牌。带目标打出由直接拒绝测试覆盖，弃置和摧毁触发抽牌分支暂缓。 | `CATALOG` OGN·182/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-sprite-lantern-equipment-create-sprite` | P1 打出官方装备《精灵提灯》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象，然后打出一名带 `瞬息` 标签的 3 战力“精灵”到控制者基地。带目标打出由直接拒绝测试覆盖，绝念和开始阶段瞬息摧毁暂缓。 | `CATALOG` UNL-078/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sumpworks-map-equipment-ephemeral` | P1 打出官方装备《地沟区地图》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象。带目标打出由直接拒绝测试覆盖，对手得分触发抽牌和开始阶段瞬息摧毁暂缓。 | `CATALOG` UNL-085/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-scrying-blossom-equipment-exhausted` | P1 打出官方装备《占卜花朵》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象。带目标打出由直接拒绝测试覆盖，洞察/抽牌/经验激活技能暂缓。 | `CATALOG` UNL-136/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-magic-beans-equipment` | P1 打出官方装备《魔法鲜豆》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，法术对决期间单位打出触发抽牌分支暂缓。 | `CATALOG` UNL-011/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-steel-ballista-equipment-exhausted` | P1 打出官方装备《钢铁弩炮》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象。带目标打出由直接拒绝测试覆盖，横置伤害技能暂缓。 | `CATALOG` OGN·017/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-heart-of-ice-equipment` | P1 打出官方装备《玄冰之心》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置战力修正技能暂缓。 | `CATALOG` SFD·052/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-remorse-orb-equipment` | P1 打出官方装备《懊悔法球》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置负战力修正技能暂缓。 | `CATALOG` OGN·090/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-soul-sword-equipment` | P1 打出官方装备《灵魂之剑》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。 | `CATALOG` UNL-039/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-jagged-dirk-equipment` | P1 打出官方装备《锯齿短匕》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配红色贴附分支暂缓。 | `CATALOG` SFD·009/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-dorans-shield-equipment` | P1 打出官方装备《多兰之盾》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。 | `CATALOG` SFD·033/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hextech-infused-bulwark-equipment` | P1 打出官方装备《海克斯注力刚壁》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配蓝色贴附分支暂缓。 | `CATALOG` SFD·073/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-dorans-blade-equipment` | P1 打出官方装备《多兰之刃》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。 | `CATALOG` SFD·095/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-dorans-ring-equipment` | P1 打出官方装备《多兰之戒》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。 | `CATALOG` SFD·124/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vanguards-eye-equipment` | P1 打出官方装备《先锋之眼》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。 | `CATALOG` SFD·153/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-recurve-bow-equipment` | P1 打出官方装备《反曲之弓》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配红色贴附分支暂缓。 | `CATALOG` SFD·016/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-brutalizer-equipment` | P1 打出官方装备《残暴之力》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。 | `CATALOG` SFD·042/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-guardian-angel-equipment` | P1 打出官方装备《守护天使》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配绿色贴附分支暂缓。 | `CATALOG` SFD·051/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hexdrinker-equipment` | P1 打出官方装备《海克斯饮魔刀》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。 | `CATALOG` SFD·102/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-warmogs-armor-equipment` | P1 打出官方装备《狂徒铠甲》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。 | `CATALOG` SFD·108/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-trinity-force-equipment` | P1 打出官方装备《三相之力》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。 | `CATALOG` SFD·115/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-boots-of-swiftness-equipment` | P1 打出官方装备《轻灵之靴》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。 | `CATALOG` SFD·133/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-cull-equipment` | P1 打出官方装备《萃取》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配紫色贴附分支暂缓。 | `CATALOG` SFD·134/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-sacred-shears-equipment` | P1 打出官方装备《神圣剪刀》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。 | `CATALOG` SFD·172/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-bf-sword-equipment` | P1 打出官方装备《暴风大剑》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配黄色贴附分支暂缓。 | `CATALOG` SFD·161/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-wanderers-guidebook-equipment` | P1 打出官方装备《云游图鉴》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配蓝色贴附分支暂缓。 | `CATALOG` SFD·086/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-arions-fall-equipment` | P1 打出官方装备《阿瑞昂的陨落》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配 1 红色贴附分支暂缓。 | `CATALOG` SFD·030/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hunters-machete-equipment` | P1 打出官方装备《猎人的宽刃刀》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配橙色贴附分支暂缓。 | `CATALOG` UNL-096/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-withered-battleaxe-equipment` | P1 打出官方装备《枯萎战斧》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配 1 红色贴附分支暂缓。 | `CATALOG` UNL-019/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-bone-club-equipment` | P1 打出官方装备《碎骨棒》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配 1 橙色贴附分支暂缓。 | `CATALOG` SFD·118/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-ancient-stele-equipment` | P1 打出官方装备《远古簇碑》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。 | `CATALOG` SFD·117/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-hextech-anomaly-equipment` | P1 打出官方装备《海克斯异常体》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。 | `CATALOG` SFD·083/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-energy-channel-equipment` | P1 打出官方装备《能量通道》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置资源技能暂缓。 | `CATALOG` OGN·098/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-time-gate-equipment` | P1 打出官方装备《预时之门》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置回响授予技能暂缓。 | `CATALOG` SFD·078/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-raven-tome-equipment` | P1 打出官方装备《邪鸦魔典》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置法术伤害修正技能暂缓。 | `CATALOG` OGN·032/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sun-disc-equipment` | P1 打出官方装备《太阳圆盘》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置鼓舞技能暂缓。 | `CATALOG` OGN·021/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-foresight-mask-equipment` | P1 打出官方装备《远见面具》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，战斗触发暂缓。 | `CATALOG` OGN·060/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-solari-altar-equipment` | P1 打出官方装备《烈阳圣坛》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，摧毁触发抽牌分支暂缓。 | `CATALOG` OGN·072/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-chemtech-barrel-equipment` | P1 打出官方装备《炼金科技桶》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，对手回合法术触发打出金币分支暂缓。 | `CATALOG` SFD·063/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-soul-wheel-equipment` | P1 打出官方装备《灵魂之轮》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，友方单位目标触发支付并抽牌分支暂缓。 | `CATALOG` SFD·144/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-mushroom-bag-equipment` | P1 打出官方装备《蘑菇袋》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，开始阶段正面朝下待命牌抽牌触发暂缓。 | `CATALOG` OGN·101/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-arena-bar-equipment` | P1 打出官方装备《竞技场酒吧》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置给予休眠友方单位增益技能暂缓。 | `CATALOG` OGN·124/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-pirate-hideout-equipment` | P1 打出官方装备《海盗避风港》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，友方单位变为活跃时战力修正触发暂缓。 | `CATALOG` OGN·143/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-forgotten-signpost-equipment` | P1 打出官方装备《被遗忘的路标》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，迅捷横置移动技能暂缓。 | `CATALOG` UNL-045/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-frozen-gem-equipment` | P1 打出官方装备《冰封宝石》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，抽第二张牌触发战力修正分支暂缓。 | `CATALOG` UNL-074/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-crumbling-palace-equipment` | P1 打出官方装备《倾颓宫殿》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，开始阶段胜利条件和横置创建战鹰分支暂缓。 | `CATALOG` UNL-088/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-scarlet-rose-equipment` | P1 打出官方装备《猩红玫瑰》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，单位打出触发经验和横置活跃单位技能暂缓。 | `CATALOG` UNL-109/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-reversal-shard-equipment` | P1 打出官方装备《逆转碎片》 | 支付 6 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，开始阶段摧毁触发分支暂缓。 | `CATALOG` UNL-174/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-assembly-rack-equipment` | P1 打出官方装备《装配架》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置创建机器人技能暂缓。 | `CATALOG` SFD·019/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sfur-song-equipment` | P1 打出官方装备《斯弗尔尚歌》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配贴附和复制技能文字分支暂缓。 | `CATALOG` SFD·059/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-z-drive-equipment` | P1 打出官方装备《Z型驱动》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配和放逐打出分支暂缓。 | `CATALOG` SFD·090/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-vanguard-armory-equipment` | P1 打出官方装备《先锋军备》 | 支付 7 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置创建多个随从分支暂缓。 | `CATALOG` SFD·168/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-remembrance-altar-equipment` | P1 打出官方装备《追忆祭坛》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，友方单位摧毁触发和牌堆放置选择暂缓。 | `CATALOG` SFD·169/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-rage-sigil-equipment` | P1 打出官方装备《暴怒之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得红色符能技能暂缓。 | `CATALOG` SFD·222/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-focus-sigil-equipment` | P1 打出官方装备《专注之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得绿色符能技能暂缓。 | `CATALOG` SFD·226/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-insight-sigil-equipment` | P1 打出官方装备《洞察之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得蓝色符能技能暂缓。 | `CATALOG` SFD·229/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-power-sigil-equipment` | P1 打出官方装备《力量之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得橙色符能技能暂缓。 | `CATALOG` SFD·231/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-discord-sigil-equipment` | P1 打出官方装备《不和之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得紫色符能技能暂缓。 | `CATALOG` SFD·234/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-unity-sigil-equipment` | P1 打出官方装备《团结之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得黄色符能技能暂缓。 | `CATALOG` SFD·238/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-rage-sigil-equipment` | P1 打出 OGN 版官方装备《暴怒之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得红色符能技能暂缓。 | `CATALOG` OGN·040/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-focus-sigil-equipment` | P1 打出 OGN 版官方装备《专注之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得绿色符能技能暂缓。 | `CATALOG` OGN·081/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-insight-sigil-equipment` | P1 打出 OGN 版官方装备《洞察之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得蓝色符能技能暂缓。 | `CATALOG` OGN·120/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-power-sigil-equipment` | P1 打出 OGN 版官方装备《力量之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得橙色符能技能暂缓。 | `CATALOG` OGN·163/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-discord-sigil-equipment` | P1 打出 OGN 版官方装备《不和之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得紫色符能技能暂缓。 | `CATALOG` OGN·204/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ogn-unity-sigil-equipment` | P1 打出 OGN 版官方装备《团结之印》 | 支付 0 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置获得黄色符能技能暂缓。 | `CATALOG` OGN·245/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-wondrous-pack-equipment` | P1 打出官方装备《奇妙行囊》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置回手技能暂缓。 | `CATALOG` OGN·181/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-siren-equipment` | P1 打出官方装备《塞壬号》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，支付并横置移动技能暂缓。 | `CATALOG` OGN·184/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-ownerless-treasure-equipment` | P1 打出官方装备《无主宝藏》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，离场触发和自毁激活技能暂缓。 | `CATALOG` OGN·186/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-scavenging-whiz-equipment` | P1 打出官方装备《拾荒小能手》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，回收/支付/横置抽牌技能暂缓。 | `CATALOG` OGN·099/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-mistfall-bladeyard-equipment` | P1 打出官方装备《雾临剑冢》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，增益触发和支付休眠分支暂缓。 | `CATALOG` OGN·152/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-shimmering-aurora-equipment` | P1 打出官方装备《闪耀极光》 | 支付 9 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，回合结束展示与免费打出分支暂缓。 | `CATALOG` OGN·160/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-solari-emblem-equipment` | P1 打出官方装备《烈阳徽记》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，战斗平局触发和全体召回分支暂缓。 | `CATALOG` OGN·227/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-vanguard-helm-equipment` | P1 打出官方装备《先锋之盔》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，增益单位摧毁触发和增益分配暂缓。 | `CATALOG` OGN·228/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-honeyfruit-equipment` | P1 打出官方装备《蜜糖果实》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌以休眠状态进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，横置资源技能和等级 6 分支暂缓。 | `CATALOG` UNL-049/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-last-rites-equipment` | P1 打出官方装备《临终仪式》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配和废牌堆回收分支暂缓。 | `CATALOG` SFD·150/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-blade-of-ruined-king-equipment` | P1 打出官方装备《破败王者之刃》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配、额外费用和贴附分支暂缓。 | `CATALOG` SFD·178/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-mysterious-weapon-equipment` | P1 打出官方装备《来路不明的武器》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，弃牌横置和摧毁替代效果暂缓。 | `CATALOG` OGN·023/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-sea-monster-hook-equipment` | P1 打出官方装备《海兽钓钩》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，激活、摧毁、查看和免费打出分支暂缓。 | `CATALOG` OGN·242/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-petricite-monument-equipment-ephemeral` | P1 打出官方装备《禁魔石丰碑》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象。带目标打出由直接拒绝测试覆盖，友方单位法盾静态效果和开始阶段瞬息摧毁暂缓。 | `CATALOG` SFD·104/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-zhonyas-hourglass-equipment` | P1 打出官方装备《中娅沙漏》 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，待命/反应时机和摧毁替代召回效果暂缓。 | `CATALOG` OGN·077/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-edge-of-night-equipment` | P1 打出官方装备《夜之锋刃》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，待命正面朝下打出、即时贴附和装配暂缓。 | `CATALOG` SFD·139/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hearthfire-cloak-equipment` | P1 打出官方专属装备《炉火斗篷》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，唯我构筑限制和装配贴附暂缓。 | `CATALOG` SFD·190/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-rabadons-deathcap-equipment` | P1 打出官方专属装备《灭世者的死亡之冠》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，唯我构筑限制和装配贴附暂缓。 | `CATALOG` SFD·191/221; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-blast-cone-equipment-no-move` | P1 打出官方装备《喷射球果》，不选择可选移动 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由当前 no-move 路径直接拒绝测试覆盖，可选移动、移动触发休眠和眩晕暂缓。 | `CATALOG` UNL-133/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-soulguard-equipment-boon` | P1 打出官方装备《奥义！魂佑》，选择一名友方单位 | 支付 2 点费用，目标必须是友方单位；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并给予目标 `增益` 标签和永久 +1 战力。敌方目标由直接拒绝测试覆盖，拥有增益友方单位获得法盾的静态效果暂缓。 | `CATALOG` OGN·063/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p33 rules 318-324; p39-p42 rules 355-356 |
| `p2-preflight-play-death-list-equipment` | P1 打出官方装备《夺命名单》 | 支付 1 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，宣告属性标签和横置战力修正技能暂缓。 | `CATALOG` UNL-138/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-cursed-sarcophagus-equipment-banish-graveyard-units` | P1 打出官方装备《受诅咒的石棺》 | 支付 4 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并将控制者废牌堆中带 `CARD_TYPE:UNIT` 标签的单位牌全部移入放逐区。带目标打出由直接测试拒绝，横置摧毁自身并打出放逐单位牌的激活技能暂缓。 | `CATALOG` UNL-148/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356 |
| `p2-preflight-play-scrying-shell-equipment-predict-recycle` | P1 打出官方装备《占卜贝壳》，预知时选择回收顶部牌 | 支付 2 点费用，选择己方主牌堆顶部一张牌作为预知回收选择；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并将所选顶部牌回收到主牌堆底部。选择非顶部牌由直接测试拒绝，迅捷摧毁并横置给予 +2 战力的激活技能暂缓。 | `CATALOG` UNL-161/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-scrying-shell-equipment-predict-no-recycle` | P1 打出官方装备《占卜贝壳》，预知时不回收顶部牌 | 支付 2 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，主牌堆顺序保持不变。迅捷摧毁并横置给予 +2 战力的激活技能暂缓。 | `CATALOG` UNL-161/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416 |
| `p2-preflight-play-forced-conscription-control-small-enemy-recall` | P1 打出官方法术《强制征召》，不支付经验额外费用 | 支付 5 点费用，选择战场上一名不高于 3 战力且带 `CARD_TYPE:UNIT` 标签的敌方单位；双方让过后获得其控制权、使其休眠并放入 P1 基地。4 战力目标由直接测试拒绝，支付 5 经验选择任意敌方单位分支暂缓。 | `CATALOG` UNL-140/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-taken-for-a-ride-control-enemy-recall` | P1 打出官方法术《据为己有》 | 支付 8 点费用，选择战场上一名带 `CARD_TYPE:UNIT` 标签的敌方单位；双方让过后获得其控制权并放入 P1 基地。非单位战场对象由直接测试拒绝；当前 2P preflight 用区域归属表达控制权，完整 owner/controller 分离模型暂缓。 | `CATALOG` OGN·203/298; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-boneclub-promo-equipment` | P1 打出官方 promo 装备《碎骨棒》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，装配贴附分支暂缓。 | `CATALOG` SFD·118a/221·P; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-hextech-gauntlet-equipment` | P1 打出官方专属装备《海克斯科技护手》 | 支付 3 点费用，0 目标加入结算链；双方让过后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象。带目标打出由直接拒绝测试覆盖，动态装配费用和贴附分支暂缓。 | `CATALOG` UNL-188/219; `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-bellows-breath-up-to-three-units-damage` | P1 打出官方法术《风箱炎息》 | 支付 1 点费用，选择当前单战场区域模型中的 1-3 名不同单位，双方让过后对每名目标各造成 1 点伤害；同一位置精确约束和有色回响路径暂缓。 | `CATALOG` SFD·080/221; `CORE-260330` p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+ |
| `p2-preflight-play-firestorm-damage-enemy-battlefield-units` | P1 打出官方法术《烈火风暴》 | 支付 6 点费用，0 目标入栈；双方让过后对当前单战场区域模型中的所有敌方战场单位各造成 3 点伤害，友方战场单位和敌方基地单位不受影响。 | `CATALOG` OGS·002/024; `CORE-260330` p14-p15 rules 142-143; p31-p33 rules 323-324; p39-p42 rules 355-356 |
| `p2-preflight-play-crescent-strike-target-plus-splash` | P1 打出官方法术《新月打击》 | 支付 3 点费用，目标必须是敌方战场单位；双方让过后对主目标造成 4 点伤害，并对当前单战场区域模型中的其他敌方战场单位各造成 1 点伤害，友方战场单位和敌方基地单位不受影响。 | `CATALOG` UNL-072/219; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-perfect-finale-draw-mode` | P1 打出官方法术《完美谢幕》，选择抽牌模式 | `PLAY_CARD.mode = DRAW_1`，支付 4 点费用，0 目标入栈，双方让过后抽 1 张牌；回响重复不同模式路径暂缓。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p57 rule 413.4 |
| `p2-preflight-play-perfect-finale-battlefield-damage-mode` | P1 打出官方法术《完美谢幕》，选择战场单位伤害模式 | `PLAY_CARD.mode = BATTLEFIELD_UNIT_DAMAGE_2`，支付 4 点费用，目标必须是战场单位，双方让过后造成 2 点伤害。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-perfect-finale-base-damage-mode` | P1 打出官方法术《完美谢幕》，选择基地单位伤害模式 | `PLAY_CARD.mode = BASE_UNIT_DAMAGE_3`，支付 4 点费用，目标必须是基地单位，双方让过后造成 3 点伤害。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340 |
| `p2-preflight-play-perfect-finale-battlefield-power-mode` | P1 打出官方法术《完美谢幕》，选择战场单位战力 -4 模式 | `PLAY_CARD.mode = BATTLEFIELD_UNIT_POWER_MINUS_4`，支付 4 点费用，目标必须是战场单位，双方让过后目标本回合内战力 -4。 | `CATALOG` UNL-182/219; `CORE-260330` p39-p42 rules 355-356; p31-p33 rules 318-324 |
| `p2-preflight-play-highway-robbery-enemy-unit-damage` | P1 打出官方法术《巧取豪夺》，目标控制者不选择让 P1 抽两张牌 | 支付 2 点费用，目标必须是敌方单位；双方让过后对目标造成 6 点伤害。友方单位和离场敌方牌目标由直接拒绝测试覆盖。 | `CATALOG` OGN·033/298; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356 |
| `p2-preflight-play-highway-robbery-target-controller-draw-choice` | P1 打出官方法术《巧取豪夺》，目标控制者选择让 P1 抽两张牌 | `PLAY_CARD.mode = TARGET_CONTROLLER_CHOOSES_DRAW_2`，支付 2 点费用，目标必须是敌方单位；双方让过后 P1 抽 2 张牌，目标不受伤害。 | `CATALOG` OGN·033/298; `CORE-260330` p14-p15 rules 142-143; p33-p35 rules 327-340; p39-p42 rules 355-356; p57 rule 413.4 |
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
| `p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit` | P1 打出官方法术《天顶之刃》且不选择可选移动 | 支付 3 点费用，目标必须是敌方战场单位；双方让过后施加 `STUNNED`，友方单位和敌方基地单位目标由直接拒绝测试覆盖；可选移动分支暂缓。 | `CATALOG` OGN·262/298; `CORE-260330` p39-p42 rules 355-356; p33-p35 rules 327-340; p31-p33 rules 318-324 |
| `p2-preflight-play-skullcrack-stun-friendly-and-enemy-battlefield-units` | P1 打出官方法术《强手裂颅》 | 支付 2 点费用，第一目标必须是友方战场单位，第二目标必须是敌方战场单位；双方让过后对两名目标施加 `STUNNED`。目标顺序、友方基地单位和敌方基地单位由直接拒绝测试覆盖；同一战场的位置约束暂按当前单战场区域模型处理。 | `CATALOG` OGN·220/298; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
| `p2-preflight-play-heroic-charge-power-plus-stun` | P1 打出官方法术《英勇冲锋》 | 支付 3 点费用，第一目标必须是友方战场单位，第二目标必须是敌方战场单位；双方让过后让友方目标本回合内战力 +1，并对敌方目标施加 `STUNNED`。目标顺序和基地单位目标由直接拒绝测试覆盖；同一位置约束暂按当前单战场区域模型处理。 | `CATALOG` UNL-155/219; `CORE-260330` p14-p15 rules 142-143; p31-p35 rules 318-340; p39-p42 rules 355-356 |
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
32. 已完成：支持 `1-2`/`1-3` 目标数量范围和多目标逐个结算，并补《星芒凝汇》双目标 fixture 与一目标直接测试。
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
87. 已完成：迁移《距破之舞》两名不同单位分别 +2/-2 的分裂战力修正路径、《换换乐》两名战场单位本回合内战力互换路径。
88. 已完成：迁移《闪现》最多两名友方战场单位移动到基地路径，并补敌方单位/友方基地单位目标拒绝测试。
89. 已完成：为对象状态补最小 `isExhausted`，迁移《大幕渐起》支付回响后重复变为活跃状态路径。
90. 已完成：迁移《痛殴》不支付消耗增益额外费用时让一名单位变为活跃状态路径；消耗增益无视费用暂缓。
91. 已完成：迁移《狩猎》让控制者所有场上单位变为活跃状态路径。
92. 已完成：迁移《过载能量》先让所有友方单位休眠、再对所有战场单位造成 12 点伤害的组合结算路径。
93. 已完成：将法术召出的“休眠符文”写入 `cardObjects.isExhausted = true`，并补《动员》《万世催化石》相关 fixture 期望。
94. 已完成：迁移《御衡守念》对手距胜利不超过 3 分时减费 2，并按卡面顺序先抽 1 张牌、再召出 1 枚休眠符文路径。
95. 已完成：迁移《择日再战》友方单位返回所属者手牌后，其拥有者召出 1 枚休眠符文路径。
96. 已完成：迁移《坠渊之流》让所有当前场上单位和装备返回所属者手牌路径，复用装备回手事件并覆盖单位/装备同时回手。
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
120. 已完成：迁移《废物利用》不选择装备目标的合法分支，覆盖可选装备摧毁被跳过后继续抽 1 张牌的路径；装备目标摧毁分支已在第 184 项补齐。
121. 已完成：迁移《占山为王》没有已控制战场时只抽基础 1 张牌的路径，战场控制额外抽牌暂缓到战场控制模型。
122. 已完成：迁移《完美谢幕》未支付有色/多次回响时的四个模式，覆盖抽牌、战场单位伤害、基地单位伤害和战场单位负战力修正。
123. 已完成：迁移《高原血统》本回合内摧毁替代效果，覆盖被摧毁时改为清除伤害、休眠召回拥有者基地，且不计入本回合摧毁记忆。
124. 已完成：迁移《战术撤退》本回合内下次摧毁替代效果，覆盖移除伤害、变为休眠并召回拥有者基地，且不计入本回合摧毁记忆。
125. 已完成：迁移《视死如归》友方单位本回合内战力 +3 基础路径；赢得战斗获得经验的触发路径暂缓到战斗胜负/经验模型。
126. 已完成：迁移《动物之友》按己方单位属性标签种类动态计算本回合内战力修正的路径。
127. 已完成：补《扑咚！》支付回响后重复眩晕进攻方单位的路径。
128. 已完成：迁移《曼舞手雷》不进入再次打出分支时对任意单位造成 2 点伤害的基础路径，覆盖基地单位伤害。
129. 已完成：迁移《决斗架势》友方单位本回合内战力 +1 基础路径；“该处唯一控制单位”额外 +1 分支暂缓到更细位置模型。
130. 已完成：迁移《月蚀》本回合内战力 -4 基础路径和洞察顶部牌回收路径；洞察不回收路径仍由只选择单位目标覆盖。
131. 已完成：迁移《天顶之刃》不选择可选移动时眩晕一名敌方战场单位的基础路径，补 `ENEMY_BATTLEFIELD_UNIT` 目标范围和友方/基地目标拒绝测试。
132. 已完成：迁移《巧取豪夺》目标控制者不选择让你抽两张牌时的 6 点伤害分支，补 `ENEMY_UNIT` 目标范围和友方/离场目标拒绝测试。
133. 已完成：迁移《狂风绝息斩》先让友方单位变为活跃状态，再按其当前战力伤害敌方战场单位的路径，复用 `FRIENDLY_THEN_ENEMY_BATTLEFIELD_UNITS` 目标范围并补非法目标测试。
134. 已完成：迁移《聚合变异》按另一名友方单位当前战力补足第一名友方单位战力的路径，补两名友方目标、敌方目标和重复目标拒绝测试。
135. 已完成：迁移《清理门户》双方各自选择并摧毁己方单位的路径，复用 `FRIENDLY_THEN_ENEMY_UNITS` 目标顺序并补非法顺序测试。
136. 已完成：迁移《禁军之墙》任意数量友方战场单位移动到基地路径，补按当前己方战场单位数动态目标上限、敌方/基地/重复目标拒绝测试；待命/迅捷窗口细节暂缓。
137. 已完成：迁移《护驾！》不支付黄色可选费用时打出一名 2 战力“黄沙士兵”到基地的基础路径；待命和有色可选费用让其变为活跃状态暂缓。
138. 已完成：迁移《强手裂颅》一名友方战场单位和一名敌方战场单位双目标眩晕路径，补 `FRIENDLY_BATTLEFIELD_THEN_ENEMY_BATTLEFIELD_UNITS` 目标范围和顺序/基地目标拒绝测试；待命/迅捷窗口细节暂缓。
139. 已完成：迁移《矢志不退》按敌方战场单位数量动态计算友方战场单位本回合内战力修正的基础路径；反应窗口和同一战场精确位置暂缓到后续时机/多战场位置模型。
140. 已完成：迁移《隔绝》敌方战场单位移动到所属基地且移动后无落单敌方单位时不抽牌的基础路径，复用 `ENEMY_BATTLEFIELD_UNIT` 目标范围并补友方/基地目标拒绝测试；落单抽牌分支暂缓到多战场位置/孤立判定模型。
141. 已完成：迁移《风箱炎息》不支付有色回响时对 1-3 名单位各造成 1 点伤害的基础路径，补重复目标和第四目标拒绝测试；同一位置精确约束和有色回响路径暂缓。
142. 已完成：迁移《烈火风暴》对敌方战场单位全体造成 3 点伤害的基础路径，补敌方战场范围伤害原语和显式单位目标拒绝测试；选择“一处战场”的精确位置暂缓到多战场位置模型。
143. 已完成：迁移《换换乐》当前单战场区域模型下两名不同战场单位本回合内战力互换路径，补战场目标、重复目标和基地目标拒绝测试；同一处战场的精确位置暂缓到多战场位置模型。
144. 已完成：迁移《英勇冲锋》友方战场单位本回合内战力 +1 后眩晕敌方战场单位路径，补通用状态/战力修正按目标序号作用原语和顺序/基地目标拒绝测试；同一位置精确约束暂缓到多战场位置模型。
145. 已完成：迁移《新月打击》敌方战场主目标 4 点伤害并对其他敌方战场单位各 1 点伤害的基础路径，补主目标外敌方战场单位溅射原语和友方/基地目标拒绝测试；选择“一处战场”的精确位置暂缓到多战场位置模型。
146. 已完成：迁移《国王诏令》当前 2P preflight 中其他玩家选择非控制者单位并摧毁的基础路径，复用 `ENEMY_UNIT` 目标范围和摧毁原语，补友方单位目标拒绝测试；多人逐玩家选择暂缓到 prompt/多席位选择模型。
147. 已完成：迁移《飓风席卷》当前 2P preflight 中每名玩家可选单位返回所属者手牌的基础路径，复用 `ANY_UNIT` 目标范围和回手原语，补重复目标拒绝测试；逐玩家 prompt/多人选择暂缓。
148. 已完成：迁移《持卫的裁决》敌方战场单位放到拥有者主牌堆顶部/底部路径，补 `UNIT_RETURNED_TO_DECK` 事件、主牌堆顶/底移动原语、缺失模式和非法目标拒绝测试；逐玩家 prompt 暂缓。
149. 已完成：迁移《预判攻势》不支付回响的基础路径，补 `FRIENDLY_MAIN_DECK_CARD` 目标范围、主牌堆顶部两张选择、抽取所选牌并回收未选牌的原语，以及顶部两张以外目标拒绝测试；回响紫色路径暂缓。
150. 已完成：迁移《卡牌骗术》基础路径，复用 `FRIENDLY_MAIN_DECK_CARD` 目标范围、主牌堆顶部三张选择、抽取所选牌并回收其余两张的原语，以及顶部三张以外目标拒绝测试。
151. 已完成：扩展《月蚀》洞察选择回收路径，补 `ANY_UNIT_THEN_FRIENDLY_MAIN_DECK_CARD` 目标范围、主牌堆顶部一张选择回收原语，以及顶部一张以外目标拒绝测试。
152. 已完成：迁移《龙虎双雄》不支付回响的基础路径，复用 `FRIENDLY_MAIN_DECK_CARD` 目标范围、主牌堆顶部三张选择、抽取所选单位牌并回收其余两张的原语，并补不选择单位牌时回收全部已查看牌、`CARD_TYPE:UNIT` 单位牌标签校验、非单位目标和顶部三张以外目标拒绝测试；回响 2 路径暂缓。
153. 已完成：迁移《增援》不选择单位牌的合法分支，复用主牌堆顶部查看/回收原语，支付 5 点费用后回收顶部五张已查看牌；从牌堆打出单位并减费 5 的分支暂缓到打出来源/费用修正模型。
154. 已完成：扩展《巧取豪夺》目标控制者选择让来源控制者抽两张牌以避免伤害的分支，用 `PLAY_CARD.mode = TARGET_CONTROLLER_CHOOSES_DRAW_2` 记录当前 2P preflight 选择。
155. 已完成：迁移《暗中破坏》基础路径，补 `OPPONENT_HAND_CARD` 目标范围、通用目标 forbidden tag 校验、从对手手牌回收到主牌堆底部原语，以及对手手牌单位牌目标拒绝测试；展示手牌的玩家视角提示细节暂缓。
156. 已完成：迁移《莲花陷阱》本回合内受到的所有伤害翻倍路径，补通用伤害调整原语，并用同回合《焚烧》验证 2 点伤害翻倍为 4 点。
157. 已完成：迁移《反击风暴》本回合内下一次伤害抵挡并抽 1 张牌路径，补通用伤害抵挡原语，并用同回合《焚烧》验证 2 点伤害被抵挡为 0 且效果被消耗。
158. 已完成：迁移《诺克萨斯断头台》本回合内下次受到伤害触发摧毁的基础路径，补通用伤害触发摧毁原语，并用同回合《焚烧》验证 2 点非致命伤害也会触发摧毁；`鼓舞` 立即摧毁分支暂缓。
159. 已完成：迁移《虹吸能量》当前单战场区域模型下友方战场单位 +1、敌方战场单位 -1 且不低于 1 的本回合内战力修正路径；精确“选择一处战场”的多战场位置模型暂缓。
160. 已完成：迁移《月之降临》当前单战场区域模型下敌方战场单位 -2 的本回合内战力修正路径；可选移动敌方单位和精确“选择一处战场”的多战场位置模型暂缓。
161. 已完成：迁移《魅惑妖术》当前目的地受限模型下敌方战场单位移动到所属基地的代表路径；完整目的地选择暂缓到多位置移动模型。
162. 已完成：迁移《升龙踢》未启用等级 6 时敌方战场单位移动到所属基地的代表路径；完整目的地选择和等级 6 眩晕暂缓。
163. 已完成：迁移《驭风而行》当前目的地受限模型下友方战场单位移动到所属基地并变为活跃状态的代表路径；完整目的地选择暂缓到多位置移动模型。
164. 已完成：迁移《妖异狐火》战场单位任意数量、总战力不高于 4 的多目标摧毁路径，补总目标战力上限校验和总战力超过 4 的拒绝测试；一处战场精确位置暂缓到多战场位置模型。
165. 已完成：迁移《帝国谕令》当前场上单位本回合受伤即摧毁代表路径，复用 `DESTROY_ON_NEXT_DAMAGE_THIS_TURN` 并用同回合《焚烧》验证非致命伤害触发摧毁；后续新进场单位暂缓到全局持续效果模型。
166. 已完成：迁移《精灵召唤》当前目的地受限代表路径，复用单位指示物打出到基地原语，验证 3 战力“精灵”创建并记录 `瞬息` 标签；完整目的地选择和瞬息到期摧毁暂缓。
167. 已完成：迁移《精灵迸发》当前目的地受限代表路径，复用单位指示物打出到基地原语，验证两名 3 战力“精灵”创建并记录 `瞬息` 标签；完整目的地选择和瞬息到期摧毁暂缓。
168. 已完成：迁移《顽皮触手》选择总战力不高于 8 的敌方战场单位移动到所属基地代表路径，复用总目标战力上限和多目标移动到基地原语，并补总战力超过 8 的拒绝测试；同一位置目的地和多控制者约束暂缓。
169. 已完成：迁移《诱饵》不支付回响时移动一名敌方单位到另一名敌方单位所在位置的基础路径，补 `ENEMY_UNIT_THEN_ENEMY_UNIT` 目标范围、目标位置移动原语和友方/重复目标拒绝测试；回响与多战场精确位置暂缓。
170. 已完成：迁移《羽毛旋风》选择战鹰模式时打出四名 1 战力“战鹰”到基地的路径，补单位指示物 tags 原语并记录 `法盾` 标记；无效化法术模式和法盾额外选取费用模型暂缓。
171. 已完成：迁移《游击战》最多两张己方废牌堆待命牌返回手牌的基础路径，补通用目标 required tag 校验和 `待命` 标签；本回合免费正面朝下布置待命牌权限暂缓到待命布置模型。
172. 已完成：迁移《暗影的召唤》让友方非瞬息单位获得 `瞬息` 标签后抽 2 张牌的基础路径，补持久对象 tag 添加事件和已有瞬息目标拒绝测试；开始阶段摧毁瞬息单位暂缓到关键词清理模型。
173. 已完成：迁移《致命华彩》对一名敌方单位造成 3 点非致命伤害的基础路径，复用敌方单位目标范围和伤害原语，并补友方目标拒绝测试；本回合摧毁后的休眠“金币”装备指示物触发暂缓到装备指示物和延迟触发模型。
174. 已完成：迁移《逝水如镜》让一名战场单位获得 `瞬息` 标签的基础路径，复用战场单位目标范围和对象 tag 添加事件，并补基地单位目标拒绝测试；装备目标已在第 185 项补齐，下个回合开始阶段摧毁瞬息对象暂缓。
175. 已完成：迁移《背水一战》让一名友方单位本回合内按当前战力翻倍并获得 `瞬息` 标签的基础路径，补目标当前战力动态战力修正原语和敌方单位目标拒绝测试；迅捷时机和下个回合开始阶段摧毁瞬息单位暂缓。
176. 已完成：迁移《痛苦之酬》战场单位 3 点伤害并打出休眠“金币”装备指示物到基地的基础路径，补最小装备指示物对象标签原语和基地单位目标拒绝测试；待命时机和金币资源技能暂缓。
177. 已完成：迁移《化为灰烬》让一件装备获得 `瞬息` 标签的基础路径，补最小 `EQUIPMENT` 目标范围和单位目标拒绝测试；开始阶段摧毁瞬息对象暂缓。
178. 已完成：迁移《印爆术》摧毁一件装备并让其控制者抽 2 张牌的基础路径，补 `EQUIPMENT_DESTROYED` 事件和装备摧毁不写入本回合单位摧毁记忆的断言；单位目标拒绝测试已覆盖。
179. 已完成：迁移《火箭轰击》摧毁装备模式，复用 `PLAY_CARD.mode = DESTROY_EQUIPMENT`、`EQUIPMENT` 目标范围和 `EQUIPMENT_DESTROYED` 事件；单位目标拒绝测试已覆盖，回响暂缓。
180. 已完成：迁移《紧急召回》让一件装备返回其拥有者手牌的基础路径，补 `EQUIPMENT_RETURNED_TO_HAND` 事件、`EQUIPMENT` 目标范围复用和单位目标拒绝测试；装备贴附/卸除细节暂缓。
181. 已完成：迁移《热电光束》摧毁所有场上装备的基础路径，补 `DestroysAllEquipment` 原语和双方装备摧毁 fixture；非装备单位不受影响，装备摧毁不写入本回合单位摧毁记忆。
182. 已完成：迁移《血钱》摧毁战场上不高于 2 战力单位后按敌/友方创建 1/2 枚休眠“金币”装备指示物路径，补高战力目标拒绝测试。
183. 已完成：迁移《折戟再战》当前 2P preflight 中双方各摧毁一件自己装备的基础路径，补 `FRIENDLY_EQUIPMENT_THEN_ENEMY_EQUIPMENT` 目标范围和单位目标拒绝测试。
184. 已完成：补齐《废物利用》选择一件装备时先摧毁该装备、再抽 1 张牌的分支，复用 `EQUIPMENT` 目标范围和 `EQUIPMENT_DESTROYED` 事件；单位目标拒绝测试已覆盖。
185. 已完成：补齐《逝水如镜》选择一件装备并给予 `瞬息` 标签的目标分支，补 `BATTLEFIELD_UNIT_OR_EQUIPMENT` 目标范围并复用 `OBJECT_TAG_ADDED` 事件；基地单位拒绝测试继续覆盖。
186. 已完成：补齐《精灵召唤》和《精灵迸发》单位指示物的 `瞬息` 对象标签，fixture 事件 payload 与最终对象状态均断言 `tags = ["瞬息"]`；瞬息到期摧毁仍暂缓到关键词清理模型。
187. 已完成：迁移《镜花水月》当前最小对象模型路径，补 `CreatedBaseUnitTokenCopiesFirstTarget` 原语，验证“映像”复制目标当前战力与标签后在控制者基地活跃进场并获得 `瞬息`；手牌目标由直接拒绝测试覆盖，完整复制牌面和不触发打出效果暂缓。
188. 已完成：迁移《罪恶快感》弃牌后按被弃手牌法力费用造成伤害的基础路径，补 `manaCost` 对象元数据、`FRIENDLY_HAND_CARD_THEN_BATTLEFIELD_UNIT` 目标顺序和对手手牌目标拒绝测试；符能费用无视已按当前 `manaCost` 字段边界记录。
189. 已完成：迁移《次元门狂欢》当前 2P preflight 的两种对手选择模式，补 `DrawsControllerAndOtherPlayers` 与 `CallsRuneForControllerAndOtherPlayers` 原语；`OTHER_PLAYERS_CHOOSE_CARDS` 验证双方各抽 1，`OTHER_PLAYERS_CHOOSE_RUNES` 验证双方各召出 1 枚休眠符文，多人逐玩家选择暂缓。
190. 已完成：迁移《丛林伏击》打出休眠“金币”装备指示物的当前 preflight 路径，复用装备指示物创建原语并断言 `CARD_TYPE:EQUIPMENT` 和 `isExhausted = true`；本回合友方单位活跃进场的全局效果暂缓到单位打出模型。
191. 已完成：迁移《沙兵现身》控制 0 件武装时的活跃化分支，目标必须是两名带 `黄沙士兵` 标签的友方单位，双方让过后让两名目标变为活跃状态；按武装数量创建黄沙士兵的动态分支暂缓。
192. 已完成：迁移《集结部队》当前 preflight 的即时抽 1 路径，支付 2 点费用、0 目标入栈并抽牌；友方单位进场给予增益的全局触发暂缓到单位打出/延迟触发模型。
193. 已完成：迁移《迎敌号令》当前 preflight 的即时抽 1 路径，支付 2 点费用、0 目标入栈并抽牌；本回合单位活跃进场的全局效果暂缓到单位打出模型。
194. 已完成：迁移《秘奥义！慈悲度魂落》当前 preflight 的单目标增益路径，支付 3 点费用并给予友方单位 `增益` 标签和永久 +1 战力；本回合所有增益额外 +1 的全局效果暂缓。
195. 已完成：迁移《叹为观止》当前 preflight 的基地友方单位增益并移动到战场路径，支付 1 点费用、目标限定为己方基地单位，结算后给予 `增益` 标签、永久 +1 战力并移动到当前单战场区域。
196. 已完成：迁移《虚空猛冲》当前 preflight 不选择免费打出牌的抽取展示牌路径，支付 2 点费用、0 目标入栈，结算后按主牌堆顶顺序抽 2 张牌；免费打出分支暂缓。
197. 已完成：迁移《公开行动》当前 preflight 无既有增益可消耗时给予所有友方单位增益的路径，支付 5 点费用、0 目标入栈，结算后每名未拥有增益的友方单位获得 `增益` 标签并永久 +1 战力；消耗增益让单位活跃分支暂缓。
198. 已完成：迁移《镜中幻影》当前 preflight 的互换位置并抽牌路径，支付 2 点费用、目标限定为两名不同公开区域的友方单位且至少一名拥有 `瞬息`，结算后互换位置并抽 1；无 `瞬息` 目标组合由直接测试拒绝。
199. 已完成：迁移《天声震落》当前 preflight 的基地友方单位战力范围伤害并移动路径，支付 6 点费用、目标限定为己方基地单位，结算后按目标战力伤害敌方战场单位并将目标移动到当前单战场区域。
200. 已完成：迁移《战斗号令》当前 2P preflight 的目标顺序移动路径，支付 3 点费用、第一目标限定为友方单位、第二目标限定为敌方单位，结算后将两名基地单位移动到当前粗粒度战场区域；完整对手选择 prompt 和多战场精确位置暂缓。
201. 已完成：迁移《弹幕时间》当前 preflight 的任意符能支付范围伤害路径，使用 `optionalCosts = ["SPEND_POWER:n"]` 记录本次支付符能，支付 1 点法力和 3 点符能后按支付数值伤害敌方战场单位；符能不足由直接测试拒绝。
202. 已完成：迁移《传送门大营救》当前 preflight 的友方单位放逐后重新打出到所属基地路径，支付 3 点费用、目标限定为带 `CARD_TYPE:UNIT` 标签的友方单位，结算后清除场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接测试拒绝。
203. 已完成：迁移《狩猎律动》当前 preflight 的友方单位放逐后重新打出到当前粗粒度战场路径，支付 2 点费用、目标限定为带 `CARD_TYPE:UNIT` 标签的友方单位，结算后清除场上伤害、本回合内战力修正和本回合内效果；敌方目标由直接测试拒绝。
204. 已完成：迁移《蚀魂夜》当前 preflight 的己方废牌堆单位打出到基地路径，支付 6 点费用、目标限定为己方废牌堆带 `CARD_TYPE:UNIT` 标签的单位牌，结算后将其打出到基地并变为活跃状态；非单位目标由直接测试拒绝，完整目的地选择暂缓。
205. 已完成：迁移《虚空来袭》当前 preflight 的友方单位后接敌方单位移动路径，支付 2 点费用、目标按顺序限定为带 `CARD_TYPE:UNIT` 标签的友方单位和敌方单位，结算后将两者移动到当前粗粒度战场；目标顺序反转由直接测试拒绝，战场控制/进攻方细节暂缓。
206. 已完成：迁移《猛龙摆尾》当前 preflight 的敌方单位移动后互伤路径，支付 4 点费用、目标按顺序限定为两名不同敌方单位，结算后第一目标移动到第二目标所在位置，两者按当前战力互伤并执行致命伤害清理；友方/重复目标由直接测试拒绝，多战场精确目的地暂缓。
207. 已完成：迁移《冷酷追击》当前 preflight 的友方单位移动与本回合征服后可召回标记路径，支付 2 点费用、目标限定为带 `CARD_TYPE:UNIT` 标签的友方场上单位，结算后移动到所属基地并添加 `MAY_RETURN_TO_BASE_ON_CONQUER_THIS_TURN`；敌方目标和非单位友方对象由直接测试拒绝，可选贴附武装和征服后触发结算暂缓。
208. 已完成：迁移《魄罗佳肴》当前 preflight 的装备入场抽牌路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象并抽 1；带目标打出由直接测试拒绝，自毁激活抽牌技能暂缓。
209. 已完成：迁移《舒瑞娅的安魂曲》当前 preflight 的专属装备入场并活跃友方单位路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并让控制者所有单位变为活跃状态；带目标打出由直接测试拒绝，唯我和装配技能暂缓。
210. 已完成：迁移《未来熔炉》当前 preflight 的装备入场并创建随从路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并打出一名带 `CARD_TYPE:UNIT` 标签的 1 战力“随从”到控制者基地；带目标打出由直接测试拒绝，摧毁装备回收废牌堆分支暂缓。
211. 已完成：迁移《废料堆》当前 preflight 的装备入场抽牌路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象并抽 1；带目标打出由直接测试拒绝，弃置和摧毁触发抽牌分支暂缓。
212. 已完成：迁移《精灵提灯》当前 preflight 的瞬息装备入场并创建精灵路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象，并打出一名带 `瞬息` 标签的 3 战力“精灵”到控制者基地；带目标打出由直接测试拒绝，绝念和开始阶段瞬息摧毁暂缓。
213. 已完成：迁移《地沟区地图》当前 preflight 的瞬息装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象；带目标打出由直接测试拒绝，对手得分触发抽牌和开始阶段瞬息摧毁暂缓。
214. 已完成：迁移《占卜花朵》当前 preflight 的休眠装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接测试拒绝，洞察/抽牌/经验激活技能暂缓。
215. 已完成：迁移《魔法鲜豆》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，法术对决期间单位打出触发抽牌分支暂缓。
216. 已完成：迁移《钢铁弩炮》当前 preflight 的休眠装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为休眠的 `CARD_TYPE:EQUIPMENT` 装备对象；带目标打出由直接测试拒绝，横置伤害技能暂缓。
217. 已完成：迁移《玄冰之心》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置战力修正技能暂缓。
218. 已完成：迁移《懊悔法球》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置负战力修正技能暂缓。
219. 已完成：迁移《灵魂之剑》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配绿色贴附分支暂缓。
220. 已完成：迁移《锯齿短匕》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配红色贴附分支暂缓。
221. 已完成：迁移《多兰之盾》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配绿色贴附分支暂缓。
222. 已完成：迁移《海克斯注力刚壁》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配蓝色贴附分支暂缓。
223. 已完成：迁移《多兰之刃》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配橙色贴附分支暂缓。
224. 已完成：迁移《多兰之戒》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配紫色贴附分支暂缓。
225. 已完成：迁移《先锋之眼》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配黄色贴附分支暂缓。
226. 已完成：迁移《反曲之弓》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配红色贴附分支暂缓。
227. 已完成：迁移《残暴之力》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配绿色贴附分支暂缓。
228. 已完成：迁移《守护天使》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配绿色贴附分支暂缓。
229. 已完成：迁移《海克斯饮魔刀》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配橙色贴附分支暂缓。
230. 已完成：迁移《狂徒铠甲》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配橙色贴附分支暂缓。
231. 已完成：迁移《三相之力》当前 preflight 的装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配橙色贴附分支暂缓。
232. 已完成：迁移《轻灵之靴》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配紫色贴附分支暂缓。
233. 已完成：迁移《萃取》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配紫色贴附分支暂缓。
234. 已完成：迁移《神圣剪刀》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配黄色贴附分支暂缓。
235. 已完成：迁移《暴风大剑》当前 preflight 的装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配黄色贴附分支暂缓。
236. 已完成：迁移《云游图鉴》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配蓝色贴附分支暂缓。
237. 已完成：迁移《阿瑞昂的陨落》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配 1 红色贴附分支暂缓。
238. 已完成：迁移《猎人的宽刃刀》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配橙色贴附分支暂缓。
239. 已完成：迁移《枯萎战斧》当前 preflight 的装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配 1 红色贴附分支暂缓。
240. 已完成：迁移《碎骨棒》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配 1 橙色贴附分支暂缓。
241. 已完成：迁移《远古簇碑》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置资源技能暂缓。
242. 已完成：迁移《海克斯异常体》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置资源技能暂缓。
243. 已完成：迁移《能量通道》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置资源技能暂缓。
244. 已完成：迁移《预时之门》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置回响授予技能暂缓。
245. 已完成：迁移《邪鸦魔典》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置法术伤害修正技能暂缓。
246. 已完成：迁移《太阳圆盘》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置鼓舞技能暂缓。
247. 已完成：迁移《远见面具》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，战斗触发暂缓。
248. 已完成：迁移《烈阳圣坛》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，摧毁触发抽牌分支暂缓。
249. 已完成：迁移《炼金科技桶》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，对手回合法术触发打出金币分支暂缓。
250. 已完成：迁移《灵魂之轮》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，友方单位目标触发支付并抽牌分支暂缓。
251. 已完成：迁移《蘑菇袋》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，开始阶段正面朝下待命牌抽牌触发暂缓。
252. 已完成：迁移《竞技场酒吧》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置给予休眠友方单位增益技能暂缓。
253. 已完成：迁移《海盗避风港》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，友方单位变为活跃时战力修正触发暂缓。
254. 已完成：迁移《被遗忘的路标》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，迅捷横置移动技能暂缓。
255. 已完成：迁移《冰封宝石》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，抽第二张牌触发战力修正分支暂缓。
256. 已完成：迁移《倾颓宫殿》当前 preflight 的装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，开始阶段胜利条件和横置创建战鹰分支暂缓。
257. 已完成：迁移《猩红玫瑰》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，单位打出触发经验和横置活跃单位技能暂缓。
258. 已完成：迁移《逆转碎片》当前 preflight 的装备入场路径，支付 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，开始阶段摧毁触发分支暂缓。
259. 已完成：迁移《装配架》当前 preflight 的装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置创建机器人技能暂缓。
260. 已完成：迁移《斯弗尔尚歌》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配贴附和复制技能文字分支暂缓。
261. 已完成：迁移《Z型驱动》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配和放逐打出分支暂缓。
262. 已完成：迁移《先锋军备》当前 preflight 的装备入场路径，支付 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置创建多个随从分支暂缓。
263. 已完成：迁移《追忆祭坛》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，友方单位摧毁触发和牌堆放置选择暂缓。
264. 已完成：迁移《暴怒之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得红色符能技能暂缓。
265. 已完成：迁移《专注之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得绿色符能技能暂缓。
266. 已完成：迁移《洞察之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得蓝色符能技能暂缓。
267. 已完成：迁移《力量之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得橙色符能技能暂缓。
268. 已完成：迁移《不和之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得紫色符能技能暂缓。
269. 已完成：迁移《团结之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得黄色符能技能暂缓。
270. 已完成：迁移 OGN 版《暴怒之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得红色符能技能暂缓。
271. 已完成：迁移 OGN 版《专注之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得绿色符能技能暂缓。
272. 已完成：迁移 OGN 版《洞察之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得蓝色符能技能暂缓。
273. 已完成：迁移 OGN 版《力量之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得橙色符能技能暂缓。
274. 已完成：迁移 OGN 版《不和之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得紫色符能技能暂缓。
275. 已完成：迁移 OGN 版《团结之印》当前 preflight 的 0 费装备入场路径，支付 0 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置获得黄色符能技能暂缓。
276. 已完成：迁移《奇妙行囊》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置回手技能暂缓。
277. 已完成：迁移《塞壬号》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，支付并横置移动技能暂缓。
278. 已完成：迁移《无主宝藏》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，离场触发和自毁激活技能暂缓。
279. 已完成：迁移《拾荒小能手》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，回收/支付/横置抽牌技能暂缓。
280. 已完成：迁移《雾临剑冢》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，增益触发和支付休眠分支暂缓。
281. 已完成：迁移《闪耀极光》当前 preflight 的装备入场路径，支付 9 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，回合结束展示与免费打出分支暂缓。
282. 已完成：迁移《烈阳徽记》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，战斗平局触发和全体召回分支暂缓。
283. 已完成：迁移《先锋之盔》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，增益单位摧毁触发和增益分配暂缓。
284. 已完成：迁移《蜜糖果实》当前 preflight 的休眠装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌以休眠状态进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，横置资源技能和等级 6 分支暂缓。
285. 已完成：迁移《临终仪式》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配和废牌堆回收分支暂缓。
286. 已完成：迁移《破败王者之刃》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配、额外费用和贴附分支暂缓。
287. 已完成：迁移《来路不明的武器》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，弃牌横置和摧毁替代效果暂缓。
288. 已完成：迁移《海兽钓钩》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，激活、摧毁、查看和免费打出分支暂缓。
289. 已完成：迁移《禁魔石丰碑》当前 preflight 的瞬息装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 和 `瞬息` 标签的装备对象；带目标打出由直接测试拒绝，友方单位法盾静态效果和开始阶段瞬息摧毁暂缓。
290. 已完成：迁移《中娅沙漏》当前 preflight 的装备入场路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，待命/反应时机和摧毁替代召回效果暂缓。
291. 已完成：迁移《忠诚不渝》当前 preflight 的低费用废牌堆单位打出路径，支付 2 点费用，目标限定为己方废牌堆中带 `CARD_TYPE:UNIT` 且 `manaCost <= 2` 的单位，结算后将该单位打出到基地；非单位和费用过高目标由直接测试拒绝，动物属性减费和完整目的地选择暂缓。
292. 已完成：迁移《夜之锋刃》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，待命正面朝下打出、即时贴附和装配暂缓。
293. 已完成：迁移《炉火斗篷》当前 preflight 的专属装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，唯我构筑限制和装配贴附暂缓。
294. 已完成：迁移《灭世者的死亡之冠》当前 preflight 的专属装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，唯我构筑限制和装配贴附暂缓。
295. 已完成：迁移《喷射球果》当前 preflight 的不选择可选移动装备入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由当前 no-move 路径直接测试拒绝，可选移动、移动触发休眠和眩晕暂缓。
296. 已完成：迁移《奥义！魂佑》当前 preflight 的装备入场并给予友方单位增益路径，支付 2 点费用，目标限定为友方单位，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并给予目标 `增益` 标签和永久 +1 战力；敌方目标由直接测试拒绝，拥有增益友方单位获得法盾的静态效果暂缓。
297. 已完成：补《大幕渐起》未支付回响的基础活跃化路径，支付 2 点费用、指定一名单位，结算后只让目标变为活跃状态一次；支付回响重复路径由既有 fixture 继续覆盖。
298. 已完成：补《点沙成兵》未支付回响的基础指示物路径，支付 2 点费用、0 目标入栈，结算后只打出一名 2 战力“黄沙士兵”；支付回响重复创建路径由既有 fixture 继续覆盖。
299. 已完成：迁移《夺命名单》当前 preflight 的装备入场路径，支付 1 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，宣告属性标签和横置战力修正技能暂缓。
300. 已完成：迁移 promo 编号《碎骨棒》当前 preflight 的装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，装配贴附分支暂缓。
301. 已完成：迁移《海克斯科技护手》当前 preflight 的专属装备入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；带目标打出由直接测试拒绝，动态装配费用和贴附分支暂缓。
302. 已完成：迁移《受诅咒的石棺》当前 preflight 的装备入场并放逐己方废牌堆单位牌路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象，并将控制者废牌堆中带 `CARD_TYPE:UNIT` 标签的单位牌移入放逐区；带目标打出由直接测试拒绝，横置摧毁自身并打出放逐单位牌的激活技能暂缓。
303. 已完成：迁移《占卜贝壳》当前 preflight 的装备入场加预知路径，支付 2 点费用后源牌进入控制者基地成为带 `CARD_TYPE:EQUIPMENT` 标签的装备对象；覆盖选择回收主牌堆顶部一张牌与不回收两条分支，非顶部牌选择由直接测试拒绝，迅捷摧毁并横置给予 +2 战力的激活技能暂缓。
304. 已完成：迁移《强制征召》当前 preflight 的未支付经验额外费用路径，支付 5 点费用、选择战场上一名不高于 3 战力且带 `CARD_TYPE:UNIT` 标签的敌方单位，结算后获得其控制权、使其休眠并放入控制者基地；4 战力目标由直接测试拒绝，支付 5 经验选择任意敌方单位分支暂缓。
305. 已完成：迁移《据为己有》当前 preflight 的获得敌方战场单位控制权并召回路径，支付 8 点费用、选择战场上一名带 `CARD_TYPE:UNIT` 标签的敌方单位，结算后获得其控制权并放入控制者基地；非单位战场对象由直接测试拒绝，完整 owner/controller 分离模型暂缓。
306. 已完成：迁移《宝藏魔像》当前 preflight 的单位入场并创建金币路径，支付 8 点费用、0 目标入栈，结算后源牌进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，并打出四个休眠的 `CARD_TYPE:EQUIPMENT` “金币”装备指示物；带目标打出由直接测试拒绝，完整目的地选择暂缓。
307. 已完成：迁移《忠实的工坊主》当前 preflight 的单位入场并创建随从路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 1 战力“随从”单位指示物；带目标打出由直接测试拒绝，精确“此处”目的地暂缓。
308. 已完成：迁移《皇家守卫》当前 preflight 的单位入场并创建黄沙士兵路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 2 战力且带 `黄沙士兵` 标签的单位指示物；带目标打出由直接测试拒绝，精确“此处”目的地暂缓。
309. 已完成：迁移《苍炎守护者》当前 preflight 的单位入场并战力修正路径，支付 8 点费用、选择一名单位，结算后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，并让目标本回合内战力 +8；非单位目标由直接测试拒绝。
310. 已完成：迁移《爆裂球果仙灵》当前 preflight 的普通打出单位入场并负战力修正路径，支付 2 点费用、选择一名单位，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让目标本回合内战力 -2 且不得低于 1；非单位目标由直接测试拒绝，待命/反应路径暂缓。
311. 已完成：迁移《调皮猎手》当前 preflight 的单位入场并创建战鹰路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 1 战力且带 `法盾` 标签的“战鹰”单位指示物；带目标打出由直接测试拒绝，精确“此处”目的地暂缓。
312. 已完成：迁移《见习工程师》当前 preflight 的单位入场并装备回手路径，支付 3 点费用、选择己方废牌堆一件装备，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让目标装备返回手牌；非装备废牌堆目标由直接测试拒绝。
313. 已完成：迁移《永黯潜伏者》当前 preflight 的单位入场、弃牌并抽牌路径，支付 3 点费用、选择另一张己方手牌，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，弃置目标手牌并抽 1 张牌；以源牌自身作为弃置目标由直接测试拒绝。
314. 已完成：迁移《牧灵犬》当前 preflight 的单位入场并单位回手路径，支付 3 点费用、选择己方废牌堆一张单位牌，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让目标单位牌返回手牌；非单位废牌堆目标由直接测试拒绝。
315. 已完成：迁移《安妮》当前 preflight 的单位入场并法术回手路径，支付 4 点费用、选择己方废牌堆一张法术牌，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让目标法术牌返回手牌；非法术废牌堆目标由直接测试拒绝。
316. 已完成：迁移《迅捷蟹》当前 preflight 的单位入场并抽牌路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 0 战力 `CARD_TYPE:UNIT` 单位对象，并抽 1 张牌；带目标打出由直接测试拒绝，绝念分支暂缓。
317. 已完成：迁移《约德尔教官》当前 preflight 的单位入场并抽牌路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并抽 1 张牌；带目标打出由直接测试拒绝。
318. 已完成：迁移《精灵之母》当前 preflight 的单位入场并创建精灵路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 3 战力且带 `瞬息` 标签的“精灵”单位指示物；带目标打出由直接测试拒绝，精确“此处”和瞬息到期暂缓。
319. 已完成：迁移《怒海大鲨炮》当前 preflight 的单位入场并伤害路径，支付 6 点费用、选择敌方战场单位，结算后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并对目标造成 6 点非致命伤害；友方目标由直接测试拒绝。
320. 已完成：迁移《流沙术士》当前 preflight 的单位入场并摧毁小型敌方单位路径，支付 5 点费用、选择一名不高于 3 战力的敌方单位，结算后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并摧毁目标；4 战力目标由直接测试拒绝。
321. 已完成：迁移《祖安保镖》当前 preflight 的单位入场并战场单位回手路径，支付 4 点费用、选择战场上一名单位，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让目标返回其拥有者手牌；基地目标由直接测试拒绝。
322. 已完成：迁移《龙骑兵》当前 preflight 的单位入场并摧毁敌方单位路径，支付 8 点费用、选择一名敌方单位，结算后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并摧毁目标；友方目标由直接测试拒绝。
323. 已完成：迁移《大副》当前 preflight 的单位入场并让另一名单位变为活跃状态路径，支付 3 点费用、选择一名单位，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并令目标变为活跃状态；非单位目标由直接测试拒绝。
324. 已完成：迁移《竞技场新人》当前 preflight 的单位入场并给予友方单位增益路径，支付 2 点费用、选择另一名友方单位，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并给予目标 `增益` 标签和永久 +1 战力；敌方目标由直接测试拒绝。
325. 已完成：迁移《斩剑浪客》当前 preflight 的单位入场并可选摧毁装备路径，支付 3 点费用、可选择一件装备，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并摧毁所选装备；不选择装备可正常入栈，单位目标由直接测试拒绝。
326. 已完成：迁移《烈阳盾卫》当前 preflight 的单位入场并眩晕单位路径，支付 3 点费用、选择一名单位，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并对目标施加本回合内 `STUNNED`；非单位目标由直接测试拒绝。
327. 已完成：迁移《雷爪氏族熊人》当前 preflight 的单位入场并召出休眠符文路径，支付 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力且带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并召出 1 枚休眠符文；带目标打出由直接测试拒绝。
328. 已完成：迁移《均衡僧侣》当前 preflight 的单位入场并给予两个友方单位增益路径，支付 4 点费用、选择两名友方单位，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，并分别给予两个目标 `增益` 标签和永久 +1 战力；敌方目标由直接测试拒绝。
329. 已完成：迁移《阴森药剂师》当前 preflight 的单位入场并可选友方战场单位回手路径，支付 3 点费用、可选择一名友方战场单位，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让目标返回手牌；不选择目标可正常入栈，敌方战场目标由直接测试拒绝，伏击路径暂缓。
330. 已完成：迁移《吟风翼》当前 preflight 的单位入场并可选小型战场单位回手路径，支付 2 点费用、可选择一名不高于 3 战力的战场单位，结算后源牌进入控制者基地成为 1 战力且带 `待命` 标签的 `CARD_TYPE:UNIT` 单位对象，并让目标返回手牌；不选择目标可正常入栈，4 战力目标由直接测试拒绝，待命/反应路径暂缓。
331. 已完成：迁移《晶能阻断器》当前 preflight 的单位入场并给予单位本回合内 `ROAM` 路径，支付 2 点费用、选择一名单位，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，并让目标获得本回合内 `ROAM`；装备目标由直接测试拒绝。
332. 已完成：迁移《邪焰巨龙 卡德雷格林》当前 preflight 的单位入场并按强力单位抽牌路径，支付 9 点费用、0 目标入栈，结算后源牌进入控制者基地成为 9 战力 `CARD_TYPE:UNIT` 单位对象，并按控制者场上强力单位数量抽牌；带目标打出由直接测试拒绝。
333. 已完成：迁移《疯狂海寇》当前 preflight 的单位入场并移动战场单位回基地路径，支付 5 点费用、选择一名战场单位，结算后源牌进入控制者基地成为 4 战力且带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象，并将目标移动到其所属基地；基地目标由直接测试拒绝。
334. 已完成：迁移《海渊巨兽》当前 preflight 的单位入场并按友方/敌方双目标回手路径，支付 7 点费用、按顺序选择另一名友方单位和一名敌方单位，结算后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象，并让两个目标分别返回所属者手牌；目标顺序反转由直接测试拒绝。
335. 已完成：迁移《泡泡机》当前 preflight 的单位入场并友方机械活跃化路径，支付 3 点费用、选择另一名带 `CARD_TYPE:UNIT` 与 `机械` 标签的友方单位，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，并让目标变为活跃状态；非机械单位由直接测试拒绝。
336. 已完成：迁移《精灵女王》当前 preflight 的单位入场并创建精灵路径，支付 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象，并打出一名 3 战力且带 `瞬息` 标签的“精灵”单位指示物；带目标打出由直接测试拒绝，开始阶段重复创建和瞬息到期暂缓。
337. 已完成：迁移《仙灵龙》当前 preflight 的单位入场并给予最多四名友方单位增益路径，支付 7 点费用、可选择最多四名友方单位，结算后源牌进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，并分别给予目标 `增益` 标签和永久 +1 战力；不选择目标可正常入栈，敌方目标由直接测试拒绝，消耗增益打出金币分支暂缓。
338. 已完成：迁移《伊泽瑞尔》当前 preflight 的单位入场并弃置另一张手牌抽 2 路径，支付 3 点费用、选择另一张己方手牌，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标并抽 2 张牌；以源牌自身作为弃置目标由直接测试拒绝，其他卡牌的可选减费分支暂缓。
339. 已完成：迁移《烈阳首领》当前 preflight 的单位入场并眩晕/摧毁已眩晕敌方单位路径，支付 5 点费用、选择一名敌方单位，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象；未眩晕目标获得本回合内 `STUNNED`，已眩晕目标改为摧毁并进入拥有者废牌堆；友方单位目标由直接测试拒绝。
340. 已完成：迁移《芭茹队长》当前 preflight 的单位入场并抽牌模式，支付 3 点费用、选择 `DRAW_1` 模式且 0 目标入栈，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后抽 1 张牌；缺失模式由直接测试拒绝，自身增益分支暂缓。
341. 已完成：迁移《炼金太保》当前 preflight 的单位入场并弃置另一张手牌路径，支付 2 点费用、选择另一张己方手牌，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标；以源牌自身作为弃置目标由直接测试拒绝，强攻战斗关键词暂缓。
342. 已完成：迁移《金克丝》当前 preflight 的单位入场并弃置另外两张手牌路径，支付 3 点费用、选择另外两张己方手牌，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后按目标顺序弃置两张牌；目标包含源牌自身由直接测试拒绝，急速和强攻战斗关键词暂缓。
343. 已完成：迁移《船坞潜伏者》当前 preflight 的无卡面效果单位源牌入场路径，支付 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
344. 已完成：迁移《先锋中士》当前 preflight 的无卡面效果单位源牌入场路径，支付 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
345. 已完成：迁移《贪玩的小鬼》当前 preflight 的无卡面效果单位源牌入场路径，支付 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
346. 已完成：迁移《超能机甲》当前 preflight 的无卡面效果单位源牌入场路径，支付 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 8 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
347. 已完成：迁移《山脉亚龙》当前 preflight 的无卡面效果单位源牌入场路径，支付 9 点费用、0 目标入栈，结算后源牌进入控制者基地成为 10 战力 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
348. 已完成：迁移《辟心玄龙》当前 preflight 的单位入场并弃置一张对手手牌路径，支付 7 点费用、选择一张对手手牌，结算后源牌进入控制者基地成为 7 战力 `CARD_TYPE:UNIT` 单位对象，然后让该手牌所属对手弃置目标；友方手牌目标由直接测试拒绝。
349. 已完成：迁移《魅惑之灵》当前 preflight 的单位入场并让被选择玩家弃置一张手牌路径，支付 3 点费用、选择一张任意玩家手牌对象代表被选择玩家实际弃置的手牌，结算后源牌进入控制者基地成为 2 战力、带 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象，然后让该手牌所属玩家弃置目标；己方手牌弃置和非手牌目标拒绝由直接测试覆盖。
350. 已完成：迁移《提莫》`OGN·197/298` 当前 preflight 的单位入场并自身本回合内战力 +3 路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后自身本回合内战力 +3；带目标打出由直接测试拒绝，待命/反应路径暂缓。
351. 已完成：迁移《提莫》`OGN·197a/298` 当前 preflight 的单位入场并自身本回合内战力 +3 路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后自身本回合内战力 +3；带目标打出由直接测试拒绝，待命/反应路径暂缓。
352. 已完成：迁移《提莫》`OGN·197b/298` 当前 preflight 的单位入场并自身本回合内战力 +3 路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后自身本回合内战力 +3；带目标打出由直接测试拒绝，待命/反应路径暂缓。
353. 已完成：迁移《瑟提》`SFD·232/221` 当前 preflight 的单位入场并自身获得增益路径，支付 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力；带目标打出由直接测试拒绝，征服触发与消耗增益激活暂缓。
354. 已完成：迁移《瑟提》`SFD·232*/221` 当前 preflight 的单位入场并自身获得增益路径，支付 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力；带目标打出由直接测试拒绝，征服触发与消耗增益激活暂缓。
355. 已完成：迁移《瑟提》`OGN·164/298` 当前 preflight 的单位入场并自身获得增益路径，支付 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力；带目标打出由直接测试拒绝，征服触发与消耗增益激活暂缓。
356. 已完成：迁移《瑟提》`OGN·164a/298` 当前 preflight 的单位入场并自身获得增益路径，支付 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后给予自身 `增益` 标签和永久 +1 战力；带目标打出由直接测试拒绝，征服触发与消耗增益激活暂缓。
357. 已完成：迁移《提莫》`FND-196/298` 当前 preflight 的单位入场并自身本回合内战力 +3 路径，支付 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `待命` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象，然后自身本回合内战力 +3；带目标打出由直接测试拒绝，待命/反应路径暂缓。
358. 已完成：迁移《伊泽瑞尔》`SFD·149a/221` 当前 preflight 的单位入场并弃置另一张手牌抽 2 路径，支付 3 点费用、选择另一张己方手牌，结算后源牌进入控制者基地成为 3 战力 `CARD_TYPE:UNIT` 单位对象，然后弃置目标并抽 2 张牌；以源牌自身作为弃置目标由直接测试拒绝，其他卡牌可选额外费用减免暂缓。
359. 已完成：迁移《金克丝》`OGN·030a/298` 当前 preflight 的单位入场并弃置另外两张手牌路径，支付 3 点费用、选择另外两张己方手牌，结算后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT` 单位对象，然后按目标顺序弃置两张牌；目标包含源牌自身由直接测试拒绝，急速和强攻战斗关键词暂缓。
360. 已完成：迁移《爆破队学员》`SFD·013/221` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象；额外 1 和红色费用与伤害分支暂缓，带目标打出由直接测试拒绝。
361. 已完成：迁移《霜衣幼崽》`SFD·067/221` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象；蓝色额外费用与战力 -2 分支暂缓，带目标打出由直接测试拒绝。
362. 已完成：迁移《船猿》`SFD·098/221` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象；额外 1 费用与自身增益分支暂缓，带目标打出由直接测试拒绝。
363. 已完成：迁移《派克》`UNL-028/219` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `待命` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；红色额外费用、条件活跃/+2、待命反应时机和游走移动路径暂缓，带目标打出由直接测试拒绝。
364. 已完成：迁移《派克》`UNL-028a/219` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `待命` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；红色额外费用、条件活跃/+2、待命反应时机和游走移动路径暂缓，带目标打出由直接测试拒绝。
365. 已完成：迁移《小小守护者》`OGN·044/298` 当前 preflight 的单位入场且不支付可选额外费用路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力 `CARD_TYPE:UNIT` 单位对象；绿色额外费用与抽 1 分支暂缓，带目标打出由直接测试拒绝。
366. 已完成：迁移《灼焰飞龙》`OGN·001/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `龙` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场路径暂缓，带目标打出由直接测试拒绝。
367. 已完成：迁移《军团后卫》`OGN·010/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `崔法利` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场路径暂缓，带目标打出由直接测试拒绝。
368. 已完成：迁移《小鲨鱼》`UNL-006/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `急速` 与 `强攻4` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和强攻战斗修正暂缓，带目标打出由直接测试拒绝。
369. 已完成：迁移《雷克塞》`SFD·029/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `强攻` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、强攻战斗修正和从手牌以外打出的友方单位获得急速路径暂缓，带目标打出由直接测试拒绝。
370. 已完成：迁移《雷克塞》`SFD·029a/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `强攻` 与 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、强攻战斗修正和从手牌以外打出的友方单位获得急速路径暂缓，带目标打出由直接测试拒绝。
371. 已完成：迁移《卡莎》`OGN·039/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和征服抽牌触发暂缓，带目标打出由直接测试拒绝。
372. 已完成：迁移《卡莎》`OGN·039a/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和征服抽牌触发暂缓，带目标打出由直接测试拒绝。
373. 已完成：迁移《雷恩加尔》`UNL-024/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `强攻2`、`急速`、`法盾`、`游走` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、强攻战斗修正、法盾目标税和游走移动路径暂缓，带目标打出由直接测试拒绝。
374. 已完成：迁移《雷恩加尔》`UNL-024a/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `强攻2`、`急速`、`法盾`、`游走` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、强攻战斗修正、法盾目标税和游走移动路径暂缓，带目标打出由直接测试拒绝。
375. 已完成：迁移《尼菈》`UNL-115/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速`、`恶魔` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、游走移动和移动获得经验触发暂缓，带目标打出由直接测试拒绝。
376. 已完成：迁移《厄运小姐》`OGN·162/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `急速`、`海盗` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、游走移动和移动触发变为活跃路径暂缓，带目标打出由直接测试拒绝。
377. 已完成：迁移《厄运小姐》`OGN·162a/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `急速`、`海盗` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、游走移动和移动触发变为活跃路径暂缓，带目标打出由直接测试拒绝。
378. 已完成：迁移《希维尔》`SFD·143/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和万能符能支付条件下的战力 +2/游走路径暂缓，带目标打出由直接测试拒绝。
379. 已完成：迁移《希维尔》`SFD·143a/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和万能符能支付条件下的战力 +2/游走路径暂缓，带目标打出由直接测试拒绝。
380. 已完成：迁移《莉莉娅》`UNL-082/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、移动触发精灵指示物和瞬息到期暂缓，带目标打出由直接测试拒绝。
381. 已完成：迁移《莉莉娅》`UNL-082a/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、移动触发精灵指示物和瞬息到期暂缓，带目标打出由直接测试拒绝。
382. 已完成：迁移《阿兹尔》`SFD·177/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 和 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和进攻触发移动指示物单位路径暂缓，带目标打出由直接测试拒绝。
383. 已完成：迁移《阿兹尔》`SFD·177a/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 和 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和进攻触发移动指示物单位路径暂缓，带目标打出由直接测试拒绝。
384. 已完成：迁移《树根先生》`UNL-127/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、移动和获得经验触发暂缓，带目标打出由直接测试拒绝。
385. 已完成：迁移《机械迷》`SFD·068/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、武装贴附和双倍基础战力加成路径暂缓，带目标打出由直接测试拒绝。
386. 已完成：迁移《琢珥鱼》`SFD·103/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和按强力单位减少费用路径暂缓，带目标打出由直接测试拒绝。
387. 已完成：迁移《卡银娜·薇蕊泽》`SFD·179/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、移动触发和随从指示物创建暂缓，带目标打出由直接测试拒绝。
388. 已完成：迁移《绯红印记树怪》`UNL-029/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、征服额外触发和征服后增益路径暂缓，带目标打出由直接测试拒绝。
389. 已完成：迁移《绯红印记树怪》`UNL-029a/219` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、征服额外触发和征服后增益路径暂缓，带目标打出由直接测试拒绝。
390. 已完成：迁移《美味仙灵》`OGN·075/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `仙灵` 和 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场和绝念触发暂缓，带目标打出由直接测试拒绝。
391. 已完成：迁移《艾克》`OGN·110/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、绝念回收和符文活跃化路径暂缓，带目标打出由直接测试拒绝。
392. 已完成：迁移《武装强袭者》`SFD·002/221` 当前 preflight 的单位入场且不支付急速可选额外费用、不选择百炼装配路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `急速` 和 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
393. 已完成：迁移《远古战狂》`SFD·131/221` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `急速` 和 `灵体` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、战场位置和动态强攻数值暂缓，带目标打出由直接测试拒绝。
394. 已完成：迁移《海妖猎手》`OGN·150/298` 当前 preflight 的单位入场且不支付急速可选额外费用、不消耗增益路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `强攻`、`急速` 和 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、强攻战斗修正和消耗增益减费路径暂缓，带目标打出由直接测试拒绝。
395. 已完成：迁移《李青》`OGN·151/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、战场位置和友方增益单位静态战力修正暂缓，带目标打出由直接测试拒绝。
396. 已完成：迁移《李青》`OGN·151a/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象；急速活跃进场、战场位置和友方增益单位静态战力修正暂缓，带目标打出由直接测试拒绝。
397. 已完成：迁移《千尾监视者》`OGN·116/298` 当前 preflight 的单位入场且不支付急速可选额外费用路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 7 战力、带 `急速` 标签的 `CARD_TYPE:UNIT` 单位对象，并让所有敌方单位本回合内战力 -3、不得低于 1；急速活跃进场路径暂缓，带目标打出由直接测试拒绝。
398. 已完成：迁移《呸呸魄罗》`OGN·013/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `法盾` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税暂缓，带目标打出由直接测试拒绝。
399. 已完成：迁移《强强魄罗》`OGN·052/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `坚守` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；防守战力修正暂缓，带目标打出由直接测试拒绝。
400. 已完成：迁移《莽莽魄罗》`OGN·210/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `强攻` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻方战力修正暂缓，带目标打出由直接测试拒绝。
401. 已完成：迁移《躁烈的副官》`OGN·215/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `强攻` 和 `海盗` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻方战力修正暂缓，带目标打出由直接测试拒绝。
402. 已完成：迁移《和风贤者》`OGS·005/024` 当前 preflight 的 keyword-only 单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `坚守` 和 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象；防守战力修正暂缓，带目标打出由直接测试拒绝。
403. 已完成：迁移《帕卡幼崽》`OGN·135/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `待命` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象；待命正面朝下和反应打出路径暂缓，带目标打出由直接测试拒绝。
404. 已完成：迁移《纳沃利侦察兵》`SFD·037/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税暂缓，带目标打出由直接测试拒绝。
405. 已完成：迁移《劳伦特剑使》`SFD·156/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `强攻2` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻方战力修正暂缓，带目标打出由直接测试拒绝。
406. 已完成：迁移《贪食魔沼蛙》`UNL-100/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `狩猎3` 标签的 `CARD_TYPE:UNIT` 单位对象；征服/据守获得经验路径暂缓，带目标打出由直接测试拒绝。
407. 已完成：迁移《哨兵好手》`SFD·008/221` 当前 preflight 的单位入场且不选择百炼装配路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `哨兵` 和 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
408. 已完成：迁移《战斗厨神》`SFD·092/221` 当前 preflight 的单位入场且不选择百炼装配路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
409. 已完成：迁移《壮壮魄罗》`SFD·099/221` 当前 preflight 的单位入场且不选择百炼装配路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `百炼` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
410. 已完成：迁移《炳文大师》`SFD·127/221` 当前 preflight 的单位入场且不选择百炼装配路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
411. 已完成：迁移《呸呸魄罗》`UNL-220/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `法盾` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税暂缓，带目标打出由直接测试拒绝。
412. 已完成：迁移《壮壮魄罗》`UNL-223/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `百炼` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配和武装贴附路径暂缓，带目标打出由直接测试拒绝。
413. 已完成：迁移《莽莽魄罗》`UNL-225/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `强攻` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻方战力修正暂缓，带目标打出由直接测试拒绝。
414. 已完成：迁移《变异猫咪》`UNL-036/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `坚守2`、`壁垒` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象；防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接测试拒绝。
415. 已完成：迁移《魁梧斗士》`UNL-099/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `坚守2` 和 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象；防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接测试拒绝。
416. 已完成：迁移《劳伦特护刃者》`SFD·096/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；游走移动路径暂缓，带目标打出由直接测试拒绝。
417. 已完成：迁移《盖伦》`OGS·007/024` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `坚守2`、`强攻2` 和 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻/防守战力修正暂缓，带目标打出由直接测试拒绝。
418. 已完成：迁移《日耀卫队》`OGN·054/298` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `坚守` 和 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象；防守战力修正和壁垒承伤顺序暂缓，带目标打出由直接测试拒绝。
419. 已完成：迁移《叨叨魄罗》`UNL-224/219` 当前 preflight 的单位入场后预知回收路径，支付基础 2 点费用、选择己方主牌堆顶部一张牌，结算后源牌进入控制者基地成为 2 战力、带 `预知` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部；选择非顶部牌由直接测试拒绝。
420. 已完成：迁移《叨叨魄罗》`OGN·171/298` 当前 preflight 的单位入场后预知回收路径，支付基础 2 点费用、选择己方主牌堆顶部一张牌，结算后源牌进入控制者基地成为 2 战力、带 `预知` 和 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部；选择非顶部牌由直接测试拒绝。
421. 已完成：迁移《宝石巨像》`OGN·086/298` 当前 preflight 的单位入场后预知回收路径，支付基础 5 点费用、选择己方主牌堆顶部一张牌，结算后源牌进入控制者基地成为 5 战力、带 `坚守` 和 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部；防守战力修正暂缓，选择非顶部牌由直接测试拒绝。
422. 已完成：迁移《大塞斥候》`OGN·174/298` 当前 preflight 的单位入场后预知回收路径，支付基础 6 点费用、选择己方主牌堆顶部一张牌，结算后源牌进入控制者基地成为 5 战力、带 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部；开放战场打出路径暂缓，选择非顶部牌由直接测试拒绝。
423. 已完成：迁移《烬》`UNL-089/219` 当前 preflight 的英雄单位入场后预知回收路径，支付基础 4 点费用、选择己方主牌堆顶部一张牌，结算后源牌进入控制者基地成为 4 战力、带 `预知` 标签的 `CARD_TYPE:UNIT` 单位对象，并将所选顶部牌回收到主牌堆底部；替代打出费用路径暂缓，选择非顶部牌由直接测试拒绝。
424. 已完成：迁移《好斗的龙犬》`SFD·006/221` 当前 preflight 的活跃单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、未休眠且带 `犬形` 和 `龙` 标签的 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
425. 已完成：迁移《易》`OGS·009/024` 当前 preflight 的活跃英雄单位入场路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、未休眠且带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；游走移动路径暂缓，带目标打出由直接测试拒绝。
426. 已完成：迁移《先锋扈从》`OGS·016/024` 当前 preflight 的活跃单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、未休眠且带 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象；带目标打出由直接测试拒绝。
427. 已完成：迁移《沃里克》`OGN·159/298` 当前 preflight 的活跃英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、未休眠且带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻触发摧毁已受伤敌方单位路径暂缓，带目标打出由直接测试拒绝。
428. 已完成：迁移《沃里克》`ARC-004/006` 当前 preflight 的活跃英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、未休眠且带 `犬形` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻触发摧毁已受伤敌方单位路径暂缓，带目标打出由直接测试拒绝。
429. 已完成：迁移《艾蕾，头号拥趸》`UNL-041/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `法盾` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；给予此处其他单位法盾的静态效果暂缓，带目标打出由直接测试拒绝。
430. 已完成：迁移《薇古丝》`UNL-055/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `坚守`、`壁垒` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；眩晕敌方战场单位后的可选移动触发暂缓，带目标打出由直接测试拒绝。
431. 已完成：迁移《野爪兽王》`UNL-057/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 7 战力、带 `壁垒` 和 `猫科` 标签的 `CARD_TYPE:UNIT` 单位对象；此处低战力单位无法被敌方法术或技能选作目标的静态限制暂缓，带目标打出由直接测试拒绝。
432. 已完成：迁移《超大型约德尔人》`SFD·055/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 10 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `坚守5`、`壁垒` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；本回合通过据守得分后的费用减少路径暂缓，带目标打出由直接测试拒绝。
433. 已完成：迁移《缇亚娜·冕卫》`SFD·060/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 和 `精锐` 标签的 `CARD_TYPE:UNIT` 单位对象；位于战场时对手无法得分的静态限制暂缓，带目标打出由直接测试拒绝。
434. 已完成：迁移《烬》`UNL-022/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；移动时获得费用资源的触发路径暂缓，带目标打出由直接测试拒绝。
435. 已完成：迁移《烬》`UNL-022a/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 和 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；移动时获得费用资源的触发路径暂缓，带目标打出由直接测试拒绝。
436. 已完成：迁移《蔚》`UNL-030/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；支付资源令自身本回合内战力翻倍的激活路径暂缓，带目标打出由直接测试拒绝。
437. 已完成：迁移《蔚》`UNL-030a/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；支付资源令自身本回合内战力翻倍的激活路径暂缓，带目标打出由直接测试拒绝。
438. 已完成：迁移《乐芙兰》`UNL-090/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `后排` 标签的 `CARD_TYPE:UNIT` 单位对象；所处战场瞬息效果不会触发的静态路径暂缓，带目标打出由直接测试拒绝。
439. 已完成：迁移《热情的播报员》`UNL-043/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `后排` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；据守战场后的群体增益触发路径暂缓，带目标打出由直接测试拒绝。
440. 已完成：迁移《踏苔蜥》`UNL-047/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `犬形` 和 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 3 以上的 +1 战力与法盾路径暂缓，带目标打出由直接测试拒绝。
441. 已完成：迁移《特雷弗·达顿尔》`UNL-048/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `坚守` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；据守战场后的活跃 `精灵` 生成触发路径暂缓，带目标打出由直接测试拒绝。
442. 已完成：迁移《风行狐》`UNL-075/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `犬形` 和 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 3 以上的 +1 战力与游走路径暂缓，带目标打出由直接测试拒绝。
443. 已完成：迁移《晶手猎人》`UNL-094/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `狩猎` 和 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 6 以上的 +1 战力路径暂缓，带目标打出由直接测试拒绝。
444. 已完成：迁移《焰爪》`UNL-016/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `犬形` 和 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 3 以上的 +1 战力与活跃进场路径暂缓，带目标打出由直接测试拒绝。
445. 已完成：迁移《无极学徒》`UNL-040/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 6 以上的打出抽 1 路径暂缓，带目标打出由直接测试拒绝。
446. 已完成：迁移《竞技场人气王》`UNL-102/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象；经验消耗给予自身增益路径暂缓，带目标打出由直接测试拒绝。
447. 已完成：迁移《易》`UNL-113/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 6 以上的法盾与游走路径暂缓，带目标打出由直接测试拒绝。
448. 已完成：迁移《易》`UNL-113a/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `狩猎2` 标签的 `CARD_TYPE:UNIT` 单位对象；等级 6 以上的法盾与游走路径暂缓，带目标打出由直接测试拒绝。
449. 已完成：迁移《卡兹克》`UNL-119/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻触发、经验消耗和伤害路径暂缓，带目标打出由直接测试拒绝。
450. 已完成：迁移《卡兹克》`UNL-119a/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象；进攻触发、经验消耗和伤害路径暂缓，带目标打出由直接测试拒绝。
451. 已完成：迁移《黑色玫瑰要员》`UNL-152/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `强攻` 标签的 `CARD_TYPE:UNIT` 单位对象；强攻战斗修正与绝念召符文路径暂缓，带目标打出由直接测试拒绝。
452. 已完成：迁移《惊艳守护者》`UNL-162/219` 当前 preflight 的 keyword-only 单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `仙灵` 和 `狩猎` 标签的 `CARD_TYPE:UNIT` 单位对象；经验消耗给予自身增益路径暂缓，带目标打出由直接测试拒绝。
453. 已完成：迁移《加里奥》`UNL-171/219` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `壁垒` 和 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、壁垒承伤顺序和无法造成战斗伤害路径暂缓，带目标打出由直接测试拒绝。
454. 已完成：迁移《芮尔》`SFD·024/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象；壁垒承伤顺序、进攻触发、免费打出武装与贴附路径暂缓，带目标打出由直接测试拒绝。
455. 已完成：迁移《贾克斯》`SFD·054/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税与手牌武装灵便静态授予路径暂缓，带目标打出由直接测试拒绝。
456. 已完成：迁移《贾克斯》`SFD·054a/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税与手牌武装灵便静态授予路径暂缓，带目标打出由直接测试拒绝。
457. 已完成：迁移《巨腕加藤》`SFD·112/221` 当前 preflight 的 keyword-only 单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、移动触发、关键词授予和本回合战力修正路径暂缓，带目标打出由直接测试拒绝。
458. 已完成：迁移《赵信》`SFD·176/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `壁垒` 标签的 `CARD_TYPE:UNIT` 单位对象；壁垒承伤顺序和条件活跃进场路径暂缓，带目标打出由直接测试拒绝。
459. 已完成：迁移《希维尔》`SFD·120/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 7 战力、带 `法盾2` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾2目标税、进攻征服、过量伤害检查和伤害触发路径暂缓，带目标打出由直接测试拒绝。
460. 已完成：迁移《希维尔》`SFD·120a/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 7 战力、带 `法盾2` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾2目标税、进攻征服、过量伤害检查和伤害触发路径暂缓，带目标打出由直接测试拒绝。
461. 已完成：迁移《德莱文》`SFD·148/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、战斗胜利得分和战斗中被摧毁的对手得分路径暂缓，带目标打出由直接测试拒绝。
462. 已完成：迁移《德莱文》`SFD·148a/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、战斗胜利得分和战斗中被摧毁的对手得分路径暂缓，带目标打出由直接测试拒绝。
463. 已完成：迁移《薇恩》`SFD·223/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `哨兵` 和 `强攻3` 标签的 `CARD_TYPE:UNIT` 单位对象；强攻战斗修正、条件活跃进场、征服触发、支付1和回手路径暂缓，带目标打出由直接测试拒绝。
464. 已完成：迁移《薇恩》`SFD·223*/221` 当前 preflight 的 keyword-only 英雄单位 promo 入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `哨兵` 和 `强攻3` 标签的 `CARD_TYPE:UNIT` 单位对象；强攻战斗修正、条件活跃进场、征服触发、支付1和回手路径暂缓，带目标打出由直接测试拒绝。
465. 已完成：迁移《艾瑞莉娅》`SFD·225/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、被选择为目标或准备时本回合 +1 路径暂缓，带目标打出由直接测试拒绝。
466. 已完成：迁移《艾瑞莉娅》`SFD·225*/221` 当前 preflight 的 keyword-only 英雄单位 promo 入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税、被选择为目标或准备时本回合 +1 路径暂缓，带目标打出由直接测试拒绝。
467. 已完成：迁移《亚索》`SFD·235/221` 当前 preflight 的 keyword-only 英雄单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；游走移动和单回合第三次移动得分路径暂缓，带目标打出由直接测试拒绝。
468. 已完成：迁移《亚索》`SFD·235*/221` 当前 preflight 的 keyword-only 英雄单位 promo 入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；游走移动和单回合第三次移动得分路径暂缓，带目标打出由直接测试拒绝。
469. 已完成：迁移《厄斐琉斯》`SFD·224/221` 当前 preflight 的 vanilla 英雄单位入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；武装贴附后的三选一触发路径暂缓，带目标打出由直接测试拒绝。
470. 已完成：迁移《厄斐琉斯》`SFD·224*/221` 当前 preflight 的 vanilla 英雄单位 promo 入场路径，支付基础 4 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；武装贴附后的三选一触发路径暂缓，带目标打出由直接测试拒绝。
471. 已完成：迁移《阿狸》`SFD·227/221` 当前 preflight 的 vanilla 英雄单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；进攻或防守时敌方单位本回合 -2 且不得低于 1 的触发路径暂缓，带目标打出由直接测试拒绝。
472. 已完成：迁移《阿狸》`SFD·227*/221` 当前 preflight 的 vanilla 英雄单位 promo 入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；进攻或防守时敌方单位本回合 -2 且不得低于 1 的触发路径暂缓，带目标打出由直接测试拒绝。
473. 已完成：迁移《永恩》`SFD·233/221` 当前 preflight 的单位入场且不选择百炼装配路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `恶魔` 和 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配、武装贴附和征服开放战场后的基地伤害路径暂缓，带目标打出由直接测试拒绝。
474. 已完成：迁移《永恩》`SFD·233*/221` 当前 preflight 的单位入场且不选择百炼装配 promo 路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 5 战力、带 `恶魔` 和 `百炼` 标签的 `CARD_TYPE:UNIT` 单位对象；百炼装配、武装贴附和征服开放战场后的基地伤害路径暂缓，带目标打出由直接测试拒绝。
475. 已完成：迁移《德莱厄斯》`SFD·236/221` 当前 preflight 的属性英雄单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `崔法利` 标签的 `CARD_TYPE:UNIT` 单位对象；鼓舞活跃进场和同处其他友方单位静态 +1 路径暂缓，带目标打出由直接测试拒绝。
476. 已完成：迁移《德莱厄斯》`SFD·236*/221` 当前 preflight 的属性英雄单位 promo 入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `崔法利` 标签的 `CARD_TYPE:UNIT` 单位对象；鼓舞活跃进场和同处其他友方单位静态 +1 路径暂缓，带目标打出由直接测试拒绝。
477. 已完成：迁移《竞技场理事》`UNL-001/219` 当前 preflight 的活跃单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、未休眠且带 `约德尔人` 标签的 `CARD_TYPE:UNIT` 单位对象；横置战力修正技能暂缓，带目标打出由直接测试拒绝。
478. 已完成：迁移《不朽凤凰》`OGN·037/298` 当前 preflight 的属性/关键词单位入场路径，支付基础 3 点费用、0 目标入栈，结算后源牌进入控制者基地成为 3 战力、带 `灵体` 和 `强攻2` 标签的 `CARD_TYPE:UNIT` 单位对象；强攻战斗修正和废牌堆打出触发暂缓，带目标打出由直接测试拒绝。
479. 已完成：迁移《亡花掠食者》`OGN·161/298` 当前 preflight 的关键词单位入场路径，支付基础 8 点费用、0 目标入栈，结算后源牌进入控制者基地成为 8 战力、带 `法盾` 标签的 `CARD_TYPE:UNIT` 单位对象；法盾目标税和敌方控制战场替代目的地路径暂缓，带目标打出由直接测试拒绝。
480. 已完成：迁移《传承者雷芙纳》`UNL-005/219` 当前 preflight 的关键词单位入场路径，支付基础 7 点费用、0 目标入栈，结算后源牌进入控制者基地成为 7 战力、带 `游走` 标签的 `CARD_TYPE:UNIT` 单位对象；游走移动和法术打出触发活跃路径暂缓，带目标打出由直接测试拒绝。
481. 已完成：迁移《莽林巨象》`UNL-008/219` 当前 preflight 的关键词单位入场路径，支付基础 6 点费用、0 目标入栈，结算后源牌进入控制者基地成为 6 战力、带 `强攻` 标签的 `CARD_TYPE:UNIT` 单位对象；强攻战斗修正和本回合有单位被摧毁时活跃进场路径暂缓，带目标打出由直接测试拒绝。
482. 已完成：迁移《哀哀魄罗》`SFD·036/221` 当前 preflight 的属性单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；绝念抽牌触发暂缓，带目标打出由直接测试拒绝。
483. 已完成：迁移《哀哀魄罗》`UNL-221/219` 当前 preflight 的属性单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 2 战力、带 `魄罗` 标签的 `CARD_TYPE:UNIT` 单位对象；绝念抽牌触发暂缓，带目标打出由直接测试拒绝。
484. 已完成：迁移《警觉的哨兵》`OGN·096/298` 当前 preflight 的单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；绝念抽牌触发暂缓，带目标打出由直接测试拒绝。
485. 已完成：迁移《侦察飞鹰》`OGN·216/298` 当前 preflight 的属性单位入场路径，支付基础 2 点费用、0 目标入栈，结算后源牌进入控制者基地成为 1 战力、带 `鸟类` 标签的 `CARD_TYPE:UNIT` 单位对象；绝念召符文触发暂缓，带目标打出由直接测试拒绝。
486. 已完成：迁移《机械戏法师》`OGN·239/298` 当前 preflight 的单位入场路径，支付基础 5 点费用、0 目标入栈，结算后源牌进入控制者基地成为 4 战力、无额外标签的 `CARD_TYPE:UNIT` 单位对象；绝念创建随从触发暂缓，带目标打出由直接测试拒绝。
487. 下一步：按“同能力族小批次”节奏迁移更多低复杂度官方卡牌，优先继续从单位入场、0 目标抽牌/召符文/创建指示物、单目标伤害、单目标回手/移动、本回合战力修正/标签添加和简单装备对象中选择 `3-5` 张同机制卡牌；批末统一全量验证并提交。

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
