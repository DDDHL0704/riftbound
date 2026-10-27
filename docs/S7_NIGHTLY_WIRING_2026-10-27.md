# S7 imposed movement：夜间入口与有限接线验证

实际依赖是完整费用头 `febf29723422ef824966e55702d3d3152f6ec31b`，包括 `tests/Riftbound.ImposedMovementAcceptance` 及其 API、PostgreSQL、原日志恢复依赖。此前主线3839和该费用头的支付 runner 都只登记四个驱动；夜间直接使用默认表，因此项目存在、编译成功并不代表夜间会运行 S7。

本变更把 `imposed-movement` 登记到默认 `PROJECTS/CASES` 及 `FINISH_CASES`。默认夜间现在包含该驱动；原四项的阶段顺序保持。缺项目或任一阶段失败必须使实际运行失败，不能跳过。原69项SQL身份、四分片、18个精确L3方法、原完整对局及断言不改。

| 入口 | 支付驱动范围 |
| --- | --- |
| 02:00上海时间的固定main夜间L3 | 原四驱动与imposed-movement；所有子任务沿用一次选定的完整main SHA |
| 普通PR的既有harness | 原显式payment-resource-surplus、instruction-power-payment两项 |
| 显式手动harness，imposed_movement=true | 只选imposed-movement作为支付驱动；原通用SQL/SignalR进程重启仍保留 |

手动入口用于本次接线的有限实际调查，不给普通规则包增加周期性重型运行，也不新增schedule。workflow按 `github.sha` checkout和校验本次选定的ref；[GitHub手动运行说明](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow?tool=cli)给出分支ref与输入的调用方式。

S7驱动自己的四项是 `standard-pay`、`standard-decline`、`effect-pay`、`effect-decline`。实际阶段必须依次为SQL、prepare、启动API、play、verify(before)、正常停启为不同PID、recover、verify(after)、finish、verify(final)、正常停止、tamper。恢复验证失败不得进入finish/tamper。双方成功Joined、轮换token、原七表、原命令事件、独立冷恢复及协调伪造拒绝均由原驱动保留；不修改生产规则或接受历史。

新增Python负控确认旧表遗漏S7且漏掉finish/final；修复后检查阶段顺序、恢复失败清理以及缺S7项目时默认运行失败。这些注入回调只证明脚本契约，不能当作真实SQL/API/进程重启。完整依赖的三个项目编译和identity观察仅归原febf输入，原63/62/494/135等L1不重标为本输入。

实际Actions结果与可取回原件记录在对应PR和本线ignored `var/qa/s7-nightly-wiring-20261027/`。四个场景是原空房入座加有限QA出生；无论本次接线验证是否通过，都不扩为普通合法牌组开局、完整矩阵、整卡或全后端认证。已知旧nightly645a的8条完整对局及2处战斗SQL归因另行处理。
