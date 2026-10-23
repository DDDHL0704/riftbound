# 规则开发测试工作流

2026-10-23 当前范围见 [后端目标](BACKEND_RULES_GOAL_2026-10-23.md)。本页替换旧“所有批次check/full/全套归档”的频率；最终质量与原测试范围不变。历史版本见e9bdfd0d。

## 风险分级与实际状态

| 等级 | 判定 | 必需验证与记录 |
|---|---|---|
| L1 | 已验证机制上的独立卡牌接入或局部修复，影响已明确 | 源码、定向+受影响回归、合法/非法/边界；一行台账。累计check并入每日集成，full并入夜间 |
| L2 | 共享时序、权限/合法性、隐藏信息、事件/序列化、恢复/持久化、新机制或影响不明 | 完整机制模块定向+相邻域+受影响原完整对局；每实际集成批次一次check；按受影响层补SQL/API/SignalR/进程重启/原日志。安全/隐藏信息/幂等/拒绝不变性本批验证 |
| L3 | 每周里程碑、候选或发布/阶段完成 | 最新固定候选完整后端验收，全部原测试/11牌组110配对/seed424242、真实SQL、双玩家网络、权限、恢复、持久化后发布、完整原日志、正常/异常终局 |

**首版CI分级已在本地实现，远端执行待验。** 新增独立测试类可L1；已识别Engine/Contracts/Persistence/Api机制路径L2/check加全SQL/网络；未知、脚本/构建/来源、原完整矩阵、删除/类型变化，以及卡牌注册/目录新增组合仍full。卡牌L1自动判定尚未实现，不能自报降级。scoped_rules会重新读取固定Git差异并验证计划；稳定rules仍只汇总真正full，不把限定绿灯叫full。夜间和每周/手动触发完整回归，原分片不变。未执行/失败/缺证据标待验证。

统一增量行字段：`功能身份 | 精确印次映射 | 来源版本/条款及引用哈希 | 行为/测试定位 | 风险等级 | 源码提交 | 结果/未覆盖项 | 原件路径或runID | 净整卡认证增量`。机制分支通过与整卡状态分开；首2–3试点额外写大致编码/测试/整理耗时即可。卡牌窗口可用13行以内Markdown交付，主线集中汇入目标表，暂不要求复制历史manifest。

同固定输入跨窗口复用完成结果，仅补真实集成差异。冻结源/bin不可改写。新大日志/TRX/SQL/二进制只放ignored本地或CI artifacts；Git保留简短来源/测试/提交/结果/可取回路径，不再逐卡生成全库manifest和压缩包。保存策略见[治理](PROJECT_GOVERNANCE.md)。

## 日常入口

在仓库根目录执行，Python 只用标准库，脚本自动定位本机 .NET；macOS/Linux 可用 `python3`，Windows 可用 `python`。

| 命令 | 范围 | 使用时机 |
|---|---|---|
| `python3 scripts/test-rules.py focus recast` | 重打、效果出牌、征服、相关费用与触发案例 | 修改对应规则时快速迭代 |
| `python3 scripts/test-rules.py focus payment` | 费用、符文、资源、金币和法盾案例 | 修改支付逻辑时 |
| `python3 scripts/test-rules.py focus --match OfficialMechanicalRecastTests` | 明确指定的测试名；可重复 `--match` | 复现及修复具体问题 |
| `python3 scripts/test-rules.py focus --include-games --match PreconstructedCatalogDecks` | 显式选取匹配的完整对局，仍校验实际方法覆盖 | 只需复核特定完整对局或测量瓶颈时 |
| `python3 scripts/test-rules.py check` | 完整非 FullGameEndToEnd 测试集，加两条指定完整对局冒烟 | L2实际集成批次收尾，或L1每日累计 |
| `python3 scripts/test-rules.py full` | 所有实时发现的测试，并行分进程执行 | 夜间、L3里程碑/候选；当前代码CI仍full |

其他域为 `triggers`、`combat`、`recovery`。域匹配是可检查的用例筛选，不是自动证明源代码依赖完整；修改相邻机制时加跑对应域，未知或广泛影响时使用 `check` / `full`。不能用 `focus` 的结果宣称整套规则完成。

原生控件、网络与重连、完整双客户端实战、打包和 Windows 实机仍是独立验收项。工具不启动、重启服务器或改写正在玩的对局。

## 锁屏期间继续执行

2026-10-18 用户确认：锁屏不暂停审计目标。继续按上述L1/L2/L3节奏执行；真实PostgreSQL、原生网络客户端连接/重连、原始日志回放及Godot无窗口控件检查可在后台执行。可见窗口操作与Windows实机保留独立状态，解锁后从已有检查点补验，不把无窗口通过标为可见验收。

冻结执行目录的 `.deps.json` 和 `.runtimeconfig.json` 保留为独立文件，可使用 APFS 写时复制。实际采集 `AppContext.BaseDirectory`、已加载规则程序集的路径、SHA-256、MVID 和 `RuleInputIdentity`，核对其与本候选一致。本轮实测 `.deps.json` 链回旧候选会使 .NET 加载旧目录中的程序集；仅核对目录内文件哈希不足以证明执行了新候选。

后台验收先核对固定提交的实际运行文件及来源哈希，给每步使用独立输出目录和收据。已完成步骤只在原运行文件、输入和收据哈希仍匹配时引用，并明确是既有证据；中断、失败或缺少数据库连接不记通过。API重启只作用于本轮专用验收进程，记录原日志、对局与数据库七表不变性，结束时正常关闭本轮服务。不等待解锁才继续下一条可执行义务。

macOS长步骤可使用`caffeinate -i -w <本轮后台进程PID>`临时阻止空闲系统休眠，记录实际进程和电源断言，绑定进程结束后自动退出。保持屏幕锁定，不修改持久电源设置；锁定观察读取IORegistry Root的`IOConsoleLocked`及会话的`CGSSessionScreenIsLocked`，无法取得时记录未知，不推定已解锁。

## 提速方式

1. 只做一次增量构建和测试发现；执行分片时使用 `--no-build --no-restore`。没有盲目复用旧测试通过结果，也没有绕过编译。
2. 主要瓶颈是 `FullGameEndToEndTests` 中同一测试类串行运行的完整对局。全量将该类按方法分到独立进程，其他测试保持原来的 xUnit 类间并发。
3. 用实际 TRX 耗时平衡进程；无本机历史时使用 `scripts/rule-test-timings.json`。耗时数据只影响调度，不控制测试是否纳入；新增测试即使没有历史耗时也必须执行。
4. 默认并发不超过 4 个进程，可用 `--jobs 2` 降低机器负载。完整预组交叉对局仍保留同一测试身份；其全部双向配对最多两场并行，每场独立状态、固定原种子、journal、胜利与回放检查。实际完成配对集合必须与全部期望配对完全相等，TRX 输出每场最终状态哈希。没有删对局、减少随机种子或删除断言。
5. 开发按上述分级执行；L2每实际集成批次一次check，L1每日累计，夜间/L3完整回归；未知影响代码CI仍full，已识别范围按首版分级。普通规则首批启动，同时尽早启动最长对局；只改变调度，不改变纳入范围。不要为同一个无变化输入反复跑完整矩阵。

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

`focus` 默认排除完整对局，只有显式 `--include-games` 才允许匹配的完整对局进入计划。单矩阵分片实测见 [历史提速记录](RULE_TEST_SPEED_2026-10-12.md)，该历史默认频率由本页分级替换。

## 后台 CI 与轻量文档检查

完整 CI 验证固定提交，记录 run ID、输入/程序集哈希、真实 TRX 和方法/用例/配对身份。开发可继续；本地并行必须使用独立固定 checkout，不能修改正在执行的源文件或 bin。被取消或未完成的运行不能标为通过。

真实 SQL 按 `tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json` 的有序执行身份分为四片，每片使用独立 PostgreSQL 服务；用例数以该固定提交实际清单为准。无参数入口仍执行全部默认项，`--ci-partition INDEX 4` 仅执行对应片，`--ci-partition-plan` 只打印计划，命名 `--...-only` 入口仍是限定验证。执行清单不表示整卡或语义认证范围。

规则全量、四片 SQL 和独立原生网络任务均在首波启动。稳定 `postgres` 检查要求四份固定提交/清单身份、有序完整用例、实际执行及产物哈希收据，并要求网络任务成功；稳定 `rules` 检查继续汇总全部子任务。缺片、重复、错版、篡改、失败、取消或跳过均不能被记为完整通过。本机单片、计划或门禁单元测试不替代实际四片终态；六小时上限导致取消的旧轮次保留原失败及未完成证据。

scripts/ci-change-scope.py 的文档豁免仍只认逐项核查过的精确治理文档路径，不使用 docs/ 或 *.md 通配。docs/DEPLOYMENT.md、docs/evidence、官方数据、fixture、打包文档、脚本、构建配置及未知路径仍 full；精确后端机制路径按上表L2执行；缺基线、类型/执行位变化及不确定差异同样 full。范围判定失败时完整任务仍会运行。纯文档工作流不会取消此前代码的完整任务；不同代码 SHA 的验收也独立，仅相同 SHA 的重复任务可取消重复。

轻量 CI 执行 Python 验证工具检查、固定差异格式、当前治理引用及来源索引完整性，报告规则/打包未执行。来源检查默认读取 docs/audit/current-manifest.json，核对当前卡牌、保留原确认日期的规则索引、限定审查和来源增量，不下载官网、不执行游戏、不提升 semanticCoverageCertified。显式 --date 2026-10-11 单独检查历史索引，不能替代当前 manifest。来源变化必须登记增量与影响，不静默改变冻结分母。

```sh
python3 -m unittest discover -s scripts/tests -v
python3 scripts/audit/check-project-governance.py --output var/qa/current-governance.json
python3 scripts/audit/check-project-governance.py --date 2026-10-11
```

轻量成功与上一完整基线分开记录。最终后端候选须等待L3完整后端验收；可见桌面及Windows实机保留后续客户端OPEN；后续需要真实 Postgres 证据时必须明确提供连接并验证实际执行。

浅 checkout 中若多提交 push 的中间祖先不可确定，即使可取得 base，也保守执行 full；不会把历史不完整当作文档豁免。只有范围任务成功且明确返回 false 才跳过完整任务，任何空/异常输出仍 full。

Desktop 矩阵始终生成既有检查名。明确文档范围时在 Ubuntu 写入打包未执行说明，跳过所有原生下载/构建/打包；其他或不确定范围使用实际矩阵 OS 执行完整步骤。文档并发组与 full 分开，同 SHA 的文档任务也不取消手动 full。
