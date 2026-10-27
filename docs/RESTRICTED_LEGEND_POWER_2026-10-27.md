# 受限传奇符能：当前用途切片与未完范围

Issue200 独立分支 `codex/payment-restricted-legend-resources-20261027` 基于 main `21b3e43fb7c3f7bb1c50e9957968890dc41b02ea`，不混入 PR203 的 No128/Hwei 或 No078/No124。最新规则代码固定为 `2d3b1918ab2fd01399e58141f95b09883cdaf1b7`；普通合入 main `406d07f9` 的后继 `d7a42c59` 只增加其CI/工具/治理依赖，src/tests/目录及构建入口逐字不变。各早期输入保持原身份，最新PR仍须自己的完整L2。

精确五个 CN 卡面原件在本树 ignored `var/qa/restricted-legend-resources-20261027/source-v1/`：山隐之焰 SFD·189/244 的符能仅用于打出装备或装备技能，虚空之女 OGN·247/299/299* 仅用于打出法术。日期、全来源适用性仍 UNKNOWN。

此次直接取回[中文核心规则 PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11` 与保留来源一致。430.2.a 规定获得资源技能不传递优先行动权或焦点；430.3 允许在启动打出过程前获得资源。仅核实这一精确文件，未宣称官网目录或全部适用性已重新闭合。

费用用途由实际服务端卡牌行为确定。匹配用途的受限彩虹符能用于费用分配、显式支出向量、授权、扣账、费用预览与对应提示；普通单位和用途不符的卡仍不能使用。真实资源命令保留原焦点/优先权，拒绝不变性、原日志、重连、去重与恢复安全检查保持。

各原件不相加、不改绑：

| 固定输入 / 原件 | 实际结果与范围 |
| --- | --- |
| c27 / neutral-resource-baseline-v2 | 原 14 例 9P/5F；五条中立取资源均拒绝，实际初始回合为3 |
| 1fd3ad82 / resource-purpose-framework-v7 | 47 例 42P/5F；用途资源池框架，非整卡通过 |
| 07712664 / resource-grant-and-timing-v8 | 56 例 46P/10F；原错误焦点与通用符能预期保留 |
| 同07712664 / complete-legacy-baseline-v10 | 25 方法、164 例 124P/40F；包括旧待结算牌型门槛与任意用途支付预期 |
| 7fcd2b04 / actual-play-counterexamples-v12 | 空目的地传入null造成编译失败，语义执行为0；后继按协议修为普通空字符串 |
| 8167e6cd / actual-play-counterexamples-v13 | 17 例 5P/12F；12条因预览夹具初始tick=1在正常恢复处停止，未到实际支付 |
| 3c5d0a79 / actual-play-counterexamples-v14 | 修正日志初始tick=0，17 例 13P/4F；四条匹配用途的预览/支付反例，八条用途拒绝及五条时点通过 |
| 77e09ada / actual-purpose-fix-v15 | 46 方法、168/168、0skip；四条原反例逐名转P，含实际法术/装备/单位、显式向量、受限池/法力、预览、原日志、重连、去重和恢复安全的定向检查 |
| 8628d6c8 / equipment-capacity-counterexamples-v19 | 2 方法、8例 1P/7F；原法术专用资源拒绝装配通过，装备资源装配及六条受限桶容量失败 |
| bb887ba8 / equipment-capacity-fix-v20 | 73 方法、240/240、0skip；七条原失败逐名转P，含真实长剑装配、受限桶物理容量和实际符文回收的提示/扣账边界 |
| 同bb887ba8 / adjacent-capacity-guards-v21 | 同一测试程序集另21方法108/108、0skip；资源费用替代、资源取得容量与 token 急速相邻守卫，独立记录不相加 |
| 5d50bddb / equipment-ability-counterexamples-v24 | 2方法8例4P/4F；预时之门/海兽钓钩两条真实支付拒绝，法球两行因传奇ID夹具冲突停于恢复，不能记作支付失败 |
| 73c22f4f / equipment-ability-fix-v25 | 122方法423例421P/2F；装备技能用途已接入，Gate/Hook原红转P；法球两行仍是旧夹具缺少显式位置而在构造时停止，不是423全绿 |
| 38e5314b / equipment-ability-fixed-fixture-v26 | 2方法8例6P/2F；法球夹具遗漏已入场SFD037的真实法盾标签，实际零目标税，两行不能记作受限支付语义失败 |
| c9e3ed6a / equipment-ability-ward-fixture-v27 | 2方法8/8、0skip；奥恩装备池支付Gate/Hook/Orb目标税，卡莎池和单位Vi技能拒绝；真实资源命令、提示/提交/扣账、原日志、每命令恢复/去重/回放保持 |
| 同c9e3ed6a / current-legacy-delta-v29 | 不重建，复用已核实冻结DLL；原25方法164例为129P/35F，五条资源时点原红已转P，旧任意用途/待结算卡型预期仍红 |
| 7b118469 / legacy-calibration-prompt-v31 | 原25方法164/164、0skip；逐行名称及重数完全相同，普通能量方法、所有Theory数据、通用支付普通符能正例与重复拒绝保留，另补五印次真实匹配消费及第二独立来源拒绝 |
| 27bad324 / instruction-resource-counterexamples-v33 | 2方法15F：10条缺少结算中的传奇资源选项，另5条已完成正确支付后因默认Other收据不带用途字段而失败，后二者不记为规则支付错误 |
| e4410359 / instruction-resource-typed-fix-v35 | 51方法182/182、0skip；新增15例及指示数量/连续确认/容量/原日志/投降相邻守卫，分别保留原27bad输入与37efe编译失败 |
| de31d928 / resource-conversion-counterexamples-v37 | 4方法21例17P/4F；奥恩实际海克斯异常体转换2F、受限桶满后仍提供溢出资源2F；新增资源改用途/来源退款/截断答案恢复反例均通过 |
| 2d3b1918 / resource-conversion-fix-v38 | 39方法158/158、0skip；上述4条逐名转P，含原15条、装备转换/容量及对应资源生产、确认、恢复相邻检查 |

最后一阶段入口 `var/qa/restricted-legend-resources-20261027/actual-purpose-delivery-v18.json`。它引用原 TRX、discovery、plan 和对应保留构建 DLL；测试计划程序集哈希与保留字节一致，未声称额外采集了运行时模块 MVID。失败、编译失败和 fixture 停止点在 `original-and-new-counterexamples-v16.json` 各归原输入。

最新入口 `var/qa/restricted-legend-resources-20261027/equipment-capacity-delivery-v23.json` 保留两个独立阶段和对应5份构建DLL；七条原红仍在v19，原红/新绿逐名比较在v22。装配计划和提示使用装备用途；回收/转换/各支付资源选择按包含受限桶的物理容量计算，默认其他费用仍不能使用受限符能。

最新装备技能切片入口 `var/qa/restricted-legend-resources-20261027/equipment-ability-delivery-v28.json`。对应支付计划、提示可付性和法盾目标筛选使用同一服务端装备技能用途；单位用途保持Other。四个独立输入各自核对2685 Git输入、705/705 tracked CS实际编译及5实际模块Info/SHA/MVID。三处夹具修正的原件完整保留；Hook只验证实际技能支付后、私有牌堆效果结算前的边界。最后8/8不改绑为旧423全绿，所有阶段不相加。

最新完整入口 `var/qa/restricted-legend-resources-20261027/restricted-resource-delivery-v39.json`，SHA256 `94118a8e57a895a44b3c2da56c3c0527e6f87dc994daf3e384b90f3fcbe7f0cc`。旧164校准只调整有来源依据的桶和取得时点：无用途的通用窗口继续拒绝受限符能，原支付正例改作明确普通符能控制；真实匹配路径通过正常卡牌/装备技能决定用途，耗尽后尝试另一张实际来源，未伪造支付用途。460964的测试API编译失败与37efe的私有命令类型编译失败另保留，语义执行均为0。

结算资源菜单现在包括可合法取得的奥恩/卡莎传奇资源；确认后独立提交横置及受限桶，仍不能支付Bullet Time内部指示。普通符文随后取得的1符能可支付该指示。投降保留已提交资源，原Origin/Intent/Command/Answers不改写；把桶改成普通符能、退还横置或删除确认答案均被恢复校验拒绝。最新2d3的15条原路径及6条转换/容量共21份专用命令/事件原件已核验，不把其他阶段结果相加。

2026-10-27 15:25 UTC另外直接取得并逐张查看SFD·083海克斯异常体、SFD·117远古簇碑、UNL-049蜜糖果实的官方API记录及完整卡面，原件在 `source-resource-cost-v36/`；三个正常目录的ID/印次/名称/卡文/费用字段与实时记录相同，errata为空、isPreview=false，发布/生效日期及全部适用性仍UNKNOWN。海克斯异常体是装备，其转换费用使用装备用途授权与扣账，奥恩桶可支付、卡莎桶不可支付；菜单上限和物理容量同时计入各受限桶。

7b118、27bad/e441、de31/2d3各自核对2685/2686/2687个Git输入、705/706/707个实际编译tracked CS及5实载模块Info/SHA/MVID；只审查一次各固定输入，后继不改绑。其余消费者、完整可选费用交互及全来源适用性尚未认证；Issue200仍OPEN。最新PR完整L2、SQL、SignalR、真实进程重启、完整胜局或矩阵L3均不由这些局部结果代替，整卡及全后端验收仍OPEN。
