# Riftbound .NET Migration Workspace

本项目是《符文战场》从 Java/Spring 迁移到 C#/.NET 10 + ASP.NET Core + SignalR 的并行开发工作区。

目标不变：服务端权威、双人联机、玩家只提交意图、服务端自动结算、事件可回放、断线可重连、长期可生产化维护。

## 当前状态

- 这是第一阶段骨架，不是可替代 Java 服务端的完整实现。
- 当前机器已安装 .NET 10 SDK、PostgreSQL 16、Redis 和 Node 24。
- 五份官方 PDF 规则文档与官网卡牌快照是最终规则权威。
- Java 项目 `/Users/dinghaolin/MyProjects/riftbound-server` 只作为旧实现行为参考、fixture 导出工具和回归对照，不再作为最终规则裁判。
- 迁移验收以 PDF/FAQ 规则依据 + command log -> events -> player snapshots 的 conformance tests 为准。

## 新窗口接手

如果在新的 Codex 窗口继续开发，先读：

1. `docs/START_HERE.md`
2. `docs/master-development-plan.md`
3. `docs/phase-1.md`
4. `docs/rules-authority-and-audit.md`
5. `docs/development-audit-status.md`
6. `docs/rules-card-baseline.md`

`docs/START_HERE.md` 是防偏离入口：它记录当前目标、资料优先级、立即开发顺序、P1 禁止范围和验收门禁。

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
```

## 项目结构

| 路径 | 职责 |
|---|---|
| `src/Riftbound.Contracts` | 协议 DTO、命令 DTO、事件和快照 envelope。 |
| `src/Riftbound.Engine` | `RoomActor/MatchSession`、`RuleEngine`、命令串行和幂等边界。 |
| `src/Riftbound.Persistence` | PostgreSQL P1 event store schema 与 `IMatchJournal` 实现。 |
| `src/Riftbound.CardCatalog` | 官网卡牌快照加载、功能逻辑单元分组。 |
| `src/Riftbound.Api` | ASP.NET Core + SignalR `GameHub`。 |
| `tests/Riftbound.ConformanceTests` | PDF/FAQ 规则依据、Java legacy oracle fixture 与 C# 回放结果对比。 |
| `data/official` | 官网卡牌快照，当前 1009 条官方图鉴条目。 |
| `scripts/dev-env.sh` | 本机开发 shell 环境入口。 |
| `docs/` | 阶段计划、架构和验收记录。 |

核心计划文档：

- `docs/START_HERE.md`：新窗口接手指南，先读它以恢复目标和当前阶段。
- `docs/rules-authority-and-audit.md`：五份 PDF 的规则权威、冲突裁决和已开发部分重审协议。
- `docs/development-audit-status.md`：当前已开发内容的保留、修改和重审状态。
- `docs/rules-card-baseline.md`：规则 PDF/FAQ 与官网卡牌快照基线。
- `docs/master-development-plan.md`：后续开发主线和阶段验收标准。
- `docs/phase-1.md`：第一阶段联机底座任务清单。
- `docs/conformance-fixture-format.md`：规则依据、Java legacy oracle 与 C# runner 共用的 fixture JSON 契约。

## 第一阶段原则

1. 不迁移全部卡牌。
2. 不重做完整 UI。
3. 不绕过五份 PDF 和官网卡面依据。
4. 不提交规则 PDF/FAQ。
5. 每个已迁移能力必须能从 command log 回放；Java 输出只能作为旧实现对照，不能覆盖 PDF/FAQ 裁决。
