# 《符文战场》前端战斗桌面视觉重构提示词（给 Codex）

## 任务定位

你负责把《符文战场》本地双人 1v1 标准构筑 Web 游戏的前端页面做成接近成品网站质感的产品级桌面 UI。

本任务是视觉与交互层重构，不是规则引擎重写。服务端仍然是唯一规则权威。前端不得自行裁决规则，不得计算支付、伤害分配、触发排序、战场控制、法术对决、战斗结果、得分或胜负。

前端只允许：
1. 展示服务端 authoritative snapshot 中当前玩家可见的信息。
2. 展示服务端 ActionPrompt / PromptView / ActionPromptCandidate 提供的合法操作。
3. 提交服务端 prompt candidate 支持的 command payload。
4. 对未冻结或未知复杂 prompt 做安全降级提示。

## 输入资料

必须阅读并遵守：
- docs/A_MASTER_AGENT_GOAL.md
- docs/CURRENT_A_MASTER_CHECKPOINT.md
- docs/CURRENT_FRONTEND_CONTRACT_GAPS.md
- docs/CURRENT_SERVER_RULE_AUDIT.md
- docs/CURRENT_COMPLETION_AUDIT.md（如果存在）
- docs/符文战场_前端Web开发需求文档_给Codex.md
- docs/符文战场_服务端核心规则自查文档.md
- 仓库内五份官方规则 / FAQ PDF
- data/official 中固定的 2026-04-27 官网卡牌快照
- 用户提供的三张成品网站截图，仅作为视觉布局参考，不作为规则来源
- 以及其他现有的文档

## 视觉参考方向

用户提供的成品网站截图体现了以下方向，必须吸收，但不要照抄第三方水印、Logo 或非项目资源：

1. 画面是横向战斗桌面，主战场占据屏幕中央。
2. 上半区是对手，下半区是我方。
3. 使用大面积英雄联盟风格背景插画作为 playmat，但卡牌区域必须有半透明暗色遮罩保证可读性。
4. 卡牌真实比例显示，手牌在底部扇形或横向叠放。
5. 符文资源以一排卡牌或图标显示，醒目展示已用 / 可用 / 总数。
6. 左侧可以有竖向功能栏：返回、日志、规则、设置、调试、帮助。
7. 右侧适合放行动面板、聊天 / 日志、结算链、Prompt 面板。
8. 中央战场区域需要清楚分隔两个战场，而不是只有普通空框。
9. 重要状态必须视觉化：争夺中、我方占领、对手占领、未受控制、待命、得分、法术对决、战斗、优先权。
10. 卡牌悬停 / 点击应放大展示高清卡图和规则文本。

当前截图也暴露了需要改进的问题：

1. 页面有英文提示，必须改成简体中文。
2. 暗色遮罩和背景有时让卡牌/文字不清晰，需要更强的对比层级。
3. 战场、基地、待命、弃牌堆、符文牌堆等区域边界需要更明确。
4. prompt/行动按钮不能挤在角落，当前行动权必须明显。
5. 对手隐藏信息不能因 debug panel、日志或 snapshot 展示泄漏。
6. 移动、战斗、伤害分配、触发排序等复杂窗口需要专门面板，不能只用一个小按钮。

## 页面目标

重点重构以下前端页面：

1. 首页 / 大厅
2. 卡牌图鉴
3. 卡组构筑
4. 创建房间 / 加入房间 / 房间准备
5. 起手调整
6. 对战桌面
7. 战斗结果 / 投降 / 胜负结算
8. 设置 / 调试 / 日志面板

其中本任务优先级最高的是：对战桌面。

## 对战桌面布局要求

### 整体结构

采用 16:9 桌面优先布局，最小支持 1366x768，推荐 1920x1080。页面结构：

- 顶部：对手摘要区
- 中央：2 个战场 + 战斗 / 法术对决状态
- 底部：我方基地、手牌、英雄、传奇、资源、行动按钮
- 左侧：功能轨 / 规则提示 / 快捷按钮
- 右侧：ActionPrompt / 结算链 / 日志 / 聊天 / 调试折叠面板

### 顶部对手区

必须展示公开信息：
- 对手名称
- 对手得分
- 对手传奇 / 英雄公开信息
- 对手手牌数量，显示卡背，不显示真实卡牌
- 对手主牌堆数量
- 对手符文牌堆数量
- 对手弃牌堆数量
- 对手公开符文 / 基地 / 装备 / 单位
- 对手当前是否拥有优先权、焦点、行动中

禁止展示：
- 对手手牌真实卡牌
- 对手牌堆顺序
- 对手面朝下待命真实信息
- hidden metadata

### 中央战场区

1v1 有两处公共战场。每个战场卡片必须展示：
- 战场名称与卡图
- controller：我方 / 对手 / 未受控制
- contested：是否争夺中
- scoredThisTurn：本回合是否已经因该战场得分
- 我方单位槽
- 对手单位槽
- 待命槽
- 战场效果摘要
- 可点击放大详情

视觉状态：
- 我方占领：蓝色/青色边框 + crown 或旗帜
- 对手占领：红色/紫红边框 + 敌方旗帜
- 未受控制：灰色/中性边框
- 争夺中：金红闪烁/斜纹 overlay，显示「争夺中」
- 本回合已计分：小徽章「已计分」
- 待命区：卡背槽位；我方自己的待命可显示真实卡牌，对手待命只能显示卡背

前端不得自行判断控制权，必须完全使用 snapshot 字段。

### 我方底部区

必须展示：
- 我方名称
- 我方得分
- 我方传奇区
- 我方英雄区
- 我方基地单位 / 装备 / 符文
- 主牌堆、符文牌堆、弃牌堆、除外区
- 手牌
- 当前资源摘要
- 当前阶段 / 回合 / 行动权
- 「结束回合」「让过」「确认」「投降」等按钮，但按钮来源必须来自 prompt candidate 或明确的服务端命令可用性

手牌要求：
- 默认底部横向扇形/弧形排列。
- 可打出的卡牌只根据服务端 candidate 高亮。
- 不要用前端资源计算自行判断能否打出。
- 点击卡牌打开详情；可拖拽只作为选择交互，最终仍提交服务端 prompt action。

### 右侧 ActionPrompt 面板

右侧是核心交互面板，必须支持：

1. 普通 prompt
2. PAY_COST prompt
3. ASSIGN_COMBAT_DAMAGE prompt
4. ORDER_TRIGGERS prompt
5. PASS / END_TURN / READY / MULLIGAN 等基础操作
6. 未知 prompt 的安全降级

PAY_COST UI：
- 显示费用来源、paymentId、可选支付来源、资源余量。
- 玩家选择后提交服务端 candidate。
- 前端不得自行判定最终支付是否合法。

ASSIGN_COMBAT_DAMAGE UI：
- 显示 damagePool。
- 显示 legalTargets。
- 显示目标已有伤害、致命阈值、可分配上限。
- 可用加减按钮或拖拽分配。
- 前端可以做基础输入校验以防止负数/非数字，但最终合法性由服务端判断。
- 不在前端结算伤害。

ORDER_TRIGGERS UI：
- 显示触发来源、控制者、摘要。
- 支持拖拽排序或上移/下移。
- 非排序玩家显示「等待对手排序触发」。
- 提交 orderedTriggerIds。
- 不在前端结算触发。

未知复杂 prompt：
- 显示「该复杂操作需要服务端正式交互支持」。
- 不显示 raw hidden metadata。
- 不允许用户输入任意 JSON，除非是开发模式且仅限本地 debug。

### 日志和规则提示

日志要区分：
- 公开日志
- 我方私密日志
- 系统错误
- 规则提示

日志应该显示：
- 回合/阶段变化
- 召出符文
- 抽牌（只公开抽牌数量，不公开对手抽到什么）
- 打出卡牌
- 移动单位
- 进入法术对决
- 让过
- 伤害分配
- 清理
- 战场控制变化
- 得分
- 胜负

规则提示用简短中文解释当前窗口，例如：
- 「当前是法术对决，双方可以让过或提交服务端允许的反应。」
- 「当前需要分配战斗伤害，最终合法性由服务端判定。」
- 「该战场本回合已经计分，不能再次因同一战场得分。」

## 视觉规范

### 风格

方向：
- 暗色幻想战场
- 真实桌游 playmat 质感
- 半透明 HUD
- 大背景插画
- 卡牌真实比例
- 蓝 / 红 双方阵营边界
- 金色高亮用于行动权和关键按钮
- 紫色/金色用于法术对决/结算链

不要做成普通后台管理系统。不要过度平面化。要像一个玩家真的愿意长时间使用的线上卡牌桌面。

### 颜色建议

- 背景：#10131d / #151b2b / 深蓝黑
- 面板：rgba(12, 20, 34, 0.78)
- 我方：#3aa7ff / #56d4ff
- 对手：#ff4d6d / #ff6b81
- 争夺：#ffb84d / #ff6a3d
- 重要按钮：#c99a3e / #ffd166
- 危险按钮：#ff4d4d
- 禁用：#5f6b7a
- 文本：#f5f7fb / #b7c3d6

六域图标颜色：
- Fury / 炽烈：红橙
- Calm / 翠意：绿色
- Mind / 灵光/心智：蓝色
- Body / 摧破/躯体：橙色
- Chaos / 混沌：紫色
- Order / 序理：金黄

### 字体与可读性

- 中文 UI 使用系统字体或项目已有字体，不引入不可授权字体。
- 小字号不要低于 12px。
- 卡牌正文可以缩略，但悬停大图必须可读。
- 所有按钮必须有 hover / active / disabled 状态。
- 当前行动权必须非常明显。

## 规则相关 UI 必须覆盖

前端页面必须能表达以下规则状态，但规则结果必须来自服务端：

1. 1v1 两处战场
2. 得分目标 8 分
3. 主牌堆、符文牌堆、弃牌堆、传奇区、英雄区、基地、战场、待命区
4. 起手调整
5. 召出符文
6. 抽牌
7. 燃尽
8. 主要阶段
9. 结算链
10. 法术对决
11. 优先权
12. 焦点
13. 移动单位
14. 战场争夺
15. 征服 / 据守得分
16. 待命可见性
17. 伤害分配
18. 战斗清理
19. 触发排序
20. 投降 / 胜负
21. 断线重连
22. 隐藏信息过滤

## 页面实现建议

优先实现组件拆分：

- `GameShell`
- `BattlefieldBoard`
- `BattlefieldPanel`
- `BattlefieldSlot`
- `PlayerHeader`
- `OpponentHeader`
- `PlayerBasePanel`
- `HandFan`
- `CardView`
- `CardZoomModal`
- `RunePool`
- `PileCounter`
- `ActionPanel`
- `PayCostPrompt`
- `AssignCombatDamagePrompt`
- `OrderTriggersPrompt`
- `StackPanel`
- `SpellDuelBanner`
- `BattleBanner`
- `GameLogPanel`
- `RuleHintPanel`
- `HiddenInfoGuardDebug`
- `ConnectionOverlay`
- `ResultModal`

CSS 建议：
- 不要把所有样式塞进一个巨大文件。
- 可以先使用现有 CSS 架构，如果项目没有 Tailwind，不要强行引入 Tailwind。
- 使用 CSS variables 管理域颜色、玩家颜色、面板透明度、卡牌尺寸。
- 需要响应式断点：desktop / narrow desktop / tablet fallback。

## Chrome smoke / Playwright 要求

如果项目已有 Playwright 或 Chrome smoke，继续沿用。否则新增最小 smoke：

1. 启动 API。
2. 启动 DevUi。
3. 打开首页。
4. 打开卡牌图鉴。
5. 打开卡组页。
6. 打开房间页。
7. 打开 match shell。
8. 验证页面无 runtime error。
9. 验证 ActionPanel 未显示 raw hidden metadata。
10. 验证 SnapshotDebugPanel 不显示对手隐藏手牌、牌堆顺序、面朝下待命真实卡牌。

如果能用 fixture/dev scenario，增加：
- PAY_COST prompt 渲染
- ASSIGN_COMBAT_DAMAGE prompt 渲染
- ORDER_TRIGGERS prompt 渲染
- battlefield contested 状态渲染
- standby hidden 状态渲染

## 验收标准

本前端视觉重构任务完成时必须满足：

1. `npm run build` 通过。
2. Chrome smoke 通过，或明确记录环境阻断。
3. 页面没有白屏、runtime error、明显布局错位。
4. 对战桌面能清楚展示上下双方、两处战场、手牌、符文、牌堆、得分、日志、ActionPrompt。
5. PAY_COST / ASSIGN_COMBAT_DAMAGE / ORDER_TRIGGERS 至少能渲染服务端提供的 prompt。
6. 未冻结 prompt 安全降级，不出现任意 JSON 输入给普通玩家。
7. 前端不自行裁决规则。
8. 对手隐藏信息不泄漏。
9. 所有用户可见文案为简体中文。
10. 更新 docs/CURRENT_A_MASTER_CHECKPOINT.md 和前端相关审计文档。
