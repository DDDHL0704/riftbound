# 两个结算付款菜单的符文容量移植
- 独立基底PR136/76049630，只取ff3008956的两个手工TAP/RECYCLE菜单守卫与原28例/审计，不依赖S7或混入S4篡改/销毁替代钩子。
- TargetSpellControllerPaysCounterCost及ChooseTokenHaste每轮读取实际working pool；普通+受限法力用CanGainSpellOnlyMana(pool,1)，符能以HasSupportedAmounts及long总和检查。int.MaxValue仅为实现表示边界，不是官方游戏资源上限。
- 保留其他合法来源、自愿获取、正确付款者、PAY/DECLINE、急速与入场/后继指令、Frame/clock、恢复/DTO/注册及原始历史；只防止菜单提供必定拒绝或生成超支持范围预览的选择。
- 依据固定CN核心PDF d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11的164.2、166-168、445.2.c、805与原四精确API原件，var/qa/rule-choice-rune-capacity-20261024/source/；原红/初编译错误和旧140相邻仅作分别限定的历史，不冒充本次移植结果。
- 新数值边界fixture与真实自然积累区分，冷恢复/本fixture完整原命令事件回放与普通出生/真实SQL/API重启区分。原相邻方法/Theory及FullGame保留，当前定向/父源对照/实际模块身份位于var/qa/payment-prs-20261024/rule-choice-capacity/。
- 合main仍需实际新L2规则CI；L3归夜间固定main，不在本机重复启动矩阵。#107普通资源候选与#114手动分色独立推进，整个费用/合法性Goal保持OPEN。

- 当前同步父 #136 `9bf8a106147f32de688c4de5278fc71547929b65`（含main `d713adad568999baa8703750b7893ec234c63d4c`），实测源 `ed3d39e666e15e617e804b91784993fce7453bda` 机制/相邻1274/1274、0skip。原82中74个当前本机适用身份、另24个原反制、父839及资源前置591身份全部保留并通过；原条件反制完整合法开局的8行Theory按既有精确L3清单单列，源码/旧证据保留，当前输入新L3仍OPEN，不计为本轮通过或TRX跳过。
- 生产仍严格两文件6+/4−，所有父测试/FullGame/fixture字节不变，新增28例源码与旧头逐字一致。24份导出中的8个满池选择均未提供、无异常、伪造选择拒绝且结果无事件；112次独立整数校验确认普通+受限法力以及符能各总和不超实现表示范围。五实际模块、235/577PDB、退出RuleInputs及运行catalog/scope核对源，identity `9de977c6450c62bc8bbb4c35a44c66ce008a67a491eda496d1d120f56af55dd3`。
- 证明 `var/qa/payment-prs-20261024/rule-choice-capacity/post-parent-source-runtime-proof-v9.json` SHA256 `b7b33d44e94bb77d01a1e3c39e55ef6c22fbdf777acfcadc287b238e172f3ef8`，含原TRX/逐例身份、既有L3分层、源码归属及独立数值导出检查；旧父635源/0PASS8FAIL只作旧输入，不重标当前父。发布头仅更新本审计，实际模块版本/MVID/RIF仍归实测源；64追加入口runtime/122Core原日志冷registry收据范围单列。父合main后仍需最新main基底实际L2，不扩大为真实SQL/网络/重启或整个后端完成。
