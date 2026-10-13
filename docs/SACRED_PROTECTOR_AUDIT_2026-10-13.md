# 神圣守护者的战斗伤害许可

2026-10-13，Goal active，平台 **NOT READY**。本批新增当前非预览中文 VEN·129，以共享条件/伤害贡献求值补齐“除非同战场恰有一个己方其他单位，否则无法造成战斗伤害”。

[中文回读](evidence/sacred-protector-2026-10-13/source-readback.json)保存官方接口、取得时间、ID37829和完整源记录哈希；[英文](evidence/sacred-protector-2026-10-13/en-current.json)保存精确 ven-129-166 / VEN-129/166，中英文身份分别记录，不合并分母。[官方发布](evidence/sacred-protector-2026-10-13/vendetta-release.json)保留7月31日发售依据，未知单卡生效日期不补造。[中文核心](evidence/sacred-protector-2026-10-13/current-core.json)现场取得字节/hash与当前适用快照一致；[精确条款](evidence/sacred-protector-2026-10-13/core-clauses.json)包括465.2伤害贡献与同时造成、466.1伤害清除及423.1眩晕区别。[FAQ](evidence/sacred-protector-2026-10-13/vendetta-faq.json)保留来源取得日期和适用性；未来10月16日核心不提前应用。

[原1025条记录逐项保留](evidence/sacred-protector-2026-10-13/catalog-input-parity.json)，当前目录1026条/827功能单元。注册派生的Implemented、RepresentativeRulePass及目录统计不是语义认证。新静态spec进入原文/复制文字实例，原慎的弟子条件关键词与本批共用当前战场、当前控制者和正面朝上UNIT的计数；来源自身、基地、其他战场、对手和隐蔽待命不计数。

在伤害步骤前重新判断条件；不改变单位当前战力、法术承伤、按战力选择目标或致命阈值。实际结算及分配提示统一调用引擎的伤害贡献求值，修复defend-two初始提示仍报6而引擎应贡献0的错误。同时伤害保持分配时的共同局面，伙伴在同次伤害中死亡不会撤销已允许的贡献。

新增测试覆盖0/1/2伙伴进攻/防守、合法回手/眩晕响应、主行动控制转移触发战斗、效应复制与眼镜复制、法术两伤加实际三伤后存活和战斗清除、正确分配者窗口拒绝零贡献来源伪造、伙伴同时死亡、隐蔽待命/基地单位以及六战力目标上限。所有接受命令检查隐私、冷恢复、去重、玩家/旁观恢复与完整动作事件状态回放；11套原预组实际输入JSON仍完全相同。

[中间失败与修正](evidence/sacred-protector-2026-10-13/intermediate-corrections.json)区分真实引擎提示不一致与测试观察、费用/目标/时机及夹具标签排序错误；原方法/Theory行未删。天顶之刃当前只实现眩晕、缺少可选移动，作为独立消费者缺口保留，不用本批通过覆盖该问题。

[原生服务端读回](evidence/sacred-protector-2026-10-13/native-replay.json)为19路径135条生产控件命令、135次冷恢复/去重和完整回放；[相邻检查](evidence/sacred-protector-2026-10-13/adjacent-focus.json)595项/393方法通过，新增23用例/12方法。无窗口控件证据不替代可见双客户端。[本批check](evidence/sacred-protector-2026-10-13/check-parity.json)10,936用例/6,838方法、161.73秒全部通过，原10,913/6,826保留，新增23/12；2,236冻结输入不变、Python27和真实专用SQL五项smoke通过，实例已停止。[完整计划](evidence/sacred-protector-2026-10-13/full-plan-parity.json)6,979方法/预期11,085用例，原143对局方法和110配对/seed424242、11套具体牌组JSON保留；新固定提交完整CI仍须独立终态。本批单位由来源精确的中盘局面进入；原手牌打出/费用/再进场所有组合未单独认证。所有整卡/全源、英文接口8项数量缺口、可见双客户端、用户Windows和完整SQL并发/故障范围继续开放，NOT READY。
