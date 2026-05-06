# 符文战场服务端核心规则自查报告

自查日期：2026-05-06
审计基准提交：`45bb446`；本轮复审代码提交至 `0fb469b`
自查依据：`docs/符文战场_服务端核心规则自查文档.md`、仓库内五个官方规则 PDF 对应的核心规则/FAQ/勘误要求，以及当前 `src/Riftbound.Engine`、`src/Riftbound.Api`、`tests/Riftbound.ConformanceTests` 实现。

## 总结论

结论：**NOT READY**

当前服务端已经具备产品原型可用的联机房间、服务端权威提交、按玩家视角发送 snapshot/prompt、动作幂等、开发场景、相当数量的代表性卡牌效果和 conformance fixture 覆盖。但如果按自查文档的最终门槛判断为“完整符合官方核心规则、所有官方卡牌均可页面操作且不误导为 CONFORMANCE_PASS”，目前仍存在 P0 级缺口。

最关键的结论是：当前实现更接近“代表性规则引擎 + 大量 fixture 与产品 UI smoke”，还不是完整官方规则状态机。官方 deck/opening/mulligan 与官方构筑负例矩阵、对象位置、typed 符能、窗口状态、持续效果视图、关键词覆盖报告、spectator replay redaction 和 replay 状态 hash 已有服务端路径；但完整战场控制/待命任务状态机、通用清理任务队列、法术对决/战斗完整生命周期、全路径官方费用模型、连续效果 LayerEngine 与逐关键词/逐卡牌完整执行仍需要补齐。

## 2026-05-06 开发进度更新

- 复审基线 `45bb446`：本轮自查整改后重新按 `docs/符文战场_服务端核心规则自查文档.md` 复核。结论仍为 **NOT READY**，但 P0 风险已进一步收窄：official opening/mulligan 边界已有测试矩阵，battlefield task 已有权威视图，replay frame 已有 authoritative state hash，battlefield trigger 支付已覆盖 typed power。剩余 NOT READY 根因集中在完整 battlefield/standby/control task 状态机、统一 cleanup task queue、由 task queue 驱动的 spell duel/battle lifecycle、全路径官方 PaymentEngine、完整 LayerEngine 与逐关键词/逐卡牌 full-official-rule-pass 证据。
- P0-001 第一批已落地：新增 `SUBMIT_DECK`、`MULLIGAN` 协议命令，新增官方卡组校验器，新增正式 deck submit 入口，双方提交合法 deck 并 ready 后会进入正式 1v1 开局、随机回合顺序、双方传奇/英雄区域、每人 3 选 1 战场、主牌堆/符文牌堆洗牌、起手 4 张、按回合顺序调度，并在双方调度后进入第一个回合。
- P0-002 第一批已落地：新增 `ObjectLocationState` 权威位置索引，snapshot 对公开对象输出 `location`，正式开局/调度/召符文/打出到结算链/结算后入场或入废牌堆/移动都会同步对象位置；精确战场游走会校验来源位置是否匹配服务端权威位置，并把目的战场写回状态。
- P0-003 第一批已落地：`MOVE_UNIT` 和精确游走完成后会执行一次致命伤害清理，并将清理后的区域重新同步回 `ObjectLocations`；若单位在行动前已经处于待清理致命状态，blocking pending task queue 会先拒绝移动，避免已摧毁单位继续行动。
- P0-004 第一批已落地：`StackItemState` 记录入栈时机上下文；迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，会回到 `SPELL_DUEL_OPEN` 并把焦点交给回合顺序下一名玩家，而不是错误关闭到普通开环；法术对决 prompt 也会在有可用来源时暴露 `PLAY_CARD`。
- P0-004 第二批已落地：`MatchState` 归一化/恢复栈项目时保留 `TimingContext`，反应/反制牌入栈会继承现有栈顶的法术对决上下文；最后一个法术对决栈项目被反制后，结算仍会回到 `SPELL_DUEL_OPEN` 并把焦点交还给下一名玩家，避免由状态恢复或反应链造成的错误窗口关闭。
- P1-004 第一批已落地：普通玩家 `SnapshotDto.Timing` 不再暴露 `seed` 和 `rngCursor`；服务端权威 `MatchState` 仍保留随机状态用于内部结算、恢复和日志，避免客户端通过 snapshot 推断牌库/随机顺序。
- P0-001 第二批已落地：新增 `MatchSessionOptions.AllowLegacyReadyWithoutDeck`；API 在非 Development 环境创建房间时关闭 legacy no-deck ready，正式/生产房间必须先 `SUBMIT_DECK` 才能 `READY`。Development 与既有测试默认保留 legacy ready，用于开发 seed 和旧 fixtures。
- P0-002 第二批已落地：`SnapshotDto.Lanes` 新增 `battlefields` 状态视图，从服务端权威 `ObjectLocations` 和 `CardObjects` 推导战场牌、控制者、占据单位、待命占位、面朝下待命数量与争夺状态，给 UI 和后续 cleanup/battle task 提供稳定服务端投影。
- P0-001 第三批已落地：新增 GameHub 级正式开局 smoke，覆盖 `SUBMIT_DECK -> READY -> OFFICIAL_OPENING_STARTED -> MULLIGAN -> MULLIGAN_PHASE_COMPLETED -> RUNES_CALLED -> MAIN`，确保 WebSocket/房间层不会绕过或吞掉服务端官方 deck/opening/mulligan 流程。
- P0-003 第二批已落地：结算链项目结算后新增统一状态性致命清理兜底。即使某个栈项目本身是无行为/未映射或单卡 resolver 漏接局部 cleanup，只要结算后场上存在伤害大于等于战力的单位，服务端会将其清理入对应非场地区域、同步 `ObjectLocations` 并记录 `UNIT_DESTROYED`。
- P0-005 第一批已落地：`RunePool` 新增 `PowerByTrait` typed 符能池和 `TotalPower` 视图；`PLAY_CARD` 支付计划可区分任意符能与指定特性符能，`SPEND_POWER:red:2` / `SPEND_POWER:红色:2` 等 token 会校验并只扣对应特性，旧的泛化 `power` fixture 仍兼容。snapshot 的 `runePool` 继续提供总 `power`，同时新增 `untypedPower` 与 `powerByTrait` 给 UI 展示支付来源。
- P1-003 第一批已落地：`BehaviorSpec` 新增 `ConformanceTier` / `ConformanceReason`，将当前 `implemented` 明确降义为 `representative-rule-pass`，并在 API summary、图鉴详情和基线测试中断言 `full-official-rule-pass = 0`。产品 UI 不再把 `implemented` 文案展示为“官方完整一致性通过”。
- P0-002 第三批已落地：新增 `BattlefieldState` 权威派生状态视图与 `MatchState.BattlefieldStates`，从服务端 `PlayerZones`、`ObjectLocations`、`CardObjects` 统一表达战场牌、控制者、占据单位、占据方控制者、待命对象、面朝下待命数量和争夺状态；snapshot 的 lanes.battlefields 改为复用该服务端状态视图。
- P0-003 第三批已落地：新增 `CleanupTaskState` / `MatchState.PendingCleanupTasks`，能显式列出致命伤害单位清理与战场争夺检查任务；移动和结算链结算后的致命伤害清理由单次 helper 升级为 `RunStateBasedCleanupLoop`，重复执行直到当前状态性致命伤害任务稳定。
- P0-004 第三批已落地：新增 `TurnWindowState`、`SpellDuelState`、`BattleState` 与对应 `MatchState` 派生视图；普通开环/闭环、法术对决开环/闭环现在有统一窗口状态，snapshot timing 会暴露 `turnWindow`、`spellDuel`、`battle`，用于 UI 和后续 task machine，而不是让前端自行推断。
- P0-005 第二批已落地：装备装配、Vi 双倍战力技能、Xerath 伤害技能等非 `PLAY_CARD` 支付路径的资源校验已改为 typed-power aware；泛化符能费用现在既可由普通 `Power` 支付，也可由 `PowerByTrait` 中任意可用特性符能支付并正确扣除，避免只有出牌路径支持彩色符能。
- P1-001 第一批已落地：新增 `ContinuousEffectState` 服务端派生视图，按 `RULE_TEXT` / `POWER_MODIFIER` 层公开全局与对象级直到回合结束效果；snapshot 中每个公开对象新增 `basePower` / `effectivePower`，timing 新增 `continuousEffects`，UI 不再需要从临时战力聚合字段自行反推基础战力。
- P1-002 第一批已落地：新增 `KeywordCoverageReporter`，将权限/战斗/资源/装备/生命周期/交互六类关键词的 implemented、representative、delegated、recognized-deferred 状态汇总为服务端报告；API `/catalog/summary`、`/catalog/p3-status` 和新增 `/catalog/keyword-coverage` 均会暴露 keyword coverage，产品侧可直接禁用或提示 deferred 关键词能力。
- P1-004 第二批已落地：新增 `ResolutionResult.BuildSpectatorSnapshot` 与 `MatchReplayRedactor.BuildSpectatorFrame`，从权威 journal entry 生成观战/回放 frame 时统一使用 spectator redaction；手牌、面朝下对象详情和随机 seed/rngCursor 不会进入观战 replay snapshot。
- P0-003 第四批已落地：争夺战场的 `PendingCleanupTasks` 现在除 `BATTLEFIELD_CONTESTED` 外，还显式列出 `START_SPELL_DUEL` 与 `START_BATTLE` 后续任务；战斗伤害、常规栈项目局部清理、Xerath 技能伤害和回合开始战场群体伤害都改为走 `RunStateBasedCleanupLoop`，并在首轮保留伤害触发摧毁目标集合，后续重复清理直到稳定。
- P0-004 第四批已落地：新增 `BattlefieldTaskState` 与 `MatchState.BattlefieldTasks`，争夺战场会生成带 `PENDING`/`ACTIVE`/`WAITING_FOR_SPELL_DUEL` 状态、参与控制者、参与单位、焦点玩家和 spell-duel stack 关联的权威任务视图；snapshot timing 新增 `battlefieldTasks`，UI 和后续 task queue 可直接消费服务端任务，而不是只从 `pendingTaskKinds` 猜测。
- P1-004 第三批已落地：新增 `MatchStateHasher`，对权威 `MatchState` 生成 canonical SHA-256 hash；`MatchReplayFrame` 现在携带 `AuthoritativeStateHash`，观战/回放帧可在不泄露手牌、面朝下对象和随机状态明文的前提下，与实时权威状态进行最终状态一致性校验。
- P0-005 第三批已落地：战场据守触发 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 现在也走 `CanPayRuneCosts` / `PayRuneCosts`，泛化 4 符能费用可以由 `PowerByTrait` 支付并正确扣除；这把 battlefield trigger payment 纳入 typed-power-aware 路径，不再只支持普通 `Power`。
- P0-003 第五批已落地：状态性 cleanup loop 新增 0 战力现代权威单位清理；`PendingCleanupTasks` 会显式列出 `DESTROY_ZERO_POWER_UNIT`，栈结算后的统一清理会把有 owner/controller 的 0 战力场上单位移入废牌堆并记录 `ZERO_POWER`，同时保留旧 fixture 中无所有权信息的 0 power 占位对象兼容行为。
- P0-003 第六批已落地：新增 `PendingTaskQueueState` 与 `MatchState.PendingTaskQueue`，把 `PendingCleanupTasks` 按官方清理优先级排序并公开 `hasTasks`、`isBlocking`、`phase`、`activeTaskId` 和 task 列表；snapshot timing 新增 `pendingTaskQueue`，后续 UI/状态机不再需要从多个数组自行拼装清理队列。
- P0-003 第七批已落地：`ResolutionResult.BuildPrompts` 与 `CoreRuleEngine.ResolveAsync` 接入 blocking pending task queue；当当前权威状态存在待处理清理/战场任务且没有栈优先权或法术对决焦点窗口时，服务端 prompt 只返回 `WAIT`，并拒绝普通玩家命令，避免前端或客户端在 cleanup/task queue 阻塞期间继续出牌、移动或结束回合。
- P0-005 第四批已落地：`ActionPromptBuilder` 将 `PLAY_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT` 纳入“必须有服务端候选源才可启用”的 prompt 规则；`PLAY_CARD` 来源现在会经过 BehaviorRegistry、时点权限和基础费用的服务端保守过滤，没有足够基础费用的手牌不会作为可执行来源暴露给 UI。
- P0-005 第五批已落地：移动/装配 prompt 继续收紧。`MOVE_UNIT` 只暴露正面、受控、非战斗中的单位；`ASSEMBLE_EQUIPMENT` 只暴露当前代表实现支持的未贴附长剑/武装装备，并要求有可支付符能和合法单位目标，减少 UI 上“按钮可点但必然被服务端拒绝”的误导。
- P0-004 第五批已落地：`CoreRuleEngine` 的拒绝/结算后 core prompt 在法术对决焦点窗口复用 `ActionPromptBuilder.SpellDuelFocusActions`；如果焦点玩家有可支付且时点合法的迅捷牌，服务端 prompt 会同时暴露 `PLAY_CARD` 与 `PASS_FOCUS`，避免被局部 `BuildCorePrompts` 降级成只能让过焦点。
- P0-002/P0-003 第八批已落地：`DECLARE_BATTLE` 战斗结算后会用服务端 `PlayerZones` 重新同步 `ObjectLocations`，避免战斗摧毁、战场触发移动或后续 cleanup 改变区域后，权威对象位置索引仍停留在旧战场并污染 snapshot、战场任务和后续合法性检查。
- 复审结论补充：截至 `0fb469b`，本轮已把 P0-001 官方开局/调度、P0-002 对象位置索引、P0-003 pending task 阻塞、P0-005 prompt 候选与代表性 typed 支付继续收紧；重新对照自查文档后，结论仍为 **NOT READY**，剩余阻断项不再是局部测试缺口，而是需要完整 board task model、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine 和 LayerEngine 的架构级后续批次。
- P0-001 第四批已落地：`OfficialDeckValidatorRejectsOfficialNegativeMatrix` 补齐基础官方构筑负例矩阵，覆盖英雄不在主牌、主牌非法类别、未知卡号、符文牌堆非符文、战场牌堆非战场、主牌/符文越出传奇特性等拒绝路径。
- P0-001 第五批已落地：`OfficialMulliganRejectsInvalidSelectionsAndWrongPlayer` 与 `OfficialMulliganWithShortMainDeckDrawsAvailableCardsAndReturnsSetAside` 补齐起手调度边界，覆盖非当前调度玩家拒绝、最多 2 张、重复选择、非手牌选择、主牌堆不足时只抽可用牌并把搁置牌回收到主牌堆且不触发燃尽。
- 已补测试：`OfficialOpeningTests` 覆盖协议解析、卡组构筑拒绝条件、官方构筑负例矩阵、正式开局、起手调度、调度非法选择/抽牌不足边界、精确战场位置写回/来源不匹配拒绝、待清理致命单位移动被 blocking queue 拒绝。
- 已补测试：`P7SpellDuelReactionInheritsStackTimingContextWhenItCountersLastSpell` 覆盖法术对决反应/反制链继承 timing context；`CoreRuleEngineRejectedSpellDuelFocusPromptIncludesPlayableSwiftCard` 覆盖 core prompt 在法术对决焦点窗口暴露可打出的迅捷牌；`SnapshotsDoNotExposeRandomSeedOrCursor` 覆盖普通玩家 snapshot 隐藏随机种子和游标；`SpectatorReplayFrameRedactsPrivateZonesFaceDownObjectsAndRngState` 覆盖观战回放 redaction 与 `AuthoritativeStateHash`；`MatchStateHashIsStableAcrossDictionaryInsertionOrder` 覆盖权威状态 hash 的字典顺序稳定性；`OfficialOnlyRoomsRejectReadyBeforeDeckSubmission` 覆盖正式房间拒绝绕过 deck submit；`SnapshotsExposeBattlefieldControlOccupantsAndStandbyState` 覆盖战场状态 snapshot 投影；`MatchStateExposesAuthoritativeBattlefieldAndCleanupTaskViews` 覆盖服务端 `BattlefieldStates`、`START_SPELL_DUEL`/`START_BATTLE`、`PendingCleanupTasks`、`BattlefieldTasks`、`PendingTaskQueue`、`timing.battlefieldTasks`、`timing.pendingTaskQueue`、blocking prompt 与 command guard；`ActionPromptFiltersPlayCardSourcesByImplementedTimingAndBaseCost`、`ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits`、`ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower` 覆盖服务端 prompt 候选过滤；`MatchStateExposesTurnWindowSpellDuelAndBattleViews` 覆盖服务端四类窗口、法术对决和战斗状态视图；`MatchStateExposesContinuousEffectPowerLayerViews` 覆盖基础/有效战力与持续效果层 snapshot；`KeywordCoverageReportExposesDeferredKeywordFamilies` 覆盖关键词 deferred 报告；`OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 覆盖 Hub 级正式开局闭环；`P7PostStackCleanupDestroysPreExistingLethalFieldUnit` / `P7PostStackCleanupDestroysZeroPowerFieldUnit` / `P7BattleCleanupReconcilesAuthoritativeObjectLocations` 覆盖栈结算和战斗结算后的统一状态/位置清理兜底；`P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait` / `P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing` 覆盖彩色符能成功支付与失败回滚；`P7TypedPowerPaymentActivatesViSkillWithTraitPool` / `P7TypedPowerPaymentActivatesXerathSkillWithTraitPool` / `P7TypedPowerPaymentAssemblesLongSwordWithTraitPool` / `P79BattlefieldHeldPaysTypedPowerToGainScore` 覆盖非出牌与战场触发路径消耗 typed 符能；`P79ProductCatalogExposesRepresentativesWithoutClaimingFullOfficialRulePass` 覆盖图鉴状态口径拆分；当前回归记录为 `dotnet test 2851/2851`、`OfficialOpeningTests 9/9`、`ConformanceFixtureRunnerTests 2664/2664`、`ConformanceFixtureShapeTests 39/39`、`MatchRecoveryTests 15/15`、`GameHubJoinTests 85/85`、`CardCatalogBaselineTests 39/39`。
- 复审验证记录：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2851/2851；本批补充 `P7BattleCleanupReconcilesAuthoritativeObjectLocations` 1/1、`ConformanceFixtureRunnerTests` 2664/2664；`CardCatalogBaselineTests` 39/39；`GameHubJoinTests` 85/85；补充 `ConformanceFixtureShapeTests` 39/39、`OfficialOpeningTests` 9/9 与 `MatchRecoveryTests` 15/15；`git diff --check` 通过；工作区仅剩预期未跟踪 `riftbound-dotnet.sln`。
- 兼容性边界：为避免打碎既有开发 seed 和旧测试，当前无 decklist 的普通 `READY` 仍保留 legacy 入口；产品 UI 和后续正式规则路径必须强制先走 `SUBMIT_DECK`。因此 P0-001 从“缺失”降为“正式路径已存在，仍需收紧 legacy 入口/前端入口和调度边界”。

## 已确认做得比较扎实的部分

- 服务端权威与串行化：`src/Riftbound.Engine/MatchSession.cs:2375` 通过 `serialGate` 串行处理命令；`src/Riftbound.Engine/MatchSession.cs:2421` 只在 `result.Accepted` 时更新权威状态；`src/Riftbound.Api/Hubs/GameHub.cs:216` 只接收命令并广播服务端结果。
- 房间/重连/按玩家发送视图：`src/Riftbound.Api/Hubs/GameHub.cs:24` 支持加入房间，`src/Riftbound.Api/Hubs/GameHub.cs:53` 支持重连，`src/Riftbound.Api/Hubs/GameHub.cs:270` 按玩家组发送 snapshot/prompt。
- 基础隐藏信息：`src/Riftbound.Engine/MatchSession.cs:743` 对非己方手牌只给数量，`src/Riftbound.Engine/MatchSession.cs:811` 对非己方面朝下对象做字段裁剪；相关测试覆盖见 `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs:459`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs:29922`。
- 开发场景安全边界：`src/Riftbound.Api/Hubs/GameHub.cs:154` 的 `SeedScenario` 被限制在 Development 环境。
- 行为目录和 fixture 体系已经很大：`tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:68`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 说明当前项目已有系统性测试基础。

## P0 问题

### P0-001 官方构筑、开局、调度流程缺失

当前状态：**PARTIALLY RESOLVED / 正式入口、Hub 级 deck submit smoke、基础负例矩阵与起手调度边界已收紧，仍需 UI 正式入口**

规则依据：自查文档 3.1/3.2；核心规则关于 1v1 构筑、开局准备、随机战场、起手、调度、P2 额外符文的要求。

代码位置：
- `src/Riftbound.Contracts/Protocol.cs` 新增 `SubmitDeckCommand` 和 `MulliganCommand`。
- `src/Riftbound.Engine/OfficialDeckRules.cs` 新增官方 decklist 模型与校验器，覆盖主牌 40+、符文 12、战场 3、传奇/英雄匹配、同名 3、唯我 1、专属卡限制、颜色/符文特性约束。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `SubmitDeckAsync`、正式开局构建、按玩家 snapshot 的 `deckSubmitted`/`mulliganCompleted` 标记。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `MULLIGAN` 解析和调度结算，并修正后手额外符文从固定 seat P2 扩展为 `OpeningSecondActionPlayerId`。

现象：正式 deck path 已可测，并且 Hub 级 smoke 已覆盖双方通过 WebSocket/房间层提交 deck、ready、进入 mulligan、完成起手调度并进入首回合。起手调度拒绝错误玩家、超过 2 张、重复选择与非手牌选择；极端抽牌不足时只抽取可用牌并把搁置牌回收到主牌堆，不触发燃尽。为了兼容既有开发 seed 和旧测试，Development 与默认测试会话仍允许 no-deck legacy `READY`；API 在非 Development 环境创建的正式房间会关闭 legacy ready，未提交 deck 的玩家提交 `READY` 会被拒绝且状态不变。产品 UI 仍需要把正式入口改成显式 `SUBMIT_DECK -> READY -> MULLIGAN`。

最小复现场景：创建房间，P1/P2 先提交合法 `SUBMIT_DECK`，再双方 `READY`，服务端进入 `MULLIGAN`，双方按顺序 `MULLIGAN` 后进入首回合 `MAIN`。如果不提交 deck 而直接 `READY`，当前仍为 legacy 兼容路径。

建议修复：
- 将产品 UI 的 ready 按钮改为依赖 `deckSubmitted`，正式对战入口强制 `SUBMIT_DECK -> READY -> MULLIGAN`。
- 已完成：增加会话配置开关，非 Development API 房间禁止 legacy no-deck ready。
- 已完成：补基础负例矩阵，覆盖英雄不在主牌、主牌非法类别、未知卡号、符文/战场牌堆类别错误、越出传奇特性等拒绝路径。
- 已完成：补起手调度边界，覆盖非法选择、错误调度玩家和主牌堆不足时的服务端行为。
- 后续可继续补专属卡超过 3、唯我同名与多特性组合的更细 fixture，但 P0-001 当前主要剩余风险已转向前端正式入口。

建议测试：
- 已新增：`tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`。
- 已新增：正式-only 会话拒绝 no-deck `READY`。
- 已新增：GameHub 级 `SUBMIT_DECK -> READY -> MULLIGAN` smoke。
- 已新增：官方 deck validator 负例矩阵。
- 已新增：起手调度非法选择与抽牌不足边界。
- 待补：前端正式入口 smoke。

### P0-002 战场、待命区、控制权和单位位置模型不足

当前状态：**PARTIALLY RESOLVED / 对象位置索引、权威派生战场状态和权威 battlefield task 视图已落地，完整战场任务状态机仍待建模**

规则依据：自查文档 4、10；核心规则关于基地、战场、待命区、战场控制权、占领/争夺、单位移动与区域归属的要求。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 新增 `ObjectLocationState` 与 `MatchState.ObjectLocations`，snapshot 会输出对象 `location`，`SnapshotDto.Lanes.battlefields` 会投影战场牌、控制者、占据单位、待命占位、面朝下待命数量与争夺状态。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldState` 与 `MatchState.BattlefieldStates`，snapshot 复用该服务端状态视图，而不是自行重算 UI 专用结构。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldTaskState` 与 `MatchState.BattlefieldTasks`，争夺战场会公开 `START_SPELL_DUEL`、`START_BATTLE` 的任务状态、参与者和关联 stack/focus 信息。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的打出、结算、移动、调度、召符文路径开始同步 `ObjectLocations`。
- 仍缺：每个战场的 held/conquered/占领结果、待命容量和 control/contest 变更由统一 task queue 推进。

现象：系统现在可以在权威状态中表达对象所在粗粒度区域和精确战场 object id，并拒绝来源位置与权威状态不一致的精确游走；`MatchState.BattlefieldStates` 能从服务端状态统一暴露已知战场的占据/争夺/待命视图；`MatchState.BattlefieldTasks` 能把争夺战场后续 spell duel/battle 任务公开给 UI 和日志。但战场本身仍没有完整控制权变更、占据、征服、战斗/法术对决任务推进生命周期，因此 P0-002 仍只能降级为部分解决。

最小复现场景：在两个友方战场之间提交精确 `MOVE_UNIT` 游走。当前结果会写回 `ObjectLocations[source].BattlefieldObjectId`；如果客户端提交的 origin 与权威位置不一致，服务端会拒绝。

建议修复：
- 在当前 snapshot 视图基础上继续把 `BattlefieldState`/`BattlefieldTaskState` 接入真正的 `PendingTaskQueue`，覆盖 held/conquered、控制权改变和占领结果。
- 将 `PlayerZones.Battlefields` 从“对象列表”升级为“玩家战场槽位 + 位置引用”，并让 `CardObjectState` 或 location index 记录对象所在具体位置。

建议测试：
- 单位从基地到战场、战场到基地、战场间游走。
- 移入空战场、敌方控制战场、双方争夺战场后的控制权、占领、战斗/法术对决 pending 状态。
- 待命区容量、面朝下信息、revealed 后转移。

### P0-003 通用清理检查与任务队列缺失

当前状态：**PARTIALLY RESOLVED / 状态性 cleanup task、battlefield task 视图、致命伤害/0 战力 cleanup loop 与 blocking guard 已接入，完整统一任务队列仍缺失**

规则依据：自查文档 5；核心规则关于“任意状态变化后进行清理检查、重复直到稳定、触发待处理任务、清理期间不能响应”的要求。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:18896` 有局部 `ApplyLethalDamageCleanup`。
- `src/Riftbound.Engine/CoreRuleEngine.cs:19362` 有回合结束清理。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10641` 的 `ResolveEndTurn` 只在回合结束路径调用 `ApplyTurnEndCleanup`，然后直接 `ResolveTurnStart`。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `PendingCleanupTasks`，当前可显式暴露致命伤害、0 战力现代权威单位清理和战场争夺检查任务；`BattlefieldTasks` 可进一步暴露争夺后的 spell duel/battle 任务状态。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `PendingTaskQueue`，按状态性清理、战场争夺、法术对决启动、战斗启动的顺序公开当前 active task 和 blocking phase。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `RunStateBasedCleanupLoop`，移动和结算链结算后会重复执行致命伤害/0 战力清理直到稳定；`ResolveAsync` 会在 blocking pending task queue 期间拒绝普通玩家命令。

现象：当前清理仍没有完全升级为官方意义上的“所有状态变化后统一检查并重复”的持久任务队列。移动、栈项目结算、战斗伤害、Xerath 技能伤害和回合开始战场群体伤害会运行状态性致命伤害 cleanup loop 并同步位置；有明确 owner/controller 的 0 战力场上单位也会在该 loop 中按 `ZERO_POWER` 清理；`PendingCleanupTasks` 已能列出待处理的致命伤害、0 战力、战场争夺、`START_SPELL_DUEL` 与 `START_BATTLE` 任务；`PendingTaskQueue` 能按清理优先级公开 active task 和 blocking phase；`BattlefieldTasks` 能给这些战场任务附带状态和参与者。服务端 prompt/command guard 已阻止普通行动在 blocking queue 期间继续执行，例如行动前已处于致命伤害待清理的单位不能再移动。但由战场控制权变化、连续效果变化、替代效果等触发的 pending duel/battle/控制权变化仍无法通过一个中央状态机保证。

最小复现场景：尝试移动一个已经带致命伤害的单位，当前会因 `DESTROY_LETHAL_UNIT` blocking task 被拒绝；结算一个无行为栈项目时，如果场上已有致命伤害单位或有 owner/controller 的 0 战力现代权威单位，也会在栈结算后被状态性清理兜底摧毁。但如果移动导致战场控制权/争夺状态变化，仍没有统一 cleanup loop 能保证后续待处理任务被稳定排入并按官方顺序解决。

建议修复：
- 引入 `PendingTaskQueue` 与 `RunCleanupLoop`，统一处理致命伤害、0 战力、离场、战场控制变化、法术对决/战斗启动、胜负检查。
- 所有命令、栈结算、触发结算、移动、进场/离场之后必须进入同一 cleanup loop。

建议测试：
- 已新增：移动、栈结算、战斗伤害、Xerath 技能伤害、回合开始战场伤害进入同一 cleanup loop；0 战力现代权威单位清理进入同一 cleanup loop；争夺战场暴露 START_SPELL_DUEL/START_BATTLE 任务及 `BattlefieldTasks` 权威任务视图；待清理 blocking queue 会关闭普通 prompt 并拒绝普通命令。
- 待补：战力变化产生的 0 战力对象应立即触发持久 task queue；替代效果、所有进出战场路径都触发同一持久 cleanup task queue。
- cleanup loop 重复直到稳定。
- 已新增：cleanup/task queue 阻塞期间 prompt 只给 `WAIT`，普通命令被 `PhaseNotAllowed` 拒绝。

### P0-004 法术对决与战斗不是完整官方状态机

当前状态：**PARTIALLY RESOLVED / 法术对决焦点恢复、显式 SpellDuel/Battle 状态和 BattlefieldTask 视图已落地，完整任务状态机仍缺失**

规则依据：自查文档 11、12；核心规则关于 FEPR、法术对决焦点、初始栈、双方行动、战斗 pending、攻击/防守单位声明、战斗清理、无战斗结果的要求。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:232` 的命令分发直接按 `PlayCard`、`MoveUnit`、`DeclareBattle` 等命令进入各自 resolver。
- `src/Riftbound.Engine/MatchSession.cs` 的 `StackItemState.TimingContext` 现在记录入栈前的 timing window。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `TurnWindowState`、`SpellDuelState`、`BattleState`，snapshot timing 暴露四类窗口、法术对决和战斗参与者视图。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldTaskState`，snapshot timing 暴露 `battlefieldTasks`，争夺战场可以把后续 spell duel/battle 任务状态化。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolvePassPriority` 现在能在法术对决栈清空时恢复 `SPELL_DUEL_OPEN` 并转移焦点。
- `src/Riftbound.Engine/CoreRuleEngine.cs:4174` 的 `ResolveDeclareBattle` 直接执行战斗。
- `src/Riftbound.Engine/CoreRuleEngine.cs:5185` 的 `TryBuildMinimalDeclareBattle` 只支持 1 个攻击者、1 到 2 个防守者，且条件被命名为 minimal。
- `src/Riftbound.Engine/CoreRuleEngine.cs:4275` 到 `src/Riftbound.Engine/CoreRuleEngine.cs:4382` 直接计算并应用伤害。

现象：当前战斗仍是显式 `DECLARE_BATTLE` 命令驱动的“立即结算战斗片段”，不是由清理任务在争夺战场时启动的完整 battle task。法术对决已修复几个关键窗口问题：迅捷牌结算后不会提前关闭法术对决；反应/反制链会继承并保留法术对决 timing context；core prompt 在法术对决焦点窗口也会暴露可支付、合法时点的迅捷出牌来源。现在服务端也能显式表达四类窗口、当前法术对决、战斗参与者和争夺战场任务视图，但仍缺少围绕某个 battle/trigger/card 的完整 pending/focus/initial-stack 生命周期。

最小复现场景：迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，当前会回到 `SPELL_DUEL_OPEN` 且焦点移交下一名玩家。单位移动到敌方控制战场时，按官方规则应进入争夺并触发法术对决/战斗流程；这一部分仍没有完整 battle task。

建议修复：
- 建立 `SpellDuelState` 和 `BattleState`，由 cleanup/task queue 创建、推进和关闭。
- 声明攻击/防守、初始栈、双方 focus/pass、swift/reaction 许可、战斗伤害、战斗结果和清理全部挂在同一状态机。

建议测试：
- 由移动/占领触发的法术对决与战斗。
- focus 轮转、pass、swift/reaction、初始栈顺序。
- 多攻击者/多防守者、伤害分配顺序、战斗没有结果时的状态。

### P0-005 彩色符能、普通费用、符能费用与资源技能模型不足

当前状态：**PARTIALLY RESOLVED / typed pool 已覆盖 PLAY_CARD、代表性非出牌支付和一个 battlefield trigger 支付路径，prompt 已做基础费用来源过滤，符文/传奇/战场技能支付来源仍待统一**

规则依据：自查文档 8、15；核心规则关于 `A/C`、阵营符能、费用支付、符文技能、可选费用、Spellshield/Encourage/Echo/Haste 等费用分支。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 的 `RunePool` 已升级为 `Mana` + 泛化 `Power` + `PowerByTrait`，并通过 `RuneTrait.Normalize` 支持中英文颜色别名。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10141` 的出牌计划从 `CardBehaviorRegistry` 获取行为并做局部费用计算。
- `src/Riftbound.Engine/MatchSession.cs` 的 `ActionPromptBuilder` 会要求 `PLAY_CARD` / `MOVE_UNIT` / `ASSEMBLE_EQUIPMENT` 存在服务端候选源才启用；出牌来源会经过 BehaviorRegistry、时点权限和基础费用过滤。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `PLAY_CARD` 支付计划已可记录任意符能与指定特性符能，并通过 `PayRuneCosts` / `CanPayRuneCosts` / `CanPayPowerCost` 校验与扣费。
- `ASSEMBLE_EQUIPMENT`、Vi 双倍战力技能、Xerath 伤害技能、能量枢纽据守支付 4 符能得分等代表性非出牌/战场触发支付路径已改为 typed-power aware；泛化符能费用会从普通 `Power` 优先扣除，再按特性名稳定扣除 `PowerByTrait`。
- 仍缺：符文技能、传奇技能、战场技能、Haste/Echo/Spellshield 等所有支付路径都统一进入同一个官方费用模型。

现象：服务端现在可以在 `PLAY_CARD` 的可选符能支付中表达并校验指定特性，例如 `SPEND_POWER:red:2` 会要求红色符能并只扣红色；旧 fixtures 的泛化 `power` 仍按任意符能兼容。装备装配、两个代表性主动技能和一个战场据守支付触发也可以用 `PowerByTrait` 支付泛化符能费用。普通开环 prompt 不再把无服务端来源、基础费用不足的出牌、非正面/战斗中移动源或无可支付装配源展示为 enabled。但同阵营符能、多符能组合，以及由 rune/legend/battlefield/skill 产生的复杂支付来源选择仍未统一。

最小复现场景：P1 拥有 `powerByTrait.red = 2` 时打出《弹幕时间》并提交 `SPEND_POWER:red:2` 会成功入栈且只消耗红色；如果只有 `powerByTrait.blue = 3`，同一命令会以 `INSUFFICIENT_COST` 拒绝且手牌、资源和结算链不变。P1 只有 `powerByTrait.red = 1` 且普通 `Power = 0` 时，也可以装配长剑、启动 Vi/Xerath 的代表性泛化符能技能，并在支付后清空对应 typed 符能。P2 只有 `powerByTrait.red = 4` 且普通 `Power = 0` 时，能量枢纽据守支付 4 符能得分也会成功并扣空 red。需要 `[A]`、`[C]`、同阵营 Haste 支付、Spellshield 多目标加税或 Echo 复杂费用的更广路径仍未完整。

建议修复：
- 继续把所有支付都通过统一 `PaymentEngine` 校验，支持普通费用、符能费用、额外/可选费用、替代费用、减费/加费、符文技能。
- 为 `CardBehaviorDefinition` 或 BehaviorSpec 费用模型补充官方颜色需求，而不是只靠客户端传入 `SPEND_POWER:<trait>:<amount>`。

建议测试：
- 已新增：指定红色符能支付成功扣对应 trait；指定红色但只有蓝色时拒绝且状态不变；装备装配、Vi 技能、Xerath 技能和能量枢纽据守得分触发可以用 typed 符能支付泛化符能费用；出牌/移动/装配 prompt 对基础不合法来源禁用。
- 待补：`[A]`、`[C]`、单阵营/多阵营费用、Haste 彩色支付、Spellshield 多目标加税、Echo 费用、支付失败不改变状态。

## P1 问题

### P1-001 连续效果、层、时间戳和依赖模型不足

当前状态：**PARTIALLY RESOLVED / 服务端持续效果层视图已落地，完整 LayerEngine 仍待实现**

规则依据：自查文档 14；核心规则关于连续效果、层、时间戳、依赖和装备/静态效果的重算。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:17140` 的 `ApplyPowerModifier` 直接修改 `CardObjectState.Power`，并用 `UntilEndOfTurnPowerModifier` 聚合记录。
- `src/Riftbound.Engine/CoreRuleEngine.cs:19395` 在回合结束时从 `Power` 中扣回聚合值。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `ContinuousEffectState` / `ContinuousEffectLayers` / `MatchState.ContinuousEffects`，snapshot 会暴露 `timing.continuousEffects`，公开对象会暴露 `basePower` 和 `effectivePower`。

现象：临时战力修正仍通过修改对象当前数值实现，但服务端现在能派生出对象级 `POWER_MODIFIER` 层、全局/对象级 `RULE_TEXT` 层以及 `basePower` / `effectivePower` 视图，供 UI、日志和后续 LayerEngine 使用。完整的“基础值 + 连续效果层 + 时间戳 + 来源 + 依赖”重算模型仍未替换所有战力/关键词/装备静态效果路径；多个装备、静态光环、控制权变化、失去/获得关键词时仍可能与官方层规则不一致。

建议修复：引入 `ContinuousEffect`、`LayerEngine`、`Timestamp`、`SourceObjectId`，计算视图时重算派生属性，避免永久修改基础属性。

建议测试：已新增 `MatchStateExposesContinuousEffectPowerLayerViews` 覆盖基础/有效战力、对象级 power modifier、对象/全局 until-end 效果 snapshot。待补：多个装备/光环叠加、控制权变化、失去来源、同层时间戳、回合结束、最小战力限制。

### P1-002 关键词覆盖仍存在“识别但 deferred”的内部事实

当前状态：**PARTIALLY RESOLVED / deferred 关键词覆盖已变成服务端/API 显式报告，完整执行仍待逐族补齐**

规则依据：自查文档 15；关键词必须不仅被识别，还要能按官方时点、费用、目标、区域和替代/触发规则执行。

代码位置：
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:63` 到 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:73` 明确装备关键词是 `RecognizedDeferred`。
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:89` 到 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs:93` 说明只有代表性 Take Up，assemble/agile/tempered/static modifier 等仍 deferred。
- `src/Riftbound.Engine/CardInteractionKeywordRules.cs:72` 到 `src/Riftbound.Engine/CardInteractionKeywordRules.cs:75` 说明 Standby/Ambush/Echo 仍有宽泛 deferred 分支。
- `src/Riftbound.Engine/CardCombatKeywordRules.cs:55` 到 `src/Riftbound.Engine/CardCombatKeywordRules.cs:60` 说明 combat damage、assignment order、roam movement execution remain deferred。
- `src/Riftbound.Engine/CardResourceKeywordRules.cs:76` 到 `src/Riftbound.Engine/CardResourceKeywordRules.cs:80` 说明 broader experience/level/encourage/spellshield branches remain deferred。
- `src/Riftbound.Engine/KeywordCoverageReporter.cs` 会把各关键词族 profile 汇总成 `KeywordCoverageReport`，API `/catalog/keyword-coverage`、`/catalog/summary`、`/catalog/p3-status` 会公开这些 deferred 状态。

现象：这些 profile 与“所有卡牌功能完整实现”存在冲突。即便某些代表性路径已有实现，关键词族整体不能判定完整。现在 deferred 状态不再只是测试/内部 profile 里的事实，而是正式服务端报告和 API 输出；前端、图鉴和本地测试入口可以按 family/status 禁用或明确提示。

建议修复：继续把关键词 profile 状态与真实规则执行路径重新对齐；没有完整执行的卡牌/功能单元不能暴露为完全 `CONFORMANCE_PASS`。下一步要按 family 优先级补真实执行路径，而不是只扩展报告。

建议测试：已新增 `KeywordCoverageReportExposesDeferredKeywordFamilies`，覆盖关键词 family 报告、deferred rows 和 implemented/deferred 并存口径。待补：按关键词族建立完整矩阵测试，每个关键词覆盖时点、费用、目标、隐藏信息、替代/触发、失败回滚。

### P1-003 BehaviorSpec/CONFORMANCE_PASS 口径疑似过度乐观

当前状态：**PARTIALLY RESOLVED / 已拆分代表性通过与官方完整通过，后续仍需逐条提升证据**

规则依据：自查文档 16、19、21；BehaviorSpec 需要真实映射到可执行规则，不能用域级占位掩盖未实现细节。

代码位置：
- `src/Riftbound.Contracts/BehaviorSpecs.cs` 新增 `BehaviorConformanceTiers` 和 `BehaviorSpec.ConformanceTier/ConformanceReason`。
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 继续保留 `Status=implemented` 作为已有代表路径/fixture 口径，但 `ConformanceTier` 只标为 `representative-rule-pass`，没有标为 `full-official-rule-pass`。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 不再断言 “CONFORMANCE_PASS = 官方完整”，而是断言 `1009/1009 representative-rule-pass` 与 `0 full-official-rule-pass`。
- 但 `docs/CURRENT_P6_STATUS.md:894` 到 `docs/CURRENT_P6_STATUS.md:901` 仍记录 P6 final 为 `713/811` implemented、`98/811` manual deferred；`src/Riftbound.Engine/P6LegendAbilityCatalog.cs:13`、`src/Riftbound.Engine/P6BattlefieldEffectCatalog.cs:20` 也保留 deferred surfaces。

现象：P7.9 文档与旧测试曾把目录推进到全 implemented，但底层规则模型仍存在 P0/P1 缺口。现在 API/UI 可以展示“代表性通过”，并明确当前不是官方完整规则闭环；后续仍需要逐卡把代表性证据提升为完整规则证据。

建议修复：
- 继续将非 PLAY_CARD 域逐条映射到真实命令、状态机和测试；只有对应 P0/P1 规则域完整后才允许提升到 `full-official-rule-pass`。
- 逐步让 UI playable filter 从旧 `status=implemented` 迁移到“服务端 prompt candidate + conformance tier + deferred surface”三重门槛。

建议测试：
- 已新增：每个 BehaviorSpec 必须有 `ConformanceTier/ConformanceReason`；当前 `full-official-rule-pass` 必须为 0。
- 待补：每个 BehaviorSpec 的 `ImplementedEffectKind` 必须能追溯到具体 resolver、prompt candidate、fixture 和官方文本锚点。
- 对 legend/battlefield/token/rune 域建立负例，防止占位 domain 把未实现能力标为 pass。

### P1-004 隐藏信息与 replay 边界仍需加固

当前状态：**PARTIALLY RESOLVED / 普通 snapshot、spectator replay redaction 与权威状态 hash 已修，严格 action-log replay 仍待补**

规则依据：自查文档 2、18；客户端不得得到能预测未来随机信息的私密状态；replay/观战要区分公开信息与玩家私有视角。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 的普通玩家 snapshot 已移除 `seed` 和 `rngCursor`，并新增 `BuildSpectatorSnapshot` 统一观战视角裁剪。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchReplayRedactor.BuildSpectatorFrame`，从权威 journal entry 生成观战/回放 frame 时不复用任一玩家私有视角。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchStateHasher`，`MatchReplayFrame.AuthoritativeStateHash` 会携带 canonical SHA-256 状态 hash，供 replay frame 与实时权威状态对账。
- `src/Riftbound.Engine/MatchSession.cs` 的 `RestoreState` 仍优先恢复 authoritative state；没有 action-log replay 到相同最终状态的独立校验路径。

现象：目前 opponent hand/face-down redaction 做得不错，普通玩家 snapshot 也已不再包含 `seed`/`rngCursor`；观战/回放 frame 现在也会从 authoritative state 重新生成 spectator snapshot，而不是直接拿玩家 snapshot，并携带稳定的权威状态 hash 用于最终状态对账。剩余风险是 replay 更像恢复快照/权威状态，不是严格的命令日志重放到最终状态的模型。

建议修复：
- 已完成：从普通玩家 snapshot 中移除 seed/rngCursor；如后续需要调试随机状态，应单独走 Development/debug stream，不能复用普通玩家 snapshot。
- 已完成：从 journal entry 构建 spectator replay frame 时强制使用 spectator redaction。
- 已完成：replay frame 携带 canonical authoritative state hash，用于最终状态一致性校验。
- 建立 action log replay：从初始公开/私有边界 + 命令日志重放到最终 authoritative state，并输出 spectator frame。

建议测试：
- 已新增：玩家 snapshot 不含 seed/rngCursor。
- 已新增：spectator replay 不泄露手牌、面朝下内容和未来随机 seed/rngCursor，并携带 64 位 hex authoritative state hash。
- 已新增：authoritative state hash 对字典插入顺序稳定。
- 待补：真正 action log replay 后的 final state hash 等于实时 state hash。

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

当前状态：**RESOLVED FOR THIS AUDIT / 当前报告已按产品、fixture、官方完整规则三层拆分口径**

`docs/CURRENT_P7_9_STATUS.md:76` 到 `docs/CURRENT_P7_9_STATUS.md:78` 记录 legend/battlefield 已 0 remaining，`docs/CURRENT_P7_9_STATUS.md:153` 到 `docs/CURRENT_P7_9_STATUS.md:158` 记录 P7.9 全完成；但本次核心规则自查显示这些结论只适合描述当前产品/fixture 口径，不能直接等同“官方完整核心规则 READY”。

当前口径说明：
- `Product UI playable smoke`: 当前 P7 页面可用性。
- `Conformance fixture pass`: 当前 fixture 全绿。
- `Official full rules ready`: 本报告结论为 NOT READY。

## 模块矩阵

| 模块 | 结论 | 说明 |
| --- | --- | --- |
| 服务端权威/幂等/非法命令不落状态 | PASS | `MatchSession.SubmitAsync` 串行且仅 accepted 更新状态。 |
| 双人房间/重连/snapshot/prompt | PASS | `GameHub` 具备 join/reconnect/request snapshot/submit flow。 |
| 隐藏手牌/面朝下对象 | PASS | 手牌、face-down、随机 seed/rngCursor 与 spectator replay frame 均已裁剪。 |
| 官方 deck/opening/mulligan | RISKY | 已有 `SUBMIT_DECK -> READY -> MULLIGAN` 正式路径、deck validator、负例矩阵、起手调度边界和 Hub smoke；剩余风险主要是产品前端仍需强制正式入口。 |
| 区域/对象/控制权/战场位置 | RISKY | 已有 `ObjectLocations`、`BattlefieldStates` 与 `BattlefieldTasks` 权威派生视图；仍缺完整 battlefield/standby/control task 状态机。 |
| FEPR/优先权/焦点 | RISKY | 有 `TurnWindowState`、`SpellDuelState`、`BattlefieldTasks`、focus/prompt；仍缺完整 pending task/state machine。 |
| 栈/出牌/费用/目标 | RISKY | 大量代表路径实现；PLAY_CARD 与代表性非出牌路径已 typed-power aware，但通用支付来源/目标语法仍不足。 |
| 通用清理检查 | RISKY | 已有 `PendingCleanupTasks`、`PendingTaskQueue` 与移动/栈结算后的致命伤害/0 战力 cleanup loop；仍缺覆盖全部状态变化的统一任务队列。 |
| 移动/战场控制 | RISKY | 精确移动可写回 object location，战场状态可表达争夺；完整控制权改变/征服/据守仍待状态机化。 |
| 法术对决 | RISKY | 已有显式 `SpellDuelState`、`BattlefieldTasks` 与焦点恢复；仍缺完整 spell duel lifecycle。 |
| 战斗 | RISKY | 已有显式 `BattleState` 参与者视图和 `START_BATTLE` 任务视图；当前仍是 direct/minimal declare battle，不是官方 battle task。 |
| 计分/胜负 | RISKY | 有部分得分/胜负实现；依赖战场控制与 cleanup 的完整性不足。 |
| 连续效果层 | RISKY | 已有 `ContinuousEffectState`、`basePower`、`effectivePower` 视图；仍缺完整 LayerEngine/timestamp/dependency 重算。 |
| 关键词 | RISKY | 已有 `KeywordCoverageReporter` 暴露 implemented/delegated/deferred 边界；多个关键词族仍需真实执行矩阵。 |
| 全卡牌效果 | RISKY | BehaviorSpec 已降义为 representative-rule-pass 且 full-official-rule-pass=0；仍需逐卡提升证据。 |
| 日志/replay/观战 | RISKY | 有 journal/recovery、普通 snapshot 随机裁剪、spectator replay redaction 与 authoritative state hash；仍缺严格 action-log replay final-state 校验。 |

## 建议下一步开发顺序

1. 已完成：冻结并重命名当前 `CONFORMANCE_PASS` 口径，避免把 fixture/domain pass 当成 full official rules pass。
2. 已完成第一批：官方 deck/opening/mulligan、Hub smoke、legacy ready 生产边界。
3. 已完成第一批：board object location、battlefield state、battlefield task、pending cleanup/turn window/spell duel/battle/continuous effect/keyword coverage/spectator replay 的服务端显式视图。
4. 下一步：把 battlefield task view 接入真正的 board task model：standby、control/contest/conquer/hold 与 battlefield pending task 生命周期。
5. 下一步：建立通用 cleanup task queue，把移动、进场、离场、伤害、战力变化、战场控制变化统一纳入。
6. 下一步：在 task queue 上重做 spell duel 和 battle state machine。
7. 下一步：扩展 typed payment engine 到 rune/legend/battlefield/keyword 全路径，支持替代费用、减费/加费、额外/可选费用。
8. 下一步：引入完整 continuous effect LayerEngine，并逐关键词、逐卡牌把 `Representative/FixturePass` 提升到 `FullOfficialRulePass`。

## 最终验收口径

在完成上述 P0/P1 之前，不建议把服务端声明为“完整符合五个官方规则文件”。推荐当前对外口径为：

> 当前服务端可支撑本地双人对战原型、服务端权威结算、正式 deck/opening/mulligan 入口、代表性规则与大量 conformance fixture；但完整官方核心规则仍处于 NOT READY，自查剩余风险集中在完整战场/待命/控制权任务状态机、通用清理任务队列、法术对决/战斗状态机、全路径官方费用模型、连续效果 LayerEngine 与逐关键词/逐卡牌完整执行。
