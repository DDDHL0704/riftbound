# 符文战场当前 Completion Audit

审计日期：2026-05-14
审计结论：**NOT READY**

本文件是 active goal 的当前收口审计清单，不代表最终完成验收。只有当本文所有 P0/P1 阻断清零、后端 full test、前端 build、Browser smoke / E2E 与隐藏信息检查全部通过后，才允许把 goal 标记为 complete。

## 0. 2026-05-13 最新状态补充

当前最新 Stage 4D 状态：4D-03 PaymentEngine focused foundation 已验收，4D-03B non-play payment focused slice 已验收，4D-03C play optional / extra payment focused slice 已验收，4D-03D `ACTIVATE_ABILITY` payment resource focused slice 已验收，4D-03E `HIDE_CARD` payment focused slice 已验收，4D-03F pending `PAY_COST` resource focused slice 已验收；项目整体仍 **NOT READY**。本批 4D-03F 实现提交信息：`feat: extend stage 4D pay cost resource actions`；4D-03F handoff checkpoint：`cfa14482 docs: prepare stage 4D pay cost resource handoff`；上一 4D-03E 实现提交信息：`feat: extend stage 4D hide card payment plans`；上一 4D-03E handoff checkpoint：`759834a1 docs: prepare stage 4D hide card payment handoff`；上一 4D-03D 实现提交信息：`feat: extend stage 4D activate resource payment plans`；上一 4D-03D handoff checkpoint 提交信息：`docs: prepare stage 4D activate resource payment handoff`；上一 4D-03C 实现提交：`be256be6 feat: extend stage 4D play optional payment plans`；上一 4D-03C handoff checkpoint：`abc93230 docs: prepare stage 4D play optional payment handoff`；上一 4D-03B 实现提交：`642bfed5 feat: extend stage 4D non-play payment plans`；上一 4D-03 foundation 提交：`8a063940 feat: add stage 4D payment engine foundation`；上一 4D-03B handoff checkpoint：`243c72db docs: prepare stage 4D non-play payment handoff`；上一 4D-02 实现证据为 `30210e38 feat: tighten stage 4D spell duel battle tasks`。上一 Stage 4C checkpoint 为 `7a2b1fa3 checkpoint: record stage 4C battlefield residual evidence alignment`；上一 active guard checkpoint 为 `4c06189 checkpoint: add active start battle guard tests`；上一 formal 18-step checkpoint 为 `3aed179 checkpoint: add formal 18 step e2e evidence`。Stage 4C-85 `炽烈符文` / `OGN·007/298` / `FU-0ec69ae7e6` 与 `翠意符文` / `OGN·042/298` / `FU-39041f4562` 已完成代表性 `RUNE_RESOURCE_DOMAIN` payment-resource evidence-only overlay 与验证。2026-05-13 另新增 formal 18-step E2E 脚本证据，房间 `formal-18-1778623926434-15` 已在同一连续正式对局中通过官方卡组、起手、首回合出牌、结算链、单位移动、重连、P2 战场得分、投降与结果页胜者展示；active `START_BATTLE` guard test-only evidence 已固化争夺战场战斗任务 prompt / command no-mutation 边界；Stage 4C-95 新增 static effect design gate，确认熔浆巨龙、娑娜、安妮与温驯的宝石龙不能 evidence-only 入账；Stage 4C-96 新增 legacy guard evidence alignment；Stage 4C-97 新增 arena / minion / Annie evidence alignment；Stage 4C-98 新增 battlefield residual evidence alignment，将 3 个战场 FU 对齐为 representative `IMPLEMENTED_TESTED`；Stage 4D P0/P1 closure plan、4D-01 handoff、baseline evidence 与 4D-01 board task queue foundation evidence 已建立并验收。4D-01 focused 31/31、adjacent 149/149、backend full 3780/3780 通过。Stage 4D-02 focused new tests 6/6、focused handoff regression 35/35、adjacent regression 127/127、backend full 3786/3786 通过；该切片收窄 P0-004，但不关闭 full official battle lifecycle。Stage 4D-03 PaymentEngine focused foundation 新增 shared `PaymentPlan` / authorize / commit helper、PLAY_CARD / PAY_COST / ASSEMBLE_EQUIPMENT 代表接入、transactional `RECYCLE_RUNE:*` rollback tests 与 plan audit payload；focused 56/56、adjacent 245/245、backend full 3791/3791 通过。Stage 4D-03B 将 Vi / Xerath `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 代表窗口接入 shared payment plan / commit；focused 18/18、adjacent 318/318、backend full 3791/3791 通过。Stage 4D-03C 将 `PLAY_CARD` Haste / Echo / Spellshield / experience / payment-resource 代表路径的 affordability preflight 与 `COST_PAID` audit metadata 进一步对齐到 shared payment plan 口径；focused 31/31、adjacent 363/363、backend full 3791/3791 通过。Stage 4D-03D 将 Vi / Xerath `ACTIVATE_ABILITY` 支付窗口的 `RECYCLE_RUNE:*` payment resource action 接入 prompt quote / command commit / audit 口径；focused 84/84、adjacent 257/257、backend full 3796/3796 通过。Stage 4D-03E 将 `HIDE_CARD` 标准待命、Teemo 替代待命与免费待命迁移到 shared payment plan / commit / audit 口径；focused 88/88、adjacent 290/290、backend full 3800/3800 通过。Stage 4D-03F 将普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` payment resource action 接入 shared payment plan / prompt quote / command commit / audit 口径；focused 55/55、adjacent 233/233、backend full 3804/3804 通过。该阶段继续收窄 P0-005 foundation，不关闭 full official PaymentEngine。项目整体仍 **NOT READY**。

2026-05-13 4D-03G 补充：上段 “当前最新” 中 4D-03F 口径已被本轮 4D-03G focused slice supersede。battlefield held score 的必要 `RECYCLE_RUNE:*` payment resource action 已接入 shared `PaymentPlan` / `TryCommitPayment` / audit 口径；focused 22/22、adjacent 224/224、backend full 3809/3809 与 `git diff --check` 通过。该补充仍只收窄 P0-005，不关闭 full official PaymentEngine；项目整体仍 **NOT READY**。

2026-05-13 4D-03H 补充：SFD Fiora trigger payment resource focused slice 已验收，目标为 `SFD·180/221` / `SFD·180a/221` 菲奥娜“友方单位变为强力后可支付黄色使其活跃”的 concrete trigger payment resource action。入口为 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`；focused 69/69、adjacent 242/242、backend full 3818/3818、`git diff --check` 通过。该补充只收窄 P0-005，不关闭 full official PaymentEngine；项目整体仍 **NOT READY**。

2026-05-14 4D-03I 补充：Malzahar resource skill focused slice 已验收，目标为 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill，承接 Stage 4C-88 design gate 与 P0-005 剩余 resource skill 缺口。入口为 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`；focused 105/105、adjacent 317/317、backend full 3840/3840、`git diff --check` 通过。该补充只验收 open-main representative resource skill，不关闭 full official PaymentEngine、完整 `[A]` / `[C]`、spell-duel / swift timing、reaction prohibition 或 payment-only lifecycle；项目整体仍 **NOT READY**。

2026-05-14 4D-03J 补充：Malzahar resource skill lifecycle handoff / baseline 已建立，目标为 4D-03I 后续的 spell-duel / swift timing、reaction prohibition 与 payment-only lifecycle。入口为 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_BASELINE_EVIDENCE.md`；focused baseline 109/109、adjacent baseline 336/336 通过。该补充已被下方 4D-03J focused slice 验收 supersede，仍保留为回归护栏；项目整体仍 **NOT READY**。

2026-05-14 4D-03J focused slice 补充：Malzahar resource skill lifecycle representative 已验收，入口为 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`；focused 116/116、adjacent 340/340、backend full 3847/3847、`git diff --check` 通过。该补充只验收 spell-duel focus representative、immediate no-stack reaction prohibition representative 与 temporary payment-only ledger，不关闭完整 `[A]` / `[C]` resource skill family、inline payment-window temporary resource consumption、reaction/counter full target-filter model 或 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03K 补充：temporary resource inline focused slice 已验收，目标为 4D-03J 后续的 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` inline payment-window consumption。入口为 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`；focused 344/344、adjacent 539/539、backend full 3860/3860、`git diff --check` 通过。该补充只验收 temporary payment-only resource inline representative，不关闭 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03L 补充：Dragon Soul Sage reaction resource skill handoff / baseline 已建立，目标为 `UNL-093/219` 龙魂贤者 `{{反应>}} {{横置}}：{{获得}}{{1}}` representative。入口为 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 126/126、adjacent baseline 374/374 通过。该补充已被下方 4D-03L focused slice 验收 supersede，仍保留为实现前回归护栏；项目整体仍 **NOT READY**。

2026-05-14 4D-03L focused slice 补充：Dragon Soul Sage reaction resource skill representative 已验收，入口为 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_EVIDENCE.md`；focused 140/140、adjacent 388/388、backend full 3874/3874、`git diff --check` 通过。该补充只验收 `UNL-093/219` reaction-speed resource skill representative，不关闭 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03M 补充：Renata Glasc colored activated draw handoff / baseline 已建立，目标为 `SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 `支付{{1}}和{{蓝色}}：抽一张牌` representative。入口为 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_BASELINE_EVIDENCE.md`；focused baseline 144/144、adjacent baseline 316/316 通过。该补充只建立实现前回归护栏，不代表功能完成，不关闭 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03M focused slice 补充：Renata Glasc colored activated draw representative 已验收，入口为 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`；focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过。该补充只验收 `SFD·088/221` / `SFD·088a/221` open-main typed-blue ordinary activated draw，不关闭 Renata score、target-bearing abilities、完整 resource skill family 或 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03N 补充：Renata Glasc colored activated score handoff / baseline 已建立，目标为 `SFD·088/221` / `SFD·088a/221` 烈娜塔·戈拉斯克 `支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分` representative。入口为 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`；focused baseline 163/163、adjacent baseline 335/335 通过。该补充只建立实现前回归护栏，不代表功能完成，不关闭 P0-005 full official；项目整体仍 **NOT READY**。

2026-05-14 4D-03N focused slice 补充：Renata Glasc colored activated score representative 已验收，入口为 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。该补充只验收 `SFD·088/221` / `SFD·088a/221` open-main typed-blue exhaust ordinary activated score，不关闭 target-bearing abilities、完整 resource skill family 或 P0-005 full official；项目整体仍 **NOT READY**。

4C-85 不修改功能代码，只把既有服务端权威符文资源域证据入账：官方符文卡映射到 non-play `RUNE_RESOURCE_DOMAIN`，不进入 direct `PLAY_CARD` registry；控制者基地符文通过服务端 `RECYCLE_RUNE` / `paymentResourcePowerByChoice` 暴露 trait/power 支付资源；typed `SPEND_POWER:red:2` 接受 red 资源并拒绝 blue 资源，generic `SPEND_POWER:2` 可接受 red / blue 任一服务端候选且防止过量回收。Focused / primary regression 命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub|FullyQualifiedName~P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub"
```

结果为 passed，10 passed / 0 failed / 10 total。追加回归 `FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~PayCostWindow|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub` 通过 240/240；backend full 通过 3754/3754；frontend build 通过；Chrome smoke 通过。4C-85 只声明 narrow red / blue rune resource-domain representative payment-resource evidence recorded，不作为 READY 或 full-official 证据；完整 rune lifecycle、完整 PaymentEngine、reaction payment windows、hidden-info / redaction 仍 deferred。

Formal 18-step E2E 本轮新增证据见 `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`：`node --check scripts/chrome-formal-18-e2e.mjs` 通过；`npm run build` 通过；`npm run e2e:formal-18 -- --start-api` 通过；`npm run smoke:chrome -- --start-api` 通过。本证据满足 A 主控 formal 18-step 的连续正式主流程，但不把代表性 battle / control / PaymentEngine / LayerEngine 升级为 full official。最终 READY 前仍需在 P0/P1 清零后重跑后端 full、前端 build、Chrome smoke、formal E2E 与隐藏信息长链路检查。

Active `START_BATTLE` guard 本轮新增证据见 `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`：focused `BattlefieldContestBattleTaskGuardTests` 通过 17/17；adjacent `BattlefieldContest|StartBattle|DeclareBattle|PendingTaskQueue` 通过 94/94；backend full `dotnet test Riftbound.slnx --no-restore` 通过 3771/3771。本证据不修改服务端功能代码，只验证 active `START_BATTLE` prompt / command guard 代表路径，不能升级为完整 battle lifecycle。

Stage 4C-86 本轮新增证据见 `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_EVIDENCE.md`：`FU-ec31812b00` / `SFD·207/221` / 帝王神坛已记录代表性 conquer pay-one return-unit create-Sand-Soldier route；focused `P79BattlefieldConquerSandSoldier` 通过 3/3；adjacent `BattlefieldConquer` 通过 45/45；backend full 通过 3771/3771。本批不修改功能代码，只做矩阵与文档入账，不能升级为完整 optional trigger / PaymentEngine / battlefield lifecycle。

Stage 4C-87 本轮新增证据见 `docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_EVIDENCE.md`：`FU-a7fbef72ba` / `SFD·043/221` / 禁军之墙已记录代表性 move-any-friendly-battlefield-units-to-base route；focused `ShieldWall` 通过 2/2；adjacent `MoveFriendly|MoveUnit|FriendlyBattlefieldUnit` 通过 63/63；backend full 通过 3771/3771。本批不修改功能代码，只做矩阵与文档入账，不能升级为完整 multi-battlefield movement / FEPR / PaymentEngine。

Stage 4C-88 本轮新增 design gate 见 `docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`：`FU-0f7cbe26ce` / `OGN·113/298` / 玛尔扎哈已确认不是 evidence-only 候选；现有覆盖只包含普通手牌打出，官方 activated resource skill 仍需服务端设计/实现、FAQ 裁定与后续测试。本批不修改功能代码、不修改矩阵、不新增 representative evidence。

Stage 4C-89 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_EVIDENCE.md`：`FU-d635fc47f4` / `OGN·142/298` / 山脉亚龙与 `FU-72ce6fb8a4` / `OGN·175/298` / 船坞潜伏者已记录无卡面效果单位 ordinary source-unit play-to-base route 与 target rejection。focused vanilla source-unit / target rejection regression 305/305 通过；adjacent source-unit / play-card / target / stack / priority / payment regression 1879/1879 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-90 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_EVIDENCE.md`：`FU-c1dc472304` / `OGS·016/024` / 先锋扈从与 `FU-1207daea8f` / `SFD·006/221` / 好斗的龙犬已记录 active-entry source-unit play-to-base route、官方标签、`IsExhausted=false` 与 target rejection。focused active-entry source-unit / target rejection regression 24/24 通过；adjacent source-unit / play-card / target / stack / priority / payment regression 1879/1879 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-91 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_EVIDENCE.md`：`FU-29d76f0175` / `SFD·157/221` / 皇家守卫已记录 source-unit play-to-base route、2 战力黄沙士兵 token creation 与 target rejection。focused Royal Guard / Sand Soldier regression 10/10 通过；adjacent source-unit / token / target / stack / priority / payment regression 1880/1880 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-92 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_EVIDENCE.md`：`FU-5f03740098` / `UNL-157/219` / 严厉军士已记录 source-unit play-to-base route、精锐标签、按友方场上单位数量获得经验与 target rejection。focused Stern Sergeant / keyword source-unit regression 460/460 通过；adjacent experience / source-unit / target / stack / priority / payment regression 1913/1913 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-93 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH93_ROYAL_ATTENDANT_LEGEND_MODE_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH93_ROYAL_ATTENDANT_LEGEND_MODE_EVIDENCE.md`：`FU-92e31978af` / `SFD·039/221` / 皇家随从已记录 source-unit play-to-base route、`READY_LEGEND` / `EXHAUST_LEGEND` mode、`LEGEND` 目标候选、目标传奇活跃/休眠状态变更与 invalid target rejection。focused Royal Attendant / legend mode regression 5/5 通过；adjacent legend / source-unit / target / stack / priority / payment regression 1894/1894 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-94 本轮新增 representative evidence 见 `docs/CURRENT_STAGE4C_BATCH94_BABBLING_PORO_PREDICT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH94_BABBLING_PORO_PREDICT_EVIDENCE.md`：`FU-677c27eea7` / `UNL-224/219` / 叨叨魄罗已记录 source-unit play-to-base route、`魄罗|预知` 标签、选择己方主牌堆顶部牌并回收到牌堆底部与 outside-top-card target rejection。focused predict source-unit regression 12/12 通过；adjacent predict / source-unit / target / stack / priority / payment regression 1830/1830 通过；backend full 3771/3771 通过。本批不修改功能代码、测试代码或前端代码；只更新矩阵与文档。

Stage 4C-95 本轮新增 design gate 见 `docs/CURRENT_STAGE4C_BATCH95_STATIC_EFFECT_DESIGN_GATE.md`：`FU-0973164d07` / 熔浆巨龙、`FU-c9bce10c0e` / 娑娜、`FU-430074702b` / 安妮、`FU-af793555bb` / 温驯的宝石龙均不能 evidence-only 入账。现有 fixture 只覆盖普通 source-unit 入场，官方核心文本仍 deferred；本批不修改功能代码、测试代码、前端代码或覆盖矩阵。

Stage 4C-96 本轮新增 evidence alignment 见 `docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_EVIDENCE.md`：`FU-fe9dbeea3d` / 烈火风暴、`FU-b2e0e1d8da` / 过载能量、`FU-f877e60407` / 狩猎、`FU-abf504d74e` / 大副、`FU-6d67456a80` / 怒海大鲨炮、`FU-d68c203b01` / 德玛西亚使节、`FU-c168bd394c` / 提伯斯、`FU-3f5a9ef0e0` / 泡泡机、`FU-7472703e56` / 宝藏魔像、`FU-2e2a00f575` / 忠实的工坊主已基于 Stage 4C-60 至 4C-69 既有代表性证据与当前 HEAD 验证对齐为 `IMPLEMENTED_TESTED`。focused 67/67、adjacent 193/193、backend full 3771/3771 通过；本批不修改功能代码、测试代码或前端代码。

Stage 4C-97 本轮新增 evidence alignment 见 `docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_EVIDENCE.md`：`FU-d5e1143438` / 竞技场勤务小队、`FU-fe2295424f` / 随从（德玛西亚）、`FU-bf81e73326` / 随从（诺克萨斯）、`FU-77e07d2cad` / 随从（祖安）、`FU-4faaf1a186` / 黑暗之女已基于既有代表性证据与当前 HEAD 验证对齐为 `IMPLEMENTED_TESTED`。focused 6/6、adjacent 87/87、backend full 3771/3771 通过；本批不修改功能代码、测试代码或前端代码。

Stage 4C-98 本轮新增 evidence alignment 见 `docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_EVIDENCE.md`：`FU-f91eded774` / 力量方尖碑、`FU-1d470821cb` / 荣耀竞技场、`FU-a47530ae04` / 冰霜要塞已基于既有代表性证据与当前 HEAD 验证对齐为 `IMPLEMENTED_TESTED`。focused 8/8、adjacent 87/87、backend full 3771/3771 通过；本批不修改功能代码、测试代码或前端代码。

Stage 4D P0/P1 closure plan 本轮新增主控计划见 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`：4D-01 board task queue foundation 收口 P0-002 / P0-003；4D-02 spell duel and battle state machine 收口 P0-004；4D-03 PaymentEngine unification 收口 P0-005；4D-04 LayerEngine / keywords / full-pass track 收口 P1；4D-05 frontend authority / Chrome gate；4D-06 final evidence / completion audit。本批不修改功能代码、测试代码、前端代码或 coverage matrix；只建立后续写锁、任务顺序和验收门槛。

Stage 4D-01 board task queue foundation handoff 本轮新增实现交接规格见 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md`：基于现有 `PendingTaskQueue`、`RunStateBasedCleanupLoop`、`AdvancePendingBattlefieldTasksAfterStateChange` 与 battle task guard tests，明确下一服务端切片的写锁、必补测试、focused / adjacent / backend full 验收命令与 no-go criteria。本批不修改功能代码、测试代码、前端代码或 coverage matrix；P0-002 / P0-003 仍未关闭。

Stage 4D-01 baseline evidence 本轮新增实现前测试基线见 `docs/CURRENT_STAGE4D_01_BASELINE_EVIDENCE.md`：focused baseline 22/22 通过，adjacent baseline 139/139 通过。本批不修改功能代码、测试代码、前端代码或 coverage matrix；baseline 只作为后续实现验收对照，不关闭 P0/P1。

Stage 4D-01 board task queue foundation evidence 本轮新增测试证据见 `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`、`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`：focused 31/31、adjacent 149/149、backend full 3780/3780 通过。覆盖 base-to-battlefield、battlefield-to-base、precise roam mixed-case destination、cleanup-first blocking、illegal standby / unattached equipment redaction、cleanup repeat、`PASS_FOCUS` task promotion 与 reconnect pending task redaction。4D-01 foundation accepted，但 P0-002 / P0-003 仍不宣称 full-official resolved；P0-004、P0-005、P1 与 READY 均未关闭。

Stage 4D-02 spell duel / battle handoff 本轮新增实现交接规格与基线证据，见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`。Focused baseline 29/29、adjacent baseline 121/121 通过；当前已有 `SpellDuelState` / `BattleState` / `BattlefieldTaskState`、`START_SPELL_DUEL` / `START_BATTLE` 代表路径、active start-battle guard 与 `ASSIGN_COMBAT_DAMAGE` 代表窗口。仍缺多争夺战场串联、wrong-focus no-mutation、swift/reaction task binding、reconnect during `SPELL_DUEL_TASKS` / `BATTLE_TASKS`、完整 battle id / participant lifecycle 与 official no-result cleanup，因此 P0-004 仍未关闭。

Stage 4D-02 spell duel / battle focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`。`MatchSession` 现在能确定 active spell-duel battlefield，并只给 active task 暴露 focus / stack metadata；`ResolvePassFocus` 在 cleanup 移除 matching battle participant 后会推进下一 pending battlefield task。新增 `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs` 覆盖多争夺战场 one-active ordering、wrong-focus no-mutation、spell-duel stack task binding、cleanup 后推进下一 task，以及 `SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata + redaction。Focused new 6/6、focused handoff 35/35、adjacent 127/127、backend full 3786/3786 通过。P0-004 仍不宣称 full-official resolved。

Stage 4D-03 PaymentEngine handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `TriggerPaymentTests`、rune resource domain、typed power、payment resource action、generic mixed trait、over-recycle guard 和 battlefield held typed-power score，51/51 通过；adjacent `RuneResourceDomain|RecycleRune|TypedPowerPayment|PaymentResource|SpendPower|RunePool|PayCost|Payment|ActionPrompt|GameHub` regression 240/240 通过。当前结论是“代表支付路径绿色，可作为 4D-03 回归护栏”；P0-005 仍未关闭，因为 quote / authorize / commit 仍分散在 helper 与 resolver，尚未形成完整 PaymentEngine。

Stage 4D-03 PaymentEngine focused foundation 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`。`PaymentCostRules` 新增 `PaymentPlan`、`AuthorizePayment`、`TryCommitPayment` 与 plan-driven `BuildCostPaidPayload`；`CoreRuleEngine` 将普通 `PAY_COST`、当前触发支付代表路径、`PLAY_CARD` 和 `ASSEMBLE_EQUIPMENT` 接入 shared plan / commit foundation；新增 `PaymentEngineUnificationTests` 覆盖 mana / typed power / generic power / experience commit、wrong-trait no-mutation、post-`RECYCLE_RUNE:*` typed-cost failure rollback、play-card / assemble `COST_PAID` audit metadata。Focused 56/56、adjacent 245/245、backend full 3791/3791 通过。该证据收窄 P0-005，但仍不宣称 full official PaymentEngine、完整 `[A]` / `[C]`、Haste / Echo / Spellshield、replacement / optional / extra cost、所有非出牌窗口或 prompt quote parity 全量完成。

Stage 4D-03B non-play payment handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `PaymentEngineUnificationTests`、Vi / Xerath `ACTIVATE_ABILITY`、Spellshield tax、`LEGEND_ACT`、battlefield held pay-power score 与相关 GameHub prompt seeds，18/18 通过；adjacent `ActivateAbility|LegendAct|BattlefieldHeld|PaymentEngineUnificationTests|PaymentResource|SpendPower|RunePool|ActionPrompt|GameHub` regression 318/318 通过。基线结论是“非出牌代表支付路径行为绿色，可作为 4D-03B 回归护栏”；后续 focused slice 迁移结果见下一段。P0-005 仍未关闭，因为完整 `[A]` / `[C]`、Haste / Echo / Spellshield、替代/额外费用与全部支付窗口仍未 full-official。

Stage 4D-03B non-play payment focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`。`CoreRuleEngine` 将 Vi / Xerath `ACTIVATE_ABILITY`、Xerath Spellshield tax、`LEGEND_ACT` mana / experience payment 与 battlefield held pay-4-power score 接入 shared `PaymentPlan` / `TryCommitPayment`；`ConformanceFixtureRunnerTests` 强化对应 `COST_PAID` plan metadata 断言。Focused 18/18、adjacent 318/318、backend full 3791/3791 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 non-play payment representative breadth，但仍不宣称 full official PaymentEngine、完整 `[A]` / `[C]`、Haste / Echo / Spellshield 全窗口、replacement / optional / extra cost 或 prompt quote parity 全量完成。

Stage 4D-03C play optional / extra payment handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `PaymentEngineUnificationTests`、`PLAY_CARD` recycle rune payment resource actions、Haste ready、Echo、Spellshield tax、experience optional cost 与相关 GameHub prompt seeds，31/31 通过；adjacent `PaymentEngineUnificationTests|PaymentResource|SpendPower|Haste|Echo|Spellshield|Experience|ActionPrompt|GameHub` regression 363/363 通过。当前结论是“PLAY_CARD optional / extra / payment-resource 代表路径行为绿色，可作为 4D-03C 回归护栏”；P0-005 仍未关闭，因为这些路径尚未进一步统一到完整 quote / authorize / commit / audit 口径，且完整 `[A]` / `[C]`、全部替代/额外/可选费用与所有支付窗口仍未 full-official。

Stage 4D-03C play optional / extra payment focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`。`CoreRuleEngine` 将 `PLAY_CARD` representative affordability preflight 改为 `PaymentPlan` / `AuthorizePayment` 口径，并为 final commit 的 plan audit metadata 补充 cost reductions、optional mana reduction、battlefield reductions/increases、Spellshield tax 与 recycled rune object ids；`PaymentEngineUnificationTests` 与 `ConformanceFixtureRunnerTests` 强化 Haste、Echo、Spellshield、experience 与 payment-resource `COST_PAID` plan metadata 断言。Focused 31/31、adjacent 363/363、backend full 3791/3791 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 `PLAY_CARD` optional / extra representative breadth，但仍不宣称 full official PaymentEngine、完整 `[A]` / `[C]`、全支付窗口或 prompt quote parity 全量完成。

Stage 4D-03D activate ability payment resource focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`。`CoreRuleEngine` 将 Vi / Xerath `ACTIVATE_ABILITY` 代表路径接入 `RECYCLE_RUNE:*` payment resource action；`MatchSession` 为 `ACTIVATE_ABILITY.sourceRequirements` 暴露 payment resource choices、per-choice contribution 与 available-power metadata；`PaymentEngineUnificationTests` 新增 Vi / Xerath quote + commit、无效 / 不必要资源动作和 Spellshield tax mana 不足 no-mutation 覆盖。Focused 84/84、adjacent 257/257、backend full 3796/3796 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 `ACTIVATE_ABILITY` payment resource representative breadth，但仍不宣称 full official PaymentEngine、完整 `[A]` / `[C]`、trigger payment resource action、全支付窗口或 prompt quote parity 全量完成。

Stage 4D-03E hide card payment handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `HIDE_CARD`、`Standby` 与 `PaymentEngineUnificationTests`，84/84 通过；adjacent `HideCard|Standby|RevealCard|PaymentEngineUnificationTests|ActionPrompt|GameHub` regression 286/286 通过。当前结论是“待命暗置支付窗口和相邻 prompt / Hub 路径绿色，可作为 4D-03E 回归护栏”；P0-005 仍未关闭，因为 `HIDE_CARD` 尚未迁移到完整 shared quote / authorize / commit / audit 口径，且完整 `[A]` / `[C]`、全部替代 / 额外 / 可选费用与所有支付窗口仍未 full-official。

Stage 4D-03E hide card payment focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`。`CoreRuleEngine` 将 `HIDE_CARD` 标准 `STANDBY_A`、Teemo `STANDBY_TEEMO_MANA` 与 Guerrilla Warfare `STANDBY_FREE` 迁移到 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`；`PaymentEngineUnificationTests` 新增标准、免费、Teemo 替代与费用不足 no-mutation plan audit 覆盖。Focused 88/88、adjacent 290/290、backend full 3800/3800 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 `HIDE_CARD` standby payment representative breadth，但仍不宣称 full official PaymentEngine、完整 standby reaction lifecycle、完整 `[A]` / `[C]`、trigger payment resource action、全支付窗口或 prompt quote parity 全量完成。

Stage 4D-03F pending PAY_COST resource handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `PaymentEngineUnificationTests`、`TriggerPayment` 与 `PAY_COST` 代表路径，51/51 通过；adjacent `PaymentEngineUnificationTests|TriggerPayment|PAY_COST|ActionPrompt|GameHub` regression 229/229 通过。当前结论是“普通 pending `PAY_COST` 与相邻 prompt / Hub 路径绿色，可作为 4D-03F 回归护栏”；P0-005 仍未关闭，因为该切片只处理普通 pending `PAY_COST` resource action foundation，完整 `[A]` / `[C]`、trigger payment resource action、全部替代 / 额外 / 可选费用与所有支付窗口仍未 full-official。

Stage 4D-03F pending PAY_COST resource focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`。`CoreRuleEngine` 将普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` payment resource action 纳入 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，并在同一支付事务中先执行合法回收、再提交扣费；`MatchSession` 为 pending payment snapshot / prompt metadata 暴露 `paymentResourceActions`、per-choice contribution 与 available-power-with-resources 元数据；`PaymentEngineUnificationTests` 新增成功回收后 typed-power 支付、不必要回收 no-mutation 与无效回收 no-mutation 覆盖。Focused 55/55、adjacent 233/233、backend full 3804/3804 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 ordinary pending `PAY_COST` resource-action representative breadth，但仍不宣称 full official PaymentEngine、trigger payment resource action、完整 `[A]` / `[C]`、全支付窗口或 prompt quote parity 全量完成。

Stage 4D-03G battlefield held score resource handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 `P79BattlefieldHeldPaysPowerToGainScore`、`P79BattlefieldHeldPaysTypedPowerToGainScore`、third-turn delay / prevent 与 `PaymentEngineUnificationTests`，21/21 通过；adjacent `BattlefieldHeld|PaymentEngineUnificationTests|ActionPrompt|GameHub` regression 219/219 通过。当前结论是“battlefield held score 与相邻 prompt / Hub 路径绿色，可作为 4D-03G 回归护栏”；P0-005 仍未关闭。

Stage 4D-03G battlefield held score resource focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`。`CoreRuleEngine` 将 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 的 4 power 支付扩展为可接受必要的 `RECYCLE_RUNE:*` payment resource action；合法资源动作会在同一 `paymentId` / `BATTLEFIELD_HELD` window 下先生成 `RUNE_RECYCLED` / `POWER_GAINED`，再通过 shared `PaymentPlan` / `TryCommitPayment` 记录 `COST_PAID`。`ConformanceFixtureRunnerTests` 新增成功回收补足 4 power 和不必要 / 错玩家 / 缺 `cardNo` / 重复资源动作 no-mutation 覆盖。Focused 22/22、adjacent 224/224、backend full 3809/3809 通过；`git diff --check` 无输出。该证据收窄 P0-005 的 battlefield held score payment resource representative breadth，但仍不宣称 full official PaymentEngine、concrete trigger payment resource action、完整 `[A]` / `[C]`、全支付窗口或 prompt quote parity 全量完成。

Stage 4D-03H trigger payment resource focused slice 本轮已验收，见 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`。候选锁定 `SFD·180/221` / `SFD·180a/221` 菲奥娜“友方单位变为强力后可支付黄色使其活跃”；focused 覆盖 `TriggerPaymentTests`、`PaymentEngineUnificationTests` 与 `PAY_COST` 代表路径，69/69 通过；adjacent `TriggerPayment|PAY_COST|PaymentEngineUnificationTests|ActionPrompt|GameHub` regression 242/242 通过；backend full 3818/3818 通过。P0-005 仍未关闭，因为完整 `[A]` / `[C]`、全部触发费用窗口与 prompt quote parity 仍未 full-official。

Stage 4D-03I Malzahar resource skill focused slice 本轮新增审计与证据，见 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`。`P4ActivatedAbilityCatalog` 新增 Malzahar resource skill definition；`MatchSession` 暴露 open-main `ACTIVATE_ABILITY` source requirement 与友方单位/装备成本目标槽；`CoreRuleEngine` 成功路径横置来源、摧毁成本对象、获得 2 点 payment-only metadata power 且不创建普通 stack item。Focused 105/105、adjacent 317/317、backend full 3840/3840 通过；`git diff --check` 无输出。P0-005 仍未关闭，因为完整 `[A]` / `[C]`、reaction prohibition、spell-duel / swift timing 与 payment-only resource restriction lifecycle 仍未 full-official。

Stage 4D-03J Malzahar resource skill lifecycle handoff / baseline 本轮新增实现交接规格与实现前测试基线，见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_BASELINE_EVIDENCE.md`。Focused baseline 覆盖 Malzahar、SpellDuel、Reaction、PaymentEngineUnification 与 ResourceSkill，109/109 通过；adjacent `ActivateAbility|SpellDuel|Priority|Reaction|PaymentResource|SpendPower|RunePool|ActionPrompt|GameHub` regression 336/336 通过。当前结论是“4D-03I 后续生命周期相邻路径绿色，可作为 4D-03J 回归护栏”；P0-005 仍未关闭。

Stage 4D-03J Malzahar resource skill lifecycle focused slice 本轮已验收，见 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`。`P4ActivatedAbilityCatalog` 更新 resource restriction；`MatchState` / snapshot 新增 `TemporaryPaymentResourceState`；`MatchSession` 在 spell-duel focus 合法窗口公开 Malzahar `ACTIVATE_ABILITY` 与 temporary payment resource metadata；`CoreRuleEngine` 在 open-main / spell-duel focus 成功路径创建 temporary payment-only ledger，不向 ordinary stack 添加可反应 item，并在 pending `PAY_COST` 中消费 / 清理该 ledger。Focused 116/116、adjacent 340/340、backend full 3847/3847 通过；`git diff --check` 无输出。P0-005 仍未关闭，因为完整 `[A]` / `[C]` resource skill family、inline payment-window temporary resource consumption、reaction/counter full target-filter model 与更多 payment quote parity 仍未 full-official。

Stage 4D-03K temporary resource inline focused slice 本轮已验收，见 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`。`MatchSession` 在 play / activate / any-power assemble metadata 中公开合法 `TEMP_PAYMENT_RESOURCE:*` choices；`CoreRuleEngine` 统一解析 recycle + temporary payment resource actions，并在 inline payment commit 中应用、扣费、清理 ledger 与输出 audit payload。Focused 344/344、adjacent 539/539、backend full 3860/3860 通过；`git diff --check` 无输出。P0-005 仍未关闭，因为 full PaymentEngine、完整 resource skill family 与全部 payment windows 仍未 full-official。

当前授权边界：用户已明确“在当前 goal 完成前不需要再申请授权”。本轮 A 继续保持主控 / 验收职责；4C-85 / 4C-86 / 4C-87 / 4C-89 / 4C-90 / 4C-91 / 4C-92 / 4C-93 / 4C-94 / 4C-96 由 A 基于 matrix 风险筛选做 evidence-only 覆盖入账、复核、验证和文档收口；4C-88 / 4C-95 则将不能 evidence-only 的候选推进为 design-gated 实现派单规格。后续在 current goal 内可继续按既定写锁、验证门槛和 checkpoint 规则推进。

## 0.1 Active Goal 门槛到证据映射

| Active goal 要求 | 当前证据 | 当前状态 |
|---|---|---|
| A 作为主控架构 / 规划 / 验收 agent，不默认亲自写功能代码 | `docs/A_MASTER_AGENT_GOAL.md` 与 `docs/CURRENT_A_MASTER_CHECKPOINT.md` 明确 A 边界；4C-56 修复已按用户授权复用 B / Maxwell，A 做复核、验证和文档收口 | 满足主控边界 |
| 服务端保持唯一规则权威 | `docs/CURRENT_SERVER_RULE_AUDIT.md` 与本文件第 3 / 6 节记录服务端 authoritative snapshot / prompt / command guard 模型 | 方向满足，但仍有 P0/P1 规则缺口 |
| 前端只展示并提交服务端 `ActionPrompt` / authoritative snapshot 支持的合法操作 | 本文件第 5 / 6 / 9 节记录前端候选驱动、多批 Chrome smoke 与 formal 18-step E2E；完整 battle/control/payment/layer 仍未 full official | 部分验证，未达到最终验收 |
| P0/P1 阻断清零 | 4D-01 已把 P0-002 / P0-003 的 board task queue foundation focused checklist 验收通过；4D-02 已把 P0-004 的 task-scoped focused slice 验收通过；4D-03 已把 P0-005 的 PaymentEngine focused foundation 验收通过；4D-03B 已把代表性 non-play payment windows 接入 shared plan / commit；4D-03C 已把代表性 `PLAY_CARD` optional / extra / payment-resource windows 接入 shared plan audit / authorize 口径；4D-03D 已把代表性 Vi / Xerath `ACTIVATE_ABILITY` payment resource window 接入 prompt quote / command commit / audit 口径；4D-03E 已把代表性 `HIDE_CARD` standby payment window 接入 shared plan / commit / audit；4D-03F 已把代表性 ordinary pending `PAY_COST` resource action 接入 shared plan / commit / audit；4D-03G 已把代表性 battlefield held score resource action 接入 shared plan / commit / audit；4D-03H 已把代表性 SFD Fiora trigger payment resource action 接入 shared plan / commit / audit；4D-03I 已把代表性 Malzahar open-main resource skill 接入 prompt / command / audit；4D-03J 已把 Malzahar lifecycle 接入 temporary ledger；4D-03K 已把 temporary ledger 接入 play / activate / assemble inline representatives；4D-03L 已把 Dragon Soul Sage reaction resource skill 接入 prompt / command / audit representative；4D-03M 已把 Renata colored activated draw 接入 prompt / command / audit representative；4D-03N 已把 Renata colored activated score 接入 prompt / command / stack resolution / audit representative；本文件第 4 / 11 节与 `docs/CURRENT_SERVER_RULE_AUDIT.md` 仍列出 P0-005 full PaymentEngine breadth、P1 LayerEngine / 关键词 / 全卡证据，并保留 P0-002 / P0-003 / P0-004 full-official lifecycle 残余 | 未完成 |
| 后端 full test 当前 HEAD 全绿 | 4D-03N focused slice 后 backend full 3914/3914 通过 | 本轮满足，最终验收前仍需重跑 |
| Chrome smoke 通过 | 4C-85 入账后 frontend build 通过，Chrome smoke 通过；本轮 `npm run smoke:chrome -- --start-api` 再次通过 | 本轮满足，最终验收前仍需随 P0/P1 清零重跑 |
| 正式 18 步 E2E 通过 | `npm run e2e:formal-18 -- --start-api` 已通过，房间 `formal-18-1778623926434-15` 覆盖双 Chrome profile、官方 deck/opening/mulligan、stack pass-pass、unit move、reconnect、P2 battlefield score、surrender result | A_MASTER 18-step 满足；整体仍 NOT READY |
| 卡牌覆盖矩阵完成 | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 已将 `stage4C98` 回填为 representative evidence alignment，`IMPLEMENTED_TESTED` 为 76、`IMPLEMENTED_UNTESTED` 为 4；但 1009/811 full-official coverage 仍未完成 | 未完成 |
| 最终 completion audit 输出 READY 后才允许标记 complete | 本文件审计结论仍为 **NOT READY**；未调用 `update_goal complete` | 未完成 |

## 1. 修改文件列表

2026-05-14 Stage 4D-03N colored activated score focused slice 本轮修改：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`
- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03N colored activated score handoff / baseline 本轮修改：

- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03M colored activated draw focused slice 本轮修改：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`
- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md`
- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03M colored activated draw handoff / baseline 本轮修改：

- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_BASELINE_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03L reaction resource skill focused slice 本轮修改：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/ReactionResourceSkillTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md`
- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03L reaction resource skill handoff / baseline 本轮修改：

- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_BASELINE_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03K temporary resource inline focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-01 board task queue foundation evidence 本轮修改：

- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md`
- `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-02 spell duel / battle handoff 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-02 spell duel / battle focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md`
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03 PaymentEngine handoff / baseline 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03 PaymentEngine focused foundation 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03B non-play payment focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md`
- `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03C play optional / extra payment handoff 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03C play optional / extra payment focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md`
- `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03D activate ability payment resource focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03E hide card payment handoff / baseline 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03E hide card payment focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md`
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03F pending PAY_COST resource handoff / baseline 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03F pending PAY_COST resource focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03G battlefield held score resource handoff / baseline 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03G battlefield held score resource focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D-03H trigger payment resource focused slice 本轮修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-14 Stage 4D-03I Malzahar resource skill focused slice 本轮修改：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md`
- `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4D P0/P1 closure plan 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_01_BASELINE_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`

2026-05-13 Stage 4C-98 battlefield residual evidence alignment 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_EVIDENCE.md`

2026-05-13 Stage 4C-97 arena / minion / Annie evidence alignment 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_EVIDENCE.md`

2026-05-13 Stage 4C-96 legacy guard evidence alignment 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_EVIDENCE.md`

2026-05-13 Stage 4C-95 static effect design gate 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_STAGE4C_BATCH95_STATIC_EFFECT_DESIGN_GATE.md`

2026-05-13 Stage 4C-88 Malzahar design gate 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-89 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-90 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-91 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-92 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-86 representative evidence 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/rules-evidence-index.md`

2026-05-13 Stage 4C-85 representative evidence 与 formal 18-step E2E 本轮修改：

- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/rules-evidence-index.md`
- `src/Riftbound.DevUi/package.json`

历史第二百五十九批修改：

- 第三百四十五批追加修改：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - `docs/CURRENT_COMPLETION_AUDIT.md`
  - `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
  - `docs/CURRENT_SERVER_RULE_AUDIT.md`

- `src/Riftbound.Api/Program.cs`
- `src/Riftbound.DevUi/package.json`
- `src/Riftbound.DevUi/scripts/check-user-facing-text.mjs`
- `src/Riftbound.DevUi/src/services/matchSocket.ts`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_FRONTEND_REBUILD_PLAN.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

本轮 active goal 的累计源码、测试和文档变更以 `git log`、`docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 与 `docs/CURRENT_SERVER_RULE_AUDIT.md` 的批次记录为准。

## 2. 新增文件列表

2026-05-13 Stage 4D-01 board task queue foundation evidence 新增：

- `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md`
- `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`

2026-05-13 Stage 4C-87 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_EVIDENCE.md`

2026-05-13 Stage 4C-88 Malzahar design gate 新增：

- `docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`

2026-05-13 Stage 4C-89 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_EVIDENCE.md`

2026-05-13 Stage 4C-90 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_EVIDENCE.md`

2026-05-13 Stage 4C-91 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_EVIDENCE.md`

2026-05-13 Stage 4C-92 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_EVIDENCE.md`

2026-05-13 Stage 4C-86 representative evidence 新增：

- `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_EVIDENCE.md`

2026-05-13 Stage 4C-85 representative evidence 与 formal 18-step E2E 新增：

- `docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_EVIDENCE.md`
- `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`
- `src/Riftbound.DevUi/scripts/chrome-formal-18-e2e.mjs`

2026-05-13 Stage 4C-85 representative evidence 新增 conformance tests：无；本批复用既有 conformance tests，并只做矩阵与文档入账。formal 18-step E2E 则新增可复跑的 DevUi 脚本。

历史第二百五十九批新增：

- `src/Riftbound.DevUi/scripts/check-user-facing-text.mjs`

第二百一十四批已新增：

- `src/Riftbound.DevUi/src/utils/errors.ts`

第二百一十三批已新增：

- `src/Riftbound.DevUi/src/utils/redaction.ts`

第二百零九批已新增：

- `docs/CURRENT_COMPLETION_AUDIT.md`

未跟踪 `riftbound-dotnet.sln` 是本地预期文件，不属于本轮审计交付，也不得提交。

## 3. 关键架构说明

- 服务端仍是唯一规则权威：Web 前端只读取 authoritative snapshot、服务端行动提示、事件和候选元数据，不在浏览器内裁决费用、时点、目标、战场控制或胜负。
- SignalR 房间层按玩家发送 snapshot/prompt，隐藏信息由服务端视角裁剪；前端只负责产品化展示与提交服务端候选中允许的命令。
- `MatchSession` 串行处理命令并只在 accepted 结果后更新权威状态；前端 reconnect/reload 必须从服务端快照恢复。
- Development-only seed 只用于 smoke 和规则回归证据，不能替代正式官方构筑、起手和完整 1v1 主流程验收。

## 4. 服务端规则补齐项

已补到可验证代表路径的部分包括：官方 deck/opening/mulligan 入口、按玩家视角快照、对象位置索引、typed rune pool、部分 PaymentEngine 元数据、spell duel 焦点恢复、代表性 battle/declare battle 路径、部分 cleanup loop、战场任务视图、隐藏信息和大量来源/目标控制权 guard。

仍存在 P0/P1 阻断：

- P0-002：4D-01 board task queue foundation 已覆盖对象位置、battlefield state、destination-scoped contest tasks 与 reconnect redaction；完整 battlefield standby/control/held/conquer task lifecycle 仍未官方化。
- P0-003：4D-01 已覆盖 cleanup-first blocking、cleanup repeat、illegal standby / unattached equipment redaction 与 no-mutation guard；central cleanup task queue 仍未覆盖所有状态变化、替代效果和进出战场路径。
- P0-004：4D-02 focused slice 已证明多争夺战场 one-active ordering、wrong-focus no-mutation、spell-duel stack task binding、cleanup 后推进下一 task 和 reconnect redaction；完整官方 pending/focus/initial-stack/伤害分配/清理状态机仍未 full-official 收口。
- P0-005：完整 PaymentEngine 与 reaction payment window 仍未统一；4D-03D 已覆盖 Vi / Xerath `ACTIVATE_ABILITY` payment resource 代表窗口，4D-03E 已覆盖 `HIDE_CARD` standby payment 代表窗口，4D-03F 已覆盖 ordinary pending `PAY_COST` payment resource 代表窗口，4D-03G 已覆盖 battlefield held score payment resource 代表窗口，4D-03H 已覆盖 SFD Fiora concrete trigger payment resource action 代表窗口；4D-03I 已覆盖 Malzahar open-main `[A A]` resource skill 代表窗口；完整 `[A]` / `[C]` resource skill family、spell-duel / swift timing、reaction prohibition、完整 trigger payment resource family、LEGEND_ACT、MOVE_UNIT、ASSEMBLE_EQUIPMENT 等仍有代表路径边界。
- P1：LayerEngine、完整关键词、全官方卡牌逐条证据、长期 replay/hash 审计仍未达到最终 READY。

## 5. 前端页面完成项

已完成并有批次证据的页面/模块包括：首页、图鉴、卡组、设置、房间页、对战桌面、行动面板、卡牌详情、规则队列、事件日志、结算页、中文化展示、内部对象标识脱敏、隐藏信息展示和服务端候选驱动交互。

仍需收口：

- Formal 18-step E2E 已用双 headless Chrome profile 覆盖 A 主控连续正式主流程；后续不应再把“缺一条 formal 18-step”作为独立缺口。
- 第三百四十五批 seeded battle/control/reconnect smoke 与本轮 formal 18-step 共同覆盖移动、战斗/控制代表路径、得分、重连和胜负结算，但严格 full official battlefield contest / battle lifecycle 仍归入 P0-002 / P0-004。
- 继续补响应窗口、复杂费用、目标法术、battle/control 全生命周期与隐藏信息长链路 smoke。
- 确认所有玩家可见页面不再裸显协议词、对象 ID、raw enum、fixture/debug 文本或未授权隐藏信息。

## 6. 接口契约说明

- Web 入口通过 API/SignalR 获取房间状态、snapshot、prompt、events，并提交 `SUBMIT_DECK`、`READY`、`MULLIGAN`、`PLAY_CARD`、`MOVE_UNIT`、`DECLARE_BATTLE`、`PASS_PRIORITY`、`PASS_FOCUS`、`TAP_RUNE`、`RECYCLE_RUNE`、`ASSEMBLE_EQUIPMENT`、`ACTIVATE_ABILITY`、`LEGEND_ACT`、`END_TURN`、`SURRENDER` 等服务端已公开候选动作。
- 前端只能提交当前 prompt 中 enabled candidate 与 source/target/payment requirement 支持的参数；缺 sourceRequirement、缺 cardNo、缺合法目标或 disabled candidate 时，产品 UI 不应提供可点击提交入口。
- 服务端应拒绝手写越权命令，并保持失败命令不改变 authoritative state。

## 7. 隐藏信息保护检查结果

当前已有多批测试和 Chrome smoke 证明：对手手牌、隐藏待命、未知对象、内部对象 ID、stack/cleanup/task id 不应进入非授权玩家可见正文。最近的 object redaction smoke 覆盖了隐藏卡详情和页面正文；第二百一十二批确认官方开局起手调整候选只显示卡号；第二百一十三批确认任务队列 activeTaskId、cleanup/task id 与 raw reason 不进入玩家可见正文；第二百一十四批确认错误日志正文不再显示 raw error code 或英文内部错误消息；第二百一十五批确认卡牌详情操作 composer 的异常 fallback 不再显示 objectId、abilityId 或 raw cost id；第二百一十六批确认开发场景事件日志不再显示 development-only seed 名称或 scenario id；第二百一十七批确认 cleanup task 阻塞 prompt/error 只显示中文原因，不显示 raw task kind；第二百一十八批确认规则队列正文把 `RECALL_UNATTACHED_EQUIPMENT` 显示为“装备清理”，不显示 raw cleanup kind/reason 或未贴附装备对象 ID；第二百一十九批确认资源条把 typed rune trait 显示为“红色符能 2”，不显示 `red:2` 这类协议键；第二百二十批确认结算链 stack item destination 显示为“去向：战场”，不显示 `BATTLEFIELD:P1-MAIN` 或 raw `BATTLEFIELD`；第二百二十一批确认服务端 disabled candidate reason 显示“横置符文 / 回收符文”等中文行动名，不显示 `TAP_RUNE` / `RECYCLE_RUNE`；第二百二十二批确认前端共享 formatter 对未来未知协议 action/phase/window/status 降级为中文占位；第二百二十三批确认官方起手候选 tooltip 显示“起手调整候选”，不显示英文内部 reason 或手牌对象 ID；第二百二十四批确认事件/错误日志标题 hover 不再暴露 `event.kind` 或 `error.code`；第二百二十五批确认卡牌详情操作选择 tooltip 过滤内部英文/协议 reason；第二百二十六批确认行动面板、房间页和卡牌详情中的 prompt/candidate reason 正文与 title 统一过滤，不显示内部英文 reason、协议 destination 或对象 ID；第二百二十七批确认规则队列 phase/result fallback 不再回显未知服务端枚举；第二百二十八批确认卡牌详情不可组合 warning 过滤 `unsupportedReason`，不显示 raw ability id、对象 id 或 sourceRequirements metadata；第二百三十三批确认非当前行动玩家手写 `DECLARE_BATTLE` 被 START_BATTLE 队列拒绝时，错误消息显示中文“开始战斗”且不含 raw `START_BATTLE`；第二百三十四批进一步确认当前行动玩家提交错误战场声明时，服务端错误消息为中文且不含 raw `DECLARE_BATTLE` / `START_BATTLE`；第二百三十五批确认非法窗口的让过优先权/让过焦点错误消息为中文且不含 raw `PASS_PRIORITY` / `PASS_FOCUS`；第二百三十六批确认符文资源动作未知牌号运行时错误消息为中文且不含 raw `TAP_RUNE` / `RECYCLE_RUNE`；第二百三十七批确认结束回合非法窗口/玩家错误消息为中文且不含 raw `END_TURN`；第二百三十八批确认正式起手调整多条拒绝错误消息为中文且不含 raw `MULLIGAN`；第二百三十九批确认待命埋伏多条拒绝错误消息为中文且不含 raw `HIDE_CARD`；第二百四十批确认待命翻开/反应多条拒绝错误消息为中文且不含 raw `REVEAL_CARD`；第二百四十一批确认移动单位多条拒绝错误消息为中文且不含 raw `MOVE_UNIT`；第二百四十二批确认无对手投降拒绝错误消息为中文且不含 raw `SURRENDER`；第二百四十三批确认出牌来源多条拒绝错误消息为中文且不含 raw `PLAY_CARD`；第二百四十四批确认已结束对局拒绝后续命令错误消息为中文且不含 raw action kind；第二百四十五批确认启动技能来源控制权拒绝错误消息为中文且不含 raw `ACTIVATE_ABILITY`；第二百四十六批确认传奇行动来源多条拒绝错误消息为中文且不含 raw `LEGEND_ACT`；第二百四十七批确认装备装配未开放路径错误消息为中文且不含 raw `ASSEMBLE_EQUIPMENT`；第二百四十八批确认伏击出牌未开放路径错误消息为中文且不含 raw `PLAY_CARD mode AMBUSH`；第二百四十九批确认回合开始非回合玩家推进拒绝错误消息为中文且不含 raw `TURN_START`；第二百五十批确认启动技能未开放路径错误消息为中文且不含 raw `ACTIVATE_ABILITY`；第二百五十一批确认战场静态禁止出牌错误消息为中文且不含 raw `PLAY_CARD`；第二百五十二批确认传奇行动未开放与非法时点错误消息为中文且不含 raw `LEGEND_ACT`；第二百五十三批确认声明战斗未开放路径错误消息为中文且不含 raw `DECLARE_BATTLE`；第二百五十四批确认启动技能时点、目标、来源、身份、横置和资源错误消息为中文且不含 raw `ACTIVATE_ABILITY` 或英文技能说明；第二百五十五批确认战场效果目标选择错误消息为中文且不含英文战场效果说明或 raw `DECLARE_BATTLE`；第二百五十六批确认传奇触发目标选择错误消息为中文且不含英文 legend trigger 说明或 raw `PLAY_CARD`；第二百五十七批确认正式卡组、房间准备、未开局/已结束提交、未知命令和重复 intent 错误消息为中文且不含英文卡组/房间诊断或 raw unsupported command；第二百五十八批确认房间、重连、开发测试状态和空 id 错误消息为中文且不含英文房间/重连/seed 诊断；第二百五十九批确认前端连接超时 fallback、API 行为规格 404 与已收口房间错误文案被 `check:user-facing-text` 构建门禁覆盖。

最终验收前仍需再跑一次长链路隐藏信息检查，覆盖正式 deck、起手、手牌、牌堆顺序、面朝下待命、reconnect/replay 视角。

## 8. 测试命令与测试结果

最近批次证据：

- 后端 build 与 `GameHubJoinTests` 在最近前端/文档收口批次通过。
- 前端 `npm run build` 在最近前端收口批次通过。
- 后端 full test 最近完整通过记录见 `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` 批次记录；最终验收前必须重新运行当前 HEAD 的 full test。

历史第二百五十九批是前端/API fallback 文案与构建门禁收口批。`MatchSocket` 在 Join/Reconnect 等待超时时返回中文错误，行为规格 API 单卡未命中返回中文 404 message；`npm run build` 现在会执行 `check:user-facing-text`，防止关键玩家可见英文 fallback 回流。该历史批次曾记录后端 full test 3157/3157 通过、前端 build 通过。此前第二百三十二批 Chrome 插件房间 `smoke-battlefield-held-score-1778247059745` 已覆盖战场得分 UI 代表路径。

当前 2026-05-13 formal 18-step E2E 收口后，`node --check scripts/chrome-formal-18-e2e.mjs` 通过；`npm run build` 通过；`npm run e2e:formal-18 -- --start-api` 通过；`npm run smoke:chrome -- --start-api` 通过。该记录满足 formal 18-step 入口，但最终验收前仍需随 P0/P1 清零重跑。

当前 active `START_BATTLE` guard test-only 批次新增后，focused `FullyQualifiedName~BattlefieldContestBattleTaskGuardTests` 通过 17/17；adjacent `FullyQualifiedName~BattlefieldContest|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~PendingTaskQueue` 通过 94/94；backend full `dotnet test Riftbound.slnx --no-restore` 通过 3771/3771。因本批仅新增测试和文档，未修改引擎或前端功能代码。

当前 Stage 4D-03F pending PAY_COST resource focused slice 新增后，focused `FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST` 通过 55/55；adjacent `FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub` 通过 233/233；backend full `dotnet test Riftbound.slnx --no-restore` 通过 3804/3804；`git diff --check` 无输出。

当前 Stage 4D-03G battlefield held score resource focused slice 新增后，focused `FullyQualifiedName~P79BattlefieldHeldPaysPowerToGainScore|FullyQualifiedName~P79BattlefieldHeldPaysTypedPowerToGainScore|FullyQualifiedName~P79BattlefieldScoreDelayPreventsHeldScorePaymentBeforeThirdTurn|FullyQualifiedName~PaymentEngineUnificationTests` 通过 22/22；adjacent `FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub` 通过 224/224；backend full `dotnet test Riftbound.slnx --no-restore` 通过 3809/3809；`git diff --check` 无输出。

## 9. Browser smoke / E2E 结果

已有大量 Browser/Chrome smoke 覆盖中文化、候选展示、隐藏信息、spell duel cleanup、battle result、reconnect 等代表路径。Codex Chrome Extension 当前已确认可通信，第二百一十六批 Chrome smoke 使用房间 `smoke-scenario-redaction-1778238780476` 覆盖 `basic-play` seed 事件日志；页面显示“载入测试状态 / 测试状态已载入”，正文不含 `basic-play`、`DEV_SCENARIO_SEEDED`、`开发测试场景已载入`、`SeedScenario` 或 `scenarioId`，应用自身 runtime error 0，并在结束后清理测试标签和临时服务进程。第二百一十八批 Chrome smoke 使用房间 `smoke-unattached-equipment-1778239965401`，P2 页面连接并由后台 P1/P2 seed `battlefield-unattached-equipment-cleanup`；规则队列显示“阶段：状态清理 / 活动任务：处理中 / 装备清理：装备脱离清理”，prompt 显示“等待服务端处理任务队列：装备清理”，正文不含 raw cleanup kind/reason 或对象 ID。第二百一十九批 Chrome smoke 使用房间 `smoke-rune-trait-label-1778240000001`，P1 页面连接并由后台 P2 seed `typed-power-payment`；资源条显示“法力 1 / 符能 2 / 红色符能 2”，正文不含 `red:2`、`red : 2` 或 `red 2`。第二百二十批 Chrome smoke 使用房间 `smoke-zone-label-field-1778240000004`，P1 打出单位到 `BATTLEFIELD:P1-MAIN` 后规则队列显示“去向：战场”，正文不含 `BATTLEFIELD:P1-MAIN` 或 raw `BATTLEFIELD`。第二百二十三批 Chrome smoke 使用房间 `smoke-mulligan-title-1778242041908`，正式提交卡组/准备后在起手调整页面显示 4 个候选，title 全为“起手调整候选”，正文和 title 不含 `opening hand mulligan candidate` 或 `P1/P2-*` 手牌对象 ID，应用 error 0。第二百二十四批 Chrome smoke 使用房间 `smoke-log-title-1778242330553`，事件日志显示“载入测试状态”，`.event-log strong[title]` / `.room-log-list strong[title]` 数量为 0，正文不含 `DEV_SCENARIO_SEEDED`、`MATCH_STARTED`、`ROOM_FULL` 或 `UNSUPPORTED_COMMAND`，应用 error 0。第二百二十五批 Chrome smoke 使用房间 `smoke-choice-title-1778242626275`，打开卡牌详情后页面和 title 不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`P1-HAND` 或 `P1-MAIN`，应用 error 0。第二百二十六批 Chrome smoke 使用房间 `smoke-reason-filter-1778243194494`，P1 通过设置页写入 `serverUrl=http://127.0.0.1:5093`、`playerId=P1` 后连接对战页，后台 P2 seed `basic-play`，打开《魔法小仙灵》详情后显示“当前玩家普通开环行动 / 服务端可提交操作”，正文和 title 不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`opening hand mulligan candidate`、`P1-UNIT-MIGHTY-FAERIE`、`P1-MAIN-001` 或 `BATTLEFIELD:P1-MAIN`，应用 error 0。第二百二十七批在 Chrome 插件本地导航被 `ERR_BLOCKED_BY_CLIENT` 阻断后，改用后台 Playwright + 系统 Chrome headless，房间 `smoke-stack-label-headless-1778243871233` 显示“阶段：空闲”，正文不含 `IDLE`、`BATTLE_TASKS`、`BATTLEFIELD_TASKS`、`SPELL_DUEL_TASKS`、`STATE_BASED_CLEANUP`、`CONTROL_RESOLVED`、`NO_RESULT` 或 `CLOSED`，应用 error 0。第二百二十八批 Chrome 插件 smoke 使用房间 `smoke-unsupported-check`，P1/P2 均通过真实页面入座，后台 seed `battlefield-legend-attach-armament`；P1 打开《圣锤之毅》详情后显示中文不可组合 warning 且 `确认传奇行动` disabled，正文/title 不含 raw ability id、对象 id、`unsupportedReason`、`composable`、`targetChoicesByIndex` 或 `sourceRequirements`，应用 error 0。第二百二十一批为服务端 prompt reason 文案契约修正，第二百二十二批为前端 formatter 未知协议 fallback 修正，均未启动新的 API/Vite/Chrome smoke；5092/5093/5094/5175/5176/9223/9224 保持无监听。最近几次 Chrome smoke 都只记录扩展脚本 autoplay `NotAllowedError` 或 favicon 404 这类非应用噪声，过滤后应用 runtime error 0，结束后已清理测试标签和临时服务进程。

第二百三十批新增一条正式房间主流程探针证据：Chrome 插件房间 `room-vnpnxy` 完成创建/加入房间、合法卡组、准备、起手、首回合、召符文、抽牌、横置符文、出单位、结算链双方让过、单位移动到战场、结束回合、P2 回合开始和 reload/reconnect 恢复；同一房间由后台 SignalR 提交 P2 投降后，headless 结果页显示“胜者：P1”。本批结束后已 finalize Chrome 标签并清理 API/Vite/headless 进程，`5092/5093/5094/5175/5176/9223/9224` 无监听。

第二百三十一批补齐纯前端投降确认 smoke：Chrome 插件房间 `smoke-surrender-confirm-1778246799998` 中，P1 页面连接，后台 P2 入座并 seed `basic-play`；P1 点击“投降”后显示“确认投降 / 取消”，取消可收起，再次“投降 -> 确认投降”后结果页显示“胜者：P2”。smoke 后已 finalize Chrome 标签并清理 API/Vite 进程，目标端口无监听。

第二百三十二批补充战场得分真实 UI smoke：Chrome 插件房间 `smoke-battlefield-held-score-1778247059745` 中，P1 从服务端行动提示打开《大力仙灵》详情，选择服务端候选战场 `SFD·214/221` 与防守单位 `SFD·125/221`，点击“确认声明战斗”；页面显示“声明战斗 / 造成伤害 / 单位摧毁 / 据守战场 / 战场触发结算 / 支付费用 / 获得分数 / 战斗结束 / 战场控制结算”，P2 分数为 `1/8`。应用 runtime error 0，结束后已清理 Chrome 标签和 API/Vite 进程。

第二百五十九批仅改前端/API fallback 文案和构建门禁，无业务交互流程变更；本批未启动新的 API/Vite/Chrome smoke，提交前目标端口保持无监听。

2026-05-13 新增 formal 18-step E2E：房间 `formal-18-1778623926434-15` 在同一连续正式牌局中完成 P1/P2 双 Chrome profile 连接、官方合法卡组提交、ready、mulligan、P1 召符文/抽牌/横置符文/打出单位、双方让过结算链、单位移动到 P2 `OGN·290/298` 战场、P1 reload/reconnect、结束回合、P2 回合开始、`BATTLEFIELD_TRIGGER_RESOLVED` 与 `SCORE_GAINED` 让 P2 分数变为 `1/8`、P2 reload/reconnect、P2 投降、双端结果页显示“胜者：P1”。页面正文同时断言不暴露 `mainDeck`、`runeDeck`、`handHidden`、`stackItemId`、`reconnectToken` 等 raw debug / hidden-info 文本。

该 E2E 满足 `docs/A_MASTER_AGENT_GOAL.md` 第 11 节 formal 18-step 主流程；`docs/任务补充.md` 中更严格的战场争夺/战斗完整官方化仍由第三百四十五批 seeded battle/control smoke 与后续 P0-002 / P0-004 收口承接，不能据此宣称 battle lifecycle full official。

## 10. 仍未完成的 P2 项

P2 项暂不作为 complete 阻断，但应在 P0/P1 清零后继续排期：

- 更完整的新手引导、可访问性细节、动画/音效 polish。
- 更丰富的回放检索、调试筛选和开发者诊断视图。
- 非 P8 范围的账号、匹配、部署、监控和风控仍不进入当前 goal。

## 11. 是否仍存在 P0/P1 阻断

**是。** 当前仍存在 P0/P1 阻断，不能把 active goal 标记 complete。

## 12. 最终结论

**NOT READY**

下一步优先级：按 4D-03 handoff 执行 PaymentEngine Unification 服务端切片，随后收口 P1 LayerEngine/全卡证据；4D-02 仅作为 spell duel / battle task-scoped regression foundation，不等同完整官方 battle lifecycle。
