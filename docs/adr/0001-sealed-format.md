# ADR 0001：现开赛协议、卡包与多局比赛

2026-11-02，已接受，按 BACKLOG 分步接入。最高依据为中国区[赛事规则（10.16 生效）](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/0d5c26b5535345d48e1afafb3e2a0018.pdf)：403–407、602.1、602.3、602.4.a；主牌包含选定英雄依核心 103.3。

## 前端契约

| 入口／字段 | 契约 |
| --- | --- |
| 建房 | `CreatePublicMatchWithOptions(playerId, { format = "constructed", setCode = null, bestOf = 1 })`；现开 `format = "sealed"`、指定系列、`bestOf = 1 / 3`；保留单参数 `CreatePublicMatch(playerId)` 作为默认构筑入口（SignalR 不支持通过 C# 可选参数省略线上的参数）；匹配队列只开放构筑。第一步落地签名和 DTO，现开建房在房间流程接入后开放，期间返回中文错误。构筑 `bestOf` 只能为 1。 |
| 系列 | `GET /sealed/sets` → `[{ setCode, name }]`；唯一开放名单为 `data/official/sealed-sets.json` 的 `enabled`；OGN、SFD 开放，UNL、VEN、RAD 未完整接入，不开放。 |
| 房间／快照 | `PublicMatchDto`、`SnapshotDto` 带 `format`、`setCode`、`bestOf`；`setCode` 非现开为空。 |
| 私有卡池 | 快照顶层 `sealedPool: { setCode, packs: [{ cards: [{ cardNo, slot, foil }] }], pool: [{ cardNo, count }] }`；只向本人返回，对手和观战者为空（序列化可省略空值）。`slot` 为 `common / uncommon / rare_or_better / foil / rune_or_token`；稀有槽、闪卡槽及符文／指示物槽 `foil = true`。 |
| 提交 | 继续 `SUBMIT_DECK { legendCardNo, championCardNo, mainDeck, runeDeck, battlefields }`；现开传奇／英雄可为 `null` 或空字符串。单局制提交 3 张战场，三局两胜每局提交 1 张。空白战场固定保留编号 `SEALED·BLANK-BATTLEFIELD`，只在现开解释。拒绝返回逐条中文错误。 |
| 多局 | `series: { bestOf, gameNumber, wins: { 玩家: 数 }, phase, usedBattlefields: { 玩家: [cardNo…] }, playOrderChooserPlayerId }`；`gameNumber` 从 1 起；phase 为 `choosing_play_order / submitting_decks / playing / finished`；已用战场保留重复编号以表达实体数量，本人全部可见，对手仅包含已公开记录，观战者仅包含公开记录。 |
| 备牌 | 快照顶层 `sideboardCounts: { 玩家: 数 }` 公开数量（403.6）；牌面、未提交配置和开包结果继续私有。备牌由服务器用卡池减去本局配置计算，客户端无需另交清单。 |

字段变动须同步本 ADR 和 DTO；实际投影、命令可空支持与多局流程在对应实施步骤接入。

## 规则和流程

每人 6 包同系列（602.4.a.1），主牌至少 25 张且英雄计入，符文恰好 12 张、只用特性内基础符文，来源不限。实际用到的特性并集不超过 3 种并包含传奇全部特性；专属法术的全部特性均须符合。传奇／英雄可缺席且不要求同英雄标签；缺任一者每局本人首回合开始阶段仅额外抽一张。现开豁免同名、专属数量和“唯我”，构筑禁卡不自动套用（602.1.b、602.4.a.2–6）。卡池按真实印次计数，除基础符文及空白战场外须由服务端校验持有数量（602.1.a.1–3）。

单局制随机从 3 张战场抽 1 张，不能换备牌（602.3.e.3、602.3.g）。三局两胜先获 2 胜结束，平局不计胜局（404.1–3）；首局随机指定玩家选先后手，此后败方选，平局沿用顺序（407.1–4）。局间顺序为败方选先后手 → 双方暗交配置／战场 → 同时公开战场 → 核心 110；该公开顺序参照 406.1.h，列待确认。平局后禁止换备牌且沿用同一战场（403.10、406.1.b）；首局完全跳过禁止换牌（403.9），判负依 406.1.c。其余局间允许调整主牌、备牌、符文特性、传奇和英雄，仍须合法（602.4.a.7）。每张实体战场每轮仅用一次，空白不限（602.3.e.4、602.3.f）。

多局模型共享，构筑入口暂不开放。比赛结束才记录一条含小分战绩；每局回放、恢复帧、局间提交和重连状态必须持久化。线上按低级别赛事，不做时限或 408 超时流程；602.4.a.8.k 打包时限仅建议。

## 卡包数据和来源等级

每包进阶包 14 张：7 普通、3 不凡、2 稀有或更高、1 闪卡、1 符文／指示物；移除最后一槽后 13 张（602.4.b.2.a）。按进阶包产品成员筛选（排除预组专有）；RAD 因目录产品字段为空按 `RAD·` 前缀筛选，开放前须补齐全部印次。所有概率在配置，生成器没有系列常量。

| 来源等级 | 依据与配置 |
| --- | --- |
| 官方 | [Origins](https://playriftbound.com/en-us/news/announcements/collectability-in-riftbound-origins/)：槽位、史诗约每 4 包出现、异画约每 24 包 2 张、超编约每 72 包 1 张、超编中签名约 1/10；[Riot 商城 SFD](https://merch.riotgames.com/en-us/product/riftbound-spiritforged-booster-display/)：相同槽位和每盒 6 张以上史诗。无社区值时回退 OGN。[中文辉耀群星系列概览](https://playriftbound.com/zh-cn/news/announcements/the-radiance-overview/)直接确认 RAD 的 14 张槽位、仅第二稀有槽可出异画等印次、终极每包约 0.025% 且最多 1 张，以及特殊异画与异画大致同频；0.025% 按整个印刷批次计算，单包、单盒、单箱均无保证。 |
| 官方转述 | [TurnZero](https://turnzerohq.com/riftbound/riftbound-booster-box)：UNL／VEN 槽位、UNL 终极低于 0.1%、VEN 特殊异画与异画同频。未核实印次编号的系列不开放。 |
| 社区 | [UNL](https://tcgtalk.com/guides/unleashed-riftbound-pull-rates-box-opening)、[VEN](https://tcgtalk.com/guides/vendetta-riftbound-pull-rates-box-opening)、[RAD](https://tcgtalk.com/guides/radiance-riftbound-pull-rates-box-opening)：史诗出包约 27%；异画 10%／9%／9%；超编 1.7%／2%／1.2%；传奇出包约 50%／50%／31%；RAD 异画符文约 3.5%。RAD／VEN 特殊异画的绝对数值 9% 沿用本系列社区异画观测值，配置标社区，并分别将同频关系标为官方／官方转述依据。社区值全部待确认，仅影响概率。 |
| 第三方产品分类 | [营地](https://www.iyingdi.com/tz/post/5608629)：中国区标准包 5 张、进阶包 14 张；不替代官方规则。 |
| 项目假设 | 闪卡槽稀有 8%、史诗 2%，互斥；同包同功能牌不重复（含闪卡／异画）；未知异画符文率先为 0；UNL 终极暂取 0.05%。史诗按 `1-sqrt(1-每包概率)` 预计算独立双稀有槽概率，允许双史诗；社区传奇率按相同方法换算并条件于非史诗槽（印次替换会改变实际传奇率）。每包最多升级一个稀有槽，印次类型互斥，OGN／SFD／UNL 随机选择一个稀有槽替换。真实相关性未知，均列待确认。 |

真实印次保留到 `pool`；异画／超编规则与平卡相同，开放系列的所有候选印次都有引擎加载和实现状态守卫。符文／指示物槽供开包展示，完全不计卡池数量，基础符文本来不限。确定性使用 SHA-256 计数器，候选按 cardNo 排序；结果携带私有种子、配置版本、配置 SHA-256 和生成器版本。房间接入时将完整结果写日志／恢复帧，禁止任何私有种子或卡池进入对手与观战者投影、提示或公开日志。

全部页面原始快照和等级在 `data/official/upstream/2026-11-02/sealed-products/manifest.json`。原始页面以确定性的 `*-raw.html.gz` 无损保存；清单的 `sha256`／`bytes` 对应解压原文，`archiveSha256`／`archiveBytes` 对应压缩文件。2026-11-02 复核[官方辉耀群星售前公告](https://playriftbound.com/zh-cn/news/announcements/preparing-for-the-radiance-pre-rift/)与上述中文系列概览的正文、产品配图：只确认 5 包加英雄卡包（1 传奇、1 稀有英雄、1 战场、辅助卡牌），以及九位传奇主题；没有逐包完整卡表或辅助卡数量。售前保持“待确认”，取得官方卡表后再接入；限定黑默丁格不能入现开卡组，不从主题、配图或目录猜辅助卡。
