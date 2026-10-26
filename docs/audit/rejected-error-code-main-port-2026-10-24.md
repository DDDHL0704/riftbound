# 拒绝结果错误码的恢复保真 · 2026-10-24

- Refs issue155；父PR153/c9f2f57e，代码378dc39c224b48742d42232c48bfb043260f29e8。缺陷是冷重复将原PROMPT_EXPIRED/INVALID_DECK/INVALID_TARGET统一变UNSUPPORTED_COMMAND；原状态与资源未重复消耗，不能用后者证明错误码一致。
- MatchJournalEntry与RecoveredCommand新增required nullable ErrorCode，JsonIgnore Never保证显式null存在；不猜测、白名单、trim或fallback。六个生产记录点捕获实际result或promptRejection.ErrorCode。
- 已有command_log.payload记录errorCode，无新列/DDL；实际reader只接受精确成员的string/null，缺失或错型RECOVERY_INCONSISTENT。旧历史、raw、events、RuleInputs及冻结DLL不回填或改写。
- 冷缓存恢复原值，仍返回当前恢复状态/视图及空冷事件；温缓存原响应事件策略不变。原日志replayer独立Ordinal比较实际ErrorCode，保留acceptance/message/tick/events/final-hash/identity全部检查；registry按现有完整审计后恢复。
- Roslyn定位173处真实逐字段投影、168文件，仅补复制实际ErrorCode；另三处加强等码断言。逆转所列插入后逐文件等于c9，原方法、Theory行、语义断言及完整对局语料保留。112处手造/非标准构造未冒充实际值迁移。
- 父源四例0P4F，普通出生三条与有限QA指令一条均实际registry复现丢码；原方法/四行断言保留。最终新34/34、非SQL相邻原2244/2244，共2278/2278；同一不可变DLL两份TRX共2297 passed/0skip，其中19条条件SQL smoke无连接而直接返回，排除数据库证据及活动例数。
- 新验证包含字段缺失/错JSON类型、IgnoreNull下显式null、真实Postgres writer/reader内存codec、冷热重复与changed-raw冲突、状态/视图/资源/日志不重复写入。三个只改代码的伪造保持state/events/raw/message/identity，普通结构通过、独立replay及实际registry拒绝；原现场仍可恢复。
- 生产六文件37+/8−；其他既有源码只有元数据搬运及三条新断言。GameCommand、客户端结果DTO、MatchState、RuleChoice、clock、RuleInputs算法不改；Frame.Commands子记录的新增required字段属于本次公开恢复契约。
- 原件根`var/qa/payment-prs-20261024/rejected-error-code/`，fixed-source-runtime-proof-v6.json SHA `0ca0abc2249ff4df6ca004c69798e39bce558f857fd41a362a2fd64bf3cd6c04`；implemented-contract-v5.json、projection-audit-v2与test-source-retention-v3.json可逐项复查。
- native Info保留c9+WIP，3044个编译输入精确等于378源码；实际加载路径/SHA/MVID/RIF均保存。v3新夹具误把服务器保留ROOM_SEAT intent当客户端重复，原31P3F保留，修正为真实SubmitDeck控制，不放松保留intent守卫。
- 本轮无真实SQL/API/网络/进程重启或完整对局矩阵；codec不冒充DB。实际新L2规则CI、main集成及夜间固定main的L3仍待，净整卡认证增量0，完整Goal active。
- 2026-10-26 组合当前153 `4d069ab7b251934dbcf2e5ff7545736fcc1ad3df`（父136/9f4及#173 main/d1e）。167文件的邻接冲突逐项保留真实ErrorCode与TypedCommand；170处测试冲突的两侧实际来源计数均保留，生产五个客户端日志点及ROOM_SEAT共六处记录实际结果。组合后的六个生产文件，忽略已存在TypedCommand表达式后，ErrorCode新增/删除行逐项等于原157发布增量。解析不猜码，回放保留两种元数据守卫。
- 同时发现当前实际Postgres writer的payload未包含entry.TypedCommand；reader虽读取该成员，原无raw请求的typed身份没有对应写入。仅在实际AddCommandPayload中追加该字段，与ErrorCode同时保留，无新列/DDL或历史回填。新增真实Core普通房间的接受/拒绝×有/无raw四行codec/完整原日志/registry/冷重试测试，原全部34行不变。保留修复前提交 `c0e5d7edddc3bcd4b666648da8298ca8b43d804d` 及源证明 `typed-writer-pre-fix-source-v9.json`；统一磁盘清理期间只完成源码，实际前后运行和新输入L2待执行，原2278结果仍归378固定源，不重标当前组合。
