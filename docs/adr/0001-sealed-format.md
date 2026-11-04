# ADR 0001：现开赛协议、卡包与多局比赛

2026-11-02，已接受，按 BACKLOG 分步接入；2026-11-03 增补锁定配置的本人投影。最高依据为中国区[赛事规则（10.16 生效）](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/0d5c26b5535345d48e1afafb3e2a0018.pdf)：403–407、602.1、602.3、602.4.a；主牌包含选定英雄依核心 103.3。

## 前端契约

| 入口／字段 | 契约 |
| --- | --- |
| 建房 | `CreatePublicMatchWithOptions(playerId, { format = "constructed", setCode = null, bestOf = 1, packs = null })`；现开单系列继续使用 `format = "sealed"`、`setCode` 和 `bestOf = 1 / 3`；自定义玩法传有序 `packs: [{ setCode, count }]`，此时 `setCode` 必须为空，系列不可重复且须已开放，每项至少 1 包、合计 6–12 包；默认仍为官方 602.4.a.1 的单系列六包。保留单参数 `CreatePublicMatch(playerId)` 作为默认构筑入口（SignalR 不支持通过 C# 可选参数省略线上的参数）；匹配队列只开放构筑。格式／系列会规范化，未开放系列、非法组合和非法局制在建房及发布目录前返回中文错误；构筑 `bestOf` 只能为 1。大厅列表保留同一房间格式、系列、局制及有序组合。 |
| 系列 | `GET /sealed/sets` → `[{ setCode, name }]`；唯一开放名单为 `data/official/sealed-sets.json` 的 `enabled`；OGN、SFD、UNL、VEN、RAD 均开放。UNL 同时接受官方目录的 `UNL-` 编号和历史指示物的 `UNL·` 编号；VEN 的 `SP1–SP6` 是进阶包特殊异画目标。RAD 进阶包排除随附英雄包 `SP1–SP5`；特殊异画、终极实体编号仍待官方完整目录确认，因此对应概率暂为零。 |
| 房间／快照 | `PublicMatchDto`、`SnapshotDto` 带 `format`、`setCode`、`bestOf`；自定义组合还带公开的有序 `sealedPacks: [{ setCode, count }]`；`setCode` 非现开为空，自定义组合也为空。 |
| 私有卡池 | 快照顶层 `sealedPool: { setCode, packs: [{ setCode, cards: [{ cardNo, slot, foil }] }], pool: [{ cardNo, count }] }`；只向本人返回，对手和观战者为空（序列化可省略空值）。自定义组合的 `sealedPool.setCode` 为空，每包的 `setCode` 标明系列；每人按 `sealedPacks` 顺序依次开包，各包使用对应系列的槽位／概率配置和按包序号派生的私有种子。`slot` 为 `common / uncommon / rare_or_better / foil / rune_or_token`；稀有槽、闪卡槽及符文／指示物槽 `foil = true`。 |
| 提交 | 继续 `SUBMIT_DECK { legendCardNo, championCardNo, mainDeck, runeDeck, battlefields }`；现开传奇／英雄可为 `null` 或空字符串。单局制提交 3 张战场，三局两胜每局提交 1 张。空白战场固定保留编号 `SEALED·BLANK-BATTLEFIELD`，只在现开解释。拒绝返回逐条中文错误。 |
| 多局 | `series: { bestOf, gameNumber, wins: { 玩家: 数 }, phase, usedBattlefields: { 玩家: [cardNo…] }, playOrderChooserPlayerId }`；`gameNumber` 从 1 起；phase 为 `choosing_play_order / submitting_decks / playing / finished`；已用战场保留重复编号以表达实体数量，本人全部可见，对手仅包含已公开记录，观战者仅包含公开记录。当前实现只在双方准备后同时记入初始战场，两人暗交和单方准备期间不暴露选牌；平局复用同一实体，不在此数组重复扣量。 |
| 锁定配置 | `series.lockedDeck: { legendCardNo, championCardNo, mainDeck, runeDeck, battlefields } \| null`；仅在本人下一局配置被锁定且仍处于 `choosing_play_order / submitting_decks` 时非空。只含查看者自己的配置，不以玩家编号索引；对手不能收到本人的配置，观战快照及公开回放始终为空；空值可省略。平局后返回上一局完整提交（单局制保留 3 张战场），客户端应允许按此配置重交，不能因 `usedBattlefields` 禁用被强制复用的战场。首局完全跳过后，已登记者返回初始配置；尚未登记者为空，首次合法提交后即返回该配置。跳过局不消耗战场，后续已合法重选的战场随本人的最新提交返回。缺席角色沿用提交契约的空字符串；普通胜负后的自由换备牌、首局、已开局和整场结束均为空。配置来自权威持久化帧，冷恢复与令牌重连后仍返回，换设备不依赖本地缓存。 |
| 先后手 | 仅 `series.phase = "choosing_play_order"` 时向 `playOrderChooserPlayerId` 提供启用的候选 `CHOOSE_PLAY_ORDER`，另一方只有等待；通过既有 `SubmitIntent` 提交 `{ cmdType: "CHOOSE_PLAY_ORDER", goFirst: true / false }`，表示选择者本人先手／后手。`goFirst` 必须显式为 JSON 布尔值；支持既有 `promptId`／`snapshotTick`。成功后进入 `submitting_decks`，清空 `playOrderChooserPlayerId`；首局双方入座前 `series` 为空，提示等待入座。 |
| 备牌 | 快照顶层 `sideboardCounts: { 玩家: 数 }` 向双方和观战公开数量（403.6）；牌面、未提交配置和开包结果继续私有。备牌由服务器用可用卡池减去本局配置计算，选定英雄已包含在主牌堆中，不重复扣除；外部基础符文、空白战场及展示用符文／指示物槽不增加卡池。客户端无需另交备牌清单。首次提交前不返回该玩家的数量；局间未重新提交的玩家沿用上一局配置对应的数量，提交后更新自己的数量。 |

字段变动须同步本 ADR 和 DTO；实际投影、命令可空支持与多局流程在对应实施步骤接入。

私有卡池的入座事务、种子派生、本人投影、卡池校验提交、可空角色／空白战场开局和空房间回放见 ADR 0006；公开现开建房已连接该流程。

比赛层房间接入见 ADR 0005。现开 `PlayerMatchDto.players[].score` 使用胜局数（如 `2:1`）；构筑继续使用该局游戏内分数。单局结束不写公开战绩，只有达到比赛所需胜局时写一条。现开建房及局间约束已具备前端联调入口，完整打牌终局与 L2 验收仍按 BACKLOG 跟踪。

## 规则和流程

每人默认 6 包同系列（602.4.a.1）；自定义多系列组合是非官方玩法，按所选组合顺序开 6–12 包。主牌至少 25 张且英雄计入，符文恰好 12 张、只用特性内基础符文，来源不限。实际用到的特性并集不超过 3 种并包含传奇全部特性；专属法术的全部特性均须符合。传奇／英雄可缺席且不要求同英雄标签；缺任一者每局本人首回合开始阶段仅额外抽一张。现开豁免同名、专属数量和“唯我”，构筑禁卡不自动套用（602.1.b、602.4.a.2–6）。卡池按真实印次计数，除基础符文及空白战场外须由服务端校验持有数量（602.1.a.1–3）。

单局制随机从 3 张战场抽 1 张，不能换备牌（602.3.e.3、602.3.g）。三局两胜先获 2 胜结束，平局不计胜局（404.1–3）；首局随机指定玩家选先后手，此后败方选，平局沿用顺序（407.1–4）。局间顺序为败方选先后手 → 双方暗交配置／战场 → 同时公开战场 → 核心 110；该公开顺序参照 406.1.h，列待确认。平局后禁止换备牌且沿用同一战场（403.10、406.1.b）；首局完全跳过禁止换牌（403.9），判负依 406.1.c。其余局间允许调整主牌、备牌、符文特性、传奇和英雄，仍须合法（602.4.a.7）。每张实体战场每轮仅用一次，空白不限（602.3.e.4、602.3.f）。

已接入的战场账本保存每局开局选定的真实印次，不从终局战场对象反推，以免卡牌效果改变场上战场后丢失原实体。三局两胜用卡池数量减去此前非复用的选牌次数判断是否仍有副本；空白不限。平局下一局必须保留传奇、选定英雄及各牌堆的印次数量（数组顺序不影响配置相等），并强制复用原先实际选中的战场；单局制继续提交原三张配置，但不再随机抽选。账本与局间配置随权威帧持久化，并经原始动作重放核对。

判负只提供进程内可信应用服务 `MatchTournamentAdjudicationService.ApplyGameLossAsync(match, decisionId, { gameNumber, loserPlayerId })`，通过依赖注入获得；底层 `IMatchTournamentAdjudicator` 负责权威状态转换，供服务和原始动作重放使用。没有 SignalR／HTTP 玩家入口或候选行动；实际管理端鉴权和操作界面不在此契约。决定仅记录为私有日志的类型化 `TOURNAMENT_GAME_LOSS` 输入，不能由原始玩家命令解码。`decisionId` 在整个房间唯一，重复幂等、冲突拒绝，日志提交失败不安装结果；恢复和动作重放走同一状态入口。公开事件只含局号、判负玩家和是否跳过，没有卡组、卡池或自由文本裁定理由。

裁定服务和玩家 Hub 的现开命令共用 `MatchResultRecords.RecordFinishedSeriesAsync`，仅在整场结束后写入一条含小分战绩；生成字段共用 `FromFinishedState`。权威日志先提交，战绩库故障不撤销已提交结果；使用相同裁定编号或玩家命令编号重试可补写，JSON 帧恢复后没有原始事件的幂等回执也能补写。已存在且与最终结果一致的战绩不再改写，保留原结束时间；冲突战绩按恢复不一致报错。局中结果、拒绝和未提交日志均不写战绩。构筑 Hub 保留既有按终局事件写入的流程。

首局在双方准备／战场公开前被判负，记作完全跳过；不消耗未展示战场，下一局不得更换传奇、选定英雄、主牌和符文配置（403.9），战场仍按本局开始流程选择。低级别赛事不要求初始登记（602.3.f），因此尚未提交初始配置的玩家可首次建组；其首次合法提交立即成为不能更换的初始配置。若在战场同时公开后判负，即使尚未调度或行动，也按已开局记录消耗原实体，后续须换其他副本（406.1.c、602.3.e.4.a）。对外 `series` 的 phase 和先后手命令不变，跳过局计入胜局及 `gameNumber`；仅达到比赛胜局要求才结束比赛和产生最终战绩。

多局模型共享，构筑入口暂不开放。比赛结束才记录一条含小分战绩；每局回放、恢复帧、局间提交和重连状态必须持久化。线上按低级别赛事，不做时限或 408 超时流程；602.4.a.8.k 打包时限仅建议。

## 卡包数据和来源等级

每包进阶包 14 张：7 普通、3 不凡、2 稀有或更高、1 闪卡、1 符文／指示物；移除最后一槽后 13 张（602.4.b.2.a）。按进阶包产品成员筛选（排除预组专有）；UNL 兼容 `UNL-` 与 `UNL·` 目录编号，RAD 因目录产品字段为空按 `RAD·` 前缀筛选并排除随附英雄包 `SP1–SP5`。所有概率在配置，生成器没有系列常量；替换概率为非零时必须有对应实体卡号，无法核实的 UNL 终极及 RAD 特殊异画／终极概率置零并标“待确认”。

| 来源等级 | 依据与配置 |
| --- | --- |
| 官方 | [Origins](https://playriftbound.com/en-us/news/announcements/collectability-in-riftbound-origins/)：槽位、史诗约每 4 包出现、异画约每 24 包 2 张、超编约每 72 包 1 张、超编中签名约 1/10；[Riot 商城 SFD](https://merch.riotgames.com/en-us/product/riftbound-spiritforged-booster-display/)：相同槽位和每盒 6 张以上史诗。无社区值时回退 OGN。[中文辉耀群星系列概览](https://playriftbound.com/zh-cn/news/announcements/the-radiance-overview/)直接确认 RAD 的 14 张槽位、仅第二稀有槽可出异画等印次、终极每包约 0.025% 且最多 1 张，以及特殊异画与异画大致同频；0.025% 按整个印刷批次计算，单包、单盒、单箱均无保证。 |
| 官方转述 | [TurnZero](https://turnzerohq.com/riftbound/riftbound-booster-box)：UNL／VEN 槽位、UNL 终极低于 0.1%、VEN 特殊异画与异画同频。未核实印次编号的系列不开放。 |
| 社区 | [UNL](https://tcgtalk.com/guides/unleashed-riftbound-pull-rates-box-opening)、[VEN](https://tcgtalk.com/guides/vendetta-riftbound-pull-rates-box-opening)、[RAD](https://tcgtalk.com/guides/radiance-riftbound-pull-rates-box-opening)：史诗出包约 27%；异画 10%／9%／9%；超编 1.7%／2%／1.2%；传奇出包约 50%／50%／31%；RAD 异画符文约 3.5%。RAD／VEN 特殊异画的绝对数值 9% 沿用本系列社区异画观测值，配置标社区，并分别将同频关系标为官方／官方转述依据。社区值全部待确认，仅影响概率。 |
| 第三方产品分类 | [营地](https://www.iyingdi.com/tz/post/5608629)：中国区标准包 5 张、进阶包 14 张；不替代官方规则。 |
| 项目假设 | 闪卡槽稀有 8%、史诗 2%，互斥；同包同功能牌不重复（含闪卡／异画）；未知异画符文率先为 0；UNL 终极暂取 0.05%。史诗按 `1-sqrt(1-每包概率)` 预计算独立双稀有槽概率，允许双史诗；社区传奇率按相同方法换算并条件于非史诗槽（印次替换会改变实际传奇率）。每包最多升级一个稀有槽，印次类型互斥，OGN／SFD／UNL 随机选择一个稀有槽替换。真实相关性未知，均列待确认。 |

真实印次保留到 `pool`；异画／超编规则与平卡相同，开放系列的所有候选印次都有引擎加载和实现状态守卫。符文／指示物槽供开包展示，完全不计卡池数量，基础符文本来不限。确定性使用 SHA-256 计数器，候选按 cardNo 排序；结果携带私有种子、配置版本、配置 SHA-256 和生成器版本。房间接入时将完整结果写日志／恢复帧，禁止任何私有种子或卡池进入对手与观战者投影、提示或公开日志。

全部页面原始快照和等级在 `data/official/upstream/2026-11-02/sealed-products/manifest.json`。原始页面以确定性的 `*-raw.html.gz` 无损保存；清单的 `sha256`／`bytes` 对应解压原文，`archiveSha256`／`archiveBytes` 对应压缩文件。2026-11-02 复核[官方辉耀群星售前公告](https://playriftbound.com/zh-cn/news/announcements/preparing-for-the-radiance-pre-rift/)与上述中文系列概览的正文、产品配图：只确认 5 包加英雄卡包（1 传奇、1 稀有英雄、1 战场、辅助卡牌），以及九位传奇主题；没有逐包完整卡表或辅助卡数量。售前保持“待确认”，取得官方卡表后再接入；限定黑默丁格不能入现开卡组，不从主题、配图或目录猜辅助卡。
