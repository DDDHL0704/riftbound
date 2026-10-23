# N3/N9 真实出生与重连验收追补

- 基线 `4256ebb585f9ff7125c76f42a9f59f483f031bb6`；只修 N3/N9 测试叶、共同只读采样/出生断言、43项永久守卫、3个已批测试工程链接/依赖及短文档；不改 production、公共 Program/manifest/阶段协议、旧命令/事件或原 FullGame 来源。
- 两 SQL Context 均使用空 MatchSession → 两次真实 EnsurePlayerAsync → 第三条 DEV_SEED_SCENARIO，逐原前缀断言 Tick0/空 seats/实际 opening seed；保留真实返回 token，用显式 QA 身份定位 actor/peer，不用显示名推断身份或制造 ROOM_SEAT 行。
- Native 每身份必须收到真实 Joined/PlayerSessionDto 和新 token，再等双视图/prompt；恢复阶段读取前阶段真实 token。零 Joined、旧 token、错误身份及所有非预期 Error 立即失败，故障 trace 单独保留；原别名、费用、技能、隐藏信息与 stale/forbidden 断言保留。
- 共用 VenEmpoweredSelfSqlAudit 仅在专用 loopback riftbound_acceptance 做只读 repeatable-read 采样：5个游戏表完全相同；matches 仅本房 updated_at、match_players 仅两身份 token hash/updated_at 可在实际重连 DB 时段变化，其余列逐行全同，matches 时间还须等于最后一条玩家会话事务时间。
- API 原 intent duplicate 前实际采样7表基线，之后逐表 exact；恢复自身、forbidden peer、篡改 finally 恢复也不放宽7表。重连前/后的允许例外不用于推断 duplicate 无写入；SQL 叶复查 Native 采样及当前真实 SQL，旧调用顺序不变，Native 需同一既有 ConnectionStrings__Riftbound。
- 原 A2 `cbcf5c24` / run39998674123 / artifact12521512384 两份 N3 after trace 均零 Joined、各2个 INVALID_RECONNECT_TOKEN，原 PASS 不证明重连；本线逐字复制/核 hash并用当前共同守卫分别拒绝，原 ZIP/收据不改，①审计位于 actions-a2-reconnect-gate-audit-v1.json。
- 本机定向 **64/64**＝43新守卫+21原 RoomSeatJournal；保留时间窗内无关房间 touch 的原红 **41/42**，补事务时间对应后通过，未删除原例。两验收项目最终 build 均0警告0错误。
- N9 空房真实出生配方 **21/21**，252命令/501事件、300次 JSON 冷恢复、36项来源/空栈/输入/费用反例；N3 普通/Flow×3边界 **6/6**，60命令/116事件、66次冷恢复。均为纯内存/JSON/原日志，未冒称 PostgreSQL 或进程重启。
- 实际生产 Ensure/Reconnect/Restore 代码另在9个内存前缀完成36次令牌轮换、36次旧令牌拒绝及原 duplicate 不变性；内存 player store 不是 PostgreSQL。实际输入 `d9f1139b5f63d31ca087b4d4895b079f6958739fd6298c8dfadbb6c8e776c88f`，目录 semantic `3bf8afc4…`。
- 原件 `var/qa/ven-empowered-self-ci-entry-20261024/`：true-birth-recipes-v9、a2-original-flow-reconnect-v11、token-boundary-proof-v12、permanent-guards-red-v13、n3-birth-proof-v14、final-birth-token-build-v15、final-guards-and-room-seats-v16；旧4256/v6源码/二进制/日志原件保留，新增探针各用独立目录。
- 待①：完整叶源码/依赖审阅并接入既有 Actions 固定候选，实跑真实 PG/双身份 SignalR/同二进制进程重启、逐阶段只读 SQL、拒绝/恢复/duplicate/tamper 与所需 check/full；本机未新增这些重验收，净整卡认证0，后端和产品仍未完成。
