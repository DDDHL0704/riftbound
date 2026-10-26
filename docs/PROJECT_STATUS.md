# 当前项目状态 · 2026-10-27

**后端规则优先；日常合入走 L2，完整后端验收仍 OPEN。**

本轮有限清积压已完成：[PR165](https://github.com/DDDHL0704/riftbound/pull/165)普通合入 main `645a045e435dbb4521eca5e5318c10ffcee36511`，12个原完整head及各公开head均为main祖先，12个PR实际merged，开放PR已核验22→10。本轮“到≤10前冻结新增功能PR”的条件已达成；后续按所有权、最新L2和串行main继续。①保持无Goal，接续既定S3/P7验收接线有限任务。

- **L2：** PR165实际合并输入 `fca4c4a4…` 的[完整L2 40317815151](https://github.com/DDDHL0704/riftbound/actions/runs/40317815151)及[main自身L2 40337538756](https://github.com/DDDHL0704/riftbound/actions/runs/40337538756)均自然成功。main实际规则8,086方法/15,313例、定向7,142/13,301，零fail/skip；两次程序集与原件各归实际SHA，不复用PR终态。strict up-to-date、Actions15368、管理员约束、人工批准0保持。
- **已修复：** P7确认资源独立提交并保留原Origin/Intent/Command/Answers；投降/冷恢复不退款，父指令不重复发布资源事件。资源触发保持原确认tick，新父触发使用实际完成tick并稳定排序，独立恢复校验保留。原3a14四例红及其[失败run40294321840](https://github.com/DDDHL0704/riftbound/actions/runs/40294321840)、旧87a绿灯与原反例均保留，未重跑/取消；大原件留ignored var/qa或CI。
- **当前候选：** [PR164](https://github.com/DDDHL0704/riftbound/pull/164)的`62c7479…`完整依赖#142/#153/#157/#158/#161/#163，保留TypedCommand与原错误码；58/58定向及[真实S3/P7 SQL/双玩家重连/进程重启harness40358525879](https://github.com/DDDHL0704/riftbound/actions/runs/40358525879)已通过，[最新完整L2](https://github.com/DDDHL0704/riftbound/actions/runs/40358525844)仍执行。[PR183](https://github.com/DDDHL0704/riftbound/pull/183)后继在完整`62c`与`521dd4e…`普通祖先组合上验证VEN110及独立触发；旧379/6和各在途输入分别保留，新组合仍须最新L2。原69项SQL/四片、17项selected-field、完整规则/原生、18个精确L3方法及nightly固定SHA的S3–S6均保留。两候选尚未合main，相关harness不等于完整L3。
- **L3：** 每天02:00上海时间只固定一次main完整SHA，保留完整规则、真实PostgreSQL/SignalR、进程重启和原日志回放；失败阻止完整验收及相关改动，不冻结无关L2绿包。L2不依赖L3。
- **职责：** ①CI/验收工具/治理及实际同方法交集；②战斗/触发/胜负与恢复；③费用/资源/合法性；④卡牌消费者/注册。普通helper不额外审批，只发完整交付、真实冲突或阻塞，不发ACK。包分支见[映射](BACKEND_RULES_GOAL_2026-10-23.md#四线与共享接点)，实际checkout在操作前以本树Git核对。
- **完成边界：** 适用来源、全部规则语义及固定输入L3仍有缺口，净整卡认证增量0。新私有费用/卡牌/战斗包未默认交付main。客户端、可见双端、安装包和Windows实机留后续阶段OPEN；旧Goal不记完成，冻结源/bin及数据库保留。

[工作流](RULE_TEST_WORKFLOW.md) · [目标与分工](BACKEND_RULES_GOAL_2026-10-23.md) · [治理](PROJECT_GOVERNANCE.md) · [来源历史入口](audit/current-manifest.json)。[历史A7](https://github.com/DDDHL0704/riftbound/actions/runs/40055102708)及A2–A6原失败/修复线索保留，B1/source queue仅作迁移清单。
