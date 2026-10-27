# 黛安娜：获得时点与法术对决内支付

PR215 的完整普通后继，实际验证输入 `96c4c98d60919a9f43209ebca334d8ef1b17d9a2`；继承原公开3199及main `aa529812826a53e408d4ae85a50b7f2f296da62f`。main仅带入现行CI参数行分片依赖，本线未修改CI、正常目录、registry或累计旧拒绝守卫；不含控制移动、No078/No124及②PR219/221。

新鲜CN记录及完整卡面已亲检：UNL-197/219、UNL-234/219、UNL-234*/219均是“反应；横置获得1法力；此法力仅可在法术对决期间消耗”。三印次errata为空、非预览，正常目录玩法字段逐项一致。官方[基础卡面](https://cdn.playloltcg.com/lol/card/20260319/d20d7e7b06b444489a45752ec0fd3af4.webp)及另两卡面/原响应保留在source-v1；发布/生效日期和完整适用性仍UNKNOWN。UNL079实际洞察付款调用者也取回记录和卡面，其非空勘误及原打印文本分别保留，不记作无勘误。

旧实现错误地禁止主阶段合法产费，并把法力存为普通余额，使真实对决结束后仍可支付主阶段装备。现在独立保存DuelOnlyMana，取得使用反应资源时点，花费由当前权威TurnWindow.IsSpellDuel决定，预览、扣账、转换、内部指示、支付提示和物理容量共用该边界。没有上下文不能授权该桶。原始TimingState为NEUTRAL_CLOSED时，真实堆叠仍可能属于法术对决，不能只查该字符串。

| 固定输入/原件 | 实际结果及边界 |
| --- | --- |
| ad3bff5f / original-boundaries-v4 | 3方法9例3P/6F；合法对决支付控制3P，主阶段产费被拒3F、真实对决结束后泄漏3F；完整实际结果保留 |
| b54a8050 / closed-duel-and-capacity-counterexamples-v10 | 8例4P/4F；真实关闭对决付款3F及满容量菜单1F，三条合法开局及恰好剩一空间控制已通过 |
| 96c4c98d / complete-scoped-resource-and-original-ci-v16 | 82方法377/377、零skip；原372/249及两份164的显示名和重数全部保留，原红逐名转P |

原run40537875102固定3199/base406d/synthetic e3a15f82完整失败；下载保留失败分片1/2/4的原TRX、收据、模块审计及三个ZIP哈希，未取消、改标或改绑。五条旧测试校准为受限符能、无待付卡前提的合法产费、支付窗口LEGEND_ACT及仍存在的源码边界；保留原方法及有意义拒绝/不扣费断言。新校准中e550输入376P/1F的错误拒绝码夹具也保留，最终使用对手堆叠来源验证原InvalidTarget断言。其余编译失败、开局端点/符文/优先权夹具错误和真实规则红分输入列明，数字不相加。

最新2698个Git输入匹配原Git blob；712/712 tracked Conformance CS实际编译；5个实载模块Info/MVID/SHA及保留DLL字节均核验。30份黛安娜原件含18条显式中局实际命令流程、9条真实产费后投影校验、3条空房间→合法40/12/3牌组→Ready/Mulligan→产费→换回合清空的开局。开局每命令实际去重及生产registry冷恢复，三种内部投影一致的改用途/增发/抹除余额伪造共9条均由原历史审计拒绝，未放宽恢复守卫。另2条容量和4条JSON表示例不宣称完整玩法历史。

完整入口 `var/qa/diana-duel-mana-20261028/complete-diana-resource-delivery-v17.json`，SHA256 `8058b3b15af772cacd1cd3b94ba6c81ed3356dd9a4ccff4a68bfbd849052daad`；最新TRX `001fbe6ff8ac45533c04350130c084895b9d08a2af90a68b4da57cc7ea1d3600`。原PR215费用来源、原生窗口及旧输入见[前包证据](PENDING_LEGEND_RESOURCES_2026-10-28.md)。main依赖的20条Python单测另列，仅验证CI工具。

Issue200/220保持OPEN；最新完整候选须自己的完整L2 required，未绿不得合。SQL、SignalR、进程重启、固定main完整L3、其余消费者与可选交互、整卡及全后端均仍OPEN。
