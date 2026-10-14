# 等待玩家投降入口限定审查

在固定14ed8981上重新取得服务端提示，并用Godot实际控件复现：魔腾私下查看静态选择与原效果结束后的待确认出牌期间，P2不负责当前任务，权威提示actionable=false，但SURRENDER候选enabled=true。Main.PromptAction却按整个提示actionable禁用全部候选，导致投降按钮消失。[原生失败收据](evidence/waiting-surrender-2026-10-14/native-red.json)保留。

修正仅让服务端明确启用的SURRENDER候选不受当前任务责任者限制。全局责任、等待消息、其他行动与服务端禁用候选保持原校验；正常ActionBar选择、确认按钮和CommandFromTemplate提交路径继续使用原promptId/snapshotTick。未改变游戏规则或卡牌目录，当前服务器权威与客户端映射证据见[追踪收据](evidence/waiting-surrender-2026-10-14/authority-trace.json)。

保留原SurrenderDoesNotStartAnotherObservedConfirmation的两条Theory行，增加执行者确在等待、权威允许投降的独立断言，重新接入原生产控件回读。Godot在两个原缺口分支实际生成10条命令，并同时验证等待责任不变、服务端禁用投降不能选择、非行动者不能获得让过优先权。随后6用例/5方法9.82秒通过，10条命令完成实际服务端接受、冷恢复、玩家/观战隐私、重复intent去重及完整journal回放。[相邻证据](evidence/waiting-surrender-2026-10-14/adjacent-focus.json)只覆盖该客户端缺陷。

2,269输入已冻结；完整发现7,169方法、预期11,540用例，原方法集合、143完整对局、110配对及seed424242不变。收尾check与精确新提交完整CI仍须各自终态证据。可见双客户端、用户Windows实机、光明未来待处理顺序、烛光圣所私下任意回收/排序及全量来源覆盖仍开放。两个本批原生分支通过不提升整卡或全量验收，Goal active，平台NOT READY。

[最终check和精确集合核对](evidence/waiting-surrender-2026-10-14/check-parity.json)11,391用例/7,028方法、252.78秒全部通过，旧用例/方法多重集合完全保留，零新增/缺失/失败/跳过/重复。2,269输入与发现/检查程序集无漂移。真实SQL五项、Python27通过，独立数据库已停止。[原生最终回读](evidence/waiting-surrender-2026-10-14/native-replay.json)两分支10命令全部通过；本批没有排除这些分支。上一14ed8981完整CI38534398383仍在实际运行规则步骤，两平台构建成功，完整SQL/网络待后续步骤；不声称当前终态或当前工作树完整CI通过。
