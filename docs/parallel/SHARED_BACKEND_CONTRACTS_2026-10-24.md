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
| S6 有限QA入口 | ③获批独立MatchSession.RestrictedManaScenario partial及两个固定dispatch：restricted-mana-lux-ogs / restricted-mana-lux-sp6；9个真实factory对象，固定普通1/受限0，显式保留RuleInputs=current.RuleInputs并设置敌方战场controller。空房两席→真实日志化DEV seed；不接受任意card/余额，不改权限。恢复只读核对后独立finish/verify final，③已交e729→84e9→ba57→79da；ba57单次185方法657/657及原657名称多重集已核，原102/111等失败保留；实际PG/API重启/tamper及组合check/full仍待①，当前候选不接 |
| C7 OGN104关联指示 | 核心359.3.e.14.a/b与CN31322：合法目标的RETURN被+3替换，独立“其拥有者召一枚休眠符文”仍执行一次；不产生返回事件或返回触发。忽略的失效目标/反制不执行关联后续。②沿原单RuneCallCountAfterTargetReturn字段实现，④补真实单RuneDeck与触发负控；既有OGN172的12/12不覆盖此路径，实际回归待交 |
| C7 连续选择时间 | 真实Echo第二次回答暴露原completion tick推演不足；仅私有C7凭据沿原指令state.Tick+1固定，命令tick照常递增。ValidRuleChoice恢复为原new(p.Origin,p.Answers)，无全局completion clock覆盖；原红保留，完整回归待②交付 |
| C7 消费者隐私守卫 | ④拥有测试源码；按JSON对象属性名递归精确匹配chosenActionReplacement，数组递归，普通sourcePath字符串不算字段。①核b98b811b补丁与实际旧泄露/普通字符串/嵌套属性4个对照；保持66身份及全部语义/回合末/回放断言，真实私有字段仍禁止 |
| C7 公开EffectId | 真实OGN051入场技能→Reaction OGN104来源回手→替换+3→Mighty，公开ledger/timing的EffectId仍嵌入私有来源ID。②只改新EffectId为原指令AppliedTick/occurrence/目标ID/代际/AppliedOrder；同目标ledger原max+1保持唯一与溢出守卫。原StackId/controller/source/card仍全在私有witness，严格shape/hash/出生重放不变；两原红及Echo/伪造回归待交 |
| C7 有限QA入口 | ②独立EmpoweredTargetReplacementScenario partial，两个id empowered-target-action-replacement / empowered-target-action-overflow；固定factory清单/current.RuleInputs/真实两席与DEV日志。普通两印次HAND起实际play/Empower；仅overflow显式表示int.MaxValue-1/已强化，固定高资源同样标QA。初态严格校验不能放宽，公共dispatcher/SQL/network由①接完整交付，未执行门禁仍OPEN |
| S7 移动强加费用 | ③：StandardMovement及MoveFieldUnitsToChosenLocation按真实同时移动组、当时有效AttachedRuleText实例计费；内部IMPOSED_MOVEMENT_POWER_PAYMENT复用RuleChoice与PaymentPlan.Other，无新DTO。效果执行者item.ControllerId付款；效果可拒绝，已确认资源与先前出牌费保留，整组不移动/不付强加费，独立指示继续。资源后重验代际/合法性，before-movement facts/observers门禁，传递live RunePools并沿既有结果返回；单个/逐次/基地移动无费。SelectedBattlefield只加movement调用pools参数，保留S5/C7方法边界；需扩大caller后续语义则再交精确冲突 |
| S7 有界资源数量 | ③：真实共享转换循环、普通Enumerable.Range和S5任意符能菜单均有大数枚举边界；仅两转换技能按真实输入/输出容量取max，范围含既有合法0，EnergyChannel保持固定+1。小范围精确选项，大范围复用RuleChoice/PAY_COST及内部RESOURCE_CONVERSION_AMOUNT，以long算区间、最多31次严格缩小选择；source/ability/range绑定请求，选量完才独立重验并横置/提交一次，无新DTO/客户端金额/共享时钟改写。普通转换与S5同共因菜单须复用有界工具，0/typed/Echo时机保持；原847/848失败和来源校准分列，完整实现/回归待③ |
| N9 强化源投影与严格恢复 | ④：旧source gen1与回手HAND gen2不能公开关联；两source predicate加EmpowerActivation非空，复用可见性；ValidateEmpoweredViews核对应stackItemId唯一行和每viewer的sourceId/visibility，缺失/重复/null均拒绝。玩家null-stack在既有ValidateAuthoritativeState入口记录required错误并跳过无效view，非空原守卫与spectator keyed校验保留。卡面/effect仍公开，owner/reveal照原权限；不改context/代际/原日志/C7Mighty。三类原红、合法边界及伪造回归已交并限定验证，SQL/API门禁仍OPEN |
| N9 有限QA与源码接入 | f50e125b完整源码按689c323a便携补丁接入①，保留S6资源视图校验，根1050输入定向221/221（含51新例），非来源1052的173复用。④获批独立partial和三个精确ven-empowered-self-{043,050,070} dispatcher，factory自身须拒绝其他suffix；固定factory清单/资源/current.RuleInputs，050五枚场上符文，后续真实play/Empower/回手/蟹死亡揭示。来源回手SQL/API重启完整入口仍待交，未进现有六case门禁 |

C7须保留旧floor/负历史守卫；失效的已选目标不能回退为对新代际的普通动作。受影响物体控制者决定同事件替换顺序，每实例一次；swap先锁差值，-0/不可执行行动没有替换。私下保留原动作与替换规则双源，公开不泄露witness；+3归因须有来源及下游断言。S6清池遵从所有玩家的正常主阶段开始/回合结束边界；混合池报价/实付/剩余一致，负数/溢出/伪造不能被归一化洗白。全原日志出生审计、拒绝回滚及原测试守卫均保持。

核对的既有CN核心SHA `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`：C7 370.2/372，S6 166–168/430；VEN FAQ SHA `59bd828207564540aa3c9885f9dfc39764034f633eb6fb84d49440c9edef4742`。S6 API原响应SHA `4dfaf40eda6e4f7b0a09624693c32f8f42933859fe12a9b1027a564a6aba44e2`。这些是所审输入，非全官网最新/整卡认证。①原件：`var/qa/related-backend-integration-20261024-v3/{c7-contract-review-v1.json,restricted-mana-shared-contract-review-v1.json}`。

C7新隐私接点：两精确印次的真实2→5 Mighty跨阈值原例2/2暴露私有chosenActionReplacement。①已审批准②仅在CapturePublicMightFrame克隆公开卡/ledger时清该字段；主CardObjects完整witness、缺失证明的严格拒绝、全部旧数字/来源/代际/层次和公开-权威frame严格比较保持，不新增DTO。原红转绿、其他捕获caller/JSON/cold/出生审计与伪造拒绝待②实际回交；本线原件c7-public-might-frame-privacy-contract-v1.json。
