# 武装夺取后的单位复制

2026-10-13。总 Goal active，平台 **NOT READY**。本批核对阿克尚夺取已贴附的可疑的眼镜，以及三个厄斐琉斯印次的后续贴附、复制选择和单位离场路径。结果不认证整卡或全部控制权交互。

重新取得八条当前中文精确记录：SFD·109/221、SFD·049/221、SFD·224/221、SFD·224*/221、VEN·137、OGN·104/298、OGN·229/298、SFD·022/221，全字段与当前保存的 CN 1502 快照一致。[来源记录](evidence/attachment-theft-2026-10-13/source-readback.json)保存官方请求、取得时间、地区语言、响应及完整记录哈希。没有新增运行目录记录或合并中英文印次身份。

当前 [Vendetta FAQ](https://playriftbound.com/en-us/news/rules-and-releases/vendetta-rules-faq-and-clarifications/)发布于 2026-08-14。夺取已贴附的眼镜后，原宿主先失去复制效果；阿克尚随后根据其控制者的选择复制另一名友方单位，新取得的厄斐琉斯技能不能看到先前已经完成的贴附。[FAQ 回读](evidence/attachment-theft-2026-10-13/faq-readback.json)与[六条当前核心规则](evidence/attachment-theft-2026-10-13/rule-excerpts.json)保留精确依据。中文核心 PDF 再次取得且哈希相同，更新 2026-07-17，独立生效日期未知；10 月 16 日未来规则继续不提前应用。

当前引擎的五条实际夺取/离场基线通过，没有据此发现运行错误。追加检查证明原宿主在公开复制选择预览中已恢复，装备拥有者仍为原玩家、控制者为夺取者；原拥有者不能选择，敌方单位和阿克尚自身不能成为复制对象。后续真实装配能独立触发新取得的厄斐琉斯技能，确认模式及目标后仍等待响应才给予增益。改变卡面不抹去阿克尚已经建立的夺取期限；返回手牌和摧毁后，装备归还原拥有者基地，单位恢复其物理阿克尚身份并使用新世代。上述路径沿既有共享生命周期运行，本批没有改写引擎来制造通过。

原生验证最初把友方武装放在场上，真实百炼确认因而开启，驱动错误地直接让过；[失败与修正](evidence/attachment-theft-2026-10-13/native-fixture-correction.json)另存。改为先保留长剑在手牌，再真正打出。长剑具有灵便，打出时必须选中宿主；第二个驱动遗漏目标被正确拒绝，[记录](evidence/attachment-theft-2026-10-13/agile-fixture-correction.json)保留。最终通过生产额外费用复选框、打出 composer、费用回读、复制选择、灵便目标、模式/目标和响应控件，生成三场 11/7/7 条命令。25 条实际读回、去重、各一条过期拒绝及动作/事件/状态回放全部一致，[原生执行](evidence/attachment-theft-2026-10-13/godot-execution.json)和[真实回放](evidence/attachment-theft-2026-10-13/native-replay.json)只覆盖 headless 生产控件。此前 44 条眼镜原生命令也在本批代码回放。147 项相邻检查含七条原完整实战通过；本批一次 check 已通过 10,689 用例 / 6,740 方法、139.77 秒，零失败、跳过或重复；原 10,677 / 6,735 全部保留，新增 12 / 5，[独立核对](evidence/attachment-theft-2026-10-13/check-parity.json)同时确认 14 项输入哈希不变。完整计划保留已完整验收 b4dbab3f 的全部 6,859 方法，增加至 6,881；预期 10,838 用例及 seed 424242 的 110 配对，[计划核对](evidence/attachment-theft-2026-10-13/full-plan-parity.json)不证明执行通过。固定提交 full 待终态核对。

前一实现 eedba831 的 CI 38329865725 中，macOS 桌面成功，Windows 导出退出时报两个纹理 RID 和两个 ObjectDB 实例泄漏，导出包启动检查未执行，规则任务仍在运行。[Windows 失败记录](evidence/attachment-theft-2026-10-13/windows-export-failure.json)保留日志哈希和实际错误。根因未知；本批只加入 export --verbose，以取得更详细的泄漏对象诊断，非零退出和 ERROR 门禁继续保留，不声称修复 Windows 导出。[问题 #23](https://github.com/DDDHL0704/riftbound/issues/23)跟踪实际导出失败。

另以当前英文图库官方 API 单页 limit=2000 实取 1381 个独立 ID，官方仍报告 1389，八项差额继续开放。与先前英文快照逐 ID 比较无新增或缺失；16 条 Radiance 记录只移除了营销 New 标记，文本及身份不变。[当前来源核对](evidence/attachment-theft-2026-10-13/en-source-gap-readback.json)保存取得时间、完整响应与数据哈希、全部记录哈希、顺序和精确替换记录；可按固定旧快照重建当前数据。该标记变化不证明发售或生效日期，没有提升到运行目录。

仍开放：全部控制权与复制交互、来源离场再入场的历史捕获权威、原文字失去及重新取得、全部同时替换排序、整卡及全量 1738 卡牌 / 14293 规则块语义、八项英文来源差额、Windows 导出泄漏根因与实际用户运行、可见双客户端完整实战及真实 PostgreSQL。索引完整、局部检查和计划不替代完整验收。
