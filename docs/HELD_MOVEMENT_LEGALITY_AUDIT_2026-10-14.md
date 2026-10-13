# 据守移动目标合法性与执行禁令

2026-10-14，Goal active，平台 **NOT READY**。当前中文UNL-207/219业余排练厅ID34760规定据守时可将战场上的一名单位移动到其基地；英文unl-207-219身份独立记录。[现场卡面](evidence/held-movement-legality-2026-10-14/source-current.json)、[英文记录](evidence/held-movement-legality-2026-10-14/en-current.json)、[核心PDF回读](evidence/held-movement-legality-2026-10-14/current-core.json)与[相关条款](evidence/held-movement-legality-2026-10-14/core-clauses.json)保留官方URL、时间、hash、语言地区与已知适用性。中文当前核心359.3.e.6区分合法目标/终点和无法执行的移动；未来10月16日规则不提前应用。

原据守目标清单按移动禁令过滤，将仍合法的战场单位错误排除；结算又直接修改区域，绕开共享禁令判断。[两条原反例](evidence/held-movement-legality-2026-10-14/red-evidence.json)证明卑鄙之喉和临时执行者禁令的目标缺失。此次保留可选确认、世代、目标保护、法盾支付、响应窗口、恢复和所有旧方法/Theory行；仅将禁令判断留到执行，并调用现有共享移动事实、装备跟随和观察者流程。技能执行者仍是P1，单位控制者及目的基地仍是P2，事件来源保留实际技能来源F。

六项新增用例/四方法覆盖两种禁令合法确认后忽略、不同执行者禁令的区分、装备/伤害/横置/控制者/世代保留、实际移动计数、可选放弃。每条接受命令核对隐私、当前及冷恢复、去重和完整动作/事件/状态回放。[中间失败](evidence/held-movement-legality-2026-10-14/intermediate-corrections.json)保留新测试直接贴附复制武装而未构造复制上下文的错误；改用普通OGN023武装，不绕过生产恢复验证。

[108项相邻验证](evidence/held-movement-legality-2026-10-14/adjacent-focus.json)及[2,022项恢复/装备扩大验证](evidence/held-movement-legality-2026-10-14/adjacent-recovery-focus.json)通过。[六路径16条实际Godot命令读回](evidence/held-movement-legality-2026-10-14/native-replay.json)完整日志含22条接受命令、22次冷恢复和完整回放；[边界](evidence/held-movement-legality-2026-10-14/native-setup-boundary.json)明确初始TurnStart夹具的服务端推进六条不计入原生按钮命令，后续真实CHOOSE_CARDS/优先权按钮均执行。无窗口验证不替代可见双客户端。

[FAQ现场回读](evidence/held-movement-legality-2026-10-14/faq-current.json)响应hash发生变化；[结构差异](evidence/held-movement-legality-2026-10-14/faq-response-delta.json)核对只有相关推荐轮播和mapper版本改变，其他所有内容块包含正文保持一致。没有补造本牌专门FAQ，原出版时间与本次取得时间分开。

运行目录1,031/830及所有现有完整对局输入不变。[check核对](evidence/held-movement-legality-2026-10-14/check-parity.json)为11,010用例/6,864方法、164.90秒，旧11,004/6,860全部保留，新增6/4、2,244输入无漂移，明确连接55439独立集群的SQL5实际通过，之后停止该集群，Python27通过。[full计划](evidence/held-movement-legality-2026-10-14/full-plan-parity.json)同一程序集7,005方法、原143完整对局全部保留，预计11,159用例/110配对seed424242；新固定完整执行仍独立核验。整卡/全量源、其他移动和持续禁止消费者、冷酷追击移动/贴附/征服临时技能、八项英文计数差、可见双客户端、用户Windows和完整SQL并发/故障继续开放。
