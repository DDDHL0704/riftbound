# Stage 4D P0/P1 Closure Plan

日期：2026-05-14
结论：**NOT READY**

本文是 Stage 4C representative evidence alignment 之后的 A 主控收口计划。4C-98 已把可用既有自动化证据对齐的 direct-card / battlefield residual `IMPLEMENTED_UNTESTED` 基本收完；剩余工作不应继续用 evidence-only overlay 推进，而应回到 P0/P1 规则模型、状态机和最终验收。

## 1. 输入事实

- 最新已提交证据基线：Stage 4C-98 battlefield residual evidence alignment。
- 当前矩阵计数：`IMPLEMENTED_TESTED=76`、`IMPLEMENTED_UNTESTED=4`、`NEEDS_ENGINE_SUPPORT=501`、`NEEDS_FAQ_REVIEW=128`、`SHARED_ORACLE_IMPLEMENTATION=102`。
- 剩余 `IMPLEMENTED_UNTESTED` 四项均已在 Stage 4C-95 判定为 design-gated static/trigger items：熔浆巨龙、娑娜、安妮、温驯的宝石龙。
- 当前 completion audit 仍列出 P0-002、P0-003、P0-004、P0-005 与 P1 LayerEngine / keywords / full-card evidence / replay-hash coverage。
- Formal 18-step E2E 已有连续正式主流程证据；它满足 A_MASTER 的主流程门槛，但不能替代 strict battlefield contest / battle lifecycle / PaymentEngine / LayerEngine 的 full official 收口。

4D-03AG focused slice 补充：`PLAY_CARD` typed resource prompt parity representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_EVIDENCE.md`；focused 454/454、backend full 4177/4177、`git diff --check` 通过。该补充只收口 `PLAY_CARD` typed optional power prompt metadata 的 representative gap，不关闭 P0-005、P1 或 READY。

4D-03AG handoff / baseline 补充：下一 PaymentEngine breadth 切片锁定 `PLAY_CARD` typed optional power costs 的 inline payment-resource prompt parity。交接规格见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_PLAY_CARD_TYPED_RESOURCE_PROMPT_BASELINE_EVIDENCE.md`。Raman 只读审计将 4D-03AF 的一般 verifier 建议收窄为更具体的 authoritative prompt / command typed payability gap；本切片仍不关闭 P0-005、P1 或 READY。

4D-03AF A 侧 audit 补充：PaymentEngine remaining-scope audit / baseline 已建立，审计与基线见 `docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_BASELINE_EVIDENCE.md`；focused baseline 587/587 通过。该补充确认 4D-03AE 后仍不能关闭 P0-005；下一步推荐补 `PLAY_CARD` / `PAY_COST` / `ASSEMBLE_EQUIPMENT` / `ACTIVATE_ABILITY` / `LEGEND_ACT` / battlefield held / trigger payment / `HIDE_CARD` / `MOVE_UNIT` 的 action-window coverage verifier。

4D-03AE focused slice 补充：pending `PAY_COST` / `TRIGGER_PAYMENT` temporary resource aggregate prompt parity representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_EVIDENCE.md`；focused 170/170、backend full 4173/4173、`git diff --check` 通过。该补充只收口 pending prompt aggregate legal-choice 口径，不关闭 P0-005、P1 或 READY。

4D-03AE handoff / baseline 补充：下一 PaymentEngine breadth 切片锁定 pending `PAY_COST` / `TRIGGER_PAYMENT` 对 `TEMP_PAYMENT_RESOURCE:*` aggregate prompt metadata 的 parity 修正。交接规格见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AE_PAYMENT_ENGINE_PENDING_TEMP_RESOURCE_PROMPT_BASELINE_EVIDENCE.md`；focused baseline 167/167、`git diff --check` 通过。该补充只建立 pending payment temporary prompt metadata 修正护栏，不关闭 P0-005、P1 或 READY。

4D-03AD focused slice 补充：SFD Fiora trigger temporary payment resource parity representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_EVIDENCE.md`；focused 149/149、backend full 4170/4170、`git diff --check` 通过。该补充只收口 SFD Fiora `TRIGGER_PAYMENT` 的 typed-yellow temporary payment-only resource parity，不关闭 P0-005、P1 或 READY。

4D-03AD handoff / baseline 补充：下一 PaymentEngine breadth 切片锁定 SFD Fiora `TRIGGER_PAYMENT` 对 `TEMP_PAYMENT_RESOURCE:*` 的 typed-yellow prompt / command / audit parity。交接规格见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AD_PAYMENT_ENGINE_TRIGGER_TEMP_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 137/137、`git diff --check` 通过。该补充只建立 trigger-payment temporary resource parity 的实现前护栏，不关闭 P0-005、P1 或 READY。

4D-03AC focused slice 补充：battlefield held temporary payment resource parity representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_EVIDENCE.md`；focused 221/221、backend full 4158/4158、`git diff --check` 通过。该补充只收口 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 的 temporary payment-only resource parity，不关闭 P0-005、P1 或 READY。

4D-03AC handoff / baseline 补充：下一 PaymentEngine breadth 切片锁定 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 对 `TEMP_PAYMENT_RESOURCE:*` 的 prompt quote / command commit / audit parity。交接规格见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AC_PAYMENT_ENGINE_BATTLEFIELD_HELD_TEMP_RESOURCE_BASELINE_EVIDENCE.md`；exploratory baseline 73/73、focused baseline 208/208、`git diff --check` 通过。该补充只建立 held-score temporary resource parity 的实现前护栏，不关闭 P0-005、P1 或 READY。

4D-03AA focused slice 补充：Image copy-token representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_EVIDENCE.md`；focused 16/16、adjacent 253/253、backend full 4136/4136、`git diff --check` 通过。该补充只收口 `UNL·T06` 映像当前对象模型 copy representative，保留 Brush battlefield replacement deferred，不关闭 P0/P1 或 READY。

4D-03AB focused slice 补充：Brush battlefield replacement representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_EVIDENCE.md`；focused 141/141、adjacent 511/511、Brush focused 8/8、backend full 4144/4144、`git diff --check` 通过。该补充清空 token deferred catalog，但只收口 `UNL·T03` 草丛当前对象模型 score-time replacement representative，不关闭 P0/P1 或 READY。

4D-03AB handoff / baseline 补充：下一服务端 token battlefield replacement 切片锁定 `UNL·T03` 草丛 `当你在此处得分时，你可以选择使用被此牌替代的战场来替代此牌`。交接规格见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_BASELINE_EVIDENCE.md`；focused baseline 141/141、`git diff --check` 通过。该补充只建立 Brush score-time replacement representative 的实现前护栏，不关闭 P0/P1 或 READY。

4D-03AA handoff / baseline 补充：下一服务端 token copy 切片锁定 `UNL·T06` 映像 `当我被打出时，变为某张卡牌的复制体`。交接规格见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_BASELINE_EVIDENCE.md`；focused baseline 11/11、`git diff --check` 通过。该补充只建立 Image copy-token representative 的实现前护栏，保留 Brush battlefield replacement deferred，不关闭 P0/P1 或 READY。

4D-03Z focused slice 补充：Baron Nest token battlefield static representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_EVIDENCE.md`；focused 77/77、adjacent 345/345、backend full 4131/4131、`git diff --check` 通过。该补充只收口 `UNL·T01` 男爵巢穴 no-ROAM destination-specific movement representative，保留 Image / Brush token surfaces deferred，不关闭 P0/P1 或 READY。

4D-03Z handoff / baseline 补充：下一服务端 token battlefield static 切片锁定 `UNL·T01` 男爵巢穴 `单位可从任意位置移动到此处`。交接规格见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_BASELINE_EVIDENCE.md`；focused baseline 66/66、`git diff --check` 通过。该补充只建立 Baron Nest no-ROAM destination-specific movement static 的实现前护栏，保留 Image / Brush token surfaces deferred，不关闭 P0/P1 或 READY。

4D-03Y focused slice 补充：battlefield deferred catalog representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_EVIDENCE.md`；focused 15/15、adjacent 641/641、backend full 4120/4120、`git diff --check` 通过。该补充只收口旧 P6 deferred catalog 误报，不关闭 P0/P1 或 READY。

4D-03Y handoff / baseline 补充：下一服务端 catalog hygiene 切片锁定 `P6BattlefieldEffectCatalog` 里四个已由 `BATTLEFIELD_RULE_DOMAIN` 实现的旧 deferred battlefield representatives。交接规格见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 15/15、adjacent baseline 641/641、`git diff --check` 通过。该补充只消除过期 deferred catalog 误报并保留 `ACTIVATE_ABILITY` blocked / battlefield positive 回归，不关闭 P0/P1 或 READY。

4D-03X focused slice 补充：legend action deferred catalog representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_EVIDENCE.md`；focused 59/59、adjacent 285/285、backend full 4120/4120、`git diff --check` 通过。该补充只收口旧 P6 deferred catalog 误报，不关闭 P0-005、P1 或 READY。

4D-03X handoff / baseline 补充：下一服务端 catalog hygiene 切片锁定 `P6LegendAbilityCatalog` 里五个已由 `LEGEND_ACT` 实现的旧 deferred legend representatives。交接规格见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 54/54、adjacent baseline 279/279 通过。该补充只消除过期 deferred catalog 误报并保留 `ACTIVATE_ABILITY` blocked / `LEGEND_ACT` positive 回归，不关闭 P0-005、P1 或 READY。

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

当前状态：**focused slice accepted / project NOT READY**。实现交接规格见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md`；实现前基线见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`；审计与证据见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`。Focused new tests 6/6、focused handoff regression 35/35、adjacent regression 127/127、backend full 3786/3786 通过。该切片收窄 P0-004，但不宣称 full-official resolved。

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

当前状态：**focused foundation accepted; 4D-03B through 4D-03Q focused slices accepted / project NOT READY**。Handoff / baseline 见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`；4D-03 foundation 审计与证据见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`。4D-03B handoff / baseline 见 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md`；4D-03B 审计与证据见 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`。4D-03C 审计与证据见 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`。4D-03D 审计与证据见 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`。4D-03E 审计与证据见 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`。4D-03F 审计与证据见 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`。4D-03G 审计与证据见 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`。4D-03H 审计与证据见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`。4D-03I 审计与证据见 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`。4D-03M 审计与证据见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`。4D-03N 审计与证据见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`。4D-03O 审计与证据见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`；4D-03P 审计与证据见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；4D-03Q 审计与证据见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`；4D-03Q handoff / baseline 见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede。4D-03P handoff / baseline 见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede。4D-03O handoff / baseline 见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede。4D-03N handoff / baseline 见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`，已被 focused slice 验收 supersede。实现前 focused payment baseline 51/51、adjacent payment / ActionPrompt / GameHub regression 240/240 通过；focused foundation 56/56、adjacent 245/245、backend full 3791/3791 通过；4D-03B focused 18/18、adjacent 318/318、backend full 3791/3791 通过；4D-03C focused 31/31、adjacent 363/363、backend full 3791/3791 通过；4D-03D focused 84/84、adjacent 257/257、backend full 3796/3796 通过；4D-03E focused 88/88、adjacent 290/290、backend full 3800/3800 通过；4D-03F focused 55/55、adjacent 233/233、backend full 3804/3804 通过；4D-03G focused 22/22、adjacent 224/224、backend full 3809/3809 通过；4D-03H focused 69/69、adjacent 242/242、backend full 3818/3818 通过；4D-03I focused 105/105、adjacent 317/317、backend full 3840/3840 通过；4D-03M focused 164/164、adjacent 335/335、backend full 3893/3893 通过；4D-03N focused 185/185、adjacent 369/369、backend full 3914/3914 通过；4D-03O focused 169/169、adjacent 396/396、backend full 3940/3940 通过；4D-03P focused 189/189、adjacent 685/685、backend full 3962/3962 通过；4D-03Q focused 239/239、adjacent 779/779、backend full 4003/4003 通过。该阶段建立 shared `PaymentPlan` / authorize / commit foundation，扩展到代表性非出牌窗口，扩展到 `PLAY_CARD` optional / extra / payment-resource 代表路径，让 Vi / Xerath / Renata / Crimson Rose / Shadow `ACTIVATE_ABILITY` 支付窗口支持 payment resource actions、typed blue costs、experience costs、target tax 和 stack-before-effect 语义，已将 `HIDE_CARD` 待命暗置支付窗口迁移到 shared plan / commit / audit 口径，将普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` payment resource action 接入同一 prompt quote / command commit / audit 口径，将 battlefield held score 的必要 `RECYCLE_RUNE:*` payment resource action 接入 shared plan / commit / audit 口径，将 SFD Fiora trigger payment resource action 接入 shared plan / commit / audit 口径，并将 Malzahar resource skill、Dragon Soul Sage reaction resource skill、Renata typed-blue draw、Renata typed-blue exhaust score、Crimson Rose ready-unit、Fluft Poro Warhawk token 与 Shadow swift stun 接入 prompt / command / stack / audit representative 口径；但不关闭 P0-004 或 P0-005 full official。

4D-03H handoff / baseline 补充：下一实现切片已锁定 `SFD·180/221` / `SFD·180a/221` 菲奥娜“友方单位变为强力后可支付黄色使其活跃”的 concrete trigger payment resource action。交接规格见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 55/55、adjacent baseline 233/233 通过。该补充只建立 B 侧实现交接，不代表功能完成，不关闭 P0-005。

4D-03H focused slice 补充：SFD Fiora trigger payment resource action 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`；focused 69/69、adjacent 242/242、backend full 3818/3818、`git diff --check` 通过。该切片只证明 concrete trigger payment resource representative 已接入 shared payment / resource-action 口径，不关闭 P0-005 full official。

4D-03I focused slice 补充：`OGN·113/298` 玛尔扎哈 `[A A]` resource skill open-main representative 已验收。审计与证据见 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`；focused 105/105、adjacent 317/317、backend full 3840/3840、`git diff --check` 通过。该补充只证明 resource skill representative 已接入服务端 prompt / command / audit 口径，不关闭 P0-005 full official。

4D-03J handoff / baseline 补充：下一实现切片锁定 Malzahar resource skill lifecycle，处理 4D-03I 残余的 spell-duel / swift timing、reaction prohibition 与 payment-only lifecycle。交接规格见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_BASELINE_EVIDENCE.md`；focused baseline 109/109、adjacent baseline 336/336 通过。该补充已被下方 4D-03J focused slice 验收 supersede，仍保留为回归护栏，不关闭 P0-005。

4D-03J focused slice 补充：Malzahar resource skill lifecycle representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`；focused 116/116、adjacent 340/340、backend full 3847/3847、`git diff --check` 通过。该切片只证明 spell-duel focus + temporary payment-only ledger representative 已接入服务端 prompt / command / audit 口径，不关闭 P0-005 full official。

4D-03K handoff / baseline 补充：下一实现切片锁定 temporary payment-only resource inline consumption。当前 `TEMP_PAYMENT_RESOURCE:*` 已服务 pending `PAY_COST`，但 `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` inline payment prompt / command 仍未消费 Malzahar temporary ledger。交接规格见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_BASELINE_EVIDENCE.md`；focused baseline 331/331、adjacent baseline 526/526 通过。该补充已被下方 4D-03K focused slice 验收 supersede，仍保留为实现前回归护栏；P0-005 仍未关闭。

4D-03K focused slice 补充：temporary payment-only resource inline representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`；focused 344/344、adjacent 539/539、backend full 3860/3860、`git diff --check` 通过。该切片只证明 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` temporary resource inline representative 已接入服务端 prompt / command / audit 口径，不关闭 P0-005 full official。

4D-03L handoff / baseline 补充：下一实现切片锁定 Dragon Soul Sage reaction-speed resource skill。当前 `UNL-093/219` 龙魂贤者官方文本 `{{反应>}} {{横置}}：{{获得}}{{1}}` 仍在 `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 中，只作为 deferred activated ability surface 审计；交接规格见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 126/126、adjacent baseline 374/374 通过。该补充已被下方 4D-03L focused slice 验收 supersede，仍保留为实现前回归护栏；P0-005 仍未关闭。

4D-03L focused slice 补充：Dragon Soul Sage reaction-speed resource skill 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_EVIDENCE.md`；focused 140/140、adjacent 388/388、backend full 3874/3874、`git diff --check` 通过。该切片只证明 `UNL-093/219` reaction resource skill representative 已接入服务端 prompt / command / audit 口径，不关闭 P0-005 full official。

4D-03M handoff / baseline 补充：下一实现切片锁定 Renata Glasc colored activated draw skill，处理 `SFD·088/221` / `SFD·088a/221` `支付{{1}}和{{蓝色}}：抽一张牌` 的 typed blue activated ability representative。交接规格见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_BASELINE_EVIDENCE.md`；focused baseline 144/144、adjacent baseline 316/316 通过。该补充只建立实现前回归护栏；P0-005 仍未关闭。

4D-03M focused slice 补充：Renata Glasc colored activated draw representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`；focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过。该切片只证明 typed-blue ordinary activated draw 已接入服务端 prompt / command / stack resolution / audit 口径，不关闭 Renata score、target-bearing abilities 或 P0-005 full official。

4D-03N handoff / baseline 补充：下一实现切片锁定 Renata Glasc colored activated score skill，处理 `SFD·088/221` / `SFD·088a/221` `支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分` 的 typed-blue exhaust activated ability representative。交接规格见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`；focused baseline 163/163、adjacent baseline 335/335 通过。该补充只建立实现前回归护栏；P0-005 仍未关闭。

4D-03N focused slice 补充：Renata Glasc colored activated score representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。该切片只证明 typed-blue exhaust ordinary activated score 已接入服务端 prompt / command / stack resolution / audit 口径，不关闭 target-bearing abilities、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03O handoff / baseline 补充：下一实现切片锁定 Crimson Rose ready-unit skill，处理 `UNL-109/219` 猩红玫瑰 `消耗3经验，{{横置}}：让一名单位变为活跃状态` 的 target-bearing equipment activated representative。交接规格见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`；focused baseline 143/143、adjacent baseline 370/370 通过。该补充只建立实现前回归护栏；P0-005 仍未关闭。

4D-03O focused slice 补充：Crimson Rose ready-unit representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`；focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过。该切片只证明 target-bearing equipment activated ready-unit 已接入服务端 prompt / command / stack resolution / audit 口径，不关闭 Crimson Rose 第一行触发、Fluft、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03P focused slice 补充：Fluft Poro Warhawk token representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过。该切片只证明 battlefield-only no-target token skill 已接入服务端 prompt / command / stack resolution / audit 口径，不关闭完整 `[A]` / `[C]` resource skill family、token-play breadth 或 P0-005 full official。

4D-03Q handoff / baseline 补充：下一实现切片锁定 Shadow swift stun，交接规格见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`；focused baseline 198/198、adjacent baseline 738/738 通过。该补充只建立实现前回归护栏，不代表 Shadow skill 已实现，不关闭 P0-004 或 P0-005。

4D-03Q focused slice 补充：Shadow swift stun representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`；focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过。该切片只证明 battle-response priority representative 已接入 prompt / command / stack resolution / audit 口径，不关闭 P0-004 battle lifecycle、完整 swift/reaction family 或 P0-005 full official。

4D-03R handoff / baseline 补充：下一实现切片锁定 `SFD·222/221` 暴怒之印 typed payment-only resource skill，交接规格见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 173/173、adjacent baseline 421/421 通过。该补充已被下方 focused slice 验收 supersede，仍保留为回归护栏，不关闭 P0-005。

4D-03R focused slice 补充：Rage Sigil typed resource representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_EVIDENCE.md`；focused 191/191、adjacent 439/439、backend full 4021/4021、`git diff --check` 通过。该切片只证明 typed red payment-only resource representative 已接入 prompt / command / temporary ledger / audit 口径，不关闭完整 Sigil family、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03S handoff / baseline 补充：下一实现切片锁定剩余五张 SFD Sigil typed payment-only resource skills（green / blue / orange / purple / yellow），交接规格见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 191/191、adjacent baseline 439/439 通过。该切片明确不实现 OGN Sigil resource skills，不关闭 P0-005。

4D-03S focused slice 补充：SFD Sigil typed resource family representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 213/213、adjacent 461/461、backend full 4043/4043、`git diff --check` 通过。该切片只证明剩余五张 SFD Sigil typed payment-only resource representatives 已接入 prompt / command / temporary ledger / audit 口径，不关闭 OGN Sigil、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03T handoff / baseline 补充：下一实现切片锁定 OGN 六张 Sigil typed payment-only resource skills，交接规格见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 213/213、adjacent baseline 461/461 通过。该切片只补完整 Sigil family representative，不关闭 P0-005。

4D-03T focused slice 补充：OGN Sigil typed resource family representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 238/238、adjacent 486/486、backend full 4068/4068、`git diff --check` 通过。该切片只证明 OGN 六张 Sigil typed payment-only resource representatives 已接入 prompt / command / temporary ledger / audit 口径，不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03U handoff / baseline 补充：下一实现切片锁定能量通道、远古簇碑、海克斯异常体三张 resource conversion equipment skills，交接规格见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_BASELINE_EVIDENCE.md`；focused baseline 209/209、adjacent baseline 464/464 通过。该切片只建立三张 conversion equipment 的实现护栏，不关闭 P0-005。

4D-03U focused slice 补充：resource conversion equipment representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md`；focused 230/230、adjacent 485/485、backend full 4089/4089、`git diff --check` 通过。该切片只证明能量通道、远古簇碑、海克斯异常体三张 reaction resource conversion equipment skills 已接入 prompt / command / rune pool / temporary ledger / audit 口径，不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

4D-03V handoff / baseline 补充：下一实现切片锁定 `UNL·T05` 与 `SFD·T03` 金币 equipment token resource / reaction ability，交接规格见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 264/264、adjacent baseline 758/758 通过。该切片只建立 Gold token destroy + exhaust + gain generic temporary payment-only rune resource 的实现护栏，当时不实现 Renata Gold extra mana bonus；该 bonus 已由 4D-03W supersede。

4D-03V focused slice 补充：Gold token resource skill representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_EVIDENCE.md`；focused 288/288、adjacent 782/782、backend full 4113/4113、`git diff --check` 通过。该切片只证明 `UNL·T05` / `SFD·T03` Gold token destroy + exhaust + gain generic temporary payment-only rune resource ability，当时不实现 Renata Gold extra mana bonus；该 bonus 已由 4D-03W supersede。

4D-03W handoff / baseline 补充：下一实现切片锁定 `RENATA_GOLD_EXTRA_1_MANA` marker 对 Gold token resource skill 的 +1 mana bonus，交接规格见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_HANDOFF.md`，实现前基线见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_BASELINE_EVIDENCE.md`；focused baseline 313/313、adjacent baseline 958/958 通过。该切片只建立 Renata near-victory Gold bonus 实现护栏，不关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family 或 P0-005。

4D-03W focused slice 补充：Renata Gold bonus representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_EVIDENCE.md`；focused 320/320、adjacent 965/965、backend full 4120/4120、`git diff --check` 通过。该切片只证明带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold token resource activation +1 mana，不关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official。

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
3. 当前 4D-03 focused foundation、4D-03B non-play focused slice、4D-03C play optional / extra focused slice、4D-03D `ACTIVATE_ABILITY` payment resource focused slice、4D-03E `HIDE_CARD` payment focused slice、4D-03F pending `PAY_COST` resource focused slice、4D-03G battlefield held score resource action focused slice、4D-03H trigger payment resource action focused slice、4D-03I Malzahar resource skill focused slice、4D-03J Malzahar lifecycle focused slice、4D-03K temporary resource inline focused slice、4D-03L Dragon Soul Sage reaction resource skill focused slice、4D-03M Renata colored activated draw focused slice、4D-03N Renata colored activated score focused slice 与 4D-03O Crimson Rose ready-unit focused slice 均已验收；4D-03P Fluft Poro Warhawk token focused slice、4D-03Q Shadow swift stun focused slice、4D-03R Rage Sigil typed resource focused slice、4D-03S SFD Sigil typed resource family focused slice、4D-03T OGN Sigil typed resource family focused slice、4D-03U resource conversion equipment focused slice、4D-03V Gold token resource skill focused slice 与 4D-03W Renata Gold bonus focused slice 已验收；下一步继续收窄完整 `[A]` / `[C]` resource skill family、remaining payment windows 或 typed resource quote parity。
4. 每个 4D 实现切片必须先给出写入范围、测试过滤器、不可并行文件和 no-go 声明，再进入代码修改。
5. A 不亲自写功能代码；只维护计划、派单、验收文档、测试验证和 completion audit。
