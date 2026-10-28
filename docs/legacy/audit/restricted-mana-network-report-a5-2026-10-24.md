# 受限法力网络报告枚举序列化 · 2026-10-24

- 问题：[GitHub #113](https://github.com/DDDHL0704/riftbound/issues/113)。固定 A5 `72e5d63d8562cee3884e1507868048e5b267e45c` / Actions `40039778429` / network job `120708374062` 的第六组 `restricted-mana-lux/play` 失败；原 artifact `12539093652` 和失败保持，不重试或改判。
- 原因：RunCase 的真实 `WsServerMessage` 中 `RECONNECT=1`，原报告使用继承的默认 Save 写成整数，而原 trace 使用既有本地 `JsonStringEnumConverter` 写成字符串。
- 修改仅为 `RestrictedManaNetworkAcceptance.RunCase` 的报告调用及私有 `SaveNetworkReport`：传入原本地 json。原 trace、全部领域断言、公共严格认证守卫、默认 Save、SQL、权限、付款及恢复源码保持。
- 真实 C# writer 离线对照：原 Save 逐字复现 OGS/SP6 两份原报告；新叶子 writer 的 98 条 envelope 均匹配原 wire trace，4 条 Joined 为字符串 RECONNECT，其他字段与实际令牌保持，原件未改。未运行 SQL/API/游戏规则或新进程重启。
- 固定 A5 公共守卫：原报告/旧 writer 拒绝，修正报告两份通过；原有 12 项纯守卫与阶段控制测试通过，输入和守卫源码不变。此结果仅是离线格式验证，不能代替新网络执行。
- 原失败、before/after 报告、源与实际程序集回执在 ignored `var/qa/restricted-mana-network-report-a5-20261024/`；回执区分 WIP 与后续固定源输入，不将 own 父分支其他功能带入修复候选。
- 原 A5 未执行/未完成的网络后续阶段和本次窄修的真实 restart/finish/verify 仍由主线另封候选验收。净整卡认证增量 0，后端 NOT READY，整体 Goal 未完成。
