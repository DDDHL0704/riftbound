# 符文战场 Web 前端重建与服务端补齐计划

更新日期：2026-05-08
当前结论：**NOT READY**
当前完成度：约 **99%**，预计仍需 completion audit 与少量收口修复。
用途：作为本轮“产品级 Web 前端重建 + 服务端规则补齐”的短入口，后续每个批次都应回到本文更新范围、验收和剩余风险。

## 1. 已读取并确认的资料

本批次已先读取并确认以下资料：

- `docs/符文战场_前端Web开发需求文档_给Codex.md`：2485 行。前端目标是产品级、中文、1v1 双人联机、服务端权威、围绕 `Snapshot` / `ActionPrompt` / `Events` / 错误运行；禁止前端自行裁决规则或隐藏服务端缺口。
- `docs/符文战场_服务端核心规则自查文档.md`：1452 行。服务端 READY 门槛是无 P0、完整官方 1v1 开局/回合/FEPR/法术对决/战斗/得分/胜负/隐藏信息/FAQ/关键词/单卡证据。
- `docs/CURRENT_SERVER_RULE_AUDIT.md`：当前明确 **NOT READY**，剩余阻断集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- `docs/START_HERE.md` 与 `README.md`：仍保留 P7/P7.9 已完成的旧交接口径；本轮以 `CURRENT_SERVER_RULE_AUDIT.md`、新的前端需求文档和本文为准。
- 根目录五个官方 PDF/FAQ，已用 `pdftotext` 抽取到临时文本并核对页数：
  - `《符文战场》核心规则_260330.pdf`：105 页，核心规则最后更新时间 2026-03-30。
  - `裁判FAQ_251023.pdf`：10 页，裁判 FAQ 最后更新时间 2025-10-23。
  - `铸魂淬炼系列_官方FAQ_260114.pdf`：21 页，官方 FAQ，含勘误和规则阐明。
  - `铸魂淬炼系列_裁判FAQ.pdf`：25 页，更新日期 2026-01-14。
  - `《符文战场》破限系列_裁判FAQ_260416.pdf`：11 页，破限系列裁判 FAQ，更新日期 2026-04-16。
- `src/Riftbound.Api`：ASP.NET Core + SignalR；REST 目前包括 `/health`、`/catalog/summary`、`/catalog/p3-status`、`/catalog/behavior-specs`、`/catalog/keyword-coverage`，实时入口为 `/hubs/game`。
- `src/Riftbound.Engine/MatchSession.cs`：当前已有 `MatchState`、视角化 snapshot、prompt builder、官方 deck submit/mulligan、对象位置、battlefield/task/pending queue 视图，以及 `serialGate` 串行命令处理。
- `src/Riftbound.Engine/CoreRuleEngine.cs`：当前已有代表性规则执行、typed power 部分支付、移动/战斗/栈结算局部 cleanup、spell duel focus 修复；仍缺统一 PaymentEngine、LayerEngine 和完整任务状态机。
- `tests/Riftbound.ConformanceTests`：当前包含 1210 个 fixture 文件，核心测试入口包括 `ConformanceFixtureRunnerTests`、`ConformanceFixtureShapeTests`、`GameHubJoinTests`、`OfficialOpeningTests`、`CardCatalogBaselineTests`、`MatchRecoveryTests`。

## 2. 当前前端现状

现有前端位于 `src/Riftbound.DevUi`，技术栈为 React 19 + TypeScript + Vite + SignalR。新的前端需求文档没有要求更换技术栈，且仓库已有可用 Vite/React 构建，因此本轮沿用该栈；新增 `lucide-react` 作为轻量图标库，不引入重 UI 框架或 Next.js。

现状问题：

- 代码集中在 `src/Riftbound.DevUi/src/main.tsx`（7083 行）和 `src/Riftbound.DevUi/src/styles.css`（4570 行），不满足长期维护、组件拆分、adapter/store 分层要求。
- 当前 UI 仍混有开发期操作台、调试面板、产品桌面和 JSON intent 工作台，不符合“清理旧 UI，重建产品级卡牌对战桌面”的执行要求。
- 已经接入真实 `GameHub`，但前端内部类型、页面结构和路由仍不够清晰；后续必须拆成 `services`、`types`、`stores`、`components`、`pages` 和 `features`。
- 现有页面已有动作候选、卡牌快捷操作、图鉴和双人测试能力，可作为协议理解参考，但不能作为最终架构继续堆叠。

## 3. 当前服务端可供前端消费的能力

已确认可用：

- SignalR 方法：`JoinRoom(roomId, playerId, reconnectToken?)`、`Reconnect(roomId, playerId, reconnectToken)`、`RequestSnapshot(roomId, playerId)`、`Ready(roomId, playerId, clientIntentId)`、`SubmitIntent(roomId, playerId, clientIntentId, cmd)`。
- Development-only：`SeedScenario(roomId, playerId, scenarioId, clientIntentId)`，只能用于本地 smoke 和开发场景，不得伪装成生产对局能力。
- 服务端消息：`Joined`、`Snapshot`、`Prompt`、`Events`、`Error`。
- 命令 DTO：`SUBMIT_DECK`、`MULLIGAN`、`PASS_PRIORITY`、`PASS_FOCUS`、`PASS`、`END_TURN`、`PLAY_CARD`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`HIDE_CARD`、`TAP_RUNE`、`RECYCLE_RUNE`、`REVEAL_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT`、`DECLARE_BATTLE`。
- Snapshot 已提供 `players`、`lanes`、`stack`、`timing`、`turnState`，其中 `timing` 已包含 `turnWindow`、`spellDuel`、`battle`、`battleResolutions`、`battlefieldTasks`、`battlefieldResolutions`、`pendingTaskQueue`、`continuousEffects` 等服务端权威视图。
- Prompt 已提供 `actions` 与结构化 `candidates`，包含 `sources`、`targets`、`destinations`、`modes`、`optionalCosts`、`metadata`。
- 图鉴与卡牌状态可从 `/catalog/behavior-specs` 和 `/catalog/keyword-coverage` 获得；当前只能展示 `representative-rule-pass`，不能宣称 full official rule pass。

## 4. 服务端缺口处理原则

本轮原则不是永久降级前端能力，而是：

1. 前端发现服务端未提供 snapshot 字段、ActionPrompt、候选、命令、事件或状态视图时，先记录为服务端缺口。
2. 对 P0/P1 或对产品级对战必需的缺口，必须按五个官方规则/FAQ PDF 补齐服务端实现、测试和文档。
3. 在服务端补齐并有测试前，前端不得自行裁决或假装可玩；只能暂时禁用入口、过滤候选或明确提示“等待服务端规则能力”。
4. 服务端补齐后，前端再接入对应 prompt/snapshot/event，而不是保留 mock 或客户端规则判断。

当前已知服务端缺口：

- 完整 battlefield/standby/control task 状态机未完成：前端只能展示服务端 `battlefieldTasks` / `pendingTaskQueue`，不能自行推进战场控制、待命移除、征服/据守或争夺结论。
- Central cleanup task queue 未完成：前端只能展示清理结果和阻塞 `WAIT` prompt，不能本地继续开放普通行动。
- Spell duel/battle lifecycle 未完整官方化：前端可以显示 `spellDuel`、`battle`、`PASS_FOCUS`、`DECLARE_BATTLE` 等候选；当前只开放服务端支持的单攻击者/多防守者代表路径、多攻击者/单防守者代表路径、最多 2 攻击者 + 最多 2 防守者代表路径、同优先级壁垒防守者顺序选择，以及无胜者战斗事件的代表性证据，不能用客户端 UI 计算“法术对决结束”“战斗伤害结算”“控制权改变”。
- PaymentEngine 未统一：前端只能提交服务端候选中暴露的 `optionalCosts` / 支付 token；未暴露的费用分支不得做成可选项。
- LayerEngine 未完整：前端展示 `basePower` / `effectivePower` / `continuousEffects`，不得从卡面和装备自行重算战力或关键词。
- 全官方卡牌证据仍不足：图鉴必须明确展示 `representative-rule-pass` / deferred family 状态，不能显示“官方完整通过”。

## 5. 前端重建批次

### Batch 1：需求与现状审计

状态：完成。

交付：

- 确认需求文档、自查文档、五个 PDF、服务端入口、关键引擎和测试入口。
- 新增本文，明确当前 NOT READY、真实接口、前端清理范围和服务端缺口。
- 提交后继续进入前端清理与新架构。

验收：

- `git diff --check`
- 提交后 `git status --short` 只剩 `?? riftbound-dotnet.sln`

### Batch 2：前端清理与新架构

状态：完成。

交付：

- 删除旧 `main.tsx` 巨型 UI 和旧 CSS 组织方式，保留 Vite/React/SignalR 构建配置与 package lock。
- 建立全新目录：
  - `src/app`
  - `src/components`
  - `src/pages`
  - `src/services`
  - `src/stores`
  - `src/types`
  - `src/utils`
  - `src/styles`
- 建立真实 REST/SignalR adapter：`ApiClient`、`MatchSocket`、catalog store、settings store、match controller。
- 建立中文产品级页面壳：首页、大厅、房间、对战桌面、图鉴、卡组、设置。
- 首批桌面只渲染服务端 snapshot / prompt / candidate / event，不保留旧 JSON 调试台和客户端规则裁决入口。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- Browser Use smoke：打开 `http://127.0.0.1:5173/`、`/cards`、`/lobby`、创建房间、入座、提交测试卡组、ready、进入 `/matches/{roomId}` 并重连同步 snapshot；控制台无 error/warn。
- Smoke 发现并修复对战桌面横向溢出，右侧行动面板已在 1248px 浏览器宽度内完整可见。
- Smoke 发现并修复首次随机玩家名不持久化导致刷新后重连身份变化的问题。

### Batch 3：服务端能力矩阵与正式房间闭环

状态：完成。

交付：

- 前端房间页与对战行动面板改为只渲染 `Prompt.candidates` 中服务端支持的游戏命令；连接/重连和页面跳转仍作为非游戏命令常驻。
- 补齐服务端房间阶段 prompt：未提交合法卡组时只给 `SUBMIT_DECK`，提交后才给 `READY`，已准备后给 `WAIT`。
- 以 `SUBMIT_DECK -> READY -> MULLIGAN -> MAIN` 作为正式 Web smoke 入口；Development legacy ready 不再出现在新前端主流程。
- 若服务端缺 deck REST/保存接口，前端先做本地 deck import/select，但 ready 前必须走 `SUBMIT_DECK`。
- 把前端发现的 P0/P1 必需服务端缺口追加到 `CURRENT_SERVER_RULE_AUDIT.md`，并补服务端测试。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`：通过 85/85。
- Browser Use smoke：P1 创建房间并入座，提交卡组前只显示 `提交卡组` 候选且不显示 `准备`；提交卡组后只显示 `准备`；P2 通过房间码加入、提交卡组、ready；双方进入 `MULLIGAN`，依次执行起手调度后进入 `MAIN`；对战桌面展示事件日志、双方公开区、对手隐藏手牌卡背和最终 snapshot。
- Smoke 发现并修复正式开局后玩家面板内容溢出覆盖中央战场的问题；玩家区/战场区现在使用面板内安全滚动，避免 UI 重叠。

### Batch 4：对战桌面与卡牌详情

状态：完成。

交付：

- 顶部状态栏、双方区域、两处战场、结算链、prompt/action 面板、事件日志、卡牌详情弹窗。
- 所有公开卡牌紧凑卡面展示名称、编号、费用、符能费用、战力、类型、短状态、控制方/所属方。
- 点击卡牌打开详情抽屉，展示服务端提供的名称、编号、费用、符能费用、战力、类型、关键词、效果、状态、控制方/所属方、对象 ID 与位置。
- 对手手牌和未公开对象只展示卡背与隐藏占位；详情抽屉明确提示“隐藏信息”，不读取或推断卡名、费用、类型或规则文本。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- Browser Use smoke：在已进入 `MAIN` 的双人房间中点击我方手牌《熔浆巨龙》，详情抽屉展示公开卡规则、费用、对象 ID、位置、所属方/控制方；点击对手手牌 `hidden-0`，详情抽屉只显示隐藏占位和隐藏提示，dialog 文本未包含真实卡名或编号。

### Batch 5：卡牌驱动操作

状态：完成第二十五片。

交付：

- 前端发现 `TAP_RUNE` 被 prompt 暴露但协议/规则引擎不可执行，已按核心规则和自查文档 8.2 补齐基础符文“横置获得 1 法力”服务端路径。
- `TAP_RUNE` 新增协议命令、JSON mapper、规则解析、`RUNE_TAPPED` / `MANA_GAINED` 事件、runePool snapshot 更新、符文横置状态更新。
- `ActionPromptBuilder` 现在只把基地中未横置、正面、受控的符文作为 `TAP_RUNE.sources`；横置后该来源会从后续候选中移除。
- `RECYCLE_RUNE` 新增协议命令、JSON mapper、规则解析、`RUNE_RECYCLED` / `POWER_GAINED` 事件、runePool typed power snapshot 更新、符文牌堆区域移动与对象位置同步。
- `ActionPromptBuilder` 现在只把基地中正面、受控、带 `COLOR:*` 特性的符文作为 `RECYCLE_RUNE.sources`；回收后该来源离开基地并进入符文牌堆底部，前端不自行判断符文特性或资源获得。
- 卡牌详情抽屉开始读取当前 `ActionPrompt.candidates`，并在具体卡牌上展示服务端给出的来源型候选；基础符文可从详情抽屉直接提交“横置符文”。
- 卡牌详情抽屉和全局行动面板接入“回收符文”：单来源时可直接提交，多来源时只允许从服务端给出的具体来源卡牌详情中提交。
- 全局行动面板只在服务端候选可直接提交时启用按钮；`PLAY_CARD` 等仍需目标/模式/费用/目的地选择的动作会显示“需选择”并保持禁用，避免前端提交不完整命令。
- `END_TURN` 也补齐服务端命令侧窗口 guard：即使客户端绕过前端直接提交，Core 也只允许当前行动玩家在 `MAIN` / `NEUTRAL_OPEN` 且结算链为空时结束回合。前端继续只按服务端 prompt 展示“结束回合”，不自行判断或推进回合。
- `TURN_START` 自动流程也补齐服务端命令侧玩家 guard：只有当前 `TurnPlayerId` 能触发服务端回合开始自动处理；非回合玩家绕过前端提交占位命令不会召符文、抽牌或推进 tick。前端仍只展示服务端 snapshot/prompt，不本地推进回合开始。
- 已结束比赛也补齐服务端全局命令 guard：`Status != IN_PROGRESS` 后，Core 会统一拒绝后续游戏命令且保留胜者、tick 和区域不变；`MULLIGAN` 这条早于普通命令分发的特殊路径也已纳入同一状态 guard。前端只展示结束状态，不允许通过客户端按钮或重连后残留操作继续推进比赛或起手调度。
- `PLAY_CARD` prompt 现在增加每来源 `sourceRequirements` 元数据，由服务端按具体手牌暴露最小/最大目标数、目标范围中文标签、逐目标槽候选、可选模式、可选费用、目的地候选和当前是否可由前端组合提交。
- `PLAY_CARD` 来源进一步收紧：需要目标的牌必须有服务端过滤后的必需目标槽候选才会作为可执行来源暴露，避免前端出现“可点但必然被服务端拒绝”的假入口。
- 卡牌详情抽屉新增出牌组合器：从服务端 `sourceRequirements` 渲染模式、目标槽、目的地、可选费用和确认按钮；确认命令只提交这些服务端候选组合，不从卡面文本或客户端规则推断。
- 当前已通过真实 UI 打出无目标单位牌并走完优先权结算；需要复杂额外费用牺牲/返回目标的牌会按服务端 `composable=false` 明确禁用，后续由服务端继续补对应费用选择模型。
- `MOVE_UNIT` prompt 从泛化来源/目的地升级为每来源 `sourceRequirements` 元数据。服务端现在按具体单位公开来源、起点区域、移动模式、目的地候选、必需/可选额外费用和可组合状态。
- `MOVE_UNIT` 来源进一步收紧：只暴露正面、受控、非战斗中的单位；基地单位公开“基地 -> 战场”，战场单位在未被静态效果禁止时公开“战场 -> 基地”，有游走权限且能从权威位置索引精确定位时才公开游走目的地与必需 `ROAM` 可选费用。
- 卡牌详情抽屉新增移动单位组合器：只读取服务端 `sourceRequirements` 渲染移动模式、目的地和费用确认；确认命令只提交服务端提供的 `origin`、`destination`、`optionalCosts`，不从卡面文本、关键词或客户端位置自行裁决。
- 当前已通过真实 UI 将已结算到基地的《军团后卫》移动到战场；事件日志出现 `UNIT_MOVED_TO_BATTLEFIELD`，后续移动候选仍由服务端 prompt 决定。
- `ASSEMBLE_EQUIPMENT` prompt 从泛化来源/目标升级为每来源 `sourceRequirements` 元数据。服务端现在只对已实现代表路径的未贴附《长剑》公开装配来源、单位目标候选、必需 `ASSEMBLE_RED` 费用、红色符能费用和 `composable` 状态。
- `ASSEMBLE_EQUIPMENT` 来源继续收紧：只有基地中正面、受控、未贴附、具备长剑/武装/灵便身份、存在合法单位目标且 `powerByTrait.red >= 1` 时才暴露；泛化符能不再被当作红色装配费用，提交侧也会以 `INSUFFICIENT_COST` 拒绝。
- 卡牌详情抽屉新增装备装配组合器：只读取服务端 `sourceRequirements` 渲染目标和费用，确认命令只提交服务端给出的 `sourceObjectId`、`targetObjectId`、`optionalCosts`，不从卡面文本或关键词自行判断。
- Development `equipment` seed 已补齐手牌长剑与目标单位的 cardNo、owner/controller 和红色符能池，避免 smoke 场景出现 prompt 来源可见但 snapshot 缺对象详情的断裂。
- 当前已通过真实 UI 将《长剑》从手牌打出、P1/P2 过优先权结算到基地，再从详情抽屉按服务端候选装配到《大力仙灵》；事件日志出现 `EQUIPMENT_PLAYED_TO_BASE`、`COST_PAID`、`EQUIPMENT_ATTACHED`，最终 snapshot 显示长剑 `attachedToObjectId = P1-UNIT-ASSEMBLE-TARGET`。
- `ACTIVATE_ABILITY` prompt 从泛化来源/目标升级为每来源 `sourceRequirements` 元数据。服务端现在只对已实现代表路径的 Vi、Xerath 和蜕变花园授予单位能力公开来源、能力、目标槽、费用、横置来源、立即结算与 `composable` 状态。
- `ACTIVATE_ABILITY` 来源继续收紧：服务端会按资源、来源是否横置、是否需要战场来源、目标槽是否存在合法目标，以及敌方 Spellshield 加税是否可支付过滤候选；没有服务端合法组合时前端不会出现可提交入口。
- 卡牌详情抽屉新增激活能力组合器：只读取服务端 `sourceRequirements` 渲染能力、目标槽、费用和确认按钮，确认命令只提交服务端提供的 `sourceObjectId`、`abilityId`、`targetObjectIds`、`optionalCosts`，不从卡面文本或关键词自行判断。
- 对战桌面补齐服务端场上对象可见性：战场占据/待命对象和玩家 `zones.battlefields` 场上对象都可作为卡牌点击，避免服务端 prompt 指向的对象在 UI 中不可操作。
- 卡牌类型展示改为优先读取服务端对象标签，再回退图鉴类别，避免开发场景中作为单位在场的对象因 catalog 分类不同被误显示成法术。
- 当前已通过真实 UI 点击蜕变花园授予能力来源，详情抽屉展示 `ACTIVATE_ABILITY` 组合器、费用 0、目标 0、横置来源和立即结算；确认后事件日志出现 `ABILITY_ACTIVATED`、`UNIT_EXHAUSTED`、`BATTLEFIELD_TRIGGER_RESOLVED`、`EXPERIENCE_GAINED`，最终 snapshot 显示单位横置且 P1 经验变为 1。
- `LEGEND_ACT` prompt 从泛化模式/费用升级为每来源 `sourceRequirements` 元数据。服务端现在按具体传奇或授予来源公开能力、目标槽、必需费用、时点、横置来源、立即结算和 `composable` 状态。
- `LEGEND_ACT` 来源继续收紧：服务端会按当前时点、来源是否横置、资源/经验是否可支付、卡牌前置条件、目标槽候选和代表路径支持状态过滤候选；依赖第一目标再决定第二目标的武装类传奇行动会以 `composable=false` 明确禁用前端提交。
- 卡牌详情抽屉新增传奇行动组合器：只读取服务端 `sourceRequirements` 渲染能力、目标槽、必需费用和确认按钮，确认命令只提交服务端提供的 `sourceObjectId`、`abilityId`、`targetObjectIds`、`optionalCosts`，不从卡面文本、关键词或客户端资源自行判断。
- 当前已通过真实 UI 点击 Poppy 传奇《圣锤之毅》，详情抽屉展示 `LEGEND_ACT` 组合器、经验费用 3、目标 0、横置来源和立即结算；确认后事件日志出现 `LEGEND_ABILITY_ACTIVATED`、`EXPERIENCE_SPENT`、`LEGEND_EXHAUSTED`、`CARD_DRAWN`，最终 snapshot 显示 P1 经验变为 0、传奇横置、手牌 +1。
- `DECLARE_BATTLE` prompt 从泛化来源/目标/战场升级为每攻击者 `sourceRequirements` 元数据。服务端现在按当前时点、攻击者是否正面/受控/未参战、防守者是否合法、战场候选和必需 `COMBAT_ASSIGNMENT` 费用过滤候选。
- 卡牌详情抽屉新增战斗声明组合器：只读取服务端 `sourceRequirements` 渲染攻击者、战场、防守槽、必需费用和确认按钮，确认命令只提交服务端提供的 `battlefieldId`、`attackerObjectIds`、`defenderObjectIds`、`optionalCosts`，不从卡面文本、关键词或客户端战场状态自行裁决。
- `DECLARE_BATTLE.sourceRequirements` 已与服务端代表性 1-2 防守者结算能力对齐：当服务端检测到至少两个合法防守者且其中包含壁垒/后排伤害分配关键词时，会公开第二个可选防守槽；第二槽候选只来自服务端给出的壁垒/后排防守者，前端不自行推断多防守者合法性。
- 同优先级防守者顺序已补代表性证据：服务端 metadata 公开 `samePriorityAssignmentPolicy`，两个同为 `壁垒` 的防守者会在相同优先级内保留玩家通过服务端候选提交的对象顺序；前端仍只渲染 `targetChoicesByIndex` 槽位，不自行判断壁垒优先级或伤害分配合法性。
- Development `battle-declare` seed 已补齐攻防单位的 cardNo、owner/controller 和单位标签，避免 smoke 场景出现 prompt 来源可见但 snapshot 仍显示卡背的断裂。
- 当前已通过真实 UI 点击己方战场《大力仙灵》，详情抽屉展示 `DECLARE_BATTLE` 组合器、战场“己方主战场”、防守者 `P2-BATTLE-DEFENDER-001` 和必需费用“战斗分配”；确认后事件日志出现 `BATTLE_DECLARED`、两条 `DAMAGE_APPLIED`、`UNIT_DESTROYED`，后续 prompt 收敛为 `END_TURN`。
- `SPELL_DUEL_OPEN` 焦点窗口的 `PLAY_CARD` 暴露已收紧到同一套服务端 `sourceRequirements`：只有存在可支付、合法时点、目标槽可组合的服务端来源时才显示 `PLAY_CARD`，避免前端出现空的响应窗口操作。
- X 符能 `PLAY_CARD` 可选费用继续收紧：服务端现在按当前可支付上限公开 `SPEND_POWER:<amount>` 与 typed `SPEND_POWER:<trait>:<amount>` 候选，例如有 2 点红色符能时公开 `SPEND_POWER:2` 与 `SPEND_POWER:red:2`，不公开超过上限的金额。前端详情抽屉把所有 `SPEND_POWER:*` 费用视为同一组单选，避免用户同时勾选多个金额并组合出服务端会拒绝的费用列表。
- 新增 Development-only `typed-power-payment` seed，用于 smoke《弹幕时间》按服务端候选选择 2 点红色符能支付，前端只提交服务端给出的 `SPEND_POWER:red:2`。
- X 符能支付资源组合继续收紧：服务端 `PLAY_CARD.sourceRequirements` 新增 `availablePower`、`availablePowerByTrait`、`availablePowerWithPaymentResources`、`availablePowerByTraitWithPaymentResources`、`paymentResourceChoices` 和 `hasteReadyPowerCost`，用于表达“当前符能是否足够、回收符文后是否足够”。当前只有 1 点红色符能但基地有红色符文时，服务端会同时公开 `SPEND_POWER:red:2` 与 `RECYCLE_RUNE:<objectId>`；前端把支付资源从普通可选费用中分离，只有当所选 `SPEND_POWER:*` / 代表性 `HASTE_READY` 确实需要补足时才把 `RECYCLE_RUNE:*` 放进命令，避免提交服务端会拒绝的多余资源动作。
- 新增 Development-only `typed-power-payment-recycle` seed，用于 smoke《弹幕时间》选择 `支付 2 红色符能` 后必须再选择服务端给出的 `回收符文支付`，前端不自行推断可回收符文，也不构造未出现在 prompt 中的支付资源 token。
- 服务端继续补上过量回收兜底：`PLAY_CARD` 中已选择的每个 `RECYCLE_RUNE:*` 都必须对本次 power cost 必要。若当前 1 点红色符能加任意一张红色符文已经足以支付 `SPEND_POWER:red:2`，命令却同时回收两张红色符文，服务端会拒绝并保持 snapshot 不变。前端仍不自行裁决“最少需要几张”，后续应由统一 PaymentEngine 暴露逐资源需求量。
- 支付资源 UI 闭环继续收紧：服务端 `PLAY_CARD.sourceRequirements` 新增 `paymentResourcePowerByChoice`，逐个 `RECYCLE_RUNE:*` token 暴露 trait 与 power 贡献；前端只按该服务端元数据计算当前所选 `SPEND_POWER:*` / 代表性 `HASTE_READY` 还缺多少资源。未补足时确认禁用，刚好补足后额外资源禁用，trait 不匹配或缺少贡献元数据的资源不会被当作可支付选择；前端仍不自行读取卡面或对象状态裁决资源能力。
- 新增 Development-only `typed-power-payment-over-recycle` seed，用于 smoke“当前 1 红 + 两张可回收红符文 + 只需支付 2 红”的 UI 场景，验证前端不会允许用户把两张符文都夹带进同一次 `PLAY_CARD`。
- 新增 Development-only `typed-power-payment-double-recycle` seed，用于 smoke“当前没有红色符能 + 两张可回收红符文 + 需要支付 2 红”的 UI 场景，验证前端不是硬性限制只能选一张支付资源，而是按服务端贡献元数据允许两张都选中后再提交。
- 新增 Development-only `typed-power-payment-mixed-recycle` seed，用于 smoke“当前 1 红 + red/blue 两张可回收符文 + 需要支付 2 红”的 UI 场景。前端选择 `SPEND_POWER:red:2` 后，blue 支付资源因 trait 不匹配禁用，red 支付资源可选并能提交；前端不从对象 tag 自行推断颜色，只消费服务端 `paymentResourcePowerByChoice`。
- 新增 Development-only `typed-power-payment-generic-mixed-recycle` seed 入口，用于 smoke“当前 1 泛化可用符能来自 red pool + red/blue 两张可回收符文 + 需要支付通用 2 符能”的 UI 场景。前端选择 `SPEND_POWER:2` 后，red 与 blue 支付资源都可作为服务端候选补足 1 点缺口；选择任一资源后另一资源禁用，避免过量回收。服务端仍通过 `paymentResourcePowerByChoice` 和权威支付计划决定最终扣费，前端不读取符文 tag 自行裁决。
- 新增 Development-only `haste-payment-recycle` seed，用于 smoke“当前 5 法力、0 符能、手牌《希维尔》、基地有可回收紫色符文”的急速支付场景。前端选择 `HASTE_READY` 后必须再选择服务端给出的 `RECYCLE_RUNE:*` 支付资源，确认按钮才会启用；结算后《希维尔》是否以活跃状态进入基地只读服务端事件和 snapshot。
- 新增 Development-only `haste-payment-colored-recycle` seed，用于 smoke“《希维尔》急速要求紫色符能、基地同时有蓝/紫两张可回收符文”的颜色精确急速支付场景。服务端 `PLAY_CARD.sourceRequirements` 公开 `hasteReadyPowerTrait = purple`；前端选择 `HASTE_READY` 后按该字段禁用 blue 支付资源，只允许 purple 资源补足费用，命令侧同样拒绝 blue + `HASTE_READY`。
- 已实现 `HASTE_READY` 的 34 张官方 Haste 卡现在都在服务端 registry/profile 中公开官方颜色 trait，所有成功类 Haste fixture 也改用对应 `powerByTrait`。前端没有新增本地规则判断；同一详情抽屉继续只依据服务端 `hasteReadyPowerTrait`、`paymentResourceChoices` 与 `paymentResourcePowerByChoice` 展示合法支付组合。
- 支付失败代表路径新增 hash 级状态不变断言：Echo 额外费用不足、Haste 错色、过量回收、单/多目标 Spellshield 税不足都会在拒绝后比对完整 authoritative `MatchState` hash。前端仍只展示服务端拒绝/错误与最终 snapshot，不承担失败回滚裁决。
- `PLAY_CARD.sourceRequirements` 新增 `legalTargetSelections`，当目标组合合法性依赖总目标战力上限或 Spellshield 目标税时，由服务端枚举可提交的目标组合。前端详情抽屉只按该列表启用“确认打出”，当前组合未被服务端列出时显示“当前目标组合不在服务端合法组合中。”，不读取卡面或目标 tag 自行裁决。
- 新增 Development-only `spellshield-multiple-tax` seed，用于 smoke“《妖异狐火》可同时指定 Spellshield 与 Spellshield2 两个单位并支付 3 点 Spellshield 加税，但不能指定 5 战力非法目标”的目标组合/加税场景。服务端 Hub 测试覆盖 `legalTargetSelections`、`COST_PAID.spellshieldTaxMana`、两个加税目标和最终墓地状态。
- `PLAY_CARD` 来源过滤继续收紧：当目标组合合法性依赖 Spellshield 目标税或总目标战力上限时，服务端现在要求至少存在一个可提交的 `legalTargetSelections` 组合。P1 只有基础 2 法力、手牌《焚烧》、唯一目标为敌方法盾单位时，服务端不再公开《焚烧》来源；前端行动面板只显示 disabled 的“打出卡牌（需选择）”，卡牌详情抽屉不会渲染“确认打出”，仍不自行计算目标税。
- 新增 Development-only `spellshield-tax-insufficient-prompt` seed，用于 smoke“卡牌和目标都公开可见，但 Spellshield 目标税使所有必需目标组合不可支付”的 UI 场景。服务端 Hub 测试覆盖 `PLAY_CARD.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 手牌仍有《焚烧》。
- `PLAY_CARD` 来源过滤继续补齐无行为定义边界：若手牌对象缺少服务端公开 `cardNo`，服务端不再为了兼容旧 fixture 把它公开为 `PLAY_CARD.sources`。前端仍可按 snapshot 展示该对象或隐藏占位，但行动面板只显示 disabled 的“打出卡牌（需选择）”，卡牌详情不会渲染“确认打出”，避免前端对无权威卡号/行为定义的对象构造命令。
- 新增 Development-only `unknown-play-source-prompt` seed，用于 smoke“手牌对象存在但 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `PLAY_CARD.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 手牌仍包含 `P1-HAND-UNKNOWN-PLAY-SOURCE` 且该对象 `cardNo` 为空。
- `PLAY_CARD` 目标过滤继续补齐无权威身份边界：手牌来源、战场/基地/墓地目标 helper 现在只把 `cardNo` 非空的对象暴露给 prompt。若公开区域存在对象但服务端不知道其卡号，前端仍可按 snapshot 展示该对象或隐藏占位，但不会收到可点击的 `targets` / `sourceRequirements.targetChoicesByIndex`，也不会构造客户端自判目标命令。
- 新增 Development-only `unknown-play-target-prompt` seed，用于 smoke“手牌《海克斯射线》公开可见，但唯一战场单位目标 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `PLAY_CARD.enabled=false`、`sources=[]`、`targets=[]`、`sourceRequirements=[]`，P1 手牌仍包含 `P1-SPELL-UNKNOWN-PLAY-TARGET`，P2 自视角仍能看到未知目标对象且 `cardNo` 为空。
- `PLAY_CARD` 命令侧同步补齐无权威身份边界：Core 现在要求已知来源对象的 `cardNo` 非空且与命令提交卡号一致；带 owner/controller 的公开目标对象也必须有非空 `cardNo`。这关闭了客户端绕过 prompt 直接提交未知来源、伪造来源卡号或把未知公开目标放进 `targetObjectIds` 的路径；前端仍只展示和提交服务端 `sourceRequirements` / `targets` 候选。
- 本批没有前端 UI 代码变更，因此未新增 browser 进程；沿用前序 `unknown-play-source-prompt` 与 `unknown-play-target-prompt` 的后台 headless Chrome/CDP UI smoke 证据。新增命令侧测试 3 个，相关未知来源/目标回归 7/7、后端 full test 2999/2999、GameHub 118/118、前端 build 均已通过。当前完成度仍约 **99%**，结论仍 **NOT READY**，进入 completion audit 前继续补 command-side hidden-info hardening 与少量收口修复。
- `ASSEMBLE_EQUIPMENT` 来源过滤也补齐无行为定义边界：代表性装配路径只支持服务端明确识别的《长剑》`SFD·022/221`，基地装备对象即使带有 `EquipmentCard` / `武装` / `灵便` 标签，只要缺少 `cardNo` 或不是长剑，就不会出现在 `ASSEMBLE_EQUIPMENT.sources` 或 `sourceRequirements`。前端仍展示 snapshot 中的装备对象，但行动面板只显示 disabled 的“装配装备（需选择）”，卡牌详情不会渲染“确认装配”。
- 新增 Development-only `unknown-assemble-source-prompt` seed，用于 smoke“基地装备对象存在但 `cardNo = null`，同时有合法单位目标和红色符能”的 UI 场景。服务端 Hub 测试覆盖 `ASSEMBLE_EQUIPMENT.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 基地仍包含 `P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE` 且该对象 `cardNo` 为空。
- `ASSEMBLE_EQUIPMENT` 目标过滤也补齐无权威身份边界：代表性长剑装配目标必须是服务端已知 `cardNo` 的公开单位。若基地单位对象缺少 `cardNo`，前端仍可按 snapshot 看到该对象或隐藏占位，但不会收到可点击的 `targets` / `sourceRequirements.targetChoicesByIndex`，也不会构造客户端自判装配命令。
- 新增 Development-only `unknown-assemble-target-prompt` seed，用于 smoke“基地《长剑》公开可见且红色符能足够，但唯一单位目标 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `ASSEMBLE_EQUIPMENT.enabled=false`、`sources=[]`、`targets=[]`、`sourceRequirements=[]`，P1 基地仍包含已知长剑和未知目标单位。
- `LEGEND_ACT` 来源过滤补齐无行为定义边界：战场授予的代表性传奇贴附能力仍要求 legend zone 来源有已知 `cardNo`。即使 P1 控制《魄罗熔炉》且有合法单位/武装目标，缺少 `cardNo` 的传奇对象也不会出现在 `LEGEND_ACT.sources` 或 `sourceRequirements`；Core 手写命令同样拒绝该未知传奇来源。
- 新增 Development-only `unknown-legend-action-source-prompt` seed，用于 smoke“legend zone 对象存在但 `cardNo = null`，同时控制《魄罗熔炉》并有合法单位/武装目标”的 UI 场景。服务端 Hub 测试覆盖 `LEGEND_ACT.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 legend zone 仍包含 `P1-LEGEND-UNKNOWN-ACTION-SOURCE` 且该对象 `cardNo` 为空。
- `LEGEND_ACT` 目标过滤补齐无权威身份边界：友方单位目标、pending 友方单位目标和武装第二目标都必须是服务端已知 `cardNo` 的公开对象。若目标对象缺少 `cardNo`，前端仍可按 snapshot 展示该对象或隐藏占位，但不会收到可点击的 `targets` / `sourceRequirements.targetChoicesByIndex`，也不会构造客户端自判传奇行动命令。
- 新增 Development-only `unknown-legend-action-target-prompt` seed，用于 smoke“亚索传奇公开可见且法力足够，但唯一友方单位目标 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `LEGEND_ACT.enabled=false`、`sources=[]`、`targets=[]`、`sourceRequirements=[]`，P1 战场仍包含未知目标单位。
- `ACTIVATE_ABILITY` 来源过滤也补齐战场授予能力的无行为定义边界：《蜕变花园》授予单位“横置：获得 1 经验”仍要求来源战场单位有已知 `cardNo`。缺少 `cardNo` 的战场单位不会出现在 `ACTIVATE_ABILITY.sources` 或 `sourceRequirements`；Core 手写命令同样拒绝该未知能力来源。
- 新增 Development-only `unknown-activate-ability-source-prompt` seed，用于 smoke“战场单位对象存在但 `cardNo = null`，同时控制《蜕变花园》”的 UI 场景。服务端 Hub 测试覆盖 `ACTIVATE_ABILITY.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 战场仍包含 `P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE` 且该对象 `cardNo` 为空。
- `ACTIVATE_ABILITY` 目标过滤也补齐无权威身份边界：泽拉斯代表性目标技能的目标必须是服务端已知 `cardNo` 的公开场上单位。若敌方场上单位对象缺少 `cardNo`，前端仍可按 snapshot 看到隐藏/未知对象，但不会收到可点击的 `targets` / `sourceRequirements.targetChoicesByIndex`，Core 手写命令也会以 `InvalidTarget` 拒绝。
- 新增 Development-only `unknown-activate-ability-target-prompt` seed，用于 smoke“战场《泽拉斯》公开可见且红色符能足够，但敌方单位目标 `cardNo = null`”的 UI 场景。由于泽拉斯自身也是合法目标，该场景验证候选只保留已知泽拉斯自身，未知敌方目标不进入目标槽；前端详情抽屉只显示服务端候选，不构造客户端自判目标。
- `MOVE_UNIT` 来源过滤补齐无行为定义边界：基地/战场单位必须公开已知 `cardNo` 才能作为移动来源。缺少 `cardNo` 的场上单位不会出现在 `MOVE_UNIT.sources` 或 `sourceRequirements`；Core 手写命令同样拒绝该未知移动来源，前端只展示隐藏信息和 disabled 候选。
- 新增 Development-only `unknown-move-unit-source-prompt` seed，用于 smoke“基地单位对象存在但 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `MOVE_UNIT.enabled=false`、`sources=[]`、`sourceRequirements=[]`、P1 基地仍包含 `P1-UNIT-UNKNOWN-MOVE-SOURCE` 且该对象 `cardNo` 为空。
- `MOVE_UNIT` 精确战场/游走目的地过滤也补齐无权威身份边界：战场牌对象必须有服务端已知 `cardNo` 才能作为精确战场 origin/destination 暴露或被 Core 手写命令接受。默认 `BATTLEFIELD:{playerId}-MAIN` 这类抽象主战场目的地仍由服务端候选保留；前端只渲染 prompt 给出的目的地，不从战场牌 tag 自行推断可游走战场。
- 新增 Development-only `unknown-move-unit-battlefield-prompt` seed，用于 smoke“已知游走单位所在战场旁还有一个 `cardNo = null` 的战场牌对象”的 UI 场景。服务端 Hub 测试覆盖 `MOVE_UNIT.enabled=true`，但 `ROAM.destinationChoices` 只包含 `BATTLEFIELD:P1-MAIN`；P1 snapshot 仍保留未知战场对象且 `cardNo` 为空，详情抽屉的游走目的地不出现未知对象。
- `TAP_RUNE` / `RECYCLE_RUNE` 来源过滤补齐无行为定义边界：基地符文必须公开已知 `cardNo` 才能作为横置/回收来源。缺少 `cardNo` 的符文不会出现在两个资源动作的 `sources` 中；Core 手写命令同样拒绝该未知符文来源，前端只展示隐藏信息和 disabled 候选。
- 新增 Development-only `unknown-rune-source-prompt` seed，用于 smoke“基地符文对象存在但 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `TAP_RUNE.enabled=false`、`RECYCLE_RUNE.enabled=false`、两个候选 `sources=[]`、P1 基地仍包含 `P1-RUNE-UNKNOWN-SOURCE` 且该对象 `cardNo` 为空。
- 新增 Development-only `unknown-declare-battle-source-prompt` seed，用于 smoke“战场攻防单位对象存在但 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `DECLARE_BATTLE.enabled=false`、`sources=[]`、`targets=[]`、`sourceRequirements=[]`，P1 战场仍包含 `P1-BATTLE-UNKNOWN-ATTACKER` 且该对象 `cardNo` 为空，同时不向 P1 泄漏 P2 隐藏防守者对象。
- `DECLARE_BATTLE` 战场目的地过滤补齐无权威身份边界：公开战场牌目的地和 active `START_BATTLE` 任务战场必须有服务端已知 `cardNo` 才能进入 `destinations` / `sourceRequirements[].battlefieldChoices`；Core 手写命令同样拒绝无 `cardNo` 的战场牌目的地。默认 `BATTLEFIELD:{playerId}-MAIN` 仍可作为服务端支持的主战场目的地，前端只渲染服务端给出的战场候选。
- 新增 Development-only `unknown-declare-battle-battlefield-prompt` seed，用于 smoke“战场牌对象存在但 `cardNo = null`，同时有已知攻击者/防守者”的 UI 场景。服务端 Hub 测试覆盖 `DECLARE_BATTLE.enabled=true` 但 `destinations` 与 `sourceRequirements[].battlefieldChoices` 只包含 `BATTLEFIELD:P1-MAIN`；P1 snapshot 仍保留未知战场对象且 `cardNo` 为空，详情抽屉的战场选择不出现未知对象。
- `HIDE_CARD` / `REVEAL_CARD` 的未知身份边界继续补齐：prompt 层已经不会公开 `cardNo = null` 的待命来源，本批进一步让 Core 手写命令拒绝客户端自行上报 `cardNo` 来执行未知来源。合法待命布置/翻开 fixture 也补成服务端权威状态知道卡号，未知来源只保留在专门的拒绝测试/seed 中。
- 新增 Development-only `unknown-hide-card-source-prompt` 与 `unknown-reveal-card-source-prompt` seed，用于 smoke“手牌待命对象/基地面朝下待命对象存在但 `cardNo = null`”的 UI 场景。服务端 Hub 测试覆盖 `HIDE_CARD.enabled=false`、`REVEAL_CARD.enabled=false`、两个候选 `sources=[]`、`sourceRequirements=[]`，P1 snapshot 仍保留对应未知对象且 `cardNo` 为空。
- `ASSEMBLE_EQUIPMENT` 支付窗口新增服务端候选支付资源动作：当前红色符能不足但基地有可回收红色基础符文时，服务端在 `sourceRequirements.paymentResourceChoices` 暴露 `RECYCLE_RUNE:<objectId>`，同时给出 `paymentResourcePowerByChoice` 与加总后的 `availablePowerByTraitWithPaymentResources`。前端详情抽屉新增“支付资源”选择组，未选择服务端候选时“确认装配”禁用，选择后才提交 `ASSEMBLE_RED` 与该 `RECYCLE_RUNE:*` token，不读取符文 tag 自行裁决。
- 新增 Development-only `assemble-payment-recycle` seed，用于 smoke“0 红色符能 + 基地《长剑》 + 合法单位目标 + 可回收红色基础符文”的装配支付场景。服务端 Core/Hub 测试覆盖回收符文进入符文牌堆、`paymentWindow = ASSEMBLE_EQUIPMENT`、红色装配费用扣空、长剑贴附目标和 reload/reconnect。
- 战场 Echo 减免现在进入 `PLAY_CARD.sourceRequirements.optionalCostChoices` 的服务端候选口径。`battlefield-static-echo-cost-reduction` seed 中 P1 只有 3 法力，基础费用 2、Echo 原费用 2 且《玛莱尖塔》减免 1；前端详情抽屉会看到“回响：额外支付 1 法力”，并只提交服务端给出的 `ECHO` token。
- 战场据守授予下一个法术 Echo 也进入同一服务端候选口径。新增 `battlefield-held-next-spell-echo-prompt` seed，把 P2 已获得 `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2`、手牌《台前作秀》、4 法力且轮到 P2 的场景直接暴露给 UI；前端详情抽屉会看到“回响：额外支付 2 法力”，提交时仍只发送服务端给出的 `ECHO` token。
- 战场装备减费现在进入 `PLAY_CARD.sourceRequirements.minimumManaCost` 的服务端候选口径，并通过 `battlefieldEquipmentCostReductionMana` 告知前端展示减费。`battlefield-static-equipment-cost-reduction` seed 中 P1 只有 1 法力、手牌《长剑》基础费用 2 且控制《奥恩的锻炉》；前端详情抽屉会看到“费用 1 / 战场减费 -1”，并只提交服务端给出的《长剑》出牌候选。
- 战场据守非衍生物单位加费现在也进入 `PLAY_CARD.sourceRequirements.minimumManaCost` 的服务端候选口径，并通过 `battlefieldHeldUnitCostIncreaseMana` 告知前端展示加费。`battlefield-held-unit-cost-increase` seed 中 P1 只有 4 法力、手牌《忠实的工坊主》基础费用 3 且受到据守加费；前端详情抽屉会看到“费用 4 / 战场加费 +1”，并只提交服务端给出的出牌候选。
- `PLAY_CARD.destinationChoices` 现在按服务端同源战场静态效果过滤单位进场目的地。`battlefield-static-prevent-play-units` seed 中伏击《阴森药剂师》只能打到战场，但该战场禁止单位进场，因此服务端不再向 P1 prompt 暴露该 `PLAY_CARD` 来源；前端行动面板和卡牌详情抽屉自然不会出现可提交入口。
- `HIDE_CARD` 现在从泛化动作升级为每来源 `sourceRequirements`。服务端只在待命手牌存在可支付费用时暴露来源，并按当前权威状态公开 `STANDBY_A`、`STANDBY_TEEMO_MANA`、`STANDBY_FREE` 和 `STANDBY` / 《班德尔树》目的地候选；无费用或无授权时前端不会看到可提交的待命布置入口。
- 卡牌详情抽屉新增“布置待命”组合器：只读取服务端 `HIDE_CARD.metadata.sourceRequirements` 渲染目的地和待命费用，确认命令只提交服务端候选的 `sourceObjectId`、`cardNo`、`destination`、`optionalCosts`，不从卡面文本、玩家法力或传奇身份自行裁决。
- `REVEAL_CARD` 现在也进入每来源 `sourceRequirements`。服务端只在普通开环或持有结算链优先权的闭环窗口公开自己基地中的面朝下待命对象；普通翻开目的地限定 `BASE`，反应打出目的地限定 `STACK`，费用限定 `STANDBY_REVEAL_0`。
- 卡牌详情抽屉新增“翻开待命”组合器：只读取服务端 `REVEAL_CARD.metadata.sourceRequirements` 渲染模式、目的地和翻开费用，确认命令只提交服务端给出的 `sourceObjectId`、`cardNo`、`mode`、`destination` 与 `optionalCosts`，不从卡面文本、对象状态或当前窗口自行判断。
- 普通结算链优先权窗口现在也会在服务端有反应牌来源时公开 `PLAY_CARD`。`ActionPromptBuilder.StackPriorityActions` 只按 `PlayCardSourceRequirements` 暴露可支付、时点合法且有服务端目标槽的反应牌；普通非反应手牌不会出现在该窗口。新增 `priority-reaction-counter` seed，用于真实 UI 验证 P2 从手牌打出《强买强卖》反制栈上《焚烧》。
- 卡牌详情抽屉继续复用既有 `PLAY_CARD` 组合器：反应牌窗口只读取服务端 `sourceRequirements` 渲染栈目标、模式和确认按钮，提交命令只包含服务端候选的 `sourceObjectId`、`cardNo`、`mode` 与 `targetObjectIds`。事件日志新增 `STACK_ITEM_COUNTERED` 中文“无效化法术”。
- Development `spell-duel` seed 补齐《海克斯射线》和目标单位的公开 cardNo、owner/controller 与标签；新增 `spell-duel-focus` seed，直接构造 P1 拥有迅捷带目标法术、P2 拥有合法战场单位、窗口为 `SPELL_DUEL_OPEN` 且焦点在 P1 的 smoke 场景。
- 现有卡牌详情 `PLAY_CARD` 组合器已能在法术对决焦点窗口读取服务端目标槽候选，选择 P2 战场单位并提交《海克斯射线》；确认命令只提交服务端给出的 `sourceObjectId`、`cardNo` 与 `targetObjectIds`。
- 当前已通过真实 UI 在 `SPELL_DUEL_OPEN` 打出《海克斯射线》：详情抽屉展示目标槽 `P2-UNIT-HEXTECH-RAY-001`，确认后事件日志出现 `CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`，后续 prompt 切到 `PASS_PRIORITY`；P2 让过优先权后服务端结算 `STACK_ITEM_RESOLVED`、`DAMAGE_APPLIED`、`UNIT_DESTROYED` 并回到 P2 `PASS_FOCUS`。
- 当前已通过后台 Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-typed-power-jvf0djpz`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `typed-power-payment`；P1 打开《弹幕时间》详情抽屉，先点 `支付 1 符能` 再点 `支付 2 红色符能` 并确认。事件日志显示“打出卡牌 / 支付费用 / 加入结算链”，authoritative snapshot 中 stack item `damageAmount = 2`，P1 `runePool.powerByTrait` 已无 red；reload/reconnect 后仍恢复该 stack snapshot。
- 当前已通过后台 Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-typed-power-recycle-1uj64kjn`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-recycle`；P1 打开《弹幕时间》详情抽屉，选择 `支付 2 红色符能` 时“确认打出”仍禁用，继续选择 `回收符文支付：P1-RUNE-RED-PARTIAL-PAYMENT-001` 后才启用。确认后事件日志显示“回收符文 / 支付费用 / 加入结算链”，authoritative snapshot 中 stack item `damageAmount = 2`，P1 基地为空、符文牌堆计数为 2、`runePool.powerByTrait` 已无 red；reload/reconnect 后仍恢复包含 `P1-SPELL-BULLET-TIME` 与 `damageAmount` 的 stack snapshot。
- 当前已通过后台 Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-over-recycle-ui-x3dj3adn`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-over-recycle`；P1 打开《弹幕时间》详情抽屉，选择 `支付 2 红色符能` 后确认仍禁用，支付资源组显示两张红色符文候选。选择第一张后确认启用，第二张立即禁用且点击不会加入命令。提交后事件日志显示“回收符文 / 支付费用 / 加入结算链”，authoritative snapshot 中 `damageAmount = 2`、只回收 `P1-RUNE-RED-EXTRA-PAYMENT-001`、`P1-RUNE-RED-PARTIAL-PAYMENT-001` 留在基地、`runeDeckCount = 2`、red power 扣空；reload/reconnect 后仍恢复该 stack snapshot。
- 当前已通过独立 Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，Computer Use 读取直启测试 Chrome 时返回 `cgWindowNotFound`，因此使用可见 Chrome 的 CDP 端口完成断言。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-double-recycle-ui-w3n10ikh`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-double-recycle`；P1 选择 `支付 2 红色符能` 后确认禁用，选择第一张支付资源后确认仍禁用且第二张仍可选，两张都选择后确认启用。提交后事件日志显示“回收符文 / 支付费用 / 加入结算链”，authoritative snapshot 中两张符文都离开基地、`runeDeckCount = 3`、stack `damageAmount = 2`；reload/reconnect 后仍恢复该 stack snapshot。
- 当前已通过独立 Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-mixed-recycle-ui-nieihg25`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-mixed-recycle`；P1 选择 `支付 2 红色符能` 后，blue 支付资源禁用且点击不生效，red 支付资源可选。选中 red 后确认启用并提交，事件日志显示“回收符文 / 支付费用 / 加入结算链”；authoritative snapshot 中 red 符文离开基地、blue 符文仍在基地、`runeDeckCount = 2`、stack `damageAmount = 2`；reload/reconnect 后仍恢复该 stack snapshot。
- 当前已通过独立 Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，Computer Use 控制独立 Chrome 时返回 `cgWindowNotFound`，因此使用同一独立 Chrome 的 CDP 端口完成断言。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-generic-mixed-recycle-ui-l9qe3xqc`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `typed-power-payment-generic-mixed-recycle`；P1 选择 `支付 2 符能` 后 red/blue 两张支付资源都可选且确认禁用，选择 blue 后 red 禁用、确认启用。提交后事件日志显示“回收符文 / 支付费用 / 加入结算链”；authoritative snapshot 中 blue 符文离开基地、red 符文仍在基地、`runeDeckCount = 2`、stack `damageAmount = 2`、red/blue typed power 均为空；reload/reconnect 后仍恢复该最终 stack snapshot。
- 当前已通过独立 Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-haste-payment-recycle-ui-k9ghd440`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `haste-payment-recycle`；P1 打开《希维尔》详情抽屉，选择“急速活跃：额外支付 1 法力 / 1 符能”后确认禁用，选择 `P1-RUNE-PURPLE-HASTE-PAYMENT-001` 支付资源后确认启用并提交。P1 通过 UI 让过优先权，后台 P2 让过后，authoritative snapshot 中《希维尔》在 P1 基地且未横置，紫色符文离开基地、`runeDeckCount = 2`、purple typed power 已花掉；reload/reconnect 后仍恢复该最终 snapshot。
- 当前已通过后台 Chrome/CDP 真实 UI smoke：本批工具上下文未提供可调用 Browser Use，且前序 Computer Use 获取独立 Chrome 窗口失败；为避免抢用户前台，使用后台 Chrome/CDP。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-haste-colored-nflnfbtp`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `haste-payment-colored-recycle`；P1 打开《希维尔》详情抽屉，选择“急速活跃：额外支付 1 法力 / 1 紫色符能”后，blue `翠意符文` 支付资源禁用且点击不会选中，purple `摧破符文` 可选。选中 purple 后确认启用并提交，事件日志显示“回收符文 / 获得符能 / 支付费用 / 加入结算链”；P1 通过 UI 让过优先权，后台 P2 让过后 authoritative snapshot 中《希维尔》在 P1 基地且未横置，blue 符文仍在基地、purple 符文进入符文牌堆、`runeDeckCount = 2`、stack 为空；reload/reconnect 后仍恢复该最终 snapshot。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；为避免抢用户前台，没有使用 Computer Use 操控前台 Chrome。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-spellshield-tax-wxp9cgbm`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `spellshield-multiple-tax`；P1 打开《妖异狐火》详情抽屉，选择 5 战力目标时页面显示“当前目标组合不在服务端合法组合中。”且确认禁用，改选 Spellshield 与 Spellshield2 两个服务端合法目标后确认启用并提交。事件日志显示“支付费用 / 加入结算链”；P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中两个法盾单位进入 P2 墓地、5 战力单位留在战场、P1《妖异狐火》进入 P1 墓地、P1 法力为 0、stack 为空；reload/reconnect 后仍恢复该最终 snapshot。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 仍未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-spellshield-insufficient-sbqmk5ci`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `spellshield-tax-insufficient-prompt`；页面能看到 P1 手牌《焚烧》和 P2 法盾单位《呸呸魄罗》，但行动面板“打出卡牌（需选择）”为 disabled，title 为“PLAY_CARD 当前没有服务端可执行候选”。打开《焚烧》详情抽屉不出现“确认打出”；authoritative prompt 中 `PLAY_CARD.enabled=false`、`sources=[]`、`sourceRequirements=[]`；reload/reconnect 后仍恢复同一禁用状态。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 仍未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-source-3a8idnup`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-play-source-prompt`；authoritative prompt 中 `PLAY_CARD.enabled=false`、`sources=[]`、`sourceRequirements=[]`，snapshot 中 P1 手牌仍包含 `P1-HAND-UNKNOWN-PLAY-SOURCE` 且对象 `cardNo = null`。页面能看到该未知手牌对象，行动面板“打出卡牌（需选择）”为 disabled，title 为“PLAY_CARD 当前没有服务端可执行候选”；打开详情只显示隐藏信息保护，不出现“确认打出”；reload/reconnect 后仍恢复同一禁用 prompt。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke 补测 `unknown-play-target-prompt`：P1 Web UI 连接，P2 后台 SignalR 入座并 seed；页面显示 `P1-SPELL-UNKNOWN-PLAY-TARGET` 与 `P2-UNIT-UNKNOWN-PLAY-TARGET`，全局“打出卡牌（需选择）”保持 disabled；打开《海克斯射线》详情只显示“服务端未提供可执行候选”，不出现“确认打出”；reload/reconnect 后仍恢复同一 disabled prompt，脚本退出码 0。结束后已清理 Chrome、API、Vite，并确认 5093/5175/9224 无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-standby-1778178260963`。P1 先以 SignalR 入座以保持 `battlefield-contest-stack` dev seed 的 seat 映射，P2 Web UI 设置 `serverUrl = http://127.0.0.1:5093`、`playerId = P2` 后连接；P2 通过前端点击“让过优先权”和“让过焦点”，P1 SignalR 提交 `DECLARE_BATTLE`。页面显示“战斗结束”“战场控制结算”“待命清理”；authoritative snapshot 确认 `P1-STANDBY-CONTEST-001` 已从 battlefield 移到 P1 graveyard，battlefield `standbyObjectIds = []`，`controllerId = P2`；reload/reconnect 后 UI 恢复 `控制：P2` 与 `0 张面朝下`。smoke 结束后已清理 Chrome、API、Vite，并确认 5093/5175/9224 无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 仍未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-assemble-mr8gvnej`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-assemble-source-prompt`；authoritative prompt 中 `ASSEMBLE_EQUIPMENT.enabled=false`、`sources=[]`、`sourceRequirements=[]`，snapshot 中 P1 基地仍包含 `P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE` 且对象 `cardNo = null`。页面能看到该未知装备对象，行动面板“装配装备（需选择）”为 disabled，title 为“ASSEMBLE_EQUIPMENT 当前没有服务端可执行候选”；打开详情只显示隐藏信息保护，不出现“确认装配”；reload/reconnect 后仍恢复同一禁用 prompt。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 仍未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-assemble-target-bm6vvl9p`。P1 Web UI 连接后，后台 SignalR 让 P2 和 P1 dev 入座并 seed `unknown-assemble-target-prompt`；authoritative prompt 中 `ASSEMBLE_EQUIPMENT.enabled=false`、sources/targets/sourceRequirements 均为空，snapshot 中 P1 基地仍包含已知《长剑》和 `P1-UNIT-UNKNOWN-ASSEMBLE-TARGET` 且目标对象 `cardNo = null`。页面行动面板“装配装备（需选择）”为 disabled；打开未知目标详情只显示隐藏信息保护，不出现“确认装配”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 5/5、装配宽回归 32/32、GameHubJoinTests 114/114、后端 full test 2984/2984 和前端 build 均已通过；smoke 后已清理后台 Chrome/CDP，9224 无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-legend-q4azhapy`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-legend-action-source-prompt`；authoritative prompt 中 `LEGEND_ACT.enabled=false`、sources 数量 0、sourceRequirements 为空，snapshot 中 P1 legend zone 仍包含 `P1-LEGEND-UNKNOWN-ACTION-SOURCE` 且对象 `cardNo = null`，同时控制《魄罗熔炉》战场和合法单位/武装目标。页面行动面板“传奇行动（需选择）”disabled，title 为“LEGEND_ACT 当前没有服务端可执行候选”；打开未知传奇详情只显示隐藏信息保护，不出现“确认传奇行动”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 5/5、后端 full test 2961/2961 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮已知无可用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-legend-target-zmziu3fw`。P1 Web UI 连接后，后台 SignalR 让 P2 和 P1 dev 入座并 seed `unknown-legend-action-target-prompt`；authoritative prompt 中 `LEGEND_ACT.enabled=false`、sources/targets/sourceRequirements 均为空，snapshot 中 P1 legend zone 仍包含已知亚索传奇，战场仍包含 `P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET` 且对象 `cardNo = null`。页面行动面板“传奇行动（需选择）”disabled；打开未知目标详情只显示隐藏信息保护，不出现“确认传奇行动”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标窄测 3/3、LegendAct 宽回归 36/36、GameHubJoinTests 115/115、后端 full test 2987/2987 和前端 build 均已通过；smoke 后已清理 API/Vite/headless Chrome，5093/5175/9224 均无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-activate-eb9aeq92`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-activate-ability-source-prompt`；authoritative prompt 中 `ACTIVATE_ABILITY.enabled=false`、sources 数量 0、sourceRequirements 为空，snapshot 中 P1 战场仍包含 `P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE` 且对象 `cardNo = null`，同时控制《蜕变花园》。页面行动面板“激活能力（需选择）”disabled，title 为“ACTIVATE_ABILITY 当前没有服务端可执行候选”；打开未知单位详情只显示隐藏信息保护，不出现“确认激活能力”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 5/5、后端 full test 2964/2964 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 in-app browser 控制入口；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-activate-target-movvkesi`。P1 Web UI 连接后，后台 SignalR 校验 authoritative prompt：`ACTIVATE_ABILITY.enabled=true`，但 sources/targets/targetChoicesByIndex 均只有 `P1-UNIT-XERATH-TARGET-FILTER`，未知敌方目标 `P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET.cardNo = null` 且不进入候选。页面行动面板显示“激活能力（需选择）”；打开泽拉斯详情有“确认激活”且不包含未知目标；打开未知目标详情只显示“隐藏信息”，不出现“确认激活”；reload/reconnect 后仍恢复同一 prompt。后端 build、目标窄测 8/8、ActivateAbility 宽回归 59/59、GameHubJoinTests 116/116、后端 full test 2990/2990 和前端 build 均已通过；smoke 后已清理 API/Vite/headless Chrome，5093/5175/9224 均无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-move-weqq9zpl`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-move-unit-source-prompt`；authoritative prompt 中 `MOVE_UNIT.enabled=false`、sources 数量 0、sourceRequirements 为空，snapshot 中 P1 基地仍包含 `P1-UNIT-UNKNOWN-MOVE-SOURCE` 且对象 `cardNo = null`。页面行动面板“移动单位（需选择）”disabled，title 为“MOVE_UNIT 当前没有服务端可执行候选”；打开未知单位详情只显示隐藏信息保护，不出现“确认移动”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 5/5、移动相关宽回归 43/43、后端 full test 2967/2967 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮仍无可连接 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-move-battlefield-movwrgls`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-move-unit-battlefield-prompt`；authoritative prompt 中 `MOVE_UNIT.enabled=true`，`destinations` 为 `BASE` 与 `BATTLEFIELD:P1-MAIN`，`ROAM.destinationChoices` 仅包含 `BATTLEFIELD:P1-MAIN`，未知战场对象 `P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION.cardNo = null` 且不进入候选。页面行动面板显示“移动单位（需选择）”；打开已知游走单位详情并切到“游走”，目的地只显示“己方主战场”，不包含未知战场对象；reload/reconnect 后仍恢复同一 prompt。后端 build、目标窄测 6/6、MoveUnit 宽回归 49/49、GameHubJoinTests 118/118、后端 full test 2996/2996 和前端 build 均已通过；smoke 后已清理 API/Vite/headless Chrome 和临时 profile，5093/5175/9224 均无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-rune-oegg6dgn`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-rune-source-prompt`；authoritative prompt 中 `TAP_RUNE.enabled=false`、`RECYCLE_RUNE.enabled=false`、两个候选 sources 均为空，snapshot 中 P1 基地仍包含 `P1-RUNE-UNKNOWN-SOURCE` 且对象 `cardNo = null`。页面行动面板“横置符文（需选择）”和“回收符文（需选择）”均 disabled；打开未知符文详情只显示隐藏信息保护，不出现横置/回收确认入口；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 6/6、后端 full test 2971/2971 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮无可调用 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-declare-movrd1ki`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `unknown-declare-battle-source-prompt`；authoritative prompt 中 `DECLARE_BATTLE.enabled=false`、sources/targets/sourceRequirements 均为空，snapshot 中 P1 战场仍包含 `P1-BATTLE-UNKNOWN-ATTACKER` 且对象 `cardNo = null`，并且未向 P1 泄漏 P2 隐藏防守者对象。页面行动面板“声明战斗（需选择）”disabled，title 为“DECLARE_BATTLE 当前没有服务端可执行候选”；打开未知攻击者详情只显示隐藏信息保护，不出现“确认声明战斗”；reload/reconnect 后仍恢复同一禁用 prompt。后端 build、目标回归 7/7、失败收口回归 8/8、GameHubJoinTests 110/110、后端 full test 2975/2975 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use 本轮仍无可连接 IAB backend；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-battlefield-movw7om1`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `unknown-declare-battle-battlefield-prompt`；authoritative prompt 中 `DECLARE_BATTLE.enabled=true`，但 `destinations` 与 `sourceRequirements[0].battlefieldChoices` 仅包含 `BATTLEFIELD:P1-MAIN`，未知战场对象 `P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION.cardNo = null` 且不进入候选。页面行动面板显示“声明战斗（需选择）”；打开已知攻击者详情，战场选择只显示“己方主战场”，不包含未知战场对象；reload/reconnect 后仍恢复同一 prompt。后端 build、目标窄测 7/7、DeclareBattle 宽回归 57/57、GameHubJoinTests 117/117、后端 full test 2993/2993 和前端 build 均已通过；smoke 后已清理 API/Vite/headless Chrome 和临时 profile，5093/5175/9224 均无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但 IAB backend 未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-unknown-standby-6rjmr6j8`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并依次 seed `unknown-hide-card-source-prompt` 与 `unknown-reveal-card-source-prompt`；authoritative prompt 中 `HIDE_CARD.enabled=false`、`REVEAL_CARD.enabled=false`、sources/sourceRequirements 均为空，snapshot 中 `P1-HAND-UNKNOWN-HIDE-SOURCE` 与 `P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE` 均保留且 `cardNo = null`。页面行动面板“布置待命（需选择）”“翻开待命（需选择）”均 disabled，详情抽屉只显示隐藏信息保护，不出现确认入口；reload/reconnect 后仍恢复 reveal disabled prompt。后端 build、目标回归 10/10、旧待命代表项回归 56/56、GameHubJoinTests 112/112、后端 full test 2979/2979 和前端 build 均已通过；smoke 后已清理 API/Vite/headless Chrome，5093/5175/9224 均无监听。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use IAB backend 本次不可用；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-assemble-payment-c6a6643h`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `assemble-payment-recycle`；P1 打开《长剑》详情抽屉，页面显示“支付资源 / 回收符文支付”，未选择资源时“确认装配”disabled，选择后 enabled 并提交。事件日志显示“回收符文 / 支付费用 / 装配长剑”；authoritative snapshot 中红色基础符文离开基地进入符文牌堆、`runeDeckCount = 2`、长剑贴附到 `P1-UNIT-ASSEMBLE-PAYMENT-TARGET`、red power 已扣空；reload/reconnect 后仍恢复该最终 snapshot。后端 build、目标回归 4/4、后端 full test 2958/2958 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但 IAB backend 仍不可用；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-echo-reduction-ldgywesc`。P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `battlefield-static-echo-cost-reduction`；P1 打开《台前作秀》详情抽屉，页面显示“回响：额外支付 1 法力”，选择后“确认打出”启用并提交。事件日志显示“支付费用 / 加入结算链”；P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 P1 抽到两张牌、`P1-SPELL-CENTER-STAGE` 入墓、P1 法力为 0、stack 为空；reload/reconnect 后仍恢复 `废牌堆 1` 与空结算链。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，继续避免抢用户前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-held-echo-prompt-3ak2nocn`。P2 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `battlefield-held-next-spell-echo-prompt`；P2 打开《台前作秀》详情抽屉，页面显示“回响：额外支付 2 法力”，选择后确认启用并提交。事件日志显示“支付费用 / 加入结算链”，后台事件含 `BATTLEFIELD_TRIGGER_RESOLVED`；P2 通过 UI 让过优先权、后台 P1 让过后 authoritative snapshot 中 P2 抽到两张牌、`P2-SPELL-CENTER-STAGE` 入墓、P2 法力为 0、stack 为空；reload/reconnect 后仍恢复 `废牌堆 1` 与空结算链。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，继续避免抢用户前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-equipment-reduction-qelxo0mo`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-static-equipment-cost-reduction`；P1 打开《长剑》详情抽屉，页面显示“费用 1 / 战场减费 -1”，点击“确认打出”后事件日志显示“支付费用”。P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 `P1-EQUIPMENT-LONG-SWORD` 位于 P1 基地、P1 法力为 0、stack 为空；reload/reconnect 后仍恢复该最终 snapshot。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，继续避免抢用户前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-held-unit-cost-fhth3xdd`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-held-unit-cost-increase`；P1 打开《忠实的工坊主》详情抽屉，页面显示“费用 4 / 战场加费 +1”，点击“确认打出”后事件日志显示“支付费用”。P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 `P1-UNIT-CRAFTSMAN` 与 `P1-UNIT-CRAFTSMAN-TOKEN-001` 位于 P1 基地、P1 法力为 0、stack 为空；reload/reconnect 后仍恢复该最终 snapshot。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：Browser Use IAB 仍不可用，继续避免抢用户前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-prevent-unit-play-rnfcoybs`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-static-prevent-play-units`；页面行动面板只显示“让过优先权”，没有全局“打出卡牌”；打开《阴森药剂师》详情抽屉也不渲染“确认打出”组合器。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-standby-hide-yy6gku5x`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-extra-standby`；P1 打开 Teemo 待命手牌详情，抽屉显示“待命费用”“支付 1 法力布置待命”和《班德尔树》目的地，点击“确认布置待命”后事件日志显示“班德尔树额外布置待命牌”；authoritative snapshot 中 `P1-STANDBY-BANDLE-TEEMO` 已从手牌移到《班德尔树》战场、`isFaceDown = true`、P1 法力为 0；reload/reconnect 后仍恢复同一最终 snapshot。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-reveal-card-3eexrdzw`。P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `standby-reaction`；P1 从面朝下待命牌详情抽屉看到“翻开费用”和“确认作为反应打出”，提交后事件日志显示“翻开待命 / 打出卡牌 / 支付费用 / 加入结算链”；authoritative snapshot 中 `P1-FACEDOWN-OGN-TEEMO-PURPLE` 已离开 P1 基地，结算链包含 `OGN·197/298` 待命反应项目；reload/reconnect 后仍恢复该栈状态。后端 build、后端 full test 2949/2949 和前端 build 均已通过。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；未使用 Computer Use 抢前台。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-priority-reaction-wotz00vd`。P2 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `priority-reaction-counter`；P2 行动面板显示服务端候选 `PLAY_CARD` 与 `PASS_PRIORITY`，打开《强买强卖》详情抽屉后选择栈上 `OGS·003/024` 目标并确认打出，事件日志显示“打出卡牌 / 支付费用 / 加入结算链”；P2 通过 UI 让过优先权、后台 P1 让过后，页面显示“无效化法术”和“当前无结算链项目”，authoritative snapshot 结算链为空；reload/reconnect 后仍恢复最终 snapshot。后端 build、后端 full test 2950/2950 和前端 build 均已通过。
- 争夺战场的服务端任务队列新增权威推进入口：状态变化后若留下争夺战场且无致命/0 战力清理优先项，服务端会广播 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 并进入 `SPELL_DUEL_OPEN`，前端只展示 resulting snapshot/prompt，不提供自定义“启动法术对决”按钮。
- 新增 Development-only `battlefield-contest-stack` seed，专门用于 smoke“优先权栈项目结算后留下争夺战场 -> 服务端自动启动法术对决”的链路。
- 当前已通过真实 UI/SignalR 混合 smoke：P2 浏览器视角看到 `BATTLEFIELD_TASKS`、争夺战场与阻塞队列；Node 让 P1 过优先权后，事件日志出现 `PRIORITY_PASSED`、`STACK_ITEM_RESOLVED`、`BATTLEFIELD_CONTESTED`、`SPELL_DUEL_STARTED`，状态切到 `SPELL_DUEL_OPEN`，P2 只获得服务端给出的 `PASS_FOCUS`。
- 争夺战场法术对决双方都让过后，服务端现在会把当前 active `START_SPELL_DUEL` 所属战场标记为 `COMPLETED`，`PendingTaskQueue` 切到 `BATTLE_TASKS`，active task 为 `START_BATTLE`；前端仍只展示服务端 snapshot/prompt，不提供本地“开始战斗/控制权”裁决按钮。
- 当前已通过真实 UI/SignalR 混合 smoke：P2 浏览器视角在 `SPELL_DUEL_OPEN` 点击服务端给出的“让过焦点”，Node 让 P1 继续 `PASS_FOCUS` 后事件日志出现 `SPELL_DUEL_CLOSED`，最终 snapshot 为 `NEUTRAL_OPEN`、规则队列 `BATTLE_TASKS`、active task `task:start-battle:P1-BATTLEFIELD-CONTEST-001`，prompt 为服务端 blocking `WAIT`。
- `START_BATTLE` active task 现在会由服务端 prompt 暴露当前行动玩家的 `DECLARE_BATTLE`，并把攻击者、防守者和目的战场候选限制在 active battlefield task 上；blocking queue 期间只有匹配该任务的 `DECLARE_BATTLE` 能穿过服务端命令 guard。
- 当前已通过真实 UI/SignalR 混合 smoke：P2 浏览器视角在 `BATTLE_TASKS` 点击己方战场单位打开详情抽屉，抽屉只展示服务端给出的战场 `P1-BATTLEFIELD-CONTEST-001` 与防守者 `P2-UNIT-CONTEST-001`；确认后事件日志出现 `BATTLE_DECLARED`、两条 `DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终 pending queue 回到 `IDLE`，prompt 回到 `END_TURN`。
- `DECLARE_BATTLE` 代表路径结算后，服务端现在会广播 `BATTLE_CLOSED`，清理幸存单位的攻防状态并关闭 `BattleState`；前端只展示该权威 snapshot，不再自行判断战斗是否结束。
- `DECLARE_BATTLE` 代表路径在双方同归于尽等无胜者结果下会广播 `BATTLE_NO_RESULT`，事件日志显示“战斗无结果”；前端只展示服务端 payload 与最终 snapshot，不从伤害数字自行判断胜者。
- 当战斗发生在真实战场对象上时，服务端会基于战后占据单位结算战场控制方并广播 `BATTLEFIELD_CONTROL_RESOLVED`；中央战场卡面和战场区控制提示都来自服务端 `controllerId`。
- 事件日志新增中文事件标签：本批关键路径中的 `BATTLE_CLOSED` 显示为“战斗结束”，`BATTLE_NO_RESULT` 显示为“战斗无结果”，`BATTLEFIELD_CONTROL_RESOLVED` 显示为“战场控制结算”；未知事件仍显示“服务端事件”，避免把未专门翻译的服务端 kind 当成前端裁决语义。
- 本批 smoke 发现 Vite 自动切到 `5175` 时 API CORS 仍只放行旧端口；已补服务端 Development-only loopback Vite 端口 fallback，并加测试，保证新窗口/新端口前端仍能连上 SignalR。Production 不放行该 fallback。
- 当前已通过新的 Chrome 窗口真实 UI/SignalR 混合 smoke：API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-battlefield-control-1`。P2 连接后，Node 加入 P1 并 seed `battlefield-contest-stack`，P1 过优先权、P2/P1 依次让过焦点后进入 `BATTLE_TASKS`；P2 点击己方《大力仙灵》打开详情抽屉，按服务端候选选择战场和防守者确认战斗；事件日志显示“战斗结束”“战场控制结算”，最终中央战场显示 `控制：P1`，pending queue 为 `IDLE`，刷新后 P2 重新连接可恢复同一 snapshot。
- 仍待后续批次补：当前只是 `START_BATTLE` 的 direct/minimal 代表路径；完整 control/held/conquer task 生命周期、官方级多参与者战斗 task、战斗响应窗口、独立战斗伤害分配 prompt、复杂可选费用/费用目标、完整法术对决/战斗 task lifecycle UI 仍未完成；部分双目标依赖型传奇行动仍需 PaymentEngine/目标依赖模型后再开放提交。

验收：

- `source ../../scripts/dev-env.sh && npm run build`：通过。
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`：通过，0 warning/0 error。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PlayCardPromptOffersSpendPowerAmountsByAvailableTraitPower|FullyQualifiedName~P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait|FullyQualifiedName~P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub"`：通过 5/5。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`：通过 93/93。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：通过 2899/2899。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：本批支付资源精确选择回归通过 2902/2902。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub"`：通过 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：本批双支付资源回归通过 2904/2904。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub"`：通过 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`：本批 mixed-trait 支付资源回归通过 2906/2906。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"`：通过 46/46。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineChangesBattlefieldControllerAfterBattle"`：通过 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen"`：通过 30/30。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ApiDevUiCorsPolicyTests"`：通过 3/3。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6SpellDuel"`：通过 2/2。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow"`：通过 1/1。
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"`：通过 29/29。
- Browser Use smoke：P1/P2 通过 UI 创建/加入房间、提交卡组、ready、双方在对战桌面执行 `MULLIGAN` 进入 `MAIN`；P2 点击基地符文《灵光符文》打开详情抽屉，详情中出现服务端候选“横置符文”，点击后事件日志出现 `RUNE_TAPPED` / `MANA_GAINED`，我方法力从 0 变为 1，符文状态变成“横置”；刷新/重连后全局单来源“横置符文”按钮可执行第二张符文，法力变为 2；随后执行 `END_TURN`，事件日志显示回合结束清理、符文池清空、P1 回合开始和召出符文。
- Browser Use smoke 第二片：P1/P2 正式房间重跑，双方提交 deck/ready/mulligan 后进入 `MAIN`；P2 重连视角横置两张符文，点击手牌《军团后卫》打开详情抽屉；抽屉展示 `PLAY_CARD` 组合器、费用 2、目标 0、目的地“基地/己方主战场”和“确认打出”；确认后事件日志出现 `CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`；P2/P1 依次让过优先权后结算，事件日志出现 `STACK_ITEM_RESOLVED`、`UNIT_PLAYED_TO_BASE`，P2 基地公开对象增加《军团后卫》；P2 执行 `END_TURN` 后进入 P1 主阶段并显示回合开始事件。
- Computer Use smoke 第三片：Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use。API `http://127.0.0.1:5088` 与 Vite `http://127.0.0.1:5173` 下创建房间 `room-9z3bds`；P1/P2 入座、提交 deck、ready、双方 mulligan 后进入 `MAIN`；P1 横置两张符文、从详情抽屉打出《军团后卫》、P1/P2 让过优先权，服务端结算到基地并记录 `UNIT_PLAYED_TO_BASE`；P1 点击基地《军团后卫》，抽屉展示服务端驱动的 `MOVE_UNIT` 组合器“基地 -> 战场”和目的地“战场”；确认后事件日志出现 `UNIT_MOVED_TO_BATTLEFIELD` / “P1 将单位移动到战场”，最终 prompt 继续来自服务端 snapshot。
- Computer Use smoke 第四片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用 Computer Use。API `http://127.0.0.1:5088` 与 Vite `http://127.0.0.1:5173` 下创建房间 `smoke-assemble-2`；通过 Development-only `SeedScenario(equipment)` 准备红色符能、手牌《长剑》和基地《大力仙灵》；P1 从详情抽屉打出《长剑》，事件日志出现 `CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`；P1/P2 让过优先权后出现 `STACK_ITEM_RESOLVED`、`EQUIPMENT_PLAYED_TO_BASE`；P1 点击基地《长剑》，详情抽屉展示服务端驱动的 `ASSEMBLE_EQUIPMENT` 组合器、目标 `P1-UNIT-ASSEMBLE-TARGET` 与费用“装配红色符能”；确认后事件日志出现 `COST_PAID`、`EQUIPMENT_ATTACHED`，最终 snapshot 显示长剑贴附到目标单位。
- Computer Use smoke 第五片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用 Computer Use。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5173` 下打开房间 `smoke-activate-1`；通过 Development-only `SeedScenario(battlefield-unit-experience-ability)` 准备蜕变花园授予能力来源；P1 对战桌面显示 `ACTIVATE_ABILITY` 候选，全局按钮因需选择来源保持禁用；点击 `P1-BATTLEFIELD-EXPERIENCE-UNIT` 后详情抽屉展示服务端驱动的激活能力组合器，确认后事件日志出现 `ABILITY_ACTIVATED`、`UNIT_EXHAUSTED`、`BATTLEFIELD_TRIGGER_RESOLVED`、`EXPERIENCE_GAINED`。额外 SignalR 校验确认最终 snapshot 中 `experience = 1`、来源 `exhausted = true`、后续 prompt 为 `MOVE_UNIT,END_TURN`。
- Computer Use smoke 第六片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用 Computer Use。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-legend-1`；通过 Development-only `SeedScenario(legend-act)` 准备 Poppy 传奇行动；P1 对战桌面显示 `LEGEND_ACT` 候选，全局按钮因需选择来源保持禁用；点击 `P1-LEGEND-POPPY` 后详情抽屉展示服务端驱动的传奇行动组合器，确认后事件日志出现 `LEGEND_ABILITY_ACTIVATED`、`EXPERIENCE_SPENT`、`LEGEND_EXHAUSTED`、`CARD_DRAWN`，最终 snapshot 显示 P1 经验 0、Poppy 横置、手牌 +1，后续 prompt 收敛为 `END_TURN`。
- Computer Use smoke 第七片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用 Computer Use。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-battle-3`；通过 Development-only `SeedScenario(battle-declare)` 准备公开攻防单位；P1 点击己方战场《大力仙灵》后详情抽屉展示服务端驱动的 `DECLARE_BATTLE` 组合器、战场“己方主战场”、防守者 `P2-BATTLE-DEFENDER-001` 与必需费用“战斗分配”；确认后事件日志出现 `BATTLE_DECLARED`、两条 `DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终 snapshot 显示防守者进入废牌堆，后续 prompt 收敛为 `END_TURN`。
- Computer Use smoke 第八片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用 Computer Use；按用户要求清理旧本地符文战场测试标签，后续 smoke 改在新的 Chrome 窗口执行。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-spell-focus-1`；通过 Development-only `SeedScenario(spell-duel-focus)` 准备 P1《海克斯射线》和 P2 战场单位。P1 连接后桌面显示 `SPELL_DUEL_OPEN`、焦点 P1、prompt `PLAY_CARD,PASS_FOCUS`；点击手牌《海克斯射线》后详情抽屉展示服务端目标槽 `P2-UNIT-HEXTECH-RAY-001`，选择目标并确认后事件日志出现 `CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`，右侧 prompt 切到 `PASS_PRIORITY`。额外 SignalR smoke 让 P2 过优先权，服务端广播 `PRIORITY_PASSED`、`STACK_ITEM_RESOLVED`、`DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终 snapshot 回到 `SPELL_DUEL_OPEN` 且 P2 prompt 为 `PASS_FOCUS`。
- Computer Use smoke 第九片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-battlefield-contest-1`；通过 Development-only `SeedScenario(battlefield-contest-stack)` 构造争夺战场与待结算栈项目。P2 浏览器视角显示 `NEUTRAL_CLOSED`、规则队列 `BATTLEFIELD_TASKS`、活动任务 `cleanup:battlefield-contested:P1-BATTLEFIELD-CONTEST-001` 且自己只能等待；Node/SignalR 让 P1 提交 `PASS_PRIORITY` 后，页面事件日志出现 `PRIORITY_PASSED`、`STACK_ITEM_RESOLVED`、`BATTLEFIELD_CONTESTED`、`SPELL_DUEL_STARTED`，snapshot 切到 `SPELL_DUEL_OPEN`，P2 prompt 只显示服务端给出的“让过焦点”。P2 点击“让过焦点”后事件日志出现 `FOCUS_PASSED`，Node 让 P1 继续 `PASS_FOCUS` 后出现 `SPELL_DUEL_CLOSED`；最终回到 `BATTLEFIELD_CONTESTED` blocking queue，记录为下一批 battle/control task 缺口。
- Computer Use smoke 第十片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-battlefield-contest-2`；通过 Development-only `SeedScenario(battlefield-contest-stack)` 构造争夺战场与待结算栈项目。P2 浏览器视角显示 `SPELL_DUEL_OPEN`、规则队列 `SPELL_DUEL_TASKS`、active task `task:start-spell-duel:P1-BATTLEFIELD-CONTEST-001` 和服务端 prompt “让过焦点”；P2 点击后事件日志出现 `FOCUS_PASSED`，Node/SignalR 让 P1 继续 `PASS_FOCUS` 后出现 `SPELL_DUEL_CLOSED`；最终页面显示 `NEUTRAL_OPEN`、规则队列 `BATTLE_TASKS`、active task `task:start-battle:P1-BATTLEFIELD-CONTEST-001`，当前行动为服务端 blocking `WAIT`。
- Computer Use smoke 第十一片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5174` 下打开房间 `smoke-battlefield-contest-3`；通过 Development-only `SeedScenario(battlefield-contest-stack)` 推进到 `BATTLE_TASKS` 后，P2 浏览器视角获得服务端 `DECLARE_BATTLE` prompt；点击己方《大力仙灵》打开详情抽屉，抽屉展示服务端限定的 `DECLARE_BATTLE` 组合器、当前争夺战场和唯一防守者，确认后事件日志出现 `BATTLE_DECLARED`、`DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终 pending queue `IDLE`、prompt 回到 `END_TURN`。
- Computer Use smoke 第十二片：Browser Use 当前仍无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口。API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5175` 下打开房间 `smoke-battlefield-control-1`；通过 Development-only `SeedScenario(battlefield-contest-stack)` 推进到 `BATTLE_TASKS` 后，P2 浏览器视角按服务端 `DECLARE_BATTLE` 候选从详情抽屉确认战斗；事件日志显示中文“战斗结束”“战场控制结算”，最终中央战场显示 `控制：P1`、pending queue `IDLE`、prompt 回到普通开环。刷新页面后 P2 点击“连接/重连”能恢复该权威 snapshot。
- Browser Use smoke 第十四片：IAB backend 可用，优先使用 Browser Use。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093` 以无持久化配置启动，房间 `local`；P2 在前端设置 `serverUrl = http://127.0.0.1:5093`、`playerId = P2` 并连接房间，后台 SignalR 让 P1 加入并 seed `battlefield-illegal-standby`。P2 页面规则队列显示中文“状态清理”“待命清理”，prompt 原因显示 `REMOVE_ILLEGAL_STANDBY`，服务端 snapshot 的战场 pendingTaskKinds 同步包含 `REMOVE_ILLEGAL_STANDBY`，规则队列阶段为 `STATE_BASED_CLEANUP`、活动任务为非法待命清理任务，prompt 为 `WAIT`；刷新页面后 P2 点击“连接/重连”能恢复同一权威 snapshot。
- 后台 Chrome/CDP smoke 第十五片：Browser Use IAB backend 本次不可用，按不抢前台的 smoke 原则使用后台 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-battlefield-resolutions-4`；P1 由 Web UI 连接，P2 后台 SignalR 入座，后台开发连接 seed `battlefield-held-draw` 并提交 P1 `DECLARE_BATTLE`。页面事件日志显示“据守战场”“战场控制结算”，规则队列从服务端 `timing.battlefieldResolutions` 只读显示 `据守：P2` 与 `控制结算：无控制者`；reload 后 P1 点击“连接/重连”恢复同一权威结果。
- 后台 Chrome/CDP smoke 第十六片：Browser Use IAB backend 本次不可用，继续使用后台 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-reconnect-fallback-1`；后台先让 P1/P2 入座并 seed `battlefield-held-draw`，随后浏览器本地故意写入过期 `riftbound.session.{room}.P1` reconnect token。P1 在 Web UI 点击“连接/重连”后，前端先收到服务端 reconnect 拒绝，再自动清理旧 token 并 fallback 到 `JoinRoom`，最终恢复含“帝柳之林”的权威 snapshot，localStorage 写回新的 `rt_` token。该补丁只处理连接恢复，不改变任何游戏命令候选或规则裁决。
- 后台 Chrome/CDP smoke 第十七片：Browser Use IAB backend 本次不可用，按不抢前台原则使用后台 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-recycle-rune-browser-mov6ieuw-1`；后台 SignalR 将正式房间推进到 P2 主阶段并横置一张基础符文后，P2 Web UI 连接房间、进入对战桌面、点击基地符文详情中的“回收符文”。页面事件日志显示“回收符文”“获得符能”，额外 SignalR 观察者确认 authoritative snapshot 中 `P2-RUNE-SFD-R03-10` 已从基地移到符文牌堆底部、`runeDeckCount 10 -> 11`、`runePool.powerByTrait.blue = 1`；reload 后点击“连接/重连”仍恢复该最终 snapshot。
- 后台 Chrome/CDP smoke 第十八片：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-multi-defender-mova8kc6`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-multi-defender`。P1 点击《沃利贝尔》打开 `DECLARE_BATTLE` 组合器，抽屉显示“防守单位 1”“防守单位 2”和第二槽“不选择”；按服务端候选选择 `P2-BATTLE-MULTI-LEBLANC` 与 `P2-BATTLE-MULTI-KITTEN` 后确认。页面事件日志显示“造成伤害”“战斗结束”，后台 SignalR 验证 `DAMAGE_APPLIED.assignmentRole` 包含 `BULWARK_FIRST` / `BACK_ROW_LAST`，P2 graveyard 为 `P2-BATTLE-MULTI-KITTEN`、`P2-BATTLE-MULTI-LEBLANC`；reload/reconnect 后页面恢复 `废牌堆 2` 与 P1《沃利贝尔》的最终 snapshot。
- 后台 Chrome/CDP smoke 第十九片：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-same-priority-bulwark-orikd055`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-same-priority-bulwark`。P1 点击《沃利贝尔》打开 `DECLARE_BATTLE` 组合器，按服务端候选在“防守单位 1”选择 `P2-BATTLE-SAME-BULWARK-B`，在“防守单位 2”选择 `P2-BATTLE-SAME-BULWARK-A` 并确认。页面事件日志显示“造成伤害”“战斗结束”；后台 SignalR 校验攻击者伤害顺序为 B `assignmentIndex = 1 / damage = 4`，A `assignmentIndex = 2 / damage = 6`，最终 snapshot 中 P1《沃利贝尔》保留 8 伤害，P2 两个壁垒单位进入 graveyard；reload/reconnect 后页面恢复 `废牌堆 2` 与同一最终 snapshot。
- 后台 Chrome/CDP smoke 第二十片：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-battle-no-result-0m20w1k8`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-no-result`。P1 点击《盖伦》打开 `DECLARE_BATTLE` 组合器，按服务端候选选择 `P2-BATTLE-NO-RESULT-DEFENDER` 并确认；页面事件日志显示“造成伤害”“战斗无结果”。后台 SignalR 校验 `BATTLE_NO_RESULT.reason = ALL_PARTICIPANTS_DESTROYED`、幸存列表为空、双方单位均进入 graveyard、`timing.battle.isActive = false`；reload/reconnect 后页面恢复最终 authoritative snapshot。
- Browser dev logs 中仍有本地 API 重启时产生的历史 SignalR 断线/协商失败记录；重启后本批功能 smoke 正常完成。

### Batch 6+：服务端 P0/P1 补齐

优先顺序：

1. 完整 battlefield/standby/control task 状态机。
2. Central cleanup task queue。
3. 由 task queue 驱动的 spell duel/battle lifecycle。
4. 全路径 PaymentEngine。
5. LayerEngine。
6. 全官方卡牌证据和图鉴状态收敛。

每个服务端批次必须先补测试，再补实现，最后更新 `CURRENT_SERVER_RULE_AUDIT.md` 和本文。

本批新增恢复前 action-log final hash audit：Postgres recovery frame 会从 `match_players` 构造 replay 初始状态，registry 在 `Restore` 前用当前 `IRuleEngine` 重放 recovered commands 并拒绝 final state hash 不一致的恢复帧。随后补充真实 Postgres store smoke，覆盖迁移、journal、state snapshot、recovery frame、replay audit 和 registry 恢复，并修复 `MatchState` authoritative snapshot 反序列化问题。recovery frame 现在还会携带已裁剪的 spectator replay frame。随后补充 P0-003 0 战力与非法待命清理证据：代表性法术栈项目会把战场单位修正到 0，服务端在同一栈结算后的状态性 cleanup loop 中以 `ZERO_POWER` 入墓；非法待命也会在栈结算后的 cleanup loop 中自动翻面入墓并清空 pending task。P0-005 也补了代表性支付资源动作与 X 符能金额候选：当前符能不足以支付本次 power cost 时，`PLAY_CARD.sourceRequirements` 会暴露 `RECYCLE_RUNE:<objectId>`，同一出牌命令可先回收基础符文获得 typed power，再用 `SPEND_POWER:*`、typed `SPEND_POWER:<trait>:<amount>` 或代表性 `HASTE_READY` 急速额外费用支付；X 符能法术 prompt 会按服务端当前可支付上限公开金额选项，前端把 `SPEND_POWER:*` 作为同组单选提交。当前又清理了旧 conformance fixture prompt 比较口径：旧 `promptActions` / 未 opt-in exact 的 `expected.prompts.actions` 只要求服务端继续公开必需动作，`WAIT` 仍精确，新 fixture 可用 `exactActions: true` 精确锁定动作列表；完整 `ConformanceFixtureRunnerTests` 和后端 full test 已恢复全绿。该批不改变前端交互，但收紧了 reload/reconnect 背后的服务端权威恢复边界、前端只能等待/展示的 cleanup 规则边界、前端只能提交服务端候选支付资源动作和金额的费用边界，以及服务端 full-test 门禁。

## 6. 当前总体进度

估算整体进度：**99%（仍 NOT READY）**

已经完成：

- 必读资料和五个 PDF 已确认。
- 真实前端栈、服务端接口、关键状态视图和测试入口已确认。
- 当前 NOT READY 根因已落到本文的前端硬约束和后续批次。
- 旧 Dev UI 已清理，新的 React/Vite 前端架构、中文页面壳、REST/SignalR adapter 和基础对战桌面已落地。
- 房间/连接/提交卡组/ready/起手调度到主阶段的正式双人 Web 闭环已由服务端 prompt 驱动，不再由前端常驻按钮绕过。
- 对战桌面已有卡牌详情抽屉，公开对象细节和隐藏信息保护已通过 Browser Use smoke。
- 基础符文横置资源能力已由服务端补齐并接入卡牌详情/行动面板；前端不再展示不可解析的 `TAP_RUNE` 假操作。
- 基础符文回收资源能力已补代表性 open-main 服务端路径并接入卡牌详情/行动面板；前端只按 `RECYCLE_RUNE.sources` 提交，当前仍不宣称完整 reaction payment-window 支持。
- `HIDE_CARD` 已有服务端每来源元数据和前端卡牌详情待命布置组合器；待命费用、Teemo 替代费用、免费待命效果与《班德尔树》额外待命目的地都只按服务端候选展示和提交，真实 UI smoke 已覆盖布置后 face-down 战场对象、法力扣除与 reload/reconnect；后端 full test 当前通过 2948/2948，前端 build 已通过。
- `REVEAL_CARD` 已有服务端每来源元数据和前端卡牌详情待命翻开/反应打出组合器；普通开环翻回基地与优先权闭环作为反应入栈都只按服务端候选展示和提交，真实 UI smoke 已覆盖待命反应入栈、事件日志和 reload/reconnect；后端 full test 当前通过 2949/2949，前端 build 已通过。
- `PLAY_CARD` 首个产品级选择器已由服务端每来源元数据驱动，可真实打出无目标单位牌并走完优先权结算；普通结算链优先权窗口现已能只按服务端候选公开反应牌来源、栈目标和模式，真实 UI smoke 已覆盖《强买强卖》反制栈上法术、事件日志和 reload/reconnect。
- `MOVE_UNIT` 已有服务端每来源元数据和前端卡牌详情移动组合器，可真实把基地单位移动到战场；前端不再自行判断移动目的地或游走费用。
- `ASSEMBLE_EQUIPMENT` 已有长剑代表路径的服务端每来源元数据、红色符能候选收紧和前端卡牌详情装配组合器，可真实打出装备并装配到服务端给出的单位目标。
- `ASSEMBLE_EQUIPMENT` 来源现在要求服务端明确 `cardNo = SFD·022/221`，未知/无卡号装备对象不会被公开为可装配来源；未知装备 prompt hygiene 已通过 Hub 测试和后台 headless Chrome/CDP smoke。
- `ASSEMBLE_EQUIPMENT` 目标现在也要求服务端明确目标单位 `cardNo`；未知/无卡号单位不会被公开为可装配目标，未知装配目标 prompt/Core hygiene 已通过 Hub 测试、后端 full test 和后台 headless Chrome/CDP smoke。
- `ASSEMBLE_EQUIPMENT` 支付资源动作已覆盖代表性红色装配费用：0 红色符能但有可回收红符文时，前端只按服务端 `paymentResourceChoices` 选择并提交 `RECYCLE_RUNE:*`，真实 UI smoke 已覆盖确认按钮禁用/启用、回收事件、贴附结果和 reload/reconnect。
- `ACTIVATE_ABILITY` 已有 Vi、Xerath 和蜕变花园授予能力代表路径的服务端每来源元数据、目标/费用/Spellshield 加税候选过滤和前端卡牌详情激活组合器；前端不再自行判断可激活来源、能力目标或横置费用。
- `ACTIVATE_ABILITY` 目标现在要求服务端明确目标单位 `cardNo`；未知/无卡号单位不会被公开为泽拉斯目标技能候选，未知激活能力目标 prompt/Core hygiene 已通过 Hub 测试、后端 full test 和后台 headless Chrome/CDP smoke。
- `LEGEND_ACT` 已有代表性传奇行动的服务端每来源元数据、经验/资源/时点/前置条件过滤和前端卡牌详情传奇行动组合器；Poppy 抽牌路径已完成真实 UI smoke。
- `LEGEND_ACT` 目标现在要求服务端明确目标对象 `cardNo`；未知/无卡号单位或武装不会被公开为传奇行动目标，未知传奇目标 prompt/Core hygiene 已通过 Hub 测试、后端 full test 和后台 headless Chrome/CDP smoke。
- `DECLARE_BATTLE` 已有攻击者/防守者/战场/战斗分配费用候选的服务端每来源元数据和前端卡牌详情组合器；单攻击者/单防守者代表路径已完成真实 UI smoke，1-2 防守者代表候选已由服务端 prompt/test 覆盖，双防守槽真实 UI smoke 也已覆盖。
- `DECLARE_BATTLE` 现已补齐 1-2 攻击者 + 单防守者代表路径：服务端 `sourceRequirements` 暴露第二攻击者槽位，前端详情抽屉只渲染服务端给出的“攻击单位 2（可选）”候选，并把 `attackerObjectIds` 限定为这些候选；后台 Chrome/CDP smoke 已覆盖 P1 点击《盖伦》、选择《易》作为第二攻击者、选择《变异猫咪》防守并确认，最终 authoritative snapshot 显示盖伦和防守者入墓、易留场 1 伤害，reload/reconnect 后恢复同一结果。
- 法术对决焦点窗口已能由服务端 prompt 暴露带目标迅捷法术出牌来源和目标槽；《海克斯射线》代表路径已完成真实 UI smoke，并验证后续优先权与 P2 `PASS_FOCUS`。
- 争夺战场 task queue 已能在状态变化后由服务端自动进入 `SPELL_DUEL_OPEN`，并通过事件与 prompt 驱动前端显示；前端不再需要本地启动法术对决入口。
- 争夺战场法术对决在双方让过焦点后已能由服务端切到 `BATTLE_TASKS` / `START_BATTLE` active task；前端只显示服务端 blocking prompt，不自行推进战斗或控制权。
- `START_BATTLE` active task 已能通过服务端 `DECLARE_BATTLE` prompt 推进代表性战斗结算，并在真实 UI 中从卡牌详情提交后回到 `IDLE`。
- 战斗代表路径结算后已能关闭 battle state、清理攻防标记，并按战后占据单位更新真实战场对象控制方；前端事件日志和战场控制提示均来自服务端事件/snapshot。
- 战场控制改变后已能清理旧控制方非法待命：待命对象不再计入占据单位，服务端会广播 `BATTLEFIELD_STANDBY_REMOVED`，前端中文显示“待命清理”，最终 snapshot 中待命进入所属者墓地且战场 `standbyObjectIds` 清空。
- 任务队列已能显式暴露非法待命清理状态，前端只读显示 `STATE_BASED_CLEANUP`、`REMOVE_ILLEGAL_STANDBY` 和战场级 pendingTaskKinds，不再本地推断待命是否合法。
- 最近 held/conquer/control 战场结果已进入服务端 `timing.battlefieldResolutions`，前端规则队列只读显示“据守 / 征服 / 控制结算”，reload/reconnect 后仍能恢复最近战场结果。
- 前端重连流程新增过期 token fallback：`Reconnect` 失败时清理本地旧 session 并退回服务端 `JoinRoom`，恢复 snapshot/prompt 后写回新的 reconnect token；前端仍不自行构造游戏状态。
- 恢复链路新增服务端 action-log final hash audit：Postgres recovery frame 带 replay 初始状态，registry 在恢复前重放 recovered commands 并阻止 hash mismatch 的恢复帧，降低重连时用错误 authoritative snapshot 静默恢复的风险。
- 真实 Postgres recovery store smoke 已覆盖 migration/journal/state snapshot/recovery frame/registry 恢复闭环，并修复 `MatchState` seed/rngCursor 构造参数类型导致 authoritative snapshot 无法反序列化的问题。
- recovery frame 已携带公开 spectator replay frame，使用 authoritative state hash 和 spectator redaction，Postgres smoke 覆盖 seed/rngCursor 不泄漏。
- recovery validator 已校验 spectator replay frame 与 authoritative state 对齐：room、tick、last event sequence、authoritative hash 必须一致，且公开 frame timing 不得泄漏 `seed` / `rngCursor`。新增恢复 validator 回归；后端 full test 当前通过 3011/3011，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- `MULLIGAN` 正式开局 UI 已改为服务端候选驱动：服务端 prompt 公开当前手牌 `sources` 与 `maxSelectionCount = 2`，前端行动面板渲染“起手调度”选择器、已选数量和每张服务端来源，只提交这些候选的 `handObjectIds`，不再默认提交空数组或在前端自行判断起手调度上限。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；为避免抢用户前台，没有使用 Computer Use 操控前台 Chrome。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-mulligan-mow0wz4v`。P1 Web UI 连接后显示 4 个服务端 `MULLIGAN.sources` 和“已选 0 / 2”；点击第一张后显示“已选 1 / 2 / 将调度”，确认后事件日志显示“P1 完成起手调度 / 双方完成起手调度，开始第一个回合”。authoritative snapshot 校验 P1 `mulliganCompleted = true`、被选对象不再在手牌、手牌仍为 4 张、prompt 收敛为 `WAIT`；reload/reconnect 后恢复 `Prompt：smoke-mulligan-mow0wz4v:5:P1:WAIT` 且不再出现调度按钮。后端 build、起手调度目标回归 3/3、GameHubJoinTests 118/118、后端 full test 3011/3011 和前端 build 均已通过；当前完成度仍约 **99%**，结论仍 **NOT READY**。
- 事件日志继续补齐中文产品化标签：正式开局与回合推进的 `MULLIGAN_COMPLETED`、`MULLIGAN_PHASE_COMPLETED`、`TURN_START_BEGAN`、`RUNES_CALLED`、`RUNE_POOL_CLEARED`、`MAIN_PHASE_BEGAN` 等事件不再退回泛化“服务端事件”，同时补齐常见单位、装备、经验、状态、触发、清理、胜负等服务端 event kind 的中文标题。前端仍只显示服务端事件本体，不自行推断规则结果。
- 当前已通过后台 headless Chrome/CDP 真实 UI smoke：API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-event-labels-mow13mof`。P1 Web UI 完成起手调度后，事件日志依次显示“完成起手调度 / 起手调度结束 / 回合开始 / 召出符文 / 抽牌 / 清空符文池 / 进入主阶段”，且不包含“服务端事件”。`source ../../scripts/dev-env.sh && npm run build` 已通过；本批没有服务端代码变更。
- 计分/战场触发事件日志也完成中文收口：新增 `BATTLEFIELD_TRIGGER_RESOLVED`、`SCORE_GAINED`、`BATTLEFIELD_SCORE_PREVENTED` 等标签，战场得分、得分阻止和胜负链路不再退回泛化标题。后台 headless Chrome/CDP smoke 房间 `smoke-score-labels-mow1a2py` 通过：P1 在 `battlefield-first-turn-score` seed 中通过 UI 点击“结束回合”，事件日志显示“宣告结束回合 / 回合结束清理 / 回合玩家推进 / 回合开始 / 战场触发结算 / 获得分数 / 召出符文 / 进入主阶段”，且不包含“服务端事件”。`source ../../scripts/dev-env.sh && npm run build` 已通过；本批没有服务端代码变更。
- 静态比对后端直接 `GameEvent` kind 与目标类型 `new(...)` 事件构造后，补齐剩余 22 个中文标题：提交卡组、玩家准备、正式开局/起手抽牌/官方战场选择、开发场景载入、战场替换/指示物、增益消耗、磨牌/展示、装备/传奇/符文重置、传奇触发、游走授予、单位征服效果和返回英雄区等。前端仍只读展示服务端事件，不增加任何本地规则判断。`source ../../scripts/dev-env.sh && npm run build` 已通过；后台 headless Chrome/CDP smoke 房间 `smoke-event-kind-coverage-mow1ka5s` 通过：P1 通过前端连接，后台 P2 入座并 seed `battlefield-conquer-mill` 后提交服务端 `DECLARE_BATTLE`，UI 事件日志显示“载入开发场景 / 征服战场 / 战场触发结算 / 磨牌”，且不包含“服务端事件”。
- Dev UI 构建现在会先运行 `npm run check:event-labels`，静态扫描 `src/Riftbound.Engine` / `src/Riftbound.Api` 中的服务端 `GameEvent` 构造并要求 `EventLog` 提供中文标题，防止后续新增后端事件时前端重新退回泛化“服务端事件”。本批为构建守护脚本变更，无运行时 UI 行为变化，验证以 frontend build 为准。
- 图鉴卡面与详情抽屉现在明确展示服务端 `conformanceTier` 与 BehaviorSpec `status/reason`：卡面同时显示“代表性规则通过/人工边界/阻断”和“已实现代表路径/需要人工规则/未实现”，详情抽屉新增“服务端证据”说明；`full-official-rule-pass` 前端文案改为“完整规则证据（待最终复审）”，页面说明改为“完整规则结论以最终复审为准”，避免在 completion audit 前误导为项目整体 READY。`source ../../scripts/dev-env.sh && npm run build` 已通过；后台 headless Chrome/CDP smoke 打开 `/cards`，确认图鉴显示“代表性规则通过”“已实现代表路径”和最终复审说明，且不裸露“官方完整通过”的肯定口径。
- 图鉴详情“服务端证据”继续补充 `implementedEffectKind`、`implementedByCardNo`、`templateIds` 和前 4 条 `effects` 状态/原因，便于把每张卡的代表性实现追溯到服务端 BehaviorSpec，而不是只看笼统通过标签；前端仍只展示 API 证据，不自行判断卡牌是否完整官方通过。详情抽屉对 `templateIds/effects` 做空数组兜底，避免旧缓存或异常响应让 UI 崩溃。`source ../../scripts/dev-env.sh && npm run build` 已通过；已优先尝试 Browser Use，但本地 IAB backend 未发现，为避免抢用户前台改用后台 headless Chrome/CDP。真实 UI smoke 房间 `smoke-card-detail-evidence-mow2agx2` 通过：P1 Web UI 连接房间，后台 P2 SignalR 入座并 seed `typed-power-payment`，点击《弹幕时间》后详情显示“服务端证据 / 效果：BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT / 代理实现：OGN·268/298 / 模板：damage / effects 已实现代表路径”，reload/reconnect 后再次点击仍恢复同一证据，且无浏览器 error。
- `/cards` 图鉴页现在也能点击卡面打开同一套卡牌详情抽屉，补齐图鉴“卡牌详情”入口；详情只读取 `BehaviorSpec` 并显示“当前服务端 prompt 没有给这张牌可提交的操作”，不会在图鉴页制造任何可玩动作或本地规则判断。`source ../../scripts/dev-env.sh && npm run build` 已通过；后台 headless Chrome/CDP smoke `smoke-card-library-detail-mow2hgca` 打开 `/cards`，设置 API 为 `http://127.0.0.1:5093`，搜索《弹幕时间》，点击图鉴卡面后确认详情显示 `服务端证据`、`BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`、`OGN·268/298`、`damage` 和无 prompt 操作提示，且无浏览器 error。
- 结果页 `/matches/{matchId}/result` 不再停留在占位文案，改为连接房间并只读服务端 authoritative snapshot 的 `timing.roomStatus`、`timing.winnerPlayerId`、`timing.winningScore` 和玩家分数；页面明确不按本地分数推断胜负。`source ../../scripts/dev-env.sh && npm run build` 已通过；后台 headless Chrome/CDP smoke `smoke-result-page-mow2mcbs` 先用 SignalR seed `battle-score` 并提交 P1 `END_TURN` 触发服务端 `MATCH_WON`，再打开 `/matches/smoke-result-page-mow2mcbs/result`，连接后确认页面显示 `FINISHED`、`胜者：P1`、`P1 8 分`、`winningScore：8`，且无浏览器 error。
- `/decks` 卡组页从数量占位升级为本地测试卡组详情：按服务端 `/catalog/behavior-specs` 显示传奇/英雄/主牌堆/符文牌堆/战场池的卡名、编号、数量、类型、规则证据和实现状态，并可点击打开同一套卡牌详情；页面明确“等待服务端验证”，不在前端本地判定卡组是否合法。`source ../../scripts/dev-env.sh && npm run build` 已通过；已优先尝试 Browser Use，但本地 IAB backend 未发现，为避免抢用户前台改用后台 headless Chrome/CDP。smoke `smoke-deck-detail-mow2ss4e` 打开 `/decks`，设置 API 为 `http://127.0.0.1:5093`，搜索“蔚”，确认 ARC-001/006 显示 3 张、代表性规则通过/已实现代表路径，点击后详情展示 `ARC_VI_ROAM_STATIC_PLAY_UNIT`、模板 `move/recycle/temp_might` 和无 prompt 操作提示，且无浏览器 error。
- spectator replay redaction 新增生成式 property 覆盖，防止多组隐藏手牌、面朝下对象和随机状态重新泄漏到公开回放帧。
- P0-003 补齐战力修正降到 0 的 pending task 证据：服务端会公开 `DESTROY_ZERO_POWER_UNIT` / `STATE_BASED_CLEANUP`，前端只能显示 WAIT，不自行继续开放普通操作。
- P0-003 补齐代表性法术栈结算触发 0 战力清理证据：`PERFECT_FINALE_BATTLEFIELD_POWER_MINUS_4` 把战场单位修正到 0 后，服务端立即以 `ZERO_POWER` 摧毁并移入墓地，前端只消费事件和 authoritative snapshot。
- P0-003 补齐代表性栈结算后的非法待命自动清理：服务端会广播 `BATTLEFIELD_STANDBY_REMOVED`、同步墓地和对象位置，并清空 `REMOVE_ILLEGAL_STANDBY` pending task；前端继续只读事件/snapshot。
- P0-003 补齐回合结束特殊清理后的状态性清理证据：临时战力修正过期导致单位变为 0 战力时，服务端会在下一回合开始前立即广播 `UNIT_DESTROYED` / `ZERO_POWER` 并同步墓地和对象位置；事件顺序保持为清理完成后再 `TURN_PLAYER_ADVANCED` / `TURN_START_BEGAN`，前端只展示事件日志与最终 snapshot。
- P0-003/P1-004 继续补齐胜负后命令侧状态 guard：普通游戏命令和 `MULLIGAN` 特殊分发路径都会在 `Status != IN_PROGRESS` 时由 Core 统一拒绝，已结束比赛不会因残留 prompt、手写命令或异常 phase 状态继续改变起手完成列表、tick、胜者或区域。后端 full test 当前通过 3005/3005，前端 build 已通过；本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P1-004 补齐胜负后 Hub/session 错误语义：已结束比赛后再次提交普通 intent 现在稳定返回 `MATCH_FINISHED / match already finished`，不再误报“match has not started”；未开局房间仍保持 `MATCH_NOT_STARTED`。前端可按服务端错误明确停留在结束状态，而不是退回未开局/重连失败语义。后端 full test 当前通过 3005/3005，前端 build 已通过；本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P1-004 同步补齐胜负后 `SUBMIT_DECK` 错误语义：已结束比赛后再提交卡组现在返回 `MATCH_FINISHED / match already finished`，进行中比赛仍返回“开局后不能改卡组”的 `PHASE_NOT_ALLOWED`。前端结束态不需要把卡组提交错误误解为普通阶段限制。后端 full test 当前通过 3005/3005，前端 build 已通过；本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐 Xerath 技能伤害后的状态性 cleanup 符能池回传：Xerath 技能造成致命伤害触发 Sett 替代摧毁时，服务端现在会把 cleanup 中支付的 1 法力写回最终 authoritative `RunePools`，避免事件显示已支付但 snapshot 仍保留旧资源。新增回归测试覆盖 Sett 替代、召回、增益消耗和法力扣减；后端 full test 当前通过 3006/3006，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐回合开始战场伤害后的对象位置同步：冰霜要塞等回合开始伤害触发 cleanup 后，服务端现在会用最终 `playerZones` reconcile `ObjectLocations`，避免墓地中的被摧毁单位仍被对象索引标记为战场位置。新增回归断言覆盖 `P1-BATTLEFIELD-FROST-FALLING` 从战场入墓后 `ObjectLocations.Zone = GRAVEYARD`；后端 full test 当前通过 3006/3006，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐出牌额外费用后的对象位置同步：`PLAY_CARD` 现在会在摧毁/返回/弃置额外费用和相关触发完成后，用最终 `playerZones` 更新 `ObjectLocations`，避免前端重连看到费用单位已在墓地但对象位置仍是战场。新增《牺牲》摧毁强力单位作为额外费用的回归测试；后端 full test 当前通过 3007/3007，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐传奇行动后的对象位置同步：`LEGEND_ACT` 现在会按最终 `playerZones` 更新 `ObjectLocations`，避免 Poppy 抽到手里的牌仍被索引为主牌堆、或移动/创建类传奇行动在重连时位置分叉。新增 Poppy 抽牌位置回归断言；后端 full test 当前通过 3007/3007，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐待命反应进栈后的对象位置同步：`REVEAL_CARD` 以 `STANDBY_REACTION` 加入结算链时，服务端现在会把来源对象标记为 `STACK`，避免前端重连看到待命牌已离开基地但对象位置仍是基地。新增提莫待命反应位置回归断言；后端 full test 当前通过 3007/3007，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐回合开始 Jinx 触发抽牌后的对象位置同步：普通召符文/抽牌后的 `ObjectLocations` 已同步，但 Jinx “手牌少于两张额外抽牌”发生在该同步之后；现在触发抽牌后会再次 reconcile，避免第二张触发抽到的牌在 `PlayerZones.Hand` 中、对象位置仍停留 `MAIN_DECK`。新增 Jinx 回合开始位置回归断言；后端 full test 当前通过 3007/3007，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐基础符文横置后的对象位置同步：`TAP_RUNE` 成功后现在会按当前 `PlayerZones` reconcile `ObjectLocations`，避免基础符文已在基地并被横置、但 authoritative object index 缺失该对象位置。新增无初始对象位置的基础符文横置回归测试；后端 full test 当前通过 3008/3008，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐基础符文回收后的对象位置同步：standalone `RECYCLE_RUNE` 成功后现在会按最终 `PlayerZones` reconcile `ObjectLocations`，避免来源符文已移到符文牌堆底、但同一符文牌堆已有对象仍缺少 authoritative location。新增无初始对象位置的基础符文回收回归测试；后端 full test 当前通过 3009/3009，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐蜕变花园授予能力后的对象位置同步：`BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE` 成功后现在会按当前 `PlayerZones` reconcile `ObjectLocations`，避免战场对象与单位已横置加经验、但 authoritative object index 缺失战场对象或来源单位位置。扩展战场经验能力回归测试；后端 full test 当前通过 3009/3009，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-003/P1-004 补齐 Vi/Xerath 激活能力后的对象位置同步：代表性 `ACTIVATE_ABILITY` 成功加入结算链后现在会按当前 `PlayerZones` reconcile `ObjectLocations`，避免 Vi 来源、Xerath 来源或目标单位在 authoritative object index 中缺失。扩展 Vi 与 Xerath 激活能力回归测试；后端 full test 当前通过 3009/3009，前端 build 已通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程。
- P0-005 补齐代表性出牌支付步骤资源动作与 X 符能金额候选：当前符能不足以支付本次 power cost 时，服务端 prompt 暴露 `RECYCLE_RUNE:<objectId>`，`PLAY_CARD` 命令可先回收基础符文获得 typed power，再用 `SPEND_POWER:*`、typed `SPEND_POWER:<trait>:<amount>` 或代表性 `HASTE_READY` 急速额外费用支付；X 符能法术 prompt 会按当前可支付上限公开金额选项，前端不得自行构造未出现在候选中的资源动作或金额。
- P0-005 进一步补齐逐支付资源贡献元数据和 UI 精确选择：`paymentResourcePowerByChoice` 让前端能只按服务端数据判断“还缺多少、哪类资源能补足”，当前 1 红 + 两张可回收红符文的场景已通过 smoke 验证只能选择一张并提交，另一张保持在基地；后端 full test 当前通过 2902/2902，前端 build 已通过。
- P0-005 补齐真实需要两张支付资源的代表性证据：当前没有红色符能、两张红色符文都必须回收时，前端会在第一张选中后保持确认禁用并允许第二张继续选择，两张都选中后才启用确认；后端 full test 当前通过 2904/2904，真实 UI smoke 已通过。
- P0-005 补齐 typed 费用错 trait 支付资源禁用证据：red/blue 两张可回收符文同时出现时，`SPEND_POWER:red:2` 只允许 red 资源补足，blue 资源在 UI 中禁用且服务端提交兜底拒绝；后端 full test 当前通过 2906/2906，真实 UI smoke 已通过。
- P0-005 补齐代表性多目标 Spellshield 加税的 Hub/UI 证据：`legalTargetSelections` 由服务端列出合法目标组合，前端只按该列表启用确认；`spellshield-multiple-tax` smoke 验证非法 5 战力目标组合被禁用，两个法盾目标组合可提交并支付 3 点加税，后端 full test 当前通过 2946/2946，前端 build 已通过。
- P0-005 补齐未知传奇来源 prompt/UI 证据：缺少 `cardNo` 的 legend zone 对象即使处在《魄罗熔炉》授予传奇行动的场景中，也不会暴露 `LEGEND_ACT.sources` / `sourceRequirements`，Core 手写命令拒绝；真实 UI smoke 验证只显示隐藏信息且无“确认传奇行动”，后端 full test 当前通过 2961/2961。
- P0-005 补齐未知战场授予能力来源 prompt/UI 证据：缺少 `cardNo` 的战场单位即使处在《蜕变花园》授予激活能力的场景中，也不会暴露 `ACTIVATE_ABILITY.sources` / `sourceRequirements`，Core 手写命令拒绝；真实 UI smoke 验证只显示隐藏信息且无“确认激活能力”，后端 full test 当前通过 2964/2964。
- P0-005 补齐未知激活能力目标 prompt/UI 证据：缺少 `cardNo` 的敌方场上单位不会暴露为泽拉斯 `ACTIVATE_ABILITY.targets` 或目标槽候选，Core 手写命令拒绝；真实 UI smoke 验证泽拉斯详情只显示服务端给出的自身目标候选，未知目标详情只显示隐藏信息，后端 full test 当前通过 2990/2990。
- P0-005 补齐未知移动单位来源 prompt/UI 证据：缺少 `cardNo` 的基地/战场单位不会暴露 `MOVE_UNIT.sources` / `sourceRequirements`，Core 手写命令拒绝；真实 UI smoke 验证只显示隐藏信息且无“确认移动”，后端 full test 当前通过 2967/2967。
- P0-005 补齐未知移动战场目的地 prompt/UI 证据：缺少 `cardNo` 的战场牌对象不会暴露为 `MOVE_UNIT` 精确战场/游走目的地，Core 手写命令拒绝已有但未知身份的战场牌对象位置；真实 UI smoke 验证游走目的地只显示服务端给出的“己方主战场”，未知战场对象不进候选，后端 full test 当前通过 2996/2996。
- P0-005 补齐未知基础符文来源 prompt/UI 证据：缺少 `cardNo` 的基地符文不会暴露 `TAP_RUNE.sources` / `RECYCLE_RUNE.sources`，Core 手写命令拒绝；真实 UI smoke 验证只显示隐藏信息且横置/回收均 disabled，后端 full test 当前通过 2971/2971。
- P0-005 补齐代表性战场 Echo 减免 prompt/UI 证据：Core 已支持的《玛莱尖塔》Echo 减免现在会反映为服务端 `optionalCostChoices` 中的“回响：额外支付 1 法力”，真实 UI smoke 已验证前端可见、可提交、结算重复抽牌并可 reload/reconnect。
- P0-005 补齐战场授予下一个法术 Echo 的 prompt/UI 证据：服务端会根据 `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:<playerId>` 回合内效果公开 `ECHO` 候选，真实 UI smoke 已验证 P2 可见、可提交、触发授予 Echo 事件、重复抽牌并可 reload/reconnect。
- 完整 `ConformanceFixtureRunnerTests` 已恢复全绿：旧 fixture 的 prompt 动作期望现在作为服务端必需动作门禁，不再因服务端公开更多合法候选而误报；该批后端 full test 验证为 2889/2889。
- P0-004 补齐同优先级壁垒防守者顺序选择的代表性证据：`DECLARE_BATTLE` metadata 记录同级顺序策略，Development-only `battle-same-priority-bulwark` seed 和后台 smoke 均验证前端只按服务端候选提交顺序；后端 full test 当前通过 2893/2893。
- P0-004 补齐无胜者战斗状态代表性证据：服务端广播 `BATTLE_NO_RESULT`，前端事件日志中文显示“战斗无结果”，Development-only `battle-no-result` seed 与后台 smoke 验证双方同归于尽后 battle inactive、双方单位入墓；后端 full test 当前通过 2895/2895。
- P0-004 补齐 2 攻击者 + 2 防守者组合代表路径：服务端 `DECLARE_BATTLE.sourceRequirements` 可同时暴露第二攻击者槽和第二防守者槽，metadata 明确 `multiParticipantBattlePolicy = up-to-two-attackers-and-defenders-without-independent-assignment-prompt`；Development-only `battle-multi-participant` seed 与后台 Chrome/CDP smoke 覆盖 P1 点击《盖伦》、选择《易》作为第二攻击者、选择壁垒与普通防守者后确认，事件日志显示“造成伤害”“战斗结束”，最终 snapshot 显示易留场 1 伤害、盖伦和 P2 两个防守者入墓，reload/reconnect 后恢复同一结果；后端 full test 当前通过 2897/2897。
- P0-004 补齐最近战斗结果 snapshot：服务端 `timing.battleResolutions` 会持久公开最近 `BATTLE_CLOSED` / `BATTLE_NO_RESULT` 的结构化结果，包含攻防玩家、胜者/无胜者、攻防对象、幸存对象和摧毁对象；前端规则队列只读显示“战斗结束：P1”等结果。后台 Chrome/CDP smoke 覆盖 2 攻 + 2 防战斗后右侧显示 `战斗结束：P1`，reload/reconnect 后仍由 authoritative snapshot 恢复同一标签；后端 full test 当前通过 2897/2897。

预计剩余批次数：**1-2 批左右**

原因：

- 前端仍需补齐法术对决/响应窗口、带目标法术和复杂费用选择等产品级操作流。
- 服务端仍需补齐完整 control/held/conquer task 生命周期、独立战斗伤害分配 prompt、战斗响应窗口和官方级 battle task 状态机。
- Browser/Computer smoke 仍需继续覆盖响应窗口、断线重连和最终长链路。
- 服务端仍有多个架构级 P0/P1 规则缺口，不是单个 UI 批次可以关闭。

## 7. 工作区与提交规则

- 不提交五个 PDF/FAQ。
- 不提交未跟踪 `riftbound-dotnet.sln`。
- 不回退用户或历史已完成改动。
- 运行 dotnet 前必须使用：`source scripts/dev-env.sh && ...`
- 每个显著前端批次都做 Browser Use 或 Computer Use smoke 并在本文或对应状态文档记录。
- 每批提交后 `git status --short` 只能剩 `?? riftbound-dotnet.sln`。
