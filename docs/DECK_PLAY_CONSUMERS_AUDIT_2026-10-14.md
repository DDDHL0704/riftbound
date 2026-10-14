# 增援、虚空猛冲及虚空遁地兽的共享牌堆出牌审计

2026-10-14，Goal active，平台 **NOT READY**。本批把三个单人牌堆消费者接入[已有待确认通路](PRIVATE_DECK_PLAY_AUDIT_2026-10-14.md)，继续保留全量目标和未审消费者。

[现取中文记录](evidence/deck-play-consumers-2026-10-14/source-current.json)分别记录 OGN·062/298 增援（ID31274）、SFD·188/221 虚空猛冲（ID33286）、SFD·187/221 与 SFD·243/221 虚空遁地兽（ID33285/33354）。增援选择可选单位并减5基础法力；虚空猛冲可选任意展示牌、减2基础法力，抽取没有放逐的展示牌；虚空遁地兽先在征服触发确认时支付横置，响应后才展示并正常支付所选牌费用。先放逐再打出的现行更正分别核对。[独立英文图库](evidence/deck-play-consumers-2026-10-14/en-current.json)保留每个印次，不因名称相同合并证据。

基础 SFD187 的中文接口勘误为空，英文图库也仍是旧文字；SFD243 的中英文均已更新。[当前官方 Spiritforged FAQ](evidence/deck-play-consumers-2026-10-14/faq-current.json)明确对 Void Burrower 效果更正，结合两印次同一已识别传奇身份及 SFD243 中文勘误，采用相同生效语义。[来源处置](evidence/deck-play-consumers-2026-10-14/source-disposition.json)说明此对应关系；不声称 SFD187 原接口已经包含勘误。虚空猛冲英文图库仍旧，中文勘误及FAQ要求按未放逐的牌抽取。未知接口发布日期/生效日期保持未知；FAQ的立即生效声明单独记录。[四处运行正文更新](evidence/deck-play-consumers-2026-10-14/effective-catalog-text-delta.json)只改有效正文，原始上游快照及所有其他字段保留。

[中文核心规则重新下载](evidence/deck-play-consumers-2026-10-14/current-core.json)哈希与适用7月版本一致，10月16日版本仍未提前使用。[目视证据](evidence/deck-play-consumers-2026-10-14/pdf-visual.json)复用相同字节的39、40、75页和已核对的329/337/427限定条款，不扩大为全文认证。雷克塞 SFD170/170a 的攻击更正也已取得，但攻击触发、“此处”和复制文字消费者属于后续核对范围。

旧增援及虚空遁地兽直接提供主牌堆出牌许可，虚空猛冲只抽两牌。现在共享 `DeckPlayKind` 捕获当前顶部牌与世代，分别私下查看单位或公开展示任意牌，先选择/放逐并完成回收或抽牌，再结束来源并形成真实待确认结算链项目。可选性在放逐选择前体现；选中后必须按正常出牌步骤确认，无法满足条件才回到所属者放逐区。全部回响选择及余牌指令先执行，待全部来源指示结束后按创建次序确认所选牌。费用减少仍保留印刷符能和强制额外费用，普通法术正常进入响应链，装备和单位走原即时确认流程。

[现取额外费用来源](evidence/deck-play-consumers-2026-10-14/additional-cost-sources.json)支持冷血贵族、军团军需官和追猎雪狼的必付费用。共享费用候选直接复用服务端实际合法性谓词，提供摧毁普通/强力/指定属性友方单位、返回友方装备的原生选择；没有可付候选时不提供虚假的出牌入口。选择仍经原费用预览及服务器确认，费用对象没有变成法术目标。本批追猎雪狼只验证基地普通打出和指定属性费用，伏击及额外战场许可继续单独审计。

新增真实反例分别揭示缺少放逐阶段、虚空猛冲只有抽牌、三个费用被原生组装标为不可用，以及回响许可拦截。另两条独立恢复反例证明伪造父卡号/对应费用策略、移除所选牌记录后仍保留队列原来可通过；现已校验策略的精确卡号和已选牌与本次队列的一致关系。旧直接MAIN_DECK许可及出牌后回收分支按[调用证据](evidence/deck-play-consumers-2026-10-14/obsolete-path-removal.json)移除，旧格式恢复明确拒绝；仍有实际调用的其他牌堆落地helper保留，等待独立审计。

原 `OfficialReinforcementsTests`、`OfficialLegendDeckPlayTests` 全部方法和Theory行保留，追加真实选牌命令，原费用、抽牌、回收、休眠、响应、世代、来源变更及恢复期待继续检查。查看时所选身份仍私人，放逐后进入结算链才公开。[原虚空猛冲fixture](evidence/deck-play-consumers-2026-10-14/original-fixture-parity.json)保留种子、原三命令、原最终状态与提示，补足实体牌和第四条明确放弃命令，仍断言抽到原两牌及原支付数值；事件增加展示和选择暂停，按先处理余牌再完成来源的新次序核对，未改写已接受日志。

[首轮完整check](evidence/deck-play-consumers-2026-10-14/first-check-failure.json)保留11条旧消费者失败，涉及六条传奇原生行、反制后的增援续接、两个传奇夹具、两个传奇守卫和原增援放弃fixture。逐一补上实际选择阶段，原方法/行/费用/结果均保留，18项原消费者/原生回读通过。[原增援fixture](evidence/deck-play-consumers-2026-10-14/reinforce-original-fixture-parity.json)保留四条意图、tick4、原种子及精确回收顺序，只更新第四条选择窗口和增加的暂停事件；另外两种旧原生组装器按实际新阶段重新生成。六条传奇原生行仍从记录的确认检查点开始，前面的完整征服/选择链由新三个实际原生分支覆盖；不将检查点以前的服务端命令计入其原生命令数。

上一固定9fc055d7已经终态核对：[完整CI](evidence/deck-play-consumers-2026-10-14/fixed-9fc055d7-ci.json)11,395用例/7,097方法、3,215.74秒，旧11,364/7,085及110配对/seed424242/最终哈希全部保留，网络与双平台任务成功。[实际SQL](evidence/deck-play-consumers-2026-10-14/fixed-9fc055d7-sql.json)共64命令、167事件、65冷恢复检查点及5处篡改拒绝。它只认证该固定实现，不覆盖本批之后工作树。

[相邻focus](evidence/deck-play-consumers-2026-10-14/adjacent-focus.json)2,427用例/2,296方法、63.49秒通过。[原生回读](evidence/deck-play-consumers-2026-10-14/native-replay.json)31分支191条完整生产命令，加六条旧传奇检查点确认和四条续接意图；旧生产器18项/11方法、8.35秒回读通过。[修正后check](evidence/deck-play-consumers-2026-10-14/check-parity.json)11,274用例/6,966方法、220.40秒，原11,246/6,956全部保留、新增28/10，零失败/跳过/重复，全部原生输出在此轮再次回读；真实PostgreSQL五项及Python27通过，专用数据库已停止。[2,261冻结输入](evidence/deck-play-consumers-2026-10-14/frozen-inputs.json)无漂移，运行1,031/830不变，限定审查80条。

[完整计划](evidence/deck-play-consumers-2026-10-14/full-plan-parity.json)与本轮check使用同一程序集，保留原7,097方法及143完整对局方法，新增10方法，共7,107方法、预计11,423用例、110配对及seed424242。计划不替代固定提交实际执行；完整CI、SQL/网络/双平台结果须推送后分别核验。可见双客户端、用户Windows实际运行、雷克塞攻击消费者、嵌套再次打出及其他未审来源保持开放。
