# 待命与替代费用的预览明细
- 问题：[Issue116](https://github.com/DDDHL0704/riftbound/issues/116)；基线3400539606a076fb8fd69785fcc4ef7a9b54bd30。只补PreviewPlayCard的减免/替代费用说明；应付、扣款、候选、DTO形状、时序、SQL与恢复实现不改。
- 来源沿用固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11 的356.1.a–b、811.1.b；手雷现有来源docs/evidence/dancing-grenade-2026-10-14/source-current.json。不是新来源全集或整卡认证。
- 新4方法10例：真实待命布置跨回合、手雷再次打出/法盾/不足、观察后替代或免费打出、普通/急速对照；原生产确认3PASS/7明细FAIL。首轮新夹具tick/跨回合清池错误及修正原因另留原件，不算生产缺陷。
- WIP定向与相邻旧预览/restricted mana/Echo/Flow共90/90、0skip；所有旧测试源码未改，新确认测试逐字节保留。提交后另重建固定源码验证，不把WIP版本当作固定提交。
- 10个新中局场景实际54条接受命令/177事件：每步原生帧校验、恢复及冷重复，全部原日志回放通过；原/修复的所有原始请求、非身份日志字段及完整事件相等，各自实际RuleInputs/MVID与完整状态哈希单独保留。
- 原件与运行时证据：var/qa/play-cost-breakdown-20261024/{original340-v1,original340-v2,wip-v3}/、original340-confirmed-v2.json、wip-proof-v4.json；固定提交结果另留同目录receipt/proof。无普通出生、SQL/API实网重启、重型全矩阵或后端READY结论。
