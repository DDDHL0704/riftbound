# N13 · VEN008 无情打击

从 main `8ac0afba21574c94e7ce8a03ed83704524fee8e7` 独立移植精确CN37693/VEN·008/166 SC，3法力红色迅捷；可选弃另一手牌为确认期额外费用，战场单位伤害基数随已付事实固定为3或5。
2026-10-24重取[官网API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)及完整图，记录SHA `9027dd01da9b155351cb8f3457ead78d0faae6d4ee036296c9bb462f0f992f8b` /图SHA `25966367b4d01b983fe4e8f9aec549dafff3681dffc2412fe47a2df52db5ff3e` 与原核读完全一致，QA/勘误空，发布日期/生效日UNKNOWN，不合并全球身份。
定义末尾默认0字段DamageAmountIfDiscardHandCardOptionalCost仅008=5；现有单弃牌费用guard认新flag，保留原解析验证及原子支付；栈固定damage fallback从已验证plan取3/5，保持main可变符能优先；新增安全自己手牌选项helper及一处调用。去掉这两处Core hunk与prompt helper/call后分别与main字节一致，无DTO/栈形状、伤害加成/防止/死亡/时钟变更。
①已核实际diff并确认与③新有界分配版本的同表达式归属：③保留有界分配/后继及SPEND_POWER迁移，④负责固定弃牌3/5及单弃牌guard新flag；整合③版本时只在固定behavior.DamageAmount处嵌入3/5，不带回main旧plan.OptionalPowerCost。后合并者rebase后由最新实际L2覆盖双方；三个新增具名默认字段共存。本包验证当前main版本，未修改③分支。
原用例所需VEN093/122仅从PR148带入精确目录/来源/注册，未带旧WIP祖先/测试/共享隐私修改。共3记录新增，原1040逐项/顺序/scopedAdditions保留，总1043/功能分组842；原1009逐字段审计保持。
原12方法31例全部断言及数据保留；两处公开测试初态At调用改用本地夹具helper，维持实际占有/空战场的控制一致。独立真实命令驱动保留冷恢复、原日志、重复幂等、拒绝不变性与隐藏信息守卫。旧659/659分开归属。
固定源码 `c506381e76231f9e9b226f26a00fa70e48638391` 全项目构建零警告零错误，定向588/588、零跳过；本类31个TRX名称与重数等于旧31，原数据/断言完整。相同冻结运行文件另跑原11牌组双格式输入1/1，未跑110对局矩阵。29最终夹具77命令（61接受/16拒绝）、183事件；3中途前缀分列，2种原日志协调篡改拒绝。逐命令原回放/低层帧恢复/接受重复幂等/拒绝状态与双方视图不变；27个Export最终夹具JSON恢复，两次resolved Save保留逐命令帧恢复与回放。2488输入/1417运行文件前后核等，实际模块路径/SHA/MVID和RIF `8e79c66fcec5950ede044464686d70f45fe57a8584bf1965b515c71aa67beace` 另核。
独立反例 `722a47a5fcd6c6c5bf6481e5719b40ab11e93487` 仅将Core恢复为字节一致main，保持目录/定义/提示/原测试相同：两例无弃牌1过且实际3伤、付弃牌1预期红（Unsupported optional cost）；3份真实输出保留。其独立源/bin/RIF `e6453fc72553e643aa0c7cd8b3cff7996d75f243e585ff5de47db11d34bcd9e7` 不进入PR祖先。
本批 `main-source-test-receipt-v1.json` SHA256 `ec52b335c790fbbb9d829ce153d8e016f84a9539dfdcafe7f6d680987771ae67`；QA `var/qa/cards-direct-pr-migration-20261024/n13-main-port-v1/`，旧原件 `var/qa/ven-008-optional-discard-20261024/` 保留。按L2等本PR实际普通规则CI后合并；未新跑SQL/network/full/矩阵，未称普通牌组registry/整卡认证，净整卡0/后端NOT READY。
