# 项目审查与已确认整改

审查基线：5965604252cfae1807f814384f8b449538f2adba，2026-10-12。范围覆盖项目入口、最近提交热点、共享规则、契约与客户端、持久化/恢复、测试/CI、官方索引和文档治理。整仓路径与调用审查不等于每一行代码或每张卡的语义认证；本报告不替代后续官方逐项重审。

使用 grilling、improve-codebase-architecture、setup-matt-pocock-skills 和 triage。用户已选择先实施验证反馈、证据治理和已证实废弃资料清理；共享战斗、模板规划及客户端契约随后逐项决定。未创建新的 Goal。

## 已确认事实

审查前 Git 跟踪 9,173 条路径，其中 6,735 个文档，约 133.15 MB；4,293 个根级 CURRENT 文件约 51.48 MB。无入站引用、日期或文件长度只用于定位，不作为删除证明。没有发现字节完全相同的代码文件；旧模板规划链有无运行用途的问题，另见候选。

最近 30 个提交中 MatchRecovery、MatchSession 和 CoreRuleEngine 分别出现 26、25、25 次。理解战斗延续需要同时阅读规则生成、提示派生、状态恢复和投影；这是本次架构审查重点。

| 范围 | 已确认问题或现状 | 本轮结果 |
|---|---|---|
| 入口与治理 | README 来源/测试停在 10/05，START_HERE 指向旧项目；多份累计调度互相覆盖 | 新状态、治理、入口和 AGENTS 统一 |
| 全量证据 | 注册状态可推导 Implemented/RepresentativeRulePass；不能证明执行或全卡语义 | 明确撤销默认继承，新增只读完整性检查 |
| 共享战斗 | 两处损伤分配账本读取重复，身份推导分散 | 保留生产及安全断言，后续处理 |
| 客户端契约 | C# 新命令字段与 DevUi 联合类型有缺口，原生/DevUi 各自构建原始 JSON | 后续处理，不改页面 |
| 模板规划 | BehaviorTemplateExecutor/PrimitiveExecutor 无已发现生产调用，只有规划自测 | 保留，删除前核对自测与真实行为 |
| 持久化/恢复 | 写日志有事务，恢复由多个查询组成；单个 registrySemaphore 跨恢复/回放持有 | 读一致性风险待复现，等待耗时未测 |
| 原生/数据库 | 部分 Postgres smoke 无连接会提前 return；打包成功不能证明实机 | 单独保留未验收状态 |

当前 Git 历史没有找到 9 月提交，不能据此否定用户给出的旧开发背景。已落实“9 月及更早默认未重新认证”；日期不能证明错误，不能批量删除实现。

## 验证效率

[基线 CI 38199241988](https://github.com/DDDHL0704/riftbound/actions/runs/38199241988) 完整执行 10,201 用例/6,625 方法，保留前一基线全部 10,183 用例/6,616 方法，11 牌组的 110 双向配对与种子 424242 完整。长期摘要见 [manifest](evidence/project-governance-2026-10-12/ci-59656042.json)。

full 实际耗时 1,358.87 秒。普通规则片 346.02 秒，却在首批启动约 855.66 秒后才启动；最长矩阵片 1,262.53 秒，其余对局片约 856–913 秒。默认四进程先启动四个对局片，使最广泛的规则失败反馈晚到。旧耗时权重来自不同环境，只适合调度估计。

已实施：普通规则首批启动，同时尽早启动最长对局；方法归属、全部 Theory 行与 110 配对不变。减少重复的本地完整验证，按 focus → 连贯批次一次 check → 固定提交 CI full 交付；CI 可与下一批开发并行，本地并行须独立固定 checkout，不能修改正在验证的源文件/bin。

已实施：明确治理文档的精确路径白名单走轻量检查。docs/DEPLOYMENT.md 被测试直接读取；docs/evidence、官方数据、fixture 和打包内 README 不在此豁免。未知路径、类型/执行位变化、重命名任一端不在名单、缺基线或不确定差异均默认 full。判定任务失败时完整任务仍执行。

完整任务按提交 SHA 使用各自并发组，不同代码候选互不取消；同一 SHA 的重复任务才可取消重复。纯文档提交不会取消此前代码验证。轻量结果明确标注规则/打包未执行，不能称为完整绿色。完整候选仍须 full 与原生验收。既有 rules/desktop 检查名称保留；未修改或声称验证 GitHub branch protection 配置。

固定时长调度模型显示普通反馈可从约 1,202 秒提前到约 346 秒；这是模型，不能声称完整 CI 或游戏执行实际加速。新调度的实际时间与覆盖须以本轮远端 TRX 读回为准。

## 证据治理

当前 ledger 1,711 个卡牌身份、14,293 个规则/示例/QA，全部 NOT_REVIEWED；中文 16 份/8,345 个文本块，加全球 20 份。31 条限定决策存在两种字段形状，十条没有统一 ruleRecords/validation/exclusions，不能直接计算完成率。

新检查同时核对原始卡池、逐印次来源哈希、CN 页/文本引用、全球文本块、文档/规则完整集合、摘要数量、精确决策引用和证据路径。所有决策与待处理引用合计 733 个精确引用、123 个印次身份；这只是引用覆盖，包含待处理项，不是已通过范围。

EN 返回 1,381、报告 1,389，8 项缺口仍未解决。10/16 未来生效资料保留未来标记，不能混为当前适用版本。缺真实 PostgreSQL、可见双客户端和 Windows 实机证据；本轮没有执行游戏页面视觉审计。

脚本报告治理完整性与 semanticCoverageCertified=false。它不刷新官网、不执行游戏、不验证所有图片/表格或远端日志，也不把文件存在当作测试通过。旧全绿、注册、fixture 与 Java 对照均不默认继承规则认证。

## 已实施清理

用户确认后删除三份被正式资源生命周期审计取代、无活跃引用的阶段交接/基线/清单，以及已完成且当前可迁移条目为零的一次性 printed-power 迁移脚本。对应正式审计、467 个 fixture 文件/471 条记录及迁移 manifest 保留。

五份累计调度正文改为固定 59656042 Git 历史入口，原文与哈希可追溯；回收 22,751,329 字节。四个删除文件原大小/哈希、五个替换文件原始与新哈希、保留范围见 [清单](evidence/project-governance-2026-10-12/cleanup.json)。历史写锁和旧暂停语句不再调度当前工作。

## 后续架构候选

架构 HTML 已在 OS 临时目录交付，本轮候选的决定保留如下。下列 interface 仅说明现有接入形状，没有制定新的核心 interface。

| 候选 | 强度 | module / interface / seam / adapter | locality 与 leverage |
|---|---|---|---|
| 验证反馈 | Strong，已选并实施 | 验证 module，CLI interface；Git/CI adapter 的输入 seam 明确 | 相同固定输入的发现、范围、执行证据集中，减少重复等待 |
| 证据治理 | Strong，已选并实施 | 来源索引 module；检查 interface；逐印次与条款 seam | 深度 depth 覆盖索引复杂性，限定结论不能越权 |
| 战斗延续 | Strong，后续 | 共享战斗 module；命令/恢复 interface；提示和投影 adapter | 集中真实身份推导，删除重复读取可提高 locality；独立安全期待必须保留 |
| 模板规划 | Worth exploring，后续 | 模板规划 module；规划 interface 没有生产 adapter | deletion test 指向删除候选，须保留实际卡牌行为并解释自测去向 |
| 命令契约 | Worth exploring，后续 | 命令 module；共享 interface；两个客户端 adapter 已形成实际 seam | 集中字段语义提高 leverage，以真实命令验证两端 |
| 恢复一致性 | Speculative，待复现 | 恢复 module；日志 interface；Postgres adapter | 事务/semaphore 深化须先有复现与等待测量，避免把复杂性搬走 |

## 任务和设计约定

已配置 GitHub Issues 与五个默认 triage 状态标签；配置时发现队列为空。后续问题按一类（bug/enhancement）与一个状态记录。已登记 [共享战斗 #1](https://github.com/DDDHL0704/riftbound/issues/1)、[模板规划 #2](https://github.com/DDDHL0704/riftbound/issues/2)、[命令契约 #3](https://github.com/DDDHL0704/riftbound/issues/3)、[数据库/恢复 #4](https://github.com/DDDHL0704/riftbound/issues/4) 和 [英文来源差异 #5](https://github.com/DDDHL0704/riftbound/issues/5)，均为 enhancement + needs-triage，等待后续逐项选择，不自动启动实施。

后续页面同时使用 frontend-design 与 Product Design；先取当前截图，再重新参考 Rift Atlas、Untap.in 和 Riftbound TTS。历史 Untap 材料没有完整对局截图，不足以得出当前视觉结论。本轮无游戏页面修改，偏好已写入项目约定和本地设计上下文。

验证输出和后续 CI 状态见 [当前状态](PROJECT_STATUS.md)。本报告完成的是准备工作，官方全量重审仍未完成，新 Goal 必须等待用户另行确认。

GitHub 实跑补充：文档提交 6e45d91f 的 scope 12 秒完成，完整代码运行仍在后台，来源/索引的十个输入哈希完全相同。但 job-level 跳过 desktop 会让矩阵不展开，只产生 desktop 检查名。已修正为保留两个平台检查名；明确文档范围时仅在 Ubuntu 写入“打包未执行”说明，原生下载/构建/打包全部跳过。文档组与 full 组隔离，包括同一 SHA 的手动 full；当前分支保护设置未检查，不能推断 required 配置。旧尝试不作为修正后的最终 CI 证据。
