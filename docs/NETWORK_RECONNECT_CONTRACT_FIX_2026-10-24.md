# C5/C6 与 C7：网络重连验收合同回修

- 基线 `9bc488e5`；仅两个 Native 叶、两个 SQL 叶、两份测试辅助类及 Native 同版 Npgsql 10.0.0 引用，不修改生产认证、令牌轮换、Program、阶段入口或已接受历史。C8 规则实例改动另包保留。
- 原失败：候选 `cbcf5c24d0c32baa9bd9e838f6e3daa2cb185ac1`，Actions `39998674123`，原网络 artifact `12521512384`。lotus/return/tokens 重启后均零 Joined、两个 INVALID_RECONNECT_TOKEN，但旧叶报绿；原 ZIP、receipt 和有效进程/快照/重复意图窄证据保留，不能认证成功重连。
- Native 每轮从各自真实 RECONNECT Joined 捕获新 PlayerSessionDto，等待双方会话和当前快照/提示。重启消费上一轮实际 token；分别保存 network-resume.json 与 network-resume-after-restart.json。成功 Joined 的房间、身份、类型、次数与新 token 均校验。
- Error 按具体接收者、提交身份、错误码预先登记；仅首轮故意错身份和过期提示各一次。INVALID_RECONNECT_TOKEN、额外/缺失/非预期 Error 均失败，不能靠后续 RequestSnapshot 绕过。
- 使用原 ConnectionStrings__Riftbound，仅 loopback/riftbound_acceptance；Native 在 READ ONLY / REPEATABLE READ 事务采样完整七表及数据库时间。新增辅助类须同时纳入① source-linked driver；Npgsql 与现 Persistence 同版，不改 framework。
- 重连逐行比较：五份日志/快照/提示表原文完全相同；matches 仅本房 updated_at，match_players 仅原两身份 token hash/updated_at。哈希须分别匹配消耗 token 与真实 Joined token，时间须处于数据库采样窗口，match 时间等于最后一次同事务玩家更新时间；其余列/身份/状态不变。
- API 重启本身在首次重连前比较原七表；重复意图在成功重连后独立采样，七表严格不变。纯原出生重放/registry 恢复、篡改恢复也分别对自己的实际基线严格比较七表，不沿用重连允许字段放行。
- 本机实际：两个叶工程及独立控件工程零警告/错误编译；已编译辅助类 57 控件通过，含原 A2 三份失败 trace、五表/身份/令牌/时间/其他列变异及严格七表 duplicate 控件。控件为合成数据和保留 trace，未执行真实 PG/API 重启验收。
- 原件：本工作树 ignored `var/qa/network-reconnect-contract-fix-20261024/`；`source-failure.json` 指向原 ZIP/hash；`guard-controls-v1/receipt.json`、源程序、build/run 日志与实际程序集/MVID保留。首个控件运行的参数错误单独留日志，不计语义失败或通过。
- 待①：组合固定候选并实际执行原 C5/C6 三房与 C7 四房双身份 PG/API/重启/原出生日志/篡改门禁；不把原绿、编译或这 57 控件提升为真实重连验收。净整卡认证增量 0，全部适用后端目标仍开放。
