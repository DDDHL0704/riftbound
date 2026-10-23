# N3 普通/Flow 临时强攻 CI 入口

- 来源/语义：VEN·012 / CN37697，沿用 [N3 来源收据](VEN_FLOW_READY_ASSAULT_INCREMENT_2026-10-23.md)；普通与 Flow 均实付3法力/1红色能量，重置并暂获强攻3，存储战力仍4；普通入废牌，Flow 放逐。
- 基线：`f11f457772fe67491bb0ac7b8e58a80f565d2882`，本追补仅新增验收类、当前 QA 夹具及已批准的 `BuildDevScenarioState` 两行分派；不含 N4/N5 注册或机制修改，可独立摘取。
- 夹具：每局新 GUID 房间/小写玩家命名空间/独立认证与重连凭据；当前从真实空房用两次 EnsurePlayerAsync 记录 ROOM_SEAT，再记录第三条 `DEV_SEED_SCENARIO:ven-flow-combat-{normal,flow}`；真实返回 token 随后轮换。[出生/重连追补](VEN_NETWORK_RECONNECT_BIRTH_FIX_2026-10-24.md)保留旧预置两席初态证据的边界。精确已注册印次经工厂构造，保留当前 RuleInputs；显式中局余额，不声称普通牌组开局，不重写旧历史或制造普朗克。
- SQL case id：`ven-flow-combat-keywords` → `VenFlowCombatKeywordSqlAcceptance.RunAsync(source, output)`；普通/Flow×到期/结算后回手/结算前换代，逐前缀实际持久化、注册表冷恢复、双方投影/prompt、拒绝不变性、幂等及原日志审计。
- 网络顺序1：同一个独立网络输出目录调用 `PrepareNetworkAsync(source, output)`；`VenFlowCombatKeywordNetworkAcceptance.RunAsync(url, output, false)`，实际双身份认证/重连、来源 prompt、报价、施放、两次优先权传递及旧 prompt 拒绝。
- 网络顺序2：`VerifyNetworkAsync(source, output, "before-restart")`；①停止并重启同一冻结 API 二进制；网络 `RunAsync(url, output, true)`；SQL `VerifyNetworkAsync(source, output, "after-restart")`，比较双方原视图/prompt；单独验证 Joined/token 与精确允许的重连行变化，在实际 duplicate 前后采样七表 exact 基线，重复末次 accepted intent 不新增日志。
- 篡改入口：`VerifyNetworkTamperAsync(source, output)`；临时改变本夹具最新持久快照的强攻/红色余额，独立原命令/事件回放和实际恢复注册表均须拒绝，finally 恢复原 JSON，七表摘要及完整原回放再验；不改 accepted 命令/事件。
- 初版内存6/6、48命令/104事件及输入 `85244629…` 保留预置两席初态范围；新完整出生6/6、60命令/116事件、66次 JSON 冷恢复，输入 `d9f1139b…`。A2 两份原 after trace 的零 Joined/无效令牌已被新守卫拒绝；本机未执行真实 PG/API 重启。
- 原件：本线 ignored `var/qa/ven-flow-ci-entry-20261024/{build-v2,memory-probe-v1,probe}/`；保留首次编译失败及修正后日志，内存原 journal 按六路径保存。
- 待①：Program/有序 SQL manifest/共享 CI 调度与实际 GitHub Actions SQL、SignalR、进程重启、固定候选 check/full；本机未执行这些重型门禁。风险L2/保守完整CI，净整卡认证0，后端及产品仍未完成。
