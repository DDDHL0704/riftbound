# 真实 PostgreSQL 与 Windows 导出验收

2026-10-13。总 Goal active，平台 **NOT READY**。本批建立独立的真实 SQL 门禁，并验证 Windows 原生 ICO 导出候选，不提升卡牌或规则的全量语义认证。

现有五条 PostgreSQL smoke 在没有连接时提前返回，测试 PASS 本身不能证明 SQL 执行。本机新建本任务 var/qa 下的独立 PostgreSQL 16.13 实例，仅监听 127.0.0.1:55439，数据库 riftbound_acceptance；现有本机实例保持独立。显式连接后，五条原方法实际执行并通过，[环境](evidence/postgres-export-2026-10-13/postgres-environment.json)、[SQL 写入统计](evidence/postgres-export-2026-10-13/original-smoke-sql-stats.json)和[验证记录](evidence/postgres-export-2026-10-13/validation.json)保存其范围。原方法未删除、跳过或改写断言，其中使用 PlaceholderRuleEngine 的范围也没有扩大为真实玩法。

新增 tests/Riftbound.PostgresAcceptance 是必须提供真实连接的独立执行程序。它限制为 loopback 的专用 riftbound_acceptance 数据库，缺少连接或指向其他数据库直接失败。程序通过生产 CoreRuleEngine、MatchSession、PostgresMatchJournal 和恢复 store，实际提交两套官网预组牌、准备、调度起手换牌，冷恢复后继续开局；保留原始拒绝命令，检查重复 intent 不追加 SQL，实际投降并再次冷恢复。五个检查点均核对当前状态哈希、完整动作/事件回放和双方视图 tick。直接修改最新 SQL 权威状态 tick 的反例被拒绝，恢复原 SQL 后再次通过。

[实际结果](evidence/postgres-export-2026-10-13/real-postgres-core.json)记录 8 条命令、19 事件、7 个权威状态帧、10 条历史快照、10 条历史提示及双方各一条当前视图/提示。历史键不同：权威帧按 tick＋事件序号保存，玩家视图按 tick 保存；预期集合从实际执行结果独立累计，未用 store 的生产推导作预期。[验收断言修正](evidence/postgres-export-2026-10-13/sql-history-expectation-correction.json)保留最初误把全部历史行视为两个当前视图，以及忽略保存键差异的失败。缺连接失败与实际房间清理另见[门禁与清理](evidence/postgres-export-2026-10-13/fail-closed-cleanup.json)。此结果不认证完整得分胜利、并发读取一致性、慢恢复、故障切换、网络加 SQL 或全部卡牌生命周期。

完整 rules CI 增加独立 PostgreSQL service，官方 postgres:16.13 镜像固定到已实取的 manifest digest；[官方镜像记录](evidence/postgres-export-2026-10-13/docker-image-source.json)保存来源和取得时间。完整规则检查的五条原 smoke 将收到真实连接；随后独立 SQL 程序必须通过，结果随 conformance-results 上传。旧网络恢复 smoke 继续使用其明确的无数据库配置，不把它当作网络加 SQL 证据。轻量文档任务不执行数据库门禁。

两个原 Windows job 均在导出退出时出现两个 DummyTexture RID 与两个引用计数为 1 的 Image 泄漏对象，启动导出包步骤未执行。[详细失败](evidence/postgres-export-2026-10-13/previous-windows-verbose-failure.json)保留固定提交身份及日志哈希。重新取得 Godot 4.7.2 的官方 Windows 导出源码；主程序和控制台程序分别处理图标，非 ICO 输入经 Texture2D/Image 转换，ICO 输入直接读取字节。[源码回读](evidence/postgres-export-2026-10-13/godot-source-readback.json)保存精确版本和哈希。两次加载与两个泄漏对象相符是合理推测，尚不证明根因。

本机同一版本 Godot 对原 SVG 的 Windows 交叉导出没有复现泄漏，[原路径记录](evidence/postgres-export-2026-10-13/original-svg-cross-export.json)不能作为 Windows 实机通过。候选把现有 SVG 直接转换为六个 PNG 尺寸的 ICO，供 Windows 主程序和控制台程序使用；[ICO 格式核对](evidence/postgres-export-2026-10-13/ico-validation.json)检查尺寸、偏移、长度及 PNG 头，直接 SVG 生成的重建字节完全相同。候选交叉导出通过，[记录](evidence/postgres-export-2026-10-13/native-ico-cross-export.json)仍限定于 macOS 的 Windows 导出平台。非零退出和全部 ERROR 门禁保留；Windows runner 的实际导出与启动证据仍必需，[问题 #23](https://github.com/DDDHL0704/riftbound/issues/23)保持开放。

原交叉导出日志还证实旧 exports/macos 中的导出文件被重新收进资源包。两平台排除 exports/* 后，候选包不再包含这些旧导出输出。原 app 图标设计未改变，编辑器插件配置在导出前暂时关闭后恢复原字节。27 项 Python 验证工具检查、actionlint 与 ICO 重建通过；本批一次 check 已通过 10,689 用例 / 6,740 方法、182.67 秒，原用例、方法全部保留，零失败、跳过或重复，[独立核对](evidence/postgres-export-2026-10-13/check-parity.json)确认 16 项输入哈希不变。五条旧 SQL 方法的连接在本次 check 中同样明确提供。完整计划精确保留前一计划的全部 6,881 方法，预期 10,838 用例和 seed 424242 的 110 配对；[计划核对](evidence/postgres-export-2026-10-13/full-plan-parity.json)不证明执行通过。实现 470dfda78cb5a96f57675b2ab62c9ff442e25e19 已推送，[固定提交完整 CI 38334584081](https://github.com/DDDHL0704/riftbound/actions/runs/38334584081) 正在运行；完整规则、真实容器 SQL 门禁与 Windows 导出/启动均待实际终态。专用本地实例在验收后已停止，现有主机 PostgreSQL 未改变。

全部官方条目、来源差额与语义审计、完整原生双客户端实战、Windows 用户运行、完整 SQL 生命周期及并发/故障情景仍开放。局部 SQL 或交叉导出通过不构成平台 READY。
