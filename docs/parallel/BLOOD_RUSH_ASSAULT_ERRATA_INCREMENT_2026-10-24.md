# N6 血性冲刺临时强攻与现行勘误

- 基线 `f11a7871375f1c3eb550bf8b04ba9e87e99b6f69`；精确 SFD·003/221 / CN33075，1法力、无彩色费用，迅捷/回响1/本回合强攻2；不新增印次。
- 官方卡图确实漏写“本回合内”；2026-10-23T17:35Z [官方接口](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)的errata字段与DOC12 p3勘误一致。记录SHA `316202ebccc4952ccef94de929c60e32a4c45ed41d5dca644c101adbd25b07ff`；原响应 `e996bc75f51b6ae27a1c87fb58311fe1b84b8e422a69b43920b6594635fcb4f5`；卡图 `02ca49d7fc27628e6655fe88c4879f30428370a32a7b6ca0c5f3b24773d7070a`。
- 17:37Z复查当前规则API及PDF字节，DOC12 `13ef6ec857b2b7d693d0e83f388c37f02a87bfb08886676e70e6bd69f8e7935d`、DOC13 `d0b1e139a35e5bced809bf5656789b019692e07880d0265e99f9bd11d8179f01`、DOC14 `16eeeea28baeeee2dae3765876c609f79cc77fdbdd6928d73f1b8fa5d5c277bc`均与已采用快照一致；版本/更新日260114，未给出的生效日不推断。
- 仅复用现有 ReviewedCardErrata，按id+cardNo补此一条有效文本；保留原始获取目录。注册改为`COMBAT_KEYWORD:强攻2`并删即时条件+2；原EffectKind/支付/回响/权限/恢复不改。真实日志证实迅捷原本可用，未额外修改其权限。
- 原红v1 9/9 FAIL：进攻前一张/回响/两张/两张均回响分别预期5/7/7/11、实际均3；对决中旧存储战力变4/6/10；有效来源文本也缺本回合限定。原件保留。
- 新6方法21例：真实入场印刷强攻、进攻前/中×普通/回响×1/2张、单结算项/独立实例/真实费用、双方非进攻目标与到期、一次反制取消两次效果、前后回手换代、重复/伪造Echo及不足/时机/来源拒绝。
- v3关联165/165 PASS；另v4回响模块25/25 PASS，同测试程序集哈希，均`--jobs 1`。15个新原日志135命令/414事件，冷前缀/幂等/隐私与完整回放；8红路径除新RuleInputs外初态/语义命令序列相同。
- 仅校准一个旧方法和一份旧fixture的错误即时战力预期；原身份/initialState/commands不变，保留单结算项、双execution、费用及全部原方法/Theory，v2实际9过1错也留原件。
- 原目录文件仍 `11fd91d2a25216ae062f523ec871f5f9e43114ac23d6ea4f7482fa25b5dccab5`；有效目录哈希由7838544e…变`298ff9a28d15f826e6b67293f76a7096a6939b6ff9a4b53ea0eaf48b4360ad52`，Authority仍70ccda58…；输入`079ea1807397a65cabfdd85b5d8bb8fbc2892bcd6f28827fa243d6e7b88eba23`只归本线1046组合，不能代替①候选身份，不迁移旧历史。
- 原件在本线ignored `var/qa/blood-rush-assault-20261024/`：source-v1当前响应/卡图/三PDF、原红v1、v2/v3/v4日志/TRX、新原journal、legacy-calibration-originals及final-source-test-receipt.json。
- L2来源/消费者增量，未知/注册路径CI仍保守full；待①下一批Actions完整check/full及所需SQL/API/SignalR重启。本机未新跑重型，净整卡认证0，后端/产品未完成。
