# Stage 4D P0/P1 Closure Plan

日期：2026-05-13
结论：**NOT READY**

本文是 Stage 4C representative evidence alignment 之后的 A 主控收口计划。4C-98 已把可用既有自动化证据对齐的 direct-card / battlefield residual `IMPLEMENTED_UNTESTED` 基本收完；剩余工作不应继续用 evidence-only overlay 推进，而应回到 P0/P1 规则模型、状态机和最终验收。

## 1. 输入事实

- 最新已提交证据基线：Stage 4C-98 battlefield residual evidence alignment。
- 当前矩阵计数：`IMPLEMENTED_TESTED=76`、`IMPLEMENTED_UNTESTED=4`、`NEEDS_ENGINE_SUPPORT=501`、`NEEDS_FAQ_REVIEW=128`、`SHARED_ORACLE_IMPLEMENTATION=102`。
- 剩余 `IMPLEMENTED_UNTESTED` 四项均已在 Stage 4C-95 判定为 design-gated static/trigger items：熔浆巨龙、娑娜、安妮、温驯的宝石龙。
- 当前 completion audit 仍列出 P0-002、P0-003、P0-004、P0-005 与 P1 LayerEngine / keywords / full-card evidence / replay-hash coverage。
- Formal 18-step E2E 已有连续正式主流程证据；它满足 A_MASTER 的主流程门槛，但不能替代 strict battlefield contest / battle lifecycle / PaymentEngine / LayerEngine 的 full official 收口。

## 2. 非目标

- 本计划不把任何代表性 evidence 升级为 full official。
- 本计划不修改功能代码、测试代码、前端代码或 coverage matrix。
- 本计划不允许实时抓取官网数据改变 2026-04-27 固定卡牌快照口径。
- 本计划不允许前端自行推断费用、目标、战场控制、战斗结果、法术对决焦点或胜负。
- 本计划不允许把四个 Stage 4C-95 design-gated FU 仅凭普通 source-unit fixture 入账。

## 3. Stage 4D 执行顺序

### 4D-01 Board Task Queue Foundation

阻断：P0-002、P0-003。

当前状态：**foundation accepted at `6a3ee038` / project NOT READY**。证据见 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`；focused 31/31、adjacent 149/149、backend full 3780/3780 通过。该状态只接受 4D-01 focused checklist，不把 P0-002/P0-003 升级为 full-official resolved。

目标：把现有 `BattlefieldState`、`BattlefieldTaskState`、`PendingCleanupTasks` 与 `PendingTaskQueue` 接入真正的 board task lifecycle，覆盖 standby、control/contest/conquer/hold、战场控制变化和状态性清理重复直到稳定。

详细实现交接规格：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md`。

建议 owner：B 服务端规则 / 协议 / 测试实现。

写入范围：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增窄域 conformance test file，但不得与其他任务并行改同一状态机文件。

验收：

- 基地到战场、战场到基地、战场间游走均同步 `ObjectLocations` 与 `BattlefieldStates`。
- 移入空战场、敌方控制战场、双方争夺战场后能稳定产生官方 task queue 状态。
- 待命容量、面朝下信息、reveal 后转移和非法待命清理均由服务端队列处理。
- 所有移动、进场、离场、伤害、战力变化、战场控制变化后都进入同一 cleanup loop。
- cleanup/task queue 阻塞期间 prompt 只允许等待或当前任务动作，手写普通命令必须 rejected 且 no mutation。
- focused tests、adjacent queue/battlefield tests、backend full test 通过。

### 4D-02 Spell Duel And Battle State Machine

阻断：P0-004。依赖：4D-01。

当前状态：**handoff ready / baseline green / project NOT READY**。实现交接规格见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md`；实现前基线见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`。Focused baseline 29/29、adjacent baseline 121/121 通过。该基线只说明既有代表路径绿色，不关闭 P0-004。

目标：把法术对决和战斗从 direct/minimal representative resolver 推进到由 task queue 创建、推进和关闭的官方状态机。

建议 owner：B 服务端规则 / 协议 / 测试实现。

写入范围：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- 可新增 spell-duel / battle lifecycle tests。

验收：

- 争夺战场创建 `START_SPELL_DUEL` 与 `START_BATTLE` task，而不是依赖 UI 手动猜测下一步。
- focus/pass、swift/reaction、initial stack、spell duel close、battle open/close 都挂在同一状态机。
- 多攻击者/多防守者、伤害分配、战斗清理、无战斗结果都能被 snapshot/prompt 表达并由服务端裁决。
- reconnect 后 pending task、focus、battle damage assignment 与 hidden info 不泄漏。
- focused lifecycle tests、adjacent battle/spell-duel tests、backend full test 通过。

### 4D-03 Payment Engine Unification

阻断：P0-005。可做只读设计并行；功能集成不得与 4D-01/4D-02 同时改 `CoreRuleEngine.cs` 的支付/状态机交叉区域。

目标：把 PLAY_CARD、MOVE_UNIT、ASSEMBLE_EQUIPMENT、ACTIVATE_ABILITY、LEGEND_ACT、battlefield trigger、keyword optional/extra cost 与 rune resource actions 统一到可回滚、可审计、服务端候选驱动的 PaymentEngine。

建议 owner：B 服务端实现，E 卡牌证据矩阵协助确认官方费用口径。

写入范围：

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- 支付相关 conformance fixtures / tests。

验收：

- 普通费用、任意符能、指定特性符能、额外/可选费用、替代费用、减费/加费、资源动作和支付失败回滚使用同一校验/扣费模型。
- `[A]`、支付步骤中的 `[C]` 资源技能、Haste、Echo、Spellshield tax 与代表性 legend/battlefield/skill 支付窗口不再各自复制费用逻辑。
- prompt 中的 `sourceRequirements` / payment choices 与命令侧实际可支付性一致。
- 支付失败保持 hand/stack/runePool/objectLocations 等权威状态不变。
- focused payment tests、adjacent ActionPrompt/GameHub tests、backend full test 通过。

### 4D-04 LayerEngine And Keyword Full-Pass Track

阻断：P1-001、P1-002、P1-003。

目标：在 P0 task/payment primitives 稳定后，引入完整 continuous effect / layer / timestamp / dependency 重算，并把 keywords 与 representative card effects 逐步提升到 full official evidence。

建议 owner：B 服务端实现，E full-card matrix / FAQ evidence 协助。

写入范围：

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- 可能新增 LayerEngine 相关源文件与测试文件。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 仅在有自动化 full-official 证据后更新。

验收：

- basePower / effectivePower / keyword / rule text 不再靠永久改写对象当前数值表达所有连续效果。
- 多装备、静态光环、控制权变化、失去/获得关键词按层、时间戳和依赖重算。
- KeywordCoverageReporter 不再只说明 delegated/deferred，而要对应真实执行矩阵。
- 逐卡矩阵只能在有 focused + adjacent + backend full 证据后升级。

### 4D-05 Frontend Authority And Chrome Gate

阻断：前端最终验收。依赖：4D-01 至 4D-03 的 snapshot/prompt 契约稳定。

建议 owner：C 前端契约 / Web UI / Chrome smoke。

验收：

- UI 只渲染服务端 authoritative snapshot、`ActionPrompt`、`sourceRequirements`、legal target/payment choices。
- battle/spell-duel/payment/cleanup task UI 不自行推断下一步动作。
- 所有玩家可见页面继续过滤 raw enum、object id、task id、hidden card details、fixture/debug text。
- `npm run build`、Chrome smoke、formal 18-step E2E、隐藏信息长链路 smoke 通过。

### 4D-06 Final Evidence And Completion Audit

阻断：active goal complete。

建议 owner：D 文档审计，E card matrix，A 最终验收。

验收：

- `docs/CURRENT_SERVER_RULE_AUDIT.md` 中 P0-002/P0-003/P0-004/P0-005 不再是 RISKY/PARTIALLY RESOLVED。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 有明确 full-official 口径、未实现口径和 FAQ review 口径。
- backend full、frontend build、Chrome smoke、formal 18-step E2E、隐藏信息长链路、replay/hash audit 均在最终 HEAD 通过。
- `docs/CURRENT_COMPLETION_AUDIT.md` 才能从 **NOT READY** 改为 READY。
- 只有上述全部满足后，A 才允许调用 goal completion。

## 4. 写锁和并行规则

- 4D-01 与 4D-02 都会触碰 `MatchSession.cs` / `CoreRuleEngine.cs`，不得并行实现。
- 4D-03 可以先做只读费用模型设计；一旦进入 `CoreRuleEngine.cs` 集成，必须等 4D-01/4D-02 写锁释放。
- 4D-04 可以先做文档与 API 草案；功能实现必须等 P0 task/payment primitives 稳定。
- 4D-05 前端只能在服务端 prompt/snapshot 合同稳定后实现产品化显示，不得先用 UI 本地推断补洞。
- 4D-06 矩阵更新只能跟随已通过的自动化证据；禁止先改状态再补测试。

## 5. A 主控下一步

1. 维持 NOT READY 结论。
2. 不再为 Stage 4C representative evidence alignment 追加新批次，除非出现遗漏的已验证自动化证据。
3. 当前已进入 4D-02 handoff 阶段；下一步复用 B / Maxwell 实现 spell duel / battle task-driven lifecycle。
4. 每个 4D 实现切片必须先给出写入范围、测试过滤器、不可并行文件和 no-go 声明，再进入代码修改。
5. A 不亲自写 4D-02 功能代码；只维护计划、派单、验收文档、测试验证和 completion audit。
