# 正式开局随机化与私密原始种子

本轮仅审查 CN / zh-CN、截至 2026-10-17 适用的单局 1v1 决斗（核心 487）。[来源与取得时间](evidence/opening-randomness-2026-10-17/source-manifest.json)记录当前 DOC-01：2026-10-10 发布、2026-10-16 生效，SHA-256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`。重新取得的核心字节和 14 条完整规则目录未变；第一次包装器使用不存在的 id 字段所致失败保留。来源变化与全量缺口不缩减，历史 ledger 的确认日期和 NOT_REVIEWED 不改写。

第 6 页 108.4.e / 108.5.d 要求主牌库、符文牌库顺序私密；第 7 页 114 为洗牌、115 为公平随机回合顺序；第 101 页 487.5 为决斗各自三个战场中随机使用一个。488.5 的比赛模式改为自行选场并在系列中处理已用战场，仍单独 OPEN，不能由本次随机消费者测试认证。精确原条款和统一剩余义务分别见[原四条链接](evidence/opening-randomness-2026-10-17/existing-obligation-links.json)与[模式条款链接](evidence/opening-randomness-2026-10-17/mode-source-links.json)。

固定 39 的实际程序集探针使用 20,000 个合成种子，各调用两个 scope/cursor，共 40,000 次实际方法调用。同一 39 张牌库的三个同名物理副本全在前十张的次数为 2629 / 2526；均匀排列的独立组合参考为 120/9139，约 1.313%。旧方法的 13.145% / 12.630% 明显聚集。终局和服务关闭后，另由公开房间/座位字段独立推算，确实匹配旧生产入口的实际种子；未在实战中利用未来牌序，也未改写历史。此前缩略图误认观察已撤回，以上反例均独立于该错误观察。[问题 #51](https://github.com/DDDHL0704/riftbound/issues/51)。

新生产会话在原始开局前取得非零的服务端私密 64 位种子，座位保存时写入已有 matches.seed，之后命令和恢复匹配该输入；缺失、非规范文本和冲突不补写。显式初始状态的 0、424242 和有符号种子保持确定性。四个开局消费者使用同一稳定 SHA-256 流及 Fisher-Yates / 拒绝采样，不再用牌名、物理 ID 的 FNV 排序；scope、cursor 和样本计数器明确隔离。恢复帧与 journal 必须记录原始种子，异常消息不输出种子。没有为旧开发局增加迁移，也不改变客户端的规则裁定责任。

[最终实际程序集量测](evidence/opening-randomness-2026-10-17/actual-method-green.json)使用同一份合成种子，三个副本均在前十张的次数为 227 / 240（1.135% / 1.200%），起手平均副本数为 0.30850 / 0.30845，独立参考为 12/39。有限样本只是回归反例与偏差边界，不是完整均匀性或全部隐私面的证明。12 份独立 Python 向量、强制重试边界、排列守恒、牌名无关性、四个实际开局消费者和错种子/缺字段拒绝由生产代码测试覆盖。

[定向记录](evidence/opening-randomness-2026-10-17/focus.json)：最初 10 例有 8 例失败，新增 38 例 / 12 方法，最终原因历史与隐私 48 例、修正范围 63 例通过。早期 641 相邻例 / 404 方法含 5 个原完整对局；该证据早于最后的无行为差异 helper 提取和不泄露种子的错误消息。唯一 check 实际执行 12097 例 / 7267 方法，12085 通过、12 失败，3174 输入从启动到终态无漂移；原 12060 用例全部保留。原失败身份均在最终 63 例中通过，没有第二次 check，不能把此记录写成 check 全绿。[原始失败及闭合](evidence/opening-randomness-2026-10-17/check.json)。

[最终源码保留](evidence/opening-randomness-2026-10-17/original-source-parity-final.json)：438 旧 Conformance 源码中 434 份逐字节相同，另四份保留所有旧方法与 Fact/Theory/InlineData/MemberData，修正测试 store、无泄漏消息、正常资源驱动和当前 fixture 前提。143 完整对局、110 配对、11 牌组、424242 及胜利/回放断言源码逐字节相同。六个 SQL 显式位置保留原 424242。旧 339c 实战及全部 16 份旧开局 fixture 原字节保留；新版本另用普通命令生成，不修改已接受历史或旧局。当前原因历史独立保留原 turn1/type1/generation1/tick8/face0 和八种伪造断言，只更换当前实际角色/物理身份；可选牺牲的提交牌组在首条命令前调整三处输入位置，完整牌组多重集合及原种子 0 不变，避免意外得分终局妨碍费用用例。

[17 份当前输入导入](evidence/opening-randomness-2026-10-17/current-fixture-import-validation.json)分别从原始空开局执行 live mapper、独立结构校验、生产 registry 冷恢复和完整行动/事件/状态回放，全部通过。[8 次新生产种子 Hub](evidence/opening-randomness-2026-10-17/fresh-production-hub-repeats.json)保留原正常出牌、投降胜利、隐藏信息和回放断言，通过服务端报价及普通资源行动支付已知薇，无手牌必然存在廉价牌的假定；真实原始私密种子用于回放，未从公开房间重算。

[最终实际 PostgreSQL](evidence/opening-randomness-2026-10-17/sql-final-execution.json)执行两条路径：生产私密开局 7 命令 / 27 事件、7 冷检查点 / 4 元数据篡改拒绝；当前原因历史 9 命令 / 28 事件、8 原因伪造拒绝及恢复。12157 源输入 / 68 运行文件全程固定，专用库停止保留。首轮 JSON 对象键顺序比较失败和全部原库/记录保留，独立 Python / .NET 确认语义相同，数组顺序始终严格比较。默认完整 SQL 入口保留原 83 路径并新增私密开局；本地显式小范围不冒称全部 84 条 SQL 已通过。

新原因历史第一份输入使用显式 `optionalCosts:null`，live 接受九条命令但独立恢复结构检查失败；该接受历史和第一次生成器仍保留。第二份独立新局使用显式空数组，并非把第一份记录改成可恢复。此契约缺陷登记 [#52](https://github.com/DDDHL0704/riftbound/issues/52)，实现与产品仍 OPEN。[收集器、fixture 和编译更正](evidence/opening-randomness-2026-10-17/post-check-corrections.json)保留所有失败；固定 39 的四份 SQL 收据确切名为 acceptance.json，初次遗漏后的修正只加入这四个精确路径，未把附属 JSON 充作场景。

固定 39 的完整 CI38963831426 已实际成功核验 12209 例 / 7397 方法、83 SQL、143 原完整对局 / 110 配对 / 424242、网络 15 次跨身份拒绝及四产物大小/digest/ZIP；原生规范包复用无副本。[固定收据](evidence/opening-randomness-2026-10-17/prior-fixed39-full-ci.json)只属于 39，新开局实现的固定 full、当前可见双客户端/网络/正常得分及回放、Windows 实机仍分别 OPEN。非开局随机化（含回收 417.5）、其他模式、消费者、Raw null 契约以及全部 1740 来源 / 1033 运行与地区、FAQ/QA、资讯媒体缺口继续保留。Goal active / NOT READY。
