# Web 简约牌桌验收证据

生成日期：2026-07-11

本目录记录 Web 对局桌面的浏览器验收结果。对局截图来自真实 ASP.NET Core API、SignalR 房间和两个隔离的有头 Chrome 会话；静态页面与复杂提示截图来自生产构建后的 Playwright QA。

## 核心截图

| 文件 | 场景 | 视口 / 来源 |
|---|---|---|
| `01-lobby.png` | 对战大厅 | 1440x810，Playwright |
| `02-mulligan.png` | P1 起手调度 | 1440x900，双客户端正式流程 |
| `03-main-action.png` | P1 主行动 | 1440x900，双客户端正式流程 |
| `04-complex-prompt.png` | 支付费用复杂提示 | 1440x900，Playwright |
| `05-result.png` | P1 胜利结算 | 1440x900，双客户端正式流程 |
| `06-mobile.png` | 移动牌桌 | 390x844，Playwright |
| `07-wide.png` | 宽屏牌桌 | 1920x1080，Playwright |

`formal-18/` 保留起手、主行动、单位移动和结算四个节点的 P1/P2 双视角原始截图。

## 双客户端流程

执行命令：

```bash
RIFTBOUND_E2E_CAPTURE_DIR=artifacts/web-arena/formal-18 \
RIFTBOUND_E2E_HEADED=1 \
npm --prefix src/Riftbound.DevUi run e2e:formal-18 -- --start-api
```

流程覆盖建房、双方提交官方卡组、准备、起手调度、抓牌与符文、出牌、优先权、堆栈结算、单位移动、重连、回合推进、战场得分、投降和服务端结算，共 18 步。

## 信息边界

- P1 与 P2 截图中，对手手牌只显示卡背和数量。
- QA 要求对手手牌中 `.card-full-image` 数量为 0，所有可访问名称均为“未公开卡牌”。
- 正式流程同时检查页面不出现 `handHidden`、`legalObjectIds`、`serverPaymentState` 等内部调试字段。
- 背景图只从当前玩家可见的公开卡牌中选择。

## 验收命令

```bash
npm --prefix src/Riftbound.DevUi run build
RIFTBOUND_QA_OUTPUT_ROOT=/tmp/riftbound-arena-qa-full npm --prefix src/Riftbound.DevUi run qa:appshots
npm --prefix src/Riftbound.DevUi run smoke:chrome -- --start-api
```

覆盖视口：1920x1080、1440x900、1280x720、390x844。浏览器检查包括无文档级溢出、约 50% 的平衡战场比例、手牌比例、传奇/英雄/基地最小尺寸、牌库与符文最小尺寸、卡牌完整可见、直接点选、Esc 清除、复杂提示、键盘可访问性和 Axe 扫描。
