# N55 战斗死亡与雷恩加尔权限集成（L2）

| 功能身份/印次 | 来源/条款 | 源码/语义断言 | 状态/证据 |
|---|---|---|---|
| 德莱文 SFD·148/221、SFD·148a/221 | CN/zh-CN，10-16生效核心PDF SHA256 d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11；383.2.c.3、429.1.a.1.b、808.1.d.1及383.3.d.1/468结果顺序；10-23两完整API行 | CoreRuleEngine.DravenBattleDeathScore、DravenBattleDeathRecoveryRules和死亡/结果续体接点；OfficialDravenBattleDeathScoreTests、DravenMutualDeathScoreTests、DravenBattleDeathRecoveryTests、DravenDeathAndWinOrderTests | 来源离场/换代仍保留已获技能，替代召回不触发死亡，对手选择与同时死亡顺序、普通胜利中止后续控制；分支通过待固定集成，整卡认证增量0 |
| 雷恩加尔 SFD·025/221、SFD·025a/221·P，API33098/34820 | 同CN核心466.2.c.3、323.2、468.7.a；完整同文印次映射 | CoreRuleEngine.RengarAttackingPermission与UnitPlayDestinationChoices/位置校验共用；原11例和RengarPermissionRecoveryTests24例 | 正在进攻的身份到战斗结束；末名单位离场不提前撤权，场地禁止/费用/错误身份仍拒绝；分支通过待固定集成，整卡认证增量0 |

- 正常源码随本提交入库，死亡交付2a761640和雷恩加尔bb118724的压缩补丁/文档提交本身不当作主线实现。主线29个运行/测试/SQL文件与唯一冻结组合check输入逐字节相同。check目录`/Users/dinghaolin/MyProjects/.worktrees/riftbound-backend-n55-rengar-check-20261023`，提交时7670方法仍运行，不记通过、不再启动第二check。
- 主线死亡/相邻/选择战场交叉324例通过；加入雷恩加尔后的两模块与N54恢复交叉85例通过。复用所有者实际158例/44方法含3原完整对局和死亡模块定向/相邻结果，仅补组合差异。
- 永久SQL69新增12场景，125条原指令全部逐前缀回放/真实冷恢复，三种结构自洽来源代次/印次/角色篡改被拒；原533审计房及147网络房七表不变，专用服务已停。原驱动遗漏ORDER_TRIGGERS的失败及一次编译失败后误启动旧二进制的失败原件都保留，v3成功构建后才是本次通过依据。
- 所有者六真实SignalR/SQL死亡房83输入（77接受）/193事件，以及雷恩加尔六房60输入（48接受）/139事件、实际API重启、拒绝/重复不变性及独立原前缀/七表读回在实际源码身份核对后复用；不冒称当前固定候选L3完整通过。
- 可取回原件：主线`var/qa/draven-battle-death-n55-20261023/`、`var/qa/rengar-permission-integration-20261023/`；雷恩加尔完整原网络/SQL位于`/Users/dinghaolin/MyProjects/.worktrees/riftbound-parallel-official-audit-20261023/var/qa/rengar-permission-runtime-20261023/`，既有source/manifest哈希及完整可审阅diff保留。新大日志/归档未加Git；原1740/14293范围、历史义务与未认证分支不缩减。
