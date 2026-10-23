# N3/N9 真实出生与重连验收追补

- 前包基线4256→2127保留完整出生/只读七表/43守卫与已批测试依赖；本次基于 `2127fd7f4b269198094953c05ca9968f0c39e6ae`，只改两 Native 叶、共用 audit、新20项永久守卫与本说明，不改 production、公共 Program/manifest/阶段协议、旧历史或原 FullGame 来源。
- 两 SQL Context 均使用空 MatchSession → 两次真实 EnsurePlayerAsync → 第三条 DEV_SEED_SCENARIO，逐原前缀断言 Tick0/空 seats/实际 opening seed；保留真实返回 token，用显式 QA 身份定位 actor/peer，不用显示名推断身份或制造 ROOM_SEAT 行。
- Native 用共用状态接收器核每个 Joined 完整 room/player/RECONNECT envelope、真实 payload/新 token，以 TryAdd 拒绝重复；Error 还核 ERROR 类型。故意错误在 Invoke 前按 recipient/claimedPlayer/code 登记并等待恰好一次，额外/缺失/迟登记均失败，异常唤醒主 RPC/Joined/视图等待，故障 trace 保留。
- 共用只读 repeatable-read 采样保留：5游戏表 exact；matches 仅本房 updated_at、players 仅2身份 hash/updated_at 在实际重连 DB 时段可变，其余列按保留字段顺序/数字原文的 Ordinal JSON 比较，禁止1→1.0归一化；matches 时间==最后一条玩家会话事务时间，游戏视图语义 JSON 比较不改。
- API 原 intent duplicate 前后仍采样7表 exact；duplicate 后清空实时 snapshots/prompts，再向两人 RequestSnapshot，等待新到原 tick 的双视图并查共享失败状态。旧 resume 与恢复前后比较保留，重连窄例外不推断 duplicate 无写入，Native 继续使用同一专用 ConnectionStrings__Riftbound。
- 原 A2 `cbcf5c24` / run39998674123 / artifact12521512384 两份 N3 after trace 均零 Joined、各2个 INVALID_RECONNECT_TOKEN，原 PASS 不证明重连；本线逐字复制/核 hash并用当前共同守卫分别拒绝，原 ZIP/收据不改，①审计位于 actions-a2-reconnect-gate-audit-v1.json。
- 前包64/64＝43守卫+21原入座及时间窗红41/42原件保留；本次纯守卫 **63/63**＝20新 envelope/精确Error/数字表示+原43，0skip，保留数字表示原红18/20；两叶当前 build 均0警告0错误。本次不重跑旧出生/大套/真实网络或PG。
- N9 空房真实出生配方 **21/21**，252命令/501事件、300次 JSON 冷恢复、36项来源/空栈/输入/费用反例；N3 普通/Flow×3边界 **6/6**，60命令/116事件、66次冷恢复。均为纯内存/JSON/原日志，未冒称 PostgreSQL 或进程重启。
- 实际生产 Ensure/Reconnect/Restore 代码另在9个内存前缀完成36次令牌轮换、36次旧令牌拒绝及原 duplicate 不变性；内存 player store 不是 PostgreSQL。实际输入 `d9f1139b5f63d31ca087b4d4895b079f6958739fd6298c8dfadbb6c8e776c88f`，目录 semantic `3bf8afc4…`。
- 原件同 `var/qa/ven-empowered-self-ci-entry-20261024/`：前包v9/v11/v12/v13/v14/v15/v16保持；本次 envelope-exact-numeric-red-v18、final-envelope-error-exact-guards-v19、a2-full-envelope-negative-v20、final-envelope-leaf-build-v21；原A2两份文件逐字不变，当前异步接收器也分别拒绝。
- 待①：完整叶源码/依赖审阅并接入既有 Actions 固定候选，实跑真实 PG/双身份 SignalR/同二进制进程重启、逐阶段只读 SQL、拒绝/恢复/duplicate/tamper 与所需 check/full；本机未新增这些重验收，净整卡认证0，后端和产品仍未完成。
