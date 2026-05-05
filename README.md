# Riftbound .NET Migration Workspace

本项目是《符文战场》从 Java/Spring 迁移到 C#/.NET 10 + ASP.NET Core + SignalR 的并行开发工作区。

目标不变：服务端权威、双人联机、玩家只提交意图、服务端自动结算、事件可回放、断线可重连、长期可生产化维护。

## 当前状态

- 当前机器已安装 .NET 10 SDK、PostgreSQL 16、Redis 和 Node 24。
- 五份官方 PDF 规则文档与官网卡牌快照是最终规则权威。
- Java 项目 `/Users/dinghaolin/MyProjects/riftbound-server` 只作为旧实现行为参考、fixture 导出工具和回归对照，不再作为最终规则裁判。
- 迁移验收以 PDF/FAQ 规则依据 + command log -> events -> player snapshots 的 conformance tests 为准。
- P1-P7 已完成：联机底座、核心规则、开发期测试 UI、卡牌数据/BehaviorSpec、高频关键词、装备/控制/触发/替换代表范围、全卡 P6 状态矩阵，以及产品级 Web 对战体验均已落地。
- P7 最终验证通过：后端 full test `2613/2613`，`ConformanceFixtureRunnerTests 2507/2507`，`CardCatalogBaselineTests 37/37`，`GameHubJoinTests 27/27`，前端 build 和 Browser smoke 均通过；详见 `docs/CURRENT_P7_STATUS.md`。
- 当前推进 P7.9 本地产品版全卡可玩：P7.9.6 已完成全部传奇规则域，P7.9.7 已进入战场规则域并完成四十个战场对象/战斗结果功能单元，最新新增 `UNL-211/219` 高费法术触发洞察并回收顶部牌；既有战场对象 destination、hold/conquer 事件、`UNL-205/219` 法术强化战场单位、`OGN·292/298` 友方法术指定抽牌、`SFD·213/221` 首个友方装备费用减少 1、`SFD·211/221` 友方回响费用减少 1、`SFD·216/221` 战场单位打出限制，以及 `UNL` / `OGN` / `SFD` 多个战场代表效果均已由后端结算。当前实现 `797/811` 个功能单元，manual deferred 剩余 `14/811`，全部集中在战场。详见 `docs/CURRENT_P7_9_STATUS.md`。

## 新窗口接手

如果在新的 Codex 窗口继续开发，先读：

1. `docs/CURRENT_P7_9_STATUS.md`
2. `docs/CURRENT_P7_STATUS.md`
3. `docs/CURRENT_P6_STATUS.md`
4. `docs/CURRENT_P5_STATUS.md`
5. `docs/CURRENT_P4_STATUS.md`
6. 本 `README.md`（如果不是从这里进入）
7. `docs/rules-evidence-index.md` 中目标卡牌对应行
8. `docs/conformance-fixture-format.md` 中 fixture schema 规则

`docs/CURRENT_P7_9_STATUS.md` 是当前短交接入口；`docs/CURRENT_P7_STATUS.md` 和 `docs/CURRENT_P6_STATUS.md` 是 P7.9 的直接基线。更早的 `CURRENT_*_STATUS.md` 保留上一阶段完成状态。`docs/START_HERE.md` 保留项目边界、资料优先级和验收门禁，默认按需读取。

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

访问 `http://127.0.0.1:5173` 后，使用默认 `server URL = http://127.0.0.1:5088`。P7 Web UI 已支持产品级房间入口、双玩家 ready、断线重连、对战桌面、ActionPrompt 操作、事件日志、战报/回放边界、图鉴和卡牌详情。P7.9 会继续把开发工具折叠到本地专用入口，并把手填 objectId/JSON 的操作推进为结构化 prompt 驱动的点击式流程。UI 只显示和转发服务端 `Snapshot`、`Prompt`、`Events`、错误和命令日志，不做规则裁定。

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

- `docs/CURRENT_P7_9_STATUS.md`：当前短交接，记录 P7.9 本地产品版全卡可玩目标、缺口审计、批次计划、验证门禁和进度。
- `docs/CURRENT_P7_STATUS.md`：P7 产品级 Web 对战完成状态、Browser smoke 和最终验证。
- `docs/CURRENT_P6_STATUS.md`：P6 全卡状态矩阵，保留 P6 final 的 `713/811` implemented 与 `98/811` manual deferred 边界；P7.9 当前数字以 `docs/CURRENT_P7_9_STATUS.md` 为准。
- `docs/CURRENT_P4_STATUS.md`：P4 高频关键词/基础模板候选、风险分层、P4.1-P4.392 完成状态、最终验证和下一阶段边界。
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
