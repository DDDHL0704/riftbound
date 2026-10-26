# 当前项目状态 · 2026-10-26

**后端规则优先；日常合入走 L2，完整后端验收仍 OPEN。**

本轮清积压目标为开放 PR 从22降至≤10；到门槛前冻结新增功能PR，先修现有红测、依赖与真实冲突。①保持无Goal，连续完成有限清积压并串行发布main。五个新聊天已接管，[活跃映射及实际分支](BACKEND_RULES_GOAL_2026-10-23.md#四线与共享接点)为唯一入口，旧窗只读。

- **L2：** 每个PR/main执行定向与相邻断言、机制规则及原两条完整对局冒烟。最新实际base/head/synthetic的 `L2 required` 必须完整成功、零skip；缺产物、错SHA、失败不能绿。strict up-to-date、Actions15368、管理员约束、人工批准0保持。
- **L3：** 每天02:00上海时间固定一次main完整SHA，保留完整规则、真实PostgreSQL/SignalR、进程重启和原日志回放。夜间红阻止完整验收及相关改动，不冻结无关L2绿包。
- **本批候选：** [PR165](https://github.com/DDDHL0704/riftbound/pull/165)以main `99c0cb6c…` 为基线，普通merge保留最后12个完整原head。修复已确认资源在投降时回退的P7缺陷：真实child差量及事件独立提交，保留外层未确认指令、严格恢复与原历史。实际head/synthetic及最新完整L2见[PR检查](https://github.com/DDDHL0704/riftbound/pull/165/checks)；只有逐项验明原head为main祖先并核对真实PR状态后才计交付。
- **原验证：** 旧87a的[L2 40281144019](https://github.com/DDDHL0704/riftbound/actions/runs/40281144019)及[harness 40281144121](https://github.com/DDDHL0704/riftbound/actions/runs/40281144121)均自然成功、未取消/重跑，但其P7反例仍成立，绿灯不复用于修复候选。原红、修复收据和大日志留ignored var/qa或CI，Git只保留源码、测试和短记录。
- **职责：** ①CI/验收工具/治理及实际同方法交集；②战斗/触发/胜负及P7消费恢复；③费用/资源产生端；④现有卡牌消费者。普通helper不额外审批，只发完整交付、真实冲突或阻塞，不发ACK。
- **完成边界：** 适用来源、全部规则语义及固定输入L3仍有缺口，净整卡认证增量0。客户端、可见双端、安装包和Windows实机留后续阶段OPEN；旧Goal不记完成，冻结源/bin及数据库保留。

[工作流](RULE_TEST_WORKFLOW.md) · [目标与分工](BACKEND_RULES_GOAL_2026-10-23.md) · [治理](PROJECT_GOVERNANCE.md) · [来源历史入口](audit/current-manifest.json)。[历史A7](https://github.com/DDDHL0704/riftbound/actions/runs/40055102708)及A2–A6原失败/修复线索保留，B1/source queue仅作迁移清单。
