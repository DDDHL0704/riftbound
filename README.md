# Riftbound .NET Migration Workspace

本项目是《符文战场》从 Java/Spring 迁移到 C#/.NET 10 + ASP.NET Core + SignalR 的并行开发工作区。

目标不变：服务端权威、双人联机、玩家只提交意图、服务端自动结算、事件可回放、断线可重连、长期可生产化维护。

## 当前状态

- 这是第一阶段骨架，不是可替代 Java 服务端的完整实现。
- 当前机器已安装 .NET 10 SDK、PostgreSQL 16、Redis 和 Node 24。
- 五份官方 PDF 规则文档与官网卡牌快照是最终规则权威。
- Java 项目 `/Users/dinghaolin/MyProjects/riftbound-server` 只作为旧实现行为参考、fixture 导出工具和回归对照，不再作为最终规则裁判。
- 迁移验收以 PDF/FAQ 规则依据 + command log -> events -> player snapshots 的 conformance tests 为准。
- P3 卡牌数据与行为系统已完成只读规格层：`1009/1009` 官方卡 schema valid、`811/811` functional units stable id、`1009/1009` BehaviorSpec 可展示，模板执行器仅作为骨架路由，不替换 P2 手写规则。
- P4 已进入高频关键词与基础卡牌小批次：P4.1 完成 template delegation bridge，P4.2 完成 `迅捷` / `反应` / `急速` 权限关键词最小模型，P4.3 完成 `瞬息` 控制者开始阶段到期摧毁，P4.4 完成 `回响` mana-only optional cost/repeat 显式模型，P4.5 完成 `draw` / `damage` / `destroy` / `stun` / `temp_might` primitive plan 小批次，P4.6 完成 `强攻` / `坚守` / `壁垒` / `后排` / `游走` combat keyword profile，P4.7 完成 `狩猎` / `等级` / `鼓舞` / `法盾` resource keyword profile，P4.8 完成 `装配` / `灵便` / `百炼` equipment keyword profile，P4.9 完成 lifecycle / interaction / basic-action 剩余 profile 与 completion audit，P4.10 完成固定数值“打出时获得经验”执行切片，P4.11 完成固定经验额外费用减费执行切片，P4.12 完成 `法盾` 法术目标税最小执行切片，P4.13 完成《灼焰飞龙》`HASTE_READY` 代表可选费用切片，P4.14 完成《诺克萨斯新兵》`鼓舞` 费用减免代表切片，P4.15 完成《踏苔蜥》`等级3` 入场 +1/法盾代表切片，P4.16 完成《风行狐》`等级3` 入场 +1/游走代表切片，P4.17 完成《无极学徒》`等级6` 打出抽 1 代表切片，P4.18 完成《小鲨鱼》`HASTE_READY` 急速活跃代表切片，P4.19 完成《严厉军士》动态经验代表切片，P4.20 完成《军团后卫》`HASTE_READY` 第三代表切片，P4.21 完成《崔法利求战者》`鼓舞` 自增益代表切片，P4.22 完成《危险二人组》`鼓舞` 目标临时战力代表切片，P4.23 完成《垃圾场小霸王》`鼓舞` 弃 2 抽 2 代表切片，P4.24 完成《先锋队长》`鼓舞` 随从指示物代表切片，P4.25 完成《树根先生》`HASTE_READY` 第四代表切片，P4.26 完成《机械迷》`HASTE_READY` 第五代表切片，P4.27 完成《琢珥鱼》`HASTE_READY` 第六代表切片，P4.28 完成《易》`等级6` 法盾/游走代表切片，P4.29 完成《易》A 版本 `等级6` 法盾/游走代表切片，P4.30 完成《卡银娜·薇蕊泽》`HASTE_READY` 第七代表切片，P4.31 完成《绯红印记树怪》`HASTE_READY` 第八代表切片，P4.32 完成《美味仙灵》`HASTE_READY` 第九代表切片，P4.33 完成《艾克》`HASTE_READY` 第十代表切片，P4.34 完成《武装强袭者》`HASTE_READY` 第十一代表切片，P4.35 完成《远古战狂》`HASTE_READY` 第十二代表切片，P4.36 完成《海妖猎手》`HASTE_READY` 第十三代表切片，P4.37 完成《李青》`HASTE_READY` 第十四代表切片，P4.38 完成《李青》A 版本 `HASTE_READY` 第十五代表切片，P4.39 完成《千尾监视者》`HASTE_READY` 第十六代表切片，P4.40 完成《卡莎》`HASTE_READY` 第十七代表切片，P4.41 完成《雷克塞》`HASTE_READY` 第十八代表切片，P4.42 完成《雷克塞》A 版本 `HASTE_READY` 第十九代表切片；当前只接入已验证的小批次可玩路径。

## 新窗口接手

如果在新的 Codex 窗口继续开发，先读：

1. `docs/CURRENT_P4_STATUS.md`
2. `docs/CURRENT_P3_STATUS.md`
3. `docs/CURRENT_P2_STATUS.md`
4. `docs/CURRENT_P2_5_STATUS.md`
5. 本 `README.md`（如果不是从这里进入）
6. `docs/rules-evidence-index.md` 中目标卡牌对应行
7. `docs/conformance-fixture-format.md` 中 fixture schema 规则

`docs/CURRENT_P4_STATUS.md` 是当前短交接入口；`docs/CURRENT_P3_STATUS.md`、`docs/CURRENT_P2_STATUS.md` 和 `docs/CURRENT_P2_5_STATUS.md` 保留上一阶段完成状态。`docs/START_HERE.md` 保留项目边界、资料优先级、P1/P2 禁止范围和验收门禁，默认按需读取。更完整的计划/审计文档也按需读取，避免每次新窗口加载重复长清单。

## 推荐本地准备

```bash
source scripts/dev-env.sh
dotnet --info
dotnet restore Riftbound.slnx
dotnet build Riftbound.slnx
dotnet test Riftbound.slnx --no-build
```

当前本机已用 user-local 方式安装 .NET 10 SDK 到 `~/.dotnet`。`scripts/dev-env.sh` 会把 `.NET 10`、`Node 24`、`PostgreSQL 16` 放到当前 shell 的 `PATH` 前面。

本地服务验证：

```bash
ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
curl http://127.0.0.1:5088/health
curl http://127.0.0.1:5088/catalog/summary
curl http://127.0.0.1:5088/catalog/p3-status
```

P2.5 开发期测试 UI：

```bash
source scripts/dev-env.sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run --project src/Riftbound.Api/Riftbound.Api.csproj
```

另开一个终端：

```bash
source scripts/dev-env.sh
cd src/Riftbound.DevUi
npm install
npm run dev
```

访问 `http://127.0.0.1:5173` 后，使用默认 `server URL = http://127.0.0.1:5088`，点击 `Join Both`、`Ready Both`、`Snapshot Both`。开发测试台包含 Battle Desk、Operation Panel、Debug Panel、`PLAY_CARD` builder、手写 JSON `SubmitIntent` 面板、fixture draft，以及 Development-only scenario seeds：`basic-play`、`movement`、`spell-duel`、`equipment`、`control`、`battle-score`、`specified-hand`。P2.5 UI 只显示和转发服务端 `Snapshot`、`Prompt`、`Events`、错误和命令日志，不做规则裁定。

## 项目结构

| 路径 | 职责 |
|---|---|
| `src/Riftbound.Contracts` | 协议 DTO、命令 DTO、事件、快照 envelope 和 P3 BehaviorSpec DTO。 |
| `src/Riftbound.Engine` | `RoomActor/MatchSession`、`RuleEngine`、命令串行和幂等边界。 |
| `src/Riftbound.Persistence` | PostgreSQL P1 event store schema 与 `IMatchJournal` 实现。 |
| `src/Riftbound.CardCatalog` | 官网卡牌快照加载、schema 校验、功能逻辑单元分组、规则文本解析和 BehaviorSpec 生成。 |
| `src/Riftbound.Api` | ASP.NET Core + SignalR `GameHub`。 |
| `src/Riftbound.DevUi` | P2.5 React + Vite 开发期 GameHub 测试 UI。 |
| `tests/Riftbound.ConformanceTests` | PDF/FAQ 规则依据、Java legacy oracle fixture 与 C# 回放结果对比。 |
| `data/official` | 官网卡牌快照，当前 1009 条官方图鉴条目。 |
| `scripts/dev-env.sh` | 本机开发 shell 环境入口。 |
| `docs/` | 阶段计划、架构和验收记录。 |

核心计划文档：

- `docs/CURRENT_P4_STATUS.md`：当前短交接，记录 P4 高频关键词/基础模板候选、风险分层、P4.1-P4.30 完成状态和下一批计划。
- `docs/CURRENT_P3_STATUS.md`：当前短交接，记录 P3 卡牌数据、BehaviorSpec、解析管线、模板骨架和验证状态。
- `docs/CURRENT_P2_STATUS.md`：新窗口短交接，记录 P2 功能基线提交、测试状态、P2 进度和下一步。
- `docs/CURRENT_P2_5_STATUS.md`：P2.5 开发期测试 UI 状态、运行方式和浏览器 smoke 记录。
- `docs/START_HERE.md`：项目边界、资料优先级、开发顺序和验收门禁。
- `docs/rules-authority-and-audit.md`：五份 PDF 的规则权威、冲突裁决和已开发部分重审协议。
- `docs/development-audit-status.md`：当前已开发内容的保留、修改和重审状态。
- `docs/rules-evidence-index.md`：五份 PDF/FAQ 到规则域、fixture 和实现状态的证据索引。
- `docs/rules-card-baseline.md`：规则 PDF/FAQ 与官网卡牌快照基线。
- `docs/p2-rules-preflight.md`：进入 P2 核心规则前的符文池、回合、优先权、焦点和清理预检清单。
- `docs/master-development-plan.md`：后续开发主线和阶段验收标准。
- `docs/phase-1.md`：第一阶段联机底座任务清单。
- `docs/conformance-fixture-format.md`：规则依据、Java legacy oracle 与 C# runner 共用的 fixture JSON 契约。

## 第一阶段原则

1. 不迁移全部卡牌。
2. 不重做完整 UI。
3. 不绕过五份 PDF 和官网卡面依据。
4. 不提交规则 PDF/FAQ。
5. 每个已迁移能力必须能从 command log 回放；Java 输出只能作为旧实现对照，不能覆盖 PDF/FAQ 裁决。
