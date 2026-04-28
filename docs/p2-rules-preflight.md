# P2 核心规则前置审查

更新时间：2026-04-28

## 1. 目的

本文件是进入 P2 核心规则实现前的执行清单。它把符文资源、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS` 和清理流程先从五份 PDF/FAQ 中拆成可实现的状态、事件和 fixture，避免继续继承旧 Java 的 `PASS -> TURN_ENDED` 混淆。

本文件不是规则全文摘录。实现时仍必须回到本地五份 PDF/FAQ 和官网卡牌快照核对。

## 2. 当前裁决

| 能力 | 裁决 | 证据 | 对旧 Java 的态度 |
|---|---|---|---|
| 符文池 | 法力/符能先进入玩家符文池，再用于支付费用；抽牌阶段结束和回合结束都会清空所有玩家符文池。 | `CORE-260330` p20 rules 164-167, p28-p31 rules 315.4, 317.2 | Java snapshot 可作对照，但 P2 必须建服务端权威资源状态。 |
| `END_TURN` | 表示主阶段没有要执行的自决行动，随后进入回合结束阶段。 | `CORE-260330` p29-p31 rules 316.1-317.3; `JFAQ-251023` p6-p7 questions 5.1-5.2 | Java 粗粒度事件可临时对照，最终事件要拆细。 |
| `PASS_PRIORITY` | 只表示 FEPR 流程中让过优先行动权；不能等同结束回合。 | `CORE-260330` p27-p28 rules 312-313, p33-p35 rules 333-340 | `java-oracle-p1-pass` 的 `TURN_ENDED` 是 legacy mismatch candidate。 |
| `PASS_FOCUS` | 只表示法术对决中让过焦点；所有玩家依次让过才关闭法术对决。 | `CORE-260330` p35-p36 rules 341-348; `JFAQ-251023` p4-p5 questions 3.1-3.3 | 需要新 fixture，不能从裸 `PASS` 推断。 |
| 清理/特殊清理 | 清理期间不结算合法项目，不授予/传递优先行动权或焦点；若清理改变状态导致再次满足清理条件，继续清理至稳定。回合结束和战斗清理是特殊清理。 | `CORE-260330` p31-p33 rules 318-324; `JFAQ-251023` p6-p7 questions 5.1-5.4 | P2 事件和状态机必须显式支持重复清理。 |

## 2.1 当前实现状态

已完成第一批代码地基：

- `ConformanceFixture` 已支持 schema v2 的 `initialState`、`expected.finalState`、`expected.events`、`expected.snapshots`、`expected.prompts` 和 `cardObjects`。
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-turn-start.fixture.json` 已记录回合开始、召出符文、抽牌、清空符文池、进入主阶段的规则审查样例。
- `MatchState` 已加入 `turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`、`cardObjects`，snapshot timing 已投影 timing 字段，玩家 `handSize` 和 `score` 来自权威状态。
- runner 已能把 P2 `initialState` 应用为真实初始局面，包含 turn/phase/timing、符文池和玩家区域。
- `CoreRuleEngine` 已实现普通回合开始最小流程，并通过 `p2-preflight-turn-start.fixture.json` 验证：召出 2 张符文、抽 1 张牌、清空所有玩家符文池、进入主阶段。
- `p2-preflight-turn-start-short-rune-deck.fixture.json` 已验证符文牌堆不足 2 张时有多少召出多少。
- `p2-preflight-turn-start-first-p2-extra-rune.fixture.json` 已验证 1v1 第二个行动玩家在自己首个召出阶段额外召出 1 张符文。
- `p2-preflight-turn-start-burnout.fixture.json` 已验证抽牌阶段主牌堆为空且废牌堆有牌可回收时：执行燃尽、对手得 1 分、回收废牌堆并完成抽牌。
- `p2-preflight-end-turn-advances-to-next-start.fixture.json` 已验证 P1 主阶段 `END_TURN` 会记录回合结束声明、执行无伤害/无持续效果的最小特殊清理、清空符文池、推进到 P2，并自动结算 P2 回合开始。
- `p2-preflight-end-turn-special-cleanup.fixture.json` 已验证回合结束特殊清理会移除单位伤害、令期限为本回合内的效果同时失效、清空符文池并进入下一回合开始流程。
- `p2-preflight-cleanup-repeats-until-stable.fixture.json` 已验证特殊清理造成对象状态变化后追加一次常规清理检查，并且不重复执行回合结束特殊步骤。
- `p2-preflight-pass-priority-does-not-end-turn.fixture.json` 已验证普通主阶段误提交 `PASS_PRIORITY` 会以 `PHASE_NOT_ALLOWED` 拒绝，不产生 `TURN_END_DECLARED`，不推进 tick，也不结束回合。
- `p2-preflight-fepr-priority-pass-resolves-stack.fixture.json` 已验证有结算链项目时，当前优先权玩家让过后优先权转移，所有玩家让过后结算最新项目并回到普通主阶段。
- `p2-preflight-fepr-resolves-latest-keeps-remaining-stack.fixture.json` 已验证最新项目结算后若结算链仍不为空，则新的最新项目控制者获得优先行动权并维持闭环。

尚未完成：

- 废牌堆也为空导致连续燃尽/胜利判定尚未建模。

## 3. P2 最小状态模型

在扩展 `MatchState` 时，优先加入以下服务端权威字段。玩家视角 snapshot 只投影允许看到的部分。

| 状态域 | 最小字段 | 用途 |
|---|---|---|
| 回合 | `turnNumber`, `turnPlayerId`, `phase`, `timingState` | 区分回合开始、主阶段、回合结束、普通/法术对决、开环/闭环。 |
| 行动权 | `priorityPlayerId`, `focusPlayerId`, `passedPriorityPlayerIds`, `passedFocusPlayerIds` | 表达谁能自决行动，以及连续让过何时推进。 |
| 符文资源 | `runePools[playerId].mana`, `runePools[playerId].power` | 费用支付、抽牌阶段结束清空、回合结束清空。 |
| 区域 | `mainDeck`, `runeDeck`, `hand`, `base`, `battlefields`, `graveyard`, `banished`, `legendZone`, `championZone` | 后续打出、召出、抽牌、公开/私密/隐秘信息边界。 |
| 对象状态 | `cardObjects[objectId].damage`, `cardObjects[objectId].untilEndOfTurnEffects` | 回合结束特殊清理移除伤害、令本回合内效果失效；后续扩展战力、控制者、附属关系等完整对象字段。 |
| 结算链 | `stackItems` 已落地；`pendingTasks`, `resolvingItemId` 后续补 | HOT FEPR、确认、执行、让过、结算。 |
| 清理 | `cleanupKind`, `cleanupIteration`, `pendingCleanupReasons` | 普通清理、回合结束特殊清理、战斗特殊清理和重复清理。 |
| 随机 | `seed`, `rngCursor` | 洗牌、抽牌和随机选择可重放。 |

## 4. P2 事件词表先行

进入实现前先稳定事件名，后续 fixture 和 UI 都依赖这些名称。

| 事件 | 触发时机 |
|---|---|
| `TURN_START_BEGAN` | 新回合玩家成为回合玩家后，开始回合开始流程。 |
| `OBJECTS_READIED` | 唤醒阶段使可活跃对象变为活跃。 |
| `BATTLEFIELDS_SECURED` | 得分计算步骤据守战场。 |
| `RUNES_CALLED` | 召出阶段从符文牌堆召出符文。 |
| `CARD_DRAWN` / `BURNOUT_APPLIED` | 抽牌阶段抽牌或牌堆不足时燃尽。 |
| `RUNE_POOL_CLEARED` | 抽牌阶段结束或回合结束清空符文池。 |
| `MAIN_PHASE_BEGAN` | 主阶段开始，回合玩家获得普通开环行动窗口。 |
| `TURN_END_DECLARED` | 回合玩家提交 `END_TURN`。 |
| `TURN_END_CLEANUP_STARTED` | 回合结束特殊清理开始。 |
| `DAMAGE_REMOVED` | 回合结束特殊清理移除单位伤害。 |
| `UNTIL_END_OF_TURN_EXPIRED` | 本回合内期限效果失效。 |
| `TURN_PLAYER_ADVANCED` | 回合队列推进到下一位玩家。 |
| `PRIORITY_PASSED` | `PASS_PRIORITY` 成功让过优先行动权。 |
| `FOCUS_PASSED` | `PASS_FOCUS` 成功让过焦点。 |
| `STACK_ITEM_RESOLVED` | 结算链最新项目完成结算。 |
| `CLEANUP_REPEATED` | 清理造成状态变化后再次启动清理。 |

P1 的 `TURN_ENDED`、`TURN_BEGAN`、`RUNE_CHANNELLED`、`CARD_DRAWN` 仍保留为 legacy placeholder，不应作为 P2 正式事件设计。

## 5. 第一批 P2 Fixture

这些 fixture 必须先成为 `RULE_AUDITED`，再实现对应规则。

| Fixture ID | 输入 | 期望重点 | 依据 |
|---|---|---|---|
| `p2-turn-start-runes-and-draw` | P2 非首个回合成为回合玩家，符文牌堆至少 2 张，主牌堆至少 1 张 | 召出 2 张符文，抽 1 张牌，抽牌阶段结束清空符文池，进入主阶段。 | `CORE-260330` p28-p29 rule 315; p20 rules 164-167; rule 481.7 |
| `p2-turn-start-short-rune-deck` | P2 非首个回合，符文牌堆不足 2 张 | 有多少召出多少，不越界，不报错。 | `CORE-260330` p28-p29 rule 315.3; rule 481.7 |
| `p2-turn-start-first-p2-extra-rune` | P2 作为第二个行动玩家的首个回合，符文牌堆至少 3 张 | 召出 3 张符文，抽 1 张牌，清空符文池，进入主阶段。 | `CORE-260330` p28-p29 rule 315.3; rule 481.7 |
| `p2-turn-start-burnout` | P2 非首个回合，主牌堆为空，废牌堆有 1 张牌 | 执行燃尽，对手得 1 分，废牌堆回收后抽 1 张牌，进入主阶段。 | `CORE-260330` p28-p29 rule 315.4; p57 rule 413.4; p90 rule 431.2 |
| `p2-end-turn-advances-to-next-start` | P1 主阶段没有伤害和本回合内持续效果，P2 牌堆足够 | 记录回合结束声明，执行最小特殊清理，清空符文池，推进回合玩家，并自动结算 P2 回合开始。 | `CORE-260330` p29-p31 rules 316.1-317.3; p20 rules 164-167; p28-p29 rule 315; rule 481.7 |
| `p2-end-turn-special-cleanup` | 主阶段有未消耗资源、单位伤害和本回合内效果 | 记录回合结束声明，移除伤害，本回合内效果失效，清空符文池，推进回合玩家，并自动进入下一回合开始。 | `CORE-260330` p30-p31 rules 317.2.a-317.2.f; `JFAQ-251023` p6-p7 questions 5.1-5.2 |
| `p2-pass-priority-does-not-end-turn` | 普通主阶段没有结算链时误提交 `PASS_PRIORITY` | 不产生 `TURN_END_DECLARED`，不推进 tick，不结束回合，并返回 `PHASE_NOT_ALLOWED`。 | `CORE-260330` p33-p35 rules 333-340 |
| `p2-fepr-priority-pass-resolves-stack` | 有已确认结算链项目，双方依次让过优先行动权 | 让过转移优先行动权，所有玩家让过后结算最新项目。 | `CORE-260330` p33-p35 rules 333-340 |
| `p2-fepr-resolves-latest-keeps-remaining-stack` | 最新结算链项目结算后仍有较早项目留存 | 维持闭环，并由新的最新项目控制者获得优先行动权。 | `CORE-260330` p35 rule 340.4 |
| `p2-spell-duel-pass-focus-closes-window` | 非战斗法术对决开环，双方没有新增法术 | 焦点传递；所有玩家让过焦点后关闭法术对决并进行清理。 | `CORE-260330` p35-p36 rules 341-348; `JFAQ-251023` p4-p5 questions 3.1-3.3 |
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
14. 下一步：实现 `PASS_FOCUS` 和法术对决最小流程。

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
