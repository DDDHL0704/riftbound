# 公开参战历史的真实 PostgreSQL 门禁

2026-10-13，Goal active，平台 **NOT READY**。本批新增四条真实 SQL 路径，只覆盖已公开参战单位被摧毁、正常抽牌或燃尽抽回手牌后的持久化、冷恢复和动作回放，不提升整卡或全量规则状态。

生产恢复从已持久化的玩家席位重建初始状态，不能把任意中局 fixture 替换成数据库回放起点。新增开发场景通过真实 `DEV_SEED_SCENARIO` 命令写入 journal，随后全部移动、目标确认、反应法术、让过和战斗收尾使用普通 CoreRuleEngine。场景使用注册印次的实际数值，入口沿用现有开发环境限制；不改旧已接受命令。此前 [来源与权限决定](evidence/battle-history-sql-2026-10-13/source-decision.json)及[法术回读](evidence/battle-history-sql-2026-10-13/soul-readback.json)保留原取得时间、官方 URL、哈希和适用范围，本批没有新规则释义。

[实际 PostgreSQL 16.13 收据](evidence/battle-history-sql-2026-10-13/sql-summary.json)为四条进攻方/防守方空或非空主牌堆组合，共 56 条接受命令、148 条事件、56 个权威状态、112 份玩家快照和 112 份提示，60 次冷恢复检查。每条命令后的生产 store/journal、全日志回放、双方提示和重复 intent 检查通过；四次 SQL 历史世代篡改都被拒绝并恢复，生成房间由各自 finally 清理。原[官方预组开局门禁](evidence/battle-history-sql-2026-10-13/core-postgres.json)仍独立执行，8 条实际命令、5 个冷恢复/回放检查点及 SQL 篡改检查通过。

新 SQL 门禁追加在原必需验收程序之后，现有完整 CI 的 `Real PostgreSQL core recovery and replay` 步骤会执行两者；缺少连接依旧失败，不能静默跳过。新增四行 Theory 从玩家席位和记录的场景命令验证同一路径，保留此前全部方法和 Theory 行。

[相邻 focus](evidence/battle-history-sql-2026-10-13/adjacent-focus.json)19 项通过；该 focus 没有数据库连接，五项 SQL smoke 的实际执行只以随后带连接的 check 和独立必需 SQL 程序为证。Python 工具 27 项通过。[连贯 check 与保留校验](evidence/battle-history-sql-2026-10-13/check-parity.json)为 **10,883 用例 / 6,813 方法，173.06 秒，零失败或跳过**；原 10,879 / 6,812 全部保留，新增四行 Theory / 一方法，2,024 个输入前后未变，五项 SQL smoke 均有实际连接。[完整计划](evidence/battle-history-sql-2026-10-13/full-plan-parity.json)为 6,954 方法、预期 11,032 用例，保留原 143 对局方法及 110 配对 / 种子 424242；固定实现 CI 的终态需另外核对。专用 PostgreSQL 在验收和 check 后已停止，未停止其他实例。Godot .NET 构建通过。

旧 `86adea6f` 的[完整 CI 38395953457](https://github.com/DDDHL0704/riftbound/actions/runs/38395953457)终态失败：[收据](evidence/battle-history-sql-2026-10-13/fixed-86adea6f-ci.json)记录普通规则 10,873 项和 games-2 的 49 项全通过，games-4 的 52 项中 50 项通过、两条备战对局驱动无法找到 HIDE_CARD，其他分片中断，必需 SQL/网络跳过，双平台桌面通过。当前本地 SQL 成功不改写该旧失败，原完整对局待下一批修复。

可见双客户端、用户 Windows、完整 SQL 得分胜利、并发读取、故障切换，以及全部重新进场/控制/指示物历史和一致伪造边界仍开放；1738 个当前精确卡牌身份及历史规则清单维持原未审状态，英文八项来源差额没有闭合。
