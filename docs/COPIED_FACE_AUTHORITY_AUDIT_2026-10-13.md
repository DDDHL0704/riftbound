# 复制卡面变化后的文字取得权威

2026-10-13。总 Goal active，平台 **NOT READY**。本批限定于当前镜花水月的映像反身复制、响应贴附斯弗尔尚歌、保留取得时的文字、卸装重新贴附及列出的当前恢复与原生路径，不认证整卡或所有复制类型。

重新回读 SFD·059/221 斯弗尔尚歌、UNL-200/219 镜花水月、SFD·011/221 取放自如、VEN·137 可疑的眼镜及结果卡面 VEN·183 黛安娜、SFD·050/221 阿兹尔，六条精确记录全字段与 CN 1502 快照一致。[主要来源](evidence/copied-face-authority-2026-10-13/source-readback.json)和[关联卡面](evidence/copied-face-authority-2026-10-13/associated-readback.json)保留官方 API URL、请求、取得时间、地区语言、响应和记录哈希。

[当前官方 Vendetta FAQ](https://playriftbound.com/en-us/news/rules-and-releases/vendetta-rules-faq-and-clarifications/)发布于 2026-08-14，明确先完成贴附，再复制当时的印刷文字；新取得的技能不能追溯此前的贴附，各次取得为独立实例。[回读记录](evidence/copied-face-authority-2026-10-13/faq-readback.json)保存当前内容哈希。中文 DOC-04 的 370.1、477.1.b、719、383-385 和 124 见[精确条款](evidence/copied-face-authority-2026-10-13/rule-excerpts.json)。核心 PDF 的最近本任务取得时间及哈希由[已验证来源](evidence/copied-face-authority-2026-10-13/core-readback.json)保留，更新日期 2026-07-17，独立生效日期未知；10 月 16 日未来规则不提前应用。

固定 c836ca10 的两条[反例](evidence/copied-face-authority-2026-10-13/red-evidence.json)执行真实“映像进场 → 取放自如响应贴附 → 镜花水月反身复制结算”。映像物体世代没有改变，武装仍贴附，但已取得的映像文字被清空，无法通过当前恢复校验。原实现误把已取得的文字绑定到宿主当前卡面。

当前共同复制路径保留同世代的卡面转换记录；文字取得和捕获来源保存取得时间及卡面版本。按取得时的版本核对卡面，保留武装版本和来源世代，避免同一命令内有多个步骤时只靠 tick 区分先后。当前复制映像的卡面转换记录不可删除，未知卡面、空记录、未来时间、错误世代、错误固有来源、缺失取得字段及伪造文字版本拒绝。记录由真实复制流程产生并在正常化中保留，不从旧开发状态补造。

在复制前贴附所取得的映像文字保持原空文字；宿主后来成为黛安娜，下一次施法只有本体一套技能，不给武装凭空增加黛安娜文字。卸装并重新贴附后才取得当时的新阿兹尔文字，并产生新的武装文字版本。两条原反例、126 项扩大定向及 71 项版本与相邻检查通过。Godot 生产 composer/quote/目标选择/独立响应控件生成两场 13/14 条命令；27 条实际读回、去重、各一条过期拒绝及动作/事件/状态回放一致，[原生执行](evidence/copied-face-authority-2026-10-13/godot-execution.json)及[实际回放](evidence/copied-face-authority-2026-10-13/native-replay.json)仅限定于 headless 生产控件。290 项相邻检查包括五条原完整实战用例；本批一次 check 已通过 10,639 用例 / 6,718 方法，128.80 秒，零失败、跳过或重复；原 10,621 / 6,712 全部保留，新增 18 / 6，[独立核对](evidence/copied-face-authority-2026-10-13/check-parity.json)同时确认 13 项测试输入哈希未改变。full 计划保留原 6,853 方法，增加至 6,859，预期 10,788 用例及 seed 424242 的 110 配对；计划不证明完整通过，固定提交 CI 待推送后记录。

仍开放：可疑的眼镜 VEN·137 尚无运行实现，当前结果不认证该卡；同世代改变既有非空文字后的全部已捕获技能组合、跨世代历史捕获权威、所有原文字来源权威、全部复制与多重变化、整卡和全量 1738 卡牌 / 14293 规则块语义、八项英文来源缺口、可见双客户端完整实战、Windows 用户运行及真实 PostgreSQL。[问题 #21](https://github.com/DDDHL0704/riftbound/issues/21)记录本批边界。本批只证明列出的结构校验与真实回放。
