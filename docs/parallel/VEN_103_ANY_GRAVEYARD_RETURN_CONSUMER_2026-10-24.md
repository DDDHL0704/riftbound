# N15 · CN VEN103 往日阴影

本包基于 main `907f9443`，显式包含完整 PR121 `7c3fd27f`（原四例坏目标 raw 的入口守卫与真实原生载荷）、完整 PR139 `d31178bf`（原 71 张预组补位集合）及完整 PR151 `2457f835`（原 VEN008 真实击杀与 VEN093/122 生命周期前置）。最初从干净 main `8ac0afba` 移植的自有增量仍为九文件；当前精确测试源 `2fbd913bdbf1acc7b5648a9eda433fc1df86a0b4`。没有旧累计 WIP 祖先、入口重复实现或缩小原测试范围。

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
