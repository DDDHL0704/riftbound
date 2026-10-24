# 两个结算付款菜单的符文容量移植
- 独立基底PR136/76049630，只取ff3008956的两个手工TAP/RECYCLE菜单守卫与原28例/审计，不依赖S7或混入S4篡改/销毁替代钩子。
- TargetSpellControllerPaysCounterCost及ChooseTokenHaste每轮读取实际working pool；普通+受限法力用CanGainSpellOnlyMana(pool,1)，符能以HasSupportedAmounts及long总和检查。int.MaxValue仅为实现表示边界，不是官方游戏资源上限。
- 保留其他合法来源、自愿获取、正确付款者、PAY/DECLINE、急速与入场/后继指令、Frame/clock、恢复/DTO/注册及原始历史；只防止菜单提供必定拒绝或生成超支持范围预览的选择。
- 依据固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11的164.2、166-168、445.2.c、805与原四精确API原件，var/qa/rule-choice-rune-capacity-20261024/source/；原红/初编译错误和旧140相邻仅作分别限定的历史，不冒充本次移植结果。
- 新数值边界fixture与真实自然积累区分，冷恢复/本fixture完整原命令事件回放与普通出生/真实SQL/API重启区分。原相邻方法/Theory及FullGame保留，当前定向/父源对照/实际模块身份位于var/qa/payment-prs-20261024/rule-choice-capacity/。
- 合main仍需实际新L2规则CI；L3归夜间固定main，不在本机重复启动矩阵。#107普通资源候选与#114手动分色独立推进，整个费用/合法性Goal保持OPEN。
