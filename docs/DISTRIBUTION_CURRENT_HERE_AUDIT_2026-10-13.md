# 进攻伤害分配与结算时的来源位置

2026-10-13，Goal active，平台 **NOT READY**。本批仅更正单位进攻伤害的当前位置引用；不认证整卡或全量规则。

[中英文实时卡面](evidence/distribution-current-here-2026-10-13/source-card-readback.json)与[英文回读](evidence/distribution-current-here-2026-10-13/en-card-readback.json)均要求向“此处”敌方单位分配五点伤害。当前中文核心 `359.3.f.2` 与 [Vendetta 官方 FAQ](https://playriftbound.com/en-us/news/rules-and-releases/vendetta-rules-faq-and-clarifications/)说明，来源位置在指示执行时检查；[来源决策](evidence/distribution-current-here-2026-10-13/source-decision.json)保留来源版本及取得哈希。触发条件的位置引用与这类当前位置引用不同。

真实进攻、目标确认、反应摧毁来源后，旧实现仍沿用最后战场并打开分配窗口。先按官方来源更正原测试期望，复现失败，再将 `DistributionHere` 改为仅使用同一世代、仍处于场地的来源当前位置。来源离场时已确认技能留在链上，但原目标失效，无分配窗口和伤害；场内移动仍跟随当前来源。最后场地位置只保留为恢复凭据。

原 `SourceDestroyedAfterConfirmationKeepsItsCapturedAbilityAndLastFieldLocation` 方法名保留以维护测试身份，结算期望按现行来源更正。原三条“来源离场 / 防守方离场 / 双方离场”Theory 行与胜利、战斗收尾断言保留；双方离场用双方各自真实的摧毁法术实现。所需抽牌来自真实非空牌堆，避免该场景混入燃尽。

该过程中独立复现了 [空牌堆战斗历史缺口](evidence/distribution-current-here-2026-10-13/empty-deck-battle-history-gap.json)：被摧毁的参战单位被燃尽回收并抽到手牌后，旧 `BattleResolutions` 私有对象引用投影与恢复校验冲突。失败收据保留，当前批次不宣称修复这一缺口；下一范围需要捕获实际战斗结束时的公开历史身份，并同时保持当前手牌内容隐私。

相邻 focus **109 用例 / 44 方法通过**，原方法及全部 Theory 行保留。[生产目标确认、响应与伤害分配原生证据](evidence/distribution-current-here-2026-10-13/native-replay.json)记录 17 条实际命令，服务端回读及最终日志重放匹配。[连贯 check](evidence/distribution-current-here-2026-10-13/check-parity.json)为 **10,875 用例 / 6,810 方法，149.25 秒，零失败或跳过**，与原用例身份和次数完全一致。2,020 输入未变，五项真实 PostgreSQL smoke 通过且专用实例已停止，Python 27 项通过。[完整计划](evidence/distribution-current-here-2026-10-13/full-plan-parity.json)维持 6,951 方法、预期 11,024 用例，固定提交 CI 执行待终态。原完整对局方法、110 配对和种子不改。

其他当前引用、复制指示物分配技能、战场世代、控制变体、一致历史伪造、全量来源缺口，以及可见双客户端、用户 Windows、完整 SQL 对局/并发/故障转移仍开放。
