# Web 简约牌桌验收证据

生成日期：2026-07-12

本目录记录 Web 对局桌面的浏览器验收结果。对局截图来自真实 ASP.NET Core API、SignalR 房间和两个隔离的有头 Chrome 会话；静态页面与复杂提示截图来自生产构建后的 Playwright QA。

同视口整改前后对比见 [Figma 审计板](https://www.figma.com/design/oXg4tTuyCZP3fCy8wYZUVx)。

## 核心截图

| 文件 | 场景 | 视口 / 来源 |
|---|---|---|
| `01-lobby.png` | 对战大厅 | 1440x810，Playwright |
| `02-mulligan.png` | P1 起手调度 | 1440x900，双客户端正式流程 |
| `03-main-action.png` | P1 主行动 | 1440x900，双客户端正式流程 |
| `04-complex-prompt.png` | 支付费用复杂提示 | 1440x900，Playwright |
| `05-result.png` | P1 胜利结算 | 1440x900，双客户端正式流程 |
| `06-mobile.png` | 移动牌桌，包含左右战场分段切换 | 390x844，Playwright |
| `07-wide.png` | 宽屏牌桌 | 1920x1080，Playwright |
| `08-populated-board.png` | 双方基地、战场、放逐、符文与手牌均有内容的布局图例 | 1440x900，Playwright |

`formal-18/` 保留起手、主行动、单位移动和结算四个节点的 P1/P2 双视角原始截图。

## 空间参考

- [Steam Workshop: Riftbound - LGS Table](https://steamcommunity.com/sharedfiles/filedetails/?id=3606647746)：参考独立双战场、横向基地带和两侧资源区。
- [Riftbound playmat layout discussion](https://www.reddit.com/r/riftboundtcg/comments/1p6094g/is_it_just_me_or_does_the_common_playmat_layout/)：参考基地与符文需要比常见单人垫更大的空间，以及两张战场应明确分区。

以上仅作为空间与交互参考，不作为规则依据。

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

覆盖视口：1920x1080、1440x900、1280x720、390x844。浏览器检查包括无文档级溢出、桌面约 34% 的战场比例、15% 手牌轨、传奇/英雄/基地最小尺寸、牌库与符文最小尺寸、上下公开区和资源轨相对牌桌中心的 180° 镜像误差不超过 2px、诊断入口与资源区零遮挡、双方基地同宽且桌面各完整显示六个独立视觉槽、移动端左右战场切换可达、按滚动容器真实裁剪边界计算的可见卡牌与资源区零交叠、官方卡图无重复覆盖标签、卡牌完整可见、直接点选、Esc 清除、复杂提示、键盘可访问性和 Axe 扫描。
