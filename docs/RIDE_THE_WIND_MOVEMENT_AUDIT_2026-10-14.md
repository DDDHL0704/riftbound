# 驭风而行完整移动范围与关联活跃

2026-10-14，Goal active，平台 **NOT READY**。当前CN OGN·173/298规定“移动一名友方单位，然后让其变为活跃状态”，原实现仅能战场到基地且先活跃后移动。此次沿共享选择目的地/移动流程修复，无卡号判断绕行。

[中文回读](evidence/ride-the-wind-movement-2026-10-14/source-current.json)保存ID31403、逐记录hash和时间；[英文](evidence/ride-the-wind-movement-2026-10-14/en-current.json)独立ogn-173-298文字为Move a friendly unit and ready it，中英文及分母分别记录。[当前核心](evidence/ride-the-wind-movement-2026-10-14/current-core.json)现场PDFhash一致，未来10月16日版本不提前使用；[条款](evidence/ride-the-wind-movement-2026-10-14/core-clauses.json)355.4要求确认有效且不同当前位置的终点，359.3.e.6直接以驭风而行/卑鄙之喉的巢穴举例，359.3.e.14规定忽略前置关联指示时忽略后续指示。[FAQ](evidence/ride-the-wind-movement-2026-10-14/vendetta-faq.json)不补造单卡裁定。

友方目标现在可以来自基地或战场，声明其控制者基地或合法战场作为终点。共享目的地枚举现在包含己方基地，实际合法性仍由所选单位的控制者/位置决定，不能向他人基地移动。单位先沿共同移动事实/装备/观察者流程移动，再执行引用成功移动单位的活跃指示；伤害、世代等保留，基地/战场之间不会成为新物体。禁止移动不使目标或终点本身不合法，移动执行时忽略无法完成的指示，关联活跃也忽略。

本次同时纠正上一天顶之刃限定结论中的过严许可：有移动禁令的友方单位可以被选择，结算时跳过移动，独立敌方眩晕仍执行。保留原方法名称/Theory行和原来完整回放证据，新的断言与来源明确记录，不倒推旧aa8d34ca语义已经正确。

14新增用例/7方法覆盖基地→战场、战场→己方基地、战场→另一战场、移动/活跃顺序、缺失/伪造/他人/当前位置的终点拒绝与状态不变、当前卑鄙之喉和临时禁令的合法确认/忽略关联指示、真实回手失效、装备和伤害跟随、场内反应移动后保留声明终点、两次回响独立目的地。每条接受命令检查隐私、玩家/旁观恢复、冷恢复、去重与完整动作/事件/状态回放。

[旧夹具输入对照](evidence/ride-the-wind-movement-2026-10-14/fixture-input-parity.json)保留原状态、种子、玩家/命令身份及选择，只为原预期己方基地移动补充必须的BASE:P1声明，并更正过时效果名称与先移动后活跃的事件顺序；没有重写接受的运行历史。原负例P1-BASE-UNIT在没有真实战场且终点为自身基地的旧局面仍非法，不删除该Theory行。新的正例证明基地单位可向真实战场移动。[中间失败](evidence/ride-the-wind-movement-2026-10-14/intermediate-corrections.json)保留原事件顺序失配及首次check的11,003通过/1失败：旧Hub测试缺少目的地。补充该测试原本预期的BASE:P1后15项定向检查通过，原方法及全部断言保留；[输入修正](evidence/ride-the-wind-movement-2026-10-14/check-repair-input-delta.json)保留这一个额外变更。

[550项相邻检查](evidence/ride-the-wind-movement-2026-10-14/adjacent-focus.json)通过；[9路径36条Godot控件命令读回](evidence/ride-the-wind-movement-2026-10-14/native-replay.json)、36冷恢复/去重/完整回放通过。验证脚本增加实际操作独立回响目的地控件，产品页面未修改。无窗口控件不替代可见双客户端。

本批同时保存[新卡牌来源版本](RECENT_SOURCE_CATALOG_REFRESH_2026-10-14.md)：中文1,502不变，英文新增26Radiance语言来源且16仅New标签移除，当前报告1,415/返回1,407；合并1,738精确身份仍全部NOT_REVIEWED，运行1,031/830不变，无预览提升。原规则文档确认日期分别保留，不把卡牌来源刷新冒充全部规则现场确认。

上一固定031a6ecd的[完整CI核对](evidence/ride-the-wind-movement-2026-10-14/fixed-031a6ecd-ci.json)通过11,128用例/6,988方法，新增43项/9方法、全部旧用例和110配对及最终哈希保留，full为2,688.10秒，网络恢复及macOS/Windows构建成功。[实际CI SQL](evidence/ride-the-wind-movement-2026-10-14/fixed-031a6ecd-sql.json)核对官方开局8命令/5恢复点及四条战斗历史56命令/148事件/60恢复点、篡改拒绝。该结果仅覆盖031a6ecd；本批及前一天顶实现分别验证，不据此前结果推定语义正确。

[本批修复后check核对](evidence/ride-the-wind-movement-2026-10-14/check-parity.json)为11,004用例/6,860方法、164.86秒，旧10,990/6,853全部保留，新增14项/7方法，2,243输入无漂移；明确连接55439独立集群的五项SQL实际执行后已停止该集群，Python27通过。[full计划](evidence/ride-the-wind-movement-2026-10-14/full-plan-parity.json)同一程序集包含7,001方法、原143完整对局全部保留，预计11,153用例/110配对seed424242；计划不是完整执行证据。新固定full仍须实际终态。其他移动/控制/替换/触发与全部地区差异、完整印刷费用和整卡/全源语义、可见双客户端、用户Windows、SQL并发故障及来源8项缺口继续开放，NOT READY。
