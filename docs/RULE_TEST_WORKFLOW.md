# 规则开发测试工作流

2026-10-24 用户授权：所属方法自行实现、提 PR，最新 PR 合并输入的 L2 通过即可合 main；真实数据库、网络、重启与完整回放移至每日夜间 L3。此约定覆盖旧“候选全绿才发布”“只有①可合 main”和逐 helper/完整包审批。完整规则目标与原测试断言保留。

## 开发、合入与完整验收

| 等级 | 执行范围 | 用途 |
|---|---|---|
| L1 | 本机定向/受影响回归，合法、非法、边界与拒绝不变性 | 开发反馈，不单独作为 main 合入绿灯 |
| L2 | 定向及相邻域断言；机制规则方法及原两条完整对局冒烟；工具/来源完整性 | PR/main 的 `L2 required`；不依赖 SQL、native_network、related_network 或其汇总 |
| L3 | 固定 main SHA 的完整规则/143完整方法151例、11牌组110配对、seed424242、真实SQL、双玩家SignalR、权限/隐藏信息、进程重启与完整原日志回放 | 每天02:00上海时间一次；显式手动调查亦可。完整验收与阶段完成依据 |

[backend-l2.yml](../.github/workflows/backend-l2.yml) 对每个 PR 合并结果 SHA、main push SHA 和手动输入执行 L2，不用路径豁免关闭规则测试。定向入口包含 payment/combat/triggers/recovery/recast 相邻域及本次变化的普通测试类；`l2` 实时发现并执行机制规则及两条原完整对局冒烟；[精确L3方法清单](../scripts/l3-rule-acceptance-methods.json)中的18个完整合法开局/冷恢复/全原日志方法留在夜间 `full`，原方法、Theory行、夹具与断言均保留，按源码用途分层，不按耗时自动豁免。未知或新增方法继续在L2执行。未知路径或目录新增同样执行 L2，其完整矩阵在夜间运行，不再强制每包发 A8/A9。

`L2 required` 必须取得 quality、targeted、rule_suite 三个成功结果，并复核对应 SHA、程序集、非空计划、实际 TRX 方法覆盖、全部通过与零跳过。失败、取消、跳过、未执行、缺产物、错版或零测试均失败；不允许 `continue-on-error` 或删语义断言换绿。L2 结果明确不认证 L3 SQL/网络/完整矩阵。真实 SQL 的原单元冒烟在未配置数据库时不代表真实 SQL 执行；实际 SQL 只看 L3 产物。

各线可自行提 PR/合并所属方法。合并前必须重新取得最新 PR head/base/merge-result SHA，并机械确认该最新合并输入的 `L2 required` 成功；PR 更新、base 前移或 merge SHA 变化即使旧检查绿也不能复用。优先用 main 分支保护要求该检查、要求分支跟上 main，不设置所有 PR 必须人工批准；若无管理权限，保留同样的逐次机械核验。合并后 main 自身也执行 L2，失败应立即修复，不绕过红灯继续相关交付。

普通接口依赖写明确提交依赖，不设全包准入审批。只有两线并发修改同一方法时由①协调先后/归属；共享文件不按整文件独占。每线保持一个主要包，可在另一包等待 CI 时推进独立工作。只在可交付包、真实同方法冲突或阻塞时集中发消息，不发 ACK。

## 夜间 L3 与失败归属

[native-desktop.yml](../.github/workflows/native-desktop.yml) 只有一个每日 schedule 和显式 workflow_dispatch；不由 PR/main push 自动启动。scope 启动时只读取一次 main，记录完整 SHA/源码索引，后续所有 checkout 使用该 SHA，即使 main 后续移动也不混入新版本。完整规则、四片 SQL、原生网络与实际进程重启在固定输入上运行，缺片/失败/取消/跳过不能通过完整汇总。既有原生打包 CI 保留。仅修改 CI/验收工具的 PR 另有 [harness 冒烟](../.github/workflows/backend-harness.yml)，检查工具自身及一条真实重启；它不加入 L2 required，不在普通规则 PR 上执行，也不代表完整 L3。

SQL 清单来自 `tests/Riftbound.PostgresAcceptance/SqlAcceptanceCases.json`，按有序身份分四片，独立真实 PostgreSQL。`postgres` 严格检查四份收据的提交、清单、有序实际用例和全部输出哈希，并要求 native_network 与 related_network 成功；`rules` 再要求完整规则及 SQL 汇总成功。重启任务用真实持久 API 的不同进程恢复原两玩家、验证认证/私有视图和原 SQL 历史；完整对局/原日志语义由保留的完整规则及 SQL 用例共同验证，不把房间恢复冒烟称为完整实战。

A7 等已启动固定候选可完成并归档为历史证据，不取消、移动标签或因治理提交而重发。A2–A6 的脚本/环境问题归①集中修；实际规则、费用、隐藏信息或恢复缺陷按领域一次性发最小复现/失败断言给②③④，用独立修复 PR 处理。不能把全部失败都降为脚本问题，也不能以一个已知缺陷冻结无关包。夜间失败阻止完整验收通过的声明及相关改动；无关包 L2 绿可继续合入。B1/source queue 是迁移清单。

原件保留候选 SHA、run/job/artifact、实际加载程序集/输入身份、原日志和失败断言。大日志/TRX/SQL/二进制留 CI artifacts 或 ignored `var/qa`；Git 保存源码测试及简短可取回引用，哈希不能替代原件。夜间产物保留90天，实际平台上限/到期及持久转存仍须核验，不宣称永久归档。

## 本机定向入口

在仓库根执行；Python 标准库，脚本自动定位本机 .NET。各线独立输出/端口/数据库，不停他人进程。主机10逻辑核/24GiB，定向 `--jobs 1` 或 `2`；新增重型 full/完整矩阵/完整SQL/SignalR重启在 Actions。具体失败可最小化隔离调试，原 N54/N55 固定源/bin/数据库不改写。

```sh
python3 scripts/test-rules.py focus payment --jobs 2
python3 scripts/test-rules.py focus combat --jobs 2
python3 scripts/test-rules.py focus --match OfficialMechanicalRecastTests
python3 scripts/test-rules.py focus --include-games --match PreconstructedCatalogDecks
```

`check` 保留旧完整普通规则加两条冒烟的范围；`l2` 仅将清单中的完整开局回放验收留给夜间。`full` 实时发现全部测试并按方法分片。两者的重型执行由 Actions 承担；`full --plan-only` 仍会构建/发现测试，不是规则通过结果。域筛选只是可检查选择，无法代替规则语义或完整认证。

## 执行完整性

- 保留全部原方法/Theory行、143完整方法151例、110双向配对、11牌组、seed424242、胜利及回放断言；来源校准保留原失败与理由。
- 实时 `--list-tests` 生成计划，无法解析或选中零项失败；每方法恰好一片，实际TRX覆盖全部计划方法，不允许多片重复/其他方法混入。
- 分片须正常退出、非空TRX、全通过且零跳过；否则失败并只清理本工具启动的进程。Theory运行时展开，实际例数来自TRX，不把发现方法数当作例数。
- 先增量构建一次，分片 `--no-build --no-restore`；耗时权重只调度，不控制成员或复用绿灯。`--expect-trx` 仅用于相同程序集性能对照，不锁死新增测试。
- 每次使用全新输出目录，保存构建/发现/计划/程序集哈希/TRX；仅通过后写 summary。不能复用旧目录制造本次结果。
- 同固定输入可复用原结果，只补真实差异；旧 full 是旧基线。不得改写已接受原始命令，或用 Noop/伪造出生证明替代恢复安全正例。
- 锁屏不暂停后端；后台执行不代表可见客户端/Windows实机通过。不建立 Codex 轮询自动化或无限等待 Goal。

轻量工具验证：

```sh
python3 -m unittest discover -s scripts/tests -v
python3 scripts/audit/check-project-governance.py --output var/qa/current-governance.json
```

治理只核对当前来源索引与引用，不下载官网、不提升语义认证。后端阶段完成仍须适用来源/语义/实现及固定输入 L3 全闭合；局部或 L2 绿不称整卡/产品 READY。
