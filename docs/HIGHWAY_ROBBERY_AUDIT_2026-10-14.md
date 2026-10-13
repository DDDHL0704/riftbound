# 巧取豪夺的目标控制者结算选择审计

2026-10-14，Goal active，平台 **NOT READY**。当前[CN OGN·033/298、ID31241](evidence/highway-robbery-2026-10-14/source-current.json)与独立[EN ogn-033-298、Shakedown](evidence/highway-robbery-2026-10-14/en-current.json)一致：选择敌方单位，由其控制者决定是否让施法者抽两张牌，否则造成6点伤害。基础符能2、红色符力1、反应，非预览。选择发生在结算时的伤害指示之前。取得时间、原始响应哈希、地区语言记录独立保存，未把取得时间当发布/生效日期。

[当前CN核心规则](evidence/highway-robbery-2026-10-14/current-core.json)重新取得且hash与适用7月版本一致，未来10月16日版本未提前应用。目视核对37、50页的321及359.3.e：结算中不清理，不合法目标的相应指示跳过。[限定条款](evidence/highway-robbery-2026-10-14/core-scoped-clauses.json)仅认证所使用范围。[当前Origins/Spiritforged FAQ页面文本](evidence/highway-robbery-2026-10-14/faq-current.json)未找到Shakedown，不据此认证内嵌图片或整个FAQ。

原实现让施法者在声明时选择两个模式，默认直接造成6伤害，另一模式直接抽两牌，均未等待目标控制者实际决定。两个真实命令反例确认结算后没有决策窗口。修复以共享DamageAlternativeDrawCount及ResolutionDamageChoiceContext描述伤害/施法者抓牌替代指示，在目标仍合法时暂停同一结算链项目，由当前目标控制者提交原有CHOOSE_CARDS命令；放弃替代选项执行伤害，接受则执行抓牌并跳过整条伤害指示。两种结果继续使用通用伤害、抓牌、清理、触发和完成流程。旧施法者选抓牌模式被移除，不给开发期非法流程增加兼容层。

回响每次执行有独立身份与决定；致死的首次伤害不在同一项目剩余指示前清理。目标返回手牌时不请求选择，反制在正常响应窗口移除项目。伤害抵挡不抹掉选择，法盾费用仅在声明时支付一次。捕获来源/目标物体世代、卡号、执行序号及目标控制者，恢复验证拒绝伪造、提前决策、缺失上下文和孤立窗口；玩家和观战视图须与权威上下文逐项一致。合成选项身份仅在该合法上下文中使用，来源与实际单位引用仍接受原有验证。

原测试方法、Theory行、种子、目标、费用、最终伤害6和施法者抓牌2期待保留。两个旧fixture补齐实体法术信息，在双方让过之后增加目标控制者的真实选择，tick由3变4；原抓牌fixture不再声明伪模式。原有事件断言保留并置于新的暂停/决策事件之后。[失败历史](evidence/highway-robbery-2026-10-14/failure-history.json)保存最初反例、恢复选项身份缺口、无效目标占位字符串及fixture事件顺序失败。

[相邻focus](evidence/highway-robbery-2026-10-14/adjacent-focus.json)1,862用例/1,827方法10.23秒通过，新增33用例/10方法。[原生回读](evidence/highway-robbery-2026-10-14/native-replay.json)10分支46条Godot生产命令、38用例/15方法通过，每条命令冷恢复、去重及完整journal重放通过。[check](evidence/highway-robbery-2026-10-14/check-parity.json)实际11,174用例/6,927方法184.00秒通过，原11,141/6,917全部保留，2,255冻结输入无漂移，真实PostgreSQL五项和Python27项通过，专用集群已停止。运行目录1,031/830不变，限定审查77条；新固定提交完整CI仍待终态，不据局部通过提升整卡、全量来源或可见双客户端/用户Windows验收状态。

[完整计划](evidence/highway-robbery-2026-10-14/full-plan-parity.json)保留原7,058方法及143完整对局，新增10方法，共7,068方法/预计11,323用例；110配对及seed424242仍须固定提交实际执行核对。上一慈悲度魂落固定dbe86315完整CI在本次观察时仍运行，不宣称完整通过。

固定2640582d [CI38477195105](https://github.com/DDDHL0704/riftbound/actions/runs/38477195105)后续已独立核验：11,323用例/7,068方法、1,598.93秒，旧11,290/7,058全部保留，110配对与最终哈希不变，实际SQL、网络及macOS/Windows构建成功。[完整收据](evidence/turn-unit-play-grants-2026-10-14/fixed-2640582d-ci.json)和[SQL收据](evidence/turn-unit-play-grants-2026-10-14/fixed-2640582d-sql.json)不覆盖之后候选源码。Goal active，NOT READY。
