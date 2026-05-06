# 《符文战场》前端 Web 开发需求文档（直接给 Codex 使用）

用途：把本文件交给 Codex，让它在“服务端已经开发完成、单卡/关键词/特效规则逻辑已经在服务端实现”的前提下，完成《符文战场》双人联机对战 Web 前端。

本文档的目标不是让 Codex 再写一份产品方案，而是让 Codex **直接实现可运行、可联机、可完整支撑 1v1 核心规则流程的前端页面**。前端必须围绕服务端权威状态运行，不能用客户端自行判定规则替代服务端。

规则依据：

- 《符文战场》核心规则_260330.pdf。
- 裁判 FAQ_251023.pdf。
- 铸魂淬炼系列_官方FAQ_260114.pdf。
- 铸魂淬炼系列_裁判FAQ.pdf。
- 《符文战场》破限系列_裁判FAQ_260416.pdf。
- 官网卡牌图鉴：https://playloltcg.com/card.html

默认优先实现范围：**1v1 决斗模式**。即 2 名玩家、2 处战场、获胜得分 8、每名玩家提供 3 张战场并随机选 1 张进入本局、第二个行动的玩家在其首个召出阶段额外召出 1 张符文。前端必须支持完整对局流程，但不负责服务端规则判定。

---

## 0. 给 Codex 的总指令

你现在是《符文战场》前端 Web 开发 Agent。请读取当前仓库结构、服务端接口、WebSocket 协议、卡牌数据接口、已有静态资源和测试配置，然后完成前端开发。

必须遵守：

1. **优先沿用仓库现有技术栈。** 如果仓库已有 React/Vue/Next/Nuxt/Svelte 等前端框架，沿用现有框架；如果完全没有前端，优先使用 React + TypeScript + Vite。不要在未必要的情况下重构服务端或替换全栈框架。
2. **服务端是唯一规则权威。** 前端只发送行动意图，不直接修改权威 match state，不决定抽牌、洗牌、目标是否合法、战斗伤害是否合法、结算链是否结算、战场控制是否改变、得分是否成立、谁获胜。
3. **前端可以做软提示，但不能信任软提示。** 例如可以根据服务端返回的 legalActions 高亮可打出的卡，但最终行动仍必须由服务端确认。
4. **隐藏信息不得泄漏。** 前端永远不得主动请求或渲染对手手牌内容、对手牌堆顺序、对手面朝下待命牌内容、未来抽牌结果、随机种子、洗牌结果。
5. **不要自己“简化规则”。** 不允许把法术对决、结算链、优先行动权、焦点、清理、待处理项目、伤害分配、待命、燃尽、终局得分限制简化成普通卡牌游戏流程。
6. **如果服务端接口缺少前端必要字段，不要擅自绕过。** 先实现前端 adapter 和 mock fallback，并在最终报告中列出“服务端接口缺口 / 阻断项”。只有当用户明确要求修服务端时，才修改服务端规则逻辑。
7. **开发完成后必须输出运行说明和自查报告。** 包括启动命令、构建命令、测试命令、已实现页面、已接入接口、未接入/缺失接口、可完成的手动对局场景。

最终交付必须是实际代码实现，不是另一份计划。

---

## 1. 产品目标

开发一个完整、精美、可双人联机对战的《符文战场》Web 前端。玩家应能完成以下流程：

1. 打开首页。
2. 浏览卡牌图鉴。
3. 创建或导入卡组。
4. 创建 1v1 房间或通过房间码加入。
5. 双方选择卡组并准备。
6. 服务端完成开局准备后进入对战页。
7. 双方通过 WebSocket 实时同步进行整局游戏。
8. 前端根据服务端快照显示当前阶段、优先行动权、焦点、结算链、法术对决、战场控制、战斗、伤害分配、得分、胜负。
9. 对局结束后显示结算页，并可查看日志/复盘。

前端体验目标：

- 熟手可以快速操作，不被过度弹窗打断。
- 新手能看懂当前是什么阶段、为什么能做/不能做某个动作。
- 裁判/测试人员能通过日志与可视化状态确认服务端规则流程是否正确。
- 整体视觉要有数字卡牌游戏质感，而不是普通后台管理页面。

---

## 2. 当前开发边界

### 2.1 P0 必须完成

P0 是“可以真实双人联机打一局 1v1”的最低标准。

必须完成：

- 首页。
- 卡牌图鉴基础页。
- 卡组列表、卡组导入/选择、卡组合法性展示。
- 创建房间、加入房间、选择卡组、准备、取消准备、开始游戏。
- 对战主页面。
- WebSocket 连接、断线提示、重连、状态快照同步。
- 起手调整 UI。
- 阶段/回合/优先行动权/焦点显示。
- 手牌、基地、战场、主牌堆、符文牌堆、废牌堆、除外区、英雄区、传奇区显示。
- 中央 2 处战场显示：控制者、争夺中、单位、待命槽、得分状态。
- 结算链面板：待处理、已确认、来源、目标、控制者、当前需要谁操作。
- 所有服务端 prompt 的通用渲染：选择目标、选择模式、支付费用、分配伤害、排序触发、确认/取消、让过、起手调整、选择卡牌、选择位置、选择对手、选择数量。
- 动作栏：根据服务端 legalActions 显示可执行动作。
- 打出卡牌、移动单位、激活技能、设置待命、从待命打出、让过、结束回合、投降等行动意图发送。
- 非法行动错误提示。
- 得分条、胜利动画/结算页。
- 对局日志。
- 响应式基础适配，至少保证桌面端 1366×768 可用。
- 构建、lint、基础测试通过。

### 2.2 P1 建议完成

- 完整卡组构筑器。
- 卡牌图鉴高级筛选。
- 卡牌大图、关键词 tooltip、FAQ/勘误提示。
- 规则助手面板。
- 观战模式。
- 复盘播放器。
- 自动让过策略。
- 高质量动画。
- 新手引导。
- 卡组分享码。
- 全局设置页。

### 2.3 P2 可后续完成

- 移动端完整对战。
- 天梯匹配。
- 好友系统。
- 赛事房间。
- 裁判模式。
- AI 练习对手。
- 多人模式：1v1 比赛、3 人乱斗、4 人乱斗、2v2。

---

## 3. 技术实现要求

### 3.1 技术栈选择

Codex 应先检查仓库现有前端技术栈：

- 如果已有 Next.js：在现有 Next.js 中实现。
- 如果已有 React + Vite：沿用。
- 如果已有 Vue/Nuxt：沿用。
- 如果完全没有前端：创建 React + TypeScript + Vite 前端。

推荐默认能力：

- TypeScript 必须启用严格类型或尽可能严格。
- 前端路由：React Router 或现有框架路由。
- 状态管理：优先使用轻量 store，例如 Zustand；如果仓库已有 Redux/Pinia，沿用。
- WebSocket 管理：封装为独立 match socket client。
- 样式：优先沿用现有 CSS/Tailwind/CSS Modules；如果无现有方案，使用 CSS Modules 或普通 CSS 变量，不要引入过重 UI 框架。
- 测试：Vitest/Jest + Testing Library；E2E 可用 Playwright，如果项目已有则接入。

### 3.2 目录结构建议

如果没有现成前端结构，建议创建：

```text
src/
  app/
    App.tsx
    routes.tsx
    providers.tsx
  pages/
    HomePage/
    CardLibraryPage/
    DecksPage/
    DeckEditorPage/
    LobbyPage/
    RoomPage/
    MatchPage/
    ResultPage/
    ReplayPage/
    SettingsPage/
  features/
    cards/
    decks/
    lobby/
    match/
    rules/
    settings/
  components/
    ui/
    layout/
    cards/
    match/
    dialogs/
  services/
    apiClient.ts
    matchSocket.ts
    adapters/
  stores/
    useAuthStore.ts
    useCardStore.ts
    useDeckStore.ts
    useLobbyStore.ts
    useMatchStore.ts
  types/
    card.ts
    deck.ts
    match.ts
    actions.ts
    api.ts
  utils/
    hiddenInfo.ts
    formatters.ts
    guards.ts
    constants.ts
  styles/
    tokens.css
    globals.css
  tests/
    fixtures/
    mocks/
```

不要把所有对战逻辑堆在一个 MatchPage 文件里。对战页必须拆组件，否则后续难以维护。

---

## 4. 服务端接口接入原则

### 4.1 先发现真实接口

Codex 必须先阅读仓库中的：

- 服务端路由文件。
- WebSocket gateway / namespace / channel 文件。
- API 文档或 OpenAPI 文件。
- 现有测试用例。
- match state / view model 类型定义。
- deck/card 数据类型定义。
- 服务端自查文档，如项目中存在《符文战场_服务端核心规则自查文档.md》或类似文件。

然后建立前端 adapter。不要假设接口一定叫 `/api/matches` 或 `/ws/match`。如果真实接口与本文档示例不同，以真实接口为准。

### 4.2 建议的前端 API adapter

即使服务端接口名称不同，前端内部最好统一成以下服务层：

```text
CardApi
  listCards(query)
  getCard(cardId)
  listKeywords()
  listSets()

DeckApi
  listDecks()
  getDeck(deckId)
  createDeck(payload)
  updateDeck(deckId, payload)
  deleteDeck(deckId)
  importDeck(textOrCode)
  exportDeck(deckId)
  validateDeck(deckId or payload)

LobbyApi
  createRoom(payload)
  joinRoom(roomCode)
  leaveRoom(roomId)
  getRoom(roomId)
  setDeck(roomId, deckId)
  setReady(roomId, ready)
  startMatch(roomId)

MatchApi
  getMatchSnapshot(matchId)
  getMatchLog(matchId)
  concede(matchId)

MatchSocket
  connect(matchId, auth/session)
  onSnapshot(callback)
  onPatch(callback)
  onEvent(callback)
  onPrompt(callback)
  onError(callback)
  sendAction(actionIntent)
  requestResync()
  disconnect()
```

如果服务端没有 REST，只通过 WebSocket 工作，也要在前端封装成相同概念。

### 4.3 不允许的接入方式

禁止：

- 前端拿到完整 matchState 后自己过滤对手隐藏信息。
- 前端保存完整牌堆顺序并自己抽牌。
- 前端根据卡牌文本自行判断目标是否合法，然后直接把结果写入状态。
- 前端绕过服务端 prompt 自己结算战斗伤害。
- 前端在本地直接加分或判胜。
- 前端把服务端错误吞掉，只在 UI 上假装成功。

---

## 5. 前端内部核心数据模型

以下是前端理想的 view model。真实服务端字段可不同，但前端 adapter 应转换到类似结构，便于页面开发。

### 5.1 MatchSnapshot

```text
MatchSnapshot
  matchId: string
  roomId?: string
  version: number
  mode: 'DUEL_1V1' | string
  status: 'SETUP' | 'MULLIGAN' | 'IN_PROGRESS' | 'FINISHED' | 'ABORTED'
  perspectivePlayerId: string
  players: PlayerView[]
  turn: TurnView
  board: BoardView
  stack: StackView
  prompt?: PromptView
  legalActions: LegalActionView[]
  timers?: TimerView
  log: LogEntryView[]
  result?: MatchResultView
  connection?: ConnectionView
```

### 5.2 PlayerView

```text
PlayerView
  playerId: string
  displayName: string
  seatIndex: number
  isMe: boolean
  isConnected: boolean
  score: number
  experience?: number
  hasPriority: boolean
  hasFocus: boolean
  isTurnPlayer: boolean
  legend?: CardInstanceView
  hero?: CardInstanceView
  hand: HandView
  base: ZoneView
  mainDeck: CountOnlyZoneView
  runeDeck: CountOnlyZoneView
  runeArea?: ZoneView
  discardPile: ZoneView
  removedZone: ZoneView
  resourcePool: ResourcePoolView
  revealedCards?: CardInstanceView[]
```

对手视角中的 `hand` 必须只包含数量和卡背，不包含 cardId。对手主牌堆与符文牌堆只显示数量。对手面朝下待命牌只显示背面。

### 5.3 BoardView

```text
BoardView
  battlefields: BattlefieldView[]
```

```text
BattlefieldView
  battlefieldId: string
  card: CardInstanceView
  controllerPlayerId?: string
  isContested: boolean
  isBattleActive: boolean
  scoredThisTurnByPlayerIds: string[]
  conquerScoredThisTurnByPlayerIds?: string[]
  holdScoredThisTurnByPlayerIds?: string[]
  unitsByPlayer: Record<playerId, CardInstanceView[]>
  standbySlotsByPlayer: Record<playerId, StandbySlotView>
  pendingCombat?: CombatView
  activeEffects?: EffectBadgeView[]
```

### 5.4 CardInstanceView

```text
CardInstanceView
  instanceId: string
  cardId?: string              // 面朝下且不可见时不得出现
  name?: string                // 面朝下且不可见时不得出现
  imageUrl?: string            // 面朝下且不可见时不得出现
  cardBackType?: 'MAIN' | 'RUNE' | 'STANDBY' | 'UNKNOWN'
  face: 'FACE_UP' | 'FACE_DOWN'
  visibility: 'PUBLIC' | 'PRIVATE_TO_ME' | 'HIDDEN'
  ownerPlayerId: string
  controllerPlayerId?: string
  zone: string
  position?: string
  orientation?: 'ACTIVE' | 'RESTED'
  typeLine?: string
  cardTypes?: string[]
  runeTraits?: string[]
  cost?: CostView
  might?: number
  printedMight?: number
  currentMight?: number
  damage?: number
  keywords?: KeywordView[]
  counters?: CounterView[]
  buffs?: BuffView[]
  attachments?: CardInstanceView[]
  statusBadges?: StatusBadgeView[]
  canInspect?: boolean
```

### 5.5 StackView

```text
StackView
  state: 'NONE' | 'OPEN' | 'CLOSED'
  items: StackItemView[]
  lastPassedPlayerIds: string[]
```

```text
StackItemView
  stackItemId: string
  sourceInstanceId?: string
  sourceCardId?: string
  controllerPlayerId: string
  kind: 'CARD' | 'SPELL' | 'ABILITY' | 'TRIGGER' | 'PLAY_PERMANENT' | 'RESOURCE_ABILITY'
  status: 'PENDING' | 'CONFIRMED' | 'RESOLVING' | 'REMOVED' | 'RESOLVED'
  title: string
  description?: string
  targets?: TargetView[]
  modes?: string[]
  costs?: CostView
  createdOrder: number
  resolvesImmediately?: boolean
```

结算链 UI 必须能显示“待处理”和“已确认”。待处理项目需要玩家确认、选择、支付费用；已确认项目等待让过和结算。

### 5.6 TurnView

```text
TurnView
  turnNumber: number
  activePlayerId: string
  phase: 'WAKE' | 'BEGIN' | 'SCORE' | 'RUNE' | 'DRAW' | 'MAIN' | 'END' | 'CLEANUP' | string
  step?: string
  loopState: 'OPEN' | 'CLOSED'
  duelState: 'NORMAL' | 'SPELL_DUEL'
  compoundState: 'NORMAL_OPEN' | 'NORMAL_CLOSED' | 'SPELL_DUEL_OPEN' | 'SPELL_DUEL_CLOSED'
  priorityPlayerId?: string
  focusPlayerId?: string
  outstandingTasks?: TaskView[]
```

页面必须直接展示 compoundState，不能只显示“我的回合”。

### 5.7 PromptView

Prompt 是前端最重要的交互入口。所有需要玩家决策的规则窗口都应由 prompt 驱动。

```text
PromptView
  promptId: string
  playerId: string
  type: PromptType
  title: string
  message?: string
  min?: number
  max?: number
  options?: PromptOptionView[]
  selectableCards?: SelectableCardView[]
  selectableTargets?: SelectableTargetView[]
  selectableZones?: SelectableZoneView[]
  constraints?: PromptConstraintView[]
  defaultAction?: ActionIntent
  expiresAt?: string
  relatedStackItemId?: string
  relatedBattlefieldId?: string
```

PromptType 至少支持：

```text
MULLIGAN_SELECT
CHOOSE_MODE
CHOOSE_OPTION
CHOOSE_NUMBER
SELECT_TARGETS
SELECT_CARDS
SELECT_ZONE_OR_POSITION
PAY_COST
CONFIRM_PENDING_ITEM
ORDER_TRIGGERS
ASSIGN_COMBAT_DAMAGE
PASS_PRIORITY
SPELL_DUEL_ACTION
MAIN_PHASE_ACTION
DECLARE_MOVE
SET_STANDBY
PLAY_FROM_STANDBY
ACTIVATE_ABILITY
SELECT_BURNOUT_OPPONENT
CONFIRM_CONCEDE
END_TURN_CONFIRM
VIEW_REVEALED_CARDS
```

### 5.8 LegalActionView

```text
LegalActionView
  actionId: string
  type: ActionType
  label: string
  playerId: string
  sourceInstanceId?: string
  sourceCardId?: string
  fromZone?: string
  toZone?: string
  targetHints?: TargetHintView[]
  requiresPrompt?: boolean
  promptType?: PromptType
  disabledReason?: string
  priority?: number
```

前端高亮与按钮启用必须以 legalActions 为准。没有 legalActions 时，前端仍可显示卡牌但不可主动提交打出/移动等关键行动，只能显示“等待服务端合法行动列表”。

---

## 6. 页面路由需求

### 6.1 首页 `/`

首页内容：

- 游戏标题：《符文战场》联机对战。
- 当前运行模式：1v1 决斗。
- 主要按钮：进入大厅、创建房间、加入房间、卡牌图鉴、卡组管理、设置。
- 连接/服务端状态：显示服务端是否可达、当前 API base、当前版本。
- 简短玩法说明：控制战场、获得 8 分、击败对手。

视觉：

- 暗色符文风格。
- 中央主视觉卡牌/战场背景。
- 按钮有金属/符文光效。
- 不要像普通表单首页。

### 6.2 卡牌图鉴 `/cards`

参考官网图鉴的基本信息架构：搜索、筛选、卡牌种类、颜色、稀有度、商品/系列、分页。

必须支持：

- 按卡名搜索。
- 按卡牌种类筛选：传奇、英雄单位、专属单位、单位、指示物单位、专属法术、法术、专属装备、指示物装备、装备、战场、指示物战场、符文等，以真实卡牌数据为准。
- 按符文特性/颜色筛选。
- 按稀有度筛选。
- 按系列/商品筛选。
- 按费用区间筛选。
- 按战力区间筛选。
- 按关键词筛选。
- 分页：20/40/60 条每页。
- 卡牌网格视图。
- 紧凑列表视图。
- 卡牌大图弹窗。
- 关键词 tooltip。
- 勘误/FAQ 标记，如果卡牌数据提供。
- “加入当前卡组”按钮，如果当前处于构筑上下文。

如果卡牌图片缺失：

- 使用精美占位卡框。
- 显示卡名、类型、费用、战力、规则文本。
- 不要让图片 404 破坏布局。

### 6.3 卡组列表 `/decks`

必须支持：

- 查看已保存卡组。
- 新建卡组。
- 导入卡组文本/分享码。
- 导出卡组文本/分享码。
- 复制卡组。
- 删除卡组。
- 进入编辑器。
- 显示合法/非法状态。
- 显示传奇、英雄、主牌数量、符文数量、战场数量。

### 6.4 卡组编辑 `/decks/:deckId`

卡组编辑器必须围绕官方构筑结构：

- 传奇区：1 张传奇卡。
- 英雄区：1 张选定英雄单位。
- 主牌堆：至少 40 张，包括选定英雄在同名上限统计中；同名最多 3 张，除非规则/关键词另有构筑限制，例如唯我。
- 符文牌堆：12 张符文卡。
- 战场池：1v1 决斗需要 3 张战场，本局服务端会随机选 1 张。

编辑器 UI：

- 左侧卡牌搜索/筛选。
- 中间卡组区域。
- 右侧统计和合法性面板。
- 顶部显示当前传奇决定的符文特性限制。
- 点击卡牌加入对应区域。
- 拖拽或按钮调整数量。
- 非法卡牌加入时显示原因。
- 支持保存、撤销、重做、导出。

合法性面板至少显示：

- 是否缺少传奇。
- 是否缺少英雄。
- 英雄标签是否与传奇匹配。
- 主牌堆是否不少于 40。
- 主牌同名是否超过 3。
- 专属卡总量是否超过规则限制。
- 唯我卡是否超过 1。
- 符文牌堆是否等于 12。
- 符文是否符合传奇特性。
- 战场是否等于 3。
- 战场是否存在同名重复。
- 卡牌颜色/符文特性是否符合传奇。
- 当前版本是否可用。
- 是否包含服务端标记为未实现/测试中的卡。

如果服务端提供 deck validation，前端必须使用服务端结果作为最终合法性展示。前端本地校验只能作为即时提示。

### 6.5 大厅 `/lobby`

必须支持：

- 创建房间。
- 输入房间码加入。
- 显示我当前昵称/账号。
- 显示可用卡组。
- 显示服务端连接状态。
- 显示最近房间/重连入口，如果服务端支持。

房间创建参数：

- 模式：默认 1v1 决斗。
- 房间名称，可选。
- 私人房间码。
- 回合计时，可选。
- 是否允许观战，可选。
- 是否启用新手提示，只影响前端。

### 6.6 房间页 `/rooms/:roomId`

必须显示：

- 房间码与复制邀请链接按钮。
- 双方玩家席位。
- 玩家昵称、连接状态、准备状态。
- 玩家选择的卡组名称、传奇、英雄、卡组合法性，但不要显示完整卡组，除非双方同意或服务端设置公开。
- 选择卡组按钮。
- 准备/取消准备按钮。
- 房主开始按钮。
- 离开房间按钮。
- 房间日志：加入、离开、准备、取消准备、开始失败原因。

规则：

- 双方准备后，如果服务端自动开始，则前端跳转对战页。
- 如果需要房主点击开始，则只有房主显示开始按钮。
- 玩家准备后，若更换卡组，前端必须自动取消准备或等待服务端确认。
- 服务端返回卡组非法时，前端必须显示具体原因。

### 6.7 对战页 `/matches/:matchId`

这是核心页面。详见第 8–16 章。

### 6.8 结算页 `/matches/:matchId/result`

显示：

- 胜者。
- 比分。
- 回合数。
- 用时。
- 胜利方式：分数胜利、认输、超时、服务端终止。
- 双方传奇/英雄。
- 关键日志。
- 查看完整日志。
- 查看复盘。
- 再来一局。
- 返回大厅。

### 6.9 复盘页 `/replays/:matchId`

P1。至少支持：

- 按日志逐步播放。
- 显示公开视角。
- 对局结束后如服务端允许，可显示全知视角。
- 时间轴拖动。
- 当前动作高亮。

---

## 7. 视觉设计系统

### 7.1 总体风格

关键词：暗色奇幻、符文能量、英雄联盟宇宙感、金属边框、战场浮雕、卡牌玻璃拟态、低饱和背景、高亮交互。

页面应看起来像正式数字卡牌客户端：

- 背景用深蓝/黑曜石/暗紫渐变。
- 重要区域使用半透明面板。
- 战场有厚重边框和地形纹理感。
- 卡牌 hover 有轻微放大和光效。
- 可操作对象有清晰高亮。
- 不可操作对象降低饱和度或透明度。

### 7.2 色彩 Token

建议定义 CSS variables：

```text
--bg-main
--bg-panel
--bg-panel-strong
--border-subtle
--border-active
--text-primary
--text-secondary
--text-muted
--accent-gold
--accent-blue
--accent-red
--accent-green
--accent-purple
--danger
--warning
--success
--player-me
--player-opponent
--battlefield-neutral
--battlefield-contested
```

符文特性色彩应统一：

- 炽烈：暖红/橙。
- 翠意：绿。
- 灵光：蓝白/青。
- 摧破：深红/铁灰。
- 混沌：紫。
- 序理：金/白。

如果真实卡牌数据的符文特性名称不同，以卡牌数据为准。

### 7.3 动画原则

动画要帮助玩家理解状态变化，而不是拖慢对局。

必须有：

- 抽牌动画。
- 召出符文动画。
- 卡牌进入结算链动画。
- 结算链项目结算动画。
- 单位移动动画。
- 战场进入争夺状态动画。
- 战斗伤害动画。
- 摧毁/进入废牌堆动画。
- 得分动画。
- 胜利动画。

必须支持 reduced motion：

- 检测系统 prefers-reduced-motion。
- 设置中允许关闭或降低动画。
- 关闭动画后仍必须有状态提示。

### 7.4 可访问性

- 所有按钮必须有文本或 aria-label。
- 卡牌可通过键盘聚焦查看。
- 弹窗必须可用 Esc 关闭，关键确认弹窗除外。
- 颜色提示必须配合文字/图标，不可只靠颜色区分。
- 高对比模式应至少可读。

---

## 8. 对战主页面布局

### 8.1 桌面端布局

推荐布局：

```text
┌──────────────────────────────────────────────────────────────────────┐
│ 顶部状态栏：房间/回合/阶段/复合状态/优先行动权/焦点/计时器/连接状态 │
├───────────────┬─────────────────────────────────────┬────────────────┤
│ 左侧日志/规则 │ 对手区域                            │ 右侧结算链/动作 │
│               ├─────────────────────────────────────┤                │
│               │ 中央公共战场区：2 处战场             │                │
│               ├─────────────────────────────────────┤                │
│               │ 我方区域：基地/资源/手牌/英雄/传奇   │                │
└───────────────┴─────────────────────────────────────┴────────────────┘
```

最低桌面宽度 1366px。更宽屏时中央战场区可以展开，日志和结算链固定宽度。

### 8.2 顶部状态栏

必须显示：

- Match ID 或房间码简写。
- 当前回合数。
- 当前回合玩家。
- 当前阶段/步骤。
- 当前复合状态：普通开环、普通闭环、法术对决开环、法术对决闭环。
- 当前拥有优先行动权的玩家。
- 当前焦点玩家，如存在。
- 当前 prompt 归属玩家。
- 当前计时器。
- WebSocket 状态：已连接、重连中、断开、同步中。

状态栏示例文案：

```text
第 3 回合｜对手主要阶段｜法术对决开环｜焦点：我方｜优先行动权：我方
```

### 8.3 对手区域

显示公开信息：

- 对手头像/昵称。
- 分数 0–8。
- 经验，如果存在。
- 传奇卡。
- 英雄卡。
- 手牌数量，以卡背显示。
- 主牌堆数量。
- 符文牌堆数量。
- 废牌堆数量，可点击查看公开废牌。
- 除外区数量，可点击查看公开除外区。
- 基地中的公开单位、装备、符文/资源区域。
- 当前资源池。

不得显示：

- 对手手牌具体 cardId。
- 对手主牌堆顺序。
- 对手符文牌堆顺序。
- 对手面朝下待命牌内容。

### 8.4 中央战场区

1v1 决斗显示 2 处战场。每处战场是一个独立面板。

每处战场必须显示：

- 战场卡牌名称/图片。
- 控制者：我方、对手、无人控制。
- 是否争夺中。
- 是否正在进行战斗。
- 本回合是否已被我方/对手征服或据守得分。
- 我方单位列表。
- 对手单位列表。
- 我方待命槽。
- 对手待命槽。
- 战场效果/技能提示。
- 相关结算链/战斗高亮。

控制权视觉：

- 我方控制：我方颜色边框。
- 对手控制：对手颜色边框。
- 无人控制：灰色边框。
- 争夺中：闪烁或脉冲，显示“争夺中”。
- 战斗中：显示“战斗中”，并标记进攻方/防守方。

单位排列：

- 我方单位靠下或靠近我方。
- 对手单位靠上或靠近对手。
- 战斗中的单位显示进攻方/防守方身份小标。
- 活跃单位竖直。
- 休眠单位横置或倾斜。

### 8.5 我方区域

显示：

- 我方头像/昵称。
- 分数 0–8。
- 经验，如果存在。
- 传奇区。
- 英雄区。
- 基地区：单位、装备、符文/资源。
- 主牌堆数量。
- 符文牌堆数量。
- 废牌堆。
- 除外区。
- 资源池。
- 手牌。

手牌交互：

- 可打出的手牌高亮。
- 当前窗口不可打出的卡牌不高亮，但可查看。
- 资源不足时显示灰化与 tooltip。
- 拖拽到合法目标时目标发光。
- 非法拖拽时显示具体原因。
- 点击卡牌打开行动菜单：打出、设置待命、查看详情。
- Shift/长按/右键可固定卡牌大图。

### 8.6 左侧日志与规则提示

左侧面板默认显示日志，支持切换到规则提示。

日志：

- 按回合分组。
- 阶段变化有标题。
- 重要事件高亮。
- 私密日志只对自己显示。
- 支持搜索和筛选：得分、战斗、结算链、错误、系统。

规则提示：

- 当前阶段说明。
- 当前状态说明。
- 当前 prompt 说明。
- 关键词解释。
- 点击规则编号可展开简述。

### 8.7 右侧结算链与动作面板

右侧面板上半部分显示结算链，下半部分显示当前动作/prompt。

结算链必须从底到顶显示：

- 更早项目在下。
- 最新项目在上。
- 待处理项目使用虚线/黄色边框。
- 已确认项目使用实体边框。
- 正在结算项目使用强光。
- 每项显示来源、控制者、目标、模式、费用、状态。

动作面板显示：

- 当前 prompt。
- 主要按钮：确认、取消、让过、结束回合、支付、拒绝支付、提交伤害分配。
- 可选行动列表。
- 非法行动原因。
- 当前倒计时。

---

## 9. 规则驱动 UI 要求

这一章是前端实现的核心。前端不判定规则，但必须正确呈现服务端规则窗口和所需选择。

### 9.1 卡组与开局

前端必须支持服务端开局流程展示：

- 双方传奇进入传奇区。
- 双方选定英雄进入英雄区。
- 双方各自 3 张战场由服务端随机选择 1 张，中央显示 2 处战场。
- 主牌堆与符文牌堆洗牌，由服务端完成。
- 服务端随机决定回合顺序。
- 双方各抽 4 张牌。
- 进入起手调整。
- 起始玩家开始第一回合。
- 第二个行动的玩家在自己的首个召出阶段额外召出 1 张符文。

起手调整 UI：

- 弹窗显示我方 4 张起手牌。
- 玩家可选 0–2 张。
- 显示已选择数量。
- 点击确认后发送 mulligan action。
- 确认后不可本地反悔，除非服务端允许。
- 等待对手确认时显示等待状态。
- 不显示对手选了几张，除非服务端公开。

### 9.2 回合阶段显示

前端必须支持以下阶段/步骤显示：

- 唤醒/整备。
- 开始阶段。
- 得分计算。
- 召出符文。
- 抽牌。
- 主要阶段。
- 结束阶段。
- 清理。
- 回合传递。

阶段变化必须在顶部状态栏和日志中可见。自动阶段可以快速播放，但不能让玩家看不懂发生了什么。

### 9.3 普通/法术对决 + 开环/闭环

核心规则有四种复合状态：

- 普通开环。
- 普通闭环。
- 法术对决开环。
- 法术对决闭环。

前端必须：

- 在顶部明确显示当前复合状态。
- 根据服务端 legalActions 显示可执行行动。
- 普通开环主要阶段通常可打出正常卡、移动、激活技能。
- 普通闭环通常只能使用反应类行动。
- 法术对决开环通常只允许迅捷/反应等合法行动。
- 法术对决闭环通常只允许反应类行动。
- 没有优先行动权时不显示主动操作按钮，只显示等待信息。

不要写死“我的回合我就能操作”。必须同时检查：阶段、复合状态、优先行动权、prompt 归属、legalActions。

### 9.4 优先行动权与焦点

前端必须可视化：

- 谁拥有优先行动权。
- 谁拥有焦点。
- 当前是否无人拥有优先行动权。
- 当前是否需要某玩家处理限定行动/prompt。

UI 表现：

- 有优先行动权的玩家头像发光。
- 有焦点的玩家显示“焦点”标记。
- 我方拥有优先行动权时动作栏高亮。
- 对手拥有时显示“等待对手行动”。
- 如果 prompt 属于我方，即使没有优先行动权，也要显示该 prompt，因为限定行动不一定等同优先行动权。

### 9.5 打出卡牌

前端打出卡牌流程必须由服务端 prompt/合法行动驱动：

1. 玩家点击或拖拽手牌/英雄区/待命牌。
2. 前端查看 legalActions 是否存在对应 play action。
3. 如果需要选择模式、目标、位置、额外费用，打开打出预览或 prompt。
4. 玩家完成选择。
5. 前端发送 action intent。
6. 等服务端 ack/snapshot。
7. 服务端确认后，卡牌进入结算链或直接进场。

打出预览面板显示：

- 卡牌大图。
- 费用。
- 可选模式。
- 可选额外费用：急速、回响、其他服务端返回的额外费用。
- 目标要求。
- 位置要求。
- 预计进入：结算链/基地/战场/待命。
- 当前可支付资源。
- 服务端返回的限制提示。

前端不得：

- 自己扣资源。
- 自己把牌从手牌移动到场上。
- 自己把法术送入废牌堆。
- 自己判断目标是否合法后跳过服务端确认。

### 9.6 支付费用与资源池

资源栏必须显示：

- 当前通用法力。
- 各符文特性符能。
- 已支付/将支付预览。
- 可用于支付的符文/资源来源。
- 费用不足原因。

支付费用 prompt：

- 显示总费用。
- 显示强制额外费用，例如法盾增加费用。
- 显示可选额外费用，例如急速、回响。
- 允许玩家选择支付方案，如果服务端需要。
- 支持“拒绝支付”用于某些触发式技能费用场景。
- 支付后等待服务端确认。

如果服务端只接受“自动支付”，前端也要显示自动支付结果。

### 9.7 结算链

前端必须完整支持结算链展示和交互：

- 卡牌/技能成为待处理项目。
- 待处理项目确认。
- 选择模式/目标/费用。
- 成为已确认项目。
- 双方轮流让过。
- 最新项目结算。
- 结算链为空后进入开环。
- 在法术对决中结算链为空后焦点可能传递。

结算链面板必须显示：

- 待处理项目。
- 已确认项目。
- 项目控制者。
- 来源对象。
- 目标。
- 状态。
- 当前等待谁确认/让过/结算。

行动按钮：

- 让过。
- 打出反应。
- 激活反应技能。
- 确认待处理。
- 拒绝支付。
- 查看来源。

如果所有玩家连续让过，服务端会结算；前端只显示等待/动画，不自行弹出结算结果。

### 9.8 法术对决

法术对决是独立视觉状态，不可隐藏在普通战斗里。

触发场景包括：

- 有玩家争夺战场控制权。
- 战斗开始前的互动窗口。
- 规则指定的其他场景。

UI 必须：

- 在战场上显示“法术对决中”。
- 显示焦点玩家。
- 显示当前可使用的迅捷/反应卡或技能。
- 让无法使用的普通卡变灰。
- 显示“让过”按钮。
- 在双方让过后显示法术对决关闭动画。
- 若进入战斗伤害，显示战斗伤害分配 prompt。
- 若非战斗争夺，显示战场控制权变化。

### 9.9 移动单位

移动是立即行动，不使用结算链，不能被反应。前端必须表现出这一点：

- 玩家拖拽单位到合法地点。
- 合法地点高亮。
- 非法地点显示原因。
- 确认移动后发送 action intent。
- 等服务端返回后执行移动动画。
- 不显示“响应窗口”。

常见路径：

- 基地 → 战场。
- 战场 → 基地。
- 战场 → 战场，仅在单位具有游走或服务端允许时。

移动后可能触发：

- 战场进入争夺状态。
- 法术对决。
- 战斗。
- 战场控制变化。
- 清理。

前端必须等待服务端快照来表现后续。

### 9.10 战场控制与争夺

每处战场必须显示：

- 当前控制者。
- 是否无人控制。
- 是否争夺中。
- 谁导致争夺。
- 当前战斗/法术对决是否与该战场相关。
- 待命槽是否仍有效。

重要边界：

- 战场处于争夺状态时，控制权不会中途随单位离开立即改变。
- 法术对决/战斗期间，即使控制者单位离开，前端也不能立刻把战场显示为无人控制，必须以服务端状态为准。
- 待命牌失效/移除时点必须由服务端决定，前端不得因控制者暂时无单位而提前隐藏待命牌。

### 9.11 待命

待命 UI 必须支持：

- 将手牌/英雄区具有待命权限的牌面朝下放到自己控制的战场。
- 每处战场每名玩家的待命槽限制，以服务端 legalActions 为准。
- 自己的待命牌显示正面小图或可查看状态。
- 对手待命牌只显示背面。
- 从待命状态打出时显示其获得反应权限的交互窗口，以服务端 legalActions 为准。
- 待命牌改变区域或游戏结束时，如服务端公开展示，前端显示翻开动画。

待命槽 UI：

- 空槽：显示“可待命”或锁定状态。
- 我方待命：显示卡牌背面 + 自己可查看正面。
- 对手待命：只显示背面。
- 无控制权/不可用：显示锁图标和原因。

### 9.12 战斗

战斗 UI 必须包含：

- 发生战斗的战场高亮。
- 进攻方/防守方标签。
- 参战单位列表。
- 战斗法术对决阶段。
- 双方让过后进入战斗伤害。
- 战斗伤害分配 prompt。
- 同时造成伤害动画。
- 致命伤害/摧毁动画。
- 战斗清理。
- 战场控制和得分变化。

不要把战斗做成“点一个敌人攻击”。《符文战场》的战斗是围绕战场争夺和双方单位群体计算战力/分配伤害。

### 9.13 战斗伤害分配

伤害分配 prompt 必须做成专门界面。

界面显示：

- 当前分配玩家。
- 可分配伤害总量。
- 对方单位列表。
- 每个单位当前战力、已有伤害、致命伤害阈值。
- 壁垒/后排/其他影响分配顺序的标记。
- 已分配伤害数。
- 剩余伤害数。
- 服务端返回的分配限制。
- 合法/非法提示。

交互：

- 点击 `+` / `-` 调整每个单位分配。
- 支持拖动伤害标记。
- 支持“一键合法分配”按钮，如果服务端提供默认方案。
- 未满足服务端约束时确认按钮禁用。
- 如果服务端返回错误，显示具体原因。

前端不得自行认定最终分配合法。必须提交给服务端。

### 9.14 伤害、标记、摧毁

单位卡牌上必须显示：

- 当前战力。
- 已标记伤害。
- 致命风险提示。
- 临时增益/减益。
- 关键词图标。
- 状态：活跃、休眠、进攻方、防守方、眩晕/法盾/壁垒等。

摧毁动画：

- 卡牌闪烁/破碎。
- 移动到废牌堆。
- 如果触发绝念等，结算链出现待处理触发。

### 9.15 得分、征服、据守、终局限制

得分 UI：

- 双方得分条 0–8。
- 每处战场显示本回合是否已产生征服/据守得分。
- 得分时从战场飞向得分条。
- 日志记录得分来源。

终局限制提示：

- 玩家达到 7 分或更高时，显示“接近致胜分”。
- 如果通过据守获得最后 1 分，服务端可判定获得致胜分，前端显示据守得分。
- 如果通过征服尝试获得最后 1 分，必须关注服务端是否显示“本回合所有战场均已得分”条件。
- 前端不自行判定是否获胜，只根据服务端 result 或 score/winner 状态显示。

### 9.16 燃尽

燃尽必须有明显 UI：

- 弹出或战场中央提示：“主牌堆不足，执行燃尽”。
- 显示尽可能完成动作。
- 显示废牌堆回收至主牌堆。
- 显示选择/指定对手获得 1 分。如果需要当前玩家选择对手，显示 prompt。
- 显示继续完成剩余抽牌/移动卡牌动作。
- 写入日志。

不要只在日志里静默处理燃尽。

### 9.17 清理

清理通常由服务端自动完成，但前端必须能显示其结果：

- 阶段变化后清理。
- 状态开环/闭环转换后清理。
- 待处理项目加入或确认后清理。
- 对象进出场后清理。
- 状态改变后清理。
- 移动完成后清理。
- 战斗清理。

前端 UI：

- 默认不要为每次清理弹窗，避免打断。
- 但重要清理结果必须可见：胜负、摧毁、触发、控制权变化、待命移除、战斗身份变化。
- 日志必须记录“清理导致的结果”。

### 9.18 投降与结束

- 菜单中提供投降按钮。
- 点击后弹确认框。
- 确认后发送 concede action。
- 服务端确认后跳转结算页。
- 对手投降时显示胜利。
- 不要本地直接判定胜利，等待服务端 result。

---

## 10. 单卡、关键词、特效的前端呈现

用户已说明所有单卡、关键词、特效都已开发完成。因此前端要做的是：

- 渲染服务端给出的关键词与状态。
- 用 tooltip 解释关键词。
- 根据服务端 legalActions 提供可操作入口。
- 根据服务端 prompt 完成选择。
- 根据服务端事件/快照播放动画。

不要在前端实现关键词规则判定。

### 10.1 关键词列表

前端关键词系统必须可扩展，不要只写死少数关键词。当前核心规则/系列中至少可能出现：

- 急速。
- 迅捷。
- 强攻。
- 绝念。
- 法盾。
- 游走。
- 待命。
- 鼓舞。
- 反应。
- 坚守。
- 壁垒。
- 瞬息。
- 预知。
- 装配。
- 灵便。
- 回响。
- 百炼。
- 伏击。
- 狩猎。
- 等级。
- 唯我。
- 后排。

如果服务端/卡牌数据中有更多关键词，前端必须自动展示，不得丢失。

### 10.2 关键词 tooltip

每个关键词显示：

- 关键词名称。
- 简短解释。
- 当前数值，如强攻 2、法盾 1、坚守 3、狩猎 1、等级 3。
- 是否当前生效，例如鼓舞/等级/强攻/坚守。
- 来源对象，如果服务端提供。

复杂解释可在规则助手中展示，不要把过长规则塞进卡牌 tooltip。

### 10.3 特效动画映射

如果服务端事件提供 effectType，前端应映射动画：

```text
DRAW_CARD              抽牌
SUMMON_RUNE            召出符文
GAIN_RESOURCE          获得资源
PAY_COST               支付费用
PLAY_CARD              打出卡牌
ADD_TO_STACK           加入结算链
CONFIRM_STACK_ITEM     确认项目
RESOLVE_STACK_ITEM     结算项目
MOVE_UNIT              移动单位
ENTER_BATTLEFIELD      进场
RECALL                 召回
RECYCLE                回收
DESTROY                摧毁
DAMAGE                 造成伤害
ASSIGN_DAMAGE          分配伤害
ATTACH                 贴附
DETACH                 卸除
SET_STANDBY            设置待命
REVEAL                 展示
SCORE_CONQUER          征服得分
SCORE_HOLD             据守得分
BURNOUT                燃尽
WIN                    获胜
CONCEDE                认输
```

如果服务端没有事件类型，前端根据日志/snapshot diff 做有限动画即可，但不要为了动画改变状态。

---

## 11. 行动意图 ActionIntent

前端发送给服务端的行动必须是“意图”，不是状态结果。

建议统一格式：

```text
ActionIntent
  clientActionId: string
  matchId: string
  playerId: string
  baseVersion: number
  type: ActionType
  payload: object
  promptId?: string
```

`baseVersion` 用于服务端拒绝过期操作或要求 resync。

### 11.1 ActionType 至少覆盖

```text
ROOM_CREATE
ROOM_JOIN
ROOM_LEAVE
ROOM_SELECT_DECK
ROOM_SET_READY
ROOM_START

MULLIGAN_CONFIRM
PLAY_CARD
ACTIVATE_ABILITY
CONFIRM_PENDING_ITEM
PAY_COST
DECLINE_COST
PASS_PRIORITY
SELECT_MODE
SELECT_OPTION
SELECT_TARGETS
SELECT_CARDS
ORDER_TRIGGERS
ASSIGN_COMBAT_DAMAGE
MOVE_UNIT
SET_STANDBY
PLAY_FROM_STANDBY
END_TURN
CONCEDE
REQUEST_RESYNC
```

真实服务端如果 action type 不同，前端 adapter 负责转换。

### 11.2 错误处理

服务端错误应显示为友好文案：

- `NOT_YOUR_TURN`：当前不是你的回合。
- `NO_PRIORITY`：你没有优先行动权。
- `PROMPT_NOT_FOR_YOU`：当前需要另一名玩家操作。
- `INVALID_PHASE`：当前阶段不能执行此行动。
- `INVALID_TARGET`：目标不合法。
- `INSUFFICIENT_RESOURCE`：资源不足。
- `CARD_NOT_VISIBLE`：你不能查看这张牌。
- `STALE_VERSION`：状态已更新，请重新同步。
- `ROOM_FULL`：房间已满。
- `DECK_INVALID`：卡组不合法。
- `CONNECTION_LOST`：连接已断开。

错误出现时：

- 不修改本地状态。
- 显示 toast 或 prompt 内错误。
- 必要时请求 resync。
- 日志面板显示系统错误，但不要暴露服务端敏感栈信息。

---

## 12. 前端状态管理

### 12.1 Store 划分

建议：

```text
useCardStore
  cards
  filters
  loading
  selectedCard

useDeckStore
  decks
  currentDeck
  validation
  dirty

useLobbyStore
  currentRoom
  roomCode
  seats
  selectedDeckId

useMatchStore
  snapshot
  connectionStatus
  pendingActionIds
  localUiSelections
  selectedCardInstanceId
  focusedBattlefieldId
  animationQueue
  lastError
```

### 12.2 Match state 更新原则

- 服务端 snapshot 是真相。
- 如果收到 patch/event，必须按 version 顺序应用。
- 如果 patch 缺失或乱序，请求 full snapshot。
- 本地可保存 UI 选择，例如正在选择目标，但 snapshot 更新后要验证选择仍存在。
- 任何 ack 前的乐观动画都必须可回滚；P0 可不做乐观更新，只在服务端确认后动画。

### 12.3 选择状态

局部选择状态包括：

- 当前选中的手牌。
- 当前打出预览。
- 当前目标选择。
- 当前伤害分配草稿。
- 当前触发排序草稿。
- 当前费用支付草稿。
- 当前悬停卡牌。
- 当前固定查看卡牌。

当服务端 promptId 改变时，清空不相关草稿。

---

## 13. 组件清单

### 13.1 通用 UI 组件

- Button。
- IconButton。
- Modal/Dialog。
- Drawer。
- Tooltip。
- Toast。
- Tabs。
- Select。
- SearchInput。
- NumberStepper。
- ProgressBar。
- CountdownTimer。
- LoadingSkeleton。
- EmptyState。
- ErrorBoundary。

### 13.2 卡牌组件

- CardTile：图鉴小卡。
- CardImage：图片与占位。
- CardFace：卡牌正面。
- CardBack：卡背。
- CardZoomModal：大图。
- CardTooltip：悬浮解释。
- CardText：规则文本富文本。
- KeywordBadge。
- CostBadge。
- RuneTraitBadge。
- RarityBadge。
- CardStatusBadges。
- AttachmentStack。

### 13.3 卡组组件

- DeckList。
- DeckCardRow。
- DeckZonePanel。
- LegendSlot。
- HeroSlot。
- MainDeckPanel。
- RuneDeckPanel。
- BattlefieldPoolPanel。
- DeckStatsPanel。
- DeckValidationPanel。
- DeckImportDialog。
- DeckExportDialog。

### 13.4 大厅组件

- LobbyActions。
- CreateRoomDialog。
- JoinRoomDialog。
- RoomSeatCard。
- DeckPickerDialog。
- ReadyStatusBadge。
- InviteLinkButton。

### 13.5 对战组件

- MatchPageShell。
- MatchTopBar。
- PlayerPanel。
- OpponentPanel。
- ScoreTrack。
- ResourceBar。
- ZonePileButton。
- ZoneViewerModal。
- HandZone。
- BaseZone。
- LegendHeroPanel。
- BattlefieldArea。
- BattlefieldPanel。
- UnitRow。
- UnitCard。
- StandbySlot。
- CombatBadge。
- ControlBadge。
- StackPanel。
- StackItemCard。
- ActionPanel。
- LegalActionButtonList。
- PromptRenderer。
- MatchLogPanel。
- RulesHelpPanel。
- ConnectionBanner。
- MatchMenu。
- SurrenderDialog。

### 13.6 Prompt 组件

- MulliganPrompt。
- ChooseModePrompt。
- ChooseOptionPrompt。
- SelectTargetPrompt。
- SelectCardsPrompt。
- SelectZonePrompt。
- PayCostPrompt。
- ConfirmPendingPrompt。
- OrderTriggersPrompt。
- AssignDamagePrompt。
- PassPriorityPrompt。
- MoveUnitPrompt。
- SetStandbyPrompt。
- RevealedCardsPrompt。

PromptRenderer 必须根据 prompt.type 自动选择组件。未知 prompt 类型不能崩溃，应显示通用 JSON/选项渲染，并提示“前端暂未适配该选择窗口”。

---

## 14. 对战交互细节

### 14.1 卡牌查看

- 单击：选中卡牌或显示可用行动。
- 双击：如果服务端 legalActions 中唯一行动是打出/激活，可进入预览，不要直接提交。
- 悬停：显示 tooltip。
- 长按/Shift/右键：固定大图。
- Esc：关闭大图/取消当前本地选择。

### 14.2 拖拽

拖拽适用于：

- 手牌打出到基地/战场。
- 单位移动到基地/战场。
- 设置待命到战场待命槽。
- 伤害分配时拖动伤害标记。
- 触发排序时拖动排序。

拖拽规则：

- 拖拽开始时，根据 legalActions 标记可能目标。
- 拖到非法目标时显示红色边框和原因。
- 拖到合法目标时显示金色/蓝色边框。
- 放下后如果需要更多选择，打开 prompt，不直接提交。
- 服务端确认后再更新权威位置。

### 14.3 动作栏

动作栏基于 legalActions 渲染。按优先级显示：

1. 当前 prompt 的主操作。
2. 让过/确认/支付/提交。
3. 打出可用反应。
4. 激活技能。
5. 移动。
6. 设置待命。
7. 结束回合。
8. 菜单操作。

如果没有可操作内容：

- 我方无权行动：显示“等待对手行动”。
- 服务端处理中：显示“服务端处理中”。
- 状态不同步：显示“正在重新同步”。

### 14.4 结束回合

结束回合按钮：

- 只在服务端允许时显示。
- 如果我方仍有可用资源、可移动单位、可打出卡牌，前端可弹二次确认，但以服务端 legalActions 为准。
- 发送 END_TURN 后等待服务端推进。

### 14.5 自动让过

P1 功能。设置项：

- 无可用反应时自动让过。
- 仅普通闭环自动让过。
- 仅法术对决闭环自动让过。
- 本次窗口自动让过。

默认关闭高级自动让过。即使开启，也必须：

- 只在服务端 legalActions 明确没有其他可用行动时自动发送。
- 显示可取消倒计时。
- 写入日志/本地提示。

---

## 15. 日志系统

### 15.1 日志分类

日志至少分：

- 系统。
- 房间。
- 回合。
- 阶段。
- 行动。
- 结算链。
- 战斗。
- 得分。
- 错误。
- 私密。

### 15.2 日志显示格式

示例：

```text
第 1 回合｜玩家 A
[开始] 玩家 A 开始回合。
[召出] 玩家 A 从符文牌堆召出 2 张符文。
[抽牌] 玩家 A 抽 1 张牌。
[打出] 玩家 A 打出「某单位」到基地。
[结算链] 「某法术」加入结算链，目标：某单位。
[让过] 玩家 B 让过优先行动权。
[结算] 「某法术」结算，对某单位造成 2 点伤害。
[清理] 某单位受到致命伤害，进入废牌堆。
[得分] 玩家 A 据守「某战场」，获得 1 分。
```

私密日志示例：

```text
[私密] 你抽到了「某卡」。
```

对手只看到：

```text
[公开] 玩家 A 抽 1 张牌。
```

### 15.3 日志来源

优先使用服务端日志。前端不要自行生成会误导规则的日志。前端可以补充 UI 日志，如“正在重连”。

---

## 16. 隐藏信息与安全要求

### 16.1 前端不得保存的信息

前端不得接收或保存：

- 对手手牌 cardId。
- 对手主牌堆顺序。
- 对手符文牌堆顺序。
- 对手面朝下待命牌 cardId。
- 未公开的搜索/查看结果。
- 随机种子。
- 未来随机结果。

如果服务端错误地返回这些信息，前端也不应渲染，并应在最终报告中列为 P0 服务端泄漏风险。

### 16.2 视角过滤测试

Codex 必须新增/执行前端测试或手动检查：

- 对手手牌 DOM 中没有具体卡名和图片 URL。
- 对手待命牌 DOM 中没有具体卡名和图片 URL。
- 对手牌堆 tooltip 不显示牌名。
- 网络响应如果本身已经过滤，则确认前端未额外请求私密接口。
- React devtools/store 中不要存对手隐藏卡牌数据。

---

## 17. WebSocket 与同步

### 17.1 连接状态

前端必须显示：

- Connecting。
- Connected。
- Reconnecting。
- Disconnected。
- Resyncing。
- Error。

断线时：

- 禁用行动按钮。
- 显示重连提示。
- 自动尝试重连。
- 重连成功后请求 full snapshot。
- 如果 match 已结束，跳结算页。

### 17.2 消息处理

前端应支持：

```text
MATCH_SNAPSHOT
MATCH_PATCH
MATCH_EVENT
MATCH_PROMPT
MATCH_ERROR
ROOM_UPDATE
PLAYER_CONNECTED
PLAYER_DISCONNECTED
ACTION_ACK
ACTION_REJECTED
RESYNC_REQUIRED
MATCH_FINISHED
```

真实服务端消息名不同则 adapter 转换。

### 17.3 版本控制

- 每个 snapshot/patch 必须有 version 或 sequence。
- 低于当前 version 的 patch 丢弃。
- version 不连续时请求 resync。
- 发送 action 时带 baseVersion。
- action rejected 且原因是 stale version 时，自动 resync 并提示。

---

## 18. 规则助手

P1，但建议至少做基础版。

规则助手可显示：

- 当前阶段解释。
- 当前复合状态解释。
- 优先行动权解释。
- 焦点解释。
- 结算链解释。
- 法术对决解释。
- 战场争夺解释。
- 待命解释。
- 战斗伤害分配解释。
- 燃尽解释。
- 得分与终局限制解释。

文本不要过长，示例：

```text
法术对决开环：正在争夺战场或战斗互动中，且当前没有结算链。拥有焦点的玩家可以根据服务端允许的时机打出迅捷/反应等行动，也可以让过。
```

---

## 19. 设置页

设置项：

- 昵称。
- API 地址/服务器地址，仅开发环境可编辑。
- 音效开关。
- 动画强度：完整、简化、关闭。
- 卡牌悬停放大。
- 新手提示。
- 显示规则编号。
- 日志密度：简洁、标准、详细。
- 自动让过策略。
- 高对比模式。
- 语言，若卡牌数据支持。

设置应保存在 localStorage 或服务端用户配置中。

---

## 20. 响应式要求

### 20.1 桌面端

必须优先保证：

- 1920×1080。
- 1600×900。
- 1366×768。

1366×768 下：

- 日志和结算链可折叠。
- 手牌可横向滚动。
- 卡牌大图不能超出屏幕。
- 动作按钮不能被遮挡。

### 20.2 平板

P1。建议：

- 战场区居中。
- 日志/结算链变成抽屉。
- 手牌底部横向滚动。

### 20.3 手机

P2。至少保证：

- 首页、图鉴、卡组、房间可用。
- 对战页可以提示“建议使用桌面端”。
- 如实现手机对战，使用纵向分层：状态栏、战场、我方区域、手牌、动作抽屉。

---

## 21. 卡牌图鉴与官网风格对齐

官网卡牌图鉴已有搜索、筛选、卡牌种类、颜色、稀有度、商品、分页等信息架构。前端应借鉴这种结构，但不要简单复制样式。

图鉴应提供：

- 搜索框。
- 筛选抽屉。
- 筛选 chip。
- 卡牌种类多选。
- 颜色/符文特性多选。
- 稀有度多选。
- 系列/商品选择。
- 排序：默认、费用、名称、稀有度、类型、战力。
- 分页大小。
- 卡牌详情。

卡牌详情显示：

- 图片。
- 名称。
- 类型。
- 符文特性。
- 费用。
- 战力。
- 关键词。
- 规则文本。
- 稀有度。
- 系列。
- 版本/勘误。
- 可用状态。

---

## 22. 开发顺序

Codex 应按以下顺序开发，避免先做视觉后无法接入规则。

### 阶段 A：接口与基础壳

1. 读取服务端接口。
2. 建立 API client。
3. 建立 WebSocket client。
4. 建立基础路由。
5. 建立全局样式和设计 token。
6. 建立错误边界和连接状态。

### 阶段 B：卡牌与卡组

1. 卡牌数据加载。
2. 卡牌图鉴。
3. 卡牌详情。
4. 卡组列表。
5. 卡组选择/导入。
6. 卡组合法性展示。

### 阶段 C：房间

1. 创建房间。
2. 加入房间。
3. 选择卡组。
4. 准备/取消准备。
5. 开始游戏/等待服务端跳转。

### 阶段 D：对战骨架

1. MatchPage 连接 WebSocket。
2. 渲染 snapshot。
3. 顶部状态栏。
4. 双方玩家区域。
5. 战场区域。
6. 手牌/基地/区域查看。
7. 日志。
8. 结算链。

### 阶段 E：Prompt 与行动

1. PromptRenderer。
2. 起手调整。
3. 通用选择目标。
4. 支付费用。
5. 让过。
6. 打出卡牌。
7. 移动单位。
8. 设置待命。
9. 触发排序。
10. 伤害分配。
11. 结束回合。
12. 投降。

### 阶段 F：规则表现与动画

1. 法术对决视觉。
2. 战斗视觉。
3. 控制权/争夺。
4. 得分。
5. 燃尽。
6. 胜利/结算页。
7. 动画。
8. 新手提示。

### 阶段 G：测试与打磨

1. 单元测试。
2. WebSocket mock 测试。
3. 房间流程 E2E。
4. 对战基本流程 E2E。
5. 隐藏信息测试。
6. 构建检查。
7. 最终报告。

---

## 23. P0 验收场景

Codex 完成后，至少能演示以下场景。

### 23.1 房间流程

- 玩家 A 创建房间。
- 玩家 B 输入房间码加入。
- A/B 选择合法卡组。
- A/B 准备。
- 服务端开始 match。
- 双方跳转对战页。

### 23.2 起手调整

- 双方看到自己的 4 张起手牌。
- 可以选择 0–2 张。
- 确认后等待对手。
- 双方确认后进入第一回合。
- 不泄漏对手手牌。

### 23.3 基础回合

- 顶部状态显示当前回合玩家、阶段、复合状态。
- 召出符文时资源/符文区域更新。
- 抽牌后手牌增加。
- 主要阶段可看到 legalActions。
- 打出单位后进入服务端确认的区域。
- 结束回合后回合传递。

### 23.4 结算链与反应

- 打出法术后结算链显示项目。
- 对手有优先行动权时我方显示等待。
- 有反应时卡牌高亮。
- 双方让过后结算。
- 结算结果更新场面和日志。

### 23.5 移动与争夺

- 单位从基地移动到战场。
- 目标战场高亮。
- 移动后服务端触发争夺。
- 战场显示争夺中。
- 法术对决显示焦点和让过。

### 23.6 战斗与伤害分配

- 战斗中的战场显示进攻方/防守方。
- 法术对决结束后进入伤害分配。
- 伤害分配 UI 显示伤害总量、单位战力、致命阈值。
- 合法分配后提交。
- 同时造成伤害，单位摧毁，战场控制更新。

### 23.7 得分与胜利

- 据守/征服得分时得分条更新。
- 8 分胜利时显示结算页。
- 投降时对手胜利。

### 23.8 燃尽

- 主牌堆不足时显示燃尽提示。
- 废牌堆回收动画或日志。
- 对手获得 1 分。
- 继续完成原动作。

### 23.9 断线重连

- 断开 WebSocket 后显示断线。
- 禁用行动按钮。
- 重连后请求 snapshot。
- 场面恢复正确。

### 23.10 隐藏信息

- 对手手牌只显示数量和卡背。
- 对手待命牌只显示卡背。
- 对手牌堆只显示数量。
- DOM/store/network 不额外暴露隐藏内容。如果服务端响应中已经泄漏，最终报告中列 P0。

---

## 24. 自动化测试建议

### 24.1 单元测试

测试：

- API adapter 转换 snapshot。
- hidden info renderer 不显示 cardId。
- PromptRenderer 对每种 prompt type 渲染正确。
- StackPanel 正确排序。
- ScoreTrack 显示 0–8。
- DamageAssignment 草稿计算与服务端约束提示。
- Connection state reducer。

### 24.2 组件测试

- CardTile 图片缺失时显示占位。
- HandZone 可渲染自己手牌。
- OpponentHand 只渲染卡背。
- BattlefieldPanel 显示控制者/争夺/战斗。
- ResourceBar 显示多种资源。
- MulliganPrompt 最多选择 2 张。
- PayCostPrompt 显示拒绝支付。
- OrderTriggersPrompt 可拖动排序。

### 24.3 E2E 测试

如果有 Playwright：

- 首页进入大厅。
- 创建房间。
- 双窗口加入同一房间。
- 双方准备。
- 进入 match。
- 起手调整。
- 玩家 A 执行动作，玩家 B 看到同步。
- 断线重连。

如果无法连接真实服务端，使用 mock WebSocket fixture。

### 24.4 视觉回归

建议为以下状态保留截图：

- 首页。
- 图鉴。
- 卡组编辑。
- 房间等待。
- 起手调整。
- 普通开环主要阶段。
- 普通闭环结算链。
- 法术对决开环。
- 战斗伤害分配。
- 得分。
- 结算页。

---

## 25. Mock 数据要求

如果服务端暂时无法在本地启动，Codex 可以创建 mock fixture，但必须保持真实接口 adapter 可切换。

Mock fixture 至少包含：

- 2 名玩家。
- 2 处战场。
- 双方传奇/英雄。
- 我方手牌 4 张。
- 对手手牌 4 张卡背。
- 基地单位。
- 战场单位。
- 待命牌。
- 结算链待处理项目。
- 法术对决状态。
- 战斗伤害分配 prompt。
- 得分接近 8 的状态。
- 燃尽事件。

Mock 不得成为正式逻辑替代。最终报告要说明真实服务端接入情况。

---

## 26. 前端不可越权实现的规则清单

以下内容只能由服务端最终判定，前端不得自行决定：

- 卡组是否合法。
- 战场随机选择。
- 洗牌和抽牌。
- 先后手。
- 后手首个召出阶段额外符文。
- 当前阶段推进。
- 谁有优先行动权。
- 谁有焦点。
- 当前是普通/法术对决、开环/闭环。
- 卡牌是否可打出。
- 技能是否可激活。
- 目标是否合法。
- 费用总额和支付是否合法。
- 法盾、急速、回响等费用变化。
- 结算链顺序。
- 让过后是否结算。
- 法术对决何时关闭。
- 移动是否合法。
- 战场是否进入争夺。
- 战斗是否发生。
- 伤害分配是否合法。
- 伤害是否同时造成。
- 单位是否被摧毁。
- 清理循环结果。
- 战场控制权。
- 征服/据守是否得分。
- 燃尽。
- 胜负。
- 投降成功。

前端只能展示、收集玩家选择、发送意图、播放服务端确认后的动画。

---

## 27. 规则编号参考矩阵

以下规则编号用于 UI 文案、规则助手、测试命名，不要求前端直接实现规则。

| 模块 | 规则依据 | 前端要做 |
|---|---:|---|
| 卡组结构 | 103 | 卡组编辑和合法性结果展示 |
| 隐藏/公开信息 | 108–109 | 不渲染不可见信息 |
| 准备流程 | 111–119 | 开局展示、起手调整 |
| 回合状态 | 305–310 | 显示阶段和四种复合状态 |
| 优先行动权/焦点 | 312–313 | 显示当前行动权和焦点 |
| 回合开始 | 315 | 阶段提示、召出符文、抽牌动画 |
| 结束阶段 | 317 | 结束/清理展示 |
| 清理 | 319–323 | 展示清理结果与日志 |
| 结算链 | 328–340 | StackPanel、让过、确认、结算展示 |
| 法术对决 | 342–346 | 法术对决状态、焦点、让过 |
| 打出卡牌 | 354–359、419 | 打出预览、选择/支付 prompt |
| 待命 | 421、811 | 待命槽、面朝下、从待命打出 |
| 燃尽 | 431 | 燃尽提示、回收、对手得分 |
| 移动 | 441–447 | 拖拽移动、立即完成展示 |
| 战斗 | 455–461 | 战斗 UI、伤害分配、清理展示 |
| 得分 | 463–467 | 得分条、战场得分标记、终局提示 |
| 1v1 决斗 | 480 | 2 人、2 战场、8 分、后手补偿 |
| 认输 | 650–651 | 投降确认与结算 |
| 关键词 | 800–826 | keyword badge/tooltip，不做最终规则判定 |

---

## 28. FAQ 边界场景的前端提醒

这些不是前端判定规则，但 UI 必须能正确显示服务端状态。

### 28.1 效果无法执行仍可结算

FAQ 说明：执行卡面文字时尽可能执行所有指示，无法实现的指示被忽略；即使所有指示无法实现，卡牌仍可被打出并完成结算但不产生效果。

前端要求：

- 不要因为“预计没有效果”就禁止玩家打出。
- 只根据服务端 legalActions 判断。
- 结算后如果没有场面变化，日志仍显示该卡已结算。

### 28.2 “无法”优先于“可以”

FAQ 说明：禁止行动/效果的卡牌优先级高于允许同一行动/效果的卡牌。

前端要求：

- 如果服务端 legalActions 未提供某行动，不要因卡面有“可以”就显示为可用。
- Tooltip 可以显示“受其他效果限制，当前不可用”。

### 28.3 战场争夺中控制权不立即变化

FAQ 说明：战场处于争夺状态时，控制权无法发生变化；控制战场的玩家在对手争夺时仍维持控制。

前端要求：

- 战斗/法术对决期间不要根据单位数量自行改变控制权。
- 以服务端 controllerPlayerId 和 isContested 为准。

### 28.4 待命牌移除时点

FAQ 说明：战场争夺状态被清除后的下一次清理中，才检测待命牌是否移除。

前端要求：

- 争夺中不要提前隐藏/移除待命牌。
- 服务端发出待命牌移除事件后再动画移除。

### 28.5 伤害分配不是造成伤害

FAQ 说明：分配伤害不等于造成伤害，所有伤害分配完毕后才同时造成。

前端要求：

- 伤害分配 prompt 阶段不要显示单位已经受伤。
- 提交后服务端确认造成伤害时再加伤害标记/摧毁。

### 28.6 不能在还有其他单位可分配时过量分配

FAQ 说明：尚有其他单位可供分配时，不能给一个单位分配超过致命所需最小值。

前端要求：

- 可以用服务端 constraints 禁用明显非法的过量输入。
- 仍以服务端最终校验为准。

---

## 29. 开发完成后的 Codex 最终报告格式

Codex 完成代码后，必须输出：

```text
# 前端开发完成报告

## 1. 总体结论
READY / PARTIAL / BLOCKED

## 2. 已实现页面
- /
- /cards
- /decks
- /decks/:deckId
- /lobby
- /rooms/:roomId
- /matches/:matchId
- /matches/:matchId/result

## 3. 已接入服务端接口
列出 REST 和 WebSocket 接口。

## 4. 使用的前端技术栈
列出框架、状态管理、样式、测试。

## 5. 关键组件
列出 MatchPage、StackPanel、PromptRenderer、BattlefieldPanel 等。

## 6. 规则窗口支持情况
- 起手调整：已支持/未支持
- 优先行动权：已支持/未支持
- 结算链：已支持/未支持
- 法术对决：已支持/未支持
- 战斗伤害分配：已支持/未支持
- 待命：已支持/未支持
- 燃尽：已支持/未支持
- 得分胜负：已支持/未支持

## 7. 隐藏信息检查
说明对手手牌、牌堆、待命牌是否不会渲染。

## 8. 如何运行
- 安装依赖：...
- 启动前端：...
- 启动服务端：...
- 构建：...
- 测试：...

## 9. 已运行测试
列出命令和结果。

## 10. 手动验收场景
列出已验证的房间/对战流程。

## 11. 服务端接口缺口或阻断项
如果没有，写“无”。如果有，按 P0/P1/P2 列出。

## 12. 后续建议
列出 P1/P2 功能。
```

---

## 30. 最低成功标准

如果时间有限，Codex 必须优先保证以下内容，而不是优先做华丽动画：

1. 能进入房间并双人联机。
2. 能正确接收服务端快照。
3. 能不泄漏隐藏信息。
4. 能显示完整战场、手牌、基地、结算链、日志。
5. 能根据 prompt 完成玩家选择。
6. 能发送行动意图并处理服务端确认/拒绝。
7. 能完整显示 1v1 对局直到胜负。
8. 能构建通过。

视觉可以逐步优化，但规则窗口和状态同步不能缺。

---

## 31. 需要特别避免的前端错误

- 只做静态卡牌桌面，没有 WebSocket 同步。
- 只显示“我的回合/对手回合”，不显示优先行动权和焦点。
- 没有结算链面板。
- 法术对决只当成普通战斗前弹窗。
- 战斗伤害分配用自动计算，玩家无法按规则选择。
- 不支持待处理触发排序。
- 不支持拒绝支付触发式技能费用。
- 待命牌对双方都显示正面。
- 对手手牌在 HTML/JS store 中有 cardId，只是 CSS 隐藏。
- 前端本地直接修改分数/胜负。
- 断线后还能点击行动按钮。
- 收到服务端错误后 UI 仍表现为行动成功。
- 图片缺失导致页面崩坏。
- 卡组非法原因不显示。
- Prompt 类型未知时整个页面崩溃。

---

## 32. 建议的 UI 文案

常用文案：

```text
等待对手行动
等待服务端同步
你拥有优先行动权
你是焦点玩家
当前是普通开环状态
当前是普通闭环状态，只能执行服务端允许的反应行动
当前是法术对决开环状态
当前是法术对决闭环状态
请选择目标
请选择支付方式
是否让过优先行动权？
是否结束回合？你仍有可执行行动
该行动当前不合法
资源不足
目标不合法
服务端状态已更新，请重新选择
主牌堆不足，执行燃尽
战场进入争夺状态
法术对决开始
法术对决关闭
请分配战斗伤害
所有伤害将同时造成
你获得 1 分
对手获得 1 分
你赢得了本局游戏
你已认输
连接已断开，正在重连
重连成功，正在同步对局状态
```

---

## 33. 结语

本前端的关键不是“把牌拖到场上”，而是：

- 准确呈现服务端规则状态。
- 准确收集玩家在每个规则窗口中的选择。
- 准确发送行动意图。
- 准确保护隐藏信息。
- 准确显示结算链、法术对决、战斗、得分和胜负。

只要服务端已经正确实现核心规则，前端就应该成为一个清晰、稳定、精美的规则可视化与玩家交互层。
