# N9 强化来源回手与真实揭示 CI 入口

- 基线 `f50e125b8dbd3b7b8c90c2e399e1f1a419b4515d`；沿用 [N9 三张精确印次来源/语义](VEN_EMPOWERED_SELF_INCREMENT_2026-10-24.md)，不新增卡牌或改强化算法。
- 已批准的 `BuildDevScenarioState` 两行只接 `ven-empowered-self-{043,050,070}`；独立 factory 再拒绝其他 suffix，经当前注册工厂构造，保留 current.RuleInputs、424242/turn5，050 有五枚横竖混合的己方场上符文。
- 当前入口每房间新 GUID/小写双身份，由真实空房调用两次 EnsurePlayerAsync 记录两条 ROOM_SEAT，再记录第三条 DEV_SEED_SCENARIO；保存真实返回 token。4256 初版仅从预置两席 SEATING 开始，其旧证据不证明完整出生，修复见[出生/重连追补](VEN_NETWORK_RECONNECT_BIRTH_FIX_2026-10-24.md)。
- SQL case 建议 `ven-empowered-self` → `VenEmpoweredSelfSqlAcceptance.RunAsync(source, output)`：三卡×永久/结算后回手/结算前回手/真实揭示后关闭，逐前缀 SQL 冷恢复、双投影/prompt、旁观来源、拒绝不变性、幂等及显式 SEATING 原日志重放。
- 网络准备 `PrepareNetworkAsync(source, output)`；读取 `ven-empowered-self-network-fixtures.json` 的九个独立房间和 tokens/apiKeys，调用 `VenEmpoweredSelfNetworkAcceptance.RunAsync(url, output, false)`，真实认证/重连、报价/打出/强化/回手/蟹揭示，分别冻结公开、私有、真实揭示的待强化来源。
- 重启顺序：SQL `VerifyNetworkAsync(source, output, "before-restart")` → ①重启同一冻结 API 二进制 → 网络 `RunAsync(url, output, true)` → SQL `VerifyNetworkAsync(source, output, "after-restart")`；原双视图/prompt及代际1→2绑定须相同；重连单独核验真实 Joined/token 轮换，实际 duplicate 前后另取七表 exact 基线，详见追补，末次 accepted 重复不新增日志。
- 篡改入口 `VerifyNetworkTamperAsync(source, output)`：仅本夹具最新行临时伪造对手来源/空栈/错误 RuleInputs/协调减一强化实付与余额，实际恢复注册表及适用原日志须拒绝；finally 精确恢复原 payload，七表摘要和原重放再验，不改 commands/events。
- 初版51/51与v6的12+9配方/210命令/459事件保留原范围；完整出生修复另有21配方/252命令/501事件、300次 JSON 冷恢复与36项反例。当前构建/64项守卫及原入座回归见追补；本机未执行真实 PostgreSQL/网络/进程重启。
- 当前网络原断言另对固定九路径的72个命令前缀通过，三卡伪造来源各被来源/隐私两守卫拒绝，精确身份扫描8项校准；公开旧栈句柄子串不等于当前私有对象身份，原 FullGame 守卫及运行投影未修改。
- 初版输入 `68c174b2…` 及 v6/v7/v8 原件保留；新出生/令牌证据输入 `d9f1139b5f63d31ca087b4d4895b079f6958739fd6298c8dfadbb6c8e776c88f`，目录仍 `3bf8afc4…`；同 ignored 目录 true-birth-recipes-v9/token-boundary-proof-v12/final-guards-and-room-seats-v16，不跨输入复用。
- 待①接公共 Program/有序 SQL manifest/CI 身份与调度，并在 Actions 实跑 SQL、SignalR、同二进制重启和固定候选所需 check/full；当前 L2 入口源码交付不关闭这些门禁，净整卡认证0，后端及产品仍未完成。
