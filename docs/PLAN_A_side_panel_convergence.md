# 方案 A：右侧行动面板收敛计划

> 状态：计划稿（未改任何代码）
> 目标 goal：把右侧从「调试面板全展开」收敛成「玩家可用的行动控制台」，白底黑线不变。
> 对应交接文档：`OTHER_MACHINE_HANDOFF.md` 第 165-217 行点名的头号问题。

## 1. 背景与根因

右侧 `aside.wire-side-panel`（`MatchPage.tsx:1001-1149`）信息过载。

**关键结论：tab 系统其实已经建好，过载来自「两套 UI 叠加」。**

- 已有 5 个 tab（`action / response / rules / log / detail`）+ 14 个 slot 的单 slot 切换系统：
  - `wireSidePanelTabPlan.ts:12-23` 定义 `WIRE_SIDE_PANEL_TABS`
  - `MatchPage.tsx:161` `activeSidePanelSlot` 控制主区当前 slot
  - `MatchPage.tsx:1098-1148` 主区按 `sidePanelFrame.entries` 渲染，非活跃 pane 用 `aria-hidden` 隐藏
- **但 tab 主区上方还常驻堆叠了一大坨**（`MatchPage.tsx:1001-1097`）：
  - `WireSidePanelDirectory`（1002-1008）目录导航
  - `WireSidePanelOperationPanel`（1009-1013）操作路由条
  - `WireSidePanelStateRail`（1014）状态轨道
  - `wire-side-panel-rail-stack`（1015-1097）里 5 条 `WireSidePanelRailEntry`：`status / focus / rules / receipt / main`

= 「tab 系统」+「旧的全展开 rail」同时渲染，每块被压成碎片。

## 2. 收敛目标态

| 区域 | 改造后 |
|---|---|
| 顶部 tab 条 | 保留 5 tab，改成窄导航（编号/短标签，选中黑框或加粗），复用 `WIRE_SIDE_PANEL_SHORT_LABELS`（`wireSidePanelTabPlan.ts:32-47`） |
| 常驻摘要 | 只保留 `status` 一条（轮到谁 / 阶段 / 能否行动） |
| `focus / rules / receipt` | 折叠成单行摘要，点开才展开，不再常驻铺开 |
| 主区默认 | 只展示三块：当前窗口状态 / 可执行行动（`commandCenter`）/ 提交反馈（`receipt`） |
| 4 个 authority slot | `overview / tableAuthority / informationBoundary / promptAuthority` 移出 `detail` 主区 → 「检查层」抽屉入口 |
| 宽度 | 固定 ~360-420px（或后续做可拖拽） |

主 tab slot：14 → 10。

## 3. 面板分级（依据组件职责）

**一级常驻（玩家马上要看）**
- `status` 摘要：连接 / 阶段 / 窗口 / canAct
- `commandCenter`：行动候选 + 提交 + 反馈
- `receipt`：服务端提交回执

**二级 tab 可切（按需）**
- `action` tab：`commandCenter / actionMap / interaction / actionPrompt`
- `response` tab：`responseCoach / turnWindow`
- `rules` tab：`ruleQueue / serverFlow`

**三级进抽屉/弹层（检查证据）**
- `detail` tab 现有 5 slot → 主留 `timelineDetail`
- `overview / tableAuthority / informationBoundary / promptAuthority` → 「检查层」抽屉
- `log` → 侧滑抽屉或固定小窗

## 4. 改动清单（文件 : 行号）

### 4.1 核心
| 文件 | 行号 | 改动 |
|---|---|---|
| `src/pages/MatchPage.tsx` | 1015-1097 | rail-stack 只保留 `status` 常驻；`focus/rules/receipt` 改折叠摘要 |
| `src/pages/MatchPage.tsx` | 674-925 | slot 映射：4 个 authority slot 从主区移到抽屉渲染 |
| `src/pages/MatchPage.tsx` | 1098-1148 | 主 pane 循环：随 `sidePanelFrame` 减少 |
| `src/pages/MatchPage.tsx` | 1168+ | 复用 `WireTimelineDetailLayer` 模式，新增「检查层」抽屉承载 authority slot |

### 4.2 plan 工具（`src/utils/`）
| 文件 | 行号 | 改动 |
|---|---|---|
| `wireSidePanelTabPlan.ts` | 12-23 | `detail` tab 的 slots 收为 `["timelineDetail"]`，其余迁抽屉 |
| `wireSidePanelDirectoryPlan.ts` | 34-49 | authority slot 标记为 `layer` 组或移出主目录 |
| `wireSidePanelFramePlan.ts` | 21-67 | `visibleSlots` 减少；`persistentSlots` 复核（`serverFlow` 是否仍常驻） |
| `wireSidePanelStackPlan.ts` | 66-150 | rail 映射：`focus/rules/receipt` 改默认折叠态 |
| `wireSidePanelOrchestrationPlan.ts` | 49-99 | 不变：继续算所有 slot 状态供抽屉导航 |

### 4.3 样式
| 文件 | 行号 | 改动 |
|---|---|---|
| `src/styles/globals.css` | 13025-13038 | `.wire-side-panel` 网格行数减少 |
| `src/styles/globals.css` | 12202-13328 | `.wire-side-panel-stack` / `.wire-side-panel-rail-stack` 行高、折叠态样式；宽度固定 |

## 5. 必须同步的 check / smoke（否则门禁红）

这些脚本断言 `data-wire-side-panel-*` 的数量和结构：

| 脚本 | 需同步 |
|---|---|
| `scripts/check-wire-side-panel-shell-plan.mjs` | aside 层级顺序、`visible-count` 14→10 |
| `scripts/check-wire-side-panel-directory-plan.mjs` | `expectedSlots` 14→10（移除 4 authority） |
| `scripts/check-wire-side-panel-operation-plan.mjs` | routes slot 列表减少 |
| `scripts/check-playwright-qa-match-state-surface.mjs` | `activeSlot` 可选值、count |
| `scripts/chrome-smoke.mjs` | `visible-count` / `persistent-count` / pane 数 |

涉及的关键 data 属性（`MatchPage.tsx:1098-1148`）：
`data-wire-side-panel-visible-count`、`data-wire-side-panel-persistent-count`、`data-wire-side-panel-pane`、`data-wire-side-panel-active-slot`、`data-wire-side-panel-rail-*`。

## 6. 实施分步（建议小步提交）

1. **抽屉骨架**：新增「检查层」抽屉容器，先把 4 个 authority slot 移入（主区暂时双挂可回退），跑 build。
2. **收 rail 常驻**：`focus/rules/receipt` 改折叠，仅 `status` 常驻。
3. **删主区 authority slot**：`detail` tab 只留 `timelineDetail`；同步 `wireSidePanelTabPlan/Directory/Frame` plan。
4. **同步 check/smoke 断言**（visible-count 等）。
5. **样式收敛**：宽度固定、网格行数、折叠态。
6. 跑 `build` + `qa:appshots` + `smoke:chrome`，截图对比。

## 7. 验收标准

- 用户一眼看到：是否轮到自己 / 下一步做什么 / 能点哪些行动。
- 右侧不再出现 10+ 个大面板连续堆叠。
- 规则队列、服务端流、信息边界仍可访问（进抽屉），不占默认主视图。
- `smoke:chrome` 仍覆盖 tab / 入口 / 抽屉可打开。
- 白底黑线风格不变。

## 8. 风险

- smoke 脚本对 DOM data 属性断言密集，漏改会红 → 步骤 4 单独成提交。
- `serverFlow` 当前是常驻 slot（`wireSidePanelFramePlan.ts:23`），收敛时需确认它归 `rules` tab 还是仍常驻。
- 抽屉化 authority slot 不能丢失 `tableAuthority / informationBoundary` 的契约校验入口（隐藏信息边界 smoke 依赖）。
