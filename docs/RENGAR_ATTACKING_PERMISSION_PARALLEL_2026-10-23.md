# 雷恩加尔向正在进攻的战场打出：独立权限消费者

中国区 / 简体中文 / 2026-10-23，核心 2026-10-16 生效。官方完整行 `33098 / SFD·025/221`、`34820 / SFD·025a/221·P` 均明确“我可以被打出到你正在进攻的战场”。本次七个相关完整官方行重新查询，与本日完整 1504 行来源相同；不是用反应关键词推导额外位置权限。

新增独立 `CoreRuleEngine.RengarAttackingPermission.cs` location helper，限定上述两印次。使用既有真实战斗声明的 `InitialAttackingPlayerId`、`InitialRolesEstablished` 和精确战场，而不是要求当前还有存活的 `IsAttacking` 单位。466 的玩家身份在战斗开始时确立，468.7.a 在战斗结束时移除；原进攻单位回手后，原战果技能仍在等待时，玩家的进攻身份尚未结束。helper 不替代反应时机、费用、场地禁止或其他强制目的地规则。

**helper 目前无共享调用点，尚未修复。** 主窗口需统一接入服务器位置验证及 `UnitPlayDestinationChoices`，保证提示与执行一致。当前 Core 验证只允许己方控制场地、伏击或既有强制/额外费用分支；SFD 雷恩加尔的专属位置权限缺失。涉及从牌堆或其他效果打出的复合目的地限制仍需后续独立审查，本批只验证手牌打出。

`RengarAttackingBattlefieldPermissionTests` **11 例 / 2 方法：4 对照通过、7 来源断言失败、0 跳过**。两印次分别覆盖战斗对决 focus、结果技能等待、真实 `OGN·104/298` 回手最后一个进攻单位后、另一不受自己控制且未在进攻的战场、原控制方反应入场。前 3 类共 6 例均在实际 `PLAY_CARD` 被错误拒绝；两印次对另一战场的拒绝与原控制者入场共 4 对照通过，原控制者提示目的地亦匹配。保留此前第 11 例原 Fact，其仍在位置权限前置失败，**不能报告进攻方入场后角色清理断言已执行**。

全部 **57 条原指令（48 接受、9 拒绝）、171 事件、57 个逐前缀完整回放** 已保存，包括反例中的拒绝；没有把预期应接受的拒绝当作测试通过。源码、初始状态、每步状态、完整请求、规则输入、提示和失败原因均入包，seed `424242`。真实回手分支确认场上已经没有 P1 的进攻单位，原 `PendingBattleResult.AttackingPlayerId` 仍为 P1。

同一最终测试 DLL 下，原伏击和局部场地权限 **70 例 / 21 方法全部通过**。本轮未修改共享 19 文件 overlay，没有额外 check/full/SQL；消费者未接线，因此受影响完整对局、双玩家 API、恢复及固定提交验收由接线后统一完成。本批依赖 `1fe687a1` 中的保留测试 helper；原晚入场失败包不变。N52 的晚入场续体修复由主窗口持有，不把它在新输入上的通过冒称成本窗口旧 overlay 已转绿。

[来源及运行清单](evidence/rengar-attacking-permission-parallel-2026-10-23/source-and-red-test-manifest.json)，[逐项 SHA/大小核验的原件包](evidence/rengar-attacking-permission-parallel-2026-10-23/original-tests-fixed-inputs-and-sources-v1.tar.gz)。所有旧来源、印次、测试、种子和失败保留，后端 Goal ACTIVE。
