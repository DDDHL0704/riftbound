# 符文战场服务端核心规则自查报告

自查日期：2026-05-06  
审计基准提交：`509af74`  
自查依据：`docs/符文战场_服务端核心规则自查文档.md`、仓库内五个官方规则 PDF 对应的核心规则/FAQ/勘误要求，以及当前 `src/Riftbound.Engine`、`src/Riftbound.Api`、`tests/Riftbound.ConformanceTests` 实现。

## 总结论

结论：**NOT READY**

当前服务端已经具备产品原型可用的联机房间、服务端权威提交、按玩家视角发送 snapshot/prompt、动作幂等、开发场景、相当数量的代表性卡牌效果和 conformance fixture 覆盖。但如果按自查文档的最终门槛判断为“完整符合官方核心规则、所有官方卡牌均可页面操作且不误导为 CONFORMANCE_PASS”，目前仍存在 P0 级缺口。

最关键的结论是：当前实现更接近“代表性规则引擎 + 大量 fixture 与产品 UI smoke”，还不是完整官方规则状态机。尤其是官方开局、战场精确位置/控制权/待命区、通用清理任务队列、法术对决/战斗完整流程、彩色符能支付、连续效果层与全卡牌文字映射仍需要补齐。

## 2026-05-06 开发进度更新

- P0-001 第一批已落地：新增 `SUBMIT_DECK`、`MULLIGAN` 协议命令，新增官方卡组校验器，新增正式 deck submit 入口，双方提交合法 deck 并 ready 后会进入正式 1v1 开局、随机回合顺序、双方传奇/英雄区域、每人 3 选 1 战场、主牌堆/符文牌堆洗牌、起手 4 张、按回合顺序调度，并在双方调度后进入第一个回合。
- P0-002 第一批已落地：新增 `ObjectLocationState` 权威位置索引，snapshot 对公开对象输出 `location`，正式开局/调度/召符文/打出到结算链/结算后入场或入废牌堆/移动都会同步对象位置；精确战场游走会校验来源位置是否匹配服务端权威位置，并把目的战场写回状态。
- P0-003 第一批已落地：`MOVE_UNIT` 和精确游走完成后会执行一次致命伤害清理，并将清理后的区域重新同步回 `ObjectLocations`，避免移动后的已摧毁单位继续留在战场位置索引中。
- P0-004 第一批已落地：`StackItemState` 记录入栈时机上下文；迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，会回到 `SPELL_DUEL_OPEN` 并把焦点交给回合顺序下一名玩家，而不是错误关闭到普通开环；法术对决 prompt 也会在有可用来源时暴露 `PLAY_CARD`。
- P0-004 第二批已落地：`MatchState` 归一化/恢复栈项目时保留 `TimingContext`，反应/反制牌入栈会继承现有栈顶的法术对决上下文；最后一个法术对决栈项目被反制后，结算仍会回到 `SPELL_DUEL_OPEN` 并把焦点交还给下一名玩家，避免由状态恢复或反应链造成的错误窗口关闭。
- P1-004 第一批已落地：普通玩家 `SnapshotDto.Timing` 不再暴露 `seed` 和 `rngCursor`；服务端权威 `MatchState` 仍保留随机状态用于内部结算、恢复和日志，避免客户端通过 snapshot 推断牌库/随机顺序。
- 已补测试：`OfficialOpeningTests` 覆盖协议解析、卡组构筑拒绝条件、正式开局、起手调度、精确战场位置写回/来源不匹配拒绝、移动后致命伤害清理与位置同步。
- 已补测试：`P7SpellDuelReactionInheritsStackTimingContextWhenItCountersLastSpell` 覆盖法术对决反应/反制链继承 timing context；`SnapshotsDoNotExposeRandomSeedOrCursor` 覆盖普通玩家 snapshot 隐藏随机种子和游标；当前回归记录为 `ConformanceFixtureRunnerTests 2654/2654`、`GameHubJoinTests 84/84`、`CardCatalogBaselineTests 38/38`。
- 兼容性边界：为避免打碎既有开发 seed 和旧测试，当前无 decklist 的普通 `READY` 仍保留 legacy 入口；产品 UI 和后续正式规则路径必须强制先走 `SUBMIT_DECK`。因此 P0-001 从“缺失”降为“正式路径已存在，仍需收紧 legacy 入口/前端入口和更多负例”。

## 已确认做得比较扎实的部分

- 服务端权威与串行化：`src/Riftbound.Engine/MatchSession.cs:2375` 通过 `serialGate` 串行处理命令；`src/Riftbound.Engine/MatchSession.cs:2421` 只在 `result.Accepted` 时更新权威状态；`src/Riftbound.Api/Hubs/GameHub.cs:216` 只接收命令并广播服务端结果。
- 房间/重连/按玩家发送视图：`src/Riftbound.Api/Hubs/GameHub.cs:24` 支持加入房间，`src/Riftbound.Api/Hubs/GameHub.cs:53` 支持重连，`src/Riftbound.Api/Hubs/GameHub.cs:270` 按玩家组发送 snapshot/prompt。
- 基础隐藏信息：`src/Riftbound.Engine/MatchSession.cs:743` 对非己方手牌只给数量，`src/Riftbound.Engine/MatchSession.cs:811` 对非己方面朝下对象做字段裁剪；相关测试覆盖见 `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs:459`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs:29922`。
- 开发场景安全边界：`src/Riftbound.Api/Hubs/GameHub.cs:154` 的 `SeedScenario` 被限制在 Development 环境。
- 行为目录和 fixture 体系已经很大：`tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:68`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 说明当前项目已有系统性测试基础。

## P0 问题

### P0-001 官方构筑、开局、调度流程缺失

当前状态：**PARTIALLY RESOLVED / 仍需收紧正式入口**

规则依据：自查文档 3.1/3.2；核心规则关于 1v1 构筑、开局准备、随机战场、起手、调度、P2 额外符文的要求。

代码位置：
- `src/Riftbound.Contracts/Protocol.cs` 新增 `SubmitDeckCommand` 和 `MulliganCommand`。
- `src/Riftbound.Engine/OfficialDeckRules.cs` 新增官方 decklist 模型与校验器，覆盖主牌 40+、符文 12、战场 3、传奇/英雄匹配、同名 3、唯我 1、专属卡限制、颜色/符文特性约束。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `SubmitDeckAsync`、正式开局构建、按玩家 snapshot 的 `deckSubmitted`/`mulliganCompleted` 标记。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `MULLIGAN` 解析和调度结算，并修正后手额外符文从固定 seat P2 扩展为 `OpeningSecondActionPlayerId`。

现象：正式 deck path 已可测，但为了兼容既有开发 seed 和旧测试，无 decklist 的普通 `READY` 仍走 legacy 入口；产品 UI/正式房间还需要改成强制提交 deck 后才允许 ready。

最小复现场景：创建房间，P1/P2 先提交合法 `SUBMIT_DECK`，再双方 `READY`，服务端进入 `MULLIGAN`，双方按顺序 `MULLIGAN` 后进入首回合 `MAIN`。如果不提交 deck 而直接 `READY`，当前仍为 legacy 兼容路径。

建议修复：
- 将产品 UI 的 ready 按钮改为依赖 `deckSubmitted`，正式对战入口强制 `SUBMIT_DECK -> READY -> MULLIGAN`。
- 增加配置开关，使非 Development/非测试房间禁止 legacy no-deck ready。
- 继续补更多负例：专属卡超过 3、颜色多特性缺失、唯我同名、战场同名、英雄不在主牌等都已经有校验器能力，仍需补更细测试矩阵。

建议测试：
- 已新增：`tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`。
- 待补：GameHub 级 `SUBMIT_DECK` smoke、前端正式入口 smoke、更多非法 deck fixtures、调度抽牌不足边界。

### P0-002 战场、待命区、控制权和单位位置模型不足

当前状态：**PARTIALLY RESOLVED / 对象位置索引已落地，战场控制与待命区仍待建模**

规则依据：自查文档 4、10；核心规则关于基地、战场、待命区、战场控制权、占领/争夺、单位移动与区域归属的要求。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 新增 `ObjectLocationState` 与 `MatchState.ObjectLocations`，snapshot 会输出对象 `location`。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的打出、结算、移动、调度、召符文路径开始同步 `ObjectLocations`。
- 仍缺：每个战场的 controller、contested/held/conquered 状态、occupants、standby、pending duel/battle 统一模型。

现象：系统现在可以在权威状态中表达对象所在粗粒度区域和精确战场 object id，并拒绝来源位置与权威状态不一致的精确游走。但战场本身仍没有完整控制权/争夺/待命子区域状态机，因此 P0-002 只能降级为部分解决。

最小复现场景：在两个友方战场之间提交精确 `MOVE_UNIT` 游走。当前结果会写回 `ObjectLocations[source].BattlefieldObjectId`；如果客户端提交的 origin 与权威位置不一致，服务端会拒绝。

建议修复：
- 新增 `BattlefieldState`/`BoardLocation` 模型，至少包含 battlefield object、controller、contested/held/conquered 状态、occupants、standby、pending duel/battle。
- 将 `PlayerZones.Battlefields` 从“对象列表”升级为“玩家战场槽位 + 位置引用”，并让 `CardObjectState` 或 location index 记录对象所在具体位置。

建议测试：
- 单位从基地到战场、战场到基地、战场间游走。
- 移入空战场、敌方控制战场、双方争夺战场后的控制权、占领、战斗/法术对决 pending 状态。
- 待命区容量、面朝下信息、revealed 后转移。

### P0-003 通用清理检查与任务队列缺失

当前状态：**PARTIALLY RESOLVED / 移动后致命伤害清理已接入，统一任务队列仍缺失**

规则依据：自查文档 5；核心规则关于“任意状态变化后进行清理检查、重复直到稳定、触发待处理任务、清理期间不能响应”的要求。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:18896` 有局部 `ApplyLethalDamageCleanup`。
- `src/Riftbound.Engine/CoreRuleEngine.cs:19362` 有回合结束清理。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10641` 的 `ResolveEndTurn` 只在回合结束路径调用 `ApplyTurnEndCleanup`，然后直接 `ResolveTurnStart`。

现象：当前清理仍是分散在战斗、伤害、回合结束、移动等局部路径里的 helper，不是官方意义上的“所有状态变化后统一检查并重复”的任务队列。移动后至少会跑一次致命伤害清理并同步位置，但由战场控制权变化、连续效果变化、替代效果等触发的 pending duel/battle/控制权变化仍无法通过一个中央状态机保证。

最小复现场景：移动一个已经带致命伤害的单位，当前会移动后清理到废牌堆；但如果移动导致战场控制权/争夺状态变化，仍没有统一 cleanup loop 能保证后续待处理任务被稳定排入并按官方顺序解决。

建议修复：
- 引入 `PendingTaskQueue` 与 `RunCleanupLoop`，统一处理致命伤害、0 战力、离场、战场控制变化、法术对决/战斗启动、胜负检查。
- 所有命令、栈结算、触发结算、移动、进场/离场之后必须进入同一 cleanup loop。

建议测试：
- 伤害、战力变化、替代效果、移动、进出战场都触发同一 cleanup loop。
- cleanup loop 重复直到稳定。
- cleanup 期间拒绝响应/行动窗口。

### P0-004 法术对决与战斗不是完整官方状态机

当前状态：**PARTIALLY RESOLVED / 迅捷入栈后的法术对决焦点恢复已修复，BattleState 仍缺失**

规则依据：自查文档 11、12；核心规则关于 FEPR、法术对决焦点、初始栈、双方行动、战斗 pending、攻击/防守单位声明、战斗清理、无战斗结果的要求。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:232` 的命令分发直接按 `PlayCard`、`MoveUnit`、`DeclareBattle` 等命令进入各自 resolver。
- `src/Riftbound.Engine/MatchSession.cs` 的 `StackItemState.TimingContext` 现在记录入栈前的 timing window。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolvePassPriority` 现在能在法术对决栈清空时恢复 `SPELL_DUEL_OPEN` 并转移焦点。
- `src/Riftbound.Engine/CoreRuleEngine.cs:4174` 的 `ResolveDeclareBattle` 直接执行战斗。
- `src/Riftbound.Engine/CoreRuleEngine.cs:5185` 的 `TryBuildMinimalDeclareBattle` 只支持 1 个攻击者、1 到 2 个防守者，且条件被命名为 minimal。
- `src/Riftbound.Engine/CoreRuleEngine.cs:4275` 到 `src/Riftbound.Engine/CoreRuleEngine.cs:4382` 直接计算并应用伤害。

现象：当前战斗仍是显式 `DECLARE_BATTLE` 命令驱动的“立即结算战斗片段”，不是由清理任务在争夺战场时启动的完整 battle task。法术对决已修复两个关键窗口问题：迅捷牌结算后不会提前关闭法术对决；反应/反制链也会继承并保留法术对决 timing context。但仍缺少围绕某个 battle/trigger/card 的完整 pending/focus/initial-stack 生命周期。

最小复现场景：迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，当前会回到 `SPELL_DUEL_OPEN` 且焦点移交下一名玩家。单位移动到敌方控制战场时，按官方规则应进入争夺并触发法术对决/战斗流程；这一部分仍没有完整 battle task。

建议修复：
- 建立 `SpellDuelState` 和 `BattleState`，由 cleanup/task queue 创建、推进和关闭。
- 声明攻击/防守、初始栈、双方 focus/pass、swift/reaction 许可、战斗伤害、战斗结果和清理全部挂在同一状态机。

建议测试：
- 由移动/占领触发的法术对决与战斗。
- focus 轮转、pass、swift/reaction、初始栈顺序。
- 多攻击者/多防守者、伤害分配顺序、战斗没有结果时的状态。

### P0-005 彩色符能、普通费用、符能费用与资源技能模型不足

规则依据：自查文档 8、15；核心规则关于 `A/C`、阵营符能、费用支付、符文技能、可选费用、Spellshield/Encourage/Echo/Haste 等费用分支。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs:41` 的 `RunePool` 只有 `Mana` 和泛化 `Power` 两个整数。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10141` 的出牌计划从 `CardBehaviorRegistry` 获取行为并做局部费用计算。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10403` 到 `src/Riftbound.Engine/CoreRuleEngine.cs:10418` 只计算总 mana 和总 power。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10419` 到 `src/Riftbound.Engine/CoreRuleEngine.cs:10434` 只比较泛化资源是否足够。

现象：服务端无法表达官方彩色符能支付、任意符能、同阵营符能、多符能费用组合，以及支付窗口中由 rune/legend/battlefield/skill 产生的复杂支付来源选择。

最小复现场景：需要 `[A]`、`[C]`、同阵营 Haste 支付、Spellshield 多目标加税或 Echo 复杂费用的牌，当前费用系统只能看到 `Power` 总量，无法判断具体颜色合法性。

建议修复：
- 将 `RunePool` 升级为 typed pool，例如 `Mana` + `PowerByTrait` + `AnyPower`，并建立支付计划/支付来源记录。
- 所有支付都通过统一 `PaymentEngine` 校验，支持普通费用、符能费用、额外/可选费用、替代费用、减费/加费、符文技能。

建议测试：
- `[A]`、`[C]`、单阵营/多阵营费用、Haste 彩色支付、Spellshield 多目标加税、Echo 费用、支付失败不改变状态。

## P1 问题

### P1-001 连续效果、层、时间戳和依赖模型不足

规则依据：自查文档 14；核心规则关于连续效果、层、时间戳、依赖和装备/静态效果的重算。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:17140` 的 `ApplyPowerModifier` 直接修改 `CardObjectState.Power`，并用 `UntilEndOfTurnPowerModifier` 聚合记录。
- `src/Riftbound.Engine/CoreRuleEngine.cs:19395` 在回合结束时从 `Power` 中扣回聚合值。

现象：临时战力修正通过修改对象当前数值实现，缺少“基础值 + 连续效果层 + 时间戳 + 来源 + 依赖”的重算模型。多个装备、静态光环、控制权变化、失去/获得关键词时容易与官方层规则不一致。

建议修复：引入 `ContinuousEffect`、`LayerEngine`、`Timestamp`、`SourceObjectId`，计算视图时重算派生属性，避免永久修改基础属性。

建议测试：多个装备/光环叠加、控制权变化、失去来源、同层时间戳、回合结束、最小战力限制。

### P1-002 关键词覆盖仍存在“识别但 deferred”的内部事实

规则依据：自查文档 15；关键词必须不仅被识别，还要能按官方时点、费用、目标、区域和替代/触发规则执行。

代码位置：
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:63` 到 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:73` 明确装备关键词是 `RecognizedDeferred`。
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:89` 到 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:93` 说明只有代表性 Take Up，assemble/agile/tempered/static modifier 等仍 deferred。
- `src/Riftbound.Engine/CardInteractionKeywordRules.cs:72` 到 `src/Riftbound.Engine/CardInteractionKeywordRules.cs:75` 说明 Standby/Ambush/Echo 仍有宽泛 deferred 分支。
- `src/Riftbound.Engine/CardCombatKeywordRules.cs:55` 到 `src/Riftbound.Engine/CardCombatKeywordRules.cs:60` 说明 combat damage、assignment order、roam movement execution remain deferred。
- `src/Riftbound.Engine/CardResourceKeywordRules.cs:76` 到 `src/Riftbound.Engine/CardResourceKeywordRules.cs:80` 说明 broader experience/level/encourage/spellshield branches remain deferred。

现象：这些 profile 与“所有卡牌功能完整实现”存在冲突。即便某些代表性路径已有实现，关键词族整体不能判定完整。

建议修复：把关键词 profile 状态与真实规则执行路径重新对齐；没有完整执行的卡牌/功能单元不能暴露为完全 `CONFORMANCE_PASS`。

建议测试：按关键词族建立完整矩阵测试，每个关键词覆盖时点、费用、目标、隐藏信息、替代/触发、失败回滚。

### P1-003 BehaviorSpec/CONFORMANCE_PASS 口径疑似过度乐观

规则依据：自查文档 16、19、21；BehaviorSpec 需要真实映射到可执行规则，不能用域级占位掩盖未实现细节。

代码位置：
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs:202` 到 `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs:250` 通过 rune/token/legend/battlefield domain 将非 PLAY_CARD 域合并为 implemented behavior。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:76` 到 `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:83` 断言 `1009/1009` 全为 implemented。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:116` 到 `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:144` 断言 `1009/1009 CONFORMANCE_PASS`、`811/811` functional units、0 manual/blocked。
- 但 `docs/CURRENT_P6_STATUS.md:894` 到 `docs/CURRENT_P6_STATUS.md:901` 仍记录 P6 final 为 `713/811` implemented、`98/811` manual deferred；`src/Riftbound.Engine/P6LegendAbilityCatalog.cs:13`、`src/Riftbound.Engine/P6BattlefieldEffectCatalog.cs:20` 也保留 deferred surfaces。

现象：P7.9 文档与测试把目录推进到全 implemented，但底层规则模型仍存在 P0/P1 缺口。若 UI 以此作为“所有卡牌均完整可玩”的唯一依据，会误导用户。

建议修复：
- 将 `CONFORMANCE_PASS` 拆成至少三层：`FixturePass`、`RepresentativeRulePass`、`FullOfficialRulePass`。
- 非 PLAY_CARD 域不要仅凭 domain 合并判定完整；需要逐条卡牌/功能单元映射到真实命令、状态机和测试。

建议测试：
- 每个 BehaviorSpec 的 `ImplementedEffectKind` 必须能追溯到具体 resolver、prompt candidate、fixture 和官方文本锚点。
- 对 legend/battlefield/token/rune 域建立负例，防止占位 domain 把未实现能力标为 pass。

### P1-004 隐藏信息与 replay 边界仍需加固

当前状态：**PARTIALLY RESOLVED / 普通 snapshot 随机状态泄漏已修，严格 action-log replay 仍待补**

规则依据：自查文档 2、18；客户端不得得到能预测未来随机信息的私密状态；replay/观战要区分公开信息与玩家私有视角。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs:640` 到 `src/Riftbound.Engine/MatchSession.cs:657` 的 snapshot timing 中对所有玩家暴露 `seed` 和 `rngCursor`。
- `src/Riftbound.Engine/MatchSession.cs:2438` 到 `src/Riftbound.Engine/MatchSession.cs:2466` 的 `RestoreState` 优先恢复 authoritative state；没有 action-log replay 到相同最终状态的独立校验路径。

现象：目前 opponent hand/face-down redaction 做得不错，普通玩家 snapshot 也已不再包含 `seed`/`rngCursor`；剩余风险是 replay 更像恢复快照/权威状态，不是严格的命令日志重放和公开/私有视角重放模型。

建议修复：
- 已完成：从普通玩家 snapshot 中移除 seed/rngCursor；如后续需要调试随机状态，应单独走 Development/debug stream，不能复用普通玩家 snapshot。
- 建立 action log replay：从初始公开/私有边界 + 命令日志重放到最终 authoritative state，并提供 spectator redaction。

建议测试：
- 已新增：玩家 snapshot 不含 seed/rngCursor。
- action log replay final state hash 等于实时 state hash。
- spectator replay 不泄露手牌、牌库顺序、面朝下内容和未来随机。

## P2 问题

### P2-001 测试通过不等于官方规则完整

当前 fixture 和 baseline 测试数量很大，但自查文档最终门槛要求的是官方规则闭环。现有测试更多证明“当前代码声明支持的路径能稳定运行”，不足以证明“官方规则所有路径都已覆盖”。

建议补充：
- 官方开局/构筑专项测试。
- cleanup/task queue 集成测试。
- FEPR/法术对决/战斗状态机集成测试。
- 关键字矩阵测试。
- FAQ/勘误回归测试。
- 随机/隐藏信息/replay property tests。

### P2-002 文档状态需要重新分层

`docs/CURRENT_P7_9_STATUS.md:76` 到 `docs/CURRENT_P7_9_STATUS.md:78` 记录 legend/battlefield 已 0 remaining，`docs/CURRENT_P7_9_STATUS.md:153` 到 `docs/CURRENT_P7_9_STATUS.md:158` 记录 P7.9 全完成；但本次核心规则自查显示这些结论只适合描述当前产品/fixture 口径，不能直接等同“官方完整核心规则 READY”。

建议补充一份状态口径说明：
- `Product UI playable smoke`: 当前 P7 页面可用性。
- `Conformance fixture pass`: 当前 fixture 全绿。
- `Official full rules ready`: 本报告结论为 NOT READY。

## 模块矩阵

| 模块 | 结论 | 说明 |
| --- | --- | --- |
| 服务端权威/幂等/非法命令不落状态 | PASS | `MatchSession.SubmitAsync` 串行且仅 accepted 更新状态。 |
| 双人房间/重连/snapshot/prompt | PASS | `GameHub` 具备 join/reconnect/request snapshot/submit flow。 |
| 隐藏手牌/面朝下对象 | PASS | 手牌、face-down 与随机 seed/rngCursor 均已从普通玩家视角裁剪。 |
| 官方 deck/opening/mulligan | FAIL | 无正式 deck validator 与官方开局状态机。 |
| 区域/对象/控制权/战场位置 | FAIL | 缺少精确 battlefield/standby/control/location 模型。 |
| FEPR/优先权/焦点 | RISKY | 有 timingState/focus/prompt，但缺少完整 pending task/state machine。 |
| 栈/出牌/费用/目标 | RISKY | 大量代表路径实现；彩色符能和通用支付/目标语法不足。 |
| 通用清理检查 | FAIL | 只有局部 cleanup helper，无全局 cleanup loop。 |
| 移动/战场控制 | FAIL | 事件可描述移动，但状态无法表达具体战场位置/控制权。 |
| 法术对决 | FAIL | 缺少完整 spell duel lifecycle。 |
| 战斗 | FAIL | 当前是 direct/minimal declare battle，不是官方 battle task。 |
| 计分/胜负 | RISKY | 有部分得分/胜负实现；依赖战场控制与 cleanup 的完整性不足。 |
| 连续效果层 | FAIL | 直接修改对象数值，缺少 layer/timestamp/dependency。 |
| 关键词 | RISKY/FAIL | 多个关键词 profile 仍标识 recognized/deferred。 |
| 全卡牌效果 | RISKY | BehaviorSpec 全 pass 口径与内部 deferred/规则模型缺口冲突。 |
| 日志/replay/观战 | RISKY | 有 journal/recovery，普通 snapshot 不再泄漏随机状态；仍缺少严格 action-log replay 和 spectator redaction。 |

## 建议下一步开发顺序

1. 先冻结并重命名当前 `CONFORMANCE_PASS` 口径，避免把 fixture/domain pass 当成 full official rules pass。
2. 实现官方 deck/opening/mulligan，这是后续所有本地测试和 UI 对局的基础。
3. 重构 board model：battlefield state、unit location、standby、control/contest/conquer/hold。
4. 建立 cleanup task queue，把移动、进场、离场、伤害、战力变化、战场控制变化统一纳入。
5. 在 task queue 上重做 spell duel 和 battle state machine。
6. 重做 typed payment engine，支持彩色符能、任意符能、普通费用、额外/可选费用与 rune/legend/battlefield 支付来源。
7. 引入 continuous effect layer engine。
8. 逐关键词、逐卡牌把 `Representative/FixturePass` 提升到 `FullOfficialRulePass`。

## 最终验收口径

在完成上述 P0/P1 之前，不建议把服务端声明为“完整符合五个官方规则文件”。推荐当前对外口径为：

> 当前服务端可支撑本地双人对战原型、服务端权威结算、代表性规则与大量 conformance fixture；但完整官方核心规则仍处于 NOT READY，自查发现官方开局、精确战场模型、通用清理任务队列、法术对决/战斗状态机、彩色符能和连续效果层需要继续开发。
