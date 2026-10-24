# VEN159 均衡寺院：当前壁垒战力消费者

2026-10-24。整卡认证净增量 **0**，后端仍 **NOT READY**。登记数、局部语义及回放结果不替代适用来源和完整候选验收。

## 官方来源与边界

- CN `37862` / `VEN·159/166 SC`，均衡寺院，战场。2026-10-24T08:48:22.197777Z 从 [官方卡牌接口](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb) 读取：`此处拥有{{壁垒}}的单位获得{{S}}+1。` 未限定控制方。
- 完整记录按 UTF-8、排序键、紧凑分隔、保留中文计算 SHA256：`ab78868b436d5b49f4956e0b936f21bbf1b3689396e041c2a6760a5aaf02a6e5`。已查看[卡图](https://cdn.playloltcg.com/lol/card/20260716/d0d87c0899a44a1f8c9b196071fe5425.png)两侧文本；图片 SHA256 `2a85df0277bfde88480e8a870c40a86a7653cbf9f225a8cb91f2bb12b23188db`。QA 为 null、errata 为空。
- DOC01 已核读快照 `/Users/dinghaolin/MyProjects/riftbound/var/official/2026-10-11/01.pdf`，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`，物理第120页815.2–815.3：重复壁垒只生效一次，持有壁垒可供其他效果检查；伤害承担优先级不是持有状态。
- 另于08:58 UTC复核莉莉娅 `UNL-058/219` / `UNL-058a/219` 和吟风翼 `SFD·138/221`，文本、费用、战力、属性、图片地址及QA与既有快照相同。旧目录未保存errata字段、当前为空串，差别单独留存。
- 发布日期、生效日期、适用性和跨区印次映射仍为 UNKNOWN；不从CDN目录推断生效日期。

## 实现与依赖

显式基础为 [PR160](https://github.com/DDDHL0704/riftbound/pull/160) 的完整头 `94d0f0f917f673f4fc1ed7aadf0aef535abb536c`。消费该提交的 `CURRENT_KEYWORD:壁垒` 过滤接口；本包未再改 Engine、Contracts 或其 Driver。

本包生产变化限于精确来源记录、`StaticAuraParser.Parse` 的壁垒战场战力分支，以及 `OfficialRuleDomainBehaviorCatalog.IsImplementedBattlefieldRuleCard` 的战场域登记。此牌没有加入可打出牌的 CardBehaviorRegistry。重复获得壁垒仍只贡献一次+1；公开依赖、当前副本、关键字重算、战力和合法性继续走共享流程。

原1040条目录记录及顺序、原来源元数据保留；新增后1041印次、840功能组。重复组115、重复条目316、节省逻辑201不变。原Ahri历史1009条循环和断言保留，当前额外项31→32；原目录统计及战场域57→58断言保留为登记统计。

## 固定版本验证

验证源码：`aa5826b6da68402e5155f6e8d6a30869b48cf19d`；后续交付头只增加本文。编译 `-m:1`、测试单工作线程，0警告、0错误。

| 范围 | 实际结果 |
|---|---|
| 新消费者7方法 | 25/25，零跳过 |
| 相邻光环、当前关键字、目标上限、常驻目标、Flow、Ahri及目录统计 | 195/195，零跳过 |
| 原11套牌组、两种格式输入守卫 | 1/1；实际完整对局0 |
| 仅去掉新增解析分支的反向对照 | 28项：9个控制通过、19个预期失败；18项实际战力3而预期4，1项缺少光环元数据 |

新消费者包含双方单位、战场控制方/无控制方、区域和背面边界、重复关键字、后排/伤害免疫与持有状态的区别、当前附着副本及过期来源。副本的4个边界是有限组件帧，不声称实际副本命令出生。

8条普通命令流程从明确的中盘手牌和公开符文初态开始，覆盖P1/P2及固有、临时、莉莉娅两印次的持续壁垒。总110条命令（94接受、16拒绝）、270事件；每条接受命令冷恢复及重复提交、拒绝不变性、完整原动作/事件回放、8个完整JSON恢复帧和32个篡改校验均通过。实际战力4时，吟风翼≤3目标的预选及选择窗口均拒绝；壁垒过期或莉莉娅离场后战力回到3，可实际选取并结算返回。后一回合的费用由真实横置符文取得。未将中盘初态计作规范开局或完整对局。

反向对照 `6724c73b8f97239fb50ede9dd568b24f223b78fb` 与验证源码的唯一源文件差异是移除新增解析分支；保留相同消费者断言、当前关键字机制及目录登记。私有对照不是交付头祖先。

实际RIF：`83e05116641c78e1044fea9b12ccfe1bc04bf4928d2b8be34b16c0096438931c`。
实际加载Engine SHA256：`14dde9d3d8aa9c2386e4c1ee120b232df7b28fddab019289c7dd664c44a10efa`，MVID `cc078093-270c-4598-b331-7151eb596fbc`。2491个编译输入、1418个运行文件逐项哈希前后一致；所有原方法/已有Theory行仅同步登记统计，六个原未跟踪文件哈希不变。

## 可取回证据

本机工作树 `/Users/dinghaolin/MyProjects/.worktrees/riftbound-cards-20261023`：

- 当前来源、卡图及DOC01读图：`var/qa/cards-direct-pr-migration-20261024/ven159-current-source-v1/`。
- 本包根目录：`var/qa/cards-direct-pr-migration-20261024/ven159-main-port-v1/`。
- `main-source-test-receipt-v1.json`，SHA256 `ee51dff40e58d56480706c0855dafaac69fa44df43937f6acaa420e2625a647d`；`verify-final-v1.py`机械核对实际TRX用例集合、源码/运行身份、反向对照及原件保存。
- `compile-inputs-v7.json`、`runtime-manifest-v7.json`、`focus-v7/ven159.trx`、`adjacent-v7/adjacent-v7.trx`、`original-preconstructed-input-v7/`、`focus-v7/ven159-evidence/`、`parser-frontier-final/`。
- 原编译可空比较错误、误用战斗分配API的18项失败、JSON读取配置及资源初态失败、旧登记统计失败分别保留于v1–v5及对应readback，未删除断言或修改已接受历史。v5/v6重复绿灯不另计覆盖。

本机结果属于L1。合并仍须最新PR head/base/merge-result SHA的实际quality、targeted、rule_suite产物及 `L2 required` 全绿；当前包未完成整卡或L3认证。1740印次/14293条款来源范围保留，适用/功能分母仍 UNKNOWN。

L2失败后续：run40081383548实际受测合并源 `f1bb569894706b48780698fcb63d8d2883e992bf`，规则套件14028例中5项失败均为目录/内容身份/历史成员断言；原ZIP12557006167已按平台digest核对并保留。新源在main `29367aaaa1b154d2b7843f8b8645d3cc64133c84` 上组合更新后的完整PR160 `688f4c503dc36a3d78ba97a32423ed7021ea625d`，两个合并均无冲突，③PermanentPlayTargets文件与该main逐字相同。

受测源码 `d66e89a456d1f6efae644d5affeefd33792ae98d` 将本组合实际元数据校准为temp_might264/212、战场58印次/55功能组及55牌名、静态22/20、关键词12/11、模板35/35，并在原精确战场牌号集合末尾增加VEN159；Uncovered非PLAY_CARD战场分组为55。Lightning Rush沿用既有1036精确历史成员夹具，仍保留1036/1037与835/836及逐组序列化断言；内容身份为该1041目录的实际75bb47f5…621f3。统计不认证规则语义。

新固定源编译0警告0错误，289/289、零跳过；原220用例名多重集完整保留，原5个L2失败均在本次通过集合，另含main127的60个相邻可选费用/目标时机用例、8个元数据用例及原11套两赛制输入守卫1例。相对旧ready576db9c0，本包目录/两个解析生产文件字节未变；相对完整父PR160，生产/数据仍仅原三文件。消费者全部断言和数据行未改，只将已实际解码的完整恢复frame加入新日志字段；不把上轮f55af44d的289重复计入当前覆盖。

8份新日志共110命令（94接受/16拒绝）、270事件、8个完整实际解码JSON恢复帧及32个伪造拒绝，玩家/旁观视图、提示、接受重复、拒绝不变和原日志回放保留。2495源码输入、1418运行文件哈希稳定；Engine MVID `05af752c-1d8a-48aa-9566-c7fe5a5b72ff`，四个受查程序集InfoVersion均指向新源，实录中三运行模块SHA/MVID与manifest一致，MVID组成的implementation哈希及RIF `a2e29108a87df8e724ce79babcbc430cfa7fe55a962ec4d2518b48c037439b7a` 已独立重算吻合。

新原件位于 `var/qa/cards-direct-pr-migration-20261024/l2-failure-pr159-pr162-v1/pr166-current-v2/`，含固定源/bin、实际TRX、8份带完整frame的实录、module-identities.json及verify-final-v1.py；source-runtime-proof-v1.json SHA256 `d84ca851545f21e0e246c8af717a66a3af40082ab6d7dcf375bc55658c6f1efa`。原失败ZIP/inspection在父目录additional-runs-v1，旧反向对照只归原固定源；ready后续仅更新此审计doc。最新L2仍须实际验证，本机没有新full/SQL/网络/110场矩阵，净整卡认证增量0。
