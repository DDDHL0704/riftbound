# Stage 4D Next Dispatch and Writelocks

日期：2026-05-16
结论：**4D-03CC HONEYFRUIT RESOURCE SKILL HANDOFF / PROJECT NOT READY**

本文件是 A 主控对下一批 B/C/D/E 工作的调度队列与写锁边界。它只做 planning / handoff / acceptance / baseline 归档；除本文明确记录的 focused verifier 外，不实现 runtime，不修改前端，不升级 full-official。当前 active goal 仍未完成，不得调用 `update_goal complete`。

## 1. 输入事实

- 当前分支为 `main`，仓库当前只保留未跟踪 `riftbound-dotnet.sln`；该文件不得被本批任务触碰或纳入提交。
- 4D-03CC PaymentEngine Honeyfruit equipment-reaction resource skill handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03CC_PAYMENT_ENGINE_HONEYFRUIT_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03CC_PAYMENT_ENGINE_HONEYFRUIT_RESOURCE_SKILL_BASELINE_EVIDENCE.md`。本批只做 A-side docs，把 4D-03CA 四条 non-legend lane 中的 Honeyfruit `UNL-049/219` equipment reaction / level-six generated mana / power lane 单独收窄为 future B-side implementation / verifier boundary；Jhin、Blue Sentinel、Lux 与 9 个 `LEGEND_ACT` bridge candidates 不进入本切片。A 侧基线验证 focused PaymentEngine coverage guard 127/127、adjacent PaymentEngine / resource skill / prompt / hub regression 685/685、backend full 4564/4564、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；未派发 B，项目仍 **NOT READY**。
- 4D-03CB PaymentEngine Jhin movement-triggered resource skill handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03CB_PAYMENT_ENGINE_JHIN_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03CB_PAYMENT_ENGINE_JHIN_RESOURCE_SKILL_BASELINE_EVIDENCE.md`。本批只做 A-side docs，把 4D-03CA 四条 non-legend lane 中的 Jhin `UNL-022/219` movement-triggered generated mana / power lane 单独收窄为 future B-side implementation / verifier boundary；Honeyfruit、Blue Sentinel、Lux 与 9 个 `LEGEND_ACT` bridge candidates 不进入本切片。A 侧基线验证 focused PaymentEngine coverage guard 127/127、adjacent PaymentEngine / resource skill / prompt / hub regression 685/685、backend full 4564/4564、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；未派发 B，项目仍 **NOT READY**。
- 4D-03CA PaymentEngine non-legend deferred resource skill runtime lanes gate 已完成并验收，入口为 `docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03CA_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_LANES_EVIDENCE.md`。本批只改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 A-side docs，把 4D-03BZ 的 `B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME` 拆成 executable `PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifest` 四条 future B-side acceptance lanes：Jhin movement-triggered resource skill、Honeyfruit equipment reaction / level-six branch、Blue Sentinel delayed next-main generated power branch 与 Lux spell-only tap reaction resource skill。A 侧验证 focused PaymentEngine coverage guard 127/127、adjacent PaymentEngine / resource skill / prompt / hub regression 685/685、backend full 4564/4564、`git diff --check` 通过；runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；未派发 B，项目仍 **NOT READY**。
- 4D-03BZ PaymentEngine deferred resource skill next-dispatch gate 已完成并验收，入口为 `docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BZ_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_NEXT_DISPATCH_GATE_EVIDENCE.md`。本批只改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 A-side docs，把 4D-03BX / 4D-03BY 后续工作固定为 executable `PaymentEngineDeferredResourceSkillNextDispatchGateManifest`：4 个 non-legend deferred resource-skill runtime / verifier candidates 与 9 个 existing `LEGEND_ACT` resource-action bridge / verifier candidates 必须走两条独立 fresh B-side dispatch gate。A 侧验证 focused PaymentEngine coverage guard 123/123、adjacent PaymentEngine / resource skill / prompt / hub regression 681/681、backend full 4560/4560、`git diff --check` 通过；runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BY PaymentEngine legend resource action bridge handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BY_PAYMENT_ENGINE_LEGEND_RESOURCE_ACTION_BRIDGE_BASELINE_EVIDENCE.md`。本批只做 A-side docs，把 4D-03BW / 4D-03BX 后的 9 个 existing `LEGEND_ACT` resource-action bridge candidates（Diana / Ornn / KaiSa / Darius 及 reprints / premium variants）收窄为 future B-side bridge / verifier boundary；4 个 non-legend 03BX runtime candidates 不进入该切片。A 侧基线验证 focused PaymentEngine coverage guard 119/119、adjacent PaymentEngine / resource skill / prompt / hub regression 677/677、backend full 4556/4556、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BX PaymentEngine non-legend deferred resource skill runtime handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BX_PAYMENT_ENGINE_NON_LEGEND_RESOURCE_SKILL_RUNTIME_BASELINE_EVIDENCE.md`。本批只做 A-side docs，把 4D-03BW 的 4 个 non-legend runtime / verifier candidates（Jhin `UNL-022/219`、Honeyfruit `UNL-049/219`、Blue Sentinel `UNL-087/219`、Lux `OGS·014/024`）收窄为下一枚 possible B-side implementation / verifier boundary；9 个 existing `LEGEND_ACT` bridge candidates 不进入该切片，仍需单独 fresh A dispatch。A 侧基线验证 focused PaymentEngine coverage guard 119/119、adjacent PaymentEngine / resource skill / prompt / hub regression 677/677、backend full 4556/4556、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BW PaymentEngine deferred resource skill family verifier 已完成并验收，入口为 `docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BW_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_VERIFIER_EVIDENCE.md`。本批只改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 A-side docs，把 4D-03BU / 4D-03BV 的 13 个 deferred official resource-skill candidates 转成 executable `DeferredResourceSkillFamilyManifest`：9 个 existing `LEGEND_ACT` resource-action bridge candidates 与 4 个 non-legend runtime / verifier candidates；现有 legend representative evidence 不能代理 `RESOURCE_SKILLS` closure。A 侧验证 focused PaymentEngine coverage guard 119/119、adjacent PaymentEngine / resource skill / prompt / hub regression 677/677、backend full 4556/4556、`git diff --check` 通过；runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BV PaymentEngine deferred resource skill family handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BV_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BV_PAYMENT_ENGINE_DEFERRED_RESOURCE_SKILL_FAMILY_BASELINE_EVIDENCE.md`。本批只做 A-side docs，把 4D-03BU 的 13 个 deferred official resource-skill candidates 拆成 9 个 existing `LEGEND_ACT` resource actions 与 4 个 non-legend unit / equipment / delayed resource skills；现有 Darius / Diana / KaiSa / Ornn `LEGEND_ACT` tests 不能代理 `RESOURCE_SKILLS` closure，Jhin / Honeyfruit / Blue Sentinel / Lux 仍需 future verifier / runtime breadth。A 侧基线验证 focused PaymentEngine coverage guard 115/115、adjacent PaymentEngine / resource skill / prompt / hub regression 673/673、backend full 4552/4552、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BU PaymentEngine resource skill official breadth verifier 已完成并验收，入口为 `docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_EVIDENCE.md`。本批只改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 A-side docs，把 fixed official catalog 中带 resource-skill reminder text 的 32 个 snapshot entries 转成 executable `ResourceSkillOfficialBreadthManifest`，并区分 19 个 current implemented `P4ActivatedAbilityCatalog.IsResourceSkill=true` source card nos 与 13 个 deferred official candidates。A 侧验证 focused 115/115、adjacent PaymentEngine / resource skill / prompt / hub regression 673/673、backend full 4552/4552、`git diff --check` 通过；runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BU PaymentEngine resource skill official breadth handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BU_PAYMENT_ENGINE_RESOURCE_SKILL_OFFICIAL_BREADTH_BASELINE_EVIDENCE.md`。A 侧确认 4D-03BT 后下一枚 concrete future B-side scope 是完整 `[A]` / `[C]` resource skill official breadth verifier / implementation slice；当前 `ResourceSkillCoverageManifest` 仍是 6 个 family entries / 19 个 current `IsResourceSkill=true` ability ids，`ResourceSkillAllWindowMatrixManifest` 仍是 36 行 representative matrix，`RESOURCE_SKILL_A_C_FAMILY` 仍为 `catalog-bound-representative`，`RESOURCE_SKILLS` 仍为 `remaining-official-gap`。A 侧 baseline 验证 focused 110/110、adjacent PaymentEngine / resource skill / prompt / hub regression 668/668、backend full 4547/4547、`git diff --check` 通过；runtime、tests、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BT PaymentEngine remaining official closure gate verifier 已完成并验收，入口为 `docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BT_PAYMENT_ENGINE_REMAINING_OFFICIAL_CLOSURE_GATE_EVIDENCE.md`。本批只改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 A-side docs，把 4D-03BS 的 B/E/D fresh-dispatch gates 固定为 executable `RemainingOfficialClosureGateManifest`，并读取 card matrix skeleton 确认 1009 / 811、0 full-official、freeze ready=false。A 侧验证 focused 110/110、adjacent PaymentEngine / resource skill / prompt / hub regression 668/668、backend full 4547/4547 通过；runtime、frontend、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BS PaymentEngine remaining official scope handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BS_PAYMENT_ENGINE_REMAINING_OFFICIAL_SCOPE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BS_PAYMENT_ENGINE_REMAINING_OFFICIAL_SCOPE_BASELINE_EVIDENCE.md`。A 侧确认 4D-03BR-B 之后仍不能关闭 P0-005：未来必须由 A 另开 B-side PaymentEngine official breadth verifier / implementation slice、E-side card matrix readiness slice 或 D-side P0 audit slice；本批不派发 worker，不打开 runtime / test / frontend / matrix 写锁。A 侧 baseline 验证 focused PaymentEngine coverage guard 107/107、adjacent PaymentEngine / resource skill / prompt / hub regression 665/665、backend full 4544/4544 通过；runtime、tests、frontend、browser scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-FE formal 18-step fresh-run 已完成并验收，入口为 `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_FE_FORMAL_18_FRESH_RUN_EVIDENCE.md`。A 侧执行 `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run e2e:formal-18 -- --start-api`，脚本启动 API `http://127.0.0.1:5088`、Vite preview `http://127.0.0.1:5174/`、P1 Chrome DevTools `127.0.0.1:9340`、P2 Chrome DevTools `127.0.0.1:9341`；房间 `formal-18-1778886172096-1`，1/18 到 18/18 全部 OK，最终 `Formal 18-step E2E passed: formal-18-1778886172096-1`。本批不改 runtime / frontend source / E2E scripts / card matrix JSON / fullOfficial / READY；P0/P1、full-card matrix、READY 与 `riftbound-dotnet.sln` 仍锁定 / 未关闭。
- 4D-FE Chrome smoke fresh-run 已完成并验收，入口为 `docs/CURRENT_STAGE4D_FE_CHROME_SMOKE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_FE_CHROME_SMOKE_EVIDENCE.md`。A 侧执行 `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api`，脚本启动 API `http://127.0.0.1:5088`、Vite preview `http://127.0.0.1:5173/` 与 Chrome DevTools `127.0.0.1:9338`，并报告 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result` 全部 `Chrome smoke OK`，最终 `Chrome smoke passed.`。本批不改 runtime / frontend source / smoke scripts / formal 18-step scripts / card matrix JSON / fullOfficial / READY；后续 formal 18 fresh-run 已在 4D-FE-FORMAL18 通过，P0/P1、full-card matrix、READY 与 `riftbound-dotnet.sln` 仍锁定 / 未关闭。
- 4D-FE event-label build gate 已完成并验收，入口为 `docs/CURRENT_STAGE4D_FE_EVENT_LABEL_BUILD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_FE_EVENT_LABEL_BUILD_EVIDENCE.md`。A 侧首次 fresh-run `source ../../scripts/dev-env.sh && npm run build` 在 `check:event-labels` 发现 12 个后端事件 kind 缺少中文标题；本批只修改 `src/Riftbound.DevUi/src/components/match/EventLog.tsx` 的 label map，不改服务端规则 / 协议 / prompt 提交逻辑。复跑 build 通过：132 backend event kinds covered、user-facing fallback text check passed、`tsc -b` passed、Vite build passed（仅既有 SignalR/Rollup PURE 注释 warning）。后续 Chrome smoke / formal 18 fresh-runs 已通过，P0/P1、full-card matrix、fullOfficial / READY 与 `riftbound-dotnet.sln` 仍锁定 / 未关闭。
- 4D-03BR-B PaymentEngine target / tax activated ability matrix verifier 已由 A 完成并验收，入口为 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BR-B audit / evidence docs，把 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 target-bearing / typed / experience / Spellshield-tax activated ability entries 与 6 个 target/payment dimensions 扩成 48 行 source-target-payment-audit-rollback matrix，并要求每行回连 `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual 与 `TARGET_TAXES` official residual axis。A 侧验证 focused 107/107、adjacent 665/665、backend full 4544/4544、`git diff --check` passed；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BR PaymentEngine target / tax activated ability matrix handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_BASELINE_EVIDENCE.md`。本批只做 A-side docs，未派发 B 实现，未打开 test/runtime 写锁；该 handoff 已被 4D-03BR-B verifier supersede；其原定范围是把 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 target-bearing / typed / experience / Spellshield-tax activated ability representatives 与 6 个 target/payment dimensions 扩成 48 行 source-target-payment-audit-rollback matrix，并继续回连 `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual 与 `TARGET_TAXES` official residual axis。A 侧 baseline 验证 focused 102/102、adjacent PaymentEngine / resource skill / prompt / hub regression 660/660、backend full 4539/4539、`git diff --check` 通过；runtime、tests、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BQ-B PaymentEngine resource skill all-window matrix verifier 已由 A 完成并验收，入口为 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BQ-B audit / evidence docs，把 4D-03AZ / `ResourceSkillCoverageManifest` 的 6 个 resource skill family entries 与 6 个 current PaymentEngine payment surfaces 扩成 36 行 all-window prompt-command-audit-generated-resource-rollback matrix，并要求每行回连 `RESOURCE_SKILL_A_C_FAMILY` residual 与 `RESOURCE_SKILLS` official residual axis。A 侧验证 focused 102/102、adjacent 660/660、backend full 4539/4539、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BQ PaymentEngine resource skill all-window matrix handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_BASELINE_EVIDENCE.md`。本批只做 A-side docs，未派发 B 实现，未打开 test/runtime 写锁；future 4D-03BQ-B 应把 4D-03AZ / `ResourceSkillCoverageManifest` 的 6 个 resource skill family entries、19 个 current catalog `IsResourceSkill=true` ability ids 与 6 个 current PaymentEngine payment surfaces 扩成 36 行 family-window prompt-command-audit-generated-resource-rollback matrix，并继续回连 `RESOURCE_SKILL_A_C_FAMILY` residual 与 `RESOURCE_SKILLS` official residual axis。A 侧 baseline 验证 focused 97/97、adjacent PaymentEngine / resource skill / prompt / hub regression 655/655、backend full 4534/4534、`git diff --check` 通过；runtime、tests、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BP-B PaymentEngine keyword branch all-window matrix verifier 已由 A 完成并验收，入口为 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BP-B audit / evidence docs，把 4D-03AY 的 8 个 keyword payment branch entries 与 6 个 current PaymentEngine payment surfaces 扩成 48 行 all-window quote-command-audit-rollback matrix，并要求每行回连 `KEYWORD_PAYMENT_BRANCHES` residual 与 `KEYWORD_BRANCHES` / `COST_MODIFIERS` / `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` / `REPLACEMENT_PREVENTION` official residual axes。A 侧验证 focused 97/97、adjacent 655/655、backend full 4534/4534、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BP PaymentEngine keyword branch all-window matrix handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_BASELINE_EVIDENCE.md`。本批只做 A-side docs，未派发 B 实现，未打开 test/runtime 写锁；future 4D-03BP-B 应把 4D-03AY 的 8 个 keyword payment branch entries 与 6 个 current PaymentEngine payment surfaces 扩成 48 行 all-window quote-command-audit-rollback matrix，并继续回连 `KEYWORD_PAYMENT_BRANCHES` residual 与 `KEYWORD_BRANCHES` / `COST_MODIFIERS` / `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` / `REPLACEMENT_PREVENTION` official residual axes。A 侧 baseline 验证 focused 92/92、adjacent 650/650、backend full 4529/4529、`git diff --check` 通过；runtime、tests、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BO-B PaymentEngine official matrix downstream aggregate verifier 已由 B-Implementation / Ramanujan `019e2d82-4c8d-7390-aa92-7636f8a15179` 完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BO-B audit / evidence docs，把 4D-03BC official row schema 的 9 representative seeds、3 missing official rows、1 MOVE_UNIT policy-deferred row 与 4D-03BL-B rollback 7/42、4D-03BM cross-window 7/42、4D-03BN card matrix 8/48 聚合成 executable audit contract，并要求每个 all-window row 回连 matching official missing row id。A 侧验证 focused 92/92、adjacent 650/650、backend full 4529/4529、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BO PaymentEngine official matrix downstream aggregate handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_BASELINE_EVIDENCE.md`。本批只做 A-side docs，未派发 B 实现，未打开 test/runtime 写锁；future 4D-03BO-B 应把 4D-03BC 的 official row schema、9 representative seeds、3 missing official rows、1 MOVE_UNIT policy-deferred row，以及 4D-03BL-B / 4D-03BM / 4D-03BN 三条 downstream all-window matrix 聚合成一个 executable audit contract。A 侧 baseline 验证 focused 85/85、adjacent 643/643、backend full 4522/4522、`git diff --check` 通过；runtime、tests、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰；项目仍 **NOT READY**。
- 4D-03BN PaymentEngine card matrix alignment all-window matrix verifier 已由 B-Implementation / Laplace `019e2d66-d12a-7233-81de-bbd0abb0dcfd` 完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BN audit / evidence docs，把 4D-03BG 的 8 个 card matrix alignment representative families 扩成 6 个 PaymentEngine payment surfaces x 8 families 的 48 行 all-window matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT` 的 matrix scope / representative surface / prompt evidence / command evidence / audit evidence / matrix sync-status / frontend-snapshot or rule-source trace / doc-anchor contract。A 侧验证 focused 85/85、adjacent 643/643、backend full 4522/4522、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BN focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BM PaymentEngine cross-window generation / consumption all-window matrix verifier 已由 B-Implementation / Feynman `019e2d5a-e982-7833-b309-03f89a55ee45` 完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BM audit / evidence docs，把 4D-03BF 的 7 个 cross-window generation / consumption representative families 扩成 6 个 PaymentEngine payment surfaces x 7 families 的 42 行 all-window matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT` 的 generation scope / consumption scope / resource-lifetime / prompt quote / command commit-or-rejection / audit / lifetime-no-mutation-restriction / doc-anchor contract。A 侧验证 focused 80/80、adjacent 638/638、backend full 4517/4517、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BM focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BL-B PaymentEngine rollback failure official matrix verifier 已由 B-Implementation / Beauvoir `019e2d4a-1164-7771-bf0a-bb68e5f62b84` 完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 4D-03BL audit / evidence docs，把 4D-03BE 的 7 个 rollback failure representative families 扩成 6 个 PaymentEngine payment surfaces x 7 families 的 42 行 all-window matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT` 的 prompt quote / command rejection / no-mutation / audit / doc-anchor contract。A 侧验证 focused 75/75、adjacent 633/633、backend full 4512/4512、`git diff --check` 通过；runtime、frontend、browser smoke scripts、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BL-B focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BL-B PaymentEngine rollback failure official matrix dispatch boundary 已建立并已被上方 accepted verifier supersede。该 dispatch boundary 继承 4D-03BL baseline focused 70/70、adjacent 628/628、backend full 4507/4507；项目仍 **NOT READY**。
- 4D-03BL PaymentEngine rollback failure official matrix handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_BASELINE_EVIDENCE.md`。该 handoff 只修改 A-side handoff / baseline docs，把 4D-03BE 的 7 个 rollback failure representative families 收敛为 future 4D-03BL-B all-window executable rollback matrix，要求后续覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT` 等当前 PaymentEngine payment surfaces 的 prompt quote / command rejection / no-mutation / audit parity。A 侧 baseline focused 70/70、adjacent PaymentEngine / resource skill / prompt / hub regression 628/628、backend full 4507/4507 通过；runtime、tests、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BL 本身是 historical handoff-only；后续已被 4D-03BL-B / 4D-03BM / 4D-03BN / 4D-03BO-B / 4D-03BP-B / 4D-03BQ-B accepted verifiers supersede；项目仍 **NOT READY**。
- 4D-03BK PaymentEngine policy-deferred MOVE_UNIT boundary verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，要求 4D-03BC 的唯一 `policy-deferred-row` 精确等于 `ROW_MOVE_UNIT_POLICY_DEFERRED`，且该 row 不进入 representative seed、missing official row 或 downstream PaymentEngine payment manifests。A 侧验证 focused 70/70、adjacent PaymentEngine / resource skill / prompt / hub regression 628/628、backend full 4507/4507 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BK focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BJ PaymentEngine representative seed upstream coverage verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BJ_PAYMENT_ENGINE_REPRESENTATIVE_SEED_UPSTREAM_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BJ_PAYMENT_ENGINE_REPRESENTATIVE_SEED_UPSTREAM_COVERAGE_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，要求 4D-03BC 的 9 个 `representative-seed` rows 精确回连上游 audit manifest，且 seed rows 不混入 4D-03BH missing-row downstream aggregate doc 或 `Missing official row` 口径。A 侧验证 focused 67/67、adjacent PaymentEngine / resource skill / prompt / hub regression 625/625、backend full 4504/4504 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BJ focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BI Active goal prompt-to-artifact checklist refresh 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BI_ACTIVE_GOAL_CHECKLIST_REFRESH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BI_ACTIVE_GOAL_CHECKLIST_REFRESH_EVIDENCE.md`。本批只修改 docs，把 `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 从旧 4D-04G 状态对齐到当前 4D-03BH 事实，记录 latest HEAD `a07197c6`、backend full 4501/4501、matrix 1009/811 且 0 full-official，并保留 frontend build / Chrome smoke / formal 18-step historical-pass-only 口径。runtime、tests、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BI docs write lock closed；项目仍 **NOT READY**。
- 4D-03BH PaymentEngine missing row downstream coverage verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，要求 4D-03BC 的全部 `missing-official-row` 均有 downstream representative manifest 覆盖，并确认 `ROW_MOVE_UNIT_POLICY_DEFERRED` 不进入 PaymentEngine payment row。A 侧验证 focused 64/64、adjacent PaymentEngine / resource skill / prompt / hub regression 622/622、backend full 4501/4501 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BH focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BG PaymentEngine card matrix alignment row manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 `ROW_CARD_MATRIX_ALIGNMENT_MISSING` 拆成 8 个 representative card matrix families，并保留 prompt / command / audit / matrix / doc anchors / NOT READY closure。A 侧验证 focused 61/61、adjacent PaymentEngine / resource skill / prompt / hub regression 619/619、backend full 4498/4498 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BG focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BF PaymentEngine cross-window generation / consumption row manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING` 拆成 7 个 representative cross-window families，并保留 prompt / command / audit / lifetime / no-mutation / doc anchors / NOT READY closure。A 侧验证 focused 56/56、adjacent PaymentEngine / resource skill / prompt / hub regression 614/614、backend full 4493/4493 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BF focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BE PaymentEngine rollback failure row manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` 拆成 7 个 representative failure families，并保留 prompt / command / audit / no-mutation / doc anchors / NOT READY closure。A 侧验证 focused 51/51、adjacent PaymentEngine / resource skill / prompt / hub regression 609/609、backend full 4488/4488 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BE focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BD PaymentEngine coverage doc anchor integrity verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BD_PAYMENT_ENGINE_COVERAGE_DOC_ANCHOR_INTEGRITY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BD_PAYMENT_ENGINE_COVERAGE_DOC_ANCHOR_INTEGRITY_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，要求 PaymentEngine coverage manifests 的 `DocAnchors` 解析到当前仓库存在的 `docs/*.md` 文件，并修复漂移锚点。A 侧验证 focused 46/46、adjacent PaymentEngine / resource skill / prompt / hub regression 604/604、backend full 4483/4483 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BD focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BC PaymentEngine official matrix row schema verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 4D-03BA 的 12 个 residual axes 固定为 13 个 row-level entries，并保留 prompt / command / audit / rollback / remaining official breadth / NOT READY closure。A 侧验证 focused 45/45、adjacent PaymentEngine / resource skill / prompt / hub regression 603/603、backend full 4482/4482 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BC focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03BB PaymentEngine official matrix implementation handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03BB_PAYMENT_ENGINE_OFFICIAL_MATRIX_IMPLEMENTATION_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BB_PAYMENT_ENGINE_OFFICIAL_MATRIX_IMPLEMENTATION_BASELINE_EVIDENCE.md`。A 主控确认 4D-03BA 只有 axis-level residual guard，尚无 concrete row schema 枚举 official combinations；下一建议 B 切片是 4D-03BC PaymentEngine official matrix row schema / seed verifier。A 侧 baseline focused 39/39、adjacent PaymentEngine / resource skill / prompt / hub regression 597/597、backend full 4476/4476 通过；本批不改 runtime / tests / frontend / card matrix，不派发 B，不打开写锁，不触碰 `riftbound-dotnet.sln`；项目仍 **NOT READY**。
- 4D-03BA PaymentEngine official matrix residual manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 4D-03AV `OFFICIAL_PAYMENT_ENGINE_MATRIX` residual blocker 拆成 12 个 executable axis-level entries，并保留 prompt / command / audit / rollback / remaining official breadth / NOT READY closure。A 侧验证 focused 39/39、adjacent PaymentEngine / resource skill / prompt / hub regression 597/597、backend full 4476/4476 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03BA focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03AZ PaymentEngine resource skill residual manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 4D-03AL resource skill catalog-bound evidence 明确回连到 4D-03AV `RESOURCE_SKILL_A_C_FAMILY` residual blocker，保持该 family 为 `catalog-bound-representative`，并保留 19 个 current `IsResourceSkill=true` representatives、prompt / command / `ABILITY_ACTIVATED` audit / rollback / NOT READY closure 与 `[A]` / `[C]` official residuals。A 侧验证 focused 34/34、adjacent PaymentEngine / resource skill / prompt / hub regression 475/475、backend full 4471/4471 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03AZ focused-test write lock closed；项目仍 **NOT READY**。
- 4D-03AY PaymentEngine keyword payment branch manifest verifier 已完成并由 A 验收，入口为 `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_EVIDENCE.md`。本批只修改 `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` 与 docs，把 4D-03AV `KEYWORD_PAYMENT_BRANCHES` residual family 拆成 `HASTE_READY`、`ECHO_OPTIONAL_PAYMENT`、`SPELLSHIELD_TARGET_TAX`、`EXPERIENCE_PAYMENT`、`BATTLEFIELD_REPLACEMENT_COSTS`、`COST_MODIFIER_PAYMENTS`、`OPTIONAL_EXTRA_ALTERNATIVE_COSTS`、`TEMPORARY_RESOURCE_PARITY` 八个 executable branch-level entries，并保留 prompt / command / `COST_PAID` audit / rollback / remaining official breadth / NOT READY closure。A 侧验证 focused 32/32、adjacent PaymentEngine / prompt / hub / keyword regression 590/590、backend full 4469/4469 通过；runtime、frontend、card matrix JSON、fullOfficial / READY 与 `riftbound-dotnet.sln` 未触碰。4D-03AY focused-test write lock closed；项目仍 **NOT READY**。
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
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md` 已确认 active goal 的 READY 门槛仍未满足：P0/P1 未清零，1009 / 811 card matrix 仍无 full-official coverage；frontend build / Chrome smoke / formal 18-step 均有当前代码状态证据，但它们不能替代未来最终代码状态 rerun、P0/P1 closure、full-card matrix 或 READY。
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_EVIDENCE.md` 已记录 4D-03AO-B implemented / A-validated：focused 28/28、handoff focused 207/207、adjacent 400/400、backend full 4321/4321、`git diff --check` 通过。

## 2. Dispatch Queue

| Queue | Owner | Status | Purpose | Write scope | Must not touch |
|---|---|---|---|---|---|
| 4D-03CC | A 主控 | Handoff / baseline recorded; no worker dispatched | Reserve future B-side implementation / verifier boundary for Honeyfruit `UNL-049/219` equipment reaction / level-six generated mana / power resource skill | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests beyond future dispatch、Jhin / Blue Sentinel / Lux lanes、`LEGEND_ACT` bridge candidates、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03CB | A 主控 | Handoff / baseline recorded; no worker dispatched | Reserve future B-side implementation / verifier boundary for Jhin `UNL-022/219` movement-triggered generated mana / power resource skill | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests beyond future dispatch、Honeyfruit / Blue Sentinel / Lux lanes、`LEGEND_ACT` bridge candidates、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03CA | A 主控 | Test-only lane gate implemented and A-validated | Split `B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME` into four per-card future B-side acceptance lanes for Jhin, Honeyfruit, Blue Sentinel and Lux | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, 4D-03CA audit / evidence docs, checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BZ | A 主控 | Test-only verifier implemented and A-validated | Make the next two deferred resource-skill dispatch gates executable and prevent non-legend runtime candidates from mixing with legend bridge candidates | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, 4D-03BZ audit / evidence docs, checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BY | A 主控 | Handoff / baseline recorded; no worker dispatched | Reserve B-side bridge / verifier boundary for the 9 legend deferred official resource-skill candidates currently represented as `LEGEND_ACT` resource actions | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests beyond future dispatch、4D-03BX non-legend runtime candidates、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BX | A 主控 | Handoff / baseline recorded; no worker dispatched | Reserve B-side implementation / verifier boundary for the 4 non-legend deferred official resource-skill candidates | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests beyond future dispatch、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BW | A 主控 | Test-only verifier implemented and A-validated | Make the 13 deferred official resource-skill family split executable and reject legend proxy closure | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, 4D-03BW audit / evidence docs, checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BV | A 主控 | Handoff / baseline recorded; no worker dispatched | Split the 13 deferred official resource-skill candidates into legend bridge vs non-legend runtime / verifier families for future B work | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BU-B | A 主控 | Test-only verifier implemented and A-validated | Reconcile fixed official resource skill candidates against current implemented resource skill catalog and keep deferred breadth explicit | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, 4D-03BU audit / evidence docs, checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BU | A 主控 | Handoff / baseline recorded; no worker dispatched | Reserve complete `[A]` / `[C]` resource skill official breadth verifier / implementation boundary after 4D-03BT | completed handoff / baseline docs and checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、tests、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BT | A 主控 | Test-only verifier implemented and A-validated | Make 4D-03BS remaining official closure gates executable and reject proxy completion | `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, 4D-03BT audit / evidence docs, checkpoint / completion / closure / dispatch / server audit / checklist docs | runtime、frontend runtime、browser scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BS | A 主控 | Handoff / baseline recorded; no worker dispatched | Route remaining PaymentEngine official scope after 4D-03BR-B into future B/E/D dispatch boundaries | completed handoff / baseline docs and checkpoint / completion / closure / dispatch docs | runtime、tests、frontend runtime、browser scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-FE-FORMAL18 | A 主控 | A-validated | Current-code formal 18-step fresh-run | no source write; audit / evidence docs and A-master docs only | runtime、frontend source、browser smoke scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-FE-SMOKE | A 主控 | A-validated | Current-code Chrome smoke fresh-run | no source write; audit / evidence docs and A-master docs only | runtime、frontend source、browser smoke scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BR-B | A 主控 | Implemented and A-validated | Expand 4D-03AW target/tax activated ability manifest across target/payment dimensions | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BR-B audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BR | A 主控 | Handoff / baseline recorded; superseded by 4D-03BR-B verifier | Reserve PaymentEngine target / tax activated ability matrix verifier boundary | completed handoff / baseline docs and checkpoint / completion / server audit / closure docs | runtime、tests、frontend runtime、browser smoke scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BQ-B | A 主控 | Implemented and A-validated | Expand 4D-03AZ resource skill manifest across current payment surfaces | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BQ-B audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BQ | A 主控 | Handoff / baseline recorded; superseded by 4D-03BQ-B verifier | Reserve PaymentEngine resource skill all-window matrix verifier boundary | completed handoff / baseline docs and checkpoint / completion / server audit / closure docs | runtime、tests、frontend runtime、browser smoke scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BP-B | A 主控 | Implemented and A-validated | Expand 4D-03AY keyword branch manifest across current payment surfaces | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BP-B audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BP | A 主控 | Handoff / baseline recorded; superseded by 4D-03BP-B verifier | Reserve PaymentEngine keyword branch all-window matrix verifier boundary | completed handoff / baseline docs and checkpoint / completion / server audit / closure docs | runtime、tests、frontend runtime、browser smoke scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BO-B | B-Implementation / Ramanujan `019e2d82-4c8d-7390-aa92-7636f8a15179` | Implemented and A-validated | Aggregate official row schema with 03BL-B / 03BM / 03BN downstream all-window matrices | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BO-B audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BO | A 主控 | Handoff / baseline recorded; superseded by 4D-03BO-B verifier | Reserve PaymentEngine official matrix downstream aggregate verifier boundary | completed handoff / baseline docs and checkpoint / completion / server audit / closure docs | runtime、tests、frontend runtime、browser smoke scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BN | B-Implementation / Laplace `019e2d66-d12a-7233-81de-bbd0abb0dcfd` | Implemented and A-validated | PaymentEngine card matrix alignment all-window verifier | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BN audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BM | B-Implementation / Feynman `019e2d5a-e982-7833-b309-03f89a55ee45` | Implemented and A-validated | PaymentEngine cross-window generation / consumption all-window verifier | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BM audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-03BL-B | B-Implementation / Beauvoir `019e2d4a-1164-7771-bf0a-bb68e5f62b84` | Implemented and A-validated | PaymentEngine rollback failure all-window verifier | completed test/docs diff in `PaymentEngineCoverageAuditTests.cs` and 4D-03BL audit / evidence docs | runtime、frontend runtime、browser smoke scripts、card matrix JSON、broad PaymentEngine rewrite、battle lifecycle、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-FE-LABEL | A 主控 | Implemented and A-validated | Current-code frontend event-label build gate | completed label-map diff in `EventLog.tsx`, audit / evidence docs, and fresh `npm run build` | server runtime、protocol、prompt/action legality、browser smoke scripts、formal 18-step scripts、card matrix JSON、fullOfficial / READY、`riftbound-dotnet.sln` |
| 4D-NEXT-A | A 主控 | 4D-FE build gate accepted / next PaymentEngine or frontend candidate requires fresh routing | 记录 active-goal prompt-to-artifact checklist refresh、evidence 与当前暂停点 | `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md`、`docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`、checkpoint / completion / frontend audit / closure docs | runtime、tests、frontend runtime beyond label map、card matrix JSON、full-official upgrade、READY、`riftbound-dotnet.sln` |
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
| 4D-FE-C | C-Review / Copernicus | Build / smoke / formal 18 fresh-runs accepted | 准备 final frontend contract checklist for future final-state reruns | DevUi scripts and existing frontend-contract docs in read-only mode; no code write unless A opens a separate C write window | server runtime, card matrix, local rule inference in frontend |
| 4D-MATRIX-E | E-Review / Poincare | Azir 4D-03AT evidence overlay recorded; other rows read-only | 检查 latest payment representative rows and full-official blockers | Azir evidence overlay only in this batch; other card coverage docs read-only | unrelated matrix rows、full-official upgrade、READY |

## 3. Exclusive Writelocks

- 4D-FE event-label build gate frontend label write scope is closed after current-code build validation. Only `src/Riftbound.DevUi/src/components/match/EventLog.tsx` was changed. Server runtime, protocols, prompt/action legality, browser smoke scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-FE Chrome smoke fresh-run is closed after A validation. No source files, smoke scripts, formal 18-step scripts, runtime, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` were touched.
- 4D-FE formal 18-step fresh-run is closed after A validation. No source files, smoke scripts, formal 18-step scripts, runtime, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` were touched.
- 4D-03CC is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and future Honeyfruit equipment-reaction generated-resource implementation / verifier work requires a fresh explicit A dispatch. Jhin, Blue Sentinel, Lux, all `LEGEND_ACT` bridge candidates and `riftbound-dotnet.sln` remain locked.
- 4D-03CB is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and future Jhin movement-triggered generated-resource implementation / verifier work requires a fresh explicit A dispatch. Honeyfruit, Blue Sentinel, Lux, all `LEGEND_ACT` bridge candidates and `riftbound-dotnet.sln` remain locked.
- 4D-03CA focused-test lane-gate write scope is closed after A validation. Runtime, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked. Future Jhin, Honeyfruit, Blue Sentinel and Lux runtime / verifier work still requires fresh explicit A dispatch.
- 4D-03BZ focused-test write scope is closed after A validation. Runtime, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked. Future work for `B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME` and `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER` still requires fresh explicit A dispatch.
- 4D-03BY is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and future legend bridge / verifier work for Diana, Ornn, KaiSa and Darius resource-action candidates requires a fresh explicit A dispatch. The 4 non-legend 4D-03BX runtime candidates remain outside this slice, and `riftbound-dotnet.sln` remains locked.
- 4D-03BX is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and future non-legend deferred resource-skill implementation / verifier work for Jhin, Honeyfruit, Blue Sentinel and Lux requires a fresh explicit A dispatch. The 9 legend bridge candidates remain outside this slice, and `riftbound-dotnet.sln` remains locked.
- 4D-03BW focused-test write scope is closed after A validation. Runtime, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked. Future bridge / implementation work for the 13 deferred official candidates requires fresh explicit A dispatch.
- 4D-03BV is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and any future deferred resource skill family verifier or implementation work requires a fresh explicit A dispatch. `riftbound-dotnet.sln` remains locked.
- 4D-03BU-B focused-test write scope is closed after A validation. Runtime, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked. Future implementation work for the 13 deferred official candidates requires a fresh explicit A dispatch.
- 4D-03BU is A-side handoff / baseline only and has now been followed by the 4D-03BU-B accepted verifier above. No runtime / frontend / matrix write lock is open, and `riftbound-dotnet.sln` remains locked.
- 4D-03BT test-only write scope is closed after A validation. Runtime, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked. Future B/E/D work still requires fresh explicit A dispatch.
- 4D-03BS is A-side handoff / baseline only. No B / C / D / E worker is dispatched, no runtime / test / frontend / matrix write lock is open, and any future PaymentEngine official breadth, card matrix readiness or P0 audit work requires a fresh explicit A dispatch. `riftbound-dotnet.sln` remains locked.
- 4D-03BR-B focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BR A-side handoff / baseline is historical and closed. It has now been followed by the 4D-03BR-B accepted verifier above; `riftbound-dotnet.sln` remains locked.
- 4D-03BQ-B focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BQ A-side handoff / baseline is historical and closed. It has now been followed by the 4D-03BQ-B accepted verifier above; `riftbound-dotnet.sln` remains locked.
- 4D-03BP-B focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BP A-side handoff / baseline is historical and closed. It has now been followed by the 4D-03BP-B accepted verifier above; `riftbound-dotnet.sln` remains locked.
- 4D-03BO-B focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BO A-side handoff / baseline is historical and closed. It has now been followed by the 4D-03BO-B accepted verifier above; `riftbound-dotnet.sln` remains locked.
- 4D-03BN focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BM focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BL-B focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend runtime, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BL handoff / baseline is historical and closed. It has now been followed by the 4D-03BL-B accepted verifier above; `riftbound-dotnet.sln` remains locked.
- 4D-03BK focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BJ focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BI docs write lock is closed after A validation and commit-ready evidence. Runtime, tests, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BH focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BG focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BF focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BE focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BD focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BC focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03BB is A-side handoff / baseline only and has now been followed by 4D-03BC. No B worker is dispatched, no runtime/frontend/matrix write lock is open, and future official matrix implementation rows require a fresh explicit dispatch. `riftbound-dotnet.sln` remains locked.
- 4D-03BA focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03AZ focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
- 4D-03AY focused-test write lock is closed after A validation and commit-ready evidence. Runtime, frontend, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
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

## 3.0 4D-03BL Handoff Gate Accepted

A accepts the 4D-03BL handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-03BC fixed the official PaymentEngine row schema, 4D-03BE exposed the rollback failure representative families, 4D-03BH proved missing-row downstream routing, and 4D-03BK kept MOVE_UNIT outside PaymentEngine payment manifests.
3. `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` is the next concrete P0-005 row to expand because it is still a missing official row, while its downstream representative families are already visible.
4. The future 4D-03BL-B scope is narrowed to an all-window rollback failure matrix for current PaymentEngine payment surfaces; it must bind prompt quote, command rejection, no-mutation state assertion, audit expectation and doc anchors per row.
5. No runtime, test, frontend, card matrix, fullOfficial / READY or `riftbound-dotnet.sln` file is touched by this handoff batch.
6. Focused PaymentEngine coverage guard passed 70/70.
7. Adjacent PaymentEngine / resource skill / prompt / hub regression passed 628/628.
8. Backend full passed 4507/4507.
9. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BL establishes the next B-side rollback failure matrix handoff and current baseline only. No B worker is dispatched, no write lock is open, and the project remains **NOT READY**.

## 3.0B 4D-03BL-B Dispatch Boundary Accepted

A accepts the 4D-03BL-B dispatch boundary because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-03BL established the rollback failure matrix handoff and baseline; no implementation has yet modified runtime, tests, frontend or matrix after that handoff.
3. `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` remains the next concrete P0-005 PaymentEngine row because it is still `missing-official-row` and the downstream representative failure families are already visible.
4. Future B implementation must expand the 7 rollback families into an executable all-window matrix covering current PaymentEngine surfaces: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.
5. Each future row must bind action window, failure dimension, payment source kind, prompt quote, command rejection, no-mutation state assertion, audit expectation and doc anchor.
6. Future B may touch runtime only if the new verifier exposes an actual rollback mismatch; otherwise the slice should remain verifier / focused-test / docs.
7. Frontend, browser smoke scripts, card matrix JSON, broad PaymentEngine rewrite, battle lifecycle / cleanup queues, fullOfficial / READY and `riftbound-dotnet.sln` remain locked.
8. The dispatch boundary inherits 4D-03BL baseline evidence: focused 70/70, adjacent 628/628 and backend full 4507/4507.
9. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Historical pause point: 4D-03BL-B reserved the next B-side implementation boundary. It has now been superseded by the accepted 4D-03BL-B verifier in section 3.0C, and the project remains **NOT READY**.

## 3.0C 4D-03BL-B Acceptance Gate Accepted

A accepts the 4D-03BL-B verifier because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this batch.
2. B-Implementation / Beauvoir `019e2d4a-1164-7771-bf0a-bb68e5f62b84` stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md` / evidence.
3. The diff creates a 42-row all-window matrix: 6 PaymentEngine payment surfaces x 7 rollback failure representative families.
4. Each matrix row binds action window, failure dimension, payment source kind, prompt quote, command rejection, no-mutation assertion, audit expectation and doc anchors.
5. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside this 4D-03BL-B payment rollback matrix.
6. No runtime, frontend, browser smoke script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` change occurred.
7. Focused PaymentEngine coverage guard passed 75/75.
8. Adjacent PaymentEngine / resource skill / prompt / hub regression passed 633/633.
9. Backend full passed 4512/4512.
10. `git diff --check` passed.
11. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BL-B is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0D 4D-03BM Acceptance Gate Accepted

A accepts the 4D-03BM verifier because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this batch.
2. B-Implementation / Feynman `019e2d5a-e982-7833-b309-03f89a55ee45` stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md` / evidence.
3. The diff creates a 42-row all-window matrix: 6 PaymentEngine payment surfaces x 7 cross-window generation / consumption representative families.
4. Each matrix row binds action window, generation scope, consumption scope, resource / lifetime dimension, prompt quote, command commit or rejection anchor, audit expectation, lifetime / no-mutation / restriction assertion and doc anchors.
5. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside this 4D-03BM payment matrix.
6. No runtime, frontend, browser smoke script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` change occurred.
7. Focused PaymentEngine coverage guard passed 80/80.
8. Adjacent PaymentEngine / resource skill / prompt / hub regression passed 638/638.
9. Backend full passed 4517/4517.
10. `git diff --check` passed.
11. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BM is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0E 4D-03BN Acceptance Gate Accepted

A accepts the 4D-03BN verifier because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this batch.
2. B-Implementation / Laplace `019e2d66-d12a-7233-81de-bbd0abb0dcfd` stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md` / evidence.
3. The diff creates a 48-row all-window matrix: 6 PaymentEngine payment surfaces x 8 card matrix alignment representative families.
4. Each matrix row binds action window, matrix scope, representative surface, prompt evidence, command evidence, audit evidence, matrix sync/status, frontend/snapshot or rule-source trace, remaining official breadth, closure status and doc anchors.
5. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside this 4D-03BN payment matrix.
6. No runtime, frontend, browser smoke script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` change occurred.
7. Focused PaymentEngine coverage guard passed 85/85.
8. Adjacent PaymentEngine / resource skill / prompt / hub regression passed 643/643.
9. Backend full passed 4522/4522.
10. `git diff --check` passed.
11. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BN is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0F 4D-03BO Handoff Gate Accepted

A accepts the 4D-03BO handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03BC established the official row schema, 4D-03BL-B established rollback failure all-window matrix evidence, 4D-03BM established cross-window generation / consumption all-window matrix evidence, and 4D-03BN established card matrix alignment all-window matrix evidence.
3. The next narrow B-side target is an aggregate verifier, not a runtime implementation: it should bind 9 representative seed rows, 3 missing official rows, 1 MOVE_UNIT policy-deferred row and the three downstream all-window matrices into one executable audit contract.
4. Future B write scope is limited to `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BO aggregate audit / evidence docs.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 85/85, adjacent PaymentEngine / resource skill / prompt / hub regression 643/643, backend full 4522/4522 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BO establishes the future B-side downstream aggregate verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0G 4D-03BO-B Acceptance Gate Accepted

A accepts the 4D-03BO-B verifier because all of the following are true:

1. B-Implementation / Ramanujan `019e2d82-4c8d-7390-aa92-7636f8a15179` stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BO-B audit / evidence docs.
2. The new aggregate manifest preserves 9 representative seed rows, 3 missing official rows and 1 MOVE_UNIT policy-deferred row from the official row schema.
3. Each missing official row has exactly one downstream aggregate entry, and each downstream all-window row points back to its matching official row id.
4. The aggregate verifies current downstream counts: rollback 7/42, cross-window 7/42 and card matrix alignment 8/48.
5. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside the current PaymentEngine payment surfaces aggregate.
6. Focused 92/92, adjacent PaymentEngine / resource skill / prompt / hub regression 650/650, backend full 4529/4529 and `git diff --check` all passed.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BO-B is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0H 4D-03BP Handoff Gate Accepted

A accepts the 4D-03BP handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03AY established 8 keyword payment branch entries, while the current PaymentEngine payment surfaces remain the same 6 surfaced by recent all-window matrices.
3. The next narrow B-side target is a matrix verifier, not a runtime implementation: it should bind 8 keyword payment branches x 6 payment surfaces into 48 quote-command-audit-rollback rows.
4. Future B write scope is limited to `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BP keyword branch matrix audit / evidence docs.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 92/92, adjacent PaymentEngine / resource skill / prompt / hub regression 650/650, backend full 4529/4529 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BP establishes the future B-side keyword branch all-window matrix verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0I 4D-03BP-B Acceptance Gate Accepted

A accepts the 4D-03BP-B verifier because all of the following are true:

1. The implementation stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BP-B audit / evidence docs.
2. The new all-window matrix preserves the 8 branch ids from `KeywordPaymentBranchManifest`.
3. The matrix covers the 6 current PaymentEngine payment surfaces: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.
4. The generated matrix has exactly 48 rows and no duplicate action-window / branch pair.
5. Each row binds prompt quote, command-side revalidation, `COST_PAID` audit, rollback/no-mutation, `KEYWORD_PAYMENT_BRANCHES` residual and relevant official residual axes.
6. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside the current keyword branch all-window matrix.
7. Focused 97/97, adjacent PaymentEngine / resource skill / prompt / hub regression 655/655, backend full 4534/4534 and `git diff --check` all passed.
8. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BP-B is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0J 4D-03BQ Handoff Gate Accepted

A accepts the 4D-03BQ handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03AZ established 6 resource skill family entries and 19 current catalog `IsResourceSkill=true` ability ids, while the current PaymentEngine payment surfaces remain the same 6 surfaced by recent all-window matrices.
3. The next narrow B-side target is a matrix verifier, not a runtime implementation: it should bind 6 resource skill families x 6 payment surfaces into 36 prompt-command-audit-generated-resource-rollback rows.
4. Future B write scope is limited to `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BQ resource skill all-window matrix audit / evidence docs.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 97/97, adjacent PaymentEngine / resource skill / prompt / hub regression 655/655, backend full 4534/4534 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BQ establishes the future B-side resource skill all-window matrix verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0K 4D-03BQ-B Acceptance Gate Accepted

A accepts the 4D-03BQ-B verifier because all of the following are true:

1. The implementation stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BQ-B audit / evidence docs.
2. The new all-window matrix preserves the 6 family entries and 19 current catalog ability ids from `ResourceSkillCoverageManifest`.
3. The matrix covers the 6 current PaymentEngine payment surfaces: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.
4. The generated matrix has exactly 36 rows and no duplicate action-window / resource-skill-family pair.
5. Each row binds prompt quote, command-side revalidation, `ABILITY_ACTIVATED` / generated-resource audit, generated-resource payment-only restrictions, rollback/no-mutation, `RESOURCE_SKILL_A_C_FAMILY` residual and `RESOURCE_SKILLS` official residual axis.
6. `MOVE_UNIT`, `HIDE_CARD` and `LEGEND_ACT` remain outside the current resource skill all-window matrix.
7. Focused 102/102, adjacent PaymentEngine / resource skill / prompt / hub regression 660/660, backend full 4539/4539 and `git diff --check` all passed.
8. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BQ-B is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0L 4D-03BR Handoff Gate Accepted

A accepts the 4D-03BR handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03AW established 8 current target-bearing / typed / experience / Spellshield-tax activated ability entries, while `TARGET_TAXES` remains a `remaining-official-gap`.
3. The next narrow B-side target is a matrix verifier, not a runtime implementation: it should bind 8 ability entries x 6 target/payment dimensions into 48 source-target-payment-audit-rollback rows.
4. Future B write scope is limited to `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BR target / tax activated ability matrix audit / evidence docs.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 102/102, adjacent PaymentEngine / resource skill / prompt / hub regression 660/660, backend full 4539/4539 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BR establishes the future B-side target / tax activated ability matrix verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0M 4D-03BR-B Acceptance Gate

A accepts the 4D-03BR-B verifier because all of the following are true:

1. The implementation stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BR-B audit / evidence docs.
2. The new matrix preserves the 8 ability entries from `TargetColoredActivatedAbilityCoverageManifest`.
3. The matrix covers 6 target/payment dimensions: source timing, target profile, payment profile, target-tax or optional branch, command rollback and official closure / card-matrix trace.
4. The generated matrix has exactly 48 rows and no duplicate ability / dimension pair.
5. Each row binds `ACTIVATE_ABILITY`, prompt quote, command-side revalidation, `COST_PAID` / `ABILITY_ACTIVATED` audit, rollback/no-mutation, `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual and `TARGET_TAXES` official residual axis.
6. Non-activated payment windows remain outside this matrix.
7. Focused 107/107, adjacent PaymentEngine / resource skill / prompt / hub regression 665/665, backend full 4544/4544 and `git diff --check` passed.
8. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BR-B is accepted and its focused-test write lock is closed. The project remains **NOT READY**.

## 3.0N 4D-03BV Handoff Gate Accepted

A accepts the 4D-03BV handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03BU fixed the official resource-skill candidate set at 32 entries, with 19 current `IsResourceSkill=true` source card nos and 13 deferred official candidates.
3. The deferred set is now split into two future B-side families: 9 existing `LEGEND_ACT` resource actions requiring an explicit resource-skill closure bridge, and 4 non-legend unit / equipment / delayed resource skills requiring verifier / runtime breadth.
4. Existing Darius / Diana / KaiSa / Ornn `LEGEND_ACT` tests and current Jhin / Honeyfruit / Blue Sentinel / Lux preflight evidence are useful inputs, but they do not close `RESOURCE_SKILLS` by proxy.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 115/115, adjacent PaymentEngine / resource skill / prompt / hub regression 673/673, backend full 4552/4552 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BV establishes the future B-side deferred resource skill family boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0O 4D-03BW Acceptance Gate Accepted

A accepts the 4D-03BW verifier because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this batch.
2. The implementation stayed inside the assigned write scope: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus 4D-03BW audit / evidence docs and A-side routing docs.
3. `DeferredResourceSkillFamilyManifest` exactly matches the 13 deferred official resource-skill candidates from 4D-03BU / 4D-03BV.
4. The executable split preserves 9 existing `LEGEND_ACT` resource-action bridge candidates and 4 non-legend runtime / verifier candidates.
5. The verifier rejects legend proxy closure: existing Darius / Diana / KaiSa / Ornn legend evidence cannot close `RESOURCE_SKILLS` until a future explicit bridge / implementation slice is dispatched and accepted.
6. No runtime, frontend, browser script, formal 18-step script, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln` change occurred.
7. Focused PaymentEngine coverage guard passed 119/119.
8. Adjacent PaymentEngine / resource skill / prompt / hub regression passed 677/677.
9. Backend full passed 4556/4556.
10. `git diff --check` passed.
11. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BW is accepted and its focused-test write scope is closed. The project remains **NOT READY**.

## 3.0P 4D-03BX Handoff Gate Accepted

A accepts the 4D-03BX handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03BW fixed the deferred official resource-skill family split at 13 candidates: 9 existing `LEGEND_ACT` bridge candidates and 4 non-legend runtime / verifier candidates.
3. 4D-03BX reserves only the 4 non-legend candidates for a future B-side implementation / verifier boundary: Jhin `UNL-022/219`, Honeyfruit `UNL-049/219`, Blue Sentinel `UNL-087/219` and Lux `OGS·014/024`.
4. The 9 legend bridge candidates remain outside this slice and require a separate fresh A dispatch before any bridge or implementation work.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 119/119, adjacent PaymentEngine / resource skill / prompt / hub regression 677/677, backend full 4556/4556 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BX establishes the future B-side non-legend deferred resource skill runtime / verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0Q 4D-03BY Handoff Gate Accepted

A accepts the 4D-03BY handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln` outside this docs-only batch.
2. 4D-03BW fixed the deferred official resource-skill family split at 13 candidates, and 4D-03BX separately reserved the 4 non-legend runtime / verifier candidates.
3. 4D-03BY reserves only the 9 legend bridge candidates for a future B-side bridge / verifier boundary: Diana `UNL-197/219`, Ornn `SFD·189/221` / `SFD·244/221`, KaiSa `OGN·247/298` / `OGN·299/298` / `OGN·299*/298`, and Darius `OGN·253/298` / `OGN·302/298` / `OGN·302*/298`.
4. Existing Darius / Diana / KaiSa / Ornn `LEGEND_ACT` tests remain evidence inputs, not proxy `RESOURCE_SKILLS` closure.
5. This handoff itself did not modify runtime, tests, frontend, browser smoke scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Baseline validation passed: focused 119/119, adjacent PaymentEngine / resource skill / prompt / hub regression 677/677, backend full 4556/4556 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BY establishes the future B-side legend resource-action bridge / verifier boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0R 4D-03BZ Next-Dispatch Gate Accepted

A accepts the 4D-03BZ test-only gate because all of the following are true:

1. `PaymentEngineDeferredResourceSkillNextDispatchGateManifest` lists exactly two fresh B-side gates: `B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME` and `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`.
2. The two gate candidate lists exactly recompose the 13 deferred official `RESOURCE_SKILLS` candidates from `DeferredResourceSkillFamilyManifest`.
3. The non-legend gate contains only Jhin `UNL-022/219`, Honeyfruit `UNL-049/219`, Blue Sentinel `UNL-087/219` and Lux `OGS·014/024`, and rejects borrowing `LEGEND_ACT` evidence.
4. The legend gate contains only Diana `UNL-197/219`, Ornn `SFD·189/221` / `SFD·244/221`, KaiSa `OGN·247/298` / `OGN·299/298` / `OGN·299*/298`, and Darius `OGN·253/298` / `OGN·302/298` / `OGN·302*/298`, and treats existing `LEGEND_ACT` tests as inputs rather than proxy closure.
5. This focused-test batch did not modify runtime, frontend, browser smoke scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.
6. Validation passed: focused 123/123, adjacent PaymentEngine / resource skill / prompt / hub regression 681/681, backend full 4560/4560 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03BZ accepts only the executable next-dispatch gate. No B worker is dispatched in this batch, no runtime write lock is open, and the project remains **NOT READY**.

## 3.0S 4D-03CA Non-Legend Runtime Lane Gate Accepted

A accepts the 4D-03CA test-only lane gate because all of the following are true:

1. `PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifest` exactly matches the four non-legend candidates from the 4D-03BZ `B_DEFERRED_NON_LEGEND_RESOURCE_SKILL_RUNTIME` gate.
2. The four lanes are distinct: Jhin `UNL-022/219` movement-triggered generated mana / power, Honeyfruit `UNL-049/219` equipment reaction / level-six branch, Blue Sentinel `UNL-087/219` delayed next-main generated power, and Lux `OGS·014/024` spell-only tap reaction generated mana.
3. Every lane requires server-filtered prompt condition, command-side revalidation, audit / generated-resource lifetime evidence, no-mutation rollback evidence and future focused test anchors.
4. The lane gate explicitly keeps `LEGEND_ACT` bridge rows, frontend runtime, browser scripts, formal 18-step scripts, card matrix JSON, fullOfficial / READY and `riftbound-dotnet.sln` out of scope.
5. This focused-test batch did not implement runtime behavior or dispatch B.
6. Validation passed: focused 127/127, adjacent PaymentEngine / resource skill / prompt / hub regression 685/685, backend full 4564/4564 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03CA accepts only the executable non-legend lane gate. No B worker is dispatched in this batch, no runtime write lock is open, and the project remains **NOT READY**.

## 3.0T 4D-03CB Jhin Handoff Gate Accepted

A accepts the 4D-03CB handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-03CA split the non-legend gate into four lanes, and this handoff reserves only `LANE_JHIN_MOVE_TRIGGERED_RESOURCE_SKILL`.
3. The future B boundary is limited to Jhin `UNL-022/219` movement-triggered generated mana / power behavior, including prompt filtering, command revalidation, audit / lifetime evidence and no-mutation rollback.
4. Honeyfruit, Blue Sentinel, Lux and the 9 `LEGEND_ACT` bridge candidates remain outside this slice.
5. No runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix, fullOfficial / READY or `riftbound-dotnet.sln` file is touched by this handoff batch.
6. Baseline validation passed: focused 127/127, adjacent PaymentEngine / resource skill / prompt / hub regression 685/685, backend full 4564/4564 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03CB establishes the future B-side Jhin movement-triggered resource skill boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

## 3.0U 4D-03CC Honeyfruit Handoff Gate Accepted

A accepts the 4D-03CC handoff / baseline because all of the following are true:

1. Current repo state remains on `main` with only expected untracked `riftbound-dotnet.sln`.
2. 4D-03CA split the non-legend gate into four lanes, 4D-03CB separately reserved the Jhin lane, and this handoff reserves only `LANE_HONEYFRUIT_EQUIPMENT_REACTION_RESOURCE_SKILL`.
3. The future B boundary is limited to Honeyfruit `UNL-049/219` equipment reaction / level-six generated mana / power behavior, including prompt filtering, command revalidation, audit / lifetime evidence and no-mutation rollback.
4. Jhin, Blue Sentinel, Lux and the 9 `LEGEND_ACT` bridge candidates remain outside this slice.
5. No runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix, fullOfficial / READY or `riftbound-dotnet.sln` file is touched by this handoff batch.
6. Baseline validation passed: focused 127/127, adjacent PaymentEngine / resource skill / prompt / hub regression 685/685, backend full 4564/4564 and `git diff --check`.
7. P0-005, P1, frontend final validation, full-card matrix and READY remain open.

Pause point: 4D-03CC establishes the future B-side Honeyfruit equipment-reaction resource skill boundary only. No B worker is dispatched in this batch, no write lock is open, and the project remains **NOT READY**.

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

This record stops after establishing 4D-03CC PaymentEngine Honeyfruit equipment-reaction resource skill handoff / baseline. The project remains **NOT READY**. No frontend, matrix, runtime or open test write window remains open, and `riftbound-dotnet.sln` remains untouched.
