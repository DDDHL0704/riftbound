# N15 · CN VEN103 往日阴影

当前固定测试源 `c3b3b91f21f27badd7aeae3371310490150457e9` 已完整吸收main `d713adad568999baa8703750b7893ec234c63d4c`、PR139作用范围修复 `03dc623de4277da9271b443814b1f8dd66e66ec8` 及原PR151消费者前置；ready仅更新本文。Registry构造参数尾部按①分配顺序保留main的减费bool在前、自有弃牌伤害int在后，默认值分别false/0；未改③目标校验方法。新主线的入口、可选减费、主动符文资源规则均保留，目录及VEN103原用例与此前ready逐字相同。

原移植基于 main `907f9443`，显式包含完整 PR121 `7c3fd27f`（原四例坏目标 raw 的入口守卫与真实原生载荷）、完整 PR139 `d31178bf`（原 71 张预组补位集合）及完整 PR151 `2457f835`（原 VEN008 真实击杀与 VEN093/122 生命周期前置）。最初从干净 main `8ac0afba` 移植的自有增量仍为九文件；该轮精确测试源 `2fbd913bdbf1acc7b5648a9eda433fc1df86a0b4`。没有旧累计 WIP 祖先、入口重复实现或缩小原测试范围。

## 当前支付主线组合

编译0警告/错误、单线程342/342通过、零跳过。原178例、main127减费/时机60例、main124入口64例、main131主动资源26例及新增消费者组合14例全部保留且无交集；原L2的12个精确失败名称均通过。此前ceda固定源 `fcc906565e3c05f4e8d38dd211d38f9a9d550c96` 的314例亦逐项包含于本轮；两输入各自保留源/bin/TRX，不相加或改标。

新增组合仅在原VEN008类添加三个方法，不改旧方法、Theory行、Driver或断言：两个伊泽瑞尔印次×实际弃/不弃的报价/付款/3或5伤害正例，以及伪造法力/通用符能减免的8项拒绝；另2例在付款时主动回收无关蓝色符文，仍付3法力、实际弃牌事实不变，剩余蓝符能保留。12个伊泽瑞尔组合日志合计20命令（12接受/8拒绝）及34事件，2个蓝符能日志为6接受命令/21事件，均经原Driver与Export执行JSON恢复和原动作事件回放，接受动作另核对重复幂等。初态为已在场伊泽瑞尔/既有中盘局面，未称伊泽瑞尔真实入场或规范完整对局。

2500编译输入及1418运行文件前后哈希稳定；四程序集InfoVersion指向本受测源。RIF `d32184e9381cf14675994b9a9ecaed72286f811ef9661f8de2c090bd3d9c9acd`，Engine MVID `46f476d7-9f4e-4f00-a393-5f3618cc7e64`。同一运行件重新导出VEN008的46个文件（含重叠前沿）及VEN103的46个文件（含4个坏raw前缀），不将重叠日志求和计作唯一命令/完整对局。目录1045/844及VEN103原源不变；PermanentPlayTargets完整文件与上述main相同，六个原未跟踪文件不变。

新原件位于ignored `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr159-maind713-v1/`，含固定源/bin、实际TRX、全部新日志、清单、module-identities.json和已通过的verify-final-v1.py；`source-runtime-proof-v1.json` SHA256 `13c0d097b378d406e9b30f04ec2f9e5f550f9e8a894d2479b0ab2c0b91f651bc`。前一输入独立收据在相邻pr159-mainceda-v1，SHA256 `0ba19aa4087c251ff5483a5b263a090c02fef99d5dd39389b8b099e21378b1f9`。最新实际head/base/merge-result的L2仍须单独全部通过；未新启本机full/SQL/网络/矩阵。整卡认证增量0、backend NOT READY，适用分母UNKNOWN。

精确来源 CN37800 / VEN·103/166 SC，3 法力、1 紫色符能、普通法术，最多两名任意废牌堆的单位分别返回其拥有者手牌。官方 API `https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb` 于 `2026-10-24T07:42:55Z` 刷新；当前规范记录 SHA256 `a497ccb0fc73058fe1693f47f9aaceaf0dd272d329d2528144bed236183c6d39`，内容和原 `55d86187` 完全相同，完整卡面已核读，image SHA256 `5310e501210fee6172c973e0dcb5cde4fa62176378ab897144209082552779f0`。该 API 的 QA 与勘误字段为空，发布/生效日及跨地区对应关系 UNKNOWN。

原法盾在废牌堆不生效的反例另需精确 CN37733 / VEN·043/166 SC 钢爪。仅补原来源与基础单位注册，卡面/数据均与原来源相同；这不构成钢爪整卡认证。目录保留父包 1043 条记录及顺序、原来源增量和 Ahri 的全部 1009 条历史字段检查，仅新增 043/103 两条，得到 1045 条/844 个功能元数据单元；重复组 115/316/201 不变。

生产改动保留原 `55d86187` 的限定接点：`AnyGraveyardCard`、Core/提示对称的已知 seated owner 校验、公开 distinct 候选、原返手支路按卡牌拥有者选目的手牌，以及 Standby 非本地范围白名单。旧友方复活范围、对象代际绑定、权限、费用和恢复守卫保持。修改前逐项读取②③实际接点，均与本基底相同；未改③ `ValidPermanentPlayTargetStack`。

原 49 例、全部方法/Theory 数据及语义断言保留。仅将旧未移植测试类的 fixture/driver 放入本类；初始空战场控制者和 At 占用与 main 的有效状态一致。Driver 仍使用真实 MatchSession、原日志、每命令回放、隐藏信息与恢复校验、拒绝前后状态/视图/提示不变，以及接受命令的幂等缓存。现有日志仅增加实际 RuleInputs 与三程序集路径/SHA/MVID。

固定 `7a48cbbe` 的原相邻范围实际 343/343、143 方法、零跳过，同一冻结运行目录的原 11 套预组/两格式输入 1/1。吸收 main907 后，生产源码、卡牌数据和本卡/父包用例逐字未变，仅增加 main 的 Wild Claw 历史夹具及其消费测试两个输入；新 `2fbd913b` 独立 115/115、零跳过 = 本卡49 + 父包31 + RuleInputs21 + 历史夹具4 + 目录9 + Ahri历史1，同一新运行目录原预组输入另 1/1。两个固定输入分别保留，不相加或重标为同一次结果。

新输入的本卡 42 份最终日志含150条命令（132接受/18合法形状拒绝）与328个事件，另4份坏 raw 前缀均零命令/零事件；全部42终态完成 JSON 冷恢复，原日志回放与两种协调伪造拒绝保留。2490源码输入、1418运行文件在执行前后全量哈希一致，46份本卡原件的实际 RuleInputs 与三程序集路径/SHA/MVID吻合。当前 RIF `4aff410402126e3d605e28145b7fda56ae8b06c64f66d0f03065dca80b22a0a0`；构建0警告0错误。

独立私有反例 `9b7d2ebcdba2a4084d4a22ac1bc352a29f3c1e4d` 仅将返手收牌人支路还原父实现，保留新范围/提示/来源及全部公共断言，仅在私有 Driver 加断言前实录；实际7例为自身/零目标3通过、含对手目标4个预期失败，21份逐命令原件保留。反例不是公开分支祖先。最初 `80fc2a2a` 的定向343虽过，原预组输入真实失败于 CoreRules/lillia-lowcurve；新增1费钢爪进入动态补位是触发点，故引入 PR139 的原71集合，未改任何预组期望或放弃法盾墓地反例。该失败及各版来源、DLL、TRX均保留。

原件位于 ignored `var/qa/cards-direct-pr-migration-20261024/n15-main-port-v1/`；当前 `main-source-test-receipt-v2.json` SHA256 `ee7d3f497ecedf800364146f773645bdd678182e291efada10ae9d8bc4c22cb7`，旧343另见v1收据；来源在相邻 `n15-current-source-v1/`。原六份未跟踪消费者哈希保持且未编译/执行。本机未跑 full/check/SQL/网络/110场矩阵。

具体后续契约：PR157 当前尚未合入 main；重基到其 required-nullable `RecoveredCommand.ErrorCode` 时，本类新 Driver.Commands 投影须传递实际 `entry.ErrorCode`，连同继承的自有投影一起核对；不推断/回填旧历史，不将当前日志冒充新契约日志。

日常合入仍须最新 PR 合并输入的 `L2 required`（quality/targeted/rule_suite）全部真实通过并重新机械核对 head/base/merge SHA 后执行；该 CI 当前待确认。本机不新跑 full/check/SQL/网络或 110 场矩阵。来源/注册/定向均不代替整卡认证；净整卡认证增量 0，backend NOT READY。1740 印次/14293 条款保留，当前适用功能分母 UNKNOWN。

L2 实际失败后续：run40077028269 真正受测合并源为 `64b413a36045f33e0095543bcf77d0544f53bd9e`，并非后来页面重算的合并SHA。原规则套件12个失败均保留：6个目录/历史成员/内容身份断言未随新增印次更新，5个自定义开局配方受到PR139白名单误限，1个seed31弃牌节点因新注册VEN043替换3张UNL-053而改变输入。没有删除失败方法、Theory行、弃牌/胜利/回放断言。

新固定测试源 `352210cd66515254f433f80925800ccf3284b813` 吸收PR139修复 `5f5e52b8d34d0c1b2e0756b134f151c396be50b6`：白名单只用于原11命名配方。测试辅助构造器从既有1036精确成员夹具加原4张后续牌固定原1040输入宇宙；Lightning Rush也复用精确1036成员，保留1036/1037及逐组序列化原断言。原待命对局全部字段与CoreRules/vex-response原夹具逐项一致；原seed31弃牌、得分胜利及原日志回放实际通过。当前P6全局统计按1045精确目录校准，不按测试数认证语义。

新源编译0警告0错误；143例定向加原115例计划中其余35例，共178例无交集全过、零跳过，原115用例名多重集完整保留，原L2失败12例全在本次通过集合。2491源码输入、1418运行文件在两轮后哈希稳定；Engine MVID `aec62ffb-3802-4c40-824a-0a588eaf77c1`，三受查程序集InformationalVersion均指向新受测源。相对旧ready47209c78，生产/数据只有PreconstructedDeckCatalog作用范围修复，注册及目录字节未变；旧42份日志仍只归原受测源。

新原件位于 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/`，含4份原CI ZIP及job日志；`pr159-current-v1/source-runtime-proof-v1.json` SHA256 `8a12e41071683a27431be966bfbd0013141bf56a23afdf1fb82ff02b10e1a617`，同目录为新固定源/bin、2份实际TRX和module-identities.json。ready后续仅更新本审计说明，最新L2仍待真实通过。若后续组合PR173，还须原样传递实际entry.TypedCommand；它的null省略语义与PR157必需保留的nullable ErrorCode不同，不合成raw或回填旧日志。
