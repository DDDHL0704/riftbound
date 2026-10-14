# 规则开发测试工作流

开发中优先获得与本次改动相关的反馈。完整对局矩阵保留在代码 CI、集成和最终验收；明确治理文档白名单使用轻量检查。

## 日常入口

在仓库根目录执行，Python 只用标准库，脚本自动定位本机 .NET；macOS/Linux 可用 `python3`，Windows 可用 `python`。

| 命令 | 范围 | 使用时机 |
|---|---|---|
| `python3 scripts/test-rules.py focus recast` | 重打、效果出牌、征服、相关费用与触发案例 | 修改对应规则时快速迭代 |
| `python3 scripts/test-rules.py focus payment` | 费用、符文、资源、金币和法盾案例 | 修改支付逻辑时 |
| `python3 scripts/test-rules.py focus --match OfficialMechanicalRecastTests` | 明确指定的测试名；可重复 `--match` | 复现及修复具体问题 |
| `python3 scripts/test-rules.py focus --include-games --match PreconstructedCatalogDecks` | 显式选取匹配的完整对局，仍校验实际方法覆盖 | 只需复核特定完整对局或测量瓶颈时 |
| `python3 scripts/test-rules.py check` | 完整非 FullGameEndToEnd 测试集，加两条指定完整对局冒烟 | 一个连贯批次的默认收尾检查 |
| `python3 scripts/test-rules.py full` | 所有实时发现的测试，并行分进程执行 | 广泛跨规则改动、集成与发布验收、CI |

其他域为 `triggers`、`combat`、`recovery`。域匹配是可检查的用例筛选，不是自动证明源代码依赖完整；修改相邻机制时加跑对应域，未知或广泛影响时使用 `check` / `full`。不能用 `focus` 的结果宣称整套规则完成。

原生控件、网络与重连、完整双客户端实战、打包和 Windows 实机仍是独立验收项。工具不启动、重启服务器或改写正在玩的对局。

## 提速方式

1. 只做一次增量构建和测试发现；执行分片时使用 `--no-build --no-restore`。没有盲目复用旧测试通过结果，也没有绕过编译。
2. 主要瓶颈是 `FullGameEndToEndTests` 中同一测试类串行运行的完整对局。全量将该类按方法分到独立进程，其他测试保持原来的 xUnit 类间并发。
3. 用实际 TRX 耗时平衡进程；无本机历史时使用 `scripts/rule-test-timings.json`。耗时数据只影响调度，不控制测试是否纳入；新增测试即使没有历史耗时也必须执行。
4. 默认并发不超过 4 个进程，可用 `--jobs 2` 降低机器负载。完整预组交叉对局仍保留同一测试身份；其全部双向配对最多两场并行，每场独立状态、固定原种子、journal、胜利与回放检查。实际完成配对集合必须与全部期望配对完全相等，TRX 输出每场最终状态哈希。没有删对局、减少随机种子或删除断言。
5. 开发过程中 `focus` → 连贯批次一次 `check` → 必要时 `full`；代码 CI 保留完整测试。普通规则首批启动，同时尽早启动最长对局；只改变调度，不改变纳入范围。不要为同一个无变化输入反复跑完整矩阵。

## 防止提速掩盖失败

- 根据实时 `--list-tests` 生成计划，不维护固定用例白名单。无法解析发现结果或筛选为零项时失败退出。
- 分片计划确保每个测试方法恰好属于一片；实际 TRX 必须覆盖该片全部方法，不允许多出其他片的方法。
- 每个分片必须正常退出、生成非空 TRX、全部通过且零跳过，否则立即失败，并停止本次工具启动的其他测试进程。
- 动态 Theory 可能在发现时只有方法、运行时才展开数据行，所以日常完整性按方法检查，最终用例数来自真实 TRX；不将发现行数当作真实测试数。
- 对未修改的测试程序集进行性能对照时，可用 `--expect-trx <baseline.trx>` 强制核对完整用例名称与数量的多重集合。这个选项用于对照；正常添加测试后不应继续使用旧基线约束用例数。
- `Ctrl+C` 会终止该次工具启动的测试进程树；不会按进程名关闭其他开发服务。

每次运行输出到新的 `var/qa/rules-tests/<timestamp>`。可用 `--output <新目录>` 指定目录；已有运行目录禁止复用，避免误读旧 TRX。目录包含增量构建日志、发现结果、完整分片计划、各片日志／TRX，以及通过后才写入的 `summary.json`。完整计划还保存测试程序集 SHA-256。

检查计划而不执行测试：

```sh
python3 scripts/test-rules.py full --plan-only
```

显式性能对照：

```sh
python3 scripts/test-rules.py full --jobs 4 \
  --expect-trx tests/Riftbound.ConformanceTests/TestResults/mechanical-full.trx
```

过滤表达式采用 [.NET 官方支持的测试筛选机制](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests)。所有命令以参数数组启动，不经 shell 拼接。

本次实测和限制见 [性能验证记录](RULE_TEST_SPEED_2026-10-09.md)。

2026-10-12 用户确认：连贯批次默认一次 `check`，固定提交的代码 CI 在后台执行 `full`，可继续下一批开发；明确文档白名单走轻量检查。广泛影响、集成与最终候选仍执行完整验收。单个矩阵的并行实测记录在 [本轮提速记录](RULE_TEST_SPEED_2026-10-12.md)。`focus` 默认继续排除完整对局，只有显式 `--include-games` 才允许匹配的完整对局进入计划。

## 后台 CI 与轻量文档检查

完整 CI 验证固定提交，记录 run ID、输入/程序集哈希、真实 TRX 和方法/用例/配对身份。开发可继续；本地并行必须使用独立固定 checkout，不能修改正在执行的源文件或 bin。被取消或未完成的运行不能标为通过。

scripts/ci-change-scope.py 只豁免逐项核查过的精确治理文档路径，不使用 docs/ 或 *.md 通配。docs/DEPLOYMENT.md、docs/evidence、官方数据、fixture、打包文档、脚本、构建配置及未知路径仍 full；缺基线、类型/执行位变化及不确定差异同样 full。范围判定失败时完整任务仍会运行。纯文档工作流不会取消此前代码的完整任务；不同代码 SHA 的验收也独立，仅相同 SHA 的重复任务可取消重复。

轻量 CI 执行 Python 验证工具检查、固定差异格式、当前治理引用及来源索引完整性，报告规则/打包未执行。来源检查默认读取 docs/audit/current-manifest.json，核对当前卡牌、保留原确认日期的规则索引、限定审查和来源增量，不下载官网、不执行游戏、不提升 semanticCoverageCertified。显式 --date 2026-10-11 单独检查历史索引，不能替代当前 manifest。来源变化必须登记增量与影响，不静默改变冻结分母。

```sh
python3 -m unittest discover -s scripts/tests -v
python3 scripts/audit/check-project-governance.py --output var/qa/current-governance.json
python3 scripts/audit/check-project-governance.py --date 2026-10-11
```

轻量成功与上一完整基线分开记录。最终发布候选仍等待完整回归与相应原生、双客户端及 Windows 验收；后续需要真实 Postgres 证据时必须明确提供连接并验证实际执行。

浅 checkout 中若多提交 push 的中间祖先不可确定，即使可取得 base，也保守执行 full；不会把历史不完整当作文档豁免。只有范围任务成功且明确返回 false 才跳过完整任务，任何空/异常输出仍 full。

Desktop 矩阵始终生成既有检查名。明确文档范围时在 Ubuntu 写入打包未执行说明，跳过所有原生下载/构建/打包；其他或不确定范围使用实际矩阵 OS 执行完整步骤。文档并发组与 full 分开，同 SHA 的文档任务也不取消手动 full。
