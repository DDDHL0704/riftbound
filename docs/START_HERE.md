# 新窗口接手指南

更新时间：2026-04-30

这份文档用于在新 Codex 窗口中快速恢复上下文，确保后续开发不偏离《符文战场》的最终目标。

## 1. 一句话目标

构建一款精美、稳定、可双人联机对战、服务端权威结算、可断线重连、可回放、可长期生产化维护的 Web 卡牌游戏。

关键原则：

- 玩家只提交行为意图。
- 服务端根据五份官方 PDF、官网卡面和当前状态自动裁决并结算。
- 前端只渲染服务端下发的 `Prompt`、`Events`、`PlayerSnapshot` 和错误信息。
- 每个规则能力都必须能通过 command log 回放，并以 PDF/FAQ 规则依据作为最终裁决；Java 输出只作为旧实现对照。

## 2. 当前项目边界

主要工作区：

- 新项目：`/Users/dinghaolin/MyProjects/riftbound-dotnet`
- 旧 Java 参考项目：`/Users/dinghaolin/MyProjects/riftbound-server`

最高优先级资料：

1. `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》核心规则_260330.pdf`
2. `/Users/dinghaolin/MyProjects/riftbound-dotnet/裁判FAQ_251023.pdf`
3. `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_裁判FAQ.pdf`
4. `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_官方FAQ_260114.pdf`
5. `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》破限系列_裁判FAQ_260416.pdf`
6. `/Users/dinghaolin/MyProjects/riftbound-dotnet/data/official/card-catalog.zh-CN.json`
7. 旧 Java 项目的实现、测试、生成矩阵和迭代日志

当前固定 Java 参考点：

- `75bf7cf feat: expand equipment and control card behaviors`

注意：

- 规则 PDF/FAQ 是未跟踪文件，不要提交。
- 不回退现有改动。
- 旧 Java 代码是历史行为参考，不是新项目架构模板；如果它与 FAQ 或官网卡面冲突，以 PDF/FAQ/官网卡面为准。

## 3. 新窗口先读顺序

打开新窗口后，按这个顺序读即可进入状态：

1. `README.md`
2. `docs/START_HERE.md`
3. `docs/master-development-plan.md`
4. `docs/phase-1.md`
5. `docs/rules-authority-and-audit.md`
6. `docs/development-audit-status.md`
7. `docs/rules-evidence-index.md`
8. `docs/protocol-semantics.md`
9. `docs/rules-card-baseline.md`
10. `docs/p2-rules-preflight.md`
11. `docs/conformance-fixture-format.md`
12. 如需迁移背景，再读旧项目的 `docs/dotnet-migration-plan.md`

如果上下文不够，再回到旧 Java 项目阅读：

- `README.md`
- `docs/current-development-status.md`
- `docs/iteration-log-260330.md` 最新章节
- `docs/multiplayer-auto-resolution-protocol.md`
- `docs/card-behavior-regression-plan.md`

## 4. 当前状态

新项目已建立第一阶段骨架：

- `Riftbound.Contracts`：协议 DTO 草案。
- `Riftbound.Engine`：`MatchSession`、串行处理、幂等占位。
- `Riftbound.Api`：ASP.NET Core + SignalR `GameHub` 草案。
- `Riftbound.Persistence`：PostgreSQL 事件日志 schema 与 `IMatchJournal` 实现。
- `Riftbound.CardCatalog`：官网卡牌快照加载与功能逻辑单元分组。
- `Riftbound.ConformanceTests`：conformance 测试骨架。

已完成的本机环境：

- .NET SDK：10.0.203，安装在 `~/.dotnet`。
- PostgreSQL：16.13，Homebrew service 已启动。
- Redis：6.2.4，Homebrew service 已启动。
- Node：24.15.0，安装在 `/opt/homebrew/opt/node@24/bin`。
- 本地数据库：`riftbound_dev`。

本机当前限制：

- .NET 10 SDK 已安装到 `~/.dotnet`。
- PostgreSQL 16、Redis、Node 24 已可用于本地开发。
- 新终端先执行 `source scripts/dev-env.sh`，确保当前 shell 使用正确版本。

最近一次验证：

- `source scripts/dev-env.sh` 通过。
- `dotnet build Riftbound.slnx --no-restore` 通过。
- `dotnet test Riftbound.slnx --no-restore` 通过，当前 298 个测试。
- Java oracle exporter 已导出 `java-oracle-p1-pass.fixture.json`、`java-oracle-p1-end-turn.fixture.json`、`java-oracle-p1-duplicate-pass.fixture.json`。
- C# 测试已能读取 Java fixture 元数据，并对齐 `PASS`、`END_TURN`、重复 `PASS` 的首批事件日志 fixture；这些 fixture 现在默认 `RequiresRuleAudit`。
- `ConformanceFixture` 已能读取可选的 `rulesEvidence`、`faqVersion`、`auditStatus`、`seed` 字段。
- `docs/rules-evidence-index.md` 已建立五份 PDF/FAQ 的规则域索引和当前 fixture 审查映射。
- Java exporter 已输出 `legacyOracle`，并暂时保留旧 `oracle` 兼容字段。
- P1 PostgreSQL schema 和 `PostgresMatchJournal` 已补 `ruleset_version`、`faq_version`、`rules_audit_status`、`rules_evidence`；schema initializer 会按文件名顺序执行 `Sql/*.sql`。
- 当前 3 条 Java legacy fixture 的 evidence 已细化到具体页码、规则号/FAQ 问题号，并记录 `PASS -> TURN_ENDED` 是 legacy mismatch candidate。
- `docs/p2-rules-preflight.md` 已建立 P2 前置审查：符文池、`END_TURN`、`PASS_PRIORITY`、`PASS_FOCUS`、清理/特殊清理的裁决、最小状态模型、事件词表和首批 P2 fixture。
- `WsClientMessage` / `WsServerMessage` 已接入默认 `protocolVersion = 1`、`schemaVersion = 1`；canonical JSON 测试已固定 camelCase envelope 字段。
- `docs/protocol-semantics.md` 已明确 `PASS_PRIORITY`、`PASS_FOCUS`、`END_TURN` 的协议边界；`PASS` 仅保留为 legacy 兼容。
- 手写 C# placeholder fixture 已迁移为 `p1-placeholder-pass-priority.fixture.json`，使用显式 `PASS_PRIORITY`；裸 `PASS` 只保留在 Java legacy fixture 和兼容测试中。
- `MatchSession` 已能为前两名加入者分配稳定 `P1` / `P2` 座位，并在 snapshot 的 `players` 视图中暴露 seat；第三名玩家会被拒绝。
- `GameHub.JoinRoom` 已有最小 SignalR 级测试，覆盖双人加入、room/player group、snapshot/prompt 推送和第三人满员错误。
- `GameHub.Reconnect`、`GameHub.RequestSnapshot`、`GameHub.Ready`、稳定 `ErrorDto` 错误码和内存重连 token 已有最小 Hub 级测试。
- P1 SQL 和 `PostgresMatchJournal` 已补 `event_sequence`、command 起止 event sequence、snapshot/prompt `last_event_sequence`，并通过新库/旧库迁移 smoke。
- `MatchJournalEntry` 和 `PostgresMatchJournal` 已保留客户端原始命令 JSON；`SubmitIntent` 的原文会进入 `command_log.payload.rawCommand`，便于后续回放、审计和 canonical diff。
- `IMatchRecoveryStore`、`PostgresMatchRecoveryStore` 和 `MatchRecoveryValidator` 已建立最小恢复读取/校验路径：可读取 match、command、events、最新 snapshot/prompt，并校验 event sequence 连续性、command 边界和 player view sequence；本机 PostgreSQL smoke 通过。
- `001_p1_event_store.sql` 已移除依赖 003 新列的 sequence 索引创建，旧库可按 `001 -> 002 -> 003` 顺序升级。
- `MatchSession` 已接入 `EMPTY -> SEATING -> IN_PROGRESS` 的最小房间生命周期；`Ready` 会记录 `PLAYER_READY` / `MATCH_STARTED` lifecycle events，snapshot/prompt 暴露 ready 状态，未 Ready 开局前提交命令会被 `MATCH_NOT_STARTED` 拒绝。
- `MatchSession.SubmitAsync` 已要求玩家先 JoinRoom、携带非空 `clientIntentId` 且房间已开始；Hub 级测试覆盖 `PLAYER_NOT_IN_ROOM`、`CLIENT_INTENT_ID_REQUIRED`、`MATCH_NOT_STARTED`、`UNSUPPORTED_COMMAND`、`CLIENT_INTENT_CONFLICT` 五条命令提交错误路径。
- `InMemoryMatchSessionRegistry` 已接入 `IMatchRecoveryStore` 异步恢复入口；恢复时只恢复 P1 底座所需的 tick、turn、active player、P1/P2 seat、lastEventSequence 和已见过的 intent，用于防止重启后重复提交污染事件流，不把玩家视角 snapshot 当完整权威规则状态；跨 room recovery frame 会被 `RECOVERY_INCONSISTENT` 拒绝。
- `state_snapshots` 权威状态快照表已加入 P1 schema 和 `004_p1_state_snapshots.sql` 迁移；`PostgresMatchJournal` 会写入服务端 `MatchState`，`PostgresMatchRecoveryStore` 会优先读取与当前 `last_event_sequence` 对齐的权威状态，并校验玩家视角 snapshot 与权威状态一致；本机 PostgreSQL state snapshot smoke 通过。
- `IMatchPlayerStore`、`PostgresMatchPlayerStore` 和 `ReconnectTokenHasher` 已接入；Join/Reconnect 会把 reconnect token 以 `sha256:` hash 写入 `match_players.reconnect_token_hash`，恢复后的 session 可用旧 token hash 重连，并在重连成功后轮换新 token/hash；恢复后已有座位但没有 live token 的玩家必须走 Reconnect，不能用 Join 直接领取新 token；本机 PostgreSQL token smoke 通过，库中不保存 token 明文。
- P2 preflight 已开始落代码：`ConformanceFixture` 已能读取 schema v2 的 `initialState`、P2 `expected.finalState/events/prompts`；新增 `p2-preflight-turn-start.fixture.json` 作为规则审查后的回合开始样例。
- conformance runner 已能把 P2 `initialState` 应用为真实权威初始局面，不再走 Join/Ready 覆盖 `TURN_START`；P2 expected 仍未升级为完整规则断言。
- `MatchState` 已增加 P2 权威字段：`turnPlayerId`、`phase`、`timingState`、`runePools`、`playerZones`、`playerScores`、`cardObjects`、`priorityPlayerId`、`passedPriorityPlayerIds`、`stackItems`、`focusPlayerId`、`passedFocusPlayerIds`、`winnerPlayerId`、`destroyedUnitOwnerIdsThisTurn`、`seed`、`rngCursor`；snapshot 已投影 timing、公开结算链、焦点、赢家字段、最小本回合摧毁记忆和可回放随机游标，玩家 `handSize` 和 `score` 来自权威状态，后续扩展对象状态、战场控制和卡牌效果时必须继续维护 `state_snapshots.payload`。
- `CoreRuleEngine` 已接入 API DI，并保留 `PlaceholderRuleEngine` 作为 legacy fallback；当前已能通过 P2 fixture 验证普通回合开始、短符文牌堆、1v1 第二个行动玩家首个召出阶段额外符文、抽牌燃尽并回收废牌堆、连续燃尽导致对手立即获胜、`END_TURN` 回合推进和特殊/重复清理、`PASS_PRIORITY` / FEPR 让过与结算、`PASS_FOCUS` / 法术对决焦点让过与关闭窗口，以及官方法术 `UNL-007/219 惩戒`、`UNL-014/219 渊海狩咒`、`UNL-020/219 曼舞手雷`、`OGS·003/024 焚烧`、`OGN·009/298 海克斯射线`、`SFD·017/221 雷霆突降`、`SFD·023/221 透体圣光`、`OGN·014/298 霹天雳地`、`OGN·085/298 彗星坠击`、`OGS·022/024 终极闪光`、`OGN·252/298 超究极死神飞弹！` 的最小 `PLAY_CARD -> 入栈 -> 双方让过 -> 结算伤害 -> END_TURN 清理伤害` 通道；`OGN·050/298 符文禁锢` 已覆盖“对一名单位”可指定基地单位；`UNL-007/219 惩戒` 和 `OGN·009/298 海克斯射线` 已覆盖“战场上的一名单位”不能指定基地单位；`OGN·014/298 霹天雳地` 已覆盖按控制单位最高战力降低法力费用和基础 5 点伤害路径；`OGN·105/298 星芒凝汇` 已覆盖 `1-2` 个目标分别造成伤害；`OGN·133/298 剑刃飓风` 已覆盖 0 目标全战场单位各 1 点伤害路径和致命伤害摧毁路径；`OGN·127/298 加农炮幕` 已覆盖只对战斗中的敌方单位造成 2 点伤害路径；`SFD·077/221 火箭轰击` 已覆盖 `PLAY_CARD.mode` 选择“基地单位伤害 4”模式，且缺失模式会拒绝；`UNL-182/219 完美谢幕` 已覆盖未支付有色/多次回响时的四个模式分支；`SFD·076/221 产量激增` 已覆盖全额费用路径和控制“机械”属性单位时减费 2 路径，并会打出 3 战力“机器人”到基地后抽 1 张牌；`OGS·015/024 共同献身` 已覆盖 `PLAY_CARD.mode = "BASE"` 选择基地目的地后打出四名 1 战力“随从”到基地路径；`UNL-044/219 羽毛旋风` 已覆盖 `PLAY_CARD.mode = "CREATE_WARHAWKS"` 战鹰模式，打出四名 1 战力带 `法盾` 标签“战鹰”到基地；`SFD·182/221 危险温度` 已覆盖未支付回响时只让己方“机械”属性单位本回合内战力 +1；`OGN·266/298 虹吸能量` 已覆盖当前单战场区域友方战场单位 +1 且敌方战场单位 -1 不低于 1；`UNL-198/219 月之降临` 已覆盖当前单战场区域敌方战场单位 -2（可选移动暂缓）；`SFD·031/221 点沙成兵` 已覆盖支付回响后重复打出两名 2 战力“黄沙士兵”到基地；`SFD·154/221 护驾！` 已覆盖不支付黄色可选费用时打出一名 2 战力“黄沙士兵”到基地；`UNL-061/219 台前作秀` 已覆盖不支付回响的 0 目标 `结算 -> 抽 1 张` 基础路径，以及 `optionalCosts = ["ECHO"]` 支付额外 2 点并重复抽牌一次；`UNL-091/219 聚心凝神`、`SFD·087/221 先知之兆`、`OGN·083/298 借鉴历史`、`OGN·114/298 进化日` 和 `OGN·144/298 以战养战` 已覆盖 0 目标多张抽牌路径，且《聚心凝神》的等级减费路径暂缓，`SFD·106/221 实力至上` 已覆盖按强力单位数量动态抽牌，`OGN·134/298 动员` 已覆盖召出休眠符文和失败抽牌，`OGN·138/298 万世催化石` 已覆盖召出两枚休眠符文和不足两枚时抽牌，`OGN·047/298 御衡守念` 已覆盖对手距胜利不超过 3 分时减费 2，并按顺序先抽 1 张再召出休眠符文，《以战养战》已覆盖本回合内敌方单位被《复仇》摧毁后的 2 点减费；`OGN·024/298 虚空索敌` 已覆盖 `结算伤害 -> 抽 1 张` 以及结算抽牌触发燃尽/回收/抽牌通道；`OGN·050/298 符文禁锢` 已覆盖 `PLAY_CARD -> 入栈 -> 双方让过 -> 施加 STUNNED -> END_TURN 清理失效` 通道；`OGN·229/298 复仇`、`UNL-186/219 涌泉之恨`、`OGS·012/024 爆能术`、`UNL-159/219 狩魂`、`OGN·213/298 暗刃`、`SFD·164/221 流沙陷坑` 和 `UNL-180/219 破败之咒` 已覆盖主动摧毁单位、移动到拥有者废牌堆并移除场上对象状态，其中《狩魂》已覆盖“不高于 3 战力”的目标限制和 4 战力目标拒绝，《暗刃》已覆盖目标控制者抽 2 张，《清理门户》已覆盖双方各自选择并摧毁己方单位，《国王诏令》已覆盖当前 2P 下其他玩家选择非控制者单位并摧毁，《飓风席卷》已覆盖当前 2P 下每名玩家可选单位返回所属者手牌，《破败之咒》已覆盖摧毁所有场上单位；《坠渊之流》已覆盖所有当前场上单位返回所属者手牌；`OGN·172/298 责退` 和 `OGN·169/298 罡风` 已覆盖战场单位返回所属者手牌，其中《罡风》已覆盖不高于 3 战力目标限制；`OGN·104/298 择日再战` 已覆盖友方单位回手后，其拥有者召出休眠符文；`OGN·168/298 战或逃` 已覆盖战场单位移动到所属者基地并保留对象状态；`OGN·043/298 魅惑妖术` 已覆盖敌方战场单位移动到所属者基地代表路径；`UNL-038/219 升龙踢` 已覆盖未启用等级 6 时敌方战场单位移动到所属者基地代表路径；`OGS·011/024 闪现` 已覆盖最多两名友方战场单位移动到基地，并拒绝敌方单位和友方基地单位目标；`SFD·043/221 禁军之墙` 已覆盖按当前己方战场单位数动态选择任意数量友方战场单位移动到基地，并拒绝敌方单位、友方基地单位和重复目标；`OGN·173/298 驭风而行` 已覆盖友方战场单位移动到基地并变为活跃状态代表路径；`UNL-009/219 大幕渐起` 已覆盖支付回响后重复变为活跃状态；`OGN·146/298 痛殴` 已覆盖基础变为活跃状态；`SFD·204/221 狩猎` 已覆盖所有友方单位变为活跃状态；`OGN·123/298 过载能量` 已覆盖友方单位休眠后全战场 12 点伤害路径；`UNL-031/219 实战经验` 已覆盖未启用等级 6 时本回合内战力 +1；`OGN·046/298 决斗架势` 已覆盖友方单位本回合内战力 +1 基础路径，`UNL-046/219 动物之友` 已覆盖按己方单位中动物标签种类动态计算战力修正；`OGN·058/298 训练有素` 已覆盖本回合内战力 +2 后抽 1 张，并在 `END_TURN` 特殊清理中移除战力修正；`SFD·034/221 蛮荒之力` 已覆盖支付回响 2 后重复本回合内战力 +2 修正；`SFD·066/221 封冻` 已覆盖支付回响 2 后重复本回合内战力 -2 修正；`SFD·196/221 距破之舞` 已覆盖两名不同单位分别 +2/-2 的本回合内战力修正；`OGN·004/298 顺劈` 已覆盖授予本回合 `OVERWHELM_3`，且目标为进攻方时本回合内战力 +3；`SFD·003/221 血性冲刺` 已覆盖支付回响后重复授予本回合 `OVERWHELM_2` 与进攻方 +2 战力；`UNL-017/219 怒吼清算` 已覆盖弃置一张手牌作为回响额外费用后重复授予本回合 `OVERWHELM_4` 与进攻方 +4 战力；`UNL-010/219 强能冲拳` 已覆盖授予本回合 `OVERWHELM_2` 与 `ROAM`，且目标为进攻方时本回合内战力 +2；`OGN·057/298 格挡` 已覆盖授予本回合 `STEADFAST_3` 与 `BARRIER`，且目标为防守方时本回合内战力 +3；`SFD·097/221 先打再问` 已覆盖本回合内战力 +5；`OGN·154/298 洪荒巨力` 已覆盖本回合内战力 +7；`UNL-063/219 月蚀` 已覆盖本回合内战力 -4 基础路径和洞察顶部牌回收路径；`UNL-066/219 月光之殇` 已覆盖本回合内战力 -10；`OGN·207/298 荣耀召唤` 已覆盖未支付消耗增益额外费用时本回合内战力 +3；`OGS·024/024 致命打击` 已覆盖所有友方单位本回合内战力 +2；`OGN·233/298 宏伟战略` 已覆盖所有友方单位本回合内战力 +5；`OGN·206/298 背靠背` 已覆盖两名友方单位本回合内战力 +2；`OGN·093/298 烟幕弹` 已覆盖本回合内战力 -4 且不得低于 1，并在 `END_TURN` 特殊清理中按实际应用修正恢复原战力；`OGN·095/298 “敲”诈` 已覆盖本回合内战力 -1 不低于 1 后继续抽 1 张牌；`UNL-007/219 惩戒` 已覆盖伤害达到单位战力时由本回合替代效果改为放逐；`OGS·020/024 高原血统` 已覆盖本回合内摧毁替代为休眠召回拥有者基地；`UNL-175/219 战术撤退` 已覆盖本回合内下次摧毁替代为移除伤害、休眠召回拥有者基地；`UNL-095/219 视死如归` 已覆盖友方单位本回合内战力 +3 基础路径；`OGN·005/298 碎裂之火` 已覆盖伤害摧毁目标后抽 1 张牌和未摧毁不抽牌；`OGN·029/298 星落` 已覆盖两次单位伤害选择、同一结算摧毁多个单位，以及同一单位可被两次选择并累积伤害；`OGN·128/298 决斗` 已覆盖友方和敌方单位按自身战力互相造成伤害；`UNL-110/219 巨人之战` 已覆盖任意两名单位按自身战力互相造成伤害；`OGN·248/298 艾卡西亚暴雨` 已覆盖六次单位伤害选择和重复命中同一目标；`UNL-042/219 走开` 已覆盖从手牌打出时眩晕目标并抽 1 张牌；`SFD·040/221 扑咚！` 已覆盖进攻方单位目标限制、基础眩晕路径和支付回响后重复眩晕路径；`OGN·262/298 天顶之刃` 已覆盖不选择可选移动时眩晕敌方战场单位路径；`SFD·023/221 透体圣光` 已覆盖不支付有色回响的 1-2 个不同战场单位各 2 点伤害路径；`UNL-103/219 处置命令` 已覆盖 `DRAW_1` 抽牌模式和对手废牌堆最多三张回收模式；`SFD·122/221 预判攻势` 已覆盖主牌堆顶部二选一抽取并回收另一张路径；`OGN·183/298 卡牌骗术` 已覆盖主牌堆顶部三选一抽取并回收其余两张路径；`OGN·048/298 冥想` 已覆盖基础抽牌路径和休眠友方单位额外费用后的额外抽牌路径；`OGN·224/298 废物利用` 已覆盖不选择装备目标时跳过可选摧毁并抽 1 张牌；`UNL-015/219 占山为王` 已覆盖没有已控制战场时只抽基础 1 张牌；`UNL-125/219 月神恩赐` 已覆盖弃置另一张友方手牌后抽 2 张牌；`OGN·170/298 亡者复生` 已覆盖己方废牌堆目标返回手牌，`OGN·264/298 游击战` 已覆盖最多两张己方废牌堆待命牌返回手牌，`UNL-165/219 暗影的召唤` 已覆盖友方单位获得 `瞬息` 标签后抽 2 张牌；`UNL-173/219 牺牲` 已覆盖摧毁友方强力单位作为强制额外费用后先抽 2 张牌再召休眠符文；`SFD·163/221 断魂一扼` 已覆盖摧毁一名友方单位后按其当前战力增益另一名友方单位并抽 1 张牌；`OGN·083/298 借鉴历史` 的待命路径暂缓；`OGN·252/298 超究极死神飞弹！` 的征服后废牌堆返回手牌触发能力暂缓；`SFD·164/221 流沙陷坑` 的非手牌位置打出减费路径暂缓。
- 本轮已补齐 `UNL-128/219 造化弄人`（友方单位再敌方单位按序回手）和 `UNL-134/219 存在焦虑`（回响重复先眩晕进攻敌方单位、再因已眩晕改为回手）；本批补齐 `OGN·048/298 冥想` 休眠友方单位作为额外费用后的额外抽牌路径，以及 `UNL-125/219 月神恩赐` 弃置另一张友方手牌后抽 2 张牌路径，并补齐 `OGN·170/298 亡者复生` 从己方废牌堆返回手牌路径，并补齐《决斗》友方和敌方单位按自身战力互相造成伤害路径，并补齐《巨人之战》任意两名单位按自身战力互相造成伤害路径，并补齐 `OGN·201/298 反转时间线` 每名玩家弃置手牌后各抽 4 张牌路径，并补齐 `OGS·008/024 绅士决斗` 先友方战力 +3 再互伤路径，并补齐 `SFD·114/221 行军号令` 支付回响后重复互伤路径，并补齐 `UNL-173/219 牺牲` 摧毁友方强力单位作为强制额外费用后先抽 2 张牌再召休眠符文路径，并补齐 `SFD·163/221 断魂一扼` 摧毁友方单位后按其当前战力增益另一名友方单位并抽 1 张牌路径，并补齐 `OGN·004/298 顺劈` 授予本回合 `OVERWHELM_3` 且进攻方目标 +3 战力路径，并补齐 `SFD·003/221 血性冲刺` 支付回响后重复授予 `OVERWHELM_2` 与进攻方目标 +2 战力路径，并补齐 `UNL-017/219 怒吼清算` 弃手牌回响重复授予 `OVERWHELM_4` 与进攻方目标 +4 战力路径，并补齐 `UNL-010/219 强能冲拳` 授予 `OVERWHELM_2` 与 `ROAM` 且进攻方目标 +2 战力路径，并补齐 `OGN·057/298 格挡` 授予 `STEADFAST_3` 与 `BARRIER` 且防守方目标 +3 战力路径，并补齐 `OGN·127/298 加农炮幕` 只伤害战斗中敌方单位路径，并补齐 `SFD·076/221 产量激增` 全额费用与控制“机械”属性单位减费后打出 3 战力“机器人”到基地并抽牌路径，并补齐 `OGS·015/024 共同献身` 选择基地目的地后打出四名 1 战力“随从”到基地路径，并补齐 `UNL-044/219 羽毛旋风` 战鹰模式打出四名 1 战力带 `法盾` 标签“战鹰”到基地路径，并补齐 `SFD·182/221 危险温度` 未支付回响时只让己方“机械”属性单位本回合内战力 +1 路径，并补齐 `SFD·031/221 点沙成兵` 支付回响后重复打出两名 2 战力“黄沙士兵”到基地路径，并补齐 `SFD·154/221 护驾！` 不支付黄色可选费用时打出一名 2 战力“黄沙士兵”到基地路径，并补齐 `OGN·224/298 废物利用` 不选择装备目标时跳过可选摧毁并抽 1 张牌路径，并补齐 `UNL-015/219 占山为王` 没有已控制战场时只抽基础 1 张牌路径，并补齐 `UNL-182/219 完美谢幕` 未支付有色/多次回响时的抽牌、战场单位伤害、基地单位伤害和战场单位 -4 战力四个模式路径，并补齐 `OGS·020/024 高原血统` 本回合内摧毁替代为休眠召回基地路径，并补齐 `UNL-175/219 战术撤退` 本回合内下次摧毁替代为移除伤害、休眠召回基地路径，并补齐 `UNL-095/219 视死如归` 友方单位本回合内战力 +3 基础路径，并补齐 `OGN·046/298 决斗架势` 友方单位本回合内战力 +1 基础路径，并补齐 `UNL-046/219 动物之友` 按己方单位中动物标签种类动态计算战力修正路径，并补齐 `SFD·001/221 矢志不退` 按敌方战场单位数量动态计算友方战场单位战力修正路径，并补齐 `UNL-063/219 月蚀` 本回合内战力 -4 基础路径和洞察顶部牌回收路径，并补齐 `UNL-020/219 曼舞手雷` 不进入再次打出分支时对基地单位造成 2 点伤害路径，并补齐 `OGN·262/298 天顶之刃` 不选择可选移动时眩晕敌方战场单位路径，并补齐 `OGN·033/298 巧取豪夺` 目标控制者不选择让你抽两张牌时的 6 点伤害分支及目标控制者选择让来源控制者抽两张牌以避免伤害分支，并补齐 `OGN·260/298 狂风绝息斩` 先让友方单位变为活跃状态再按其当前战力伤害敌方战场单位路径，并补齐 `OGN·108/298 聚合变异` 按另一友方单位当前战力补足第一目标战力路径，并补齐 `OGN·209/298 清理门户` 双方各自选择并摧毁己方单位路径，并补齐 `SFD·043/221 禁军之墙` 按当前己方战场单位数动态选择任意数量友方战场单位移动到基地路径，并补齐 `OGN·173/298 驭风而行` 当前目的地受限模型下友方战场单位移动到所属基地并变为活跃状态代表路径，并补齐 `OGN·043/298 魅惑妖术` 当前目的地受限模型下敌方战场单位移动到所属基地代表路径，并补齐 `UNL-038/219 升龙踢` 未启用等级 6 时敌方战场单位移动到所属基地代表路径，并补齐 `UNL-124/219 隔绝` 敌方战场单位移动到基地且无落单敌方单位时不抽牌基础路径，并补齐 `UNL-054/219 顽皮触手` 总战力不高于 8 的敌方战场单位移动到基地代表路径，并补齐 `SFD·129/221 诱饵` 不支付回响时移动敌方单位到另一敌方单位所在位置路径，并补齐 `SFD·080/221 风箱炎息` 不支付有色回响时 1-3 名单位各 1 点伤害路径，并补齐 `OGS·002/024 烈火风暴` 敌方战场单位范围 3 点伤害路径，并补齐 `UNL-072/219 新月打击` 敌方战场主目标 4 点伤害加其他敌方战场单位各 1 点伤害路径，并补齐 `SFD·145/221 换换乐` 两名战场单位本回合内战力互换路径，并补齐 `OGN·237/298 国王诏令` 当前 2P 下其他玩家选择非控制者单位并摧毁路径，并补齐 `OGN·187/298 飓风席卷` 当前 2P 下每名玩家可选单位返回所属者手牌路径，并补齐 `UNL-204/219 持卫的裁决` 敌方战场单位放到拥有者主牌堆顶/底路径，并补齐 `SFD·122/221 预判攻势` 主牌堆顶部二选一抽取并回收另一张路径，并补齐 `OGN·183/298 卡牌骗术` 主牌堆顶部三选一抽取并回收其余两张路径，并补齐 `UNL-032/219 龙虎双雄` 不支付回响时主牌堆顶部三选一单位牌抽取并回收其余两张路径及不选择单位牌回收全部已查看牌路径，并补齐 `OGN·156/298 暗中破坏` 对手手牌非单位牌回收路径，并补齐 `UNL-013/219 莲花陷阱` 本回合内受到后续伤害翻倍路径，并补齐 `SFD·194/221 反击风暴` 本回合内下一次伤害抵挡并抽 1 张牌路径，并补齐 `OGN·254/298 诺克萨斯断头台` 本回合内下次受到伤害触发摧毁路径，并补齐 `UNL-198/219 月之降临` 当前单战场区域敌方战场单位 -2 路径；最小 card behavior registry 当前为 141 条卡牌/模式参数。
- API 在 `http://127.0.0.1:5088` 启动成功。
- 本轮追加 `OGN·221/298 帝国谕令` 当前场上单位受伤即摧毁代表路径；后续新进场单位暂缓到全局持续效果模型。
- 本轮追加 `OGN·094/298 精灵召唤` 当前目的地受限代表路径，打出一名 3 战力“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。
- 本轮追加 `UNL-069/219 精灵迸发` 当前目的地受限代表路径，打出两名 3 战力“精灵”到基地；完整目的地选择和瞬息到期摧毁暂缓。
- `/health` 返回 ok。
- `/catalog/summary` 返回 1009 官方条目、811 功能逻辑单元。
- PostgreSQL 已创建 P1 表：`matches`、`command_log`、`game_events`、`state_snapshots`、`snapshots`、`action_prompts`、`official_cards`、`functional_units`、`oracle_fixtures`。

## 5. 立即开发顺序

当前阶段是从 P1 联机底座过渡到 P2 核心规则 preflight。可以继续做 fixture schema、权威 `MatchState`、符文池和回合开始/结束流程；仍不要进入大规模全卡牌迁移、最终 UI 或复杂 AI。

第一批任务：

1. 继续按 `docs/p2-rules-preflight.md` 扩展 P2 preflight：`PLAY_CARD` 已有最小 card behavior registry，并覆盖二十四张官方伤害法术和《完美谢幕》两条模式化伤害路径、七张眩晕法术、一条回响重复眩晕路径、十三条 0 目标抽牌/手牌重置路径、单次回响、主动摧毁单位、双方各自选择并摧毁己方单位、目标控制者抽牌、伤害达到战力后的致命摧毁、《惩戒》放逐替代和《高原血统》/《战术撤退》休眠召回基地替代、结算后抽牌/条件抽牌/抽牌燃尽路径、四条费用修正路径（本回合敌方单位被摧毁、控制单位最高战力、对手距胜利不超过 3 分、控制指定属性单位），以及先抽牌再召出休眠符文、强制摧毁友方强力单位作为额外费用后先抽牌再召出休眠符文、正向/负向本回合内战力修正、六条单纯正战力修正路径、两条所有友方单位本回合内正战力修正路径、一条两名友方单位本回合内正战力修正路径、一条多目标回响重复正战力修正路径、一条负战力修正后抽牌路径、一条回响重复战力修正路径、两条单纯负战力修正路径、一条回响重复负战力修正路径、一条分裂正负战力修正路径、一条两名战场单位本回合内战力互换路径、一条友方战场单位本回合内 +1 后眩晕敌方战场单位路径、五条授予关键词并按战斗状态条件加战力路径（其中两条支付回响重复，一条以弃手牌作为回响费用，两条同时授予两个关键词，一条按防守状态加战力）、一条友方单位回手后召出休眠符文路径、一条全场单位返回所属者手牌路径、一条每名玩家可选单位返回所属者手牌路径、两条敌方战场单位放到拥有者主牌堆顶/底路径、六条战场单位移动到所属基地路径（含《驭风而行》友方战场单位移动到基地并变为活跃状态、《魅惑妖术》与《升龙踢》敌方战场单位移动到基地、《隔绝》敌方战场单位不抽牌基础路径、《顽皮触手》总战力不高于 8 的敌方战场单位移动到基地路径）、一条最多两名友方战场单位移动到基地路径、一条按当前己方战场单位数动态选择任意数量友方战场单位移动到基地路径、一条总战力不高于 8 的敌方战场单位移动到基地路径、三条变为活跃状态路径、一条友方单位休眠后全战场单位伤害路径、一条敌方战斗中单位范围伤害路径、一条敌方战场单位范围伤害路径、一条敌方战场主目标加其他敌方战场单位溅射伤害路径、一条单位指示物打出到基地后抽牌路径、一条多枚单位指示物打出到基地路径、一条回响重复单位指示物打出到基地路径、一条单枚 2 战力黄沙士兵单位指示物打出到基地路径、一条单枚 3 战力精灵单位指示物打出到基地路径、一条双 3 战力精灵单位指示物打出到基地路径、一条可选装备摧毁被跳过后的抽牌路径、一条无已控制战场时的基础抽牌路径、一条按对象标签筛选的所有友方单位正战力修正路径、一条当前单战场区域友方战场单位正战力修正和敌方战场单位负战力修正路径、一条当前单战场区域敌方战场单位负战力修正路径、一条按己方单位属性标签种类动态计算的正战力修正路径、一条按敌方战场单位数量动态计算的友方战场单位正战力修正路径、一条按另一友方单位当前战力补足目标战力的本回合内动态战力修正路径、一条进攻方目标条件伤害路径、一条对手废牌堆回收路径、一条对手手牌非单位牌回收路径、三条主牌堆顶部选择抽取并回收未选牌路径（其中一条要求 `CARD_TYPE:UNIT` 单位牌标签）、一条主牌堆顶部五张不选择单位牌时回收全部已查看牌路径、一条洞察顶部牌回收路径、一条弃置手牌后抽牌路径、一条所有玩家弃置手牌后各抽四张路径、一条己方废牌堆目标回手路径、一条最多两张己方废牌堆待命牌回手路径、一条友方单位获得 `瞬息` 标签后抽 2 张牌路径、一条友方和敌方单位按自身战力互伤路径、一条任意两名单位按自身战力互伤路径、一条先战力修正再互伤路径、一条先让友方单位变为活跃状态再按其当前战力伤害敌方战场单位路径、一条摧毁友方单位后按其当前战力增益另一名友方单位并抽牌路径；《渊海狩咒》已覆盖基础 2 点伤害和“控制正面朝下卡牌则改为 4 点伤害”的条件路径；《曼舞手雷》已覆盖不进入再次打出分支时对基地单位造成 2 点伤害路径；《天顶之刃》已覆盖不选择可选移动时眩晕敌方战场单位路径；《强手裂颅》已覆盖友方战场单位和敌方战场单位双目标眩晕路径；《英勇冲锋》已覆盖友方战场单位本回合内 +1 后眩晕敌方战场单位路径；《巧取豪夺》已覆盖目标控制者不选择抽牌时对敌方单位造成 6 点伤害分支，以及目标控制者选择让来源控制者抽两张牌以避免伤害分支，《狂风绝息斩》已覆盖先让友方单位变为活跃状态再按其当前战力伤害敌方战场单位路径，《聚合变异》已覆盖按另一友方单位当前战力补足第一目标战力路径，《清理门户》已覆盖双方各自选择并摧毁己方单位，《国王诏令》已覆盖当前 2P 下其他玩家选择非控制者单位并摧毁，《飓风席卷》已覆盖当前 2P 下每名玩家可选单位返回所属者手牌路径，《持卫的裁决》已覆盖敌方战场单位放到拥有者主牌堆顶/底路径，《预判攻势》已覆盖主牌堆顶部二选一抽取并回收另一张路径，《卡牌骗术》已覆盖主牌堆顶部三选一抽取并回收其余两张路径，《龙虎双雄》已覆盖不支付回响时主牌堆顶部三选一单位牌抽取并回收其余两张路径及不选择单位牌回收全部已查看牌路径，《增援》已覆盖不选择单位牌时回收顶部五张已查看牌路径，《禁军之墙》已覆盖按当前己方战场单位数动态选择任意数量友方战场单位移动到基地路径，《驭风而行》已覆盖友方战场单位移动到基地并变为活跃状态代表路径，《隔绝》已覆盖敌方战场单位移动到基地且无落单敌方单位时不抽牌基础路径，《风箱炎息》已覆盖不支付有色回响时 1-3 名单位各 1 点伤害，《烈火风暴》已覆盖敌方战场单位范围 3 点伤害，《新月打击》已覆盖敌方战场主目标 4 点伤害和其他敌方战场单位各 1 点伤害路径，《护驾！》已覆盖不支付黄色可选费用时打出一名 2 战力“黄沙士兵”到基地路径；《矢志不退》已覆盖按敌方战场单位数量动态计算友方战场单位战力修正路径。
2. richer expected diff 已开始接入，当前可比较 final tick、event kinds、event tick/sequence/payload 局部字段、prompt actions、最终 timing、符文池、分数、玩家区域、对象状态和结算链；目标校验已能区分 `ANY_UNIT`、`BATTLEFIELD_UNIT`、`BASE_UNIT`、`FRIENDLY_UNIT`、`FRIENDLY_THEN_ENEMY_UNITS`、`FRIENDLY_THEN_ENEMY_BATTLEFIELD_UNITS`、`FRIENDLY_BATTLEFIELD_THEN_ENEMY_BATTLEFIELD_UNITS`、`FRIENDLY_BATTLEFIELD_UNIT`、`FRIENDLY_HAND_CARD`、`OPPONENT_HAND_CARD`、`FRIENDLY_MAIN_DECK_CARD`、`ANY_UNIT_THEN_FRIENDLY_MAIN_DECK_CARD`、`FRIENDLY_GRAVEYARD_CARD`、`ATTACKING_UNIT`、`ENEMY_ATTACKING_UNIT`、`ENEMY_BATTLEFIELD_UNIT`、`ENEMY_UNIT`、`OPPONENT_GRAVEYARD_CARD`，支持 `1-2` 这种目标数量范围，可按 `PLAY_CARD.mode` 选择模式行为，并可用 `optionalCosts = ["ECHO"]` 支付一次回响额外费用，且 `optionalCosts = ["EXHAUST_FRIENDLY_UNIT:<objectId>"]` 与 `optionalCosts = ["DESTROY_FRIENDLY_POWERFUL_UNIT:<objectId>"]` 已用于额外费用；下一步逐批迁移更多低复杂度官方卡牌，并继续扩大通用 diff 覆盖面。
3. P1 协议错误码保持稳定；P2 再加入费用不足、目标非法、阶段不允许等规则错误码。
4. 协议版本治理剩余项：TypeScript DTO 生成、客户端兼容策略、SignalR 方法版本和事件 upcaster；不要在没有前端接入点前过度设计。
5. 后续新增 fixture 必须使用 `PASS_PRIORITY` / `PASS_FOCUS` / `END_TURN`，裸 `PASS` 只保留在 Java legacy oracle 和兼容测试中。
6. 后续扩展 `MatchState` 时，同步扩展 `state_snapshots` 的权威 payload，不把隐藏信息或完整规则状态塞进玩家视角 snapshot。

P1 验收前不要开始：

- 最终产品级 UI。
- 全卡牌迁移。
- 复杂 AI。
- 移动端适配。
- 多实例房间热迁移。

## 6. 阶段验收门禁

任何功能要进入“完成”状态，必须满足：

- PDF/FAQ 规则点已记录。
- 官网卡面文本已记录。
- 行为规格已结构化。
- C# 实现已完成。
- 单元测试通过。
- MatchSession/SignalR 测试通过。
- PDF/FAQ 裁决通过；Java legacy oracle conformance 通过或差异已记录。
- 前端 smoke 或等价 E2E 通过。
- 文档和状态矩阵已更新。

未通过 conformance 的能力不能进入正式可玩卡池。

## 7. UI 和浏览器测试原则

UI 分两段：

- P2.5：先做简单但精美的开发期测试 UI，用于人工测试、fixture 复现和 Browser Use smoke。
- P7：规则和卡池稳定后，再做产品级 Web 对战 UI。

开发期 UI 必须保持服务端权威：

- 所有按钮来自 `ActionPrompt`。
- UI 不自行判断规则合法性。
- UI 展示真实 `Snapshot`、`Events`、`Prompt`。

P2.5 后，每个高风险规则能力都要用 Codex 内置浏览器做真实 P1/P2 smoke，并记录本地 URL、roomId、操作路径、事件和最终 snapshot 摘要。

## 8. 防偏离检查

每次开始新任务前，先确认：

- 这是否服务于“服务端权威双人 Web 卡牌游戏”？
- 是否符合五份 PDF、FAQ 和官网卡牌优先级？
- 是否把 Java 当作旧实现对照，而不是最终规则裁判？
- 是否能产出 command log、events、snapshots？
- 是否有明确 conformance 或 Browser Use smoke 验收？
- 是否避免提前做最终 UI、全卡牌、复杂 AI 或移动端？

答案不清楚时，先补文档或测试，不直接扩张实现。

## 9. 推荐给新窗口的开场指令

可以直接给新窗口这段话：

```text
继续 /Users/dinghaolin/MyProjects/riftbound-dotnet 的《符文战场》新项目。
先读取 README.md、docs/START_HERE.md、docs/master-development-plan.md、docs/phase-1.md、docs/rules-authority-and-audit.md、docs/development-audit-status.md、docs/rules-evidence-index.md、docs/rules-card-baseline.md、docs/p2-rules-preflight.md。
目标不变：.NET 10 + ASP.NET Core + SignalR 服务端权威双人 Web 卡牌游戏。五份官方 PDF、FAQ 与官网卡牌快照是最终规则权威，旧 Java 项目只作为历史行为参考和 fixture 导出工具。
当前阶段从 P1 联机底座过渡到 P2 preflight：按 docs/p2-rules-preflight.md 继续 fixture schema、权威 MatchState、符文池和回合开始/结束流程；不要跳到全卡牌迁移。
不要重做最终 UI，不要全量迁移卡牌，不要提交规则 PDF/FAQ，不要回退现有改动。
```
