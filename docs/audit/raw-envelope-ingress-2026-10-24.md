# 原始wire信封在日志前校验
- [Issue115](https://github.com/DDDHL0704/riftbound/issues/115)。直接堆叠PR依赖#121的目标入站校验d9a53da2，仅移植b9ba79cc的七文件增量；四入口SeedScenarioAsync/SubmitDeckAsync/ReadyAsync/SubmitAsync及同一private helper共38行。
- uncached present raw必须是object；规范且Ordinal唯一的顶层属性名、与实际命令精确匹配的cmdType、可选规范promptId及非负Int64 snapshotTick。RequirePlayer及已有缓存原响应/冲突仍优先；坏请求INVALID_PAYLOAD且不写日志/cache、不改状态/tick/event，同intent可纠正。
- 原JSON null存在于jsonb却在冷恢复变成nullable缺值、造成冲突语义丢失的真实单房间SQL红/合法object对照保留于var/qa/raw-envelope-ingress-20261024/sql-null-original-proof-v17.json；本PR修入口，不改SQL读取、Mapper、恢复DTO、日志或旧RuleInputs。
- 新四类9方法64例覆盖普通出生、入站/Hub/seed/缓存和真实registry正控；原相邻87方法及135例列表保留。本主线堆叠分支的固定源码定向/相邻结果、原始请求、源输入与实际程序集身份在var/qa/payment-prs-20261024/issue115/；旧199例结果只作历史。
- C# nullable-absent typed raw、payload与typed等价、冷拒绝ErrorCode字段仍独立OPEN；本修复不宣称整个入口契约关闭。最新人类约定以定向及实际规则套件CI构成L2，真实网络/数据库/重启L3归夜间固定main SHA。
