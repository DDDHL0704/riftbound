# 当前项目状态 · 2026-10-24

**后端未完成，产品 NOT READY，净整卡认证 0。** 原1740印次/14293条款及来源缺口保留；适用功能卡分母未知。①事件驱动、无续跑Goal；②③④继续独立迭代。目标见[后端目标](BACKEND_RULES_GOAL_2026-10-23.md)，执行边界见[工作流](RULE_TEST_WORKFLOW.md)，来源见[当前manifest](audit/current-manifest.json)。客户端阶段保持原OPEN身份。

当前唯一验收候选是A4：`0ec2e692d1f82ff0487e40a79d5e97c0c704eb59` / 不可变tag `backend-related-20261024-a4`，已实际启动[Actions 40031882948](https://github.com/DDDHL0704/riftbound/actions/runs/40031882948)。scope原产物12534897634已核：actual/event/expected SHA相同，tree `222f7a84…`、18553条source index `cd8df164…`与本地封板一致；这是执行身份通过，不是门禁通过。A3原失败及其余有价值作业保留。下一功能批只取[有限B1成员与依赖闭包](parallel/NEXT_FUNCTIONAL_BATCH_B1_2026-10-24.json)：C8/S7/N10/N11/N12/C9/N13；新增C10、容量提示及N14（即使已完整交付）均留更后批，不扩A4或B1。①先完成固定候选调度，后续按有限边界接入，不用持续接口审查替代真实候选验证。

新通用入口阻塞由③修复：N15另发现旧OGN264亦会把原raw `targetObjectIds=["","  "]`经Mapper过滤成合法typed `[]`后接受付款/入栈，而严格恢复拒绝原日志。①用A4等价生产/目录源码独立Mac最小复现：合法`[]`结构检查无误；坏目标正常请求accepted、扣2法力/3事件/1日志；坏目标的stale请求虽PROMPT_EXPIRED/0事件，仍写1条坏raw并同样出现两条恢复结构错误。故须在MatchSession既有prejournal入口检查原raw required-array，Core typed拒绝或放宽恢复均不能修复。显式中盘正控不证明完整出生回放，Mac/MVID不是A4 Linux；原件另存、A4固定输入/原作业不改，独立窄修优先于继续功能接入，B1成员不扩，发布继续禁止。

另有直接typed调用的`raw=null`持久化缺口保持OPEN：③原出生前缀8命令后第9条合法PLAY_CARD实际accepted，原9条日志仅报缺少PLAY_CARD raw；①核原件/源码，未独立重跑。原控制权三Theory使用NoopMatchJournal，须保留其控制权语义。本批仅修raw JSON object内目标数组，不因测试兼容称null持久化安全，也不合成raw、改恢复或按journal类型旁路；后续另审真实调用方原wire与持久化入口契约。当前src可见GameHub与replay两调用均携带原raw，尚未证实外部typed/null路径；本批修复不能表述为所有PLAY_CARD raw入口已封闭。

| 位置 | 固定输入 / 批次 | 已确认状态 / 主责 |
|---|---|---|
| 当前验收：A3两项失败窄修A4 | 不可变ref `backend-related-20261024-a4`；[封板成员](audit/backend-related-20261024-a4.json) | 功能边界仍93a3b117，Engine/目录与A3逐字相同，1052/3bf8afc4；只纳入scope预算0322857d及S3自洽篡改验收04042a90和必要守卫/元数据。76 SQL/四分区、八组network、full与desktop门禁不减；后续C8/S7/N10–N14/C9/C10/容量提示全部排除。以tag解析完整SHA核对实际checkout，当前尚未有远端通过结果，不复用不同输入的旧绿；A3剩余有价值作业保留 |
| 原固定候选，整体未过 | `cbcf5c24` / 不可变tag `backend-related-20261024-a2` | [Actions 39998674123](https://github.com/DDDHL0704/riftbound/actions/runs/39998674123)的scope实际SHA/tree/index一致；SQL分区2原FAIL已留原件并另作后继修复。scope/旧native_network/双平台desktop及SQL分区3最后读取为成功，full/SQL0/1最后读取仍运行；未据旧快照推断终态。原related_network作业虽绿且有两次实际API PID，**六房重启后均0 Joined、各两条INVALID_RECONNECT_TOKEN，不能证明成功重连**。原ZIP/绿receipt保留，[纠正合同](CI_BACKEND_ACCEPTANCE_CONTRACT.md) |
| 原固定A3已有失败，其余门禁继续 | 不可变tag `backend-related-20261024-a3`；[成员与边界](audit/backend-related-20261024-a3.json)，功能边界`93a3b117`；SQL终局窄修复及N4/S4/N5/S5/N6/S6/N8/Lux/N9/C7 | 根目录1052/语义SHA `3bf8afc4…`；组合原运行**885/888、3 FAIL、0skip**，18533跟踪输入不变。三红均为新增卡的目录统计基线，逐卡核对后仅这三项另跑**3/3**；原885/888不改记888绿。C7机制54例、五消费者66例、N9的51例及所选S6/邻接例均在原运行通过。API/Postgres/公共driver构建过，公共CI工具58/58、重连永久纯守卫根47/47；现接76 SQL/八组重启，非真实门禁通过 |
| A3实际结果回收 | [Actions 40022865383](https://github.com/DDDHL0704/riftbound/actions/runs/40022865383)，固定`9dbf36b3` | scope/related实际checkout SHA、tree、18550源index与封板一致；远端124检查过。scope被GitHub明确5分钟超时，停在历史基线fetch，后续治理/diff步骤未跑；①仅给后继scope增至15分钟，原run不改。related首S3 play/recover真实2人Joined、token轮换、API PID4194→4409、同Engine SHA/MVID及独立七表验证过，**tamper FAIL，后七组未执行**；③同源单房PG确认旧攻击被资源视图一致性提前拒绝，未到原日志审计。后继三文件窄修`73d65208`已接为`04042a90`：协调五投影payload后仍严格要求原日志末态hash拒绝，finally七表exact还原；根新负控/旧恢复守卫12/12、SQL及公共driver构建过。③单房PG原件另核通过，非原Linux或完整网络门禁通过；Engine/目录及九FullGame源码不变。旧native/双desktop过；规则与四SQL最后读取仍运行，整体未过 |
| 下一队列与共享接点 | C8 `dd6d43ac`、S7 `65630619`、N10 `c9006d00`、N11 `3fa64e7b`、N12 `0dd00f81`完整源包独立排队；C9 `7386fa60`及叶返修`606bb8d`已审、待公共接入；S7父付款补测`bab33e63`依赖原S7 | [已审接点](parallel/SHARED_BACKEND_CONTRACTS_2026-10-24.md)。全部排除A3，来源151/4/内存167、1626-WIP/固定163、20+112、单次182及N12单次333均保持各自输入，不合称根通过。N12六文件/原333项与123方法、31完整日志205命令651事件13拒绝及3前缀已核；仅获批nullable/固定金额四行，普通Empower待决Howl真实拒绝、结算后得4。原2/3、15/16、30/32红保留。N10/N11/N12原补丁遇根最后五目录行顺序差异，已制保留根1052旧记录/顺序/provenance前缀的临时组合补丁，仅index检查、未改根源码。C9原80/80与另2/2、原及返修后各自内存8流程124命令分列；606仅两叶/短文，三处审阅缺口已修，①复制冻结程序集到独立目录复跑37/37纯检查、13诊断原件成立。连接Dispose后最终回复审计/trace、七表RR只读摘要及篡改每mode先存完整实际诊断再严格拒绝均已核；公共dispatcher/Program/source links及17新永久guard仍须根接入，非真实PG/API通过。S7补测仅两新文件，固定8/8另于WIP121/121，不代表出生/SQL/重启证明。自然装备变单位及VEN011/027/073适用来源缺口继续OPEN |
| 新资源/阶段共享接点 | ③容量提示Issue107与②C10战斗阶段均获批最小范围，未完整交付 | 已核MAX-1资源池经SETT先付1再获2合法到MAX但候选被隐藏的反例；只批DestructionOptions末尾可选applied set及ALTAR一表达式给③只读前瞻，旧默认frame/结算不变。C10原3/7、4机制红及实际Conquest88→MATCH_WON90→DAMAGE_REMOVED91→CLOSE92已核；只批移除已判winner后无earned skill的早退，复用原cleanup/result/control及终局切点，扩大既有事件生产语义须L2/full/真实门禁，不能按一行当低影响 |
| N13完整包及根组合边界 | ④`8f13d452`依赖N12，七文件/四接点已核，来源单次659/659、163方法；根未接入/执行 | 图25966367/record9027dd01及旧1056记录/prefix不变已核；29最终日志77命令195事件16拒绝+3前缀，原2/3红保留。Swift/3mana/战场单位3伤，实际原子付弃一手牌额外费则固定5；安全本人手牌提示，不改伤害结算/DTO/旧Echo减费/N12/C9。原补丁在根S5代码上下文不能直接应用：根已把可变符能指令改为结算期选择/支付，①临时port只在根固定伤害fallback加discard3/5，不恢复来源旧confirmation-time OptionalPowerCost或枚举，并保留根目录顺序。组合S5+N13定向及full/真实门禁仍待验；来源659不冒充根通过 |

| 历史固定输入 | 结果与边界 |
|---|---|
| `2773a353`与`6dc5b635` | 分别是1047输入777/777与1050输入221/221，非当前1052输入结果；都不是full/真实SQL/API门禁通过 |
| [Actions 39997314116](https://github.com/DDDHL0704/riftbound/actions/runs/39997314116) / `1baf5e51` | 原related_network在业务前因CI导入缓存触发严格未跟踪检查而FAIL，①最小复现后独立修复；原日志/产物保留，其他在途任务不取消、不改输入 |
| `87fc55c6`本机固定check | **14316/14324，8 FAIL**，18442源输入/模式及运行时稳定；原TRX与终态保留，不重跑。[后继窄修复](parallel/RELATED_BACKEND_CHECK_FAILURES_2026-10-24.md)仍须后继候选实际门禁；原[Actions 39973503211](https://github.com/DDDHL0704/riftbound/actions/runs/39973503211)最后读取仍运行，未核终态 |

**发布与执行：** `main`已核对仍`8ac0afba`；候选实际门禁未闭合，不发布运行源码。新增重型check/full、完整矩阵、全套SQL/SignalR进程重启/soak统一Actions；原本机固定任务及所有原件保留。无CI结果自动唤醒，不新增云端聊天或轮询自动化。

**原件入口：** ①ignored `var/qa/related-backend-integration-20261023-v2/`保留87原失败；`var/qa/related-backend-integration-20261024-v3/`保留组合、重连原件核查、工具/build及调度记录；交付审查见`var/qa/next-batch-queue-20261023-v1/`。历史见本页Git版本。旧N54/N55固定源/bin/数据库及未跟踪`.claude`等证据继续保留，未核对终态不升级为完成。
