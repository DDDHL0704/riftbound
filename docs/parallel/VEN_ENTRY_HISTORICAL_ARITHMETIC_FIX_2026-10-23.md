# C1 追补：历史修正与后续永久变化

基线4c677328a33adfbb9e618967b8b309325da5ee5a；①已将C1集成为98571ebe并组成a6c1ccfd候选。本增量仅测试守卫、独立回归及本台账；运行源码/目录保持基线，待①组合验证。
- 真实合法红测：乐团+3给予ALLY，来源UNIT召回手中，+3结算为7，再实际打出OGN053给予增益使当前Power为8。`historical-current-power-red-v1`原1例失败保留，错误在新增守卫把历史ResultingPower=7等同后来的当前Power=8。
- 修复：只移除这项跨时间相等假设；每一历史修正仍以其自身BasePower、应用顺序和截至该次的Delta重建Effective/Resulting，打印金额、精确旧Finalized/Resolved、唯一ledger/effectId及所有原隐区扫描保留。原20类伪造用例未改；未扩展负向修正族。
- 验证：`historical-current-power-fixed-v2`实际63例/21方法通过，含三项新增完整协议序列；`historical-current-power-adjacent-v3`相邻增益/回收增益43例/19方法通过，均零失败/跳过。原20类伪造、任意额外JSON隐区ID、协调篡改及未记录的Power/增益变更仍被独立完整动作/事件/状态回放拒绝。
- 新合法序列包含后续增益、永久变化后再给第二次+3并召回第二来源、回合结束保留永久增益。原modifier记录逐项不变，后一次的历史BasePower可为5而前一次仍为4；当前状态验证与历史出处验证分开。
- 可取回原件：本树`var/qa/ven-unit-entry-20261023/`上述三目录；fixed-v2/original-journals中三份完整initial/final/commands/events分别为11/18/7接受指令，共36次原前缀冷恢复和完整回放，未改原接受历史。源码为本提交；净整卡认证0，固定候选验收仍由①安排。
