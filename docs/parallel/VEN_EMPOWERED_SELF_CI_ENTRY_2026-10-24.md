# N9 强化来源回手与真实揭示 CI 入口

- 基线 `f50e125b8dbd3b7b8c90c2e399e1f1a419b4515d`；沿用 [N9 三张精确印次来源/语义](VEN_EMPOWERED_SELF_INCREMENT_2026-10-24.md)，不新增卡牌或改强化算法。
- 已批准的 `BuildDevScenarioState` 两行只接 `ven-empowered-self-{043,050,070}`；独立 factory 再拒绝其他 suffix，经当前注册工厂构造，保留 current.RuleInputs、424242/turn5，050 有五枚横竖混合的己方场上符文。
- 每房间新 GUID、独立小写双玩家/认证/重连身份，从真实 SEATING 记录 DEV_SEED_SCENARIO；后续打出、强化、回手、蟹死亡揭示及关闭均为真实命令，不声称普通牌组开局，不改旧 accepted 历史。
- SQL case 建议 `ven-empowered-self` → `VenEmpoweredSelfSqlAcceptance.RunAsync(source, output)`：三卡×永久/结算后回手/结算前回手/真实揭示后关闭，逐前缀 SQL 冷恢复、双投影/prompt、旁观来源、拒绝不变性、幂等及显式 SEATING 原日志重放。
- 网络准备 `PrepareNetworkAsync(source, output)`；读取 `ven-empowered-self-network-fixtures.json` 的九个独立房间和 tokens/apiKeys，调用 `VenEmpoweredSelfNetworkAcceptance.RunAsync(url, output, false)`，真实认证/重连、报价/打出/强化/回手/蟹揭示，分别冻结公开、私有、真实揭示的待强化来源。
- 重启顺序：SQL `VerifyNetworkAsync(source, output, "before-restart")` → ①重启同一冻结 API 二进制 → 网络 `RunAsync(url, output, true)` → SQL `VerifyNetworkAsync(source, output, "after-restart")`；原双视图/prompt、代际1→2绑定和七表摘要须相同，末次 accepted 重复不新增日志。
- 篡改入口 `VerifyNetworkTamperAsync(source, output)`：仅本夹具最新行临时伪造对手来源/空栈/错误 RuleInputs/协调减一强化实付与余额，实际恢复注册表及适用原日志须拒绝；finally 精确恢复原 payload，七表摘要和原重放再验，不改 commands/events。
- 本机两个叶项目 build 均0警告0错误；同引擎输入定向 **51/51**；纯内存原配方 **12+9** 路径、210条原命令/459事件、258次 JSON 冷恢复、36项私有来源/空栈/输入/实付反例通过，未运行真实 PostgreSQL/网络/进程重启。
- 当前网络原断言另对固定九路径的72个命令前缀通过，三卡伪造来源各被来源/隐私两守卫拒绝，精确身份扫描8项校准；公开旧栈句柄子串不等于当前私有对象身份，原 FullGame 守卫及运行投影未修改。
- 本机实际输入 `68c174b22b5a673f0066a07b4ab184753e77df900fd433bf077f76af7a01856b`、目录 semantic `3bf8afc44964c6706be63503055fedb383004119d4d632c7803ca2bf9b8cd2f0`；原件 `var/qa/ven-empowered-self-ci-entry-20261024/` 的 memory-probe-v6/network-projection-audit-v7/final-consumer-focus-v8，首次编译/参数/预期错误及私有边界诊断保留。
- 待①接公共 Program/有序 SQL manifest/CI 身份与调度，并在 Actions 实跑 SQL、SignalR、同二进制重启和固定候选所需 check/full；当前 L2 入口源码交付不关闭这些门禁，净整卡认证0，后端及产品仍未完成。
