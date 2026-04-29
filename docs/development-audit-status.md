# 当前开发审查状态

更新时间：2026-04-29

## 1. 结论

当前已开发内容仍然可以作为 P1 工程骨架保留，不需要立刻推倒重写。但任何涉及规则裁决的内容，不能再以“已对齐 Java”为完成标准，必须经过五份 PDF/FAQ 重审。

当前总体判断：

- 工程底座：继续保留并迭代。
- 规则占位实现：保留为测试骨架，但标记为 `NEEDS_RULE_AUDIT`。
- Java fixture：保留为 legacy oracle 对照，但不再作为最终 expected 的来源。
- 开发计划：需要调整为“先规则索引和审查状态，再扩展更多规则”。

## 2. 已开发内容状态

| 范围 | 当前状态 | 是否需要修改 | 处理决定 |
|---|---|---|---|
| `.NET 10` solution、项目分层、`scripts/dev-env.sh` | 工程骨架已可 build/test | 暂不需要 | 保留。 |
| `Riftbound.Contracts` 协议 DTO | 可表达基础 message、command、event、snapshot，已区分 `READY`、`PLAY_CARD`、`PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN`，并有 `ErrorDto` / `PlayerSessionDto`；client/server envelope 已带默认 `protocolVersion = 1`、`schemaVersion = 1`；P2 规则拒绝已新增 `PHASE_NOT_ALLOWED`、`INSUFFICIENT_COST`、`INVALID_TARGET`、`CARD_NOT_IN_HAND`、`UNSUPPORTED_CARD_BEHAVIOR` | 需要后续扩展 | P1 后续补 TypeScript DTO 生成、客户端兼容策略、SignalR 方法版本、事件 upcaster，并扩大稳定错误码覆盖面。 |
| `GameHub` | 支持 Join、Reconnect、RequestSnapshot、Ready、Pass、EndTurn、SubmitIntent、snapshot/prompt/events 推送；加入/重连/Ready/快照/提交错误有最小 SignalR 级测试，空 `clientIntentId` 会稳定返回 `CLIENT_INTENT_ID_REQUIRED`；Join/Reconnect 走异步 token hash 持久化路径；API DI 已改用 `CoreRuleEngine` | 需要后续扩展 | 保留；后续补 P2 规则错误码。 |
| `MatchSession` / `InMemoryMatchSessionRegistry` | 支持串行、幂等、journal、占位状态、P1/P2 座位分配、snapshot seat/ready、最小房间生命周期、reconnect token hash 持久化、重连 token 轮换；提交命令要求玩家已 JoinRoom、携带非空 `clientIntentId` 且房间已开始；registry 可从 recovery frame 恢复 P1 底座状态和已见过 intent，并优先使用权威 `MatchState`；`MatchState` 已有 P2 基础权威字段 `turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`、`cardObjects`、`priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`、`winnerPlayerId`；恢复后已有座位必须用 Reconnect 验证旧 token | 需要规则审查 | 串行、幂等、座位、Ready/lifecycle、恢复入口、持久化重连可保留；规则状态和 prompt 只是占位，后续扩展 MatchState 时必须同步扩展权威状态快照。 |
| `PlaceholderRuleEngine` / `CoreRuleEngine` | `PlaceholderRuleEngine` 仍对齐旧 Java 的 `PASS`、`END_TURN`、重复 `PASS` 事件形状；`CoreRuleEngine` 已实现 P2 普通回合开始、短符文牌堆、1v1 P2 首回合额外符文、燃尽/连续燃尽立即胜利、`END_TURN -> 下一回合开始` 最小闭环、回合结束特殊清理中的伤害移除/本回合内效果失效，`PASS_PRIORITY` / FEPR 最小让过与结算，`PASS_FOCUS` / 法术对决焦点让过和关闭窗口，以及官方法术《惩戒》《渊海狩咒》《焚烧》《海克斯射线》《彗星坠击》《终极闪光》的最小 `PLAY_CARD -> 入栈 -> 结算伤害 -> END_TURN 清理伤害` 通道；《惩戒》《符文禁锢》已覆盖 `ANY_UNIT` 目标范围中的基地单位，《惩戒》已覆盖伤害达到战力后的致命摧毁，《碎裂之火》已覆盖目标被此法术摧毁后的抽牌，《星落》《艾卡西亚暴雨》已覆盖多次单位伤害选择和重复选择同一目标，《走开》已覆盖从手牌打出时眩晕后抽牌，《处置命令》已覆盖抽牌模式，《海克斯射线》已覆盖 `BATTLEFIELD_UNIT` 不能指定基地单位；《星芒凝汇》已覆盖 `1-2` 目标范围和多目标逐个伤害；《火箭轰击》已覆盖 `PLAY_CARD.mode` 模式选择和 `BASE_UNIT` 目标范围；《台前作秀》已覆盖不支付回响的 0 目标抽牌基础路径和支付回响后的重复抽牌路径；《先知之兆》《进化日》已覆盖 0 目标多张抽牌路径；《复仇》已覆盖主动摧毁单位并移入拥有者废牌堆；《虚空索敌》已覆盖结算伤害后抽 1 张牌，以及结算抽牌触发燃尽/回收/抽牌分支；《符文禁锢》已覆盖 `PLAY_CARD -> 入栈 -> 施加 STUNNED -> END_TURN 清理失效` 通道；《渊海狩咒》已覆盖正面朝下卡牌条件伤害；十九张卡参数已进入最小 card behavior registry；并将 placeholder 作为 legacy fallback | 需要继续规则审查 | P2 新规则继续进入 `CoreRuleEngine`；legacy placeholder 只保留为旧 fixture 对照和未迁移命令兜底。 |
| `PostgresMatchJournal`、`PostgresMatchRecoveryStore`、`PostgresMatchPlayerStore` 和 P1 SQL | 能记录命令、lifecycle/game events、权威 state snapshot、玩家 snapshot、prompt、match_players token hash，并写入 ruleset/FAQ/audit 元数据、event sequence 边界和客户端原始 command payload；`matches.status` 使用权威 `MatchState.Status` 更新；已能读取恢复帧并校验 sequence 连续性和权威状态一致性 | 需要后续扩展 | 保留；后续随核心规则状态扩展 `state_snapshots.payload`。 |
| `Riftbound.CardCatalog` | 能加载 1009 官方条目并生成 811 功能单元 | 需要 FAQ 标注 | 保留；后续增加 FAQ 涉及卡牌/关键词标记。 |
| `ConformanceFixture` | 能回放 command log 并比较 event/prompt；已能读取 P2 schema v2 的 `initialState` 和 richer `expected`，并能把 `initialState` 应用为权威初始状态；`CompareExpected` 已开始通用比较 final tick、event kinds、event tick/sequence/payload 局部字段、prompt actions、最终 timing、符文池、分数、玩家区域、对象状态和结算链 | 已补规则审查字段 | 新增 `rulesEvidence`、`faqVersion`、`auditStatus` 读取能力；旧 fixture 默认需要重审；后续继续扩展 snapshots canonical diff。 |
| 3 条 Java fixture | `PASS`、`END_TURN`、重复 `PASS` 已与 C# 占位规则对齐，evidence 已细化 | 必须重审 | 保留为 legacy oracle；`PASS -> TURN_ENDED` 标记为 legacy mismatch candidate。 |
| P2 前置规则审查 | 已建立 `docs/p2-rules-preflight.md`，覆盖符文池、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS`、清理/特殊清理、最小状态模型、事件词表和首批 P2 fixture；fixture schema、`MatchState` 基础权威字段、runner 初始状态应用、普通回合开始、短符文牌堆、1v1 首回合额外符文、燃尽/连续燃尽胜利、`END_TURN` 自动推进、特殊清理伤害/效果、清理重复、`PASS_PRIORITY` 误提交拒绝、FEPR 最小让过/结算、剩余结算链优先权、法术对决焦点让过和《惩戒》《渊海狩咒》《焚烧》《海克斯射线》《彗星坠击》《终极闪光》《台前作秀》《先知之兆》《进化日》《复仇》《星芒凝汇》《火箭轰击》《虚空索敌》《符文禁锢》《碎裂之火》《星落》《艾卡西亚暴雨》《走开》《处置命令》打出/结算 fixture 已落地；《惩戒》《符文禁锢》已覆盖基地单位目标；《惩戒》已覆盖伤害达到战力后的致命摧毁；《碎裂之火》已覆盖条件抽牌；《星落》《艾卡西亚暴雨》已覆盖多次单位伤害选择和重复选择同一目标；《走开》已覆盖眩晕后抽牌；《处置命令》已覆盖抽牌模式；《星芒凝汇》已覆盖多目标伤害；《火箭轰击》已覆盖模式选择；《台前作秀》已覆盖不支付回响的 0 目标抽牌基础路径和支付回响路径；《先知之兆》《进化日》已覆盖 0 目标多张抽牌；《复仇》已覆盖主动摧毁单位；《虚空索敌》已覆盖结算抽牌触发燃尽/回收/抽牌；《符文禁锢》已覆盖本回合内眩晕随回合结束清理失效；《渊海狩咒》已覆盖正面朝下卡牌条件伤害 | 继续执行 | 下一步迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。 |
| 前端 UI | 尚未开始 | 暂不开始最终 UI | P2/P2.5 后做开发期测试 UI。 |

## 3. 计划需要调整的点

必须调整：

1. P0 不只是“规则 PDF 索引”，而是“五份 PDF/FAQ 索引 + FAQ 问题索引 + 规则冲突裁决表”。
2. P1 不应继续优先扩展 10 条玩法 fixture；应先补现有 3 条 fixture 的 `rulesEvidence`。
3. 完成状态必须新增 `NEEDS_RULE_AUDIT`，防止旧 Java conformance 被误当成规则完成。
4. 数据库和 fixture schema 已开始记录 `rulesetVersion` / `faqVersion` / audit 状态；后续事件回放必须持续带上这些字段。
5. Java exporter 的输出应逐步从 `oracle` 改成 `legacyOracle`；最终 `expected` 由 PDF/FAQ 裁决产生。

暂不需要调整：

- .NET 10 + ASP.NET Core + SignalR + PostgreSQL + Redis 的技术路线。
- “服务端权威、玩家只提交意图、事件日志、断线重连、长期生产化”的目标。
- UI 分两阶段：先开发期测试 UI，后产品级 UI。
- 卡牌批次按风险分层，不固定每批最多 6 个。

## 4. 下一步顺序

下一步不要直接继续扩展玩法实现。推荐顺序：

1. 继续 P2 preflight：迁移更多低复杂度官方卡牌，优先选择费用、单目标、伤害/本回合效果这类可复用路径。
2. 随卡牌迁移继续抽象对象控制者/所属者、隐藏信息和结算链事件 payload。
3. 继续把已有 P2 fixture 从手写断言迁移到 `CompareExpected`。
4. P1 提交路径已覆盖未知玩家、空 intent、未开局、unsupported command、重复 intent 冲突；P2 需要加入费用不足、目标非法、阶段不允许等规则错误码。
5. 新增 fixture 使用 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`，裸 `PASS` 仅保留在 legacy oracle。
6. 后续扩展核心规则时，同步扩展 `MatchState` 和 `state_snapshots.payload`，玩家视角 snapshot 只作为重连视图和一致性校验依据。

## 5. 重审验收

一个已开发能力从 `NEEDS_RULE_AUDIT` 恢复为可继续开发状态，至少需要：

- 覆盖五份 PDF/FAQ 中相关章节或 FAQ 问题。
- `rulesEvidence` 记录到 fixture 或状态矩阵。
- 如果 Java legacy oracle 与 PDF/FAQ 冲突，记录差异并以 PDF/FAQ 更新 `expected`。
- `dotnet test Riftbound.slnx --no-build` 通过。
- 文档中的当前状态同步更新。
