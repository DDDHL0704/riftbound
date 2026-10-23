# 后继包共享接点 · 2026-10-24

这些是①核读来源、代码及交付提案后分配的最小接口；均排除固定候选`cbcf5c24` / Actions `39998674123`，不代表实现或后端验收通过。

| 接点 | 主责 / 已审范围 |
|---|---|
| C7 目标行动替换 | ②：显式真实选择、当前代际且可执行的STUN/负Might/RETURN原子规划；PowerModifierLedgerEntry可空私有ChosenActionReplacement，独立于旧ChosenNegativeMight；一个临时occurrence序列及现有RuleChoice复用，无新StackItem/Repeats/continuation序列化 |
| C7 精确消费者/profile | ④已交`d93d2b1f`：CN37984 / VEN·086与37985 / VEN·181；`EmpoweredTargetReplacementSpec(IReadOnlyList<string> ActionKinds, int MightBonus)`，`EmpowerParser.TargetReplacements(text)`。①核对来源/原Parser字节保留，已转②五文件依赖（1045→1047，排除来源线1049全局预期）。v1只0/1个完整复合条款，三动作共一替换实例、bonus3；身份来自既有独立规则文本实例，不用数组下标造身份。来源82/82仅前置1049输入，②实际替换/恢复与④完整消费者语义仍OPEN |
| S5/C7 SelectedBattlefield | ③S5 `007a72da`已交、待审；②后续只在实际该方法上给instructionState追加已付局部RunePools快照、既有Might调用追加chosen proof，保持付款/逐次执行/DamageAmount/伤害死亡顺序 |
| S6 受限法力 | ③：RunePool.SpellOnlyMana默认0，Mana/Power含义不变；构造/保留/Normalize/Equals/hash/状态/公共资源投影/严格恢复；PaymentPlan明确服务端确认的PlaySpell用途，其他用途默认不得消耗；最小跨partial字段保留，不改别线算法 |
| S6 精确目录依赖 | ④已交`16f1e19f`，①已核API/记录/卡图及原目录行；为③的`d430fa77`制成三文件补丁（1040→1041），已转③，实际S6验证待回交。本线1047全局数量与依赖缺失辅助类的新测试不移入③；源2/2来自未提交Plank的1049输入，不能冒充独立输入通过。当前候选不接此印次 |
| S6 报价标量 | ③：PlayCostBreakdownDto的record body加可选AvailableSpellOnlyMana:int（0省略）/RemainingSpellOnlyMana:int?（不可付null），不改原位置参数/命令/页面。均为本次用途授权的受限部分，包含于原行动可用/剩余总量；非PlaySpell为0。钱包实际受限余额仍独立保留，报价不改池，不能为凑总量清零；相同PaymentPlan推导与JSON/混合池回归待实际验证 |

C7须保留旧floor/负历史守卫；失效的已选目标不能回退为对新代际的普通动作。受影响物体控制者决定同事件替换顺序，每实例一次；swap先锁差值，-0/不可执行行动没有替换。私下保留原动作与替换规则双源，公开不泄露witness；+3归因须有来源及下游断言。S6清池遵从所有玩家的正常主阶段开始/回合结束边界；混合池报价/实付/剩余一致，负数/溢出/伪造不能被归一化洗白。全原日志出生审计、拒绝回滚及原测试守卫均保持。

核对的既有CN核心SHA `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`：C7 370.2/372，S6 166–168/430；VEN FAQ SHA `59bd828207564540aa3c9885f9dfc39764034f633eb6fb84d49440c9edef4742`。S6 API原响应SHA `4dfaf40eda6e4f7b0a09624693c32f8f42933859fe12a9b1027a564a6aba44e2`。这些是所审输入，非全官网最新/整卡认证。①原件：`var/qa/related-backend-integration-20261024-v3/{c7-contract-review-v1.json,restricted-mana-shared-contract-review-v1.json}`。
