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
- P4 已进入高频关键词与基础卡牌小批次：P4.1-P4.106 已完成 template delegation/primitive plan、权限关键词代表时机、`瞬息` 到期、`回响` mana-only、战斗/资源/装备/生命周期/互动/basic-action profile、固定/动态经验、经验费用、法盾法术目标税、法盾法术目标税费用不足拒绝、多目标法盾税聚合费用不足拒绝、《妖异狐火》总战力上限拒绝、多目标税聚合与友方目标 no-tax 边界、`ACTIVATE_ABILITY` / `HIDE_CARD` / `REVEAL_CARD` / `MOVE_UNIT` / `ASSEMBLE_EQUIPMENT` / `DECLARE_BATTLE` command 前置模型、《蔚》无目标付费技能入栈/结算代表路径、带目标/额外费用/费用不足/非《蔚》来源拒绝、对手控制来源拒绝和来源不在场上拒绝、《泽拉斯》带目标技能敌方法盾税、法盾税费用不足拒绝、己方法盾 no-tax、已横置来源拒绝、缺目标/多目标/额外费用/非泽拉斯来源/非单位目标/来源不在战场/对手控制来源拒绝、横置和伤害代表路径、场上对象 `cardNo` 身份边界、对手正面朝下对象 snapshot redaction、待命 `HIDE_CARD` 最小正面朝下放置和费用不足拒绝、`REVEAL_CARD` 基地显露、待命 `STANDBY_REACTION` 入栈代表路径、无优先权窗口拒绝和《游击战》`STANDBY_FREE` 免费待命暗置/无权限拒绝/非待命废牌堆目标拒绝代表路径、`PLAY_CARD` 伏击目的地前置模型及显式拒绝 fixture、`MOVE_UNIT` 游走/基础移动前置拒绝 fixture、`ASSEMBLE_EQUIPMENT` 装配前置拒绝 fixture、`DECLARE_BATTLE` 战斗声明前置拒绝 fixture、`预知` 非顶部牌目标拒绝 fixture、回收/放逐/增益 template skeleton、多条等级/鼓舞代表路径、34 条 `HASTE_READY` 急速代表路径及 power 不足拒绝 fixture、P4.58《取放自如》武装贴附/卸除代表路径，以及 P4.75 对 33 个显式目标 surface 的 baseline 覆盖审计；当前只接入已验证的小批次可玩路径，完整战斗、待命触发/完整隐藏区/目标伤害、伏击真实反应战场打出、游走真实移动、更多技能目标税/通用技能 registry、完整装备装配/灵便/百炼和复杂触发仍 deferred。

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

- `docs/CURRENT_P4_STATUS.md`：当前短交接，记录 P4 高频关键词/基础模板候选、风险分层、P4.1-P4.106 完成状态和下一批计划。
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
