# 固定 00420 双客户端、SQL、重连与正常得分回放

固定候选 `00420e2a2b453918e611812d18fda9f62632e15f` 已完成可见 macOS 双客户端、实际 PostgreSQL、两种断网恢复、正常 8:0 得分终局和从原始开局的完整回放。38 条命令、198 个事件，最终 tick 37，终态哈希 `a8b2b01d957bce1358c8ba572aacf7f2f827a80a56d12822f87311a3fccf51a2`。没有投降、预置终局、强制胜者或历史补写；本次业务规则观察和 Windows 实机仍分别未闭合。

客户端来自该提交 CI `38630389077` 的 macOS 导出包。仅为定位两个窗口修改测试副本的 bundle ID、显示名和本地签名，两端各 372 个 DLL 与原导出包一致。隔离 API 在 `127.0.0.1:5154`，实际 PostgreSQL 为独立 `riftbound_acceptance:55439`，房间为 `paired-00420-20261015`。Cua 操作真实起手确认、符文、手牌、出牌、移动目的地、预知确认、响应和回合控件；两端分别显示胜利及失败，原因均为达到胜利分数。

3:0 时停止本次 API，两端冻结行动，长中断耗尽重试后分别按实际重连按钮恢复同一 tick 27 状态。7:0 且团结圣坛技能待响应时进行短中断并立即启动同一 API，两端自动恢复，没有点击重连按钮，原技能及响应责任保留；随后通过双方响应继续对局。短中断收据比较保留的 SQL tick 33 检查点；其后已继续行动，因此没有声称最新状态始终不变。

实际 SQL 恢复帧包含原始席位、卡组、起手、全部命令和事件；恢复一致性及完整动作回放均通过。实际规则输入身份为 `e139f4b8e64f6646e20b657cb9405ba925570e10764a65d49906bc2c5500793a`。原始 SQL 帧、日志、两个中断检查点、导出包和程序集指纹保留在 `var/qa/paired-sql-score-20261015`；本次专用客户端、API 和数据库均已停止，实际数据及已接受历史保留。

[流程收据](evidence/paired-sql-score-2026-10-15/product-receipt.json)、[胜者](evidence/paired-sql-score-2026-10-15/winner.png)、[对手](evidence/paired-sql-score-2026-10-15/opponent.png)、[长中断](evidence/paired-sql-score-2026-10-15/long-outage.json)、[短中断](evidence/paired-sql-score-2026-10-15/short-outage.json)只覆盖该固定候选的组合流程。其完整 CI 已另行核验；后续游戏输入改变将重新打开最终候选验收。

该固定提交完整 CI38630389077随后已实际终态成功，[TRX证明](evidence/sivir-payment-2026-10-15/full-ci-00420.json)为11,744条 /7,245方法，原143对局、110配对及种子保留；仅适用于00420。现场金币问题现由[单位征服金币限定修复](CONQUEST_TOKEN_TRIGGER_AUDIT_2026-10-15.md)接续，原现场与历史不改写，新游戏输入重新验证。

现场坏坏魄罗 SFD·069/221 征服后金币立即出现，没有可见响应链。重新取得的当前官方单卡身份为 33149，原始内容、哈希、地区语言及未知日期见[来源](evidence/paired-sql-score-2026-10-15/bad-poro-source.json)；[原始观察](evidence/paired-sql-score-2026-10-15/bad-poro-observation.json)保持取得时状态，其后两种印次独立红例及限定修复由上述新审查接续，不因计分及回放通过认证整卡。用户暂无 Windows 设备，实机验收保持 OPEN。全量来源与剩余义务保留，Goal active / NOT READY。
