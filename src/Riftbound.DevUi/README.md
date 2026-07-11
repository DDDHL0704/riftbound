# Riftbound Web 客户端

React + Vite Web 客户端只渲染玩家作用域内的服务端快照、事件与行动提示，并向服务器提交命令意图。前端不判断规则，也不推断隐藏卡牌身份。

## 本地运行

仓库根目录启动开发 API：

```bash
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound="" \
dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
```

另开终端启动 Web 客户端：

```bash
npm --prefix src/Riftbound.DevUi install
npm --prefix src/Riftbound.DevUi run dev
```

访问 `http://127.0.0.1:5173/`。

## 对局桌面

- 桌面首屏把公共战场收敛到有效牌桌高度的约 34%，为传奇、英雄、分槽基地、牌库和符文保留更大的上下玩家区。
- 本玩家手牌位于底部独立横向队列，双方手牌轨静止高度为牌桌的 15%；符文、手牌、主牌堆和相关牌堆按牌桌中心作 180° 镜像，区域互不遮挡。
- 两张战场牌位于公共战场左右两侧。双方公开区同样以牌桌中心作 180° 镜像：我方依次为传奇、英雄、基地、分数，对手对应为分数、基地、英雄、传奇。
- 双方基地至少展示六个同宽、可扩展的视觉卡槽，桌面首屏完整显示六格，移动端至少完整显示当前一格。每个单位独占一格，视觉卡槽不构成规则容量。
- 牌桌缩略图直接显示官方完整卡面，不再叠加重复的费用、力量或名称方框。
- 直接点击服务器标记为合法的来源、位置和目标；上下文托盘只提交服务器候选命令。按 `Esc` 清除本地选择草稿。
- 起手调度、支付费用、分配战斗伤害、触发排序和手牌选择使用居中复杂提示。
- 连接、日志和规则细节从牌桌资源区移到顶栏图标，桌面端按需打开右侧“连接与规则诊断”抽屉；移动端隐藏该诊断入口，避免覆盖牌桌。
- 对手手牌与暗牌只显示卡背及数量；桌面收束为单张卡背加数量，移动端保留卡背队列。任何视口都不使用正面图或身份作为背景。

桌面优先视口为 1440x900 与 1920x1080；1280x720 保持核心操作可达；390x844 使用独立移动布局，并通过“左战场 / 右战场”分段控件或触控滑动切换公共战场。

## 验收

```bash
npm --prefix src/Riftbound.DevUi run build
npm --prefix src/Riftbound.DevUi run qa:appshots
npm --prefix src/Riftbound.DevUi run smoke:chrome -- --start-api
RIFTBOUND_E2E_HEADED=1 npm --prefix src/Riftbound.DevUi run e2e:formal-18 -- --start-api
```

截图和双客户端流程记录见 [`artifacts/web-arena`](./artifacts/web-arena/README.md)。
