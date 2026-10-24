# P7 原测试的指示付款时点校准

- [Issue108](https://github.com/DDDHL0704/riftbound/issues/108)，S5验收修正，按原L2门禁交付；仅测试/来源说明，无生产代码或旧持久日志修改，不把局部通过变成整卡认证。
- 原`affected-v5`预筛WIP完整发现1132方法、实际2512例，2499PASS/13FAIL/0skip，方法集合与计划相同；其中9条P7和4条N2消费者。原TRX SHA `31136cba1c8b045f94986c61f5844bfc8c52ae903adbe795e8acc59fd133b4d4`。另扫描出1条被费用名称过滤漏选的同类P7提示方法，共10条；N2三方法/四数据例仍归④，本包不改。
- 对照证明不是f868容量改动引入：656与f868的整个原ConformanceFixtureRunnerTests文件字节相同；同一未修改测试程序集分别实际加载冻结656 Engine `d5f2277e…`/MVID `b9139f52-936d-4b14-a43a-8aa5cf13ab73`及f868 Engine `53db257a…`/MVID `81d05bef-d961-4673-b2d9-384035e3f00a`，10/10均逐条同错。明确是“656冻结引擎+未改原方法”的组合对照，不冒称整份656提交测试；startup hook只记录实际路径/版本/哈希，不改游戏状态。
- 来源为已核CN[核心2026-10-10](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)204.3.a及740.4.a.1，SHA `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`；本日[官方API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)精确OGN·268/298响应SHA `570d3034ac4392af1334c53a6bf6622c32317e4a33e58927097bfaefa72acd23`，无附加QA/勘误，出版/生效日未给出。弹幕时间的变量符能是结算指示费用，不在施放时支付。
- 保留原方法名、Fact/Theory/数据属性、数值/来源初始化、原牌/玩家/符文/intent ID、按色余额、符文次序及盈余断言。原SPEND_POWER施放命令逐条保留为零事件/原hash拒绝控制；当前路线只在声明时获取原指定合法资源并付印刷费用，经真实双PASS到既有指示窗口再付原数量。核对实际最终伤害、余额及先付费后伤害顺序；声明的符文/获取收据与结算费收据分别核对，不伪造成功、事件、tick或stack。
- 旧手工目标只有5战力、0伤害及P2战场位置，无类型/控制者，原来只检查预定stack数值。原输入继续用于拒绝控制且hash不变；当前正向副本严格核对该唯一原边界后，仅补UNIT及已知P2区域控制者，不补造CardNo/OwnerId、不改数值/卡牌/牌组。由此将原1/2伤害预算检查落实到实际目标伤害；这是显式手工fixture，不称普通出生或完整恢复认证。错误预期不存在DAMAGE事件的中间2/10原件保留。
- 实际定向10/10；相邻P7全组、S5时点/有界数量、S3自愿资源及原日志伪造控制共 **423/423、0skip**。均为f868加本批测试WIP，独立artifacts；不重标为后续提交或合并候选。本地仅focus/build，没有新check/full、SQL/网络重启或完整对局矩阵。
- 原件位于本线ignored `var/qa/payment-p7-regression-20261024/`：`baseline-input-receipt.json`、`paired-original-ten-result-v4.json`、`broader-terminal-v3.json`、`source/`、`corrected-ten-v5/`、`corrected-ten-v7/`、`affected-v8/`。`original-retention-v6.json`独立检查恰为这10方法变化、其他方法块及全部原初始化前缀/数据属性一致、FullGame九文件不变；一条提示Fact改为async Task，完整方法身份及用例保留。
- 需①收取这份独立测试提交并按固定diff执行实际门禁，N2四例不在本包闭合；不能把旧2499与新10作算术拼接宣称2512绿灯。净整卡认证增量0，S5及整个后端Goal的完整源/语义/恢复/Actions义务继续OPEN。
