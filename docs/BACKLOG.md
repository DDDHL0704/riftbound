# 待办与状态

这是项目唯一的状态文档。按优先级从上往下做：领任务时在条目后写上分支名，合入后勾掉；发现新问题直接追加。流程见 [AGENTS.md](../AGENTS.md)。

**当前状态（2026-10-31）**
- **main**：最新为 `2dfa92873`（#329 文档记录）。合入 #329 后的完整运行 [41159049774](https://github.com/DDDHL0704/riftbound/actions/runs/41159049774) 九个规则分片和 `L2 required` 全部通过，墙钟约 35 分钟；主干完整验证恢复，但 P3-2 的 30 分钟目标仍未达到。最新定时夜验 [41136063296](https://github.com/DDDHL0704/riftbound/actions/runs/41136063296) 全绿，固定 `3959436b`；PostgreSQL 分区 0 用时约 218 分钟，见 P0-2 的 SQL 资源采样记录。
- **夜间验收**：完整验收 [40990497783](https://github.com/DDDHL0704/riftbound/actions/runs/40990497783)、[41010811413](https://github.com/DDDHL0704/riftbound/actions/runs/41010811413) 和 [41024183616](https://github.com/DDDHL0704/riftbound/actions/runs/41024183616) 已连续三次全绿，P0-2 达标；之后只依靠每晚定时运行，失败时按 P0-2 登记。
- **卡牌目录**：基准快照 1089 个印次；未接入的效果及待核对印次见 [卡牌清单](CARD_BACKLOG.md)。
- **重置前已暂停开发线的工作**：都已推到远端分支，接手说明见下面的"接手旧分支"；当前按 A/B/C/D 四线分工。

## P0 · 让 main 和夜间验收恢复全绿（A 线）

- [x] **P0-1 合入夜间验收的测试修正**。[PR #236](https://github.com/DDDHL0704/riftbound/pull/236) 已合入，基于 `codex/nightly-optional-reduction-selector-20261028`（`7aae34be`，已包含付款线的 `a79b0f32`）。只改测试：
  - 付款重连夹具：#223 改变了可选减费的候选结构，导致 `OptionalCostReductionAcceptance` 失败，这里同步更新；
  - 修正 6 局完整对局和对应的投影测试。

  SourceLone 对局已在伤害结算前无条件检查双方参与者，死亡后检查墓地和投影中的反向结果。L2 通过后已合入；[手动夜验 40783707647](https://github.com/DDDHL0704/riftbound/actions/runs/40783707647) 的付款重连、完整对局进程分片及其他集成验收均通过，规则全量因六小时超时而未完成，详见 P0-2。
- [x] **P0-2 夜间验收连续通过**。`40990497783`、`41010811413`、`41024183616` 已连续三次完整验收全绿，手动运行按当前口径计入，P0-2 达标。之后不再手动连跑，只依靠每晚定时运行；失败时在这里登记失败项、原因和修复 PR。
  - [#296](https://github.com/DDDHL0704/riftbound/pull/296) 已合入：SQL 验收独立进程复用完整规则测试已验证的 1,024 条静态正则缓存，并在现有分区结果中记录实际缓存容量、CPU 数和 GC 模式。保留全部 69 个 SQL 场景、原始命令、冷恢复、反伪造和完整回放检查；现有验收驱动任务同时编译完整 PostgreSQL Release 项目，任务数保持。45 项工具检查及 CI 验收驱动通过；完整 [41005415912](https://github.com/DDDHL0704/riftbound/actions/runs/41005415912) 的 9,491 个方法、19,625 条用例与门槛通过，Counter 与固定计划及全绿 main 一致，零跳过。九片实际约 19.2–24.1 分钟，末片排队约四分钟使总墙钟为 30 分 26 秒。真实 SQL 提速幅度仍待完整夜验测量。
  - [40990497783](https://github.com/DDDHL0704/riftbound/actions/runs/40990497783)（#289 修复后运行，固定 `c7100536`，不含 #292 缓存及 #294 配置，第 1 次）：最终 **success**，八片完整规则、四个 PostgreSQL 分区、SQL SignalR、网络、双桌面及最终完整门槛全部通过。9,649 个方法、19,890 条用例逐条 Counter 与固定 full 计划一致，含全部 144 个完整对局方法；69 个 SQL 场景恰好各执行一次。SQL 四片实际场景耗时约 211.1 / 112.1 / 165.7 / 133.6 分钟，销毁墓地最慢约 100.8 分钟，非卡死。
  - [41010811413](https://github.com/DDDHL0704/riftbound/actions/runs/41010811413)（运行，固定 `03106805`，第 2 次）：最终 **success**。八片完整规则、四个 PostgreSQL 分区、SQL SignalR 进程重启、网络、Windows/macOS 桌面及最终完整门槛全部通过；规则清单为 9,649 个方法、19,890 条用例，八片 Counter 合计 19,890，全部通过且零跳过，69 个 SQL 场景四分区恰好各执行一次。规则 testhost 全部确认四处理器 ARM64、Server GC、Regex 缓存 1,024；SQL 分区也确认 Regex 缓存 1,024。规则分片实际约 35.0 / 35.1 / 53.5 / 35.0 / 45.7 / 74.9 / 63.0 / 43.8 分钟；SQL 分区 0–3 最慢场景分别为 `destruction-graveyard` 57.8 分钟、`copied-last-breath-insight` 32.4 分钟、`copied-duel-insight` 24.6 分钟、`empowered-insight` 26.7 分钟，未见卡死。
  - [40708127555](https://github.com/DDDHL0704/riftbound/actions/runs/40708127555)（10 月 28 日手动运行）：6 局完整对局的夹具与当前战斗、触发时机及战场归属不一致；付款重连的可选减费夹具未适配候选结构变更。修复：[PR #236](https://github.com/DDDHL0704/riftbound/pull/236)，已合入，待完整夜验验证。
  - [40779960890](https://github.com/DDDHL0704/riftbound/actions/runs/40779960890)（10 月 29 日定时运行，固定 `b5691868`，不含 #236）：规则全量的 6 局完整对局与上次失败相同；付款重连的可选减费 SQL 夹具对多个候选调用 `Single()` 失败。原因同上，修复：[PR #236](https://github.com/DDDHL0704/riftbound/pull/236)。
  - [40783707647](https://github.com/DDDHL0704/riftbound/actions/runs/40783707647)（#236 合入后手动运行，固定 `d7f0ab9e`，最终 `cancelled`）：`rule_suite` 从 10 月 28 日 23:32 跑到 29 日 05:32 UTC，达到六小时上限被取消，聚合 `rules` 因此失败。PostgreSQL 四个分区、SQL SignalR 进程重启、网络、Windows/macOS 桌面及四个完整对局进程分片全部通过；普通规则集中在单个进程，未完成。修复：[PR #263](https://github.com/DDDHL0704/riftbound/pull/263)，已合入：一次 Release 构建、八个 GitHub 分片（最多六个并发）按实际耗时分配全部规则和完整对局，逐个核对全部实际用例恰好执行一次，保留完整验收门槛。
  - [40863260089](https://github.com/DDDHL0704/riftbound/actions/runs/40863260089)（#263 合入后运行，固定 `a7da2b67`）：完整清单包含 9,064 个方法、18,070 条用例和 144 个完整对局方法，无延后豁免。最终 **success**：八片完整规则、四个 PostgreSQL 分区、SQL SignalR 进程重启、网络及 Windows/macOS 桌面全部通过，规则与 PostgreSQL 汇总、最终完整验收门槛也通过；随后恢复完整夜验。
  - [40967451157](https://github.com/DDDHL0704/riftbound/actions/runs/40967451157)（北京时间 10 月 30 日定时运行，固定 `92a8350b`）：shard-3 的皮城学院回响完整对局失败；连续惩戒伤害留下多个同一单位的强制放逐替换，测试助手在 `DESTRUCTION_REPLACEMENT` 窗口继续让出优先权，未由指定玩家提交替换顺序选择。[#289](https://github.com/DDDHL0704/riftbound/pull/289) 已合入，保留付款、回响与完整日志回放断言，补充目标实际放逐和真实选择命令断言，并加入常规 L2，原有两局完整对局继续执行。本地该方法 1/1 通过；完整 [40980378753](https://github.com/DDDHL0704/riftbound/actions/runs/40980378753) 的 9,454 个方法、19,497 条用例和门槛全部通过，旧 19,496 条零移除，仅新增该回归；ARM64 实测该用例约 34.5 秒。最终仅该用例及依赖它的规则汇总、完整门槛失败；其余七个规则分片、四个 PostgreSQL 分区及 PostgreSQL 汇总、SQL SignalR、网络和双桌面全部通过。
  - [41024183616](https://github.com/DDDHL0704/riftbound/actions/runs/41024183616)（固定 `07592c16`，第 3 次）：最终 **success**，八片完整规则、四个 PostgreSQL 分区、SQL SignalR 进程重启、网络、Windows/macOS 桌面及最终完整门槛全部通过；规则与 PostgreSQL 汇总也通过。分区 0 于 08:06:30–11:47:00 UTC 完成（约 220 分钟），18 个场景均通过。与 #257 合入前的 41010811413 固定提交 `03106805` 对比，`destruction-graveyard` 为 6,562.7 秒（此前 3,468.3 秒）、`paka-move-reveal` 为 2,731.2 秒（此前 1,702.6 秒）、`attached-predict` 为 1,948.2 秒（此前 1,104.5 秒）、`multi-controller-trigger-order` 为 987.7 秒（此前 544.2 秒）；用例数量、冷恢复和结果均一致，未发现死循环。该差异与 #257 后的主干回归时间相符，但期间还有其他合入，后续按场景继续 profiling。
  - [41136063296](https://github.com/DDDHL0704/riftbound/actions/runs/41136063296)（10 月 31 日定时运行，固定 `3959436b`）：最终 **success**。规则八片、四个 PostgreSQL 分区、SQL SignalR、网络、双桌面及完整门槛全部通过；分区 0 从 22:11:33 到 01:49:41 UTC 完成，约 218 分钟，仍明显偏慢但未见卡死或失败。资源采样中 PostgreSQL 测试进程持续使用约 1 个 CPU，峰值 RSS 约 1.07 GiB，cgroup 没有 CPU throttling；分区从头到尾完成，无等待或挂起迹象。

  - **分区 0 变慢复核（10-30）**：固定提交 `03106805` 与 `07592c16` 之间，四个慢场景对应的测试和 fixture 文件没有变化；两次 PostgreSQL 产物中的命令数、事件数和冷恢复次数也一致，增加的是执行耗时。该区间还包含 #257 之后的其他合入，现有运行无法单独把回归归因于 #257；未发现用例增加或循环迹象。
  - **分区 0 复核（10-31）**：`03106805`（#257 合入前启动）到 `07592c16`（#257 合入后）间 18 个场景总耗时由约 122.4 分钟增至 219.2 分钟，所有场景均变慢；之后固定 `3959436b` 总耗时约 216.8 分钟，与前一次接近。最大场景 `destruction-graveyard` 从约 57.8 分钟升至约 109.4 分钟，最新约 110.3 分钟。提交间还合入了 #298、#293 及其他变更，所以结果显示回归发生于 #257 后，但不能单独归因于 #257；命令、事件和冷恢复数未增加，资源采样显示持续 CPU 计算而非等待或挂起。
  - [#317](https://github.com/DDDHL0704/riftbound/pull/317) 已合入：进一步对比同两次分区结果，分区 0 的 18 个场景全部变慢（约 1.16–1.92 倍），总场景耗时从 122.4 分钟升至 219.2 分钟；两次均为四处理器、Workstation GC、正则缓存 1,024。四个 SQL 分区的任务墙钟也都增加：0–3 分别从约 123.6／57.2／43.0／37.6 分钟变为 220.5／165.1／112.2／97.7 分钟，回归不限于分区 0。已有产物只有墙钟，尚不能区分规则计算量与 runner 性能差异。PostgreSQL 分区现已接入 L2 资源采样，按分钟保留进程树 CPU、RSS、cgroup 限流和 CPU 硬件信息，并保留原始 SQL 日志；193 项脚本检查通过（含 5 项真实子进程回归）。完整 [41099392372](https://github.com/DDDHL0704/riftbound/actions/runs/41099392372) 的 9,766 个方法、20,333 条用例和 `L2 required` 已通过，实际 Counter 与固定计划及 main 逐条一致、零增删；真实 PostgreSQL／SignalR／API 重启驱动通过，合入后的定时夜验测量实际 SQL 资源。全部 69 场景、冷恢复与完整门槛保持，不额外触发手动夜验。

## P1 · 规则缺陷与进行中的规则改动（C 线）

- [x] **P1-1 征服时机（#231）**。征服触发的技能应先让对手响应，再结算效果。PR [#275](https://github.com/DDDHL0704/riftbound/pull/275) 覆盖 SFD·212 磨牌、薇恩返回手牌、祖安、Shirana、Moonveil 的响应窗口与费用确认；对象跨区身份追踪和伪造历史校验已在 `c/zone-identity` 补齐。依据中国区核心规则 383.3.a–d、383.4.c.2.a–c、337.1.b、204.3.a、359.3.e、124、186.1。
- [ ] **P1-2 出牌候选计入可横置的符文**。现在只有法力池里已有的法力才算可用，手上能靠横置符文付费的牌也不会出现在可打出列表里。规则依据：204.4.b.1、430.3。前端目前靠询价绕过，详见 `clients/web/docs/server-findings.md` 的 S1。
- [x] **P1-3 待命布置时允许回收符文支付**。[#259](https://github.com/DDDHL0704/riftbound/pull/259) 已实现：`HIDE_CARD` 候选在支付待命费用时可携带回收符文资源动作，并在同一原子结算中完成回收与扣费。依据：811.1.b、430.3（见 S2）。
- [ ] **P1-4 其余前端反馈的服务端问题**：详见 `clients/web/docs/server-findings.md`。2026-10-30 已在 main 上逐条复核，以下问题仍然存在。每条单独一个小 PR，按顺序做：
  - [ ] **P1-4a 支付窗口里允许横置符文（S5）**。`PAY_COST` 的 `paymentChoiceIds` 像回收符文一样，可以携带横置符文；法力池不够时不再拒绝"资源不足，可以先使用符文"。涉及 `CoreRuleEngine.Tempered.cs`、`CoreRuleEngine.TriggerCosts.cs`。依据：204.4.b.1、430.3。和 P1-2 同属一类，可放在 P1-2 之后、复用同一套"横置付费动作"。
  - [ ] **P1-4b 传奇技能和主动技能的候选按可横置、可回收的符文计算（S2 末尾）**。复核 `LEGEND_ACT`、`ACTIVATE_ABILITY` 的候选是否也只看法力池；有问题的话按 P1-2 的同一方式修。依据同上。
  - [ ] **P1-4c 询价失败时返回具体原因（S4）**。`CoreRuleEngine.PlayCostPreview.cs` 现在固定返回"当前选择无法打出，请检查目标、位置和支付资源。"，丢掉了 `rejection` 的原因。改为返回具体原因（不能泄露隐藏信息）；缺少必选目标时，给出基础费用和已知增减费，并把目标相关的附加费用（如法盾）标注为"取决于目标"。
  - [ ] **P1-4d 拒绝原因改中文（S6）**。`CoreRuleEngine.cs` 里的 "Invalid printed power allocation."、"Payment resource actions are not required to play …"、"Not enough mana / power / experience / resources to play …" 改成中文，错误码不变。
  - [ ] **P1-4e 待命布置费用标明单选，并修正错误码（S7）**。`HIDE_CARD` 的费用选项在提示元数据里标明"必选 1 个"；`CoreRuleEngine.Standby.cs` 中"不能以该费用进行待命埋伏"的错误码从 `UNSUPPORTED_CARD_BEHAVIOR` 改为 `INVALID_PAYLOAD`。
  - [ ] **P1-4f 提示里没提供的命令统一返回"已过期 / 未提供"（S8）**。命令类型不在当前提示的已启用候选里时，返回 `PROMPT_EXPIRED` 或专门的错误码，不再进入该命令的阶段检查。
  - [ ] **P1-4g 出牌时多回收的符文被拒绝（S3）· `待确认`**。`AreRecycleRunePaymentResourceActionsRequired` 只允许"刚好够用"的回收。164.2.b、203–205、417、430 里都没找到这个限制；单独提交 `RECYCLE_RUNE` 也没有这个限制。需要规则负责人决定：放开，或者写明规则来源。决定之前不要改。
- [x] **P1-5 处理旧 PR**。#272 已合入，六片完整规则、`L2 required` 与验收驱动检查全部通过；#231 的替代交付继续在 P1-1 跟踪。
  - #158（S3–S6 严格重连验收）：已被后续改动覆盖，复核后保持关闭。
  - #194（把 S7 接入夜间验收）：旧 PR 已关闭；仍适用的施加移动费用已由 #272 接入默认付款重连夜间验收，覆盖真实 SQL、进程重启、恢复与回放。
  - 已复核 #158：保持关闭；严格 Joined 令牌、SQL 不变基线、付款重连和协调伪造负控已在 main，相关共享助手与测试逐字一致。网络消费者的后续差异来自额外减费与受限法力适配。

## P2 · 卡牌（B 线）

- [x] **P2-1 完成 #205**：VEN078 / 089 / 129 的相关工作已在 main，VEN117 / 159 的真实付款与战斗测试由 #237 合入；#205 已随合并自动关闭。
- [x] **P2-2 合入分支 `codex/cards-ven010-paid-current-grave-20261028`**：VEN010、013、093、103、122 的付款、墓地计数和反应测试由 #237 合入。
- [ ] **P2-3 VEN 卡牌**，见 [卡牌清单](CARD_BACKLOG.md)：
  - D 线负责普通单位、法术、装备与战场；[PR #265](https://github.com/DDDHL0704/riftbound/pull/265) 的 30 张法术／移动牌已合入；[PR #257](https://github.com/DDDHL0704/riftbound/pull/257) 收拢余下 51 张普通牌（28 名单位、14 件装备、9 处战场），已同步 #262 并修复回合开始事件兼容性，定向复验后统一送检。影分身由 B 线 #262 合入。
  - [x] #242 已合入：流光镜影、铁血狼母及阿卡丽·无形致命，共八个印次。
  - [x] #260 已合入：杰斯·进步先锋及杰斯·举锤待发，共四个印次。
  - B 线负责传奇、英雄单位、专属法术及已有 PR；D 线负责其余单位、法术、装备和战场。
  - 按机制成批接入，同一机制的牌放在一个 PR，共用表格式测试；
  - 每张牌至少覆盖正常结算、非法或边界情况各一条；
  - 先做传奇和英雄，预组和自定义卡组都依赖它们；不同副标题的英雄按不同效果分别跟踪。
  - [x] #254 已合入：13 项 VEN 传奇与英雄效果、31 个官方 VEN 印次；包含未来守护者、狂暴之心、影流之主、沙漠死神和杰斯「推陈出新」。六片完整规则及 `L2 required` 全部通过，#243、#244、#246–#251、#253、#261 已自动标为合入；对应条目已从 CARD_BACKLOG 删除。
  - [x] #262 已合入：补齐七项 VEN 英雄效果、全部七张专属法术及同效果待核对印次，共 36 个官方印次；六片完整规则与 `L2 required` 全部通过，历史修正来源和隐藏信息守卫恢复通过。对应英雄、专属法术及 12 个待核对印次已从 CARD_BACKLOG 移除；VEN 传奇、英雄、专属法术由 B 线全部完成，其余 51 项普通牌效果由 D 线 #257 接入；影分身已随 #262 合入。
- [ ] **P2-4 RAD 153 种**：A 线已核实中国区已生效。中国区官方[赛事规则](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/0d5c26b5535345d48e1afafb3e2a0018.pdf) 601.3.c.6 将“辉耀群星”（RAD）列入当前标准赛制，601.3.c.6.a 明确发售日期为 **2026-10-23**（#264）。B 线完成 VEN 传奇、英雄和专属法术后，接 RAD 传奇、英雄单位、专属法术及专属装备。官方快照中的 RAD 英雄按 30 项不同副标题分别跟踪；按 P2-3 同样方式逐卡验证、合入后移除清单。
  - [x] [#281](https://github.com/DDDHL0704/riftbound/pull/281) 已合入：厄斐琉斯·出尘、伊泽瑞尔·翩然俊朗、永恩·一剑封尘、凯隐·巨镰奔扬、瑟提·魁首、放逐之刃、铁铠冥魂及轮回绝境，共八项效果、17 个新 RAD 印次；同时补齐三个既有 SFD 伊泽瑞尔印次。六片完整规则与 `L2 required` 全部通过，对应八项已从 CARD_BACKLOG 移除，RAD 剩余 164 项效果。
  - [x] [#285](https://github.com/DDDHL0704/riftbound/pull/285) 已合入：莫德凯撒·冥王进军与冥途二渡，共两个英雄效果、四个官方印次；可选弃牌摧毁、每个文本实例每回合一次的摧毁费用与休眠召回均已接入，九片完整规则和 `L2 required` 全部通过。对应两项已从 CARD_BACKLOG 移除，RAD 剩余 162 项效果。
  - B 线 `b/rad-entry-cost`：艾克·灵光一现与放逐之锋合批，共两项效果、三个 RAD 印次。接入返回友方单位的必付额外费用、装配费用二选一及百炼减费后的费用选择（204.1.b、818.1.c、821.1.c）；定向回归中，通过后只开一个 PR，合入后移除清单。
  - **共享机制矩阵（A 线，待逐项实现）**：已将当前 `CARD_BACKLOG` 的 RAD 原文与引擎现有解析／结算能力交叉核对。下列条目是主干尚无独立共享模型、或仍缺少完整恢复与隐私覆盖的机制；后续卡牌 PR 必须复用对应共享流程，不能为单张卡旁路实现。编号按中国区官方快照，异画以同一组编号合并。
    - 炸弹装备指示物的生成、同处连锁摧毁、回合结束重建及来源绑定：`RAD·001`、`023/023a`、`094`、`096`、`112/112a`、`141`、`142`、`169*/169`。
      - [x] [#319](https://github.com/DDDHL0704/riftbound/pull/319) 已合入：部署的非单位装备免于未贴附召回（核心 323.6、830.1.b.3），实际清理与待清理任务共用判断；本地 Release 54/54 通过。完整 [41109837298](https://github.com/DDDHL0704/riftbound/actions/runs/41109837298) 的九片规则、9,794 个方法、20,414 条用例和门槛全部通过；实际 Counter 与固定计划一致，较前次全绿 main 仅新增 11 条部署清理回归，零移除。炸弹生成、同处连锁及打出落点继续另行实现。
      - [x] [#320](https://github.com/DDDHL0704/riftbound/pull/320) 已合入：按核心 830.1.b、830.2 捕获对手据守时的部署自毁技能，复用现有响应窗口与摧毁流程，保留原控制者和来源代际；覆盖真实反应、有目标反制的拒绝、移动、控制权／关键词变化、返手再进场、恢复反伪造及隐藏来源投影。Release 原有据守 34/34、新部署 26/26 定向通过；完整 [41118008709](https://github.com/DDDHL0704/riftbound/actions/runs/41118008709) 九片规则与 `L2 required` 通过，9,799 个方法、20,440 条实际用例与固定计划 Counter 逐条一致，较 #319 零移除、仅新增 26 条部署据守回归。投影在 `BuildStackItemSnapshotView`、`OrderTriggerSourceObjectIdForViewer`、两个隐藏来源判断方法及 `CapturedBattleSourceIsPrivate` 两个重载追加部署分支；这些方法也见旧 #275，先合入者优先，后续 C 线同步 main 时保留双方分支。
    - 通用随从／机器人／金币／战鹰指示物的生成、关键字、控制者和替换位置：`RAD·007`、`009`、`022`、`049`、`052`、`100`、`101`、`105`、`116`、`119`、`127`、`132`、`138`、`157`、`159`。
    - 正面朝下的待命、伏击和反应牌状态转换，含额外费用、零费用响应、回收与回放：`RAD·081`、`090/090a`、`095`、`100`、`102`、`103`、`107`、`108`、`153/175`、`154`。
    - 亮出与多来源牌库检视的隐私投影、来源身份和选择顺序：`RAD·011`、`032`、`033`、`060`、`067`、`074`、`082`、`103`、`111`、`127`、`129`。
    - 装备在场期间转换为单位、保留原装备身份并可再次转换：`RAD·145`、`171*/171`。
    - 基础战力临时设为卡牌法力费用或固定数值，并与增益、静态光环叠加：`RAD·057`、`110`、`118`。
    - 任意数量符能作为费用、逐个放逐后按原位置或基地重放，以及顺序可选的多对象回放：`RAD·140`。
    - 每名玩家独立的每回合首次打出／摧毁计数和战场技能使用权：`RAD·047`、`089`、`158`、`159`、`163`、`165`。
    - 符文牌堆为空、征服后下一张牌和据守时机的区域条件及得分：`RAD·160`、`162`、`166`。
    - 开放战场选择、跨战场移动、强制对手移动和移动费用支付：`RAD·062`、`084`、`087`、`098`、`102`、`113`、`115`、`133/133a`、`146`、`154`、`155/176`。
    - 法术锁定整回合、临时获得反应／急速以及待结算对象的时机窗口：`RAD·041`、`063`、`084`、`090`、`154`。
    - 多模式选择、同回合不得重复模式和“从四项选两项”的声明与恢复：`RAD·013`、`033`、`043/043a`、`045/045a`、`105`、`127`、`146`、`152`。
    - 返手、休眠、颜色符能和装配费用的替代支付，以及按本回合行动／休眠数量动态减费：`RAD·014`、`028`、`029`、`068/068a`、`070`、`075`、`080`、`122`、`123`、`150`、`151/174`、`162`、`163`。
    - 以坚守、战力或装备法力费用计算伤害／关键字，并与壁垒、法盾和不能造成战斗伤害叠加：`RAD·004`、`006`、`048`、`056`、`058`、`059`、`078`、`079`、`085`、`117`、`121`、`124`、`128`、`134`、`144`、`147`、`148`。
    - 中立卡组准入、起始传奇之外的传奇选择和动态传奇区域：`RAD·167`。
    - 对手猜测卡牌类型后按命中／未命中分流抽取或回收：`RAD·064`。
    - 按战场类型、英雄、部署装备及友方数量组合的静态光环，并在占领变化后重算：`RAD·002`、`017`、`024`、`037`、`039`、`040`、`044`、`072`、`073`、`087`、`128`、`164/184`。
    - 装备被摧毁后付费返手、据守时自返手和征服／进攻时的同处触发：`RAD·130`、`156`。
- [x] **P2-5 杰斯英雄遗漏与现有效果修正**：VEN·068 / 068a「进步先锋」与 VEN·088 / 088a「举锤待发」由 #260 合入；VEN·175「推陈出新」及 SFD·084/221 的完整技能由 #254 合入。使用中国区 260403 勘误后的“若如此做”，按实际摧毁结果授予装备打出权限。

- [x] E 线缴械首批：接入神秘的时间法师、饰品商贩、叮响巡警、处置专家、浅滩海妖；共享缴械按核心 831.1.d.1 / 831.2 合计数值、每次进攻只触发一次，支持机械光环、机器人、复制文本、法盾、恢复与回放。CARD_BACKLOG 的五项移除随本 PR 合入。
- [x] E 线进场与额外费用六张：弥留之灵、微光泳者、盘踞野猪、熔炉之灵、满载的旅人、鹰翼中队。共享摧毁时控制者回合账本、可选摧毁单位或装备费用及付款后伤害响应、可选活跃进场及本回合移动禁令、可选返回装备费用与减费、返回装备法力费用收据及同战场装备关键词条件战力；对应 CARD_BACKLOG 六项随本 PR 移除。
- [x] E 线移动与条件触发五张：狂热目击者、重甲象骑、古灵翼兽、觅血虫群、圣所保管员。共享移动展示／强力判定／回收、按当前坚守值计算伤害、立即结算征服资源技能、按本回合实际征服战场减费，以及成功打出高费用卡牌后的来源活跃；正常、非法目标、费用、来源离场、恢复与回放测试及 CARD_BACKLOG 五项移除放在同一 PR。
- [x] E 线摧毁与据守触发五张：风暴锋喙鸟、光照者教团、哀戚悲灵、战地甲胄师、钟塔守护者。共享眩晕敌方死亡观察、每批友方死亡增益、死亡位置捕获后的可选摧毁、战场装备死亡付费返手、据守洞察，以及绝念放逐牌库顶后可选打出；只免法力时保留符能及额外费用。Release 零错误，装备复制文本与回放 9/9、其余新牌及完整 ConformanceFixtureRunnerTests、目录／隐私／来源身份／Provenance 守卫 3669/3669 通过；CARD_BACKLOG 五项删除随同一 PR 合入。

- [ ] E 线亮出与打出选择批次（`e/rad-reveal-play`，#315）：龙犬驯兽师、冥顽构装体、苍穹腾蛟、暗巷守护者、祸心陌客。亮出在确认阶段选择并锁定信息，手牌展示、场上只挑选且排除背面待命；零符能亮出仍完成后续抽牌。龙犬展示全部对手后合计选一张免费打出，祸心在进场后确认战场与可选返手目标、结算时检查四单位门槛。亮出卡牌排除指示物，亮出机械允许机器人及机械复制指示物，并记录其后消失时的身份凭据。Release 零警告错误；初批回归 3763/3763、共享洞察／目标／恢复／待命 248/248；补充指示物边界与完整 ConformanceFixtureRunnerTests、目录／隐私／来源身份／Provenance 回归 3732/3732 通过，均含冷恢复与完整回放。CARD_BACKLOG 五项移除随同一 PR。依据：核心 185、832.1.c.2 / 832.1.c.4 / 832.1.d.1、355.8；暗巷 FAQ、811.5.a、129.4。

## P3 · 工程与提速（A 线，L2 提速最优先）

- [x] **P3-1 拆分巨型文件**：[#283](https://github.com/DDDHL0704/riftbound/pull/283) 已合入。按领域纯搬移为 68 个部分类文件，三个入口分别降至 6,102 / 5,668 / 1,723 行；55 个类型、4,243 个成员正文、16 个完整声明和各类型字段／属性初始化顺序均保持一致。源码守卫读取全部拆分文件。最新 main 定向 34/34、187 项 Python 检查通过；完整 [40958041215](https://github.com/DDDHL0704/riftbound/actions/runs/40958041215) 的 9,453 个方法、19,496 条用例、完整对局及门槛全部通过，零跳过，Counter 与 #281 一致。
- [ ] **P3-2 L2 提速**（新增 VEN 后重新打开，`a/l2-ven-opening-batch`）：
  - [x] 包含 #327 的固定 main `d0c0487a` 已由 [41154516777](https://github.com/DDDHL0704/riftbound/actions/runs/41154516777) 完整验证；九片及 `L2 required` 全部成功，墙钟约 38 分 16 秒，最慢片约 32 分 36 秒。主干验证已恢复，30 分钟目标未达。
  - `a/l2-next-batch`：将十个测试类中的十四处连续横置准备改为生产批量命令，保持原符文顺序、资源数量、关键费用／战斗／选择断言及原始历史恢复与回放；仅改测试夹具。固定 `7573837b4` 的本地 Release 受影响测试与固定守卫 36 类、1,465/1,465 用例通过，零跳过；相对固定主干 `90f021ec1` 的 1,453 条对应 L2 用例零缺失，另含 12 条洞察完整开局回归。长运行采样确认依次推进冷恢复／原始日志重放，无循环或等待迹象。前一项 #320 已合入，已同步包含 #259／#320 的 main `9f8eb0476`，十个测试文件均未发生上游改动；同步后的 Release 构建和关键回归正在补跑，独立 PR 保留完整 L2 与门槛。
  - `a/l2-runtime-profile`：复核新增 VEN 机制后的主干 [41013909621](https://github.com/DDDHL0704/riftbound/actions/runs/41013909621)，原有慢方法耗时中位数增加约 38%，testhost 分配量增加约 40%；九片完全均衡的执行下界也约 30.2 分钟，需继续优化测试夹具或执行方式，不能只靠重平衡。本轮把共享测试驱动消费者中的 45 处连续单符文横置准备改为生产批量命令，保留相同符文顺序、资源总量、逐批实际事件和完整恢复／去重／回放断言；不改引擎。完整规则清单与唯一门槛保留。
  - [#310](https://github.com/DDDHL0704/riftbound/pull/310) 已合入：将 `VenRunePrintingConsumerTests` 中真实开局的三张符文逐枚横置／回收和重复失败提交改为批量命令，保留每张印次身份、代际、区域、法力／符能、符文顺序、RNG 游标、非法外部符文、过期命令、自然重入和冷恢复断言；不改引擎。完整 [41063997240](https://github.com/DDDHL0704/riftbound/actions/runs/41063997240) 及包含该优化的主干 [41078351418](https://github.com/DDDHL0704/riftbound/actions/runs/41078351418) 的九片规则与 `L2 required` 全部通过；后者分片约 27.4–31.1 分钟，仍高于 30 分钟整次目标。
  - [#323](https://github.com/DDDHL0704/riftbound/pull/323) 已合入（`cd545d595`）：将九个测试夹具中连续的符文横置／回收准备改为生产批量命令，不改引擎；本地 Release 定向回归 24/24 通过，PR L2 [41137679904](https://github.com/DDDHL0704/riftbound/actions/runs/41137679904) 九片及 `L2 required` 全部通过。
  - [#327](https://github.com/DDDHL0704/riftbound/pull/327) 已合入（`d0c0487a`）：将 VEN013/103 合法开局的提交牌组、就绪和调度起手阶段冷恢复合并为阶段末一次，保留支付、结算、回放及关键恢复检查；PR L2 [41151087570](https://github.com/DDDHL0704/riftbound/actions/runs/41151087570) 九片与 `L2 required` 全部通过，分片用时约 25.4–33.4 分钟。
  - `a/l2-ven-opening-batch`：继续压缩 VEN013/103 合法开局 fixture 的冷恢复成本，仅在支付、移动、结算边界和最终 journal replay 做恢复；不改引擎或 #287 共享文件。两个合法开局 actor 用例的本地 Release 定向回归 2/2 通过，串行用时约 7 分 35 秒；固定 main L2 [41159049774](https://github.com/DDDHL0704/riftbound/actions/runs/41159049774) 中对应的 P1/P2 用例分别用时约 12 分 23 秒和 10 分 52 秒，合计约 23 分 16 秒。运行环境不同，仍需 PR L2 验证真实收益；当前改动只涉及测试夹具，未改引擎或 #287 共享文件。
  - 固定 `cc16515b6` 的主干完整 [41057698950](https://github.com/DDDHL0704/riftbound/actions/runs/41057698950) 九片规则与 `L2 required` 均通过，运行墙钟约 38 分 48 秒，主干验证恢复但仍高于 30 分钟目标。
  - 包含 #316 的固定 `febefb0be` 主干完整 [41098699611](https://github.com/DDDHL0704/riftbound/actions/runs/41098699611) 已于 18:08 UTC 通过九片规则及 `L2 required`，确认合入后主干仍能完整验证；本次墙钟约 42 分 40 秒，P3-2 保持打开。
  - 包含 #317 的固定 `6ef93d4fb` 主干完整 [41104458818](https://github.com/DDDHL0704/riftbound/actions/runs/41104458818) 已通过九片规则和 `L2 required`；9,792 个方法、20,403 条实际用例 Counter 与固定计划及前次全绿 main 完全一致，零增删。
  - [x] 第一优先：[PR #263](https://github.com/DDDHL0704/riftbound/pull/263) 已合入，修复 P0-2 的规则全量六小时超时，复用固定构建与逐用例校验，把完整夜验分散到八个独立任务（最多六个并发）；手动完整夜验 [40863260089](https://github.com/DDDHL0704/riftbound/actions/runs/40863260089) 的规则、集成验收和最终门槛已全部通过；
  - 把最慢的测试分散到各个分片；目标是单次 L2 在 30 分钟内跑完；
  - [#292](https://github.com/DDDHL0704/riftbound/pull/292) 合入后的固定 `9a248f9d` 主干完整 [40996327487](https://github.com/DDDHL0704/riftbound/actions/runs/40996327487) 全部通过，包含准备和汇总的总墙钟 **27 分 37 秒**；9,491 个方法、19,625 条用例逐条 Counter 与 #292 及固定计划一致，零跳过。九片实际执行约 19.7–24.1 分钟，全部实际 testhost 确认四核 Server GC 和 1,024 条正则缓存；完整规则、三局完整对局及唯一门槛保留。并发名额占满仍会增加排队时间；后续新增卡牌若让主干完整耗时超出 30 分钟，重新打开本项。
  - 后续固定 `958e8b11` 的主干 [41000890024](https://github.com/DDDHL0704/riftbound/actions/runs/41000890024) 与固定 `40953bcb` 的主干 [41005207988](https://github.com/DDDHL0704/riftbound/actions/runs/41005207988) 也分别在 **26 分 28 秒 / 27 分 43 秒**完整通过相同 9,491 个方法、19,625 条用例和门槛；Counter 与固定计划及前次全绿结果逐条一致，零跳过。
  - 包含 #296 的固定 `ee4d1dfc` 主干 [41008311699](https://github.com/DDDHL0704/riftbound/actions/runs/41008311699) 在 **26 分 39 秒**完整通过同一清单与门槛，零跳过，逐条 Counter 一致。
  - [#294](https://github.com/DDDHL0704/riftbound/pull/294) 已合入：夜验的固定规则构建及八个完整规则分片复用 L2 已验证的四核 ARM64 runner；四个 PostgreSQL 分区先显式构建 Release，再使用同一产物执行完整分区，减少 Debug 构建的运行开销。最多六片规则并发、全部完整对局、PostgreSQL 服务、固定输入与逐用例汇总保持；现有 PostgreSQL 16.13 固定镜像摘要已确认包含 Linux ARM64。源码没有以 DEBUG 为条件的验收分支；完整 [40996611444](https://github.com/DDDHL0704/riftbound/actions/runs/40996611444) 的九片规则、19,625 条用例与门槛及验收驱动检查通过，Counter 与固定计划及 main 逐条一致；实际完整夜验耗时仍须合入后验证。
  - `a/l2-regex-cache` / [#292](https://github.com/DDDHL0704/riftbound/pull/292)：测试进程把正则缓存下限从默认 15 调整为 1,024，并在实际 testhost 中记录容量。现有解析、目录及排序器定向测试 385/385、零跳过。[40985858213](https://github.com/DDDHL0704/riftbound/actions/runs/40985858213) 九片完整 9,491 个方法、19,625 条用例和门槛通过，实际 testhost 全部确认缓存 1,024、四核 Server GC，含排队总墙钟 **29 分 10 秒**；相对固定 main #285 零移除，仅新增 #289 的完整对局。随后八片重平衡 [40988967761](https://github.com/DDDHL0704/riftbound/actions/runs/40988967761) 也完整通过相同 Counter，但最慢片约 27 分钟，加上准备、汇总与队列共 **35 分 34 秒**；因此最终保留九片及原始分配。完整规则、三局完整对局、固定构建和唯一门槛保持。最终九片 [40992807104](https://github.com/DDDHL0704/riftbound/actions/runs/40992807104) 的全部规则与门槛通过；9,491 个方法、19,625 条用例逐条 Counter 与固定 main 及最终计划一致，零跳过，九个 testhost 均确认缓存 1,024、四核 Server GC，#292 已合入。九片实际执行约 19.9–24.0 分钟；因全仓并发名额占满，最后一片晚启动约 12 分钟，整次墙钟 **36 分 42 秒**。包含该优化的固定 `9a248f9d` 主干 [40996327487](https://github.com/DDDHL0704/riftbound/actions/runs/40996327487) 后续已在 27 分 37 秒内完整通过，详情见本项首条结果。
  - [x] [#290](https://github.com/DDDHL0704/riftbound/pull/290) 已合入：CI 命令失败时，把完整日志末尾 80 行打印到 Actions 标准输出，保留原始退出异常及完整日志文件；编译错误可直接在任务日志中查看。完整 [40982404811](https://github.com/DDDHL0704/riftbound/actions/runs/40982404811) 的九片规则与门槛全部通过。
  - [x] [PR #256](https://github.com/DDDHL0704/riftbound/pull/256) 已合入：取消被新提交取代的 PR 运行，汇总遵守取消状态，保留测试失败时的门禁校验，解决旧 `always()` 汇总继续排队的问题（如 [40783866463](https://github.com/DDDHL0704/riftbound/actions/runs/40783866463)）；
  - 队列复核发现 #284 于 10 月 29 日 23:02 UTC 关闭且未合入，其 [40966626313](https://github.com/DDDHL0704/riftbound/actions/runs/40966626313) 仍在执行，23:13 UTC 还占三个运行名额，已手动取消。[#288](https://github.com/DDDHL0704/riftbound/pull/288) 已合入：独立关闭事件工作流使用同一个 PR 并发组自动取消旧验证；关闭事件不发布 `L2 required` 检查，重新打开 PR 后仍须通过完整规则。真实关闭／重开验证通过，重开后的完整 [40972608937](https://github.com/DDDHL0704/riftbound/actions/runs/40972608937) 全部 19,496 条用例和门槛通过，Counter 与 #286 一致；main 的并发组保留独立。
  - [x] #256 已加入文档/BACKLOG 白名单，纯文档 PR 只运行 2 个轻量任务；常规 L2 从 10 个任务减到 8 个，保留完整规则与 `L2 required`；
  - [x] [PR #264](https://github.com/DDDHL0704/riftbound/pull/264) 最初因浅克隆缺少 Git 祖先而拒绝文档豁免；[PR #263](https://github.com/DDDHL0704/riftbound/pull/263) 保留两层历史，并在选择分片前完成两次范围核对，判定不了时退回完整规则。同步后真实重验 [40863258647](https://github.com/DDDHL0704/riftbound/actions/runs/40863258647) 的 prepare、文档凭据核对及 `L2 required` 全部通过，规则套件按预期跳过，门槛结果为 `DOCUMENTATION_EXEMPTION_VERIFIED`；#264 已合入；
  - [x] [PR #299](https://github.com/DDDHL0704/riftbound/pull/299) 已合入：夜验 `postgres` 与 `rules` 聚合 job 使用 `always() && !cancelled()`，失败子任务仍进入完整门禁，取消的旧 workflow 不再排队新的汇总 job；21 项 full gate、SQL gate、L3 workflow 及 harness smoke 检查通过。
  - #256 六片完整通过；shard-5 从 04:47:44 到 08:00:58 UTC 完成 2,875 条测试，用时约 193 分钟，并非卡死。VEN013/103 单条合法开局分别耗时 49/107 分钟；#263 的[完整 L2 40852494017](https://github.com/DDDHL0704/riftbound/actions/runs/40852494017) 覆盖 8,883 个方法、17,748 条用例，最慢分片仍需 74 分钟，其中 P2 单条约 62 分钟；
  - [PR #266](https://github.com/DDDHL0704/riftbound/pull/266) 已合入；[40867854592](https://github.com/DDDHL0704/riftbound/actions/runs/40867854592) 六片及 `L2 required` 全部通过：8,905 个方法、17,804 条用例，较 #263 原始清单零移除。最慢片约 44 分钟，VEN013/103 P2 单条约 6 分钟。本地 136 条定向用例也已逐条对齐原清单通过；保留关键阶段冷恢复与完整原始历史校验。30 分钟目标尚未达到，`a/l2-scheduling` 用这次完整 Release 实测刷新权重并平衡每个测试类内的串行负载；四、五片的工作量下界分别约 42、34 分钟，本轮继续保留六片；
  - [PR #267](https://github.com/DDDHL0704/riftbound/pull/267) 已合入；[40881439549](https://github.com/DDDHL0704/riftbound/actions/runs/40881439549) 完整 8,905 个方法、17,804 条用例及门槛全部通过，原始清单逐条一致。六片实测约 38.6 / 30.1 / 37.3 / 40.6 / 39.5 / 19.0 分钟；
  - [PR #268](https://github.com/DDDHL0704/riftbound/pull/268) 已合入：用 #267 实测更新权重，将安全可枚举参数行的拆分阈值从 20 分钟降到 10 分钟。[40899479233](https://github.com/DDDHL0704/riftbound/actions/runs/40899479233) 完整 8,992 个方法、18,018 条用例及门槛全部通过；原 17,804 条零移除，新增 214 条恰好来自 #265 的十个机制测试类。六片实测约 19.8 / 32.4 / 42.0 / 42.2 / 41.0 / 22.9 分钟，30 分钟目标未达到；
  - [PR #269](https://github.com/DDDHL0704/riftbound/pull/269) 已合入：减少 VEN010 自然等牌与重复准备恢复，将销毁夹具连续横置合成生产批量命令，并优先启动慢测试集合。[40909584287](https://github.com/DDDHL0704/riftbound/actions/runs/40909584287) 完整 8,996 个方法、18,030 条用例及门槛全部通过，旧 18,018 条零移除；六片实测约 32.3 / 27.6 / 39.8 / 27.2 / 35.4 / 37.6 分钟，慢类已在各片起点启动，30 分钟目标仍未达到；
  - [PR #273](https://github.com/DDDHL0704/riftbound/pull/273) 已合入：取消关键词精确查找的临时集合分配，保留空白修剪、Ordinal 与运行时标签语义，并用 #269 实测刷新权重。[40919287657](https://github.com/DDDHL0704/riftbound/actions/runs/40919287657) 完整 9,186 个方法、18,477 条用例及门槛全部通过；相对固定 main 的 18,457 条零移除，新增 20 条解析测试。六片实测约 37.4 / 38.9 / 40.3 / 26.5 / 39.8 / 34.0 分钟，30 分钟目标仍未达到；
  - [PR #276](https://github.com/DDDHL0704/riftbound/pull/276) 已合入：完整规则和夜验的实际测试进程启用 Server GC，并记录 testhost 的 GC、分配、暂停、CPU 与内存。[40927908912](https://github.com/DDDHL0704/riftbound/actions/runs/40927908912) 完整 9,186 个方法、18,477 条用例及门槛全部通过，原清单和六片选择器逐条一致；六个 testhost 均确认四核 Server GC，峰值进程树内存约 1.2–1.6 GiB。六片实测约 28.1 / 32.4 / 15.9 / 14.8 / 16.1 / 27.4 分钟，整次约 35.2 分钟，30 分钟目标仍未达到；
  - [PR #277](https://github.com/DDDHL0704/riftbound/pull/277) 已合入：按 #276 完整实测刷新权重并记录 CPU 拓扑。[40934738477](https://github.com/DDDHL0704/riftbound/actions/runs/40934738477) 完整 9,186 个方法、18,477 条用例与门槛全部通过，原 Counter 逐条一致。六片实测约 31.4 / 30.4 / 32.4 / 22.5 / 31.1 / 33.2 分钟，整次 37.0 分钟，30 分钟目标未达到；五片使用 EPYC 7763，一片使用 EPYC 9V74，均观测为四逻辑处理器、两核心。
  - [PR #278](https://github.com/DDDHL0704/riftbound/pull/278) 已合入：L2 固定构建和六片执行改用公开仓库免费的四核 ARM64 标准 runner。[40940869443](https://github.com/DDDHL0704/riftbound/actions/runs/40940869443) 完整 9,186 个方法、18,477 条用例及门槛全部通过，原 Counter 与六片选择器逐条一致；Linux aarch64 的参数行预检及负控通过，六个 testhost 均确认四核 Server GC。六片实测约 25.7 / 23.7 / 26.4 / 23.7 / 24.1 / 24.4 分钟，整次 **29 分 59 秒**，首次进入 30 分钟，但余量只有一秒。
  - [PR #280](https://github.com/DDDHL0704/riftbound/pull/280) 已合入：按当前完整 ARM64 实测调整为八片，常规 PR 共 10 个任务、峰值并发 8 个，两个完整运行的规则峰值共 16 个，文档 PR 仍为两个轻量任务。[40952937606](https://github.com/DDDHL0704/riftbound/actions/runs/40952937606)（固定 `0e9eff9c`）完整 9,406 个方法、19,220 条用例及门槛全部通过，零跳过，Counter 与 #262 及六片基线逐条一致；整次 **28 分 46 秒**，八片分别约 24.3 / 23.0 / 22.3 / 23.8 / 22.8 / 24.9 / 25.0 / 24.4 分钟，八个 testhost 均确认为四处理器 ARM64 Server GC。此前六片基线总工作量约 778.1 用例分钟，六、七片执行下界约 32.4 / 27.8 分钟，尚未计准备与汇总，因此选用八片。
  - #283 的完整 [40958041215](https://github.com/DDDHL0704/riftbound/actions/runs/40958041215) 全绿，但 19,496 条用例的总耗时回升到 **31 分 17 秒**；八片约 25.2–27.6 分钟，总工作量约 831.3 用例分钟，最慢单例约 12 分钟，无卡死证据。新增 276 条用例仅约 1.4 用例分钟，已有用例运行时间普遍增加；具体原因尚未定位。八片即使完全平衡，执行下界仍约 26.0 分钟，加上本次准备和汇总约 3.7 分钟仅余约 20 秒，因此重新打开本项。
  - [#286](https://github.com/DDDHL0704/riftbound/pull/286) 已合入：刷新 #283 完整实测权重并改为九片，两个完整规则运行峰值为 18 个任务；常规 PR 共 11 个任务，文档 PR 仍只有两个。完整 [40963666309](https://github.com/DDDHL0704/riftbound/actions/runs/40963666309) 的 9,453 个方法、19,496 条用例、完整对局及门槛全部通过，Counter 与 #283 逐条一致，零跳过；九片实际执行约 21.8 / 22.8 / 21.1 / 22.8 / 22.1 / 23.7 / 22.3 / 22.0 / 24.0 分钟。因 runner 排队，总墙钟 **57 分 07 秒**，当次不能作为整次小于 30 分钟的验收，当时仍保持未勾选；包含 #286 的主干 [40970885455](https://github.com/DDDHL0704/riftbound/actions/runs/40970885455) 也已完整通过相同 19,496 条用例和门槛，Counter 逐条一致，零跳过；因排队总墙钟为 **77 分 11 秒**。夜验保留八片、最多六并发；完整规则、完整对局、逐用例 Counter 和唯一门槛保留。
  - main 的 [40840918172](https://github.com/DDDHL0704/riftbound/actions/runs/40840918172) 被 #260 的新提交取消；后续 [40853978883](https://github.com/DDDHL0704/riftbound/actions/runs/40853978883)（固定 `25913d6a`）已完成，六片全部 17,804 条用例及 `L2 required` 通过，恢复合入后的主干完整验证。已让包含 #266 新夹具的 main [40880177592](https://github.com/DDDHL0704/riftbound/actions/runs/40880177592) 接替刚启动的旧版本 40863395876。包含 #266 的该次 main 运行现已完整通过。包含 #269 的固定 `94092d89` main [40917321493](https://github.com/DDDHL0704/riftbound/actions/runs/40917321493) 也完整通过 18,457 条用例与门槛。后续固定 `d9f26d83` 的 main [40913724782](https://github.com/DDDHL0704/riftbound/actions/runs/40913724782) 也完整通过 18,445 条用例与门槛，已包含 #254 新卡。PR 与 main 的并发组隔离，#263 保留运行中的 main、待跑只留最新；文档豁免只适用于 PR，main 推送始终全量；
  - main [40927710867](https://github.com/DDDHL0704/riftbound/actions/runs/40927710867)（固定 `e8de1e21`）及包含 #276 的 main [40933612646](https://github.com/DDDHL0704/riftbound/actions/runs/40933612646)（固定 `b17468bc`）均已完整通过 9,186 个方法、18,477 条用例与门槛，原 Counter 逐条一致。 包含 #277 的 main [40940552211](https://github.com/DDDHL0704/riftbound/actions/runs/40940552211)（固定 `6c934aef`）也已完整通过，原 Counter 逐条一致。
  - 包含 #262 和 #278 的 main [40946282257](https://github.com/DDDHL0704/riftbound/actions/runs/40946282257)（固定 `afab4e71`）已完整通过 9,406 个方法、19,220 条用例和门槛，Counter 与 #262 逐条一致。
  - 所有代码 PR 保留完整 L2 规则、完整对局及 `L2 required` 门槛；分片与夹具优化均逐用例核对完整清单。
- [x] **P3-4 推送前受影响测试**（`a/affected-tests`）：[#304](https://github.com/DDDHL0704/riftbound/pull/304) 已合入。`scripts/test-rules.py affected --base origin/main --jobs 1` 根据源码、测试文件和 RAD/VEN 卡号选择受影响测试，并固定追加卡牌目录、隐私、来源身份、Provenance、OfficialOpening 及源码架构守卫；分支落后 main 时先提示同步。184 项脚本测试、Release 构建及 26 个测试类 / 1,300 个用例定向回归通过，0 跳过。
  - [#316](https://github.com/DDDHL0704/riftbound/pull/316) 已合入：修复七个以 `Consumers` 结尾的真实规则测试类在改测试文件、源码类型或卡号时被漏选的问题，继续排除嵌套的 Driver／Store 等辅助类；受影响筛选回归加入 L2 准备阶段。完整 [41093074040](https://github.com/DDDHL0704/riftbound/actions/runs/41093074040) 的九片规则与 `L2 required` 全部通过，固定计划中的 9,766 个方法、20,333 条用例 Counter 与主干完整基线逐条一致，零增删。
  - [#318](https://github.com/DDDHL0704/riftbound/pull/318) 已合入：受影响测试改为一次 PATH `dotnet test` 完成还原、Release 构建与定向执行，避免构建完成后再次排队；保留原筛选器、固定守卫、单任务、编译诊断和 TRX 校验。197 项脚本检查通过，补充构建／测试失败、跳过用例及只打印筛选器的回归；PR 与合入后的固定 main `f27807109` 完整 L2 均通过，9,792 个方法、20,403 条用例与固定计划 Counter 逐条一致。
- [x] **P3-3 清理 `docs/legacy/`**：[#295](https://github.com/DDDHL0704/riftbound/pull/295) 已合入。4 个测试仍读取的 JSON 原样迁到 `tests/Riftbound.ConformanceTests/Fixtures/official-source/`，内容逐字节一致；删除其余退役文档和没有调用方的治理报告工具，历史内容保留在 Git 中。夜验只删除退役审计提交获取步骤，完整验收保留。AGENTS 同步四线职责、P1 归 C 线及本地单任务和 PATH 限流约束。32 条夹具读取定向测试逐条对齐 #262 清单通过，零跳过；182 项 Python 检查通过；完整 [41001117429](https://github.com/DDDHL0704/riftbound/actions/runs/41001117429) 的九片规则、三局完整对局及门槛全部通过，9,491 个方法、19,625 条用例 Counter 与固定计划及全绿 main 逐条一致，零跳过。
- [ ] **P3-5 卡牌登记冲突热点**（`a/registration-hotspots`，统计先行）：2026-10-30 复核最近 30 个 `feat(cards)` PR（#315 至 #239，#262／#257 超过 100 个文件的分页已完整计入）。`src/Riftbound.Engine/CardBehaviorRegistry.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs` 和 `tests/Riftbound.ConformanceTests/ReviewedRuntimeCatalogAdmissionTests.cs` 各被 30/30 个 PR 修改；`src/Riftbound.CardCatalog/RuleTextParsers.cs` 被 27/30 个修改；`src/Riftbound.CardCatalog/VendettaCardSources.cs` 被 19/30 个修改；`src/Riftbound.CardCatalog/RadiantCardSources.cs` 被 11/30 个修改；`src/Riftbound.Engine/PrintedCardFactory.cs` 被 15/30 个修改；`tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 被 3/30 个修改。下一 PR 要把这些汇总表改为按批次独立登记并从数据推导计数；当前 #287（31 文件、1,827 行新增）仍在等待合入且正修改上述共享文件，按约束待其合入后再开独立拆分 PR，避免与 B/D 的大 PR 交叉冲突。

## 接手旧分支

旧的四条线已暂停，交接位置如下：

| 内容 | 位置 |
|---|---|
| 已提交的工作 | 远端分支 `codex/*`，对应 P0-1、P1-1、P2-1、P2-2 |
| 未提交的改动和未跟踪文件 | 只在用户本机：主仓库里的本地分支 `archive/wt-*-20261029`，没有推送 |
| 重置前的全部文档 | 标签 `archive/pre-reset-20261029` |

未提交改动的分支里有几份值得捡回的测试草稿：
- 甘普朗相关 5 个测试文件、`Ven126FinitePreventionConsumerTests.cs`，在 `archive/wt-cards-*` 和 `archive/wt-combat-*` 里；
- 德莱文的旧并行实现，在 `archive/wt-parallel-official-audit-*` 里。

需要时用下面的命令取出单个文件：

```sh
git show <分支>:<路径> > <路径>
```

## 前端（Claude）

- 网页对战前端在 `clients/web`，PR #233 / #234 及分支 `web/battle-polish-2-20261028` 已覆盖桌面、卡组编辑器、战绩、音效、导览等。
- 服务端规则改了提示或命令格式时，在这里追加一条"前端需适配"，写明 PR 号。
  - `e/rad-reveal-play` 前端需适配：亮出候选位于打出提示的 `optionalCostChoices`，原样通过 `PLAY_CARD.optionalCosts` 提交最多一个 `SHOWCASE:<objectId>`；可以不选。此选择不支付费用，确认时锁定信息，手牌执行展示，场上卡牌只作挑选。
  - #262 前端需适配：凯尔的强化层数通过 `empowermentCount` 显示，最多三层。凯南征服授予流转新增可选费用：提示 `flowCostChoiceId` / `flowCostChoices`，通过 `PLAY_CARD.optionalCosts` 提交对应的 `FLOW_COST:<mana>:<power>:<traits>`。
  - #262 前端需适配：终极统治授予的回合内技能复用 `ACTIVATE_ABILITY` 提示，按返回的 `GRANTED:<hash>` 原样提交 `abilityId`，展示两点任意符能费用及付款候选；结算链的 `grantedAbilityId` 用于对应技能展示。
  - #281 前端需适配：铁铠冥魂的 `LEGEND_ACT` 提示新增 `additionalCostChoices` 和 `requiredAdditionalCostChoiceCount: 1`，从中选择一个放逐费用，按原值通过 `optionalCosts` 提交；这些费用候选不是技能目标。
  - `b/rad-discard-destroy` 前端需适配：选择触发目标后，使用 `CHOOSE_CARDS` / `TRIGGER_HAND_DISCARD_COST` 选择一张手牌；空选择放弃技能。若随后出现 `TRIGGER_TARGET_COST`，支付或放弃法盾费用与弃牌一起提交，支付成功后再开放响应。所选手牌在付款前只对操作者可见。
