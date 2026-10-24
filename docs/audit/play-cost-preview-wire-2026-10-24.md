# 费用预览旧测试的完整出牌请求

- [Issue109](https://github.com/DDDHL0704/riftbound/issues/109)后续，仅改 `PlayCostPreviewTests.SessionPreviewIsReadOnlyAndCannotAuthorizeAStaleSubmission` 输入构造及先行负控；不改Engine/DTO/恢复/guard或全局helper。原提示信封缺required targets，冻结527与b9各自真实8例均7PASS/1同红；不是b9新增入口补丁引入。
- 保留原坏raw为INVALID_PAYLOAD、零event/日志/状态变动前控；随后用实际PlayCardCommand+Web序列化，加原prompt/tick，经原生Mapper以同intent提交完整新wire。原只读/成本/accepted/stale/日志断言、原Fact/Theory/InlineData、其余方法及fixture不改，反向还原整个文件与原件逐字相同。
- WIP费用预览8例＋restricted mana29例共37/37、0skip；各版本Source、TRX、实际模块SHA/MVID/RuleInputs分列，提交后另重建复测。没有以降低guard、清洗旧接受日志或Noop分流获得绿色。
- 原件在本线ignored `var/qa/play-cost-preview-wire-20261024/`：双冻结 `original527-v1/`/`originalb9-v1/`、原测试源、`dual-baseline-and-retention-v3.json`、WIP目录及后续固定提交收据。属于显式中盘测试，不冒称新普通出生/SQL/API/网络/完整对局证明。
- 仅此测试/短审计增量，不导入父运行功能；集成与实际CI分级由①按固定差异处理。后端NOT READY，净整卡认证0，完整Goal继续active。
