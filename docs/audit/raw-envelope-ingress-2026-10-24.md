# 原始 wire 信封在日志前校验

- [Issue115](https://github.com/DDDHL0704/riftbound/issues/115)，L2；基线 `801256d6`。①批准精确生产 patch `6969b224…` 和五新增测试 `49c21acc…`，正式审阅收据 SHA `d8f4e09b…`。仅四入口 `SeedScenarioAsync/SubmitDeckAsync/ReadyAsync/SubmitAsync` 加同一 private pure helper，共38行；RequirePlayer、缓存原响应/冲突仍在前，其余原方法与测试不改。
- 旧实现会把坏信封写成拒绝日志，结构恢复随后拒绝；原生 Mapper 的 `MALFORMED_PLAY_CARD` 也可能与 raw `PLAY_CARD` 不匹配。实际单房间 PostgreSQL 原红另确认 JSON null 存在于 jsonb，却被原读取器变成 nullable 缺值，热态 changed-payload 冲突在冷 registry 后丢失；合法 object 对照仍冲突。原8命令/12事件、原数据与七表不变性保留，专用54391实例已停止。
- 现在 uncached present raw 必须为object；顶层属性名规范且Ordinal唯一，非空trim cmdType精确绑定实际命令，finite seed wire显式为 `DEV_SEED_SCENARIO`。promptId允许缺省/null/空串，其余为规范string；snapshotTick缺省或非负Int64 number，不做字符串转换。坏请求返回既有 `INVALID_PAYLOAD`，不写日志/cache、不改变状态/tick/event，原intent可以纠正。
- canonical未知命令/stale拒绝继续留原审计；已有缓存冲突优先级不变。DTO、Mapper、恢复校验、SQL/事件/日志形状、IntentConflicts及全局normalizer不改；不重写旧raw、RuleInputs或接受历史。C# nullable-absent direct raw、所有命令payload等价及另一个冷拒绝ErrorCode字段契约仍独立OPEN。
- 原未修复42/8/10/4轮及错误新seed控制的v6源码/TRX均保留；v7只校正新合法seed热重复应返回原事件值的断言。五新测试共549行、四类9方法64例，共用真实普通出生、validator、完整原日志audit/replay和实际registry夹具，Hub认证真实、client/context仅记录传输。
- WIP新源码实际 **199/199、0skip**：64新增与135原相邻用例，96方法含原87方法；完整case名多重集与各原基线完全相同，122份正常native frame/audit/replay/registry均通过。WIP1114源输入、实际模块/MVID/RuleInputs单列；源码提交后另用同提交重建复测，不能把WIP身份当固定结果。
- 原件在本线ignored `var/qa/raw-envelope-ingress-20261024/`：`sql-null-original-proof-v17.json`、`adjacent-baseline-proof-v12.json`、`consolidated-tests-baseline-proof-v14.json`、`patched-wip-proof-v20.json`，以及各引用的真实source/TRX/SQL/runtime目录。后续固定提交收据由同目录交付；不生成全库历史证据包。
- 不混原A5/A6、N9-only后继或冻结B1；受影响SQL/API/真实wire/进程重启/security及主线当前目录组合仍由①后批固定Actions验收。本地单房间SQL诊断与定向通过不替代这些门禁，后端NOT READY、净整卡认证0、完整目标仍active。
