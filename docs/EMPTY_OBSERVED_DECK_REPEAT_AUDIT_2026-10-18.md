# 空查看结果下的已支付回响

CN / zh-CN，2026-10-18 取得和观察，适用日期冻结为 2026-10-16。本批限定共享牌堆查看流程：首次查看的牌全部以魔腾静态能力离开牌堆时，只完成这一次查看，再执行已支付的回响；两次法术效果完成后才处理延迟出牌许可。其他消费者、正向魔腾出牌和整卡语义仍 OPEN。

## 来源与赛制

[来源 manifest](evidence/empty-observed-deck-repeat-2026-10-18/source-manifest.json)保留三张精确印次的当前官方 API 记录、QA、取得时间和压缩原始字节哈希，14 项当前规则列表仍相同。核心 PDF 为 2026-10-10 发布、10-16 生效，40278538 字节，SHA-256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`；重新流式取得并核对，未保存重复 PDF。820.1.d.1 要求重复法术效果，432.1.c 保留短牌堆查看及继续结算。未公开的卡牌发布/生效日期仍为未知。

[当前禁卡表字节](evidence/empty-observed-deck-repeat-2026-10-18/banlist-current-reconfirmation.json)与运行采用的 DOC-08 哈希 `e0326381f003a3e5d0a12db9521dc15eb1cc4ed0456188cee9fc46e0eb6d5ef7` 相同。[分段日期说明](evidence/empty-observed-deck-repeat-2026-10-18/banlist-effective-date-addendum.json)区分 7-17 发布、最新增量 7-24 生效及《预判攻势》本身从 3-31 起的标准赛制禁用；不改写此前保留的未知日期记录。《预判攻势》普通开局只属于 CoreRules；《龙虎双雄》两种开局选择均通过 CoreRules 和中国区标准赛制校验。最初双赛制检查实际拒绝《预判攻势》，保留原失败日志。

## 原错误与共享修复

[实际固定 46 Release 红例](evidence/empty-observed-deck-repeat-2026-10-18/actual-fixed46-empty-repeat-observation.json)使用实现 `d21fa804…d4dcb`：首次查看的 2 或 3 张魔腾全部放逐后，直接进入延迟出牌许可，跳过已付费的第二次查看，原尾牌完全未动。两份显式中盘的原行动和事件仍能完整回放；可回放不代表语义正确。原前缀、实际程序集及源码哈希在[保留收据](evidence/empty-observed-deck-repeat-2026-10-18/actual-fixed46-red-retention.json)登记。

修复仅在共享 `BeginDeckChoice` 的空查看分支增加已完成次数，并按总次数决定整项是否完成；后续沿用原共享结算。没有牌名绕过、通用 DSL 扩张、整体重写或旧开发局迁移。独立恢复仍检查次数、当前选择和许可，原行动日志继续保护协调伪造。

## 测试、原日志和真实数据库

[焦点执行](evidence/empty-observed-deck-repeat-2026-10-18/focus.json)包含新显式 13 例、共享相邻 237 例/120 方法及最终普通开局合计 16 例/6 方法。首次新夹具非零 tick 导致的 13 个帮助方法失败、随后实际语义 6 失败/7 控制通过、普通开局的 3 个新错误断言均按原执行记录保留。普通断言把待确认的魔腾误认作已结束的放逐区，修正后按移出牌堆、原放逐事件、延迟许可及最终放逐区核对；没有为这些帮助方法错误改生产规则。六魔腾的双空查看行明确只是显式中盘，不伪称普通合法牌组。

[三个普通空白分支](evidence/empty-observed-deck-repeat-2026-10-18/ordinary-openings.json)共 86 命令/296 事件、12 冷恢复和 9 条原下一命令核对，从 tick0/seed0、零对象通过合法牌组提交、准备、调度、正常回合及资源支付到达目标窗口。只规划提交牌组的原始顺序，不注入手牌、洗牌结果或中途状态。原 11 牌组及 424242 不变。

[真实 PostgreSQL](evidence/empty-observed-deck-repeat-2026-10-18/real-sql.json)相同三分支共 86 命令/296 事件、30 冷恢复、9 条原下一命令、18 次篡改拒绝。错误次数、提前完成、放宽最大选择及错实现身份由独立结构检查拒绝；协调修改第二轮牌序及双方视图能通过形状检查，仍被原始行动日志审计拒绝。拒绝不写库、不缓存，原七表精确恢复。此前 24 个审计房间及原生两局七表未变；复用原专用库，结束后 smart stop，未新增数据库。

[实际编译身份](evidence/empty-observed-deck-repeat-2026-10-18/actual-compiled-inputs.json)由 PE MVID 独立复算：当前 Debug `c4ecf67f5bee1b6f0a00d38b9a4f34f4090eb1a55aa057be390d59d2e0c840fe`。Conformance、SQL、原 115 对局及冻结四模块/三数据相同；旧固定 Release `d21fa804…` 单独保留，源码相同不视为二进制相同。

[原 5 场和 110 配对](evidence/empty-observed-deck-repeat-2026-10-18/original-five-and110.json)全通过，配对 ID 集合、11 牌组、424242、两赛制逐牌有序 golden 及原胜利/事件/回放断言保留。[原测试保留](evidence/empty-observed-deck-repeat-2026-10-18/test-retention.json)：458 份原 Conformance、1278 原夹具、8 份 FullGame/143 方法逐字节相同；原 SQL 33 份中的 32 份字节相同，原 32 项默认调用顺序不变，只追加新入口。唯一[check](evidence/empty-observed-deck-repeat-2026-10-18/one-check.json) 12490 例/7346 方法全部通过，760.42 秒，原 12474 例/7340 方法多重集合完整保留，新增 16/6；12667 执行输入和即时核验未漂移。[55 项工具测试](evidence/empty-observed-deck-repeat-2026-10-18/tooling-tests.json)通过。

## 原生和剩余义务

[生产原生控件](evidence/empty-observed-deck-repeat-2026-10-18/native-controls.json)在真实 Godot headless 进程中执行三个第二轮提示的选择和确认按钮信号，覆盖必选、放弃、前一轮卡牌、未查看尾牌、等待网络和旧窗口防护；三条原生模板命令由同一冻结引擎接受并完成原始回放。本批未改变页面布局。headless 不替代新固定候选的可见双端和 Windows 实机。

[前固定 46 的可见基线](evidence/empty-observed-deck-repeat-2026-10-18/fixed46-visible-native-baseline.json)完成真实 46 条原生命令/237 事件、服务停机 45.495 秒后的原库冷恢复、双方原待处理视图及下一命令、正常 8:0、双方终局及返回大厅。自动重试耗尽后使用双方原生重连按钮，不声称全自动重连；三技能排序仅属单控制者，不提升双控制者交接，也不覆盖本批新修复。原始私有种子和完整原帧保留，原 H 局 16/56 及七表精确不变，双端/API/专用库均正常退出。固定 46 完整 CI 39103607902 仍须按其实际终态独立核验，不从双平台或原生基线推定完整成功。

[边界](evidence/empty-observed-deck-repeat-2026-10-18/acceptance-boundaries.json)保留新固定 full、当前可见第二轮选择/延迟出牌、网络/正常得分/原始回放及 Windows 实机；正向魔腾出牌、其他消费者、双控制者目标/支付/实际入链/细时序和全部整卡语义仍 OPEN。统一义务保留 127 原审查和 16612 原 ID，新增四分支及三门禁后为 128 审查、43 上下文、16619 义务；语义、测试、固定 CI 和产品状态分开，不提升整卡认证。

[空间清单](evidence/empty-observed-deck-repeat-2026-10-18/storage-control.json)确认仅最新 46 A/B 与 Mac/Windows 各一规范包；无完整源码树复制，来源、所有测试/TRX/原始回放/数据库及必要实际模块保留。Goal active / NOT READY。

两项[原生观察](evidence/empty-observed-deck-repeat-2026-10-18/native-ui-observations.json)各自登记为 OPEN 产品门禁：先启动客户端再启动服务后的目录恢复，以及三技能排序显示实现枚举。仅确认可见行为，不推定原因；后续须独立复现、修复及固定可见验收。
