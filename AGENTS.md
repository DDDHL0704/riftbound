# 符文战场项目开发指南（Codex / Claude 通用）

2026-10-29 起采用快速迭代模式。旧的五窗协作、治理台账和 L2/L3 证据流程已退役；历史文档在 Git 标签 `archive/pre-reset-20261029` 中可找回，不再作为任务、准入或认证依据。

## 目标与分工

- **目标**：尽快把服务端规则开发完毕——全部已接入卡牌的效果正确，剩余官方卡牌接入，夜间完整验收稳定通过。随后由 Claude 适配前端。
- **服务端**：Codex 四条线并行，从 [待办清单](docs/BACKLOG.md) 领任务。
  - **A 线**：P0 夜间验收、L2 提速（最优先）、P3 工程项、CI 与工具，以及 RAD 中国区生效确认；不做 P1。提速只改测试、夹具、CI 和工具；必须改共享流程时，先查看 C 线打开的 PR，避免改同一方法。
  - **B 线**：VEN/RAD 传奇、英雄单位、专属法术、专属装备及现有卡牌 PR，按待办清单逐卡验证。
  - **C 线**：P1-1 至 P1-5 的全部规则缺陷与旧 PR，包括征服时机、费用候选和待命支付。
  - **D 线**：其余单位、法术、装备和战场，按待办清单分配。
- **前端**：Claude 负责 `clients/web`；只在用户要求时参与服务端。
- 只有两条线要同时改**同一个方法**时才需要协调：先合入的一方优先，另一方同步 main 后再改。其余情况各自开发、各自合入，不需要审批或确认往返。

## 每个任务的流程

1. 从 `origin/main` 新建分支：对应各线使用 `a/<主题>`、`b/<主题>`、`c/<主题>` 或 `d/<主题>`（前端用 `web/<主题>`）。一个分支只做一件事，不叠加依赖 PR。
2. 实现并补测试；本地只跑相关测试（见下）。
3. 推送并开 PR，PR 描述写清改了什么、依据哪条官方规则或卡牌文本、跑了哪些测试。
4. `L2 required` 通过后**直接合入**：`gh pr merge <号> --merge --delete-branch`；仓库开启自动合并后可以改用 `--auto`。PR 不要求先同步最新 main；有冲突时再同步。
5. 合入后在 [BACKLOG](docs/BACKLOG.md) 勾掉对应条目，或把新发现的问题追加进去。这是唯一需要维护的状态文档。

小步快跑：一个 PR 最好在一天内合入；大改动拆成几个可以各自合入的 PR。不要再写阶段报告、证据台账或逐卡审计文档。确有长期价值的设计决定，写一段到 `docs/adr/`。

## 本地运行与测试

需要 .NET 10 SDK；Python 3 仅用于脚本。

```sh
# 内存模式后端（前端联调用）
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088 \
ConnectionStrings__Riftbound="" dotnet run --project src/Riftbound.Api

# 构建与定向测试（按类名或方法名过滤）
DOTNET_PROCESSOR_COUNT=1 dotnet build tests/Riftbound.ConformanceTests -m:1
DOTNET_PROCESSOR_COUNT=1 dotnet test tests/Riftbound.ConformanceTests --no-build -m:1 --filter "FullyQualifiedName~Teemo"

# 按类名或方法名运行快速回归
python3 scripts/test-rules.py focus --match Teemo --jobs 1

# 脚本自身的测试
python3 -m unittest discover -s scripts/tests
```

本机 10 核 / 24 GB 内存由四条线共用。本地构建和测试必须使用 `-m:1`、`--jobs 1`；只运行按类名或方法名过滤的规则测试，不在本地跑完整规则套件。`dotnet` 的 PATH 包装全机最多放行两个重任务，负载高时排队是正常现象，不要绕过包装或使用 SDK 的绝对路径。完整规则套件、真实 PostgreSQL、SignalR 和进程重启只在 GitHub Actions 里跑。

## CI

- **`Backend L2`**（PR 与 main）：构建一次，全部规则测试分 9 个 Linux ARM64 分片并行，外加两局完整对局。检查名 `L2 required` 是合入的唯一门槛。同一 PR 推送新提交会取消旧的运行。
- 公开仓库免费账户约有 20 个并发任务名额。常规 L2 共 11 个任务，规则峰值为 9 个；文档 PR 只需两个轻量任务。按全仓任务数控制负载，避免叠加依赖尚未合入分支的 PR；及时取消被新提交取代或已关闭 PR 的运行。
- **`Backend L3 nightly`**：每天北京时间 02:00 对 main 跑完整验收（全部规则、PostgreSQL 分区、SignalR、进程重启、回放）。失败时由 A 线在 BACKLOG 的 P0 里登记并修复；它不阻塞无关的合入。
- 改了 `.github/workflows` 或 CI 脚本时，`backend-harness` 会额外检查验收驱动。

## 规则依据

- 官方来源快照在 `data/official/`：中文卡牌目录 `card-catalog.zh-CN.json`，以及 `upstream/` 下按日期保存的官方规则、FAQ 和勘误抓取。中国区官方中文规则、FAQ、勘误和禁卡表是依据；社区站点、TTS 和旧 Java 实现只能作参考。
- 拿不准的规则先查官方规则或 FAQ 原文，在 PR 描述里写明条款号。查不到依据时，在 BACKLOG 里标 `待确认`，不要凭猜测实现。
- 关键词、时机、费用、区域、触发等通用机制必须在共享流程里实现，不要为单张卡写绕过逻辑。

## 必须守住的底线

- 服务端权威：客户端只提交意图；付款、伤害、控制权、触发顺序和胜负都由服务端裁定。
- 隐藏信息：任何投影、提示和日志都不能泄露对手手牌、牌库顺序、未公开符文和背面待命牌。
- 被拒绝的命令不能改变状态；同一命令重复提交结果一致。
- 断线重连、持久化恢复和回放必须和实时对局得到同样的结果。不要为了让新代码通过而改写已接受的历史记录。
- 开发中的旧版本对局和协议不需要兼容，直接替换旧实现即可。
- 删除代码前确认没有调用方，并同步删除或改写对应测试。

## 项目结构

| 路径 | 内容 |
|---|---|
| `src/Riftbound.Contracts` | 命令、事件、快照、提示 |
| `src/Riftbound.CardCatalog` | 官方卡池加载与解析 |
| `src/Riftbound.Engine` | 规则引擎：合法性、结算、投影、回放、恢复（`MatchSession.cs` 是主入口） |
| `src/Riftbound.Persistence` | PostgreSQL 日志与恢复 |
| `src/Riftbound.Api` | HTTP / SignalR、房间、匹配、战绩 |
| `clients/web` | 网页对战前端（Claude 维护，见其 README） |
| `clients/godot`、`src/Riftbound.DevUi` | 原生客户端与调试界面（暂停迭代） |
| `tests/Riftbound.ConformanceTests` | 规则与卡牌测试（主测试项目） |
| `tests/Riftbound.*Acceptance` | 夜间验收用的真实数据库与网络测试 |
| `scripts/` | 测试与 CI 工具 |
| `data/official/` | 官方来源快照 |
| `docs/BACKLOG.md` | 唯一的待办与状态 |
| `tests/Riftbound.ConformanceTests/Fixtures/official-source/` | 解析与官方卡牌身份测试使用的来源快照 |

## 工作环境

- 每条线在自己的工作树里开发：`git worktree add ../.worktrees/riftbound-<线名> -b <分支> origin/main`。用完就删，不要保留冻结树或大体积产物。
- 本地产物放在被忽略的 `var/` 下；Git 里只放源码、测试和必要的小数据。
- 外接盘可用空间低于 20 GB 时，先清理 `var/` 和不用的工作树。
