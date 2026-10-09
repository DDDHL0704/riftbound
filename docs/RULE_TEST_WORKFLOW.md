# 规则开发测试工作流

开发中优先获得与本次改动相关的反馈。完整对局矩阵保留在阶段性验收和 CI，不必在每次小修正后重新遍历全部组合。

## 日常入口

在仓库根目录执行，Python 只用标准库，脚本自动定位本机 .NET；macOS/Linux 可用 `python3`，Windows 可用 `python`。

| 命令 | 范围 | 使用时机 |
|---|---|---|
| `python3 scripts/test-rules.py focus recast` | 重打、效果出牌、征服、相关费用与触发案例 | 修改对应规则时快速迭代 |
| `python3 scripts/test-rules.py focus payment` | 费用、符文、资源、金币和法盾案例 | 修改支付逻辑时 |
| `python3 scripts/test-rules.py focus --match OfficialMechanicalRecastTests` | 明确指定的测试名；可重复 `--match` | 复现及修复具体问题 |
| `python3 scripts/test-rules.py check` | 完整非 FullGameEndToEnd 测试集，加两条指定完整对局冒烟 | 一个连贯批次的默认收尾检查 |
| `python3 scripts/test-rules.py full` | 所有实时发现的测试，并行分进程执行 | 广泛跨规则改动、集成与发布验收、CI |

其他域为 `triggers`、`combat`、`recovery`。域匹配是可检查的用例筛选，不是自动证明源代码依赖完整；修改相邻机制时加跑对应域，未知或广泛影响时使用 `check` / `full`。不能用 `focus` 的结果宣称整套规则完成。

原生控件、网络与重连、完整双客户端实战、打包和 Windows 实机仍是独立验收项。工具不启动、重启服务器或改写正在玩的对局。

## 提速方式

1. 只做一次增量构建和测试发现；执行分片时使用 `--no-build --no-restore`。没有盲目复用旧测试通过结果，也没有绕过编译。
2. 主要瓶颈是 `FullGameEndToEndTests` 中同一测试类串行运行的完整对局。全量将该类按方法分到独立进程，其他测试保持原来的 xUnit 类间并发。
3. 用实际 TRX 耗时平衡进程；无本机历史时使用 `scripts/rule-test-timings.json`。耗时数据只影响调度，不控制测试是否纳入；新增测试即使没有历史耗时也必须执行。
4. 默认并发不超过 4 个进程，可用 `--jobs 2` 降低机器负载。完整预组交叉对局仍作为一个完整用例运行，没有删对局、减少随机种子、删除断言或缩短获胜／回放检查。
5. 开发过程中 `focus` → 批次 `check` → 必要时 `full`；CI 仍执行完整测试。不要为同一个无变化的工作区反复跑完整矩阵。

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
