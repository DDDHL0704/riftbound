# Stage 4D Next Dispatch and Writelocks

日期：2026-05-16
结论：**4D-03AX PAYMENT ENGINE LEGEND / BATTLEFIELD / TRIGGER RESOURCE ACTION VERIFIER ACCEPTED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件是 A 主控对下一批 B/C/D/E 工作的调度队列与写锁边界。它只做 planning / handoff / acceptance / baseline 归档，不实现 runtime，不修改前端，不修改测试代码，不升级 full-official。当前 active goal 仍未完成，不得调用 `update_goal complete`。

## 1. 输入事实

- 当前分支为 `main`，仓库当前只保留未跟踪 `riftbound-dotnet.sln`；该文件不得被本批任务触碰或纳入提交。
- 4D-03AX PaymentEngine legend / battlefield / trigger resource action manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 4D-03AV `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` residual family 拆成 `LEGEND_ACT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`、`TRIGGER_PAYMENT` 三个 executable window-level entries，并保留 prompt / command / audit / rollback / remaining official breadth / NOT READY closure。A 侧验证 focused 27/27、adjacent LegendAct / BattlefieldHeld / TriggerPayment / PaymentEngine / prompt / hub regression 408/408、backend full 4464/4464 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03AX focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03AW PaymentEngine target / colored activated ability manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把当前 `P4ActivatedAbilityCatalog` 中非 resource-skill 且 target-bearing / typed-color / experience / Spellshield-tax 的 8 个 activated ability representatives 固定为 executable manifest，并保留 prompt / command / audit / rollback / remaining official breadth / NOT READY closure。A 侧验证 focused 22/22、adjacent target / payment / prompt / hub regression 530/530、backend full 4459/4459、`git diff --check` 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03AW focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03AV PaymentEngine residual blocker manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 official PaymentEngine matrix、`[A]` / `[C]` resource skill family、target-bearing colored activated abilities、legend/battlefield/trigger resource actions、keyword payment branches、MOVE_UNIT policy-deferred 六个 residual families 固定为 executable manifest。A 侧验证 focused 18/18、adjacent payment / prompt / hub / keyword regression 576/576、backend full 4455/4455 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03AV focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03AU PaymentEngine residual official scope handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03AU_PAYMENT_ENGINE_RESIDUAL_SCOPE_BASELINE_EVIDENCE.md`。A 主控确认 P0-005 在现有 4D-03 action-window / resource-skill / Spellshield / HASTE_READY verifiers 和 4D-03AM-AT representative evidence 后仍未 full official closure；下一建议 B 切片是 4D-03AV residual blocker manifest / quote-parity verifier。A 侧 baseline focused PaymentEngine coverage guard 14/14、adjacent payment / prompt / hub / keyword regression 572/572、backend full 4451/4451 通过。本批不改 runtime / tests / frontend / card matrix，不派发 B，不打开写锁，不触碰 `riftbound-dotnet.sln`；项目仍 **NOT READY**。
- 4D-04Q-B LayerEngine static aura source lifecycle 已由 B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_EVIDENCE.md`。本批只做 P1-001 foundation：新增 `STATIC_AURA` continuous-effect foundation view，让 Ornn friendly-equipment static recompute 与 battlefield all-units +1 representative 暴露 source/target、condition、lifecycle、participant metadata，并证明 source/condition 失效后不留 stale metadata；现有 power / combatPower arithmetic 不变。A 侧验收 focused static-aura / LayerEngine-view guard 11/11、adjacent static / continuous-effect / equipment regression 49/49、backend full 4451/4451、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime/full `百炼` breadth、完整 LayerEngine/timestamp dependency graph rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04Q-B 写锁已关闭。本批不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04Q-B LayerEngine static aura source lifecycle 此前已派发给 B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c`。A 当时打开窄 runtime / focused-test 写锁，允许范围仅 `src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 中 focused static aura / battlefield static representative，以及必要时最小 model / snapshot helper；frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime/full `百炼` breadth、完整 LayerEngine/timestamp dependency graph rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 仍锁定。该派发的验收门槛是 focused static-aura / LayerEngine-view guard、adjacent static / continuous-effect / equipment regression、backend full、`git diff --check`，以及新增 4D-04Q audit/evidence；门槛现已在 4D-04Q-B acceptance 中满足并关闭写锁。项目仍 **NOT READY**。
- 4D-04Q LayerEngine static aura source lifecycle handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_BASELINE_EVIDENCE.md`。A 主控确认 4D-04L-P 已把 until-end power modifier 的 source / effect / direct-path / requested / applied / minimum / resulting / order metadata 打通；下一建议 B 切片转向 dynamic static aura / equipment static source lifecycle foundation，优先绑定 Ornn friendly-equipment static recompute 与 battlefield static power representative。A 侧 baseline focused static-aura / LayerEngine-view guard 10/10、adjacent static / continuous-effect / equipment regression 48/48、backend full 4450/4450 通过。frontend、card matrix JSON、runtime、tests、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；handoff 本身不派发 B、不打开写锁、不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04P-B LayerEngine minimum-power ordering 已由 B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_EVIDENCE.md`。本批未改 runtime，只新增同目标 Smoke Bomb floor -> Extortion zero-applied floor -> Power Bind +1 representative，并补强 Smoke Bomb end-turn cleanup ledger/effect/snapshot assertion。A 侧验收 focused minimum-power ordering guard 8/8、adjacent minimum / ordering / continuous-effect regression 16/16、backend full 4450/4450、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime/static aura rewrite、完整 LayerEngine rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04P-B 写锁已关闭。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering beyond this representative、P1-002、full official 或 READY。
- 4D-04P-B LayerEngine minimum-power ordering 已派发给 B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e`。A 已打开窄 runtime / focused-test 写锁，允许范围仅 `src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`、`tests/Riftbound.ConformanceTests/**` 中 focused minimum-power / power modifier ordering representatives，以及必要时最小 fixture/helper/model；frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime/static aura rewrite、完整 LayerEngine rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 仍锁定。A 等待 B diff 后必须验收 focused minimum-power ordering guard、adjacent minimum / ordering / continuous-effect regression、backend full、`git diff --check`，并新增 4D-04P audit/evidence。项目仍 **NOT READY**。
- 4D-04P LayerEngine minimum-power ordering handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_BASELINE_EVIDENCE.md`。A 主控确认 4D-04M minimum-power requested/applied/minimum/resulting metadata 与 4D-04O explicit applied order metadata 已分别成立；下一建议 B 切片只补同目标 minimum floor 与 applied order 的组合 representative，不实现完整 LayerEngine。A 侧 baseline focused minimum-power ordering guard 7/7、adjacent minimum / ordering / continuous-effect regression 15/15、backend full 4449/4449 通过。frontend、card matrix JSON、runtime、tests、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；handoff 本身不派发 B、不打开写锁、不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04O-B LayerEngine power modifier ordering metadata 已由 B-Implementation / Leibniz `019e2c86-8abd-74c3-8c3d-3e8ccd5453ab` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_EVIDENCE.md`。本批只做 P1-001 foundation：为 ledger-backed until-end power modifiers 增加 nullable `AppliedOrder` / snapshot `appliedOrder`，让 state ledger、`ContinuousEffectState` 与 `timing.continuousEffects[]` 能表达同目标同层 append order；legacy untracked remainder 不伪造 order。A 侧验收 focused ordering guard 6/6、adjacent LayerEngine / power metadata regression 39/39、backend full 4449/4449、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime、完整 LayerEngine rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04O-B 写锁已关闭。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering、P1-002、full official 或 READY。
- 4D-04O-B LayerEngine power modifier ordering metadata 已派发给 B-Implementation。本批写锁只允许实现 ledger-backed until-end power modifier 的显式 application order metadata，并保持现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic、minimum floor metadata、direct-path metadata、legacy untracked remainder fallback 与 `END_TURN` cleanup 行为。默认写入范围仅 `src/Riftbound.Engine/MatchSession.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、focused conformance tests，以及必要时的最小 helper/model；frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue、wide equipment runtime、完整 LayerEngine rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln` 仍锁定。A 等待 B diff 后必须验收 focused ordering guard、adjacent LayerEngine / power metadata regression、backend full、`git diff --check`，并新增 4D-04O audit/evidence。项目仍 **NOT READY**。
- 4D-04O LayerEngine power modifier ordering handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_BASELINE_EVIDENCE.md`。A 主控确认 4D-04L / 4D-04M / 4D-04N 已补 source / effect / direct-path / requested / applied / minimum / resulting metadata，但 `PowerModifierLedgerEntry` 仍无显式 application order / timestamp 字段，且 `CardObjectState.NormalizePowerModifierLedger` 与 continuous effect projection 仍按 `EffectId` 排序。下一建议 B 切片只补 ledger-backed power modifier ordering metadata，保持现有 arithmetic、minimum floor、direct path metadata 与 cleanup 行为；不实现完整 LayerEngine。A 侧 baseline focused ordering guard 6/6、adjacent LayerEngine / power metadata regression 37/37、backend full 4447/4447 通过。frontend、card matrix JSON、runtime、tests、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；handoff 本身不派发 B、不打开写锁、不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04N-B LayerEngine direct until-end power mutation ledger exactness 已由 B-Implementation / Godel `019e2c69-aa6d-7701-9525-6a79a50fa210` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_EVIDENCE.md`。本批只做 P1-001 foundation：新增 `ApplyDirectUntilEndPowerModifier`，让 Icevale Archer、Ember Monk、conquest +8、Rengar、battlefield moved +1、optional ready power、Vi double power 等 direct until-end power mutation representatives 追加 source/effect/direct-path ledger metadata，保持现有 arithmetic 与 cleanup 行为。A 侧验收 focused direct-power guard 6/6、adjacent power/layer/trigger regression 185/185、backend full 4447/4447、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue 语义重写、wide equipment runtime、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04N-B 写锁已关闭。本批不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04N LayerEngine direct power ledger handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_BASELINE_EVIDENCE.md`。handoff 阶段只做 A-side P1-001 routing：把 4D-04L / 4D-04M 后仍绕过 `ApplyPowerModifier` ledger 的 direct until-end power mutation representatives 收敛为下一建议 B 切片，要求后续补足 source/effect/direct-path metadata，同时保持现有 arithmetic、turn-end cleanup 与 snapshot compatibility。A 侧 baseline focused direct-power guard 6/6、adjacent power/layer/trigger regression 185/185、backend full 4447/4447、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue 语义重写、wide equipment runtime、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；handoff 本身不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04M-B LayerEngine minimum-power ledger exactness 已由 B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_EVIDENCE.md`。本批只做 P1-001 foundation：为 `MinimumPowerAfterModifier > 0` 的 current power modifier representatives 显式保留 requested delta、applied delta、minimum floor 与 resulting power metadata，保持现有 arithmetic 行为，同时继续明确 `FOUNDATION_ONLY`。A 侧验收 focused minimum-power foundation guard 9/9、adjacent power/layer/minimum regression 16/16、backend full 4447/4447、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue 语义重写、wide equipment runtime、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04M-B 写锁已关闭。本批不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04L-B LayerEngine foundation / source-aware power modifier ledger 已由 B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_EVIDENCE.md`。本批只做 P1-001 foundation：为 current until-end power modifier representatives 建立 source-aware / effect-aware metadata ledger，保持现有 arithmetic 行为，同时明确 `ContinuousEffectState` 只是 snapshot/report view，不是完整 LayerEngine。A 侧验收 focused LayerEngine foundation guard 11/11、adjacent power/layer/equipment regression 141/141、backend full 4447/4447、`git diff --check` 通过。frontend、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue 语义重写、equipment runtime 广泛改造、full official / READY 与 `riftbound-dotnet.sln` 未触碰；4D-04L-B 写锁已关闭。本批不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04K-B Equipment state profile alignment / verifier 已由 B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_EVIDENCE.md`。`CardEquipmentKeywordRules.cs` 现在显式记录 Long Sword P5 equipment state representative manifest，绑定 owner/controller/attachment invariant、controller mismatch no-mutation、controlled opponent-owned target attach、attached equipment follows host base <-> battlefield、host destroyed detach/recall 等现有 verifier anchors；`CardCatalogBaselineTests.cs` 用反射确认这些 anchor 仍存在。A 侧验收 focused state/profile guard 12/12、adjacent equipment regression 195/195、`git diff --check` 通过。本批不改 runtime、frontend、card matrix、full official、P1-001、P1-002 或 READY；profile-verifier 写锁已关闭。
- 4D-04J Equipment remaining breadth refresh / handoff 已建立，入口为 `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_BASELINE_EVIDENCE.md`。本批确认 4D-04I-B 后 equipment residual 不应继续写成一整块：现有 P5 representatives 已覆盖 Long Sword owner/controller attach invariant、controller mismatch rejection、controlled opponent-owned target attach、显式 attached equipment follows host、host destroyed detach / recall；A 侧 baseline focused state / keyword guard 11/11 通过。下一建议 4D-04K 是 profile / verifier alignment，不派发 B、不打开 runtime/test/frontend/matrix 写锁，不关闭 P1-001、P1-002、full official 或 READY。
- 4D-04I-B Ornn dynamic friendly-equipment static recompute 已由 B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_EVIDENCE.md`。服务端现在会在 accepted core command 后，对已在公开 field 且 registry 标记 `AddsFriendlyFieldEquipmentCountToSourceUnitPower` 的 Ornn，从 registered source unit power + 当前 controller 友方公开 field equipment count + until-end power modifier 做窄重算，并重建 authoritative snapshots / prompts。A 侧验证 focused / keyword / LayerEngine-view guard 9/9、adjacent equipment / payment regression 117/117、backend full 4446/4446、`git diff --check` 通过。本批不改 frontend / card matrix / full-official，不关闭完整 LayerEngine、full `百炼`、其他装备静态修正、P1-001、P1-002 或 READY；4D-04I-B runtime / focused-test 写锁已关闭。
- 4D-04I Ornn dynamic equipment static recompute handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_BASELINE_EVIDENCE.md`。该 B 切片锁定 `SFD·085/221` / `SFD·085a/221`《奥恩》已在公开 field 后，友方公开场上装备数量变化时的 dynamic static recompute representative；baseline 验证 focused / keyword / LayerEngine-view guard 6/6、adjacent equipment / payment regression 114/114、`git diff --check` 通过。
- 4D-04H Ornn friendly equipment static power 已由 A 侧直接实现并验收，入口为 `docs/CURRENT_STAGE4D_04H_ORNN_FRIENDLY_EQUIPMENT_STATIC_POWER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04H_ORNN_FRIENDLY_EQUIPMENT_STATIC_POWER_EVIDENCE.md`。服务端现在让 `SFD·085/221` / `SFD·085a/221`《奥恩》从手牌 `PLAY_CARD` 入场时，按 controller 友方公开 field equipment 数量增加入场战力，并在非零加成时写 `friendlyEquipmentPowerBonus` event payload；手牌、face-down、敌方、脏 controller 与非装备对象不计入。A 侧验证 focused / keyword guard 5/5、adjacent equipment / payment regression 114/114、backend full 4443/4443、`git diff --check` 通过。本批不关闭 full `百炼`、其他装备静态修正、dynamic static recompute / LayerEngine、owner/controller breadth、attach lifecycle breadth、frontend、card matrix JSON、P1-001、P1-002 或 READY。
- 4D-04G Armed Assaulter HASTE_READY + Tempered optional attach combination 已由 B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_HANDOFF.md`、`docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_EVIDENCE.md`。服务端现在允许 `SFD·002/221`《武装强袭者》同一 `PLAY_CARD` 可同时提交 `HASTE_READY` 与合法 `TEMPERED_ATTACH:<equipmentObjectId>`，合并支付 base 6 + haste 1 mana / 1 red power，并在结算后 active 入基地且贴附己方合法 `SFD·186/221`《旋转飞斧》。A 侧验证 focused / keyword guard 26/26、adjacent equipment / payment regression 235/235、backend full 4440/4440、`git diff --check` 通过。本批不关闭 full `百炼`、full Haste、Ornn static modifiers、owner/controller breadth、attach lifecycle breadth、LayerEngine、frontend、card matrix JSON、P1-002 或 READY。
- 4D-04F Akshan orange extra equipment steal 已由 B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_HANDOFF.md`、`docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_EVIDENCE.md`。服务端现在允许 `SFD·109/221`《阿克尚》从手牌 `PLAY_CARD` 时选择合法敌方在场装备作为 `AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>` optional cost，额外支付 2 橙色符能；结算时 Akshan 入基地，合法装备移动到 P1 基地、`ControllerId=P1`、`OwnerId` 保留，若为 `武装` 则贴附到 Akshan，且 Akshan 离场时归还 owner base。A 侧验证 focused / keyword guard 28/28、adjacent equipment / payment regression 209/209、backend full 4417/4417、`git diff --check` 通过。本批不关闭 full `百炼`、Ornn / Armed Assaulter、owner/controller breadth、attach lifecycle breadth、LayerEngine、frontend、card matrix JSON、P1-002 或 READY。
- 4D-04E Jax Tempered optional attach trigger 已由 B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_HANDOFF.md`、`docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_EVIDENCE.md`。服务端现在允许 `SFD·119/221` / `SFD·119a/221` Jax 从手牌 `PLAY_CARD` 时选择己方已在场 `SFD·186/221`《旋转飞斧》作为零额外费用 `TEMPERED_ATTACH:<equipmentObjectId>` 代表路径；结算时重验合法后设置《旋转飞斧》`AttachedToObjectId` 为 Jax，并复用既有 `JAX_WEAPON_ATTACH_PAY_1_DRAW_1` 打开 `TRIGGER_PAYMENT`。A 侧验证 focused / keyword guard 41/41、adjacent equipment / payment regression 243/243、backend full 4397/4397、`git diff --check` 通过。本批不关闭 full `百炼`、Ornn / Akshan / Armed Assaulter、owner/controller changes、attach lifecycle breadth、LayerEngine、frontend、card matrix JSON、P1-002 或 READY。
- 4D-04D Tempered optional attach 已由 B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_HANDOFF.md`、`docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_EVIDENCE.md`。服务端现在允许 `SFD·008/221`《哨兵好手》从手牌 `PLAY_CARD` 时选择己方已在场 `SFD·186/221`《旋转飞斧》作为零额外费用 `TEMPERED_ATTACH:<equipmentObjectId>` 代表路径；结算时重验合法后设置 `AttachedToObjectId` 并发出 `EQUIPMENT_ATTACHED` / `TEMPERED_OPTIONAL_ATTACH`。A 侧验证 focused / keyword guard 14/14、adjacent equipment regression 139/139、backend full 4380/4380、`git diff --check` 通过。frontend、card matrix JSON、full `百炼`、Jax / Ornn / Akshan / Armed Assaulter special branches、LayerEngine、PaymentEngine broad refactor、battle lifecycle 与 `riftbound-dotnet.sln` 未触碰。P1-002、full-card matrix、frontend final validation 与 READY 均未关闭。
- 4D-04C Agile equipment direct attach 已由 B-Implementation / Singer `019e2b7e-8eed-7803-b03a-ab9bf538171c` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_HANDOFF.md`、`docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_EVIDENCE.md`。服务端现在让 `SFD·022/221`、`SFD·056/221`、`SFD·064/221`、`SFD·186/221` 从手牌 `PLAY_CARD` 时公开/校验己方单位目标，并在结算时贴附到目标单位。A 侧验证 focused 57/57、rejected/shape 113/113、adjacent 207/207、keyword guard 17/17、historical recheck 6/6、backend full 4368/4368、`git diff --check` 通过。frontend、card matrix JSON、broad equipment rewrite、LayerEngine、battle lifecycle、PaymentEngine broad refactor、Azir/Maduli/Ezreal historical slices 与 `riftbound-dotnet.sln` 未触碰。P1-002、full-card matrix、frontend final validation 与 READY 均未关闭。
- 4D-04B Equipment keyword status split 已由 B-Implementation / Confucius `019e2b70-60f0-7a50-9a95-dd497c62ff96` 实现并由 A 验收，入口为 `docs/CURRENT_STAGE4D_04B_EQUIPMENT_KEYWORD_STATUS_SPLIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04B_EQUIPMENT_KEYWORD_STATUS_SPLIT_EVIDENCE.md`。写锁仅覆盖 `CardEquipmentKeywordRules.cs`、`MatchSession.cs`、`CardCatalogBaselineTests.cs`；frontend、card matrix JSON、broad equipment rewrite、LayerEngine 与 `riftbound-dotnet.sln` 未触碰。A 侧验证 focused 4/4、adjacent 98/98、broader keyword 8/8、backend full 4355/4355、`git diff --check` 通过。P1-002、LayerEngine、full-card matrix、frontend final validation 与 READY 均未关闭。
- 4D-04A Keyword deferred surface handoff / baseline 已完成，入口为 `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_AUDIT.md`、`docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_BASELINE_EVIDENCE.md`。本批从 4D-03AT matrix evidence overlay 转回 P1-002 keyword execution-boundary 规则模型，建议后续 4D-04B 先处理 equipment keyword execution-boundary status split。A 侧已验证 keyword catalog/profile 8/8 passed 与 representative keyword fixtures 144/144 passed；不派发 B，不开 runtime / test / frontend / matrix 写锁，不关闭 P1-002、LayerEngine、full-card matrix、frontend final validation 或 READY。
- 4D-03AT Azir matrix evidence alignment 已完成，入口为 `docs/CURRENT_STAGE4D_03AT_AZIR_MATRIX_EVIDENCE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AT_AZIR_MATRIX_EVIDENCE_ALIGNMENT_EVIDENCE.md`。本批只为 `SFD·050/221` / `SFD·050a/221` / `FU-105abedc17` 记录 `stage4D03AT` representative evidence overlay，降低 representative automated-test-evidence blocker；`stage4B.freezeStatus`、`stage4B.statusFlags`、`fullOfficial=false`、P0/P1、frontend final validation 与 READY 均不变。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_HANDOFF.md` 已把下一枚 Azir full-text follow-up 锁定为 `SFD·050/221` / `SFD·050a/221` 阿兹尔 optional armament reattach branch：目标单位已配武装时，可以选择 0 或 1 件武装贴附到 Azir。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Azir / ActivateAbility / MoveUnit / PaymentEngine 194/194 通过；Azir / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 387/387 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Azir `FU-105abedc17` 当前仍为 `fullOfficial=false`；4D-03AM unarmed / no-reattach position-swap evidence 不能代理 optional armament reattach full official branch，后续 4D-03AT 只记录 matrix evidence overlay，不升级 full-official。
- 用户恢复 active goal 后，4D-03AS-B 已派发给 B-Implementation / Raman `019e2b49-28c3-7ad2-b3f8-ef1347b56996`。B 派发期间独占 Azir optional armament reattach runtime/test 写锁：`CoreRuleEngine.cs`、`MatchSession.cs`、`P4ActivatedAbilityCatalog.cs`、`tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs`；可选且仅最小必要时触碰 `src/Riftbound.Contracts/Protocol.cs` / `GameCommandJsonMapper.cs` / focused contract serialization tests。该写锁已在 A 验收后关闭；frontend、card matrix JSON、broad equipment rewrite、unrelated abilities、swift timing breadth、battle lifecycle、LayerEngine、HASTE_READY 与 `riftbound-dotnet.sln` 均未触碰。
- `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_EVIDENCE.md` 已记录 4D-03AS-B implemented / A-validated：focused 204/204、adjacent 397/397、backend full 4355/4355、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md` 已刷新为 readiness improved / matrix evidence overlay recorded：Azir `FU-105abedc17` optional armament reattach blocker 已有 accepted runtime evidence and 4D-03AT matrix evidence overlay, but `fullOfficial=false` remains unchanged.
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_HANDOFF.md` 已把下一枚 Gatekeeper Maduli static slice 锁定为 `UNL-144/219` 守门者马杜里 `我无法变为活跃状态。` cannot-ready representative。
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Maduli / Gatekeeper / CrimsonRose / HuntReadyGuardTests 61/61 通过；Maduli / Gatekeeper / CrimsonRose / HuntReadyGuardTests / ActivateAbility / PaymentEngine / ActionPrompt / GameHub / Priority 371/371 通过；`git diff --check` 通过。
- 4D-03AR-B 已派发并验收，B-Implementation / Schrodinger `019e291f-dd42-75c3-8f12-05220a1629df` 的 Maduli cannot-ready static runtime/test 写锁已关闭；frontend、card matrix、broad LayerEngine、unrelated ready effects、HASTE_READY / swift timing、battle lifecycle 与 `riftbound-dotnet.sln` 均未触碰。
- `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_EVIDENCE.md` 已记录 4D-03AR-B implemented / A-validated：focused 65/65、adjacent 375/375、backend full 4345/4345、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AR_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Maduli `FU-d5d5707b0e` matrix readiness improved by 4D-03AN movement + 4D-03AR cannot-ready static evidence, but matrix JSON remains unchanged and `fullOfficial=false` until a future explicit matrix write window.
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_HANDOFF.md` 已把下一枚 P0-005 test-only guard 锁定为 implemented HASTE_READY registry/profile set 与 existing P4 fixture evidence 的 catalog-bound verifier。
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：PaymentEngineCoverageAuditTests / HasteOptional / HasteReady 102/102 通过；PaymentEngineCoverageAuditTests / HasteOptional / HasteReady / PlayCard / ActionPrompt / GameHub / Priority 442/442 通过；`git diff --check` 通过。
- 4D-03AQ-B 已派发并验收，B-Implementation / Halley `019e290f-73b2-7d62-a19e-2a252ad6ef2e` 的 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` test-only 写锁已关闭；runtime、frontend、card matrix、broad Haste rewrite、battle lifecycle 与 `riftbound-dotnet.sln` 均未触碰。
- `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_EVIDENCE.md` 已记录 4D-03AQ-B test-only verifier implemented / A-validated：focused 105/105、adjacent 445/445、backend full 4341/4341、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_HANDOFF.md` 已把下一枚 P0-005 focused guard 锁定为 `SFD·029/221` / `SFD·029a/221` 雷克塞 HASTE_READY extra 1 mana + 1 red typed power exactness representative。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Rek'Sai / HasteOptional / PaymentEngine 109/109 通过；Rek'Sai / HasteOptional / PaymentEngine / PlayCard / ActionPrompt / GameHub / Priority 425/425 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Rek'Sai `FU-1945f6918c` 当前仍为 `fullOfficial=false`；4C-52 ordinary no-optional evidence 与 old P4 HASTE_READY fixtures 不代理 red exactness、strong/overflow、non-hand haste granting、LayerEngine 或 FAQ breadth。
- 4D-03AP-B 已派发给 B-Implementation / Archimedes `019e2900-bcc5-7763-8f3a-db41a0aaa0a1`，默认 focused test write lock；runtime edits only if tests expose an actual typed-red payment gap。
- `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_EVIDENCE.md` 已记录 4D-03AP-B test-only guard implemented / A-validated：focused 17/17、handoff focused 126/126、adjacent 442/442、backend full 4338/4338、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_HANDOFF.md` 已把下一枚 P0-005 implementation slice 锁定为 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔 blue swift no-target self move-to-base activated ability representative。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_BASELINE_EVIDENCE.md` 已记录 implementation-before baseline：Ezreal / ActivateAbility / MoveUnit / PaymentEngine 179/179 通过；Ezreal / ActivateAbility / MoveUnit / PaymentEngine / ActionPrompt / GameHub / Priority 372/372 通过；`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` 已确认 Ezreal `FU-2dca1ad450` 当前仍为 `fullOfficial=false`；4C-49 ordinary play-unit evidence 不代理 blue swift move-to-base、attack / defense trigger、cannot-combat-damage static、full swift timing 或 FAQ breadth。
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md` 已记录 4D-03AN-B implemented / A-validated：focused 25/25、handoff focused 188/188、adjacent 381/381、backend full 4293/4293、`git diff --check` 通过。
- `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md` 已记录 4D-03AM-B implemented / A-validated：focused 23/23、handoff focused 191/191、adjacent 384/384、backend full 4268/4268、`git diff --check` 通过。
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 已确认 active goal 的 READY 门槛仍未满足：P0/P1 未清零，1009 / 811 card matrix 仍无 full-official coverage，frontend build / Chrome smoke / formal 18-step 仍需在最终代码状态 fresh run。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_EVIDENCE.md` 已记录 4D-03AO-B implemented / A-validated：focused 28/28、handoff focused 207/207、adjacent 400/400、backend full 4321/4321、`git diff --check` 通过。

## 2. Dispatch Queue

| Queue | Owner | Status | Purpose | Write scope | Must not touch |
|---|---|---|---|---|---|
| 4D-NEXT-A | A 主控 | 4D-03AX focused verifier accepted / paused after batch | 记录 PaymentEngine legend / battlefield / trigger resource action manifest 验收、写锁关闭与当前暂停点 | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`、`docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`、checkpoint / completion / server audit / closure docs、新增 4D-03AX audit / evidence | runtime、frontend runtime、card matrix JSON、full-official upgrade、`riftbound-dotnet.sln` |
| 4D-04Q-B | B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c` | Implemented and A-validated | LayerEngine static aura source lifecycle foundation | completed narrow runtime / focused-test diff in `MatchSession.cs`, `OrnnFriendlyEquipmentStaticPowerTests.cs`, `ConformanceFixtureRunnerTests.cs` | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle、wide equipment runtime/full `百炼`、full LayerEngine/timestamp dependency graph、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04P-B | B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e` | Implemented and A-validated | LayerEngine minimum-power + applied-order sequence representative | completed focused-test diff in `ConformanceFixtureRunnerTests.cs` and fixture JSON | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle、wide equipment runtime、full LayerEngine rewrite、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04O-B | B-Implementation / Leibniz `019e2c86-8abd-74c3-8c3d-3e8ccd5453ab` | Implemented and A-validated | LayerEngine power modifier explicit ordering metadata | completed narrow runtime / focused-test diff in `MatchSession.cs`, `CoreRuleEngine.cs`, focused tests | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle、wide equipment runtime、full LayerEngine rewrite、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04N-B | B-Implementation / Godel `019e2c69-aa6d-7701-9525-6a79a50fa210` | Implemented and A-validated | LayerEngine direct until-end power mutation ledger exactness | completed narrow runtime / focused-test diff in `CoreRuleEngine.cs`, `TriggerPaymentTests.cs`, `ConformanceFixtureRunnerTests.cs` | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle、wide equipment runtime、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04M-B | B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` | Implemented and A-validated | LayerEngine minimum-power power-modifier ledger exactness | completed narrow runtime / focused-test diff in `MatchSession.cs`, `CoreRuleEngine.cs`, `ConformanceFixtureRunnerTests.cs` | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle、wide equipment runtime、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04L-B | B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` | Implemented and A-validated | LayerEngine foundation / source-aware power modifier ledger | completed narrow runtime / focused-test diff in `MatchSession.cs`, `CoreRuleEngine.cs`, `SwitcherooGuardTests.cs` | frontend runtime、card matrix JSON、broad PaymentEngine、battle lifecycle/task queue rewrite、wide equipment runtime、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04K-B | B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` | Implemented and A-validated | Equipment state representative profile alignment / verifier | completed profile / focused-test diff in `CardEquipmentKeywordRules.cs` and `CardCatalogBaselineTests.cs` | runtime semantics、frontend runtime、card matrix JSON、broad LayerEngine、PaymentEngine、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-04I-B | B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` | Implemented and A-validated | Ornn dynamic friendly-equipment static recompute representative | completed narrow runtime / focused tests | frontend runtime、card matrix JSON、broad LayerEngine、unrelated equipment statics、PaymentEngine、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-04H-A | A 主控 | Implemented and A-validated | Ornn friendly-equipment static power entry-time representative | completed narrow runtime / focused tests / profile guard | frontend runtime、card matrix JSON、full `百炼`、dynamic LayerEngine/static recompute、owner/controller breadth、attach lifecycle breadth、`riftbound-dotnet.sln` |
| 4D-04G-B | B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` | Implemented and A-validated | Armed Assaulter same-command HASTE_READY + Tempered attach combination representative | completed narrow runtime / focused tests / profile guard | frontend runtime、card matrix JSON、full `百炼`、full Haste、Ornn static modifiers、Akshan/Jax branches、LayerEngine、PaymentEngine broad refactor、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-04F-B | B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` | Implemented and A-validated | Akshan orange-orange optional enemy equipment control / weapon attach / leave-play return representative | completed narrow runtime / focused tests | frontend runtime、card matrix JSON、full `百炼`、Ornn / Armed Assaulter branches、LayerEngine、PaymentEngine broad refactor、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-04E-B | B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` | Implemented and A-validated | Jax `百炼` optional attach to Spinning Axe opens existing weapon-attach trigger payment | completed narrow runtime / focused tests / profile guard | frontend runtime、card matrix JSON、full `百炼`、Ornn / Akshan / Armed Assaulter branches、LayerEngine、PaymentEngine broad refactor、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-04D-B | B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` | Implemented and A-validated | `SFD·008/221` `百炼` optional attach to `SFD·186/221` representative | completed narrow runtime / focused tests / profile guard | frontend runtime、card matrix JSON、full `百炼`、Jax / Ornn / Akshan / Armed Assaulter special branches、LayerEngine、PaymentEngine broad refactor、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-04C-B | B-Implementation / Singer `019e2b7e-8eed-7803-b03a-ab9bf538171c` | Implemented and A-validated | printed Agile equipment direct-play attach representative | completed narrow runtime / focused tests / fixture migration | frontend runtime、card matrix JSON、broad equipment rewrite、LayerEngine、battle lifecycle、PaymentEngine broad refactor、Azir/Maduli/Ezreal historical slices、`riftbound-dotnet.sln` |
| 4D-04B-B | B-Implementation / Confucius `019e2b70-60f0-7a50-9a95-dd497c62ff96` | Implemented and A-validated | equipment keyword execution-boundary status split | completed narrow code/test diff | frontend runtime、card matrix JSON、broad equipment runtime rewrite、LayerEngine、`riftbound-dotnet.sln` |
| 4D-03AS-B | B-Implementation / Raman `019e2b49-28c3-7ad2-b3f8-ef1347b56996` | Implemented and A-validated | 实现 Azir optional armament reattach branch | completed runtime / focused tests | frontend runtime、card matrix JSON、broad equipment rewrite、unrelated abilities、swift timing breadth、battle lifecycle、LayerEngine、HASTE_READY、`riftbound-dotnet.sln` |
| 4D-03AS-E | E-Review | 4D-03AT evidence overlay recorded | 检查 Azir `FU-105abedc17` optional armament blocker and full-official gate | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` Azir evidence overlay and card coverage docs | full-official upgrade、Stage 4B status count changes、unrelated matrix rows |
| 4D-03AT-E | E-Review | Completed and closed | 将 accepted Azir representative automated evidence 写入 matrix overlay | `FU-105abedc17` / `SFD·050/221` / `SFD·050a/221` matrix evidence only | runtime、tests、frontend、unrelated matrix rows、READY |
| 4D-03AR-B | B-Implementation / Schrodinger `019e291f-dd42-75c3-8f12-05220a1629df` | Implemented and A-validated | 实现 Gatekeeper Maduli cannot-ready static representative | completed runtime / focused tests | frontend runtime、card matrix JSON、broad LayerEngine rewrite、unrelated ready effects、HASTE_READY / swift timing、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AQ-B | B-Implementation / Halley `019e290f-73b2-7d62-a19e-2a252ad6ef2e` | Implemented and A-validated | 新增 HASTE_READY catalog-bound coverage verifier | completed focused verifier; no runtime changes | `src/**`、frontend runtime、card matrix JSON、broad Haste rewrite、strong/overflow、non-hand haste granting、LayerEngine、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AP-B | B-Implementation / Archimedes `019e2900-bcc5-7763-8f3a-db41a0aaa0a1` | Implemented and A-validated | 补强 Rek'Sai HASTE_READY red typed payment exactness focused tests / evidence | completed focused tests; no runtime changes | frontend runtime、card matrix JSON、broad Haste rewrite、strong/overflow、non-hand haste granting、LayerEngine、battle lifecycle、`riftbound-dotnet.sln` |
| 4D-03AP-E | E-Review | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Rek'Sai `FU-1945f6918c` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AO-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Ezreal blue swift no-target self move-to-base representative | completed runtime / focused tests | frontend runtime、card matrix JSON、LayerEngine broad rewrite、attack / defense trigger runtime、unrelated battle lifecycle / cleanup queue files、unrelated activated abilities、`riftbound-dotnet.sln` |
| 4D-03AO-E | E-Review / Poincare | Read-only readiness audit recorded in `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` | 检查 Ezreal `FU-2dca1ad450` matrix readiness and full-official blockers | card coverage docs in read-only mode | `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` unless A opens a future matrix write window |
| 4D-03AM-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Azir green swift position-swap representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-03AN-B | B-Implementation / Raman `019e2257-8d40-7630-9201-28df44dd689a` | Implemented and A-validated | 实现 Gatekeeper Maduli purple enemy-battlefield move representative | completed runtime / focused tests | frontend runtime、card matrix JSON、unrelated files |
| 4D-FE-C | C-Review / Copernicus | Read-only preflight recorded in `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md` | 准备 final frontend contract / Chrome smoke fresh-run checklist | DevUi scripts and existing frontend-contract docs in read-only mode; no code write unless A opens a separate C write window | server runtime, card matrix, local rule inference in frontend |
| 4D-MATRIX-E | E-Review / Poincare | Azir 4D-03AT evidence overlay recorded; other rows read-only | 检查 latest payment representative rows and full-official blockers | Azir evidence overlay only in this batch; other card coverage docs read-only | unrelated matrix rows、full-official upgrade、READY |

## 3. Exclusive Writelocks

- 4D-03AW focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03AX focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03AV focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03AU is A-side handoff / baseline only. No B worker is dispatched, no runtime/test/frontend/matrix write lock is open, and a future 4D-03AV verifier requires a fresh explicit dispatch. `riftbound-dotnet.sln` remains locked.
- 4D-04Q-B static aura source lifecycle runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime/full `百炼` breadth, full LayerEngine/timestamp dependency graph rewrite, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04P-B LayerEngine minimum-power ordering runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime/static aura rewrite, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04O-B LayerEngine power modifier ordering runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04N-B LayerEngine direct until-end power mutation ledger runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantic rewrites, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04M-B LayerEngine minimum-power ledger exactness runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantic rewrites, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04L-B LayerEngine foundation runtime / focused-test write lock is closed after A validation and commit-ready evidence. Frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantic rewrites, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04K-B profile-verifier write lock is closed after A validation and commit-ready evidence. Runtime semantics, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-04J is A-side handoff / baseline only. No B worker is dispatched; no runtime, test, frontend or matrix write lock is open.
- 4D-04I-B Ornn dynamic static recompute runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04H-A Ornn friendly-equipment static power runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04G-B Armed Assaulter HASTE_READY + Tempered combination runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04F-B Akshan orange extra equipment steal runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04E-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04D-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04C-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04B-B code / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-04A remains closed as A-side handoff / baseline only; 4D-04B has now been implemented and accepted as the follow-up narrow slice.
- 4D-03AS-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-03AT Azir matrix evidence write window is closed after recording `stage4D03AT`; no frontend or matrix write lock is open now.
- 4D-03AR-B runtime / focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-03AQ-B test-only write lock is closed after A validation and commit-ready evidence.
- 4D-03AP-B focused-test write lock is closed after A validation and commit-ready evidence.
- 4D-03AO-B runtime / focused-test write lock is closed after A validation and commit.
- D/A audit docs for 4D-03AO are recorded; no further 4D-03AO runtime edits should occur without a fresh dispatch.
- C remains read-only while B might alter server prompt shape. Any frontend write window must wait until server `ActionPrompt` payload and event shape are stable.
- E returns to read-only after 4D-03AT. The matrix must not be upgraded to `fullOfficial=true` for Azir, Maduli, Ezreal or other latest representatives merely because focused runtime evidence passed.
- No parallel task may edit card matrix JSON, frontend stores, `ActionPrompt` contracts, battle state machine, stack, cleanup, hidden-info redaction, or E2E fixtures without an explicit owner and a fresh write-lock note.

## 3.1 4D-04Q-A Handoff Gate Accepted

A accepts the 4D-04Q handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04L through 4D-04P are accepted and their runtime / focused-test write locks are closed.
3. Current code has Ornn dynamic friendly-equipment static recompute and battlefield static power representative evidence, but no stable static aura source / lifecycle continuous-effect or equivalent server audit view.
4. Focused static-aura / LayerEngine-view guard passed 10/10.
5. Adjacent static / continuous-effect / equipment regression passed 48/48.
6. Backend full passed 4450/4450.
7. The handoff itself did not modify runtime, tests, frontend or card matrix JSON, and it did not dispatch B.
8. P1-001, P1-002, full-official card matrix and READY remain open.

Historical pause point: A stopped here until the user resumed and opened 4D-04Q-B implementation; current 4D-04Q-B acceptance is recorded in section 3.3.

## 3.2 4D-04Q-B Dispatch Gate Accepted

A accepts the 4D-04Q-B dispatch because all of the following are true:

1. The user resumed the active goal after the 4D-04Q handoff pause point.
2. The active goal explicitly keeps A in architecture / planning / validation mode and allows sub-agent division of work.
3. The B implementation scope is narrow and matches `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_HANDOFF.md`.
4. Runtime / focused-test write scope is limited to Ornn dynamic static recompute, battlefield static power, `ContinuousEffectState` / snapshot helper surfaces, and focused representative tests.
5. Frontend, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue, wide equipment runtime/full `百炼`, full LayerEngine/timestamp dependency graph, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
6. A will not accept the B diff until focused static-aura / LayerEngine-view guard, adjacent static / continuous-effect / equipment regression, backend full and `git diff --check` pass.

## 3.3 4D-04Q-B Acceptance Gate Accepted

A accepts the 4D-04Q-B implementation because all of the following are true:

1. B stayed inside the runtime / focused-test write lock: `MatchSession.cs`, `OrnnFriendlyEquipmentStaticPowerTests.cs`, and `ConformanceFixtureRunnerTests.cs`.
2. Runtime arithmetic remains unchanged: static aura metadata is a derived foundation view, not a second power application.
3. Ornn friendly-equipment static aura metadata exposes source/target, participants, condition, lifecycle, power delta, base power and effective power.
4. Ornn source leaving field removes static aura metadata.
5. Battlefield all-units +1 representative exposes participant static aura metadata before combat and removes it after participants leave field.
6. Snapshot `timing.continuousEffects[]` exposes static aura condition/lifecycle/participants when present.
7. Focused static-aura / LayerEngine-view guard passed 11/11.
8. Adjacent static / continuous-effect / equipment regression passed 49/49.
9. Backend full passed 4451/4451.
10. `git diff --check` passed.
11. Frontend, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue, wide equipment runtime/full `百炼`, full LayerEngine/timestamp dependency graph, fullOfficial / READY and `riftbound-dotnet.sln` were not touched.

Pause point: 4D-04Q-B is accepted and its write lock is closed. The project remains **NOT READY**.

## 3.4 4D-04P-A Handoff Gate Accepted

A accepts the 4D-04P handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04M-B and 4D-04O-B are already accepted and their runtime / focused-test write locks are closed.
3. Current code has minimum-power requested/applied/minimum/resulting metadata and explicit applied order metadata, but no same-target sequence representative that combines minimum floor behavior with applied order.
4. Existing Blastcone coverage proves single minimum-floor metadata; existing Power Bind Echo coverage proves same-target `[1, 2]` ordering; existing Extortion coverage proves zero-applied floor does not create misleading ledger.
5. The next suggested implementation is narrow: prove or minimally support a minimum floor + ordering representative while preserving current arithmetic, direct-path metadata, legacy fallback and `END_TURN` cleanup.
6. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln`.
7. Timestamp dependency graph, source-ordering breadth, keyword gain/loss ordering, multiple equipment/static aura interactions, complete minimum-power ordering, full official coverage and READY remain open.

A-side baseline commands:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **focused minimum-power ordering guard 7/7 passed; adjacent minimum / ordering / continuous-effect regression 15/15 passed; backend full 4449/4449 passed**.

This record establishes the 4D-04P handoff / baseline and stops before dispatching B. The project remains **NOT READY**.

## 3.5 4D-04P-B Dispatch Gate Accepted

A dispatches 4D-04P-B because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04P-A handoff / baseline is accepted, and its baseline evidence is green.
3. 4D-04M-B and 4D-04O-B are already accepted and their runtime / focused-test write locks are closed.
4. Current code has minimum-power requested/applied/minimum/resulting metadata and explicit applied order metadata, but lacks a same-target sequence representative combining minimum floor interaction with applied order.
5. The write scope is narrow and owned by B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e`: `CoreRuleEngine.cs`, `MatchSession.cs`, focused minimum-power / ordering tests, and optional minimal fixture/helper/model only.
6. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime/static aura rewrite, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln`.

A-side expected acceptance commands after B diff:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

This dispatch opens the 4D-04P-B B-side runtime / focused-test write lock. The project remains **NOT READY**.

## 3.6 4D-04P-B Acceptance Gate Accepted

A accepts the 4D-04P-B diff because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04P-A handoff / baseline and 4D-04P-B dispatch gates are already recorded.
3. The diff stays inside the narrow focused-test scope: `ConformanceFixtureRunnerTests.cs` plus one fixture JSON.
4. No runtime, frontend, card matrix, fullOfficial / READY, broad PaymentEngine, battle lifecycle or wide equipment runtime file is touched.
5. The new same-target sequence proves Smoke Bomb floor, Extortion zero-applied floor and Power Bind later +1 can coexist without misleading zero ledger or skipped visible order.
6. State ledger, `ContinuousEffectState` and snapshot view expose matching requested/applied/minimum/resulting/base/effective/order metadata for the two visible modifiers.
7. Smoke Bomb end-turn cleanup now asserts state ledger, continuous effects and snapshot continuous effect view all clear the expired power modifier.
8. Existing Blastcone, Power Bind Echo, Extortion, Smoke Bomb, continuous-effect ordering shape and legacy fallback regressions remain green.
9. Forbidden closure surfaces remain open: full LayerEngine, timestamp/dependency/source ordering, keyword gain/loss ordering, multiple equipment/static aura interactions, complete minimum-power ordering beyond this representative, P1-002, full official coverage and READY.

A-side accepted commands:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~PowerModifierAppliedOrderFollowsPowerBindEchoAppendSequence|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~ContinuousEffectPowerModifierAppliedOrderSurvivesEffectIdNormalization"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~Extortion|FullyQualifiedName~SmokeBomb|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

Result: **focused minimum-power ordering guard 8/8 passed; adjacent minimum / ordering / continuous-effect regression 16/16 passed; backend full 4450/4450 passed; git diff --check passed**.

This record accepts the 4D-04P-B implementation and closes the B runtime / focused-test write lock. The project remains **NOT READY**.

## 3.7 4D-04O-B Acceptance Gate Accepted

A accepts the 4D-04O-B diff because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04O-A handoff / baseline and 4D-04O-B dispatch gates are already recorded.
3. `PowerModifierLedgerEntry` and `ContinuousEffectState` now expose nullable `AppliedOrder`, and snapshot `timing.continuousEffects[]` exposes `appliedOrder` only when present.
4. `CoreRuleEngine.ApplyPowerModifier` and `ApplyDirectUntilEndPowerModifier` assign append-based order for nonzero applied deltas while preserving existing arithmetic, source/effect metadata, requested/applied/minimum/resulting metadata and cleanup behavior.
5. Same-target multiple modifier ordering is covered by the Power Bind Echo representative with state / continuous effect / snapshot `[1, 2]`.
6. A shape test proves ordered ledger entries are not re-sorted by `EffectId`, and legacy untracked power modifier view does not emit `appliedOrder`.
7. Rengar, Icevale, Switcheroo and minimum-power representatives remain covered.
8. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln`.
9. Timestamp dependency graph, source-ordering breadth, keyword gain/loss ordering, multiple equipment/static aura interactions, complete minimum-power ordering, full official coverage and READY remain open.

A-side accepted commands:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Rengar|FullyQualifiedName~Icevale|FullyQualifiedName~Switcheroo|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

Result: **focused ordering guard 6/6 passed; adjacent LayerEngine / power metadata regression 39/39 passed; backend full 4449/4449 passed; git diff --check passed**.

This record accepts the 4D-04O-B implementation and closes the B runtime / focused-test write lock. The project remains **NOT READY**.

## 3.8 4D-04O-B Dispatch Gate Accepted

A dispatches 4D-04O-B because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04O-A handoff / baseline is accepted, and its baseline evidence is green.
3. 4D-04L-B, 4D-04M-B and 4D-04N-B are already accepted and their runtime / focused-test write locks are closed.
4. Current code has ledger-backed source / effect / direct-path / requested / applied / minimum / resulting metadata, but no explicit application order / timestamp field.
5. `CardObjectState.NormalizePowerModifierLedger` and `BuildContinuousEffectStates` still sort by `EffectId`; the next diff must expose stable ordering metadata instead of relying on projection order or parsing `EffectId`.
6. The write scope is narrow and owned by B-Implementation: `MatchSession.cs`, `CoreRuleEngine.cs`, focused conformance tests, and an optional minimal helper/model only.
7. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln`.

A-side expected acceptance commands after B diff:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Rengar|FullyQualifiedName~Icevale|FullyQualifiedName~Switcheroo|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

This dispatch opens the 4D-04O-B B-side runtime / focused-test write lock. The project remains **NOT READY**.

## 3.9 4D-04O-A Handoff Gate Accepted

A accepts the 4D-04O handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04L-B, 4D-04M-B and 4D-04N-B are already accepted and their runtime / focused-test write locks are closed.
3. Current code has ledger-backed source / effect / direct-path / requested / applied / minimum / resulting metadata, but no explicit application order / timestamp field.
4. `CardObjectState.NormalizePowerModifierLedger` and `BuildContinuousEffectStates` still sort by `EffectId`, so a future LayerEngine consumer should not infer same-layer order from projection order or from parsing `EffectId`.
5. The next suggested implementation is narrow: expose stable ordering metadata for ledger-backed until-end power modifiers while preserving existing arithmetic, minimum floor behavior, direct path metadata and `END_TURN` cleanup.
6. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, full LayerEngine rewrite, fullOfficial / READY and `riftbound-dotnet.sln`.
7. Timestamp dependency graph, source-ordering breadth, keyword gain/loss ordering, multiple equipment/static aura interactions, complete minimum-power ordering, full official coverage and READY remain open.

A-side baseline commands:

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Rengar|FullyQualifiedName~Icevale|FullyQualifiedName~Switcheroo|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~LayerEngine"
```

```sh
set -e
source scripts/dev-env.sh
dotnet test Riftbound.slnx --no-restore
```

Result: **focused ordering guard 6/6 passed; adjacent LayerEngine / power metadata regression 37/37 passed; backend full 4447/4447 passed**.

This record establishes the 4D-04O handoff / baseline and stops before dispatching B. The project remains **NOT READY**.

## 3.10 4D-04N-B Acceptance Gate Accepted

A accepts the 4D-04N-B diff because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04L-B and 4D-04M-B are already accepted and their runtime / focused-test write locks are closed.
3. `ApplyDirectUntilEndPowerModifier` keeps existing `Power` / `UntilEndOfTurnPowerModifier` arithmetic and only appends ledger entries when applied delta is nonzero.
4. Icevale Archer, Ember Monk, Rengar, Vi, conquest +8, battlefield moved +1 and optional ready power direct mutation paths now share ledger-backed metadata without moving to full LayerEngine.
5. Icevale payment representative verifies state ledger, `ContinuousEffectState` and snapshot source/effect/requested/applied/minimum/resulting metadata.
6. Rengar representative verifies state/snapshot metadata and confirms `END_TURN` clears direct ledger metadata with the aggregate modifier.
7. Existing 4D-04L/04M source/effect/minimum metadata, adjacent trigger/payment regressions and backend full suite remain green.
8. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln`.
9. Timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, complete minimum-power ordering, full official coverage and READY remain open.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn|FullyQualifiedName~IcevaleArcherAttackPaymentAcceptedAppliesTemporaryPowerMinusOne|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed|FullyQualifiedName~P4ActivateAbilityCommandResolvesViDoublePowerSkillOnStack|FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~Rengar|FullyQualifiedName~ViDoublePower|FullyQualifiedName~EmberMonk|FullyQualifiedName~HasteOptional"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

Result: **focused direct-power guard 6/6 passed; adjacent power / layer / trigger regression 185/185 passed; backend full 4447/4447 passed; git diff --check passed**.

This record accepts the 4D-04N-B implementation and closes the B runtime / focused-test write lock. The project remains **NOT READY**.

## 3.11 4D-04M-B Acceptance Gate Accepted

A accepts the 4D-04M-B diff because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04L-B is already accepted and its runtime / focused-test write lock is closed.
3. `PowerModifierLedgerEntry.PowerDelta` remains applied delta, while `RequestedPowerDelta`, `MinimumPower` and `ResultingPower` preserve the minimum-power floor audit metadata.
4. `CoreRuleEngine.ApplyPowerModifier` keeps existing `Power` / `UntilEndOfTurnPowerModifier` arithmetic and only appends ledger entries when applied delta is nonzero.
5. Blastcone Sprout now verifies requested `-2`, applied `-1`, minimum `1` and resulting `1` in state, continuous effect and snapshot metadata.
6. Extortion applied-zero floor path keeps no-mutation compatibility and does not create a misleading zero-delta continuous effect view.
7. Existing Switcheroo source/effect metadata, minimum-power representatives and adjacent power/layer regressions remain green.
8. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln`.
9. Timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, complete minimum-power ordering, full official coverage and READY remain open.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor|FullyQualifiedName~CoreRuleEnginePlaysSiphonEnergyBattlefieldPowerSplit|FullyQualifiedName~CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3|FullyQualifiedName~CoreRuleEnginePlaysSmokeBombPowerFloorThroughStack|FullyQualifiedName~CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn|FullyQualifiedName~CoreRuleEnginePlaysExtortionPowerFloorDrawThroughStack|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForThousandTailedWatcher|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PowerModifier|FullyQualifiedName~MinimumPower|FullyQualifiedName~Blastcone|FullyQualifiedName~SiphonEnergy|FullyQualifiedName~ThousandTailed|FullyQualifiedName~SmokeBomb|FullyQualifiedName~Extortion|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~UntilEndOfTurnPowerModifier"
```

```sh
git diff --check
```

Result: **focused minimum-power foundation guard 9/9 passed; adjacent power / layer / minimum regression 16/16 passed; git diff --check passed**.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: **backend full 4447/4447 passed**.

## 3.12 4D-04L-B Acceptance Gate Accepted

A accepts the 4D-04L-B diff because all of the following are true:

1. Current repo state remains on `main` with expected untracked `riftbound-dotnet.sln` still untouched.
2. `CoreRuleEngine.ApplyPowerModifier` keeps existing `Power` / `UntilEndOfTurnPowerModifier` arithmetic and appends source/effect-aware ledger metadata.
3. `MatchSession` exposes ledger-backed power modifiers through `ContinuousEffectState` / snapshot view while preserving old `powerDelta` / `basePower` / `effectivePower` fields.
4. Switcheroo now verifies source/effect metadata in both state and snapshot: `sourceObjectId`, `sourceCardNo`, `effectKind`, `sourcePath`, `FOUNDATION_ONLY` and deferred residuals.
5. Existing Ornn / Switcheroo / trigger-payment / battle-response / pending-task / turn-end cleanup representatives remain green.
6. Forbidden surfaces remain locked: frontend runtime, card matrix JSON, broad PaymentEngine, battle lifecycle/task queue semantics, wide equipment runtime, fullOfficial / READY and `riftbound-dotnet.sln`.
7. Timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, minimum-power layering and full official coverage remain open after this batch.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~PendingTaskQueueDoesNotExposeUndamagedZeroPowerFromPowerModifierAsStateBasedTask|FullyQualifiedName~TurnEndCleanupRestoresNegativeBasePowerAfterPositiveModifierExpires|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~Switcheroo|FullyQualifiedName~Ornn|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattleDamageAssignmentLifecycleTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

Result: **focused LayerEngine foundation guard 11/11 passed; adjacent power / layer / equipment regression 141/141 passed; backend full 4447/4447 passed; git diff --check passed**.

## 3.13 4D-04L-A Handoff Gate Accepted

A-side handoff is accepted because A verified all of the following:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04K-B is already accepted and its profile-verifier write lock is closed.
3. P1-001 remains open because `ContinuousEffectState` is currently a snapshot/report view, while `ApplyPowerModifier` still mutates `Power` and accumulates `UntilEndOfTurnPowerModifier`.
4. The next suggested B slice is narrowed to source-aware / effect-aware power modifier ledger or verifier foundation, not a broad LayerEngine rewrite.
5. Current focused LayerEngine representatives and adjacent power/layer/equipment regressions are green before any B diff.
6. Timestamp, dependency, source ordering, keyword gain/loss, multiple equipment/static aura interactions, minimum-power layering, full official coverage and READY remain open.

A-side baseline commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~PendingTaskQueueDoesNotExposeUndamagedZeroPowerFromPowerModifierAsStateBasedTask|FullyQualifiedName~TurnEndCleanupRestoresNegativeBasePowerAfterPositiveModifierExpires|FullyQualifiedName~SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn|FullyQualifiedName~NaturalBattleResponseActivationPowerModifierUsesEffectiveAssignmentDamagePool|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ContinuousEffect|FullyQualifiedName~PowerModifier|FullyQualifiedName~UntilEndOfTurnPowerModifier|FullyQualifiedName~Switcheroo|FullyQualifiedName~Ornn|FullyQualifiedName~TriggerPayment|FullyQualifiedName~BattleDamageAssignmentLifecycleTests"
```

Result: **focused LayerEngine guard 11/11 passed; adjacent power / layer / equipment regression 141/141 passed**.

## 3.14 4D-04K-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. `CardEquipmentKeywordRules` exposes a named Long Sword equipment-state representative manifest.
2. The manifest records owner/controller/attachment invariant, controller mismatch no-mutation rejection, controlled opponent-owned target attach, attached equipment follows host in both movement directions, and host destroyed detach / recall to owner base.
3. The manifest binds the existing P5 verifier/test anchors, and the new profile test reflects over `ConformanceFixtureRunnerTests` to prove those anchor methods exist.
4. Long Sword remains `recognized-deferred`, while its reason now acknowledges P5 equipment state representatives.
5. Full owner/controller breadth, full attach lifecycle breadth, Agile reaction timing, Jax-granted Agile, full Tempered official breadth, other static modifiers, copy-text effects, LayerEngine, full official coverage and READY remain open.
6. Assemble-only representatives are not downgraded; deferred Agile / Tempered / weapon rows remain visible.
7. The slice does not change runtime semantics, frontend runtime, card matrix JSON, fullOfficial or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixtureProfileBindsExistingVerifierAnchors|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

```sh
git diff --check
```

Result: **focused state / profile guard 12/12 passed; adjacent equipment regression 195/195 passed; git diff --check passed**.

## 3.15 4D-04K-A Handoff Gate Accepted

A-side handoff is accepted because A verified all of the following:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04J is already accepted as an A-side remaining-breadth refresh.
3. Existing P5 equipment state anchors are green and can be used by a future profile / verifier slice.
4. Adjacent equipment representatives remain green before any B diff.
5. The next suggested B slice is narrowed to `CardEquipmentKeywordRules.cs` / `CardCatalogBaselineTests.cs` profile-verifier alignment, not runtime implementation.
6. Full owner/controller breadth, full attach lifecycle, Agile reaction timing, Jax-granted Agile, full Tempered breadth, other static modifiers, copy-text effects, LayerEngine, card matrix full-official and READY remain open.

A-side baseline commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentRepresentativeKeepsTakeUpAttachDetachFixturesGreen|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests"
```

Result: **focused state / profile guard 11/11 passed; adjacent equipment regression 195/195 passed**.

## 3.16 4D-04J-A Handoff Gate Accepted

A-side handoff is accepted because A verified all of the following:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-04I-B is already accepted and its write lock is closed.
3. Existing equipment residuals are not a blank slate: P5 owner/controller, attached-equipment follows host and host-destroy detach / recall representatives exist.
4. The next suggested B slice is narrowed to profile / verifier alignment, not broad runtime rewrite.
5. Full owner/controller breadth, full attach lifecycle, Agile reaction timing, Jax-granted Agile, full Tempered breadth, other static modifiers, copy-text effects, LayerEngine, card matrix full-official and READY remain open.
6. The batch does not modify runtime, tests, frontend runtime, card matrix JSON or `riftbound-dotnet.sln`.

A-side baseline command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P5EquipmentStateAssembleLongSwordPreservesOwnerControllerAndAttachment|FullyQualifiedName~P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects|FullyQualifiedName~P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBattlefield|FullyQualifiedName~P5MoveUnitCommandMovesExplicitAttachedEquipmentWithHostToBase|FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed|FullyQualifiedName~P5EquipmentStateAssembleLongSwordOwnerControllerFixture|FullyQualifiedName~P5MoveUnitCommandAttachedEquipmentFollowsHostFixture|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **focused state / keyword guard 11/11 passed**.

## 3.17 4D-04I-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Accepted core commands apply a narrow Ornn friendly-equipment static recompute before returning authoritative snapshots and prompts.
2. Recompute targets only public field units whose registry behavior has `AddsFriendlyFieldEquipmentCountToSourceUnitPower`.
3. Ornn's recomputed power is registered base power + current controller friendly public field equipment count + until-end power modifier, so 4D-04H entry-time bonus does not double-count.
4. Friendly public equipment entering field after Ornn is already in field raises Ornn power from 4 to 5.
5. A counted equipment leaving field lowers Ornn power from 6 to 5.
6. Repeated accepted commands do not make Ornn drift above base + current count.
7. Hand, enemy, face-down, dirty-controller and non-equipment objects remain excluded during dynamic recompute.
8. Snapshot `power`, `basePower` and `effectivePower` remain consistent under the current snapshot model.
9. Existing Ornn entry-time tests, equipment keyword profile guards, Tempered, Jax, Akshan, Armed Assaulter and continuous-effect snapshot representatives remain green.
10. The slice does not update frontend runtime, card matrix JSON, P1-001 / P1-002 status, full-official status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **focused / keyword / LayerEngine-view guard 9/9 passed; adjacent equipment / payment regression 117/117 passed; backend full 4446/4446 passed; git diff --check passed**.

## 3.18 4D-04H-A Acceptance Gate Accepted

A-side implementation is accepted because A verified all of the following:

1. `SFD·085/221` and `SFD·085a/221` register the Ornn friendly-equipment static power representative boundary.
2. Hand-play entry resolution counts only controller friendly public field equipment in base / battlefield zones.
3. Hand, face-down, enemy, dirty-controller and non-equipment objects do not contribute to the bonus.
4. Ornn enters with base power 4 plus the friendly equipment count; with two legal friendly public field equipment objects he enters at power 6.
5. `UNIT_PLAYED` payload includes `friendlyEquipmentPowerBonus` only when the bonus is positive.
6. The keyword profile marks the representative boundary while keeping full `百炼`, full static recompute, LayerEngine, owner/controller breadth, attach lifecycle breadth and READY residuals open.
7. Existing Tempered, Jax, Akshan, Armed Assaulter, attachment profile and continuous-effect snapshot representatives remain green.
8. The slice does not update frontend runtime, card matrix JSON, full-official status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrnnFriendlyEquipmentStaticPowerTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~MatchStateExposesContinuousEffectPowerLayerViews"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **focused / keyword guard 5/5 passed; adjacent equipment / payment regression 114/114 passed; backend full 4443/4443 passed; git diff --check passed**.

## 4. 4D-04G-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata exposes both `HASTE_READY` and legal `TEMPERED_ATTACH:<equipmentObjectId>` for `SFD·002/221` when resources and legal Spinning Axe are present.
2. Legal both-cost command pays base 6 mana plus 1 haste mana and 1 red power, records both optional costs on `COST_PAID` and the stack item, and keeps target arrays empty.
3. Stack resolution plays Armed Assaulter to P1 base active and attaches selected Spinning Axe to Armed Assaulter if it remains legal.
4. Legal HASTE-only existing fixture remains green; legal Tempered-only path attaches but does not mark haste ready.
5. Duplicate/conflicting optional costs, invalid equipment choices, insufficient mana, insufficient red, wrong trait and malformed optional costs reject no-mutation.
6. Stale selected equipment before resolution makes only the attach side effect no-op; HASTE_READY remains applied if paid.
7. Existing Tempered, Jax trigger-payment, Akshan, Agile direct attach, Assemble, Take Up, Azir reattach, HASTE_READY and PaymentEngine representative tests remain green.
8. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ArmedAssaulterHasteTemperedTests|FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForArmedAssaulter|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~Akshan|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **focused / keyword guard 26/26 passed; adjacent equipment / payment regression 235/235 passed; backend full 4440/4440 passed; git diff --check passed**.

## 5. 4D-04F-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata exposes an Akshan optional cost choice for legal enemy field equipment only, with typed orange power affordability and legal payment-resource choices reflected by the server.
2. Legal command pays base mana plus 2 orange power, records `powerByTrait.orange = 2`, preserves the selected optional cost on the stack, and keeps no-extra Akshan unchanged.
3. Stack resolution rechecks selected equipment. If legal, Akshan enters P1 base, selected enemy equipment moves to P1 base, controller changes to P1, owner remains unchanged, and previous attachment clears.
4. Weapon equipment becomes attached to Akshan; non-weapon equipment is only moved/controlled.
5. Resolution emits auditable control/move and attachment events with source/equipment/controller/owner/reason/optional cost payload.
6. Stale selected equipment before resolution makes only the equipment side effect no-op; Akshan still enters base and no false success event is emitted.
7. Akshan leaving the field returns the controlled equipment to owner base, restores owner control, clears attachment, and emits a return event; end turn alone must not return it.
8. Invalid choices, insufficient orange, wrong trait, malformed or conflicting optional costs reject no-mutation.
9. Existing Tempered, Jax trigger-payment, Agile direct attach, Assemble, Take Up, Azir reattach and PaymentEngine representative tests remain green.
10. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **focused / keyword guard 28/28 passed; adjacent equipment / payment regression 209/209 passed; backend full 4417/4417 passed; git diff --check passed**.

## 6. 4D-04E-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata exposes `TEMPERED_ATTACH:<equipmentObjectId>` for Jax `SFD·119/221` and `SFD·119a/221` only when legal controlled `SFD·186/221` exists.
2. Missing object, enemy object, non-equipment object, wrong-card object, hand / deck / graveyard object, face-down object, stale object or wrong-controller object is rejected no-mutation.
3. Legal command preserves existing `PLAY_CARD` no-target payment / stack behavior and records the optional cost on the stack item.
4. Stack resolution rechecks the selected armament; if still legal, the Jax unit enters base and the selected `SFD·186/221` gets `AttachedToObjectId=sourceObjectId`.
5. Resolution emits `EQUIPMENT_ATTACHED` with auditable `TEMPERED_OPTIONAL_ATTACH` payload and opens exactly one `TRIGGER_PAYMENT` pending payment for `JAX_WEAPON_ATTACH_PAY_1_DRAW_1`.
6. Pay 1 draws 1 and closes the window; decline closes with no draw; insufficient payment rejects and keeps the window without drawing.
7. If the selected equipment becomes stale before resolution, Jax still enters base but attach and payment window are skipped.
8. Existing `TemperedEquipmentOptionalAttachTests`, `TriggerPaymentTests` Jax assemble path, Agile direct attach, AssembleEquipment, Take Up and Azir reattach representative tests remain green.
9. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxTemperedOptionalAttach|FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **focused / keyword guard 41/41 passed; adjacent equipment / payment regression 243/243 passed; backend full 4397/4397 passed; git diff --check passed**.

## 7. 4D-04D-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata exposes a `TEMPERED_ATTACH:<equipmentObjectId>` optional cost choice for `SFD·008/221` only when a legal controlled `SFD·186/221` is available.
2. Missing object, enemy object, non-equipment object, hand/deck/graveyard object, face-down object, stale object, wrong-controller object or unsupported equipment card is rejected no-mutation.
3. Legal command preserves existing `PLAY_CARD` no-target payment / stack behavior and records the optional cost on the stack item.
4. Stack resolution rechecks the selected armament; if still legal, the new `SFD·008/221` unit enters base and the selected `SFD·186/221` gets `AttachedToObjectId=sourceObjectId`.
5. Resolution emits `EQUIPMENT_ATTACHED` with auditable Tempered optional attach payload and reason `TEMPERED_OPTIONAL_ATTACH`.
6. The no-optional `SFD·008/221` path remains green and does not attach equipment.
7. Existing assemble, Take Up, Agile direct attach, Azir reattach, Jax weapon attach and equipment cleanup representative tests remain green.
8. Keyword profile/report language says `百炼` has one optional attach representative while full printed tempered breadth, dynamic colored costs, owner/controller changes, static modifiers, attach lifecycle, LayerEngine and full official breadth remain deferred. As of 4D-04D, Jax trigger integration was still deferred; 4D-04E later closed only the narrow Jax + Spinning Axe trigger-payment representative.
9. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TemperedEquipmentOptionalAttachTests|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies"
```

Result: **14/14 passed**.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach"
```

Result: **139/139 passed**.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Result: **backend full 4380/4380 passed; git diff --check passed**.

## 8. 4D-04C-B Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata exposes legal `PLAY_CARD` friendly-unit targets for printed Agile equipment from hand.
2. Missing target, enemy unit, non-unit, stale object and wrong-controller target are rejected no-mutation.
3. Legal command preserves existing payment / stack behavior and resolves by setting `AttachedToObjectId` on the equipment object to the selected unit.
4. Resolution emits `EQUIPMENT_ATTACHED` with auditable Agile direct-play attach payload.
5. Existing assemble, Take Up, Azir reattach, Maduli, Ezreal, equipment cleanup, P79 and Arena Service Crew representative tests remain green after fixture migration.
6. Keyword profile/report language says Agile has a direct-play representative while reaction timing, Jax-granted Agile, ephemeral/static breadth, Tempered optional attachment, weapon/static modifiers, copy-text effects, owner/controller changes, full attach lifecycle and full official breadth remain deferred.
7. The slice does not update frontend runtime, card matrix JSON, P1-002 status or READY.

A-side accepted commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AgileEquipmentDirectPlayAttachTests|FullyQualifiedName~LongSword|FullyQualifiedName~Steraks|FullyQualifiedName~ClothArmor|FullyQualifiedName~SpinningAxe|FullyQualifiedName~P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4RejectedFixtures|FullyQualifiedName~ConformanceFixtureShapeTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment|FullyQualifiedName~TakeUp|FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Maduli|FullyQualifiedName~Ezreal|FullyQualifiedName~SeaMonsterHook|FullyQualifiedName~SfurSong|FullyQualifiedName~P6EquipmentSeedBroadcastsPlayAndAssembleInDevelopment"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~KeywordCoverageReportExposesDeferredKeywordFamilies|FullyQualifiedName~P6EquipmentKeywordFamiliesReportSpecAndExecutionBoundaryCoverage|FullyQualifiedName~P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags|FullyQualifiedName~P4EquipmentAttachmentProfileMapsTakeUpToRepresentativeAttachDetach|FullyQualifiedName~AgileEquipmentDirectPlayAttachTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 9. 4D-03AS-B Historical Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server prompt metadata no longer claims Azir `armamentReattachPolicy="deferred"` after implementation; metadata exposes target-scoped armament reattach choices or an equivalent server-owned shape.
2. The command path allows no reattach even when the selected target has attached armament.
3. The command path rejects invalid reattach choices no-mutation: missing object, non-equipment object, unattached equipment, equipment attached to a different unit, multiple selected armaments, opponent-controlled illegal object, or reattach selection without a legal selected target.
4. Stack resolution rechecks the selected armament. If still legal and attached to the selected target, it sets `AttachedToObjectId` to Azir and emits existing-shape equipment reattach evidence such as `EQUIPMENT_REATTACHED` with previous / new attachment payload.
5. If the selected armament becomes stale before resolution, the existing source / target legality still governs position swap; reattach itself becomes no-effect and does not emit a false attach event.
6. `UNIT_LOCATIONS_SWAPPED` or a companion event carries auditable payload for selected armament id, `armamentReattachApplied`, and a non-deferred policy marker.
7. 4D-03AM Azir payment, once-per-turn, target validation, stale target no-effect, rune recycle and no-mutation tests remain green.
8. The slice does not update frontend runtime, card matrix JSON, Azir full-official status, P0/P1 status or READY.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 10. 4D-03AR-B Historical Acceptance Gate Accepted

B implementation is accepted because A verified all of the following:

1. Server runtime has an explicit `UNL-144/219` cannot-ready policy or helper, preferably reused by every ready path touched in this representative slice.
2. Crimson Rose ready-unit prompt does not offer exhausted Maduli as a legal ready target.
3. A hand-written or stale Crimson Rose ready-unit target cannot make Maduli active; resolution must leave `IsExhausted=true` and emit no Maduli `UNIT_READIED`.
4. Hunt mass friendly ready, or an equivalent mass ready representative, readies other legal friendly units while skipping exhausted Maduli.
5. 4D-03AN Maduli purple move prompt / command / typed payment / movement / stale no-effect tests remain green.
6. Prompt metadata no longer claims Maduli cannot-ready static is `deferred` after implementation.
7. The slice does not update frontend runtime, card matrix JSON, Maduli full-official status, P0/P1 status or READY.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 11. 4D-03AQ-B Historical Acceptance Gate

B test-only verifier is acceptable only if A can verify all of the following:

1. `PaymentEngineCoverageAuditTests` binds implemented HASTE_READY registry/profile entries to typed trait metadata and existing P4 fixture anchors.
2. The verifier fails on missing trait, missing fixture, duplicate manifest entry or closure text that claims READY / full official.
3. Closure status explicitly says `NOT READY` and `P0-005 remains open`.
4. No runtime, frontend, card matrix or `riftbound-dotnet.sln` edits occur.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~HasteOptional|FullyQualifiedName~HasteReady|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 12. 4D-03AP-B Historical Acceptance Gate

B implementation / test guard is acceptable only if A can verify all of the following:

1. Both `SFD·029/221` and `SFD·029a/221` profiles expose `HASTE_READY` as extra 1 mana + 1 red typed power.
2. Server prompt exposes `PLAY_CARD`, `HASTE_READY`, base / total cost metadata and red payment-resource choices only when legal.
3. Command with existing red power succeeds and emits `COST_PAID` with `baseManaCost=3`, `totalManaCost=4`, `genericPower=0`, `totalPowerCost=1`, `powerByTrait.red=1`.
4. Command with necessary `RECYCLE_RUNE:<redRuneObjectId>` succeeds, recycles the rune and records payment-resource audit payload.
5. Wrong trait, generic temporary resource, insufficient red, duplicate / invalid / unnecessary recycle and unsupported optional cost reject no-mutation.
6. Command cannot bypass no-target route by submitting target object ids.
7. Strong / Overwhelm battle modifier, damage overflow, non-hand haste granting, LayerEngine, FAQ, card matrix full-official and READY remain residual unless separately dispatched.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ReksaiHasteReady|FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reksai|FullyQualifiedName~HasteOptional|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PlayCard|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 13. 4D-03AO-B Historical Acceptance Gate

B implementation is acceptable only if A can verify all of the following:

1. `P4ActivatedAbilityCatalog` exposes executable definitions or aliases for `SFD·082/221`, `SFD·082a/221` and `SFD·082b/221·P`.
2. Ability metadata uses blue typed power cost 1, zero mana cost, no target, no exhaust cost and `swift=true` / reaction-speed marker.
3. Server prompt exposes `ACTIVATE_ABILITY` only when legal, with source requirement, blue typed cost, no-target policy, destination base / self-movement metadata and stack-before-move policy.
4. Payment goes through shared PaymentEngine / `PaymentCostRules` with blue power spend and necessary `RECYCLE_RUNE:<objectId>` support.
5. Wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, insufficient blue and unsupported optional cost are rejected no-mutation.
6. Source validation rejects base, hand, deck, graveyard, face-down, enemy-controlled, stale, wrong-card and dirty-source Ezreal attempts; accepted sources must be controlled public Ezreal units in precise battlefield locations.
7. Command rejects submitted targets, battlefield destinations and arbitrary destination overrides because the skill is no-target self movement.
8. Resolution is server-authoritative and moves Ezreal to the activating player's base, updating `ObjectLocations`, snapshot and `UNIT_MOVED_TO_BASE` event payload with a distinguishable movement permission.
9. Stale source / no-longer-controlled / no-longer-battlefield / already-base source at resolution becomes no-effect without frontend inference.
10. Attack / defense damage trigger, cannot-combat-damage static and full swift / reaction timing are either implemented with tests or explicitly recorded as residual risk; success fixtures must not claim full-official Ezreal if these branches remain unimplemented.

Suggested post-implementation commands:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 14. C / E Preflight Boundaries

C may prepare a final validation checklist, but must not turn historical frontend evidence into final READY evidence. Final frontend validation still requires fresh runs in the final code state:

```sh
cd src/Riftbound.DevUi
source ../../scripts/dev-env.sh && npm run build
source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api
```

E may identify matrix rows and official text blockers for Azir / Ezreal, but must not update `fullOfficial` status until A accepts runtime, rules evidence, tests, residual handling and FAQ review.

## 15. Current Batch Stop Point

This record stops after accepting 4D-04P-B LayerEngine minimum-power ordering representative and closing the B runtime / focused-test write lock. The project remains **NOT READY**. No frontend, matrix, runtime or test write window remains open, and `riftbound-dotnet.sln` remains untouched.
