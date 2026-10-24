# 当前项目状态 · 2026-10-24

**日常合入走 L2；完整后端验收尚未闭合。** 用户已授权四线在所属方法内自行实现、提 PR，最新 PR 合并输入的 `L2 required` 通过即可合 main，不要求每个 PR 人工批准。只有跨线同时修改同一方法才由①协调；普通 helper、完整包和逐表达式不再额外审批。

- **L2：** [backend-l2.yml](../.github/workflows/backend-l2.yml) 在 PR/main/manual 执行定向与相邻域断言、机制规则套件及原两条完整对局冒烟。汇总核对固定 SHA、测试程序集和实际 TRX；失败、跳过或缺产物不能绿，不依赖 SQL/网络汇总。
- **L3：** [native-desktop.yml](../.github/workflows/native-desktop.yml) 每天 02:00（上海）固定一次 main 起始完整 SHA，保留完整矩阵、真实 PostgreSQL/SignalR、进程重启和原日志回放。夜间失败由对应模块独立修复，阻止完整验收声明及相关改动，不冻结无关规则包。
- **当前交付：** CI/策略分支从 `8ac0afba…` 单独起步，未带入 `cc2bbe79…` 集成积压或三线未合并规则源码。当前 PR/运行结果以 GitHub 为准；本页不累计历史交付表。
- **历史候选：** A7=`da80739b…` / [run 40055102708](https://github.com/DDDHL0704/riftbound/actions/runs/40055102708) 保留原运行；04:15 UTC 收件时已核验 SQL 1/3 各 19 项，其余完整结果待收。A2–A6 原失败与修复线索保留，不作为所有日常合入的先决条件；B1/source queue 仅作迁移清单。
- **职责：** ① CI/验收脚本/环境及实际方法冲突；②战斗/触发/胜负；③费用/资源/合法性；④卡牌消费者/注册。只发送可交付包、真实冲突或阻塞，不发 ACK。
- **完成边界：** 当前适用来源、全部规则语义及 L3 仍有缺口；本次净整卡认证增量 0。客户端、可见双端、安装包和 Windows 实机留后续阶段 OPEN。旧两窗 Goal 保持 paused，固定证据/数据库保留。

[工作流](RULE_TEST_WORKFLOW.md) · [目标与分工](BACKEND_RULES_GOAL_2026-10-23.md) · [治理](PROJECT_GOVERNANCE.md) · [来源历史入口](audit/current-manifest.json)
