# 待办与状态

这是项目唯一的状态文档。按优先级从上往下做：领任务时在条目后写上分支名，合入后勾掉；发现新问题直接追加。流程见 [AGENTS.md](../AGENTS.md)。

**当前状态（2026-11-01）**
- **main**：最新为 `a17ae2085`（#361 合入）。main 的 L2 [41280577175](https://github.com/DDDHL0704/riftbound/actions/runs/41280577175) 正在运行；#360 已 ready，新的 L2 [41280740291](https://github.com/DDDHL0704/riftbound/actions/runs/41280740291) 已通过 prepare 并运行九个规则分片。此前主干完整验证已恢复，但 P3-2 的 30 分钟目标仍未达到。
- **夜间验收**：此前 [40990497783](https://github.com/DDDHL0704/riftbound/actions/runs/40990497783)、[41010811413](https://github.com/DDDHL0704/riftbound/actions/runs/41010811413) 和 [41024183616](https://github.com/DDDHL0704/riftbound/actions/runs/41024183616) 连续三次全绿；定时夜验 [41242878171](https://github.com/DDDHL0704/riftbound/actions/runs/41242878171)（固定 `e0a389ded`）失败，连续计数归零，失败项与修复见 P0-2。
- **卡牌目录**：基准快照 1089 个印次；未接入的效果及待核对印次见 [卡牌清单](CARD_BACKLOG.md)。
- **重置前已暂停开发线的工作**：都已推到远端分支，接手说明见下面的"接手旧分支"；当前按 A/B/C/D 四线分工。

## P0 · 让 main 和夜间验收恢复全绿（A 线）

- [x] **P0-1 合入夜间验收的测试修正**。[PR #236](https://github.com/DDDHL0704/riftbound/pull/236) 已合入，基于 `codex/nightly-optional-reduction-selector-20261028`（`7aae34be`，已包含付款线的 `a79b0f32`）。只改测试：
  - 付款重连夹具：#223 改变了可选减费的候选结构，导致 `OptionalCostReductionAcceptance` 失败，这里同步更新；
  - 修正 6 局完整对局和对应的投影测试。

  SourceLone 对局已在伤害结算前无条件检查双方参与者，死亡后检查墓地和投影中的反向结果。L2 通过后已合入；[手动夜验 40783707647](https://github.com/DDDHL0704/riftbound/actions/runs/40783707647) 的付款重连、完整对局进程分片及其他集成验收均通过，规则全量因六小时超时而未完成，详见 P0-2。
- [ ] **P0-2 夜间验收连续通过（当前计数归零）**。此前 `40990497783`、`41010811413`、`41024183616` 已连续三次完整验收全绿，手动运行按当前口径计入；定时夜验 `41242878171` 失败后重新计数。修复合入后由管理者手动触发一次 `native-desktop.yml` 夜验确认，再恢复只依靠每晚定时运行。
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

  - [41242878171](https://github.com/DDDHL0704/riftbound/actions/runs/41242878171)（11 月 1 日定时运行，固定 `e0a389ded`）：规则全量通过，但两个真实 PostgreSQL 集成项失败。`postgres_partitions` 分区 0 的 `DravenBattleDeathSqlAcceptance` 在加载排队技能时报告 SQL 行与输入不一致；恢复时的触发器规范化复制器遗漏 `DravenBattleDeathScore`，导致 `DravenBattleDeathRecoveryRules` 无法验证复制后的技能。SQL SignalR 进程重启的 `optional-additional-cost-reductions` `play` 步骤退出码非零；可选减费 copied case 的首次连接需要重放完整恢复历史，旧 90 秒夹具 deadline 到期，取消最终在 `PostgresMatchJournal.RecordAsync` 表现出来。修复 [PR #361](https://github.com/DDDHL0704/riftbound/pull/361)（`a/p0-nightly-fix`）补上触发器字段复制（包括 Draven 分数和死亡观察者），并将该夹具 deadline 提高到 3 分钟并注明恢复重放成本；定向验证已通过，合入后手动夜验确认。

  - [41286487339](https://github.com/DDDHL0704/riftbound/actions/runs/41286487339)（11 月 1 日手动验证 #361，固定 `d1e8e100`）：规则、桌面和大多数集成项仍在运行；SQL SignalR 的 `restricted-mana-lux` 在拒绝无法支付的单位出牌时失败。#361 前已合入的提示守卫会把未出现在当前提示中的命令拒绝为 `PromptExpired`，而旧验收夹具仍断言 `InsufficientCost`。`a/p0-nightly-restricted`（`fd8078b6b`）同步 Restricted Mana 的本地、网络和 SQL 夹具，保留引擎直接调用的 `InsufficientCost` 断言，并将会话层未提供操作改为 `PromptExpired`。

  - **分区 0 变慢复核（10-30）**：固定提交 `03106805` 与 `07592c16` 之间，四个慢场景对应的测试和 fixture 文件没有变化；两次 PostgreSQL 产物中的命令数、事件数和冷恢复次数也一致，增加的是执行耗时。该区间还包含 #257 之后的其他合入，现有运行无法单独把回归归因于 #257；未发现用例增加或循环迹象。
  - **分区 0 复核（10-31）**：`03106805`（#257 合入前启动）到 `07592c16`（#257 合入后）间 18 个场景总耗时由约 122.4 分钟增至 219.2 分钟，所有场景均变慢；之后固定 `3959436b` 总耗时约 216.8 分钟，与前一次接近。最大场景 `destruction-graveyard` 从约 57.8 分钟升至约 109.4 分钟，最新约 110.3 分钟。提交间还合入了 #298、#293 及其他变更，所以结果显示回归发生于 #257 后，但不能单独归因于 #257；命令、事件和冷恢复数未增加，资源采样显示持续 CPU 计算而非等待或挂起。
  - **分区 0 场景分解（41136063296）**：Actions 日志的场景边界显示 `multi-controller-trigger-order` 约 16 分钟、`attached-predict` 约 31 分钟、`paka-move-reveal` 约 45 分钟、`destruction-graveyard` 约 110 分钟，四项合计约占分区 0 的 216 分钟墙钟；其余场景大多在 9 分钟以内。`destruction-graveyard` 的六个格式／付款分支共 44 次冷恢复（5/9/8 次各重复两种格式），每次都重放增长中的完整历史并校验七张 SQL 表。资源采样显示测试进程持续约 100% 单核、无 cgroup CPU throttling，说明主要成本是规则计算与恢复重放，而不是等待或死循环；后续提速应先从这四个场景分别采样规则计算与恢复耗时。
  - [#317](https://github.com/DDDHL0704/riftbound/pull/317) 已合入：进一步对比同两次分区结果，分区 0 的 18 个场景全部变慢（约 1.16–1.92 倍），总场景耗时从 122.4 分钟升至 219.2 分钟；两次均为四处理器、Workstation GC、正则缓存 1,024。四个 SQL 分区的任务墙钟也都增加：0–3 分别从约 123.6／57.2／43.0／37.6 分钟变为 220.5／165.1／112.2／97.7 分钟，回归不限于分区 0。已有产物只有墙钟，尚不能区分规则计算量与 runner 性能差异。PostgreSQL 分区现已接入 L2 资源采样，按分钟保留进程树 CPU、RSS、cgroup 限流和 CPU 硬件信息，并保留原始 SQL 日志；193 项脚本检查通过（含 5 项真实子进程回归）。完整 [41099392372](https://github.com/DDDHL0704/riftbound/actions/runs/41099392372) 的 9,766 个方法、20,333 条用例和 `L2 required` 已通过，实际 Counter 与固定计划及 main 逐条一致、零增删；真实 PostgreSQL／SignalR／API 重启驱动通过，合入后的定时夜验测量实际 SQL 资源。全部 69 场景、冷恢复与完整门槛保持，不额外触发手动夜验。

## P1 · 规则缺陷与进行中的规则改动（C 线）

- [x] **P1-1 征服时机（#231）**。征服触发的技能应先让对手响应，再结算效果。PR [#275](https://github.com/DDDHL0704/riftbound/pull/275) 覆盖 SFD·212 磨牌、薇恩返回手牌、祖安、Shirana、Moonveil 的响应窗口与费用确认；对象跨区身份追踪和伪造历史校验已在 `c/zone-identity` 补齐。依据中国区核心规则 383.3.a–d、383.4.c.2.a–c、337.1.b、204.3.a、359.3.e、124、186.1。
- [x] **P1-2 出牌候选计入可横置的符文**。出牌候选现计入可横置的活跃符文，并允许 `PLAY_CARD` 在同一原子命令中携带 `TAP_RUNE:<objectId>` 完成资源获取与支付；容量、顺序、恢复和回放均由共享支付流程校验。规则依据：204.4.b.1、430.3（S1）。
- [x] **P1-3 待命布置时允许回收符文支付**。[#259](https://github.com/DDDHL0704/riftbound/pull/259) 已实现：`HIDE_CARD` 候选在支付待命费用时可携带回收符文资源动作，并在同一原子结算中完成回收与扣费。依据：811.1.b、430.3（见 S2）。
- [x] **P1-4 其余前端反馈的服务端问题**：详见 `clients/web/docs/server-findings.md`。2026-10-30 已在 main 上逐条复核，以下问题仍然存在。每条单独一个小 PR，按顺序做：
  - [x] **P1-4a 支付窗口里允许横置符文（S5）**。`PAY_COST` 的 `paymentChoiceIds` 现可携带 `TAP_RUNE:<objectId>`，在同一原子结算中横置符文并继续支付，恢复与回放投影也同步可用候选。依据：204.4.b.1、430.3。
  - [x] **P1-4b 传奇技能和主动技能的候选按可横置、可回收的符文计算（S2 末尾）**。`LEGEND_ACT`、`ACTIVATE_ABILITY` 现计入可横置和可回收的符文，并在同一原子命令中按提交顺序应用横置、回收及传奇资源行动；容量、非法来源、重复和回放均由共享支付流程校验。依据：204.4.b.1、430.3。
  - [x] **P1-4c 询价失败时返回具体原因（S4）**。费用询价现在保留具体拒绝原因；缺少必选目标时返回已知费用并标注目标相关费用。依据：204.4.b.1、430.3。
  - [x] **P1-4d 拒绝原因改中文（S6）**。`CoreRuleEngine.cs` 里的 "Invalid printed power allocation."、支付资源动作无效、"Not enough mana / power / experience / resources to play …" 已改成中文，错误码不变。
  - [x] **P1-4e 待命布置费用标明单选，并修正错误码（S7）**。`HIDE_CARD` 的费用选项在提示元数据里标明"必选 1 个"；`CoreRuleEngine.Standby.cs` 中"不能以该费用进行待命埋伏"的错误码已从 `UNSUPPORTED_CARD_BEHAVIOR` 改为 `INVALID_PAYLOAD`。
  - [x] **P1-4f 提示里没提供的命令统一返回"已过期 / 未提供"（S8）**。命令类型不在当前提示的已启用候选里时，返回 `PROMPT_EXPIRED`，不再进入该命令的阶段检查。
  - [x] **P1-4g 出牌时多回收的符文被拒绝（S3）**。支付期间主动回收符文属于自愿获取资源，允许获取多于当前费用所需的符能；实际费用仍须满足指定特性，非法、重复、非本方、错误区域或未付足原费继续拒绝。PR #131 已合入。依据中国区核心规则 164.2.b、166–168、204.4.b.1、430.3。
  - [x] **P1-4h 单位技能“回收一张废牌”的费用标明单选（S9，`c/p1-s9`）**。`ACTIVATE_ABILITY` 提示为 `RECYCLE-MIGHT` 主动技能补充 `requiredOptionalCostChoiceCount: 1`，同时保留现有 `additionalCostChoices` / `requiredAdditionalCostChoiceCount`。前端可据此强制且只能选择一张废牌。
- [x] **P1-5 处理旧 PR**。#272 已合入，六片完整规则、`L2 required` 与验收驱动检查全部通过；#231 的替代交付继续在 P1-1 跟踪。
  - #158（S3–S6 严格重连验收）：已被后续改动覆盖，复核后保持关闭。
  - #194（把 S7 接入夜间验收）：旧 PR 已关闭；仍适用的施加移动费用已由 #272 接入默认付款重连夜间验收，覆盖真实 SQL、进程重启、恢复与回放。
  - 已复核 #158：保持关闭；严格 Joined 令牌、SQL 不变基线、付款重连和协调伪造负控已在 main，相关共享助手与测试逐字一致。网络消费者的后续差异来自额外减费与受限法力适配。

- [ ] **P1-7 RAD 单位响应边界（`c/rad-unit-response-boundaries`，1 项规则修复及 4 类回归，待定向验证）**。来源在响应期间复制为另一张卡面时，恢复和结算现按激活时捕获的卡面修订核对，复用 `CopyFaceTimeline.FaceAt`；覆盖实体／复制指示物续接和负数、过早、未来修订伪造拒绝。另补易位法师四类响应回归：来源易主仍由原玩家选择打出；目标易主后失效、重新成为友方后恢复合法；目标离场再入场不得复用旧代际；目标只在场地间移动则保留代际。四类回归用于补齐边界覆盖，并非四项新发现的生产缺陷。依据：核心 191.5.b、355.9.c、359.3.e.3–4、377.3、446.1、479.1.b。
  - 2026-11-01 本地验证累计 596.3 秒：Release 构建成功，570.1 秒上限终止了刚启动的类级测试；随后用剩余 26.2 秒运行卡面修订五条用例，三条负控通过，两条正例暴露通用 Driver 要求日志从 tick 0 开始的夹具限制。已改用组件序列化恢复、会话重复提交及从响应快照开始的后续日志重放；修正后的正例和新增六条响应回归尚未编译执行，下一轮先定向验证该类。#364、#370 均占用 L2 名额，本批暂只推分支，不开第三个 PR。

## P2 · 卡牌（B 线）

- [x] **P2-1 完成 #205**：VEN078 / 089 / 129 的相关工作已在 main，VEN117 / 159 的真实付款与战斗测试由 #237 合入；#205 已随合并自动关闭。
- [x] **P2-2 合入分支 `codex/cards-ven010-paid-current-grave-20261028`**：VEN010、013、093、103、122 的付款、墓地计数和反应测试由 #237 合入。
- [ ] **P2-3 VEN 卡牌**，见 [卡牌清单](CARD_BACKLOG.md)：
  - [x] D 线普通单位、法术、装备与战场已完成：[PR #265](https://github.com/DDDHL0704/riftbound/pull/265) 的 30 张法术／移动牌及 [PR #257](https://github.com/DDDHL0704/riftbound/pull/257) 的余下 51 张普通牌（28 名单位、14 件装备、9 处战场）均已合入。影分身复用 B 线 #262 的指示物实现；CARD_BACKLOG 的 VEN 四类已清空。
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

  - D 线 `d/rad-next`（#287）：收拢 `d/rad-basic-effects`、`d/rad-entry-static`、`d/rad-cost-targets`、`d/rad-equipment-ward`、`d/rad-static-power`、`d/rad-activated`、`d/rad-combat-spells`，共 33 项普通牌效果（15 张单位、16 张法术、2 件装备）；同步含 #283、#292、#294 的 main，按新文件位置保留双方实现。整批使用 Release 构建并按类定向回归，本地全绿后一次推送，合入后 RAD 剩余 122 项。
  - 其中狩猎犬、不速之客、清籁穿云和气敛神聚共用绝念造单位、休眠单位计数和全玩家移动禁令；缄默术、随手投掷共用牌面符能费用下限筛选与装备法力费用伤害，覆盖非法目标信息不产生伤害加成（359.3.e.12）。
  - 警觉的收藏家共用装备关键词光环和法盾询价贡献集合，按当前控制者生效，真实复制文本独立叠加（356.2.a.2、809.1–809.2）；坚定游骑按同一战场其他友方单位和装备总数检查战力门槛，超过门槛不重复加成，查询、投影和复制文本共用参与者集合。
  - D 线 `d/rad-combat-spells`：雪崩、为了德玛西亚！、冥火销魂接入基地范围伤害、选定战场友方临时关键词及放逐后燃烧；提示、询价与结算共用单位／装备范围和战力上限判定，覆盖响应、法盾、无效化、回响、燃尽及恢复回放。已与主动技能批次一同收拢到 #287。
  - D 线 `d/rad-combat-spells` 继续接入震魂怒吼、破厄新生：强制横置友方单位共用付款计划、候选和询价校验；额外费用每次出牌只支付一次，无效化不退还，忽略全部费用的出牌复用原有豁免。已收拢到 #287 统一定向验证。
  - [x] C 线 `c/rad-unit-batch`（#355，待自动合入）：提灯亚龙、易位法师、幻彩飞禽、无情潜伏者四项主动技能及 4 个官方印次已接入；目录基线更新为 1365 个运行时印次，定向回归 23/23 通过。
  - 2026-11-01 分工调整：B 线负责 #287、#336、#338、#342 及 RAD 传奇、英雄、专属牌；C 线负责 P1 和其余 RAD 单位（含易位法师）；D 线负责 RAD 普通法术；E 线负责 RAD 普通装备和战场。不要再领取其他线负责的类型。
  - [x] [#293](https://github.com/DDDHL0704/riftbound/pull/293) 已合入：艾克·灵光一现与放逐之锋，共两项效果、三个 RAD 印次。返回友方单位的必付额外费用、装配费用二选一及百炼减费后的费用选择已接入（204.1.b、818.1.c、821.1.c）。
  - **共享机制矩阵（A 线，待逐项实现）**：已将当前 `CARD_BACKLOG` 的 RAD 原文与引擎现有解析／结算能力交叉核对。下列条目是主干尚无独立共享模型、或仍缺少完整恢复与隐私覆盖的机制；后续卡牌 PR 必须复用对应共享流程，不能为单张卡旁路实现。编号按中国区官方快照，异画以同一组编号合并。
    - 炸弹装备指示物的生成、同处连锁摧毁、回合结束重建及来源绑定：`RAD·001`、`023/023a`、`094`、`096`、`112/112a`、`141`、`142`、`169*/169`。
      - [x] [#319](https://github.com/DDDHL0704/riftbound/pull/319) 已合入：部署的非单位装备免于未贴附召回（核心 323.6、830.1.b.3），实际清理与待清理任务共用判断；本地 Release 54/54 通过。完整 [41109837298](https://github.com/DDDHL0704/riftbound/actions/runs/41109837298) 的九片规则、9,794 个方法、20,414 条用例和门槛全部通过；实际 Counter 与固定计划一致，较前次全绿 main 仅新增 11 条部署清理回归，零移除。炸弹生成、同处连锁及打出落点继续另行实现。
      - [x] [#320](https://github.com/DDDHL0704/riftbound/pull/320) 已合入：按核心 830.1.b、830.2 捕获对手据守时的部署自毁技能，复用现有响应窗口与摧毁流程，保留原控制者和来源代际；覆盖真实反应、有目标反制的拒绝、移动、控制权／关键词变化、返手再进场、恢复反伪造及隐藏来源投影。Release 原有据守 34/34、新部署 26/26 定向通过；完整 [41118008709](https://github.com/DDDHL0704/riftbound/actions/runs/41118008709) 九片规则与 `L2 required` 通过，9,799 个方法、20,440 条实际用例与固定计划 Counter 逐条一致，较 #319 零移除、仅新增 26 条部署据守回归。投影在 `BuildStackItemSnapshotView`、`OrderTriggerSourceObjectIdForViewer`、两个隐藏来源判断方法及 `CapturedBattleSourceIsPrivate` 两个重载追加部署分支；这些方法也见旧 #275，先合入者优先，后续 C 线同步 main 时保留双方分支。
      - [x] [#344](https://github.com/DDDHL0704/riftbound/pull/344) 已抽出共享的战场装备指示物批量创建入口，统一指示物身份、控制者、落点、静态进场替换、事件捕获和对象编号；炸弹工厂判定也统一复用 `P6TokenFactoryCatalog`。回合结束重建与各张 RAD 卡的接入仍待后续卡牌 PR。
      - [x] [#345](https://github.com/DDDHL0704/riftbound/pull/345) 已合入：共享炸弹重建机制在回合结束捕获带来源代际的炸弹数量，控制者在响应窗口后选择一处公开战场，再复用 #344 的批量战场装备指示物入口一次创建全部炸弹；恢复与公共投影只暴露数量和公开战场选择，不暴露来源代际或创建堆栈凭据。本次补充回归断言，覆盖缺失 `UntilEndOfTurnEffects` 的旧恢复状态；不接入 RAD142 或任何单张 RAD 卡。
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

- [x] E 线亮出与打出选择批次（`e/rad-reveal-play`，#315）：龙犬驯兽师、冥顽构装体、苍穹腾蛟、暗巷守护者、祸心陌客。亮出在确认阶段选择并锁定信息，手牌展示、场上只挑选且排除背面待命；零符能亮出仍完成后续抽牌。龙犬展示全部对手后合计选一张免费打出，祸心在进场后确认战场与可选返手目标、结算时检查四单位门槛。亮出卡牌排除指示物，亮出机械允许机器人及机械复制指示物，并记录其后消失时的身份凭据。Release 零警告错误；初批回归 3763/3763、共享洞察／目标／恢复／待命 248/248；补充指示物边界与完整 ConformanceFixtureRunnerTests、目录／隐私／来源身份／Provenance 回归 3732/3732 通过，均含冷恢复与完整回放。九片规则与 L2 required 全部通过，#315 已合入；CARD_BACKLOG 五项已移除，指定 42 张累计完成 31 张。依据：核心 185、832.1.c.2 / 832.1.c.4 / 832.1.d.1、355.8；暗巷 FAQ、811.5.a、129.4。

- [x] E 线休眠与资源观察五张（#333）：翼之神谕者、鲁莽的发明家、能量聚合体、皮城向导机器人、细心的修复师已随主干合入；共享休眠、资源观察、来源代际、恢复与公共投影均已覆盖。

## P3 · 工程与提速（A 线，L2 提速最优先）

- [x] **P3-1 拆分巨型文件**：[#283](https://github.com/DDDHL0704/riftbound/pull/283) 已合入。按领域纯搬移为 68 个部分类文件，三个入口分别降至 6,102 / 5,668 / 1,723 行；55 个类型、4,243 个成员正文、16 个完整声明和各类型字段／属性初始化顺序均保持一致。源码守卫读取全部拆分文件。最新 main 定向 34/34、187 项 Python 检查通过；完整 [40958041215](https://github.com/DDDHL0704/riftbound/actions/runs/40958041215) 的 9,453 个方法、19,496 条用例、完整对局及门槛全部通过，零跳过，Counter 与 #281 一致。
- [ ] **P3-2 L2 提速**（新增 VEN 后重新打开，`a/l2-ven-opening-batch`）：
  - [x] [#337](https://github.com/DDDHL0704/riftbound/pull/337) 已合入（`94f3e1bcc`）：main 推送仅含明确文档时，或其 Git tree 与已通过 PR `Backend L2` 的测试 tree 完全一致时，跳过 9 个规则分片；scope/API/tree/receipt 任一环节不确定都回退完整套件，PR 代码仍保留完整 L2 与 `L2 required`。
  - [x] [#343](https://github.com/DDDHL0704/riftbound/pull/343) 已合入（`05be7124c`）：合并慢开局的检查点优化：`VenRunePrintingConsumerTests`、`Ven035DelayedEmpowerConsumerTests`、canonical burnout、三类 recast 费用开局及 LastBreath rune-call setup 共享完整历史恢复；响应窗口只在完成边界冷恢复，逐命令接受、重复提交、隐私、规则和最终原始日志回放断言保留。已测 VenRune 节省 5.99 分钟、Ven035 节省 12.31 分钟；完整 [41189973826](https://github.com/DDDHL0704/riftbound/actions/runs/41189973826) 九片及 `L2 required` 全部成功。
  - [x] [#346](https://github.com/DDDHL0704/riftbound/pull/346) 已合入（`635c15642`）：`BattleLimitedCleanupTests` 将无争议的战斗准备阶段、`Ven010PaidCurrentGraveConsumers` 的物理开局改为阶段冷恢复，保留每条命令的重复提交／视图校验、反应边界恢复和最终原始回放；本地 Release 定向回归分别为 2/2（3 分 32 秒）和 4/4（3 分 29 秒）。Backend L2 并发组改为自动运行在新 PR 或 main push 取代时取消旧运行，手动 `workflow_dispatch` 保持稳定以便调查；完整 [41199074067](https://github.com/DDDHL0704/riftbound/actions/runs/41199074067) 九片及 `L2 required` 全部成功。
  - `a/l2-next-battle-boundary`（本 PR，基于 `a17ae2085`）：继续合并两批阶段边界优化中的四个测试类，移除 `ColdCheckpoint()` 后已被后续边界覆盖的 `RecoverAfterEveryCommand = true`，保留实际战斗结果、替代选择、逐命令接受／重复提交、隐私、资源／代际和最终原始日志回放断言。只改测试夹具，不改引擎或共享登记文件。
  - `a/l2-empower-boundary`（[#360](https://github.com/DDDHL0704/riftbound/pull/360)，草稿）：`Ven057EmpoweredMovementConsumerTests` 与 `Ven110EmpoweredBanishConsumerTests` 将无争议的单位出牌、强化准备和回合转换集中到设置边界冷恢复；移动、强化、放逐目标、响应窗口、逐命令接受／重复提交、隐私和最终回放仍保留。只改测试夹具；并合并最新完整 L2 timing 权重刷新，作为同一提速 PR 验证。
  - `a/l2-boundary-batch-next`（待 PR，合并两批）：`BattleWinnerPhaseBoundaryTests`、`CopiedDestructionReplacementInstanceTests`、`AttachedSelfDestructionReplacementTests`、`BattleExpirySpellKillAttributionTests`、`BattleLimitedExpiryReplacementTests`、`BattleLimitedFirstRoleBoundaryTests`、`BattleLimitedNegativeMightTests`、`BattleLimitedCopiedRuleTests`、`LastBreathAreaReplacementOrderingTests`、`Ven117SubmittedStandbyConsumerTests`、`Ven159PhysicalBulwarkCombatConsumerTests` 和 `Ven159SubmittedBattlefieldConsumerTests` 的无争议开局、资源准备及目标建立改为阶段边界冷恢复；实际战斗结果、Guardian 替代选择、附着／代际边界、逐命令接受／重复提交、隐私、战斗与替代窗口、资源／代际和最终原始日志回放仍保留。只改测试夹具，不改引擎或共享登记文件。
  - [x] 包含 #327 的固定 main `d0c0487a` 已由 [41154516777](https://github.com/DDDHL0704/riftbound/actions/runs/41154516777) 完整验证；九片及 `L2 required` 全部成功，墙钟约 38 分 16 秒，最慢片约 32 分 36 秒。主干验证已恢复，30 分钟目标未达。
  - [x] [#321](https://github.com/DDDHL0704/riftbound/pull/321) 已合入（`685721050`）：将十个测试类中的十四处连续横置准备改为生产批量命令，保持原符文顺序、资源数量、关键费用／战斗／选择断言及原始历史恢复与回放；仅改测试夹具。固定 `7573837b4` 的本地 Release 受影响测试与固定守卫 36 类、1,465/1,465 用例通过，零跳过；完整 [41123453797](https://github.com/DDDHL0704/riftbound/actions/runs/41123453797) 九片及 `L2 required` 全部成功。
  - `a/l2-runtime-profile`：复核新增 VEN 机制后的主干 [41013909621](https://github.com/DDDHL0704/riftbound/actions/runs/41013909621)，原有慢方法耗时中位数增加约 38%，testhost 分配量增加约 40%；九片完全均衡的执行下界也约 30.2 分钟，需继续优化测试夹具或执行方式，不能只靠重平衡。本轮把共享测试驱动消费者中的 45 处连续单符文横置准备改为生产批量命令，保留相同符文顺序、资源总量、逐批实际事件和完整恢复／去重／回放断言；不改引擎。完整规则清单与唯一门槛保留。
  - [#310](https://github.com/DDDHL0704/riftbound/pull/310) 已合入：将 `VenRunePrintingConsumerTests` 中真实开局的三张符文逐枚横置／回收和重复失败提交改为批量命令，保留每张印次身份、代际、区域、法力／符能、符文顺序、RNG 游标、非法外部符文、过期命令、自然重入和冷恢复断言；不改引擎。完整 [41063997240](https://github.com/DDDHL0704/riftbound/actions/runs/41063997240) 及包含该优化的主干 [41078351418](https://github.com/DDDHL0704/riftbound/actions/runs/41078351418) 的九片规则与 `L2 required` 全部通过；后者分片约 27.4–31.1 分钟，仍高于 30 分钟整次目标。
  - [#323](https://github.com/DDDHL0704/riftbound/pull/323) 已合入（`cd545d595`）：将九个测试夹具中连续的符文横置／回收准备改为生产批量命令，不改引擎；本地 Release 定向回归 24/24 通过，PR L2 [41137679904](https://github.com/DDDHL0704/riftbound/actions/runs/41137679904) 九片及 `L2 required` 全部通过。
  - [#327](https://github.com/DDDHL0704/riftbound/pull/327) 已合入（`d0c0487a`）：将 VEN013/103 合法开局的提交牌组、就绪和调度起手阶段冷恢复合并为阶段末一次，保留支付、结算、回放及关键恢复检查；PR L2 [41151087570](https://github.com/DDDHL0704/riftbound/actions/runs/41151087570) 九片与 `L2 required` 全部通过，分片用时约 25.4–33.4 分钟。
  - [x] [#330](https://github.com/DDDHL0704/riftbound/pull/330) 已合入（`fd33bd493`）：继续压缩 VEN013/103 合法开局 fixture 的冷恢复成本，仅在支付、移动、结算边界和最终 journal replay 做恢复；不改引擎或 #287 共享文件。两个合法开局 actor 用例的本地 Release 定向回归 2/2 通过，串行用时约 7 分 35 秒；固定 main L2 [41159049774](https://github.com/DDDHL0704/riftbound/actions/runs/41159049774) 中对应的 P1/P2 用例分别用时约 12 分 23 秒和 10 分 52 秒，合计约 23 分 16 秒。随后在同一 PR 中继续合并 VEN013/103 三个等待阶段的恢复，完整 PR L2 [41165082786](https://github.com/DDDHL0704/riftbound/actions/runs/41165082786) 九片及 `L2 required` 全部成功；当前改动只涉及测试夹具，未改引擎或 #287 共享文件。
  - [x] [#347](https://github.com/DDDHL0704/riftbound/pull/347) 已合入（`824ff4ce`）：将 `MandatoryRecastHeldTaxOpeningTests` 的两个回合转换阶段从每个自动命令一次完整冷恢复改为阶段边界一次；每条命令仍执行接受、重复提交和隐私断言，支付／税费窗口及最终原始日志回放仍逐边界冷恢复。只改测试夹具，不改引擎或 #287 共享文件；完整 PR L2 [41203889195](https://github.com/DDDHL0704/riftbound/actions/runs/41203889195) 九片及 `L2 required` 全部成功。
  - [x] [#348](https://github.com/DDDHL0704/riftbound/pull/348) 已合入（`467f3abc4`）：`CanonicalDistributedGuardianOpeningTests` 的自然持有点／牌库耗尽开局保留每个命令的状态断言及关键战斗、分配、替代和最终回放恢复，只移除两个被后续阶段覆盖的中间全历史冷恢复；完整 [41208611862](https://github.com/DDDHL0704/riftbound/actions/runs/41208611862) 九片及 `L2 required` 全部成功。
  - [x] [#353](https://github.com/DDDHL0704/riftbound/pull/353) 已合入（`b7a6d5b9e`）：在 `CanonicalDelayedAuraDeathOpeningTests` 与 `CanonicalAreaGuardianOpeningTests` 中移除自然抽牌阶段被后续已接受结算边界覆盖的重复全历史冷恢复；完整 [41215987946](https://github.com/DDDHL0704/riftbound/actions/runs/41215987946) 九片及 `L2 required` 全部成功。
  - [x] [#354](https://github.com/DDDHL0704/riftbound/pull/354) 已合入（`617d90be3`）：`Ven122PhysicalWardSpellConsumers`、`Ven001ConditionalWardConsumerTests`、`Ven050PhysicalConditionalWardConsumerTests`、`Ven117PaidConditionalSteadfastConsumerTests` 与 `Ven129PhysicalCombatConsumerTests` 集中到边界冷恢复，并保留逐命令接受、隐私、重复提交、资源、代际、回放及最终状态断言；完整 [41238654251](https://github.com/DDDHL0704/riftbound/actions/runs/41238654251) 九片及 `L2 required` 全部成功。
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
- [x] **P3-5 卡牌登记冲突热点**（`a/registration-hotspots`）：[#359](https://github.com/DDDHL0704/riftbound/pull/359) 记录统计结论；#287 已于 2026-10-31 合入；数据见 [`docs/registration-hotspots.csv`](registration-hotspots.csv)，覆盖最近 30 个 `feat(cards)` PR（含已关闭的 #241/#284）。`scripts/registration-hotspots.py` 按每个批次的文件命中标记推导计数，不能由手工汇总替代。当前数据推导为：`CardBehaviorRegistry.cs` 30/30、逻辑 `CoreRuleEngine` 30/30、`RuleTextParsers.cs` 27/30、`VendettaCardSources.cs` 19/30、`RadiantCardSources.cs` 11/30、`PrintedCardFactory.cs` 15/30、`CardCatalogBaselineTests.cs` 3/30；`ReviewedRuntimeCatalogAdmissionTests.cs` 为 29/30（#257 未修改该文件），与旧记录的 30/30 不一致。登记脚本及现有 `scripts/tests` 共 209 项测试已通过；30 条账本的 GitHub 分支、标题和状态元数据逐条一致；#287 的 61 个变更文件与账本逐项一致。当前账本状态为 28 个 merged、2 个 closed，脚本测试 4/4 通过。

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
