# N13 · VEN008 无情打击

从 main `8ac0afba21574c94e7ce8a03ed83704524fee8e7` 独立移植精确CN37693/VEN·008/166 SC，3法力红色迅捷；可选弃另一手牌为确认期额外费用，战场单位伤害基数随已付事实固定为3或5。
2026-10-24重取官网API及完整图，记录SHA `9027dd01da9b155351cb8f3457ead78d0faae6d4ee036296c9bb462f0f992f8b` /图SHA `25966367b4d01b983fe4e8f9aec549dafff3681dffc2412fe47a2df52db5ff3e` 与原核读完全一致，QA/勘误空，发布日期/生效日UNKNOWN，不合并全球身份。
定义末尾默认0字段DamageAmountIfDiscardHandCardOptionalCost仅008=5；现有单弃牌费用guard认新flag，保留原解析验证及原子支付；栈固定damage fallback从已验证plan取3/5，保持main可变符能优先；新增安全自己手牌选项helper及一处调用。去掉这两处Core hunk与prompt helper/call后分别与main字节一致，无DTO/栈形状、伤害加成/防止/死亡/时钟变更。
已向①报告与③新有界分配版本的真实同表达式交点：其固定behavior.DamageAmount处应仅嵌入已付弃牌3/5；不可将main旧plan.OptionalPowerCost分支带回新有界分配。此包只对当前main验证，未改③分支。
原用例所需VEN093/122仅从PR148带入精确目录/来源/注册，未带旧WIP祖先/测试/共享隐私修改。共3记录新增，原1040逐项/顺序/scopedAdditions保留，总1043/功能分组842；原1009逐字段审计保持。
原12方法31例全部断言及数据保留；两处公开测试初态At调用改用本地夹具helper，维持实际占有/空战场的控制一致。独立真实命令驱动保留冷恢复、原日志、重复幂等、拒绝不变性与隐藏信息守卫。旧659/659分开归属。
本主线构建、定向及实际L2 CI待验；QA `var/qa/cards-direct-pr-migration-20261024/n13-main-port-v1/`，旧原件 `var/qa/ven-008-optional-discard-20261024/` 保留。净整卡0/后端NOT READY。
