# 对决链弃牌选择与严格冷恢复限定审查

适用范围：中国区、zh-CN、截至2026-10-17；核心规则2026-10-16生效。
[来源manifest](evidence/triggered-duel-continuation-2026-10-17/source-manifest.json)保留首次范围和后续扩大范围的独立记录，当前核心PDF、14项官方目录和本次精确卡牌返回均已现场复核。
所有原来源与未闭合义务保留，不将本批分支结果提升为整卡或规则族认证。

## 问题与实现

固定067完整CI实际失败：[终态与产物收据](evidence/triggered-duel-continuation-2026-10-17/full-ci-067483dc-failure.json)。普通规则12097例通过，三条原完整对局失败，其他游戏分片未完成；SQL与网络跳过。失败在本地原方法重现，旧记录保留。

- 旅行商人触发的弃一抽一会把对决错误恢复为中立时机；空手分支及多触发排序也可能错误传递焦点。340.2.a与346.1要求区分普通牌起源和触发起源，在整条链、选择及插入响应结束后分别传递或保留焦点。
- 卧底特工绝念在对决中的时机标记不是旧的固定ORDERED_TRIGGER字符串，旧分派会误将坟场来源当单位重新打出。分派改用已有ordered-堆叠身份与精确效果类型，保留实际绝念选择。
- 待命生产端追加标签后未规范排序，JSON构造时重排导致原始状态哈希变化。修正生产端标签排序，未放松哈希或恢复守卫。
- 攀圣长阶提高所有玩家的胜利分数。独立恢复检查错误要求该战场由其物理区域玩家控制，漏算被对手控制的战场。独立按去重战场身份计算，不复用生产计算作为期望。

相关问题：[53](https://github.com/DDDHL0704/riftbound/issues/53)、[54](https://github.com/DDDHL0704/riftbound/issues/54)、[55](https://github.com/DDDHL0704/riftbound/issues/55)、[56](https://github.com/DDDHL0704/riftbound/issues/56)。恢复状态显式绑定对决链的后续焦点，保留独立结构检查和完整原始回放；不添加旧开发局迁移，不改写已接受历史。

## 已执行范围与测试保留

[42例定向收据](evidence/triggered-duel-continuation-2026-10-17/focus.json)覆盖31方法，其中新增5方法/11例；原15项卧底特工和16项移动/原生相邻测试保留。
新增覆盖空手、一个或多个弃牌候选、多个移动触发、四个其他消费者、真实响应插入、普通牌起源的弃两抽两，以及恢复焦点的结构和因果伪造拒绝。

原种子31从真实空白开局到正常得分终局，完成原始行动/事件/状态回放、JSON往返、实际registry冷恢复和恢复后原下一命令比对。胜利分数9独立核对，伪造8拒绝。显式中盘夹具使用限定检查点回放，不冒称原始空白开局。

[首次冻结源码核对](evidence/triggered-duel-continuation-2026-10-17/original-source-parity.json)确认原7410方法、143完整对局全部保留，当时440份原Conformance源码中439份逐字节不变，110矩阵方法逐字节相同。原FullGame文件保留全部2869行原Assert和225行原种子相关内容，增加金币定义存在断言；两处驱动先结算真实移动触发，奥恩中盘明确布置长剑与金币，原6点伤害断言保留。

[23条原完整对局](evidence/triggered-duel-continuation-2026-10-17/original-games.json)全部通过，其中原[110双向配对](evidence/triggered-duel-continuation-2026-10-17/matrix110.json)的完成集合与11牌组全部期望组合精确相同，原种子424242及胜利/回放断言保留。3195非文档输入和固定程序集至运行结束无漂移。

[本批一次check](evidence/triggered-duel-continuation-2026-10-17/check.json)实际12110例/7274方法，12108通过、2失败、零跳过；原12099例/7269方法身份全部保留。两条失败单独重现：Swift原断言仍要求NEUTRAL_CLOSED，应按331.1/340.2.a保留SPELL_DUEL_CLOSED；攀圣长阶恢复夹具缺战场标签，实际未施加加分。修正前提后新增真实9分、伪造8分拒绝及对决焦点断言，原恢复拒绝谓词和其他Swift断言保留。

[最终46例/34方法focus](evidence/triggered-duel-continuation-2026-10-17/final-focus.json)全部通过，包含前述42例和两条原check失败身份。[最终源码核对](evidence/triggered-duel-continuation-2026-10-17/original-source-parity-final.json)记录437份原文件逐字节相同、三份精确修改及全部方法/Theory保留；[原对局后仅两处测试输入变化](evidence/triggered-duel-continuation-2026-10-17/post-originals-test-only-corrections.json)不改变生产实现与原FullGame输入，不重复无变化矩阵或第二次check。check失败不改写为通过；新固定提交full必须覆盖最终完整输入。

[验证汇总](evidence/triggered-duel-continuation-2026-10-17/validation-summary.json)另记录55项Python工具通过。当前真实SQL、可见双端、网络/冷重启和Windows实机各自独立待验。

## 剩余义务

[精确既有义务关联](evidence/triggered-duel-continuation-2026-10-17/existing-obligation-links.json)连接条款、印次和当前来源版本；原118份审查字典不改写。[输入重开记录](evidence/triggered-duel-continuation-2026-10-17/prior-context-input-updates.json)将受共享输入影响的旧上下文重新标为CI待验证，历史CI保持原提交身份。

[剩余消费者与验收](evidence/triggered-duel-continuation-2026-10-17/remaining-consumers.json)保留全部旧16534义务，并显式新增本批五个行为分支和一个验收门禁，合计16540项。其他组合、消费者、来源缺口、issue52及旧067原生初始调度显示验收仍OPEN；当前工具取不到原生窗口不能推断程序卡住或Mac锁定。Goal active / NOT READY。
