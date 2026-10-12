# 当前项目状态

2026-10-12：**NOT READY / 全量官方语义未认证**。旧Goal已删除，本轮治理与验证整改未创建替代Goal。

## 已验证基线

代码基线 5965604252cfae1807f814384f8b449538f2adba 的 [CI 38199241988](https://github.com/DDDHL0704/riftbound/actions/runs/38199241988) 成功：

- 10,201用例、6,625方法，零失败/跳过，无重复/缺失。
- 之前10,183用例/6,616方法全部保留，新增18用例/9方法。
- 11牌组、110双向配对、种子424242，动作/事件/状态回放完成。
- macOS/Windows构建与网络恢复步骤成功。
- full耗时1,358.87秒；普通片346.02秒，被延迟约855.66秒启动。

[长期manifest](evidence/project-governance-2026-10-12/ci-59656042.json)记录指定基线，不使后续改动自动继承通过。

## 全量范围与缺口

1,711卡牌身份、14,293规则/示例/QA记录全部NOT_REVIEWED；31条限定重审及733个精确引用（含待处理条款）、123个印次身份不等于完整认证。active的1,009牌已注册，但inventory未建立完整行为验证。

英文来源仍有8项报告/返回缺口；未来生效规则的适用性、所有共享规则消费者、客户端契约及全量逐项语义继续处理。

PostgreSQL真实执行、当前候选可见双客户端完整操作和Windows实机仍缺独立完整证据；return、打包和网络smoke不能代替。

[项目约定](PROJECT_GOVERNANCE.md)、[工作流](RULE_TEST_WORKFLOW.md)、[项目审查](REPOSITORY_AUDIT_2026-10-12.md)为当前入口。历史CURRENT/P阶段只供追溯。

## 本轮准备验证

验证工具24项检查、actionlint、固定快照治理/引用检查和6,625方法的调度成员对照通过；源代码、客户端、C#测试及官方数据没有改动。

准备提交 c379b4a53c701e7516602cbaa342d8ca9d3d96c4 已推送，[CI 38204782987](https://github.com/DDDHL0704/riftbound/actions/runs/38204782987) 在固定提交上执行。scope 任务成功，明确 full=true，来源/治理完整性输出保持 semanticCoverageCertified=false；完整规则与打包结果尚待读回。59656042 仍是上一完整基线。

本次独立文档提交用于验证轻量路径与后台代码回归可以并行，不能把轻量通过记为 full 通过。后续候选 [#1–#5](https://github.com/DDDHL0704/riftbound/issues) 已登记为 enhancement + needs-triage，未启动结构实施或新 Goal。
