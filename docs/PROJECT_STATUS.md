# 当前项目状态 · 2026-10-24

**日常合入走 L2；完整后端验收尚未闭合。** 用户已授权四线在所属方法内自行实现、提 PR，最新 PR 合并输入的 `L2 required` 通过即可合 main，不要求每个 PR 人工批准。只有跨线同时修改同一方法才由①协调；普通 helper、完整包和逐表达式不再额外审批。

- **L2：** [backend-l2.yml](../.github/workflows/backend-l2.yml) 在 PR/main/manual 执行定向与相邻域断言、机制规则套件及原两条完整对局冒烟。汇总核对固定 SHA、测试程序集和实际 TRX；失败、跳过或缺产物不能绿，不依赖 SQL/网络汇总。
- **L3：** [native-desktop.yml](../.github/workflows/native-desktop.yml) 每天 02:00（上海）固定一次 main 起始完整 SHA，保留完整矩阵、真实 PostgreSQL/SignalR、进程重启和原日志回放。夜间失败由对应模块独立修复，阻止完整验收声明及相关改动，不冻结无关规则包。
- **当前交付：** 独立 CI/策略 [PR #120](https://github.com/DDDHL0704/riftbound/pull/120) 经实际 PR L2 通过后已合 main=`907f9443…`，未带入 `cc2bbe79…` 集成长分支积压。main 自身 [L2 run 40076127398](https://github.com/DDDHL0704/riftbound/actions/runs/40076127398) 及 `L2 required` 全部成功，原始 TRX/汇总已核对定向9,850例、机制13,946例（范围重叠，不相加），零失败/跳过、同一测试程序集。main strict 保护已要求 `L2 required`，人工批准数0；nightly 已启用。
- **历史候选：** A7=`da80739b…` / [run 40055102708](https://github.com/DDDHL0704/riftbound/actions/runs/40055102708) 原运行最终失败；四片SQL共76个执行身份原件已核验通过，普通规则15,129例中41例失败。151例完整对局通过，但原11牌组输入不变式失败，不能认证原矩阵。原失败已按领域交付，未取消、重跑或移动候选；A2–A6原件保留，B1/source queue仅作迁移清单。
- **职责：** ① CI/验收脚本/环境及实际方法冲突；②战斗/触发/胜负；③费用/资源/合法性；④卡牌消费者/注册。只发送可交付包、真实冲突或阻塞，不发 ACK。
- **完成边界：** 当前适用来源、全部规则语义及 L3 仍有缺口；本次净整卡认证增量 0。客户端、可见双端、安装包和 Windows 实机留后续阶段 OPEN。旧两窗 Goal 保持 paused，固定证据/数据库保留。

[工作流](RULE_TEST_WORKFLOW.md) · [目标与分工](BACKEND_RULES_GOAL_2026-10-23.md) · [治理](PROJECT_GOVERNANCE.md) · [来源历史入口](audit/current-manifest.json)
