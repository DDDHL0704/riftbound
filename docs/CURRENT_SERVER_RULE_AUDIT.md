# 符文战场服务端核心规则自查报告

自查日期：2026-05-16
历史自查基准提交：`45bb446`。
2026-05-16 4D-03BR-B PaymentEngine target / tax activated ability matrix verifier 补充：A 侧已完成 test-only verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在新增 `TargetTaxActivatedAbilityMatrixManifest`，要求 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 target-bearing / typed / experience / Spellshield-tax activated ability entries 与 6 个 target/payment dimensions 形成 48 行 target/tax activated ability matrix，并锁定 ability id、`ACTIVATE_ABILITY` domain、source timing、target profile、payment profile、target-tax / optional branch、prompt quote、command-side revalidation、`COST_PAID` / `ABILITY_ACTIVATED` audit、rollback/no-mutation、`TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual 与 `TARGET_TAXES` official residual axis。验证：focused 107/107、adjacent PaymentEngine / resource skill / prompt / hub regression 665/665、backend full 4544/4544、`git diff --check` passed。本批不改 runtime / frontend / matrix，不关闭完整 target/tax official coverage、P0-005 或 READY。
2026-05-16 4D-03BR PaymentEngine target / tax activated ability matrix handoff / baseline 补充：A 主控已建立下一枚 docs-only target/tax activated ability matrix handoff，交接与基线见 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BR_PAYMENT_ENGINE_TARGET_TAX_ACTIVATED_ABILITY_MATRIX_BASELINE_EVIDENCE.md`。该 handoff 已被 4D-03BR-B verifier supersede；其原定范围是把 `TargetColoredActivatedAbilityCoverageManifest` 的 8 个 target-bearing / typed / experience / Spellshield-tax activated ability representatives，按 source timing、target profile、payment profile、target-tax / optional branch、command rollback 与 official-closure trace 6 个 dimensions 扩成 48 行 target/tax matrix，并继续回连 `TARGET_BEARING_COLORED_ACTIVATED_ABILITIES` residual 与 `TARGET_TAXES` official residual axis。A 侧 baseline 验证 focused 102/102、adjacent PaymentEngine / resource skill / prompt / hub regression 660/660、backend full 4539/4539、`git diff --check` 通过。本批不改 runtime / tests / frontend / matrix，不派发 B，不打开写锁，不关闭 P0-005 或 READY。
2026-05-16 4D-03BQ-B PaymentEngine resource skill all-window matrix verifier 补充：A 侧已完成 test-only verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在新增 `ResourceSkillAllWindowMatrixManifest`，要求 4D-03AZ / `ResourceSkillCoverageManifest` 的 6 个 resource skill family entries 与 6 个 current PaymentEngine payment surfaces 形成 36 行 all-window matrix，并锁定 family、ability ids、generated resource / payment-only lifecycle、prompt quote、command-side revalidation、`ABILITY_ACTIVATED` / generated-resource audit、rollback/no-mutation、`RESOURCE_SKILL_A_C_FAMILY` residual 与 `RESOURCE_SKILLS` residual axis。验证：focused 102/102、adjacent PaymentEngine / resource skill / prompt / hub regression 660/660、backend full 4539/4539、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 resource skill official family、P0-005 或 READY。
2026-05-16 4D-03BQ PaymentEngine resource skill all-window matrix handoff / baseline 补充：A 主控已建立下一枚 docs-only resource skill all-window matrix handoff，交接与基线见 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BQ_PAYMENT_ENGINE_RESOURCE_SKILL_ALL_WINDOW_MATRIX_BASELINE_EVIDENCE.md`。future 4D-03BQ-B 应把 4D-03AZ / `ResourceSkillCoverageManifest` 的 6 个 resource skill family entries 与 19 个 current catalog `IsResourceSkill=true` ability ids，按 6 个 current PaymentEngine payment surfaces 扩成 36 行 family-window prompt-command-audit-generated-resource-rollback matrix，并继续回连 `RESOURCE_SKILL_A_C_FAMILY` residual 与 `RESOURCE_SKILLS` official residual axis。A 侧 baseline 验证 focused 97/97、adjacent PaymentEngine / resource skill / prompt / hub regression 655/655、backend full 4534/4534、`git diff --check` 通过。本批不改 runtime / tests / frontend / matrix，不派发 B，不打开写锁，不关闭 P0-005 或 READY。
2026-05-16 4D-03BP-B PaymentEngine keyword branch all-window matrix verifier 补充：A 侧已完成 test-only verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在新增 `KeywordPaymentBranchAllWindowMatrixManifest`，要求 4D-03AY 的 8 个 keyword payment branch entries 与 6 个 current PaymentEngine payment surfaces 形成 48 行 all-window matrix，并锁定 branch id、prompt quote、command-side revalidation、`COST_PAID` audit、rollback/no-mutation、`KEYWORD_PAYMENT_BRANCHES` residual 与 `KEYWORD_BRANCHES` / `COST_MODIFIERS` / `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` / `REPLACEMENT_PREVENTION` residual axes。验证：focused 97/97、adjacent PaymentEngine / resource skill / prompt / hub regression 655/655、backend full 4534/4534、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 keyword payment parity、P0-005 或 READY。
2026-05-16 4D-03BP PaymentEngine keyword branch all-window matrix handoff / baseline 补充：A 主控已建立下一枚 docs-only keyword branch matrix handoff，交接与基线见 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BP_PAYMENT_ENGINE_KEYWORD_BRANCH_MATRIX_BASELINE_EVIDENCE.md`。future 4D-03BP-B 应把 4D-03AY 的 8 个 keyword payment branch entries 与 6 个 current PaymentEngine payment surfaces 扩成 48 行 all-window quote-command-audit-rollback matrix，并继续回连 `KEYWORD_PAYMENT_BRANCHES`、`KEYWORD_BRANCHES` / `COST_MODIFIERS` / `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` / `REPLACEMENT_PREVENTION` residual axes。A 侧 baseline 验证 focused 92/92、adjacent PaymentEngine / resource skill / prompt / hub regression 650/650、backend full 4529/4529、`git diff --check` 通过。本批不改 runtime / tests / frontend / matrix，不派发 B，不打开写锁，不关闭 P0-005 或 READY。
2026-05-16 4D-03BO-B PaymentEngine official matrix downstream aggregate verifier 补充：B-Implementation / Ramanujan `019e2d82-4c8d-7390-aa92-7636f8a15179` 已完成 test-only aggregate verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03BC official row schema 与 downstream all-window matrices 精确聚合：9 representative seeds、3 missing official rows、1 MOVE_UNIT policy-deferred row、rollback 7/42、cross-window 7/42、card matrix alignment 8/48，并要求每个 all-window row 回连 matching official missing row id；`MOVE_UNIT`、`HIDE_CARD`、`LEGEND_ACT` 仍不进入当前 PaymentEngine payment surfaces。验证：focused 92/92、adjacent PaymentEngine / resource skill / prompt / hub regression 650/650、backend full 4529/4529、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BO PaymentEngine official matrix downstream aggregate handoff / baseline 补充：A 主控已建立下一枚 docs-only aggregate handoff，审计与基线见 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BO_PAYMENT_ENGINE_OFFICIAL_MATRIX_DOWNSTREAM_AGGREGATE_BASELINE_EVIDENCE.md`。future 4D-03BO-B 应把 4D-03BC official row schema、9 representative seeds、3 missing official rows、1 MOVE_UNIT policy-deferred row、rollback 7/42、cross-window 7/42 与 card matrix alignment 8/48 聚合成 executable audit contract。A 侧 baseline 验证 focused 85/85、adjacent PaymentEngine / resource skill / prompt / hub regression 643/643、backend full 4522/4522、`git diff --check` 通过。本批不改 runtime / tests / frontend / matrix，不派发 B，不打开写锁，不关闭 P0-005 或 READY。
2026-05-16 4D-03BN PaymentEngine card matrix alignment all-window matrix verifier 补充：B-Implementation / Laplace `019e2d66-d12a-7233-81de-bbd0abb0dcfd` 已完成 test-only all-window card matrix alignment verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BN_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 4D-03BG 的 8 个 card matrix alignment families 扩展为 48 行 matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`，并锁定 matrix scope / representative surface / prompt evidence / command evidence / audit evidence / matrix sync-status / frontend-snapshot or rule-source trace / remaining official breadth / doc anchors。验证：focused 85/85、adjacent PaymentEngine / resource skill / prompt / hub regression 643/643、backend full 4522/4522、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 card matrix alignment official coverage、P0-005 或 READY。
2026-05-16 4D-03BM PaymentEngine cross-window generation / consumption all-window matrix verifier 补充：B-Implementation / Feynman `019e2d5a-e982-7833-b309-03f89a55ee45` 已完成 test-only all-window cross-window matrix verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BM_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 4D-03BF 的 7 个 generated-resource cross-window representative families 扩展为 42 行 matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`，并锁定 generation scope / consumption scope / resource-lifetime / prompt quote / command commit-or-rejection / audit / lifetime-no-mutation-restriction / doc anchors。验证：focused 80/80、adjacent PaymentEngine / resource skill / prompt / hub regression 638/638、backend full 4517/4517、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 cross-window generation / consumption official coverage、P0-005 或 READY。
2026-05-16 4D-03BL-B PaymentEngine rollback failure official matrix verifier 补充：B-Implementation / Beauvoir `019e2d4a-1164-7771-bf0a-bb68e5f62b84` 已完成 test-only all-window rollback matrix verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 4D-03BE 的 7 个 rollback failure representative families 扩展为 42 行 matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`，并锁定 prompt quote / command rejection / no-mutation / audit / doc anchors。验证：focused 75/75、adjacent PaymentEngine / resource skill / prompt / hub regression 633/633、backend full 4512/4512、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 rollback failure official coverage、P0-005 或 READY。
2026-05-16 4D-03BL-B PaymentEngine rollback failure official matrix dispatch boundary 补充：A 主控已在 `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md` 保留下一枚 B-side rollback matrix 写锁边界。future B 只能将 4D-03BE 的 failure families 扩成 all-window executable rollback matrix，覆盖 prompt quote / command rejection / no-mutation / audit parity；只有 verifier 暴露真实 rollback mismatch 时才允许最小 runtime fix。A 侧未改 runtime / tests / frontend / matrix；该边界继承 4D-03BL baseline focused 70/70、adjacent 628/628、backend full 4507/4507。P0-005 与 READY 仍未关闭。
2026-05-16 4D-03BL PaymentEngine rollback failure official matrix handoff / baseline 补充：A 主控已建立下一枚 P0-005 rollback failure matrix handoff，交接与基线见 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_BASELINE_EVIDENCE.md`。未来 4D-03BL-B 应把 4D-03BE 的 7 个 rollback failure representative families 转成 all-window executable rollback matrix，覆盖 `PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT` 等当前 PaymentEngine payment surfaces 的 prompt quote / command rejection / no-mutation / audit parity。A 侧 baseline focused 70/70、adjacent 628/628、backend full 4507/4507 通过。本批不改 runtime / tests / frontend / card matrix，不派发 B，不打开写锁，不关闭 P0-005 或 READY。
2026-05-16 4D-03BK PaymentEngine policy-deferred MOVE_UNIT boundary verifier 补充：A 侧已完成 test-only policy-deferred aggregate verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03BC 的唯一 `policy-deferred-row` 精确等于 `ROW_MOVE_UNIT_POLICY_DEFERRED`，保留 `ACTION_WINDOWS` / `MOVE_UNIT` movement-permission policy boundary，并确认它不进入 representative seed、missing official row 或 downstream PaymentEngine payment manifests。验证：focused 70/70、adjacent PaymentEngine / resource skill / prompt / hub regression 628/628、backend full 4507/4507 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BJ PaymentEngine representative seed upstream coverage verifier 补充：A 侧已完成 test-only representative-seed aggregate verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BJ_PAYMENT_ENGINE_REPRESENTATIVE_SEED_UPSTREAM_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BJ_PAYMENT_ENGINE_REPRESENTATIVE_SEED_UPSTREAM_COVERAGE_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03BC 的 9 个 `representative-seed` rows 均有 upstream audit manifest anchors，并保持 prompt、command、audit、no-mutation rollback evidence 分离；seed rows 不得混入 4D-03BH missing-row aggregate doc 或 `Missing official row` 口径。验证：focused 67/67、adjacent PaymentEngine / resource skill / prompt / hub regression 625/625、backend full 4504/4504 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BI Active goal prompt-to-artifact checklist refresh 补充：A 侧已刷新 `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md`，审计与证据见 `docs/CURRENT_STAGE4D_03BI_ACTIVE_GOAL_CHECKLIST_REFRESH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BI_ACTIVE_GOAL_CHECKLIST_REFRESH_EVIDENCE.md`。当前 checklist 已对齐 4D-03BH、backend full 4501/4501、matrix 1009/811 且 0 full-official，并继续明确 P0/P1、frontend final fresh-run、full-card matrix 与 READY 未关闭。本批只改 docs，不改 runtime / tests / frontend / matrix。
2026-05-16 4D-03BH PaymentEngine missing row downstream coverage verifier 补充：A 侧已完成 test-only missing-row aggregate verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03BC 的全部 `missing-official-row` 均有 downstream representative manifest 覆盖，并确认 `ROW_MOVE_UNIT_POLICY_DEFERRED` 不进入 PaymentEngine payment row。验证：focused 64/64、adjacent PaymentEngine / resource skill / prompt / hub regression 622/622、backend full 4501/4501 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BG PaymentEngine card matrix alignment row manifest verifier 补充：A 侧已完成 test-only card matrix alignment row verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BG_PAYMENT_ENGINE_CARD_MATRIX_ALIGNMENT_ROW_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 `ROW_CARD_MATRIX_ALIGNMENT_MISSING` 拆成 8 个 representative card matrix families，并锁定 prompt、command、audit、matrix、doc anchors 与 NOT READY closure。验证：focused 61/61、adjacent PaymentEngine / resource skill / prompt / hub regression 619/619、backend full 4498/4498 通过。本批不改 runtime / frontend / matrix，不关闭完整 card matrix alignment、P0-005 或 READY。
2026-05-16 4D-03BF PaymentEngine cross-window generation / consumption row manifest verifier 补充：A 侧已完成 test-only cross-window generated-resource row verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BF_PAYMENT_ENGINE_CROSS_WINDOW_GENERATION_CONSUMPTION_ROW_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING` 拆成 7 个 representative families，并锁定 prompt、command、audit、lifetime/no-mutation、doc anchors 与 NOT READY closure。验证：focused 56/56、adjacent PaymentEngine / resource skill / prompt / hub regression 614/614、backend full 4493/4493 通过。本批不改 runtime / frontend / matrix，不关闭完整 cross-window generation / consumption matrix、P0-005 或 READY。
2026-05-16 4D-03BE PaymentEngine rollback failure row manifest verifier 补充：A 侧已完成 test-only rollback failure row verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` 拆成 7 个 representative failure families，并锁定 prompt、command、audit、no-mutation、doc anchors 与 NOT READY closure。验证：focused 51/51、adjacent PaymentEngine / resource skill / prompt / hub regression 609/609、backend full 4488/4488 通过。本批不改 runtime / frontend / matrix，不关闭完整 rollback failure matrix、P0-005 或 READY。
2026-05-16 4D-03BD PaymentEngine coverage doc anchor integrity verifier 补充：A 侧已完成 test-only doc-anchor existence verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BD_PAYMENT_ENGINE_COVERAGE_DOC_ANCHOR_INTEGRITY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BD_PAYMENT_ENGINE_COVERAGE_DOC_ANCHOR_INTEGRITY_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 PaymentEngine coverage manifests 的 `DocAnchors` 解析到当前仓库真实 `docs/*.md`，并修复 HASTE_READY + Tempered、Brush replacement、cost modifier、Tempered/Jax/Akshan equipment branch 的漂移锚点。验证：focused 46/46、adjacent PaymentEngine / resource skill / prompt / hub regression 604/604、backend full 4483/4483 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BC PaymentEngine official matrix row schema verifier 补充：A 侧已完成 test-only row schema / seed verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在把 4D-03BA 的 12 个 residual axes 固定为 13 个 row-level entries，并区分 `representative-seed`、`missing-official-row`、`policy-deferred-row`；每行必须保留 prompt、command、audit、no-mutation rollback、remaining official breadth、doc anchors 与 NOT READY closure。验证：focused 45/45、adjacent PaymentEngine / resource skill / prompt / hub regression 603/603、backend full 4482/4482 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03BB PaymentEngine official matrix implementation handoff / baseline 补充：A 主控已建立下一枚 official matrix row schema / seed verifier handoff，交接与基线见 `docs/CURRENT_STAGE4D_03BB_PAYMENT_ENGINE_OFFICIAL_MATRIX_IMPLEMENTATION_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03BB_PAYMENT_ENGINE_OFFICIAL_MATRIX_IMPLEMENTATION_BASELINE_EVIDENCE.md`。当前 4D-03BA 只有 12 个 residual axes，尚无 concrete row schema 枚举 official combinations；下一建议 4D-03BC 应在 test-only conformance 层建立 row schema / seeded rows，同时保持 P0-005 open。验证：focused 39/39、adjacent PaymentEngine / resource skill / prompt / hub regression 597/597、backend full 4476/4476 通过。本批不改 runtime / frontend / matrix，不派发 B，不关闭 P0-005 或 READY。
2026-05-16 4D-03BA PaymentEngine official matrix residual manifest verifier 补充：A 侧已完成 test-only axis-level residual verifier，审计与证据见 `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03AV `OFFICIAL_PAYMENT_ENGINE_MATRIX` residual blocker 拆成 12 个 remaining-official-gap axes，并继续锁定 prompt、command、audit、no-mutation rollback、doc anchors、remaining official breadth 与 NOT READY closure。验证：focused 39/39、adjacent PaymentEngine / resource skill / prompt / hub regression 597/597、backend full 4476/4476 通过。本批不改 runtime / frontend / matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
2026-05-16 4D-03AZ PaymentEngine resource skill residual manifest verifier 补充：A 侧已完成 test-only catalog-bound residual verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AZ_PAYMENT_ENGINE_RESOURCE_SKILL_RESIDUAL_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在要求 4D-03AL resource skill coverage manifest 回连 4D-03AV `RESOURCE_SKILL_A_C_FAMILY`，保持该 family 为 `catalog-bound-representative`，并继续锁定 19 个 current `IsResourceSkill=true` representatives、prompt、command、`ABILITY_ACTIVATED` audit、no-mutation rollback 与 `[A]` / `[C]` official residuals。验证：focused 34/34、adjacent PaymentEngine / resource skill / prompt / hub regression 475/475、backend full 4471/4471 通过。本批不改 runtime / frontend / matrix，不关闭完整 `[A]` / `[C]` resource skill family、P0-005 或 READY。
2026-05-16 4D-03AY PaymentEngine keyword payment branch manifest verifier 补充：A 侧已完成 test-only branch-level verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AY_PAYMENT_ENGINE_KEYWORD_PAYMENT_BRANCH_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在锁定 4D-03AV `KEYWORD_PAYMENT_BRANCHES` residual family 的 8 个 executable entries：`HASTE_READY`、`ECHO_OPTIONAL_PAYMENT`、`SPELLSHIELD_TARGET_TAX`、`EXPERIENCE_PAYMENT`、`BATTLEFIELD_REPLACEMENT_COSTS`、`COST_MODIFIER_PAYMENTS`、`OPTIONAL_EXTRA_ALTERNATIVE_COSTS`、`TEMPORARY_RESOURCE_PARITY`；并要求每项保留 prompt、command、`COST_PAID` audit、no-mutation rollback、remaining official breadth 与 NOT READY closure。验证：focused 32/32、adjacent PaymentEngine / prompt / hub / keyword regression 590/590、backend full 4469/4469 通过。本批不改 runtime / frontend / matrix，不关闭完整 keyword payment branch family、all-window PaymentEngine parity、P0-005 或 READY。
2026-05-16 4D-03AX PaymentEngine legend / battlefield / trigger resource action manifest verifier 补充：A 侧已完成 test-only window-level verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AX_PAYMENT_ENGINE_LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTION_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在锁定 4D-03AV `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` residual family 的 3 个 executable entries：`LEGEND_ACT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`、`TRIGGER_PAYMENT`；并要求每项保留 prompt、command、audit、no-mutation rollback、remaining official breadth 与 NOT READY closure。验证：focused 27/27、adjacent LegendAct / BattlefieldHeld / TriggerPayment / PaymentEngine / prompt / hub regression 408/408、backend full 4464/4464 通过。本批不改 runtime / frontend / matrix，不关闭完整 legend / battlefield / trigger PaymentEngine family、P0-005 或 READY。
2026-05-16 4D-03AW PaymentEngine target / colored activated ability manifest verifier 补充：A 侧已完成 test-only catalog-bound verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AW_PAYMENT_ENGINE_TARGET_COLORED_ACTIVATED_ABILITY_MANIFEST_EVIDENCE.md`。`PaymentEngineCoverageAuditTests` 现在锁定当前 `P4ActivatedAbilityCatalog` 中非 resource-skill 且 target-bearing / typed-color / experience / Spellshield-tax 的 8 个 activated ability representatives：Xerath、Renata draw / score、Azir、Gatekeeper Maduli、Ezreal、Crimson Rose、Shadow；并要求每项保留 prompt、command、audit、no-mutation rollback、remaining official breadth 与 NOT READY closure。验证：focused 22/22、adjacent target / payment / prompt / hub regression 530/530、backend full 4459/4459、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭完整 activated ability family、P0-005 或 READY。
2026-05-16 4D-04Q-B LayerEngine static aura source lifecycle 补充：B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c` 已完成 P1-001 static aura source lifecycle foundation 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_EVIDENCE.md`。`ContinuousEffectState` / snapshot view 现在可暴露 `STATIC_AURA` foundation view 的 source/target、condition、lifecycle、participants metadata；Ornn dynamic friendly-equipment static recompute 与 battlefield `OGN·294/298` all-units +1 representative 均有 source lifecycle evidence，且 source/condition 失效后不保留 stale metadata。验证：focused static-aura / LayerEngine-view guard 11/11、adjacent static / continuous-effect / equipment regression 49/49、backend full 4451/4451、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、broader static aura/equipment breadth、P1-001、P1-002 或 READY。
2026-05-16 4D-04Q-B historical dispatch 补充：A 主控此前把 P1-001 static aura source lifecycle foundation 派发给 B-Implementation / Euclid `019e2caf-92c5-7502-8db3-b091e443ad3c`。该窄写锁只允许 B 在 Ornn dynamic static recompute、battlefield static power、`ContinuousEffectState` / snapshot helper 与 focused representative tests 范围内补 source / lifecycle audit foundation，并保持 existing arithmetic；禁止 frontend、card matrix、PaymentEngine、battle lifecycle/task queue、wide equipment runtime/full `百炼`、完整 LayerEngine/timestamp dependency graph、fullOfficial / READY 与 `riftbound-dotnet.sln`。该 dispatch 已被上一条 4D-04Q-B 验收补充关闭写锁；当前项目仍 **NOT READY**。
2026-05-16 4D-04Q LayerEngine static aura source lifecycle handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04Q_LAYERENGINE_STATIC_AURA_SOURCE_LIFECYCLE_BASELINE_EVIDENCE.md`。当前 Ornn dynamic friendly-equipment static recompute 与 battlefield static power representative 已有 arithmetic / payload evidence，但尚未把 dynamic static aura / equipment static source lifecycle 纳入稳定服务端 continuous-effect 或等价审计视图。Baseline：focused static-aura / LayerEngine-view guard 10/10、adjacent static / continuous-effect / equipment regression 48/48、backend full 4450/4450 通过。该 handoff 不实现 runtime、不派发 B、不打开写锁、不关闭完整 LayerEngine、P1-001、P1-002 或 READY。
2026-05-16 4D-04P-B LayerEngine minimum-power ordering 补充：B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e` 已完成 P1-001 minimum-power ordering representative 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_EVIDENCE.md`。本批未改 runtime，只新增同目标 Smoke Bomb floor -> Extortion zero-applied floor -> Power Bind +1 fixture/test；state ledger、`ContinuousEffectState` 与 snapshot view 的 requested/applied/minimum/resulting/base/effective/order metadata 一致，zero-applied floor 不生成 misleading ledger 或消耗 visible order。验证：focused minimum-power ordering guard 8/8、adjacent minimum / ordering / continuous-effect regression 16/16、backend full 4450/4450、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering beyond this representative、P1-001、P1-002 或 READY。
2026-05-16 4D-04P-B dispatch 补充：A 主控已把 P1-001 LayerEngine minimum-power ordering 交给 B-Implementation / Carson `019e2c9e-1e05-7130-94de-83a9ef0c982e`。该窄写锁只允许 B 补同目标 minimum floor 与 applied order 的组合 representative，并保持现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic、requested/applied/minimum/resulting metadata、direct-path metadata、legacy fallback 与 `END_TURN` cleanup；禁止 broad LayerEngine rewrite、frontend、card matrix、PaymentEngine、battle lifecycle、wide equipment runtime/static aura rewrite、fullOfficial / READY 与 `riftbound-dotnet.sln`。A 等待 B diff 后再更新 4D-04P audit/evidence；当前项目仍 **NOT READY**。
2026-05-16 4D-04P LayerEngine minimum-power ordering handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_BASELINE_EVIDENCE.md`。当前 4D-04M minimum-power metadata 与 4D-04O applied-order metadata 已分别成立，但尚无同目标 sequence representative 同时覆盖 minimum floor interaction、requested/applied/minimum/resulting metadata 与 `appliedOrder`。Baseline：focused minimum-power ordering guard 7/7、adjacent minimum / ordering / continuous-effect regression 15/15、backend full 4449/4449 通过。该 handoff 不实现 runtime、不派发 B、不打开写锁、不关闭完整 LayerEngine、complete minimum-power ordering、P1-001、P1-002 或 READY。
2026-05-16 4D-04O-B LayerEngine power modifier ordering 补充：B-Implementation / Leibniz `019e2c86-8abd-74c3-8c3d-3e8ccd5453ab` 已完成 P1-001 explicit ordering metadata foundation 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_EVIDENCE.md`。`PowerModifierLedgerEntry`、`ContinuousEffectState` 与 snapshot view 现在暴露 nullable `AppliedOrder` / `appliedOrder`；ordered ledger entries 按该字段投影，legacy untracked remainder 不暴露 order。验证：focused ordering guard 6/6、adjacent LayerEngine / power metadata regression 39/39、backend full 4449/4449、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering、P1-001、P1-002 或 READY。
2026-05-16 4D-04O-B dispatch 补充：A 主控已把 LayerEngine power modifier ordering metadata 派发给 B-Implementation。该窄写锁只允许 B 为 ledger-backed until-end power modifier 暴露稳定 application order metadata，并保持现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic、minimum floor metadata、direct-path metadata、legacy untracked remainder fallback 与 `END_TURN` cleanup；禁止 broad LayerEngine rewrite、frontend、card matrix、PaymentEngine、battle lifecycle、wide equipment runtime、fullOfficial / READY 与 `riftbound-dotnet.sln`。A 等待 B diff 后再更新 4D-04O audit/evidence；当前项目仍 **NOT READY**。
2026-05-16 4D-04O LayerEngine power modifier ordering handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04O_LAYERENGINE_POWER_MODIFIER_ORDERING_BASELINE_EVIDENCE.md`。当前 ledger-backed power modifier view 已有 source / effect / direct-path / requested / applied / minimum / resulting metadata，但 `PowerModifierLedgerEntry` 仍无显式 application order / timestamp 字段，且 `CardObjectState.NormalizePowerModifierLedger` / continuous effect projection 仍按 `EffectId` 排序。下一建议 B 只补稳定 ordering metadata，不改现有 arithmetic、不重写完整 LayerEngine。Baseline：focused ordering guard 6/6、adjacent LayerEngine / power metadata regression 37/37、backend full 4447/4447 通过。该 handoff 本身不关闭 P1-001、P1-002 或 READY。
2026-05-16 4D-04N-B LayerEngine direct power ledger 补充：B-Implementation / Godel `019e2c69-aa6d-7701-9525-6a79a50fa210` 已完成 P1-001 direct until-end power mutation ledger metadata foundation 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_EVIDENCE.md`。`CoreRuleEngine` 新增 `ApplyDirectUntilEndPowerModifier`，selected direct paths 只在 applied delta 非零时追加 `PowerModifierLedgerEntry`，并通过现有 `ContinuousEffectState` / snapshot view 暴露 source/effect/direct-path requested/applied/minimum/resulting metadata；验证：focused direct-power guard 6/6、adjacent power/layer/trigger regression 185/185、backend full 4447/4447、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering、unselected direct/static/replacement breadth、P1-001、P1-002 或 READY。
2026-05-16 4D-04N LayerEngine direct power ledger handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04N_LAYERENGINE_DIRECT_POWER_LEDGER_BASELINE_EVIDENCE.md`。该 handoff 不要求立刻重写完整 LayerEngine，只要求后续 B 为 direct until-end power mutation representatives 追加或暴露 source/effect/direct-path ledger metadata，并保持现有 `Power` / `UntilEndOfTurnPowerModifier` arithmetic、turn-end cleanup 与 snapshot compatibility。Baseline：focused direct-power guard 6/6、adjacent power/layer/trigger regression 185/185、backend full 4447/4447、`git diff --check` 通过。该 handoff 本身不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04M-B LayerEngine minimum-power ledger 补充：B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 已完成 P1-001 minimum-power ledger metadata foundation 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_EVIDENCE.md`。`PowerModifierLedgerEntry.PowerDelta` 继续表示 applied delta，并新增 requested/minimum/resulting metadata；`CoreRuleEngine.ApplyPowerModifier` 对非零 applied delta 追加 ledger，对 Extortion applied-zero floor 不生成 misleading zero ledger；`ContinuousEffectState` / snapshot view 暴露 requested/applied/minimum/resulting。验证：focused minimum-power foundation guard 9/9、adjacent power/layer/minimum regression 16/16、backend full 4447/4447、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering、P1-001、P1-002 或 READY。
2026-05-15 4D-04M LayerEngine minimum-power ledger handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04M_LAYERENGINE_MINIMUM_POWER_LEDGER_BASELINE_EVIDENCE.md`。该 handoff 不要求立刻重写完整 LayerEngine，只要求后续 B 为 `MinimumPowerAfterModifier > 0` 的现有 power modifier representatives 显式保留 requested delta、applied delta、minimum floor 与 resulting power metadata，并继续明确 timestamp、dependency、source ordering、keyword gain/loss、multiple equipment/static aura、complete minimum-power ordering 与 full official coverage deferred。Baseline：focused minimum-power foundation guard 9/9、adjacent power/layer/minimum regression 16/16、`git diff --check` 通过。该 handoff 本身不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04L-B LayerEngine foundation 补充：B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 已完成 P1-001 source-aware power modifier ledger foundation 并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_EVIDENCE.md`。`CoreRuleEngine.ApplyPowerModifier` 现在在保持现有 `Power` / `UntilEndOfTurnPowerModifier` 算术的同时追加 `PowerModifierLedgerEntry`，`MatchSession` 的 continuous effect view 对 ledger-backed power modifier 暴露 source/effect metadata 和 `FOUNDATION_ONLY` residuals；验证：focused LayerEngine foundation guard 11/11、adjacent power/layer/equipment regression 141/141、backend full 4447/4447、`git diff --check` 通过。本批不关闭完整 LayerEngine、timestamp/dependency/source ordering、keyword gain/loss、multiple equipment/static aura、minimum-power layering、P1-001、P1-002 或 READY。
2026-05-15 4D-04L LayerEngine foundation handoff / baseline 补充：A 主控已建立 P1-001 下一切片 handoff，入口为 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04L_LAYERENGINE_FOUNDATION_BASELINE_EVIDENCE.md`。该 handoff 不要求立刻重写完整 LayerEngine，只要求后续 B 为现有 until-end power modifier representatives 建立 source-aware / effect-aware ledger 或 verifier foundation，并继续明确 timestamp、dependency、source ordering、keyword gain/loss、multiple equipment/static aura、minimum-power layering 与 full official coverage deferred。Baseline：focused LayerEngine guard 11/11、adjacent power/layer/equipment regression 141/141 通过。该 handoff 本身不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04K-B Equipment state profile alignment / verifier 补充：B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 已完成并由 A 验收，审计与证据见 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_EVIDENCE.md`。`CardEquipmentKeywordRules` 现在显式记录 Long Sword P5 equipment state representative manifest，并把 owner/controller/attachment invariant、controller mismatch no-mutation、controlled opponent-owned target attach、attached equipment follows host base <-> battlefield、host destroyed detach / recall 等现有 verifier anchors 接入 profile 口径；`CardCatalogBaselineTests` 通过反射确认这些 anchors 仍存在。验证：focused state/profile guard 12/12、adjacent equipment regression 195/195、`git diff --check` 通过。本批不改 runtime / frontend / matrix，不关闭 P1-001、P1-002、full official 或 READY。
2026-05-15 4D-04K-B dispatch 补充：A 主控已将 Equipment state profile alignment / verifier 派发给 B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2`。本轮只允许 profile / verifier 层承认已有 P5 equipment state representatives，默认写入 `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 与 `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`，可选最小触碰 fixture manifest / reporter；禁止 runtime semantics、frontend、card matrix、fullOfficial / READY 与 `riftbound-dotnet.sln`。本 dispatch 不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04K Equipment state profile alignment handoff / baseline 补充：A 主控已建立下一枚 B handoff，入口为 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_BASELINE_EVIDENCE.md`。该 handoff 不要求新增 runtime，只要求后续 B 在 profile / verifier 层承认现有 P5 Long Sword owner/controller attach invariant、controller mismatch rejection、controlled opponent-owned target attach、explicit attached equipment follows host、host destroyed detach / recall representatives，并继续保留 full owner/controller breadth、full attach lifecycle、Agile reaction timing、Jax-granted Agile、full Tempered、copy-text、其他 static modifiers、LayerEngine 和 full equipment official coverage deferred。Baseline：focused state / profile guard 11/11、adjacent equipment regression 195/195 通过。本批不改 runtime / tests / frontend / matrix，不派发 B，不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04J Equipment remaining breadth refresh / handoff 补充：A 主控已建立 equipment residual 刷新与下一切片路由，入口为 `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04J_EQUIPMENT_REMAINING_BREADTH_REFRESH_BASELINE_EVIDENCE.md`。当前已有 P5 representative anchors 覆盖 Long Sword owner/controller attach invariant、controller mismatch rejection、controlled opponent-owned target attach、显式 owner/controller 匹配时贴附装备随宿主 base <-> battlefield 移动、宿主 destroyed 时装备 detach / recall；focused state / keyword guard 11/11 通过。下一建议 4D-04K 应做 profile / verifier alignment，承认这些 representative boundaries，同时继续保留 full owner/controller breadth、full attach lifecycle、Agile reaction timing、Jax-granted Agile、full Tempered、copy-text、其他 static modifiers、LayerEngine 和 full equipment official coverage deferred。本批不改 runtime / tests / frontend / matrix，不派发 B，不关闭 P1-001、P1-002 或 READY。
2026-05-15 4D-04I-B Ornn dynamic equipment static recompute 补充：B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 已完成 P1-001 / P1-002 窄 representative，审计与证据见 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_EVIDENCE.md`。`CoreRuleEngine` 现在会在 accepted core command 后，对已在公开 field 且 registry 标记 `AddsFriendlyFieldEquipmentCountToSourceUnitPower` 的 Ornn，从 registered source unit power + 当前 controller 友方公开 field equipment count + until-end power modifier 重算 `Power`，并在 state 改变时重建 authoritative snapshots / prompts；后续装备进入/离开、hand/enemy/face-down/dirty-controller/non-equipment exclusion 与重复命令不漂移已有 focused tests。验证：focused / keyword / LayerEngine-view guard 9/9、adjacent equipment / payment regression 117/117、backend full 4446/4446、`git diff --check` 通过。本批不关闭完整 LayerEngine、full `百炼` breadth、其他装备静态修正、owner/controller breadth、attach lifecycle breadth、P1-001、P1-002 或 READY。
2026-05-15 4D-04I Ornn dynamic equipment static recompute handoff / baseline 补充：A 主控已建立下一枚 P1-001 / P1-002 交接，入口为 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04I_ORNN_DYNAMIC_EQUIPMENT_STATIC_RECOMPUTE_BASELINE_EVIDENCE.md`。该 handoff 锁定 4D-04H 后的剩余缺口：Ornn 已在公开 field 后，友方公开场上装备数量变化时必须从 base power + 当前装备数重算，避免 entry-time bonus double-count 或 stale power。baseline：focused / keyword / LayerEngine-view guard 6/6、adjacent equipment / payment regression 114/114、`git diff --check` 通过。本批不改 runtime / tests / frontend / matrix，不关闭完整 LayerEngine、P1-001、P1-002 或 READY。
2026-05-15 4D-04H Ornn friendly equipment static power 补充：A 侧已完成 P1-001 / P1-002 窄 representative，审计与证据见 `docs/CURRENT_STAGE4D_04H_ORNN_FRIENDLY_EQUIPMENT_STATIC_POWER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04H_ORNN_FRIENDLY_EQUIPMENT_STATIC_POWER_EVIDENCE.md`。`CardBehaviorRegistry` / `CoreRuleEngine` / `CardEquipmentKeywordRules` 现在让 `SFD·085/221` / `SFD·085a/221`《奥恩》从手牌 `PLAY_CARD` 入场时，按 controller 友方公开 field equipment 数量增加入场战力，并在非零加成时写 `friendlyEquipmentPowerBonus` event payload；手牌、face-down、敌方、脏 controller 与非装备对象不计入。验证：focused / keyword guard 5/5、adjacent equipment / payment regression 114/114、backend full 4443/4443、`git diff --check` 通过。本批不关闭 full `百炼` breadth、其他装备静态修正、dynamic static recompute、LayerEngine、owner/controller breadth、attach lifecycle breadth、P1-001、P1-002 或 READY。
2026-05-15 4D-04G Armed Assaulter HASTE_READY + Tempered optional attach 补充：B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成 P1-002 Armed Assaulter same-command optional-cost combination representative，审计与证据见 `docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_EVIDENCE.md`。`CoreRuleEngine` / `CardEquipmentKeywordRules` 现在让 `SFD·002/221`《武装强袭者》从手牌 `PLAY_CARD` 时可同时公开并校验 `HASTE_READY` 与己方已在场 `SFD·186/221`《旋转飞斧》的 `TEMPERED_ATTACH:<equipmentObjectId>` optional cost choice；命令支付 base 6 mana + 1 haste mana + 1 red power，结算后 Armed Assaulter active 入 P1 base 并在装备仍合法时贴附。验证：focused / keyword guard 26/26、adjacent equipment / payment regression 235/235、backend full 4440/4440、`git diff --check` 通过。本批不关闭 full `百炼` breadth、full Haste breadth、Ornn static modifiers、owner/controller breadth、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04F Akshan orange extra equipment steal 补充：B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成 P1-002 Akshan orange extra equipment steal representative，审计与证据见 `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_EVIDENCE.md`。`CoreRuleEngine` / `MatchSession` 现在让 `SFD·109/221`《阿克尚》从手牌 `PLAY_CARD` 时公开并校验合法敌方在场装备的 `AKSHAN_STEAL_EQUIPMENT:<equipmentObjectId>` optional cost choice；命令支付 2 orange power，支持必要 orange `RECYCLE_RUNE:*` payment-resource；结算后 Akshan 入基地，装备移动/控制到 P1，武装贴附，Akshan 离场时归还 owner base。验证：focused / keyword guard 28/28、adjacent equipment / payment regression 209/209、backend full 4417/4417、`git diff --check` 通过。本批不关闭 full `百炼` breadth、Ornn / Armed Assaulter special branches、owner/controller breadth、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04E Jax Tempered optional attach trigger integration 补充：B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成 P1-002 Jax `百炼` optional attach trigger representative，审计与证据见 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_EVIDENCE.md`。`CoreRuleEngine` / `MatchSession` 现在让 `SFD·119/221` / `SFD·119a/221` Jax 从手牌 `PLAY_CARD` 时公开并校验己方已在场 `SFD·186/221`《旋转飞斧》的 `TEMPERED_ATTACH:<equipmentObjectId>` optional cost choice；结算后设置 `AttachedToObjectId` 并发出 `EQUIPMENT_ATTACHED` / `TEMPERED_OPTIONAL_ATTACH`，再通过 `StackResolutionResult.PendingPayment` 窄承载复用既有 `JAX_WEAPON_ATTACH_PAY_1_DRAW_1` trigger payment window。验证：focused / keyword guard 41/41、adjacent equipment / payment regression 243/243、backend full 4397/4397、`git diff --check` 通过。本批不关闭 full `百炼` breadth、Ornn / Akshan / Armed Assaulter special branches、owner/controller changes、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04D Tempered optional attach 补充：B-Implementation / Bacon `019e2ba3-4b9a-7710-a702-1e8e28ecd8ea` 已完成 P1-002 `百炼` optional attach representative，审计与证据见 `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_EVIDENCE.md`。`CoreRuleEngine` / `MatchSession` 现在让 `SFD·008/221`《哨兵好手》从手牌 `PLAY_CARD` 时公开并校验己方已在场 `SFD·186/221`《旋转飞斧》的 `TEMPERED_ATTACH:<equipmentObjectId>` optional cost choice；结算后设置 `AttachedToObjectId` 并发出 `EQUIPMENT_ATTACHED` / `TEMPERED_OPTIONAL_ATTACH`；`CardEquipmentKeywordRules` 标记 Tempered optional attach representative while preserving deferred official breadth。验证：focused / keyword guard 14/14、adjacent equipment regression 139/139、backend full 4380/4380、`git diff --check` 通过。本批不关闭 full `百炼` breadth、Jax trigger integration、Ornn / Akshan / Armed Assaulter special branches、owner/controller changes、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04C Agile equipment direct attach 补充：B-Implementation / Singer `019e2b7e-8eed-7803-b03a-ab9bf538171c` 已完成 P1-002 printed Agile equipment direct-play attach representative，审计与证据见 `docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04C_AGILE_EQUIPMENT_DIRECT_ATTACH_EVIDENCE.md`。`CoreRuleEngine` / `MatchSession` 现在让 `SFD·022/221`、`SFD·056/221`、`SFD·064/221`、`SFD·186/221` 从手牌 `PLAY_CARD` 时公开并校验己方单位目标，结算后设置 `AttachedToObjectId` 并发出 `EQUIPMENT_ATTACHED`；`CardEquipmentKeywordRules` 标记 Agile direct-play representative while preserving deferred official breadth。验证：focused 57/57、rejected/shape 113/113、adjacent 207/207、keyword guard 17/17、historical recheck 6/6、backend full 4368/4368、`git diff --check` 通过。本批不关闭 reaction timing、Jax-granted Agile、Tempered、weapon/static modifiers、copy-text effects、owner/controller changes、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04B Equipment keyword status split 补充：B-Implementation / Confucius `019e2b70-60f0-7a50-9a95-dd497c62ff96` 已完成 P1-002 equipment keyword execution-boundary status split，审计与证据见 `docs/CURRENT_STAGE4D_04B_EQUIPMENT_KEYWORD_STATUS_SPLIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_04B_EQUIPMENT_KEYWORD_STATUS_SPLIT_EVIDENCE.md`。`CardEquipmentKeywordRules` 现在用 `implemented-representative` 和 `HasImplementedRepresentativeAssembleBoundary` 区分已登记的 assemble representative boundary；`MatchSession` 只读暴露 implemented assemble profile query；`CardCatalogBaselineTests` 锁住 equipment implemented/deferred 并存状态。验证：focused 4/4、adjacent equipment 98/98、broader keyword 8/8、backend full 4355/4355、`git diff --check` 通过。本批不关闭 agile reaction attachment、tempered optional attachment、weapon/static modifiers、copy-text effects、owner/controller changes、attach lifecycle breadth、P1-002、LayerEngine 或 READY。
2026-05-15 4D-04A Keyword deferred surface handoff / baseline 补充：P1-002 keyword execution-boundary 下一批已收窄，审计、交接和基线见 `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_AUDIT.md`、`docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_04A_KEYWORD_DEFERRED_SURFACE_BASELINE_EVIDENCE.md`。A 侧确认当前 keyword catalog/profile baseline 8/8 通过，representative keyword fixture baseline 144/144 通过；下一建议 B 切片为 equipment keyword execution-boundary status split，使已存在的 assemble / Take Up representative execution boundaries 与仍 deferred 的 agile / tempered / static modifier / owner-controller / full official breadth 区分开。本批不改 runtime / tests / frontend / matrix，不派发 B，不关闭 P1-002、LayerEngine、frontend final validation 或 READY。
2026-05-15 4D-03AS focused slice 补充：Azir optional armament reattach representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_EVIDENCE.md`；focused 204/204、adjacent 397/397、backend full 4355/4355、`git diff --check` 通过。服务端现在用 `AZIR_REATTACH_ARMAMENT:*` optional token 和 stack-time revalidation 补齐阿兹尔 official optional armament branch，合法时 emits `EQUIPMENT_REATTACHED`，stale selected armament no-effect；本切片不关闭 full official P0-005、swift timing breadth、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AS dispatch 补充：用户恢复 active goal 后，4D-03AS-B 已派发给 B-Implementation / Raman `019e2b49-28c3-7ad2-b3f8-ef1347b56996`。B 派发期间独占 Azir optional armament reattach runtime/test 写锁；A 已用 handoff acceptance gate 验收。该 dispatch 不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AS handoff / baseline 补充：Azir optional armament reattach 下一 follow-up 切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AS_AZIR_OPTIONAL_ARMAMENT_REATTACH_BASELINE_EVIDENCE.md`，matrix readiness 见 `docs/CURRENT_STAGE4D_03AS_CARD_MATRIX_READINESS_AUDIT.md`；baseline 194/194 与 387/387 通过，`git diff --check` 通过。该 handoff 只锁定 `SFD·050/221` / `SFD·050a/221` 阿兹尔 optional armament reattach branch，要求 future implementation 复用现有 equipment attachment / reattach 状态与事件语义；按用户指令，本批不派发实现 worker，不改 runtime / tests / frontend / matrix，不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AR focused slice 补充：Gatekeeper Maduli cannot-ready static representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_EVIDENCE.md`；focused 65/65、adjacent 375/375、backend full 4345/4345、`git diff --check` 通过。服务端现在用 official `UNL-144/219` policy 过滤 / 拒绝 / 跳过 ready Maduli 路径，Crimson Rose 与 Hunt representatives 均被测试锁住，Maduli prompt metadata 标记 cannot-ready static implemented；本切片不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AR handoff / baseline 补充：Gatekeeper Maduli cannot-ready static 下一切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_BASELINE_EVIDENCE.md`；baseline 61/61 与 371/371 通过，`git diff --check` 通过。该 handoff 只锁定 `UNL-144/219` 守门者马杜里 `我无法变为活跃状态。` static representative，要求 server-authoritative ready prompt / targeted ready / mass ready 路径均不得使 Maduli 变为 active；本批不实现 runtime，不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AQ focused verifier 补充：HASTE_READY coverage verifier 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_EVIDENCE.md`；focused 105/105、adjacent 445/445、backend full 4341/4341、`git diff --check` 通过。`PaymentEngineCoverageAuditTests` 现在将当前 implemented HASTE_READY 34 个 registry/profile entries 绑定到 expected official `RuneTrait`、1 mana + 1 typed power metadata、existing P4 fixture anchors 与 representative `NOT READY` closure；本切片不改 runtime，不关闭 full official Haste、strong/overflow、non-hand haste granting、LayerEngine、full official P0-005、P1、frontend final validation 或 READY。
2026-05-15 4D-03AQ handoff / baseline 补充：HASTE_READY coverage verifier 下一 P0-005 test-only 切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AQ_PAYMENT_ENGINE_HASTE_READY_COVERAGE_VERIFIER_BASELINE_EVIDENCE.md`；baseline 102/102 与 442/442 通过，`git diff --check` 通过。该 handoff 只锁定 implemented HASTE_READY registry/profile set 与 P4 fixture evidence 的 catalog-bound verifier，不实现 runtime，不关闭 full official Haste、full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AP focused guard 补充：Rek'Sai HASTE_READY red exactness representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_EVIDENCE.md`；focused 17/17、handoff focused 126/126、adjacent 442/442、backend full 4338/4338、`git diff --check` 通过。服务端现有 `PLAY_CARD` prompt / command / shared `PaymentCostRules` audit 现在被 tests 锁住：`SFD·029/221` / `SFD·029a/221` 雷克塞 HASTE_READY 需要 extra 1 mana + 1 red typed power，支持必要 red `RECYCLE_RUNE:*`，wrong trait / generic temporary resource / invalid recycle / target bypass 等拒绝 no-mutation。Strong / Overwhelm battle modifier、ASSIGN_COMBAT_DAMAGE overflow、non-hand friendly unit gains haste、LayerEngine 与 FAQ breadth 仍 deferred；本切片不关闭 full official P0-005、P1、frontend final validation 或 READY。
2026-05-15 4D-03AP handoff / baseline 补充：Rek'Sai HASTE_READY red exactness 下一 P0-005 切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AP_PAYMENT_ENGINE_REKSAI_HASTE_READY_RED_EXACTNESS_BASELINE_EVIDENCE.md`；baseline 109/109 与 425/425 通过，`git diff --check` 通过。该 handoff 只锁定 `SFD·029/221` / `SFD·029a/221` 雷克塞 HASTE_READY extra 1 mana + 1 red typed power representative，不实现 runtime，不关闭 strong / Overwhelm battle modifier、ASSIGN_COMBAT_DAMAGE overflow、non-hand friendly unit gains haste、full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AP card matrix readiness 补充：`docs/CURRENT_STAGE4D_03AP_CARD_MATRIX_READINESS_AUDIT.md` 确认 Rek'Sai `FU-1945f6918c` 当前矩阵仍为 `fullOfficial=false`，4C-52 ordinary no-optional evidence 与 old P4 HASTE_READY fixtures 不能代理 red exactness、strong/overflow、non-hand haste granting、LayerEngine 或 FAQ breadth；本批不修改 matrix JSON。
2026-05-15 4D-03AO focused slice 补充：Ezreal blue swift move-to-base representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_EVIDENCE.md`；focused 28/28、handoff focused 207/207、adjacent 400/400、backend full 4321/4321、`git diff --check` 通过。服务端现在为 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔 blue-pay swift self move-to-base representative 提供 catalog aliases、ActionPrompt metadata、shared typed-blue PaymentEngine commit、no-target command guard、server-side `UNIT_MOVED_TO_BASE` event 和 stale no-effect resolution。Attack / defense trigger、cannot-combat-damage static 与 full swift timing 仍 deferred；本切片不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AO handoff / baseline 补充：Ezreal blue swift move-to-base 下一 P0-005 切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_BASELINE_EVIDENCE.md`；baseline 179/179 与 372/372 通过，`git diff --check` 通过。该 handoff 只锁定 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 伊泽瑞尔 no-target blue swift self move-to-base representative，不实现 runtime，不关闭 attack / defense trigger、cannot-combat-damage static、full swift timing、full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AO card matrix readiness 补充：`docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md` 确认 Ezreal `FU-2dca1ad450` 当前矩阵仍为 `fullOfficial=false`，4C-49 ordinary play-unit evidence 不能代理 official blue swift move-to-base、damage trigger、combat-damage prevention static、movement / timing / FAQ breadth；本批不修改 matrix JSON。
2026-05-15 4D-03AN focused slice 补充：Gatekeeper Maduli purple move representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md`；focused 25/25、handoff focused 188/188、adjacent 381/381、backend full 4293/4293、`git diff --check` 通过。服务端现在为 `UNL-144/219` 守门者马杜里 purple-pay enemy-controlled weaker-battlefield movement representative 提供 catalog ability、ActionPrompt metadata、shared typed-purple PaymentEngine commit、target guards、server-side movement event 和 stale no-effect resolution。“我无法变为活跃状态”静态仍 deferred；本切片不关闭 full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AN handoff / baseline 补充：Gatekeeper Maduli purple move 下一 P0-005 切片已建立，交接规格见 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_BASELINE_EVIDENCE.md`；baseline 163/163 与 356/356 通过，`git diff --check` 通过。该 handoff 只锁定 `UNL-144/219` 守门者马杜里 target-bearing purple movement representative，不实现 runtime，不关闭 Maduli cannot-ready static text、full official P0-005、LayerEngine、P1、frontend final validation 或 READY。
2026-05-15 4D-03AM focused slice 补充：Azir swift swap representative 已验收，审计与证据见 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_EVIDENCE.md`；focused 23/23、handoff focused 191/191、adjacent 384/384、backend full 4268/4268、`git diff --check` 通过。服务端现在为 `SFD·050/221` / `SFD·050a/221` 阿兹尔 green swift controlled-unit position-swap representative 提供 catalog alias、ActionPrompt metadata、shared typed-green PaymentEngine commit、target guards、server-side location swap event 和 once-per-turn marker。可选 armament reattach 分支仍 deferred；本切片不关闭 full official P0-005、完整 swift timing、P1、frontend final validation 或 READY。
上一版 Stage 4D PaymentEngine 总览（已由 4D-03AQ focused verifier 与上方补充 supersede）：PaymentEngine 总览已对齐到 4D-03AH，刷新审计见 `docs/CURRENT_STAGE4D_03AI_PAYMENT_ENGINE_STATUS_REFRESH_AUDIT.md`。4D-03AJ 已补 Renata draw / score typed-blue `ACTIVATE_ABILITY` branches 的 matching blue temporary resource quote / command commit / audit metadata，审计与证据见 `docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AJ_PAYMENT_ENGINE_RENATA_TYPED_TEMP_RESOURCE_EVIDENCE.md`；focused 85/85、adjacent 687/687、backend full 4239/4239 通过。4D-03AK 已补 activated ability Spellshield tax catalog-bound coverage verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AK_PAYMENT_ENGINE_SPELLSHIELD_TAX_COVERAGE_EVIDENCE.md`；focused 8/8、adjacent 382/382、backend full 4242/4242 通过。4D-03AL 已补 resource skill catalog-bound coverage verifier，审计与证据见 `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_EVIDENCE.md`；focused 11/11、adjacent 452/452、backend full 4245/4245 通过。4D-03AM 已建立 Azir swift swap handoff / baseline，交接规格见 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_HANDOFF.md`，实现前证据见 `docs/CURRENT_STAGE4D_03AM_PAYMENT_ENGINE_AZIR_SWIFT_SWAP_BASELINE_EVIDENCE.md`；baseline 361/361 与 449/449 通过。4D-03 foundation through 4D-03W、4D-03AC / 03AD / 03AE / 03AG / 03AJ focused representatives、4D-03AK / 03AL verifiers 与 4D-03AF / 03AH audit / verifier 已验收或建立；4D-03X / 03Y / 03Z / 03AA / 03AB 是 catalog / token representative 收口，不等价于 P0-005 closure。项目仍 **NOT READY**；完整 `[A]` / `[C]` resource skill family、remaining payment windows、target-bearing colored-cost activated abilities、keyword payment branches、replacement / optional / alternative / tax quote-command-audit parity、LayerEngine、前端最终验收与 full-card matrix 仍未关闭。
上一版 Stage 4D 状态（已由上方 2026-05-15 refresh supersede）：4D-03 PaymentEngine focused foundation、4D-03B non-play、4D-03C play optional / extra、4D-03D `ACTIVATE_ABILITY` payment resource、4D-03E `HIDE_CARD` payment、4D-03F pending `PAY_COST` resource、4D-03G battlefield held score resource、4D-03H trigger payment resource、4D-03I Malzahar resource skill、4D-03J Malzahar lifecycle、4D-03K temporary inline、4D-03L Dragon Soul Sage reaction resource skill、4D-03M Renata colored activated draw、4D-03N Renata colored activated score 与 4D-03O Crimson Rose ready-unit focused slices 均已验收；4D-03P Fluft Poro Warhawk token focused slice 已验收；4D-03Q Shadow swift stun focused slice 已验收；项目仍 **NOT READY**。4D-03Q 审计 / 证据入口：`docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`；focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过。4D-03Q 交接 / 实现前基线入口（已由 focused slice supersede，保留为回归护栏）：`docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`；focused baseline 198/198、adjacent baseline 738/738 通过。4D-03P 审计 / 证据入口：`docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过。4D-03O 审计 / 证据入口：`docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`；focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过。4D-03O 交接 / 实现前基线入口（已由 focused slice supersede，保留为回归护栏）：`docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`；focused baseline 143/143、adjacent baseline 370/370 通过。4D-03N 审计 / 证据入口：`docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。4D-03M 审计 / 证据入口：`docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md`；focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过。4D-03N 交接 / 实现前基线入口（已由 focused slice supersede，保留为回归护栏）：`docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`；focused baseline 163/163、adjacent baseline 335/335 通过。上一 Stage 4C checkpoint：`7a2b1fa3 checkpoint: record stage 4C battlefield residual evidence alignment`。上一 active guard checkpoint：`4c06189 checkpoint: add active start battle guard tests`。上一 formal 18-step checkpoint：`3aed179 checkpoint: add formal 18 step e2e evidence`。

2026-05-15 4D-02AK focused slice 补充：Battle response activation power-modifier assignment damage pool 已验收，入口为 `docs/CURRENT_STAGE4D_02AK_BATTLE_RESPONSE_ACTIVATION_POWER_MODIFIER_ASSIGNMENT_DAMAGE_POOL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AK_BATTLE_RESPONSE_ACTIVATION_POWER_MODIFIER_ASSIGNMENT_DAMAGE_POOL_EVIDENCE.md`；targeted 1/1、focused 294/294、adjacent 824/824、backend full 4236/4236、`git diff --check` 通过。服务端现在把非眩晕 modified participant 的 assignment damage pool、lethal threshold 与 participant power 统一为 current effective `Power`，并拒绝旧 double-count assignment shape；合法 effective-power assignment 关闭当前 battle 后才推进下一 contested battlefield。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AK handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned power-modifier assignment damage pool parity，入口为 `docs/CURRENT_STAGE4D_02AK_BATTLE_RESPONSE_ACTIVATION_POWER_MODIFIER_ASSIGNMENT_DAMAGE_POOL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AK_BATTLE_RESPONSE_ACTIVATION_POWER_MODIFIER_ASSIGNMENT_DAMAGE_POOL_BASELINE_EVIDENCE.md`；targeted existing prerequisites 3/3、focused baseline 293/293、adjacent baseline 823/823、backend full 4235/4235、`git diff --check` 通过。该 handoff 只建立非眩晕 modified participant 使用 effective `Power` 一次的 assignment prompt / runtime validation / committed damage pool 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AJ focused slice 补充：Battle response activation stunned assignment damage pool 已验收，入口为 `docs/CURRENT_STAGE4D_02AJ_BATTLE_RESPONSE_ACTIVATION_STUNNED_ASSIGNMENT_DAMAGE_POOL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AJ_BATTLE_RESPONSE_ACTIVATION_STUNNED_ASSIGNMENT_DAMAGE_POOL_EVIDENCE.md`；targeted 1/1、focused 293/293、adjacent 823/823、backend full 4235/4235、`git diff --check` 通过。服务端现在把 Shadow stun 后 returned assignment prompt / runtime validation 的 stunned attacker damage pool、lethal threshold 与 participant power 统一为 0，并拒绝旧 attacker-nonzero assignment；合法 zero-attacker assignment 关闭当前 battle 后才推进下一 contested battlefield。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AJ handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned stunned assignment damage pool，入口为 `docs/CURRENT_STAGE4D_02AJ_BATTLE_RESPONSE_ACTIVATION_STUNNED_ASSIGNMENT_DAMAGE_POOL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AJ_BATTLE_RESPONSE_ACTIVATION_STUNNED_ASSIGNMENT_DAMAGE_POOL_BASELINE_EVIDENCE.md`；targeted existing prerequisites 3/3、focused baseline 292/292、adjacent baseline 822/822、backend full 4234/4234、`git diff --check` 通过。该 handoff 只建立 Shadow stun 后 returned assignment prompt / runtime validation 使用 attacker damage pool 0、拒绝旧 attacker-nonzero assignment、合法 zero-attacker assignment 后推进 BF-B 的实现前护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AI focused slice 补充：Battle response activation held-score prevention next-contest advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02AI_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PREVENTION_NEXT_CONTEST_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AI_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PREVENTION_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 292/292、adjacent 822/822、backend full 4234/4234、`git diff --check` 通过。服务端现有 runtime 已由 test-only guard 证明：BF-A/B 双争夺场景中 actual Shadow activation / stack resolution / returned response 后，BF-A held-score `BATTLEFIELD_SCORE_PREVENTED` / battle close 完成后才推进 BF-B `START_SPELL_DUEL`，且不支付 held-score cost、不获得分数。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AI handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned held-score prevention next-contest advancement，入口为 `docs/CURRENT_STAGE4D_02AI_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PREVENTION_NEXT_CONTEST_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AI_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PREVENTION_NEXT_CONTEST_ADVANCEMENT_BASELINE_EVIDENCE.md`；targeted existing cross-product prerequisites 4/4、focused baseline 291/291、adjacent baseline 821/821、backend full 4233/4233、`git diff --check` 通过。该基线已被上方 focused slice 验收 supersede，仍保留为回归护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AH focused slice 补充：Battle response activation Brush next-contest advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02AH_BATTLE_RESPONSE_ACTIVATION_BRUSH_NEXT_CONTEST_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AH_BATTLE_RESPONSE_ACTIVATION_BRUSH_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 291/291、adjacent 821/821、backend full 4233/4233、`git diff --check` 通过。服务端现有 runtime 已由 test-only guard 证明：BF-A/B 双争夺场景中 actual Shadow activation / stack resolution / returned response 后，BF-A Brush replacement-aware held-score payment / score / battle close 完成后才推进 BF-B `START_SPELL_DUEL`。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AH handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned Brush replacement-aware held-score next-contest advancement，入口为 `docs/CURRENT_STAGE4D_02AH_BATTLE_RESPONSE_ACTIVATION_BRUSH_NEXT_CONTEST_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AH_BATTLE_RESPONSE_ACTIVATION_BRUSH_NEXT_CONTEST_ADVANCEMENT_BASELINE_EVIDENCE.md`；targeted existing cross-product prerequisites 4/4、focused baseline 290/290、adjacent baseline 820/820、backend full 4232/4232、`git diff --check` 通过。该 handoff 只建立 BF-A Brush replacement-aware held-score payment / score / battle close 后推进 BF-B spell duel 的实现前护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02Z handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response conquer result ordering，入口为 `docs/CURRENT_STAGE4D_02Z_BATTLE_RESPONSE_CONQUER_RESULT_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02Z_BATTLE_RESPONSE_CONQUER_RESULT_ORDERING_BASELINE_EVIDENCE.md`；targeted existing 4D-02Y guard 1/1、focused baseline 280/280、adjacent baseline 810/810、backend full 4222/4222、`git diff --check` 通过。该 handoff 只建立 response-pass assignment conquer result ordering 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02Z focused slice 补充：Battle response conquer result ordering 已验收，入口为 `docs/CURRENT_STAGE4D_02Z_BATTLE_RESPONSE_CONQUER_RESULT_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02Z_BATTLE_RESPONSE_CONQUER_RESULT_ORDERING_EVIDENCE.md`；targeted 1/1、focused 281/281、adjacent 811/811、backend full 4223/4223、`git diff --check` 通过。服务端 assignment prompt 分支现在会在 Hunt attacker 征服 battlefield 时先输出 `BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 并持久化 experience，再进入 cleanup / battle close / battlefield control / next task advancement。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AA handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response held result ordering，入口为 `docs/CURRENT_STAGE4D_02AA_BATTLE_RESPONSE_HELD_RESULT_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AA_BATTLE_RESPONSE_HELD_RESULT_ORDERING_BASELINE_EVIDENCE.md`；targeted existing 4D-02Z guard 1/1、focused baseline 281/281、adjacent baseline 811/811、backend full 4223/4223、`git diff --check` 通过。该 handoff 只建立 response-pass assignment held result ordering 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AA focused slice 补充：Battle response held result ordering 已验收，入口为 `docs/CURRENT_STAGE4D_02AA_BATTLE_RESPONSE_HELD_RESULT_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AA_BATTLE_RESPONSE_HELD_RESULT_ORDERING_EVIDENCE.md`；targeted 1/1、focused 282/282、adjacent 812/812、backend full 4224/4224、`git diff --check` 通过。服务端 assignment prompt 分支现在会在 Hunt defender 守住 battlefield 时先输出 `BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 并持久化 experience，再进入 cleanup / battle close / battlefield control / next task advancement。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AB handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned assignment conquer result ordering，入口为 `docs/CURRENT_STAGE4D_02AB_BATTLE_RESPONSE_ACTIVATION_CONQUER_RESULT_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AB_BATTLE_RESPONSE_ACTIVATION_CONQUER_RESULT_ORDERING_BASELINE_EVIDENCE.md`；targeted existing 4D-02AA guard 1/1、focused baseline 282/282、adjacent baseline 812/812、backend full 4224/4224、`git diff --check` 通过。该 handoff 只建立 activation-returned assignment conquer result ordering 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AB focused slice 补充：Battle response activation conquer result ordering 已验收，入口为 `docs/CURRENT_STAGE4D_02AB_BATTLE_RESPONSE_ACTIVATION_CONQUER_RESULT_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AB_BATTLE_RESPONSE_ACTIVATION_CONQUER_RESULT_ORDERING_EVIDENCE.md`；targeted 1/1、focused 283/283、adjacent 813/813、backend full 4225/4225、`git diff --check` 通过。服务端现有 assignment prompt Hunt conquer result ordering 已由 test-only guard 证明可穿过 actual Shadow activation / stack resolution / returned response priority 后继续保持：`BATTLEFIELD_CONQUERED` / `EXPERIENCE_GAINED` 早于 cleanup / battle close / battlefield control / next task advancement。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AC handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned assignment held result ordering，入口为 `docs/CURRENT_STAGE4D_02AC_BATTLE_RESPONSE_ACTIVATION_HELD_RESULT_ORDERING_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AC_BATTLE_RESPONSE_ACTIVATION_HELD_RESULT_ORDERING_BASELINE_EVIDENCE.md`；targeted existing 4D-02AB guard 1/1、focused baseline 283/283、adjacent baseline 813/813、backend full 4225/4225、`git diff --check` 通过。该 handoff 只建立 activation-returned assignment held result ordering 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AC focused slice 补充：Battle response activation held result ordering 已验收，入口为 `docs/CURRENT_STAGE4D_02AC_BATTLE_RESPONSE_ACTIVATION_HELD_RESULT_ORDERING_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AC_BATTLE_RESPONSE_ACTIVATION_HELD_RESULT_ORDERING_EVIDENCE.md`；targeted 1/1、focused 284/284、adjacent 814/814、backend full 4226/4226、`git diff --check` 通过。服务端现有 assignment prompt Hunt held result ordering 已由 test-only guard 证明可穿过 actual Shadow activation / stack resolution / returned response priority 后继续保持：`BATTLEFIELD_HELD` / `EXPERIENCE_GAINED` 早于 cleanup / battle close / battlefield control / next task advancement。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AD handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned held-score payment resource context，入口为 `docs/CURRENT_STAGE4D_02AD_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AD_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；targeted existing 4D-02AC guard 1/1、focused baseline 284/284、adjacent baseline 814/814、backend full 4226/4226、`git diff --check` 通过。该 handoff 只建立 activation-returned held-score `RECYCLE_RUNE:*` payment-resource context preservation 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AD focused slice 补充：Battle response activation held-score payment resource context 已验收，入口为 `docs/CURRENT_STAGE4D_02AD_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AD_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；targeted 2/2、focused 286/286、adjacent 816/816、backend full 4228/4228、`git diff --check` 通过。服务端现在允许 active battle-response context capture 暂存稍后可能因 response 消耗而必要的 held-score `RECYCLE_RUNE:*`，final resume 仍严格消费该 action；若 no-response 后该 action 仍不必要，会在 resume 前剔除并避免不必要回收或卡住。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AE handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned held-score temporary payment resource context，入口为 `docs/CURRENT_STAGE4D_02AE_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_TEMP_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AE_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_TEMP_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；targeted existing 4D-02AD guards 2/2、focused baseline 286/286、adjacent baseline 816/816、backend full 4228/4228、`git diff --check` 通过。该 handoff 只建立 activation-returned held-score `TEMP_PAYMENT_RESOURCE:*` context preservation / no-response normalization 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AE focused slice 补充：Battle response activation held-score temporary payment resource context 已验收，入口为 `docs/CURRENT_STAGE4D_02AE_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_TEMP_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AE_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_TEMP_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；targeted 2/2、focused 288/288、adjacent 818/818、backend full 4230/4230、`git diff --check` 通过。服务端现在允许 active battle-response context capture 暂存稍后可能因 response 消耗而必要的 held-score `TEMP_PAYMENT_RESOURCE:*`，final resume 仍严格消费该 action；若 no-response 后该 action 仍不必要，会在 resume 前剔除并避免不必要 temporary resource 消耗或卡住。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AF handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned held-score next-contest advancement，入口为 `docs/CURRENT_STAGE4D_02AF_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_NEXT_CONTEST_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AF_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_NEXT_CONTEST_ADVANCEMENT_BASELINE_EVIDENCE.md`；targeted existing cross-product prerequisites 3/3、focused baseline 288/288、adjacent baseline 818/818、backend full 4230/4230、`git diff --check` 通过。该 handoff 只建立 BF-A held-score temporary payment / score / battle close 后推进 BF-B spell duel 的实现前护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AF focused slice 补充：Battle response activation held-score next-contest advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02AF_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_NEXT_CONTEST_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AF_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 289/289、adjacent 819/819、backend full 4231/4231、`git diff --check` 通过。服务端现有 runtime 已由 test-only guard 证明：BF-A/B 双争夺场景中 actual Shadow activation / stack resolution / returned response 后，BF-A held-score temporary payment / score / battle close 完成后才推进 BF-B `START_SPELL_DUEL`。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AG handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response activation-returned held-score recycle next-contest advancement，入口为 `docs/CURRENT_STAGE4D_02AG_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_RECYCLE_NEXT_CONTEST_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02AG_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_RECYCLE_NEXT_CONTEST_ADVANCEMENT_BASELINE_EVIDENCE.md`；targeted existing cross-product prerequisites 3/3、focused baseline 289/289、adjacent baseline 819/819、backend full 4231/4231、`git diff --check` 通过。该 handoff 只建立 BF-A held-score recycle payment / score / battle close 后推进 BF-B spell duel 的实现前护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02AG focused slice 补充：Battle response activation held-score recycle next-contest advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02AG_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_RECYCLE_NEXT_CONTEST_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02AG_BATTLE_RESPONSE_ACTIVATION_HELD_SCORE_RECYCLE_NEXT_CONTEST_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 290/290、adjacent 820/820、backend full 4232/4232、`git diff --check` 通过。服务端现有 runtime 已由 test-only guard 证明：BF-A/B 双争夺场景中 actual Shadow activation / stack resolution / returned response 后，BF-A held-score `RECYCLE_RUNE:*` payment / score / battle close 完成后才推进 BF-B `START_SPELL_DUEL`。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02V focused slice 补充：Battle response nonparticipant completed battlefield advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02V_BATTLE_RESPONSE_NONPARTICIPANT_COMPLETED_BATTLEFIELD_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02V_BATTLE_RESPONSE_NONPARTICIPANT_COMPLETED_BATTLEFIELD_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、关联 targeted 1/1、focused 234/234、adjacent 756/756、backend full 4219/4219、`git diff --check` 通过。服务端现有 same-turn `SpellDuelCompleted` skip policy 有 focused guard 绑定非参战 Shadow precise location：final immediate battle close 不为 completed current battlefield 同链路重开 spell duel，而推进下一处未完成 contested battlefield。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02V handoff / baseline 补充：下一 P0-004 窄切片锁定 battle response nonparticipant completed battlefield advancement，入口为 `docs/CURRENT_STAGE4D_02V_BATTLE_RESPONSE_NONPARTICIPANT_COMPLETED_BATTLEFIELD_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02V_BATTLE_RESPONSE_NONPARTICIPANT_COMPLETED_BATTLEFIELD_ADVANCEMENT_BASELINE_EVIDENCE.md`；targeted existing immediate path 1/1、focused baseline 233/233、adjacent baseline 755/755、backend full 4218/4218、`git diff --check` 通过。该 handoff 只建立 same-turn completed battlefield skip policy 与非参战 response source precise location 的组合护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02U focused slice 补充：Battle response nonparticipant source location 已验收，入口为 `docs/CURRENT_STAGE4D_02U_BATTLE_RESPONSE_NONPARTICIPANT_SOURCE_LOCATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02U_BATTLE_RESPONSE_NONPARTICIPANT_SOURCE_LOCATION_EVIDENCE.md`；targeted 1/1、新增关联 targeted 1/1、focused 233/233、adjacent 755/755、backend full 4218/4218、`git diff --check` 通过。服务端现在会在 battlefield source stack item 无明确 movement destination 且 source 仍在 battlefield zone 时保留当前 precise battlefield id；该切片只关闭非参战 response source stack-resolution object-location gap，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02U handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual Shadow activation 作为非参战 source 的 precise object-location preservation，入口为 `docs/CURRENT_STAGE4D_02U_BATTLE_RESPONSE_NONPARTICIPANT_SOURCE_LOCATION_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02U_BATTLE_RESPONSE_NONPARTICIPANT_SOURCE_LOCATION_BASELINE_EVIDENCE.md`；focused baseline 232/232、adjacent baseline 754/754、backend full 4217/4217、`git diff --check` 通过。该 handoff 只建立非参战 response source 在无 movement destination stack resolution 后仍保留 concrete battlefield id 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02T focused slice 补充：Battle response activation standby cleanup advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02T_BATTLE_RESPONSE_ACTIVATION_STANDBY_CLEANUP_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02T_BATTLE_RESPONSE_ACTIVATION_STANDBY_CLEANUP_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 232/232、adjacent 754/754、backend full 4217/4217、`git diff --check` 通过。服务端现在有 focused guard 证明 Shadow activation stack-open、stack resolved returned-response、assignment window open 期间均不会提前推进 `BF-NEXT`；legal assignment close / battlefield control resolve 后，`BATTLEFIELD_STANDBY_REMOVED` 先于 `BF-NEXT` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。本切片只最小修正 battle-response declaration replay 对已横置参战者的容忍和 active battle participant stack source precise location preservation，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02T handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation / stack resolution / return-to-response -> assignment -> battlefield-control-driven illegal standby cleanup -> next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02T_BATTLE_RESPONSE_ACTIVATION_STANDBY_CLEANUP_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02T_BATTLE_RESPONSE_ACTIVATION_STANDBY_CLEANUP_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 231/231、adjacent baseline 754/754、backend full 4216/4216、`git diff --check` 通过。该 handoff 只建立 activation-returned assignment branch 的 cleanup-first blocker ordering 实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02S audit 补充：Battle lifecycle remaining-scope refresh audit 已建立，入口为 `docs/CURRENT_STAGE4D_02S_BATTLE_LIFECYCLE_REMAINING_SCOPE_REFRESH_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02S_BATTLE_LIFECYCLE_REMAINING_SCOPE_REFRESH_BASELINE_EVIDENCE.md`；focused 231/231、adjacent 754/754、backend full 4216/4216、`git diff --check` 通过。该审计确认 4D-02B-R 已收窄 natural assignment、battle response、actual activation、post-payment blocker、no-result 与 next contested advancement 多个 representative cross-products；remaining P0-004 仍包括 battle-response breadth、cleanup blocker matrix、battle-result ordering matrix、damage assignment breadth、replacement/prevention/LayerEngine 交织与最终 frontend/E2E gates。下一建议切片是 activation-returned assignment 后 battle-control-driven illegal standby cleanup blocker ordering。

2026-05-15 4D-02R focused slice 补充：Battle response activation assignment no-result advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02R_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_NO_RESULT_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02R_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_NO_RESULT_ADVANCEMENT_EVIDENCE.md`；targeted 1/1、focused 231/231、adjacent 754/754、backend full 4216/4216、`git diff --check` 通过。服务端现在有 test-only guard 证明 Shadow activation stack-open、stack resolved returned-response、assignment window open 期间均不会提前推进 `BF-NEXT`；legal no-result assignment 产生 `BATTLE_NO_RESULT` / `NO_RESULT` battle resolution，关闭当前 battle / 清理当前 task 后推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS`。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02R handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation / stack resolution / return-to-response -> assignment no-result -> next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02R_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_NO_RESULT_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02R_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_NO_RESULT_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 230/230、adjacent baseline 753/753、backend full 4215/4215、`git diff --check` 通过。该 handoff 只建立 activation-returned assignment window 与 no-result battle close 后恢复 next battlefield advancement 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02Q focused slice 补充：Battle response activation post-payment advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02Q_BATTLE_RESPONSE_ACTIVATION_POST_PAYMENT_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02Q_BATTLE_RESPONSE_ACTIVATION_POST_PAYMENT_ADVANCEMENT_EVIDENCE.md`；targeted 3/3、focused 230/230、adjacent 753/753、backend full 4215/4215、`git diff --check` 通过。服务端现在有 focused guard 证明 Shadow activation stack-open、stack resolved returned-response、trigger payment open 期间均不会提前推进 `BF-NEXT`；accepted payment / decline 关闭窗口后才推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS`；rejected payment 保持 no-mutation 和 payment blocker。本切片只最小调整 Icevale trigger payment opening order，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02Q handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation / stack resolution / return-to-response -> immediate battle close -> trigger payment blocker -> payment-close next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02Q_BATTLE_RESPONSE_ACTIVATION_POST_PAYMENT_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02Q_BATTLE_RESPONSE_ACTIVATION_POST_PAYMENT_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 227/227、adjacent baseline 750/750、backend full 4212/4212、`git diff --check` 通过。该 handoff 只建立 activation-returned immediate battle close 后仍遵守 post-battle `PendingPayment` blocker，并在 accepted payment / decline 关闭窗口后恢复 next battlefield advancement 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02P focused slice 补充：Battle response activation immediate advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02P_BATTLE_RESPONSE_ACTIVATION_IMMEDIATE_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02P_BATTLE_RESPONSE_ACTIVATION_IMMEDIATE_ADVANCEMENT_EVIDENCE.md`；focused 227/227、adjacent 750/750、backend full 4212/4212、`git diff --check` 通过。服务端已有 test-only guard 证明 Shadow activation stack-open、stack resolved returned-response、P2 final response pass 期间均不会提前推进 `BF-NEXT`，P1 final response pass 后不打开 assignment，而是在 current battle close / control resolution 后推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS`。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02P handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation / stack resolution / return-to-response -> immediate battle close -> next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02P_BATTLE_RESPONSE_ACTIVATION_IMMEDIATE_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02P_BATTLE_RESPONSE_ACTIVATION_IMMEDIATE_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 226/226、adjacent baseline 749/749、backend full 4211/4211、`git diff --check` 通过。该 handoff 只建立 stack-open / returned-response 不提前推进、final immediate battle close 后恢复 next battlefield advancement 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02O handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation / stack resolution / return-to-response -> `ASSIGN_COMBAT_DAMAGE` -> next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02O_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02O_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 225/225、adjacent baseline 748/748、backend full 4210/4210、`git diff --check` 通过。该 handoff 只建立 stack-open / returned-response / assignment window 不提前推进、legal assignment close 后恢复 next battlefield advancement 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02O focused slice 补充：Battle response activation assignment advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02O_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02O_BATTLE_RESPONSE_ACTIVATION_ASSIGNMENT_ADVANCEMENT_EVIDENCE.md`；focused 226/226、adjacent 749/749、backend full 4211/4211、`git diff --check` 通过。服务端已有 test-only guard 证明 Shadow activation stack-open、stack resolved returned-response、P2/P1 pass 后 assignment window open 期间均不会提前推进 `BF-NEXT`，合法 `ASSIGN_COMBAT_DAMAGE` 后会在 current battle close / control resolution 后推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS`。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02N focused slice 补充：Battle response assignment advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02N_BATTLE_RESPONSE_ASSIGNMENT_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02N_BATTLE_RESPONSE_ASSIGNMENT_ADVANCEMENT_EVIDENCE.md`；focused 179/179、adjacent 706/706、backend full 4210/4210、`git diff --check` 通过。服务端已有 test-only guard 证明 response priority open、P2/P1 pass 后 assignment window open 期间均不会提前推进 `BF-NEXT`，合法 `ASSIGN_COMBAT_DAMAGE` 后会在 current battle close / control resolution 后推进 `BF-NEXT` 到 `SPELL_DUEL_TASKS`。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-15 4D-02N handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response priority -> `ASSIGN_COMBAT_DAMAGE` -> next contested battlefield task advancement 的组合链路，入口为 `docs/CURRENT_STAGE4D_02N_BATTLE_RESPONSE_ASSIGNMENT_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02N_BATTLE_RESPONSE_ASSIGNMENT_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 178/178、adjacent baseline 705/705、backend full 4209/4209、`git diff --check` 通过。该 handoff 只建立 response window / assignment window 不提前推进、legal assignment close 后恢复 next battlefield advancement 的实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02M focused slice 补充：Post-payment battle task advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02M_POST_PAYMENT_BATTLE_TASK_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02M_POST_PAYMENT_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`；focused 178/178、adjacent 705/705、backend full 4209/4209、`git diff --check` 通过。服务端现在在 post-battle trigger payment open 时保留 `PendingPayment` blocker，并在 accepted payment / accepted decline 关闭窗口后复用 shared battlefield task advancement 推进下一处 contested battlefield。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02M handoff / baseline 补充：下一 P0-004 窄切片锁定 post-battle `PendingPayment` 关闭后恢复 pending battlefield task advancement，入口为 `docs/CURRENT_STAGE4D_02M_POST_PAYMENT_BATTLE_TASK_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02M_POST_PAYMENT_BATTLE_TASK_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 175/175、adjacent baseline 702/702、backend full 4206/4206、`git diff --check` 通过。该 handoff 只建立 trigger payment blocker close 后的 task advancement 护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02L focused slice 补充：Immediate battle task advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02L_IMMEDIATE_BATTLE_TASK_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02L_IMMEDIATE_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`；focused 115/115、adjacent 610/610、backend full 4206/4206、`git diff --check` 通过。服务端现在让 immediate `DECLARE_BATTLE` direct-resolution branch 在 battle close / control resolution 后复用 pending battlefield task advancement，推进 next contested battlefield；cleanup blocker 与 `PendingPayment` blocker 会阻止提前推进。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02L handoff / baseline 补充：下一 P0-004 窄切片锁定 immediate `DECLARE_BATTLE` 直接结算分支在 battle close / control / held / conquer 后推进 next contested battlefield 的 task advancement parity，入口为 `docs/CURRENT_STAGE4D_02L_IMMEDIATE_BATTLE_TASK_ADVANCEMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02L_IMMEDIATE_BATTLE_TASK_ADVANCEMENT_BASELINE_EVIDENCE.md`；focused baseline 113/113、adjacent baseline 608/608、backend full 4204/4204、`git diff --check` 通过。该 handoff 只建立 immediate battle branch 下一实现护栏；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-01C focused slice 补充：Battlefield control standby cleanup 已验收，入口为 `docs/CURRENT_STAGE4D_01C_BATTLEFIELD_CONTROL_STANDBY_CLEANUP_AUDIT.md` 与 `docs/CURRENT_STAGE4D_01C_BATTLEFIELD_CONTROL_STANDBY_CLEANUP_EVIDENCE.md`；focused 124/124、adjacent 608/608、backend full 4204/4204、`git diff --check` 通过。服务端现在有 dedicated guard 证明 battle-control-driven standby removal 在 next contested battlefield task advancement 前完成；`MatchSession` lane snapshot 也会对非授权 viewer 过滤 hidden standby object id，避免 `lanes.battlefieldObjectIds` 泄漏。本切片不关闭 full official P0-002、P0-003、P0-004、P0-005、P1 或 READY。

2026-05-14 4D-01C handoff / baseline 补充：下一 P0-002 / P0-003 窄切片锁定 natural battle close / battlefield control resolve 后的 illegal standby cleanup queue ordering，入口为 `docs/CURRENT_STAGE4D_01C_BATTLEFIELD_CONTROL_STANDBY_CLEANUP_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_01C_BATTLEFIELD_CONTROL_STANDBY_CLEANUP_BASELINE_EVIDENCE.md`；focused baseline 123/123、adjacent baseline 607/607、backend full 4203/4203 通过。该 handoff 只建立 battle-control-driven standby removal、hidden-info redaction 与 next contested battlefield task ordering 的实现护栏；不关闭 full official P0-002、P0-003、P0-004、P0-005、P1 或 READY。

2026-05-14 4D-01B handoff / baseline 补充：下一 P0-002 / P0-003 窄切片锁定 control-change-to-battlefield 后 precise battlefield identity preservation，入口为 `docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_BASELINE_EVIDENCE.md`；focused baseline 32/32、adjacent baseline 173/173、backend full 4201/4201 通过。该 handoff 只建立 Hostile Takeover / gain-control-to-battlefield target 变更 controller 后仍保留 concrete battlefield identity，并在同一 battlefield 有双方 occupant 时自然派生 contested / spell-duel / battle pending tasks 的实现护栏；不关闭 full official P0-002、P0-003、P0-004、P0-005、P1 或 READY。

2026-05-14 4D-01B focused slice 补充：Control change precise battlefield 已验收，入口为 `docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_01B_CONTROL_CHANGE_PRECISE_BATTLEFIELD_EVIDENCE.md`；focused 29/29、adjacent 175/175、backend full 4203/4203、`git diff --check` 通过。服务端现在在 Hostile Takeover / gain-control-to-battlefield target controller 改变后保留原 concrete battlefield identity，并让同一 battlefield 上的 P1/P2 occupant 自然触发 contested / spell-duel / battle pending tasks；无 opponent occupant 时不误开 contest。本切片不关闭 full official P0-002、P0-003、P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02C handoff / baseline 补充：Battle lifecycle remaining-scope audit 已建立，入口为 `docs/CURRENT_STAGE4D_02C_BATTLE_LIFECYCLE_REMAINING_SCOPE_AUDIT.md`；下一 P0-004 实现交接为 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_BASELINE_EVIDENCE.md`；focused baseline 319/319、adjacent baseline 598/598、backend full 4188/4188 通过。该补充只建立 natural active `START_BATTLE` 接入 `ASSIGN_COMBAT_DAMAGE` prompt/runtime 的下一实现护栏，不关闭 P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02C focused slice 补充：Natural battle damage assignment lifecycle 已验收，入口为 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02C_NATURAL_BATTLE_DAMAGE_ASSIGNMENT_EVIDENCE.md`；focused 425/425、adjacent 607/607、backend full 4193/4193 通过。服务端现在可在 natural active `START_BATTLE` / minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 且多防守者包含 `Bulwark` / `Back Row` assignment-ordering 时打开 `ASSIGN_COMBAT_DAMAGE` prompt，并在合法 assignment 后完成 simultaneous damage、cleanup、battle close、battlefield control 与 matching task cleanup。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02D focused guard 补充：Battle response -> assignment integration guard 已验收，入口为 `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02D_BATTLE_RESPONSE_ASSIGNMENT_INTEGRATION_EVIDENCE.md`；focused 426/426、adjacent 607/607、backend full 4194/4194 通过。该 test-only 切片证明 legal Shadow battle-response priority 会先于 assignment window 打开，response pass 后再进入 `ASSIGN_COMBAT_DAMAGE` prompt，并在 assignment 后清理 battle task；不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02E focused slice 补充：Battle task advancement 已验收，入口为 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`；focused 427/427、adjacent 608/608、backend full 4195/4195、`git diff --check` 通过。服务端现在会在 `ASSIGN_COMBAT_DAMAGE` 完成 damage / cleanup / battle close / battlefield control 后复用 battlefield task advancement，使剩余 contested battlefield 立即进入 `SPELL_DUEL_OPEN` / `START_SPELL_DUEL`。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02I focused slice 补充：Battle response held-score payment-resource context 已验收，入口为 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；focused 431/431、adjacent 608/608、backend full 4199/4199、`git diff --check` 通过。服务端现在有 dedicated guard 证明 `RECYCLE_RUNE:*` held-score optional cost 可经过 natural battle-response pass 保留，并在 resumed `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` path 中产生 `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID` / `SCORE_GAINED` 审计。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02J focused slice 补充：Battle response held-score temporary payment-resource context 已验收，入口为 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_EVIDENCE.md`；focused 432/432、adjacent 608/608、backend full 4200/4200、`git diff --check` 通过。服务端现在有 dedicated guard 证明 `TEMP_PAYMENT_RESOURCE:*` held-score optional cost 可经过 natural battle-response pass 保留，并在 resumed `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` path 中产生 `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `SCORE_GAINED` 审计。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02K handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response actual activation + Brush context preservation，入口为 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 432/432、adjacent baseline 608/608、backend full 4200/4200、`git diff --check` 通过。该 handoff 只建立 `BRUSH_USE_REPLACED_BATTLEFIELD:*` optional cost 穿过 Shadow activation / stack resolution / return-to-response 后继续进入 replacement-aware held-score path 的实现前护栏，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02K focused slice 补充：Battle response actual activation + Brush context 已验收，入口为 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02K_BATTLE_RESPONSE_ACTIVATION_BRUSH_CONTEXT_EVIDENCE.md`；focused 433/433、adjacent 608/608、backend full 4201/4201、`git diff --check` 通过。服务端现在有 dedicated guard 证明 `BRUSH_USE_REPLACED_BATTLEFIELD:*` context 可穿过 Shadow activation、stack pass-pass、return-to-response priority 与 final response pass-pass，并最终进入 replacement-aware held-score payment / score / battle close path。本切片不改 runtime、不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02J handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response held-score temporary payment-resource context preservation，入口为 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_02J_BATTLE_RESPONSE_TEMP_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 431/431、adjacent baseline 608/608、backend full 4199/4199、`git diff --check` 通过。该 handoff 只建立 `TEMP_PAYMENT_RESOURCE:*` optional cost 经过 response pass 后继续进入 held-score payment / audit path 的实现前护栏，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02F focused slice 补充：Battle assignment no-result 已验收，入口为 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_EVIDENCE.md`；focused 428/428、adjacent 608/608、backend full 4196/4196、`git diff --check` 通过。服务端现在在 natural `ASSIGN_COMBAT_DAMAGE` 双方全灭时输出 `BATTLE_NO_RESULT`、`BATTLE_CLOSED`、`BattleResolutionState.Kind = NO_RESULT`，并清理 matching `START_BATTLE`。本切片不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02I handoff / baseline 补充：下一 P0-004 窄切片锁定 natural battle response held-score payment-resource context preservation，入口为 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_BASELINE_EVIDENCE.md`；focused baseline 430/430、adjacent baseline 608/608、backend full 4198/4198、`git diff --check` 通过。该 handoff 只建立 `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` optional cost 经过 response pass 后继续进入 held-score payment / audit path 的实现前护栏，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-02F handoff / baseline 补充：下一 P0-004 窄切片锁定 natural `ASSIGN_COMBAT_DAMAGE` no-result guard，入口为 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_BASELINE_EVIDENCE.md`；focused baseline 427/427、adjacent baseline 608/608、backend full 4195/4195、`git diff --check` 通过。该 handoff 只建立实现前护栏，不关闭 full official P0-004、P0-005、P1 或 READY。

2026-05-14 4D-03AA focused slice 补充：Image copy-token representative 已验收，入口为 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_EVIDENCE.md`；focused 16/16、adjacent 253/253、backend full 4136/4136、`git diff --check` 通过。服务端现在让 Mirror Image / LeBlanc Image 在当前对象模型中复制目标 `CardNo` / `Power` / tags，并在事件 payload 中写入 copied-card audit metadata 与 `tokenFactoryCardNo=UNL·T06`；Image creation 不触发 copied card on-play effects。本切片不关闭 LayerEngine / copy full official、Brush replacement 或 READY。

2026-05-14 4D-03AB focused slice 补充：Brush battlefield replacement representative 已验收，入口为 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_EVIDENCE.md`；focused 141/141、adjacent 511/511、Brush focused 8/8、backend full 4144/4144、`git diff --check` 通过。服务端现在通过 `BRUSH_USE_REPLACED_BATTLEFIELD:<original>` 在 Brush 据守得分 representative 中使用 original battlefield identity，并写 `BATTLEFIELD_REPLACEMENT_APPLIED` audit event；token deferred catalog 已清空。本切片不关闭完整 replacement ordering、LayerEngine 或 READY。

2026-05-14 4D-03AB handoff / baseline 补充：下一服务端 token battlefield replacement 切片锁定 `UNL·T03` 草丛 score-time optional replacement representative，交接入口为 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_03AB_TOKEN_FACTORY_BRUSH_REPLACEMENT_BASELINE_EVIDENCE.md`；focused baseline 141/141、`git diff --check` 通过。该补充只建立实现前护栏，不代表 Brush replacement 已实现，不关闭 P0/P1 或 READY。

2026-05-14 4D-03AA handoff / baseline 补充：下一服务端 token copy 切片锁定 `UNL·T06` 映像 `当我被打出时，变为某张卡牌的复制体`，交接入口为 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_03AA_TOKEN_FACTORY_IMAGE_COPY_BASELINE_EVIDENCE.md`；focused baseline 11/11、`git diff --check` 通过。该补充只建立实现前护栏，不代表 Image copy-token 已实现，不关闭 P0/P1 或 READY。

2026-05-14 4D-03Z focused slice 补充：Baron Nest token battlefield static representative 已验收，入口为 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_EVIDENCE.md`；focused 77/77、adjacent 345/345、backend full 4131/4131、`git diff --check` 通过。服务端现在支持受控正面非战斗单位从另一处精确战场 no-ROAM 移动到受控 `UNL·T01` 男爵巢穴；事件以 `movementPermission=BARON_NEST_MOVE_STATIC` 审计，不写 `movementKeyword=游走`。本切片不关闭 token factory / movement / battlefield full official 或 READY。

2026-05-14 4D-03Z handoff / baseline 补充：下一服务端 token battlefield static 切片锁定 `UNL·T01` 男爵巢穴 `单位可从任意位置移动到此处`，交接入口为 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_HANDOFF.md`，实现前基线为 `docs/CURRENT_STAGE4D_03Z_TOKEN_FACTORY_BARON_NEST_STATIC_BASELINE_EVIDENCE.md`；focused baseline 66/66、`git diff --check` 通过。该补充只建立实现前护栏，不代表 Baron Nest static 已实现，不关闭 P0/P1 或 READY。

2026-05-14 4D-03Y focused slice 补充：battlefield deferred catalog representative 已验收，入口为 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_EVIDENCE.md`；focused 15/15、adjacent 641/641、backend full 4120/4120、`git diff --check` 通过。服务端 catalog 现在明确 P6 battlefield deferred surfaces 为空，同时保留四个已实现 battlefield representative 的官方文本 / `BATTLEFIELD_RULE_DOMAIN` / 非 P4 registry / 非 direct registry 审计；activated-grant hand-written `ACTIVATE_ABILITY` 继续中文拒绝且 no-mutation。本切片不改 runtime、不关闭 P0/P1 full official 或 READY。

2026-05-14 4D-03Y handoff / baseline 补充：下一服务端 catalog hygiene 切片锁定 `P6BattlefieldEffectCatalog` 旧 deferred battlefield surface 退役。当前 BehaviorSpec 已证明 battlefield specs `57/57` implemented、manual battlefield specs `0`，且 Poro Forge / Dream Tree / Blood Altar / Blackflame Altar 四个代表已有 battlefield positive 回归；旧 catalog 仍返回四个 deferred surfaces，需改为 retired / implemented representative 语义并继续锁住 activated-grant `ACTIVATE_ABILITY` rejected no-mutation。入口为 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Y_BATTLEFIELD_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 15/15、adjacent baseline 641/641、`git diff --check` 通过。本切片不改 runtime、不关闭 P0/P1 full official 或 READY。

2026-05-14 4D-03X focused slice 补充：legend action deferred catalog representative 已验收，入口为 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_EVIDENCE.md`；focused 59/59、adjacent 285/285、backend full 4120/4120、`git diff --check` 通过。服务端 catalog 现在明确 P6 legend deferred surfaces 为空，同时保留五个已实现 `LEGEND_ACT` representative 的官方文本 / `LEGEND_ACTION_DOMAIN` / 非 P4 registry / 非 direct registry 审计；手写 `ACTIVATE_ABILITY` 继续中文拒绝且 no-mutation。本切片不改 runtime、不关闭 P0-005 full official 或 READY。

2026-05-14 4D-03X handoff / baseline 补充：下一服务端 catalog hygiene 切片锁定 `P6LegendAbilityCatalog` 旧 deferred legend surface 退役。当前 BehaviorSpec 已证明 legend specs `106/106` implemented、manual legend specs `0`，且 Yasuo / Lee Sin / Diana / Poppy / Viktor 五个代表已有 `LEGEND_ACT` positive 回归；旧 catalog 仍返回五个 deferred surfaces，需改为 retired / implemented representative 语义并继续锁住 `ACTIVATE_ABILITY` rejected no-mutation。入口为 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03X_LEGEND_ACTION_DEFERRED_CATALOG_BASELINE_EVIDENCE.md`；focused baseline 54/54、adjacent baseline 279/279 通过。本切片不改 runtime、不关闭 P0-005 full official 或 READY。

2026-05-14 4D-03W focused slice 补充：Renata Gold bonus representative 已验收，入口为 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_EVIDENCE.md`；focused 320/320、adjacent 965/965、backend full 4120/4120、`git diff --check` 通过。服务端已覆盖带 `RENATA_GOLD_EXTRA_1_MANA` marker 的 Gold token activation +1 mana bonus、prompt bonus metadata、`MANA_GAINED` audit event 与 rejected no-mutation guards；当前仅为 focused representative，不关闭 equipment-token full rules、P0-005 full official 或 READY，项目仍 **NOT READY**。

2026-05-14 4D-03W handoff / baseline 补充：Renata Gold bonus 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03W_PAYMENT_ENGINE_RENATA_GOLD_BONUS_BASELINE_EVIDENCE.md`；focused baseline 313/313、adjacent baseline 958/958 通过。该基线已被 focused slice 验收 supersede，仍保留为回归护栏。

2026-05-14 4D-03V focused slice 补充：Gold token resource skill representative 已验收，入口为 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_EVIDENCE.md`；focused 288/288、adjacent 782/782、backend full 4113/4113、`git diff --check` 通过。服务端已覆盖 `UNL·T05` / `SFD·T03` Gold token reaction-speed destroy + exhaust + gain generic temporary payment-only rune resource ability、destroy-as-cost、temporary ledger、non-priority prompt redaction 与 no-mutation guards；当前仅为 focused representative，当时不实现 Renata Gold extra mana bonus；该 bonus 已由 4D-03W supersede。

2026-05-14 4D-03V handoff / baseline 补充：Gold token resource skill 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03V_PAYMENT_ENGINE_GOLD_TOKEN_RESOURCE_SKILL_BASELINE_EVIDENCE.md`；focused baseline 264/264、adjacent baseline 758/758 通过。该基线已被 focused slice 验收 supersede，仍保留为回归护栏。

2026-05-13 4D-03G 补充：上方 latest status 已被 battlefield held score resource focused slice supersede。本轮已验收 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 的必要 `RECYCLE_RUNE:*` payment resource action，focused 22/22、adjacent 224/224、backend full 3809/3809 与 `git diff --check` 通过；审计 / 证据入口为 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md`。该切片仍不关闭 P0-005 full official，项目仍 **NOT READY**。

2026-05-13 4D-03H 补充：SFD Fiora trigger payment resource focused slice 已验收，入口为 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md`。候选锁定 `SFD·180/221` / `SFD·180a/221` 菲奥娜“友方单位变为强力后可支付黄色使其活跃”；focused 69/69、adjacent 242/242、backend full 3818/3818 通过。当前仅为 focused slice，不关闭 P0-005 full official，项目仍 **NOT READY**。

2026-05-14 4D-03I 补充：Malzahar resource skill focused slice 已验收，入口为 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md`。候选锁定 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill；focused 105/105、adjacent 317/317、backend full 3840/3840 通过。当前仅为 open-main representative，不关闭 spell-duel / swift timing、reaction prohibition、payment-only lifecycle 或 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03J 补充：Malzahar resource skill lifecycle handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_BASELINE_EVIDENCE.md`；focused baseline 109/109、adjacent baseline 336/336 通过。该基线已被下方 4D-03J focused slice 验收 supersede，仍保留为回归护栏；项目仍 **NOT READY**。
2026-05-14 4D-03J focused slice 补充：Malzahar resource skill lifecycle representative 已验收，入口为 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md`。候选锁定 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill；focused 116/116、adjacent 340/340、backend full 3847/3847 通过。当前仅为 spell-duel focus / no ordinary stack item / temporary payment-only ledger representative，不关闭完整 `[A]` / `[C]` family、inline payment-window temporary resource consumption、reaction/counter full target-filter model 或 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03K handoff / baseline 补充：temporary resource inline handoff / baseline 已建立，入口为 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_BASELINE_EVIDENCE.md`；focused baseline 331/331、adjacent baseline 526/526 通过。该基线已被下方 4D-03K focused slice 验收 supersede，仍保留为回归护栏；项目仍 **NOT READY**。
2026-05-14 4D-03K focused slice 补充：temporary payment-only resource inline representative 已验收，入口为 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md`。当前仅为 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` representative，不关闭 full official PaymentEngine、完整 resource skill family 或 P0-005 full official；focused 344/344、adjacent 539/539、backend full 3860/3860 通过，项目仍 **NOT READY**。
2026-05-14 4D-03N handoff / baseline 补充：Renata Glasc colored activated score 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md`；focused baseline 163/163、adjacent baseline 335/335 通过。当前仅为实现前回归护栏；目标是 `SFD·088/221` / `SFD·088a/221` `支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分` representative，不关闭 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03N focused slice 补充：Renata Glasc colored activated score representative 已验收，入口为 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md`。服务端 prompt / command / stack resolution 已覆盖 `SFD·088/221` / `SFD·088a/221` 支付 4 mana + 4 blue typed power、横置来源作为费用、普通 stack item before score、pass-pass 获得 1 分与 winning-score 语义；focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过。当前仅为 no-target colored activated score representative，不关闭 target-bearing activated abilities、完整 resource skill family 或 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03O handoff / baseline 补充：Crimson Rose ready-unit 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md`；focused baseline 143/143、adjacent baseline 370/370 通过。目标是 `UNL-109/219` 猩红玫瑰 `消耗3经验，{{横置}}：让一名单位变为活跃状态` representative，用以验证 equipment source、experience cost、target-bearing skill、enemy Spellshield target tax 与 ordinary stack-before-ready；当前仅为实现前回归护栏，不关闭 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03O focused slice 补充：Crimson Rose ready-unit representative 已验收，入口为 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md`。服务端 prompt / command / stack resolution 已覆盖 `UNL-109/219` 猩红玫瑰 base equipment source、3 experience cost、enemy Spellshield target tax、source exhaust-as-cost、ordinary stack item before ready、pass-pass 后目标单位 active；focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过。当前仅为 target-bearing equipment activated ready-unit representative，不关闭 Crimson Rose 第一行触发、Fluft、完整 resource skill family 或 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03P focused slice 补充：Fluft Poro Warhawk token representative 已验收，入口为 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`；focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过。服务端 prompt / command / stack resolution 已覆盖 `UNL-160/219` 绵绵魄罗 battlefield-only no-target activated token representative：横置 battlefield source、创建普通 stack item、pass-pass 后打出两名 `UNL·T02` 1 power Spellshield Warhawk token 到 controller base。当前仅为 focused representative，不关闭 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03Q handoff / baseline 补充：Shadow swift stun 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md`；focused baseline 198/198、adjacent baseline 738/738 通过。目标是 `UNL-194/219` 黑影 swift combat-response representative：支付 1 mana + 1 generic power、横置 battlefield source、选择同一战场正在进攻的敌方单位、创建普通 stack item、pass-pass 后应用 `STUNNED`。当前仅为实现前回归护栏，不关闭 P0-004/P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03Q focused slice 补充：Shadow swift stun representative 已验收，入口为 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`。服务端 prompt / command / stack resolution 已覆盖 `UNL-194/219` 黑影 swift combat-response stun representative：battle response priority 下支付 1 mana + 1 generic power、横置 battlefield source、选择同一战场正在进攻的敌方单位、创建普通 stack item、pass-pass 后应用 `STUNNED`，并覆盖 Spellshield target tax、`RECYCLE_RUNE:*` payment resource、stale target no-effect 与 no-mutation 边界；focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过。当前仅为 focused representative，不关闭完整 battle lifecycle、完整 activated ability family 或 P0-004/P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03R handoff / baseline 补充：Rage Sigil typed resource 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_BASELINE_EVIDENCE.md`；focused baseline 173/173、adjacent baseline 421/421 通过。该基线已被下方 focused slice 验收 supersede，仍保留为回归护栏；项目仍 **NOT READY**。
2026-05-14 4D-03R focused slice 补充：Rage Sigil typed resource representative 已验收，入口为 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03R_PAYMENT_ENGINE_RAGE_SIGIL_TYPED_RESOURCE_EVIDENCE.md`；focused 191/191、adjacent 439/439、backend full 4021/4021、`git diff --check` 通过。服务端 prompt / command / temporary ledger 已覆盖 `SFD·222/221` 暴怒之印 typed red payment-only resource representative：stack-priority reaction window 下横置 base equipment source、创建 1 点 red typed temporary payment-only resource、不创建 ordinary stack item，并覆盖 red/generic rune-cost consumption、wrong trait / mana-only no-mutation 与 cleanup。当前仅为 focused representative，不关闭完整 Sigil family、完整 `[A]` / `[C]` resource skill family 或 P0-005 full official，项目仍 **NOT READY**。
2026-05-14 4D-03S handoff / baseline 补充：SFD Sigil typed resource family 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 191/191、adjacent baseline 439/439 通过。该切片锁定 `SFD·226/221` 专注之印 green、`SFD·229/221` 洞察之印 blue、`SFD·231/221` 力量之印 orange、`SFD·234/221` 不和之印 purple、`SFD·238/221` 团结之印 yellow，明确不实现 OGN Sigil resource skills；项目仍 **NOT READY**。
2026-05-14 4D-03S focused slice 补充：SFD Sigil typed resource family representative 已验收，入口为 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03S_PAYMENT_ENGINE_SFD_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 213/213、adjacent 461/461、backend full 4043/4043、`git diff --check` 通过。服务端已覆盖剩余五张 SFD Sigil 的 stack-priority reaction base equipment resource skill、typed temporary ledger、同色/generic rune-cost consumption、wrong-color / mana-only no-mutation 与 OGN no-prompt guard；项目仍 **NOT READY**。
2026-05-14 4D-03T handoff / baseline 补充：OGN Sigil typed resource family 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_BASELINE_EVIDENCE.md`；focused baseline 213/213、adjacent baseline 461/461 通过。该切片只锁定 OGN 六张同名 Sigil resource skills；项目仍 **NOT READY**。
2026-05-14 4D-03T focused slice 补充：OGN Sigil typed resource family representative 已验收，入口为 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03T_PAYMENT_ENGINE_OGN_SIGIL_TYPED_RESOURCE_FAMILY_EVIDENCE.md`；focused 238/238、adjacent 486/486、backend full 4068/4068、`git diff --check` 通过。服务端已覆盖 OGN 六张 Sigil 的 stack-priority reaction base equipment resource skill、typed temporary ledger、同色/generic rune-cost consumption、wrong-color / mana-only no-mutation 与 SFD/OGN cross-print guard；项目仍 **NOT READY**。
2026-05-14 4D-03U handoff / baseline 补充：resource conversion equipment 下一服务端切片已建立，入口为 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_BASELINE_EVIDENCE.md`；focused baseline 209/209、adjacent baseline 464/464 通过。该切片只锁定能量通道、远古簇碑、海克斯异常体三张 resource conversion equipment skills；项目仍 **NOT READY**。
2026-05-14 4D-03U focused slice 补充：resource conversion equipment representative 已验收，入口为 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md`；focused 230/230、adjacent 485/485、backend full 4089/4089、`git diff --check` 通过。服务端已覆盖能量通道获得 1 mana、远古簇碑 mana-to-generic temporary payment-only resource、海克斯异常体 ordinary-generic-power-to-mana、conversion choices、no-stack policy 与 no-mutation guards；项目仍 **NOT READY**。
当前已 verified representative evidence：Stage 4C-85 `炽烈符文` / `翠意符文` red / blue basic rune `RUNE_RESOURCE_DOMAIN` payment-resource route 已入账，并通过 focused / rune resource payment prompt regression / backend full / frontend build / Chrome smoke。2026-05-13 formal 18-step E2E 已通过，房间 `formal-18-1778623926434-15` 覆盖官方 deck/opening/mulligan、stack pass-pass、unit move、reconnect、P2 battlefield score 与 surrender result。Active `START_BATTLE` guard test-only evidence 已新增，覆盖争夺战场 battle task prompt / command no-mutation 边界。Stage 4C-86 至 Stage 4C-98 已完成多批 representative evidence / design gate alignment。Stage 4D-01 board task queue foundation 已新增 focused acceptance tests，覆盖 base-to-battlefield、battlefield-to-base、precise roam destination-scoped contest tasks、cleanup-first blocking、illegal standby / unattached equipment redaction、cleanup repeat、`PASS_FOCUS` task promotion 与 reconnect pending task redaction；focused 31/31、adjacent 149/149、backend full 3780/3780 通过。Stage 4D-02 focused slice 已新增 `SpellDuelBattleStateMachineTests`，覆盖多争夺战场 one-active ordering、wrong-focus no-mutation、spell-duel stack task binding、cleanup 后推进下一 task、`SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata + redaction；focused new 6/6、focused handoff 35/35、adjacent 127/127、backend full 3786/3786 通过。Stage 4D-03 PaymentEngine focused foundation 已新增 shared `PaymentPlan` / authorize / commit helper、PLAY_CARD / PAY_COST / ASSEMBLE_EQUIPMENT 代表接入、transactional `RECYCLE_RUNE:*` rollback tests 与 plan audit payload；focused 56/56、adjacent 245/245、backend full 3791/3791 通过。Stage 4D-03B non-play payment focused slice 已把 Vi / Xerath `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 代表窗口接入 shared plan / commit；focused 18/18、adjacent 318/318、backend full 3791/3791 通过。Stage 4D-03C play optional / extra payment focused slice 已把 `PLAY_CARD` Haste / Echo / Spellshield / experience / payment-resource 代表路径的 affordability preflight 与 `COST_PAID` audit metadata 进一步接入 shared plan 口径；focused 31/31、adjacent 363/363、backend full 3791/3791 通过。Stage 4D-03D focused slice 已把 Vi / Xerath `ACTIVATE_ABILITY` 支付窗口的 `RECYCLE_RUNE:*` payment resource action 接入 prompt quote / command commit / audit 口径；focused 84/84、adjacent 257/257、backend full 3796/3796 通过。Stage 4D-03E focused slice 已把 `HIDE_CARD` 标准待命、Teemo 替代待命与免费待命迁移到 shared plan / commit / audit 口径；focused 88/88、adjacent 290/290、backend full 3800/3800 通过。Stage 4D-03F focused slice 已把普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` payment resource action 接入 shared plan / prompt quote / command commit / audit 口径；focused 55/55、adjacent 233/233、backend full 3804/3804 通过；该阶段继续收窄 P0-005 foundation，不关闭 full official PaymentEngine。项目仍 **NOT READY**。
历史复审范围：battlefield standby cleanup/control lifecycle / illegal standby task visibility / battlefield resolution snapshot / battle resolution snapshot / basic rune recycle / recovery action-log audit / multi-defender declare battle prompt / multi-attacker declare battle representative path / multi-participant declare battle representative path / same-priority battle damage assignment order / battle no-result visibility / haste payment resource evidence / haste payment resource hub-browser evidence / haste colored payment resource evidence / all haste colored payment profile evidence / payment failure state hash matrix / typed spend power prompt / typed spend power amount prompt / typed spend power payment resource combo / over-recycle payment guard / payment resource contribution metadata / required double payment resources / mixed trait payment resources / generic mixed trait payment resources / legacy prompt fixture gate cleanup / spellshield multi-target hub-ui evidence / echo reduction prompt-ui evidence / granted echo prompt-ui evidence / equipment cost reduction prompt-ui evidence / held unit cost increase prompt-ui evidence / prevent unit play destination prompt evidence / standby hide prompt-ui evidence / standby reveal prompt-ui evidence / priority reaction prompt-ui evidence / spellshield insufficient target prompt-ui evidence / unknown cardNo prompt-ui evidence / unknown assemble prompt-ui evidence / assemble payment resource prompt-ui evidence / unknown legend action prompt-ui evidence / unknown activate ability prompt-ui evidence / unknown move unit prompt-ui evidence / unknown move unit battlefield prompt-ui evidence / unknown rune prompt-ui evidence / unknown declare battle prompt-ui evidence / unknown declare battle battlefield prompt-ui evidence / unknown standby hide-reveal prompt-ui evidence / unknown play target prompt-ui evidence / unknown assemble target prompt-ui evidence / unknown legend action target prompt-ui evidence / unknown activate ability target prompt-ui evidence / play card command cardNo guard / end turn command window guard / turn-end state cleanup loop / turn-start command guard / finished match command guard / finished mulligan guard / finished submit error guard / finished submit deck error guard / xerath cleanup rune pool guard / turn-start cleanup object location guard / play additional cost object location guard / legend action object location guard / standby reaction object location guard / Jinx turn-start draw object location guard / tap rune object location guard / recycle rune object location guard / battlefield experience ability object location guard / activated ability stack object location guard / recovery spectator frame validator guard / service-driven mulligan selection guard / card detail evidence display guard / standby source control guard / move unit source control guard / activate ability source control guard / declare battle participant control guard / play card source control guard / assemble equipment control guard / legend action control guard / play card target control guard / play card private-zone target control guard / play card enemy target control guard / play card opponent private target control guard / play card any hand target control guard / play card any main deck top-five target control guard / play card sacred judgment keep target control guard / mulligan hand selection control guard / mulligan replacement draw control guard / play card main deck look-window control guard / rune deck call control guard / main deck draw control guard / burnout graveyard recycle control guard / main deck reveal trigger control guard / recycle target control guard / battlefield recycle rune control guard / LeBlanc trigger control guard / graveyard unit banish control guard / battlefield discard draw hand control guard / discard all hands control guard / any hand discard resolution guard / friendly hand discard resolution guard / graveyard return hand resolution guard / graveyard play base resolution guard / hand play base resolution guard / opponent top main deck play base resolution guard / target owner main deck resolution guard / target return hand resolution guard / banish play field resolution guard / gain control resolution guard / move target owner base resolution guard / move target battlefield resolution guard / move target unit location resolution guard / swap target location resolution guard / destroy target resolution guard / field target mutation resolution guard / special target interaction resolution guard / field area selection resolution guard / battlefield static source control guard / battlefield movement static source guard / battlefield scoring start source guard / battlefield prompt source guard / standby destination prompt source guard / standby destination command source guard / battlefield granted ability source guard / battlefield required legend source guard / battlefield return-call-rune source guard / battlefield held conquest unit source guard / legend trigger source guard / battlefield conquer target source guard / rune source prompt payment guard / activate ability prompt source guard / move unit prompt source guard / battlefield snapshot controller guard / battlefield standby controller fallback guard / zero power cleanup identity guard / battlefield control occupant source guard / battlefield control standby source guard / battlefield defender move source guard / battlefield unit experience source guard / unattached equipment cleanup / legacy unattached equipment cleanup / spell duel close cleanup / spell duel cleanup battle invalidation / official deck session error localization / room reconnect error localization / frontend fallback text gate / petal pixie static power / scarlet pigeon multi-attacker power / soul shepherd token static power / scouting warhawk last breath rune / mechanical trickster last breath minions / ironclad vanguard last breath robots / honest broker last breath gold / unsung hero last breath draw / ghostly centaur destroyed-unit power / eclipse vanguard stun trigger / last rites graveyard recycle assemble / hextech gauntlet dynamic assemble / any unit target scope guard / enemy battlefield unit target scope guard / demacia envoy experience evidence / tibbers all-battlefield damage evidence / bubblebot friendly Mechanical ready evidence 补丁。
历史复审范围补充：treasure golem Gold token evidence。
历史复审范围补充：faithful craftsman Minion token evidence。
历史复审范围补充：skullcrack battlefield stun evidence。
历史复审范围补充：diana spell-duel static evidence。
历史复审范围补充：hextech ray damage / cleanup / swift evidence。
历史复审范围补充：plunder alley battlefield defend move-to-base evidence。
历史复审范围补充：sivir haste ready payment evidence。
历史复审范围补充：gentleman duel mutual power damage evidence。
历史复审范围补充：long sword equipment play / assemble identity evidence。
历史复审范围补充：syndra spell-duel echo static evidence。
历史复审范围补充：moon rise enemy battlefield power -2 evidence。
历史复审范围补充：forced conscription control small enemy recall evidence。
历史复审范围补充：bullet time power-spent enemy battlefield damage evidence。
历史复审范围补充：flame chompers source-unit-to-base evidence。
历史复审范围补充：duel mutual current-power damage evidence。
历史复审范围补充：mighty faerie source-unit-to-base evidence。
历史复审范围补充：heimerdinger shared source-unit-to-base evidence。
历史复审范围补充：red / blue rune resource-domain payment-resource evidence。
历史复审范围补充：mountain drake / dockside lurker vanilla source-unit evidence。
历史复审范围补充：vanguard squire / aggressive dragonhound active-entry source-unit evidence。
历史复审范围补充：royal guard source-unit plus Sand Soldier token evidence。
历史复审范围补充：stern sergeant source-unit plus dynamic experience evidence。
历史复审范围补充：royal attendant source-unit plus legend mode evidence。
历史复审范围补充：UNL babbling poro source-unit plus predict recycle evidence。
历史复审范围补充：static effect design gate for molten drake / sona / annie / gentle gem dragon。
历史复审范围补充：legacy guard evidence alignment for stage 4C-60 through 4C-69 direct-card FUs。
历史复审范围补充：arena service crew / official minion token factory / dark child Annie evidence alignment。
历史复审范围补充：power obelisk / glory arena / frosthold battlefield residual evidence alignment。
历史复审范围补充：formal 18-step E2E continuous official-room evidence。
历史复审范围补充：imperial shrine conquer pay-one return-unit Sand Soldier evidence。
历史复审范围补充：shield wall move-friendly-battlefield-units evidence。
历史复审范围补充：malzahar resource-skill design gate。
历史复审范围补充：Stage 4D-01 board task queue foundation evidence。
历史复审范围补充：Stage 4D-02 spell duel / battle handoff and baseline evidence。
历史复审范围补充：Stage 4D-02 spell duel / battle focused slice evidence。
历史复审范围补充：Stage 4D-03 PaymentEngine handoff and baseline evidence。
历史复审范围补充：Stage 4D-03 PaymentEngine focused foundation evidence。
历史复审范围补充：Stage 4D-03B non-play payment handoff and baseline evidence。
历史复审范围补充：Stage 4D-03B non-play payment focused slice evidence。
历史复审范围补充：Stage 4D-03C play optional / extra payment handoff and baseline evidence。
历史复审范围补充：Stage 4D-03C play optional / extra payment focused slice evidence。
历史复审范围补充：Stage 4D-03D activate ability payment resource handoff and baseline evidence。
历史复审范围补充：Stage 4D-03D activate ability payment resource focused slice evidence。
历史复审范围补充：Stage 4D-03E hide card payment handoff and baseline evidence。
历史复审范围补充：Stage 4D-03E hide card payment focused slice evidence。
历史复审范围补充：Stage 4D-03F pending PAY_COST resource handoff and baseline evidence。
历史复审范围补充：Stage 4D-03F pending PAY_COST resource focused slice evidence。
历史复审范围补充：Stage 4D-03G battlefield held score resource focused slice evidence。
历史复审范围补充：Stage 4D-03H trigger payment resource handoff and baseline evidence。
历史复审范围补充：Stage 4D-03K temporary resource inline handoff and baseline evidence。
历史复审范围补充：Stage 4D-03N Renata colored activated score handoff and baseline evidence。
历史复审范围补充：Stage 4D-03N Renata colored activated score focused slice evidence。
历史复审范围补充：Stage 4D-03O Crimson Rose ready-unit handoff and baseline evidence。
历史复审范围补充：Stage 4D-03P Fluft Poro Warhawk token handoff and baseline evidence。
自查依据：`docs/符文战场_服务端核心规则自查文档.md`、仓库内五个官方规则 PDF 对应的核心规则/FAQ/勘误要求，以及当前 `src/Riftbound.Engine`、`src/Riftbound.Api`、`tests/Riftbound.ConformanceTests` 实现。

## 2026-05-13 Stage 4D-01 Board Task Queue Foundation Evidence

证据入口：`docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md` 与 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_EVIDENCE.md`。本批新增 `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`，未修改引擎、API、前端或卡牌矩阵。

证据点：base-to-battlefield empty / occupied contest、battlefield-to-base contest removal、cleanup-first blocking、cleanup repeat until stable、illegal standby / unattached equipment redaction、`PASS_FOCUS` spell duel close -> `START_BATTLE` promotion、precise roam mixed-case `BATTLEFIELD:<objectId>` preservation and destination-only contest queue、reconnect pending task phase / active task / hidden-info redaction。

验证：focused 31/31、adjacent 149/149、backend full 3780/3780，diff whitespace checks 无输出。本证据把 P0-002 / P0-003 的 board task queue foundation 收窄并固化为自动化回归；仍不宣称完整 held/conquer/control lifecycle、replacement/prevention、完整 battle cleanup、P0-004 spell-duel/battle state machine、P0-005 PaymentEngine、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-02 Spell Duel / Battle Handoff And Baseline

证据入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`。本批仅新增 handoff / baseline 文档，不修改引擎、API、前端、测试源码或卡牌矩阵。

基线点：当前已有 `SpellDuelState`、`BattleState`、`BattlefieldTaskState`、`START_SPELL_DUEL` / `START_BATTLE` 代表路径、法术对决焦点恢复、active `START_BATTLE` prompt / command guard、代表性 `DECLARE_BATTLE`、`ASSIGN_COMBAT_DAMAGE`、battle cleanup 和 battlefield control resolution。

验证：focused baseline 29/29、adjacent baseline 121/121 通过。该基线只说明既有代表路径绿色；仍不宣称多个争夺战场串联、wrong-focus no-mutation、swift/reaction task binding、reconnect during `SPELL_DUEL_TASKS` / `BATTLE_TASKS`、完整 battle id / participant lifecycle、official no-result cleanup、P0-004 resolved 或 READY。

## 2026-05-13 Stage 4D-02 Spell Duel / Battle Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/MatchSession.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`，新增 `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`，不修改前端、PaymentEngine、卡牌矩阵或 READY 结论。

证据点：`MatchSession` 新增 active spell duel battlefield 推导，多争夺战场时只把 deterministic 第一个未完成战场标为 active，并只给该 active task 暴露 focus / stack metadata。`ResolvePassFocus` 在 spell duel close 后跑 cleanup；若 cleanup 让 matching `START_BATTLE` 消失，会继续推进下一 pending battlefield task。新增 tests 覆盖多争夺战场 one-active ordering、非焦点 / 错时机 `PASS_FOCUS` no-mutation、spell-duel stack 回到同一 active task、cleanup 后推进下一 task、`SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata 与 hidden-info redaction。

验证：focused new 6/6、focused handoff regression 35/35、adjacent regression 127/127、backend full 3786/3786 通过。该证据收窄 P0-004，但仍不宣称 full official battle lifecycle、complete swift/reaction chain、battle response window、no-result 全矩阵、replacement/prevention、PaymentEngine、LayerEngine、1009/811 full-official 或 READY。

## 2026-05-13 Stage 4D-03 PaymentEngine Handoff And Baseline

证据入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md`。本批仅新增 handoff / baseline 文档，不修改引擎、API、前端、测试源码或卡牌矩阵。

基线点：当前已有 typed `RunePool`、`PaymentCostRules` helper、`PendingPaymentState` 最小窗口、`PLAY_CARD` typed power / Haste / Echo / Spellshield tax / payment resource action、`TRIGGER_PAYMENT` 代表支付或拒付、Long Sword / Vi / Xerath / battlefield held score typed-power 代表路径，以及 ActionPrompt / GameHub payment resource candidates。

验证：focused baseline 51/51、adjacent payment / ActionPrompt / GameHub regression 240/240 通过。该基线只说明既有代表支付路径绿色；仍不宣称 prompt quote 与 command commit 已共用 PaymentPlan，不宣称资源动作事务回滚已中心化，不关闭 P0-005、P1 或 READY。

## 2026-05-13 Stage 4D-03 PaymentEngine Focused Foundation Evidence

证据入口：`docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/PaymentCostRules.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`，新增 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`，不修改前端、卡牌矩阵或 READY 结论。

证据点：`PaymentCostRules` 新增 `PaymentPlan`、`AuthorizePayment`、`TryCommitPayment` 与 plan-driven `BuildCostPaidPayload`，统一 mana、generic power、typed power、experience、optional / extra labels、payment resource actions、legal choices 与 audit metadata。`CoreRuleEngine` 将普通 `PAY_COST`、当前触发支付代表路径、`PLAY_CARD` 和 `ASSEMBLE_EQUIPMENT` 接入 shared plan / commit foundation；新增测试覆盖 wrong-trait no-mutation、post-`RECYCLE_RUNE:*` insufficient typed-cost rollback、play-card / assemble `COST_PAID` metadata。

验证：focused 56/56、adjacent 245/245、backend full 3791/3791，`git diff --check` 无输出。本证据把 P0-005 的 shared payment plan foundation 收窄并固化为自动化回归；仍不宣称完整 `[A]` / `[C]` resource skills、Haste / Echo / Spellshield 全分支、replacement / optional / extra cost、所有非出牌 payment windows、trigger context typed metadata、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-03B Non-Play Payment Handoff And Baseline

证据入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md`。本批仅新增 handoff / baseline 文档，不修改引擎、API、前端、测试源码或卡牌矩阵。

基线点：当前 Vi / Xerath `ACTIVATE_ABILITY`、Spellshield tax、`LEGEND_ACT` mana / experience payment、battlefield held pay-power score 与相关 GameHub prompt seeds 行为绿色，可作为 4D-03B 迁移 shared `PaymentPlan` / `TryCommitPayment` 的回归护栏。

验证：focused baseline 18/18、adjacent baseline 318/318 通过。该基线不证明上述非出牌窗口已完成 PaymentEngine 统一；P0-005、P1 和 READY 均仍未关闭。

## 2026-05-13 Stage 4D-03B Non-Play Payment Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/CoreRuleEngine.cs` 与 `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`，不修改前端、卡牌矩阵、`MatchSession.cs`、`PaymentCostRules.cs` 或 READY 结论。

证据点：Vi `ACTIVATE_ABILITY`、Xerath `ACTIVATE_ABILITY` + Spellshield tax、`LEGEND_ACT` mana / experience payment 与 battlefield held pay-4-power score 改为使用 shared `PaymentPlan` / `TryCommitPayment`。对应 `COST_PAID` payload 保留兼容键，并补充 payment id/window、source / ability / reason、total cost、remaining pool / experience metadata。

验证：focused 18/18、adjacent 318/318、backend full 3791/3791，`git diff --check` 无输出。本证据收窄 P0-005 的 non-play payment representative breadth；仍不宣称完整 `[A]` / `[C]` resource skills、Haste / Echo / Spellshield 全窗口、replacement / optional / extra cost、所有支付窗口、prompt quote parity、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-03C Play Optional / Extra Payment Handoff And Baseline

证据入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_BASELINE_EVIDENCE.md`。本批仅新增 handoff / baseline 文档，不修改引擎、API、前端、测试源码或卡牌矩阵。

基线点：当前 `PLAY_CARD` recycle rune payment resource action、Haste ready、Echo、Spellshield target tax、experience optional cost 与相关 GameHub prompt seeds 行为绿色，可作为 4D-03C 迁移 shared quote / authorize / commit / audit 口径的回归护栏。

验证：focused baseline 31/31、adjacent baseline 363/363 通过。该基线不证明上述 optional / extra payment 路径已完成 PaymentEngine 统一；P0-005、P1 和 READY 均仍未关闭。

## 2026-05-13 Stage 4D-03C Play Optional / Extra Payment Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` 与 `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`，不修改前端、卡牌矩阵、`PaymentCostRules.cs`、`MatchSession.cs` 或 READY 结论。

证据点：`PLAY_CARD` representative affordability preflight 通过 `PaymentPlan` / `AuthorizePayment` 统一检查 mana、generic / typed power 与 experience；final commit 继续在 payment resource action 后使用 `TryCommitPayment`；`COST_PAID` payload 增补 cost reductions、optional mana reduction、battlefield reductions/increases、Spellshield tax 和 recycled rune metadata。

验证：focused 31/31、adjacent 363/363、backend full 3791/3791，`git diff --check` 无输出。本证据收窄 P0-005 的 `PLAY_CARD` optional / extra representative breadth；仍不宣称完整 `[A]` / `[C]` resource skills、Haste / Echo / Spellshield 全窗口、replacement / optional / extra cost、所有支付窗口、prompt quote parity、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-03D Activate Ability Payment Resource Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs` 与 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`，不修改前端、卡牌矩阵、`PaymentCostRules.cs` 或 READY 结论。

证据点：Vi / Xerath `ACTIVATE_ABILITY` source requirements 在 power 不足且基地有可回收基础符文时公开 `RECYCLE_RUNE:<objectId>` payment resource choice、per-choice contribution 与 available-power metadata。命令侧先在本地副本中回收符文并产生同一 `ACTIVATE_ABILITY` `paymentId` 的 `RUNE_RECYCLED` / `POWER_GAINED`，随后通过 shared `PaymentPlan` authorize / commit 并在 `COST_PAID` 中记录 `paymentResourceActions`、`recycledRuneObjectIds` 与 remaining pool metadata。

验证：focused 84/84、adjacent 257/257、backend full 3796/3796，`git diff --check` 无输出。本证据收窄 P0-005 的 `ACTIVATE_ABILITY` payment resource representative breadth；仍不宣称完整 `[A]` / `[C]` resource skills、trigger payment resource action、`LEGEND_ACT` / battlefield held score resource action、所有支付窗口、prompt quote parity、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-03E Hide Card Payment Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md`、`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md`、`docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/CoreRuleEngine.cs` 与 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`，不修改前端、卡牌矩阵或 READY 结论。

证据点：`HIDE_CARD` 标准 `STANDBY_A`、Teemo `STANDBY_TEEMO_MANA` 与 Guerrilla Warfare `STANDBY_FREE` 已迁移到 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`。新增测试覆盖标准、免费、Teemo 替代和费用不足 no-mutation plan audit。

验证：实现前 focused 84/84、adjacent 286/286 通过；focused 88/88、adjacent 290/290、backend full 3800/3800，`git diff --check` 无输出。本证据收窄 P0-005 的 `HIDE_CARD` standby payment representative breadth；仍不宣称完整 standby reaction lifecycle、完整 `[A]` / `[C]` resource skills、trigger payment resource action、所有支付窗口、prompt quote parity、P1 LayerEngine 或 READY。

## 2026-05-13 Stage 4D-03F Pending PAY_COST Resource Focused Slice Evidence

证据入口：`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md`、`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md`、`docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md`。本批修改 `src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs` 与 `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`，不修改前端、卡牌矩阵或 READY 结论。

证据点：普通 pending `PAY_COST` 现在可在同一 payment window 中提交合法 `RECYCLE_RUNE:*` payment resource action，Core 会事务性复制 zone / object state，先应用资源动作，再通过 shared `PaymentPlan` / `TryCommitPayment` 扣费。Snapshot / prompt metadata 暴露 `paymentResourceActions`、per-choice contribution 和 available-power-with-resources 信息；`COST_PAID` audit payload 保留 legacy fields，并追加 `paymentResourceActions`、`legalPaymentChoiceIds`、剩余资源池与 `recycledRuneObjectIds`。

验证：实现前 focused 51/51、adjacent 229/229 通过；focused 55/55、adjacent 233/233、backend full 3804/3804，`git diff --check` 无输出。本证据收窄 P0-005 的 ordinary pending `PAY_COST` resource-action representative breadth；仍不宣称 trigger payment resource action、完整 `[A]` / `[C]` resource skills、所有支付窗口、prompt quote parity、P1 LayerEngine 或 READY。

## 2026-05-13 Formal 18-Step E2E Evidence

证据入口：`docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md`。本批新增 `src/Riftbound.DevUi/scripts/chrome-formal-18-e2e.mjs`，以双 headless Chrome profile 和 SignalR 驱动同一连续正式房间主流程；`npm run e2e:formal-18 -- --start-api` 已通过，房间 `formal-18-1778623926434-15`。本证据证明当前服务端/前端可衔接官方 deck submit、ready、mulligan、首回合 rune/draw/play、stack pass-pass、unit move、reconnect、P2 `OGN·290/298` 首回合得分、surrender 和 result page。

口径限制：该证据满足 A 主控 formal 18-step 主流程，但不关闭本报告的 P0-002 / P0-004 full official battlefield contest / battle lifecycle 缺口，也不替代完整 PaymentEngine、LayerEngine、1009/811 full-official card matrix 或最终 READY audit。

历史批次中仍出现的“formal 18-step E2E 仍为缺口”文字均被本节与 `docs/CURRENT_FORMAL_18_STEP_E2E_EVIDENCE.md` supersede；当前 active blocking P0 不再包含“缺一条 formal 18-step”，而是集中在 P0-002 / P0-003 / P0-004 / P0-005 与 P1 full-official 证据。

## 2026-05-13 Active START_BATTLE Guard Evidence

证据入口：`tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`。本批仅新增测试，不修改 `src/Riftbound.Engine` 或前端代码。

证据点：构造 spell-duel 已完成的争夺战场 active `START_BATTLE` 状态；验证 `ActionPrompt` 只向 active player 暴露当前争夺战场的 `DECLARE_BATTLE`，并且 sourceRequirements 只包含当前战场上的公开、正面、ready、受控 attacker / defender；wrong battlefield、其他战场单位、base、stale、face-down standby、equipment、spell、rune 等 command 均拒绝且 no-mutation；合法 active task `DECLARE_BATTLE` 后 `START_BATTLE` 不再留在 pending queue，并保留代表性 battle/control 事件。

验证：focused 17/17、BattlefieldContest / StartBattle / DeclareBattle / PendingTaskQueue adjacent regression 94/94、backend full 3771/3771。该证据只关闭 active `START_BATTLE` guard representative test gap，不关闭完整 battle state machine、spell duel / battle lifecycle、multi-battlefield APNAP、complete damage assignment、replacement / prevention、PaymentEngine、LayerEngine、1009/811 full-official 或 READY。

## 2026-05-13 阶段 4C-87 Shield Wall Move Guard 审计

阶段 4C-87 审计入口：`docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH87_SHIELD_WALL_MOVE_GUARD_EVIDENCE.md`。本批记录 Shield Wall / 禁军之墙 `FU-a7fbef72ba` / `SFD·043/221` / cardId `33119` 的 direct-card-behavior representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·043/221` 为支付 2、`FriendlyBattlefieldUnit` target scope、`MinTargetCount=0`、按己方战场单位数动态 target 上限、移动目标到基地。既有 fixture 与 tests 覆盖普通手牌打出、stack pass-pass 后移动所选己方战场单位到拥有者基地，以及敌方单位、己方基地单位、重复目标 no-mutation 拒绝。

验证：focused 2/2、MoveFriendly / MoveUnit / FriendlyBattlefieldUnit adjacent regression 63/63、backend full 3771/3771。该阶段只关闭 narrow Shield Wall representative evidence；完整 multi-battlefield movement、standby / quick timing windows、完整 FEPR、完整 PaymentEngine、control-zone movement 全矩阵、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-88 Malzahar Resource Skill Design Gate

阶段 4C-88 design gate 入口：`docs/CURRENT_STAGE4C_BATCH88_MALZAHAR_RESOURCE_SKILL_DESIGN_GATE.md`。本批记录 Malzahar / 玛尔扎哈 `FU-0f7cbe26ce` / `OGN·113/298` / cardId `31332` 不是 evidence-only 候选。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：该设计门禁建立时 `CardBehaviorRegistry` 与 fixture 只覆盖 Malzahar 普通手牌打出进基地；`P4ActivatedAbilityCatalog` 未登记 Malzahar；`CoreRuleEngine.ResolveActivateAbility` 没有 destroy-friendly-unit-or-equipment-as-cost / gain payment-only power-resource 分支。该未实现状态已被 4D-03I open-main representative focused slice supersede；4D-03J 已建立 spell-duel / reaction / payment-only lifecycle 实现交接，但功能尚未完成，完整 spell-duel / swift timing、reaction prohibition 与 payment-only lifecycle 仍未 full-official。

口径：本批不修改功能代码、不修改矩阵、不新增 representative evidence、不设置 `stage4C88`。完整 swift / spell-duel timing、resource restriction lifecycle、reaction target prohibition、PaymentEngine、hidden-info / redaction matrix、FAQ full adjudication、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-89 Vanilla Unit Evidence 审计

阶段 4C-89 审计入口：`docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH89_VANILLA_UNIT_EVIDENCE.md`。本批记录 Mountain Drake / 山脉亚龙 `FU-d635fc47f4` / `OGN·142/298` / cardId `31366` 与 Dockside Lurker / 船坞潜伏者 `FU-72ce6fb8a4` / `OGN·175/298` / cardId `31405` 的 vanilla source-unit representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·142/298` 为费用 9、0 目标、source unit to base power 10，登记 `OGN·175/298` 为费用 3、0 目标、source unit to base power 3。既有 fixtures 覆盖 ordinary hand play、base payment、zero-target stack、pass-pass resolution、源牌进入控制者基地并保持 `CARD_TYPE:UNIT`；generic vanilla source-unit target rejection 覆盖显式目标输入 no-mutation。

验证：focused vanilla source-unit / target rejection regression 305/305、source-unit / play-card / target / stack / priority / payment adjacent regression 1879/1879、backend full 3771/3771。该阶段只关闭 narrow vanilla unit representative evidence；其他 source-zone play routes、active-entry / keyword / movement / control-zone rules、完整 PaymentEngine / FEPR、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-90 Active Entry Unit Evidence 审计

阶段 4C-90 审计入口：`docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH90_ACTIVE_ENTRY_UNIT_EVIDENCE.md`。本批记录 Vanguard Squire / 先锋扈从 `FU-c1dc472304` / `OGS·016/024` / cardId `31595` 与 Aggressive Dragonhound / 好斗的龙犬 `FU-1207daea8f` / `SFD·006/221` / cardId `33078` 的 active-entry source-unit representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGS·016/024` 为费用 6、0 目标、source unit to base power 5、`精锐` 标签，登记 `SFD·006/221` 为费用 3、0 目标、source unit to base power 3、`犬形|龙` 标签。既有 fixtures 覆盖 ordinary hand play、base payment、zero-target stack、pass-pass resolution、源牌进入控制者基地、官方标签和 `IsExhausted=false`；active-entry source-unit target rejection 覆盖显式目标输入 no-mutation。

验证：focused active-entry source-unit / target rejection regression 24/24、source-unit / play-card / target / stack / priority / payment adjacent regression 1879/1879、backend full 3771/3771。该阶段只关闭 narrow active-entry source-unit representative evidence；其他 active-entry family、roam / battlefield movement、tap skills、attack triggers、完整 PaymentEngine / FEPR、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-91 Royal Guard Sand Soldier Evidence 审计

阶段 4C-91 审计入口：`docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH91_ROYAL_GUARD_SAND_SOLDIER_EVIDENCE.md`。本批记录 Royal Guard / 皇家守卫 `FU-29d76f0175` / `SFD·157/221` / cardId `33251` 的 source-unit + Sand Soldier token representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·157/221` 为费用 4、0 目标、source unit to base power 2、创建 1 名 2 战力 `黄沙士兵` 单位指示物且 token tags 为 `CARD_TYPE:UNIT|黄沙士兵`。既有 fixture 覆盖 ordinary hand play、base payment、zero-target stack、pass-pass resolution、源牌进入控制者基地、`UNIT_TOKEN_CREATED` 与最终 base 中源牌 + token；Royal Guard target rejection 覆盖显式目标输入 no-mutation。

验证：focused Royal Guard / Sand Soldier regression 10/10、source-unit / token / target / stack / priority / payment adjacent regression 1880/1880、backend full 3771/3771。该阶段只关闭 narrow Royal Guard representative evidence；精确“此处”目的地选择、完整 token factory、control-zone / battlefield 目的地矩阵、replacement / cleanup、完整 PaymentEngine / FEPR、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-92 Stern Sergeant Experience Evidence 审计

阶段 4C-92 审计入口：`docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH92_STERN_SERGEANT_EXPERIENCE_EVIDENCE.md`。本批记录 Stern Sergeant / 严厉军士 `FU-5f03740098` / `UNL-157/219` / cardId `34705` 的 source-unit + dynamic experience representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-157/219` 为费用 6、0 目标、source unit to base power 6、`精锐` 标签、结算后每名友方场上单位获得 1 经验。既有 fixtures 覆盖 ordinary hand play、base payment、zero-target stack、pass-pass resolution、源牌进入控制者基地、`EXPERIENCE_GAINED.amount=1`，以及已有友方场上单位动态计数为 3 且友方装备/敌方单位不计入；keyword source-unit target rejection 覆盖显式目标输入 no-mutation。

验证：focused Stern Sergeant / keyword source-unit regression 460/460、experience / source-unit / target / stack / priority / payment adjacent regression 1913/1913、backend full 3771/3771。该阶段只关闭 narrow Stern Sergeant representative evidence；battle / movement triggered experience branches、experience spending abilities、complete experience economy、完整 PaymentEngine / FEPR、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-93 Royal Attendant Legend Mode Evidence 审计

阶段 4C-93 审计入口：`docs/CURRENT_STAGE4C_BATCH93_ROYAL_ATTENDANT_LEGEND_MODE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH93_ROYAL_ATTENDANT_LEGEND_MODE_EVIDENCE.md`。本批记录 Royal Attendant / 皇家随从 `FU-92e31978af` / `SFD·039/221` / cardId `33115` 的 source-unit + legend mode representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·039/221` 为费用 3、source unit to base power 4，并提供 `READY_LEGEND` / `EXHAUST_LEGEND` 两个 `LEGEND` 目标 mode。既有 fixture、engine theory 与 Hub tests 覆盖 ordinary hand play、base payment、legend target、stack pass-pass resolution、源牌进入控制者基地、目标传奇活跃/休眠状态变更、ActionPrompt mode/target choices 与 invalid target rejection。

验证：focused Royal Attendant / legend mode regression 5/5、legend / source-unit / target / stack / priority / payment adjacent regression 1894/1894、backend full 3771/3771。该阶段只关闭 narrow Royal Attendant representative evidence；complete legend interaction domain、non-hand source-zone play routes、complete PaymentEngine / FEPR、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-94 Babbling Poro Predict Evidence 审计

阶段 4C-94 审计入口：`docs/CURRENT_STAGE4C_BATCH94_BABBLING_PORO_PREDICT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH94_BABBLING_PORO_PREDICT_EVIDENCE.md`。本批记录 Babbling Poro / 叨叨魄罗 `FU-677c27eea7` / `UNL-224/219` / cardId `34777` 的 source-unit + predict recycle representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-224/219` 为费用 2、`FriendlyMainDeckCard` target scope、`MainDeckLookCount=1`、回收所选主牌堆目标、source unit to base power 2、`魄罗|预知` 标签。既有 fixture 与 tests 覆盖 ordinary hand play、base payment、top main-deck target、stack pass-pass resolution、源牌进入控制者基地、`CARDS_RECYCLED` 将顶部牌移至牌堆底部，以及 outside-top-card target rejection。

验证：focused predict source-unit regression 12/12、predict / source-unit / target / stack / priority / payment adjacent regression 1830/1830、backend full 3771/3771。该阶段只关闭 narrow UNL Babbling Poro representative evidence；predict no-recycle / decline branch、complete main-deck look-window privacy matrix、non-hand source-zone play routes、complete PaymentEngine / FEPR、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-95 Static Effect Design Gate

阶段 4C-95 design gate 入口：`docs/CURRENT_STAGE4C_BATCH95_STATIC_EFFECT_DESIGN_GATE.md`。本批记录 Molten Drake / 熔浆巨龙 `FU-0973164d07`、Sona / 娑娜 `FU-c9bce10c0e`、Annie / 安妮 `FU-430074702b`、Gentle Gem Dragon / 温驯的宝石龙 `FU-af793555bb` 不是 evidence-only 候选。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：四张卡的现有 registry 只提供普通 source-unit-to-base 入场参数；对应 fixtures 和 `rules-evidence-index.md` 明确写着官方核心文本 deferred：其他友方单位活跃进场、回合结束活跃符文、法术/技能伤害 +1、龙单位打出后活跃符文。

口径：本批不修改功能代码、不修改测试代码、不修改前端代码、不修改覆盖矩阵、不新增 representative evidence；四个 FU 仍保持 `IMPLEMENTED_UNTESTED` / `NEEDS_AUTOMATED_TEST_EVIDENCE`，等待后续功能设计、服务端实现、prompt 契约与新测试。

## 2026-05-13 阶段 4C-96 Legacy Guard Evidence Alignment

阶段 4C-96 审计入口：`docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH96_LEGACY_GUARD_EVIDENCE_ALIGNMENT_EVIDENCE.md`。本批记录 Stage 4C-60 至 Stage 4C-69 已验证 direct-card representative guard evidence 的矩阵状态对齐。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch96LegacyGuardEvidenceAlignment`、`functionalUnits[].stage4C96` 与 `snapshotEntries[].stage4C96`，并将烈火风暴、过载能量、狩猎、大副、怒海大鲨炮、德玛西亚使节、提伯斯、泡泡机、宝藏魔像、忠实的工坊主共 10 个 FU 从 `IMPLEMENTED_UNTESTED` 对齐为 representative `IMPLEMENTED_TESTED`。

验证：focused legacy guard regression 67/67、adjacent legacy guard regression 193/193、backend full 3771/3771。该阶段只关闭代表性自动化证据状态缺口；完整 FEPR、PaymentEngine、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-97 Arena / Minion / Annie Evidence Alignment

阶段 4C-97 审计入口：`docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH97_ARENA_MINION_ANNIE_EVIDENCE_ALIGNMENT_EVIDENCE.md`。本批记录竞技场勤务小队、三张官方随从 token factory 与黑暗之女已验证代表路径的矩阵状态对齐。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch97ArenaServiceCrewMinionAnnieEvidenceAlignment`、`functionalUnits[].stage4C97` 与 `snapshotEntries[].stage4C97`，并将 `FU-d5e1143438` / 竞技场勤务小队、`FU-fe2295424f` / 随从（德玛西亚）、`FU-bf81e73326` / 随从（诺克萨斯）、`FU-77e07d2cad` / 随从（祖安）、`FU-4faaf1a186` / 黑暗之女从 `IMPLEMENTED_UNTESTED` 对齐为 representative `IMPLEMENTED_TESTED`。

验证：focused arena / minion / Annie regression 6/6、adjacent regression 87/87、backend full 3771/3771。该阶段只关闭代表性自动化证据状态缺口；完整 equipment trigger / token factory / legend action domain、FEPR、PaymentEngine、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-98 Battlefield Residual Evidence Alignment

阶段 4C-98 审计入口：`docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH98_BATTLEFIELD_RESIDUAL_EVIDENCE_ALIGNMENT_EVIDENCE.md`。本批记录力量方尖碑、荣耀竞技场、冰霜要塞已验证 battlefield rule-domain 代表路径的矩阵状态对齐。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 新增 `stage4CBatch98BattlefieldResidualEvidenceAlignment`、`functionalUnits[].stage4C98` 与 `snapshotEntries[].stage4C98`，并将 `FU-f91eded774` / 力量方尖碑、`FU-1d470821cb` / 荣耀竞技场、`FU-a47530ae04` / 冰霜要塞从 `IMPLEMENTED_UNTESTED` 对齐为 representative `IMPLEMENTED_TESTED`。

验证：focused battlefield residual regression 8/8、adjacent regression 87/87、backend full 3771/3771。该阶段只关闭代表性自动化证据状态缺口；完整 battlefield rule-domain matrix、battle / spell-duel / assign-combat-damage lifecycle、FEPR、PaymentEngine、LayerEngine、hidden-info / redaction、FAQ full adjudication、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-86 Imperial Shrine Conquer Sand Soldier 审计

阶段 4C-86 审计入口：`docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH86_IMPERIAL_SHRINE_CONQUER_SAND_SOLDIER_EVIDENCE.md`。本批记录 Imperial Shrine / 帝王神坛 `FU-ec31812b00` / `SFD·207/221` / cardId `33306` 的 battlefield rule-domain representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CoreRuleEngine` 已有 `TryResolveBattlefieldConquerPayOneReturnUnitCreateSandSoldierTrigger`，在征服 `SFD·207/221` 时可支付 1 法力、返回一个确定的受控战场单位到其拥有者手牌，并在战场区创建 `SFD·T02` / 2 战力 / 黄沙士兵 token。既有 tests 覆盖支付成功、无法力时不触发、不返回、不创建，以及 GameHub development seed 的 ActionPrompt / authoritative snapshot 路径。

验证：focused 3/3、BattlefieldConquer adjacent regression 45/45、backend full 3771/3771。该阶段只关闭 narrow Imperial Shrine representative evidence；完整 optional trigger prompt / decline、完整 PaymentEngine、完整 battlefield / spell-duel / battle lifecycle、multi-battlefield APNAP、FAQ p22 完整裁定、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-85 Rune Resource Domain 审计

阶段 4C-85 审计入口：`docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH85_RUNE_RESOURCE_DOMAIN_EVIDENCE.md`。本批记录 Rune Resource Domain / 符文资源域 `FU-0ec69ae7e6`、`FU-39041f4562` / `OGN·007/298`、`OGN·042/298` 的 red / blue basic rune representative payment-resource evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`OfficialRuleDomainBehaviorCatalog` 把官方符文卡映射为 `RUNE_RESOURCE_DOMAIN` 且符文卡不进入 direct `PLAY_CARD` registry；`RECYCLE_RUNE` prompt metadata 暴露 controlled-trait-base-rune resource policy；`PlayCardPaymentResourcePowerByChoiceForBehavior` 只从控制者基地中可回收且带 `COLOR:*` 的符文生成服务端支付资源候选，并公开 `trait` / `power: 1`。既有 tests 覆盖 red typed payment resource、partial spend power、red/blue metadata、wrong-trait rejection、generic mixed-trait payment、double-resource requirement、over-recycle no-mutation guard 与四个 Hub development seed。

验证：focused 10/10、rune resource / recycle / payment / ActionPrompt / Hub adjacent regression 240/240、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow red / blue rune resource-domain payment-resource evidence；完整 rune call / tap / recycle timing lifecycle、完整 PaymentEngine、替代/额外费用、reaction payment windows、hidden-info / redaction matrix、1009/811 full-official 仍为缺口。

## 2026-05-13 阶段 4C-84 Heimerdinger Source Unit 审计

阶段 4C-84 审计入口：`docs/CURRENT_STAGE4C_BATCH84_HEIMERDINGER_SOURCE_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH84_HEIMERDINGER_SOURCE_UNIT_EVIDENCE.md`。本批记录 Heimerdinger / 黑默丁格 `ARC-003/006`、`OGN·111/298` / cardIds `31571`、`31329` / `FU-02075a26e3` 的 shared ordinary source-unit-to-base representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记两张黑默丁格为 direct card behavior，费用 3、0 目标、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`、`SourceUnitTags: 约德尔人`；既有 fixtures 覆盖 ARC 与 OGN 普通手牌打出、支付 3、0 目标入栈、双方让过后源牌进入 P1 基地，成为 3 战力、带 `CARD_TYPE:UNIT|约德尔人`、未休眠的单位对象；active-entry / keyword-source-unit target rejection 覆盖两张共享条目带目标打出拒绝，official opening smoke 覆盖 ARC hand / card candidate 可见性。

验证：focused 484/484、source-unit / target / stack / priority / payment / activated-ability adjacent regression 1847/1847、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow shared Heimerdinger ordinary source-unit-to-base evidence；copied tap skills、static ability-copy model、FAQ p11/p22 review、PaymentEngine、FEPR、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-83 Mighty Faerie Source Unit 审计

阶段 4C-83 审计入口：`docs/CURRENT_STAGE4C_BATCH83_MIGHTY_FAERIE_SOURCE_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH83_MIGHTY_FAERIE_SOURCE_UNIT_EVIDENCE.md`。本批记录 Mighty Faerie / 大力仙灵 `SFD·125/221` / cardId `33215` / `FU-95b4531e4e` / `MIGHTY_FAERIE_MOVE_PAYMENT_PLAY_UNIT` 的 ordinary source-unit-to-base representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·125/221` 为 direct card behavior，费用 4、0 目标、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 4`、`SourceUnitTags: 仙灵`；既有 fixture 覆盖普通手牌打出、支付 4、0 目标入栈、双方让过后源牌进入 P1 基地，成为 4 战力、带 `CARD_TYPE:UNIT|仙灵`、未休眠的单位对象；P4 fixture 覆盖当前普通手牌打出路径携带显式目标时拒绝且 no-mutation。

验证：focused 460/460、keyword/source-unit / battlefield / movement / target / stack / priority / payment adjacent regression 2117/2117、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Mighty Faerie ordinary source-unit-to-base evidence；move-to-battlefield trigger、optional purple power payment、same-battlefield friendly-unit movement、control-zone movement、PaymentEngine、FEPR、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-82 Duel Mutual Power Damage 审计

阶段 4C-82 审计入口：`docs/CURRENT_STAGE4C_BATCH82_DUEL_MUTUAL_POWER_DAMAGE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH82_DUEL_MUTUAL_POWER_DAMAGE_EVIDENCE.md`。本批记录 Duel / 决斗 `OGN·128/298` / cardId `31352` / `FU-2779c06158` / `DUEL_MUTUAL_POWER_DAMAGE` 的 mutual current-power damage representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·128/298` 为 direct card behavior，费用 2、2 目标、`TargetScope: FriendlyThenEnemyUnits`、`DealsMutualTargetPowerDamage: true`；既有 fixture 覆盖普通手牌打出、支付 2、友方 4 战力单位 then 敌方 2 战力单位、双方让过后两次 `DAMAGE_APPLIED`，敌方单位因致命伤害进入弃牌堆，源法术进入弃牌堆；反向目标顺序拒绝有直接测试与 fixture。

验证：focused 3/3、Duel / mutual damage / target / stack / priority / damage / cleanup adjacent regression 1410/1410、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Duel representative mutual current-power damage evidence；complete battle / spell-duel lifecycle、LayerEngine、FEPR、replacement / prevention、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-81 Flame Chompers Source Unit 审计

阶段 4C-81 审计入口：`docs/CURRENT_STAGE4C_BATCH81_FLAME_CHOMPERS_SOURCE_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH81_FLAME_CHOMPERS_SOURCE_UNIT_EVIDENCE.md`。本批记录 Flame Chompers / 嚼火者手雷 `OGN·006/298` / cardId `31210` / `FU-af2c43c430` / `OGN_FLAME_CHOMPERS_DISCARD_ALT_PLAY_UNIT` 的 ordinary source-unit-to-base representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·006/298` 为 direct card behavior，费用 3、0 目标、`PlaysSourceToBaseAsUnit: true`、`SourceUnitPower: 3`；既有 fixture 覆盖普通手牌打出、支付 3、0 目标入栈、双方让过后源牌进入 P1 基地，成为 3 战力、仅带 `CARD_TYPE:UNIT`、未休眠的单位对象；通用 source-unit target rejection 覆盖该卡带目标打出拒绝，official opening smoke 覆盖 hand / card candidate 可见性。

验证：focused 306/306、source-unit / play-card / target / stack / priority / payment / discard / cleanup adjacent regression 1954/1954、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Flame Chompers ordinary source-unit-to-base evidence；discard replacement pay-red-power play path、cleanup replacement queue、PaymentEngine、FEPR、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-80 Bullet Time Power Damage 审计

阶段 4C-80 审计入口：`docs/CURRENT_STAGE4C_BATCH80_BULLET_TIME_POWER_DAMAGE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH80_BULLET_TIME_POWER_DAMAGE_EVIDENCE.md`。本批记录 Bullet Time / 弹幕时间 `OGN·268/298` / cardId `31511` / `FU-b646702ec0` / `BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT` 的 pay-power enemy battlefield damage representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·268/298` 为 direct card behavior，费用 1、0 目标、`DamagesAllEnemyBattlefieldUnits: true`、`DamageAmountFromOptionalPowerCost: true`；既有 fixture 覆盖普通手牌打出、支付 1 mana 与 `SPEND_POWER:3`、0 目标入栈、双方让过后对敌方战场单位造成 3 点伤害，敌方基地与己方战场单位不受影响，法术进入弃牌堆且符能池扣空；符能不足拒绝与 typed power / `RECYCLE_RUNE` 支付资源守卫均有直接测试。

验证：focused 24/24、payment / pay-cost / power / recycle / enemy battlefield damage / stack / priority adjacent regression 250/250、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Bullet Time representative power-spent damage evidence；complete `JFAQ-251023 p6`、battle / spell-duel lifecycle、PaymentEngine、FEPR、noncombat damage replacement / layer matrix、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-79 Forced Conscription Control Small Enemy Recall 审计

阶段 4C-79 审计入口：`docs/CURRENT_STAGE4C_BATCH79_FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH79_FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL_EVIDENCE.md`。本批记录 Forced Conscription / 强制征召 `UNL-140/219` / cardId `34683` / `FU-0681eefc4e` / `FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL` 的 control-small-enemy-and-recall representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-140/219` 为 direct card behavior，费用 5、1 目标、`TargetScope: EnemyBattlefieldUnit`、`MaxTargetPower: 3`、`TargetRequiredTag: CARD_TYPE:UNIT`、`GainsControlOfTargetToBase: true`、`ExhaustsControlledTarget: true`；既有 fixture 覆盖普通手牌打出、支付 5、目标为 3 战力敌方战场单位、双方让过后获得控制权、休眠并召回到控制者基地；target-power guard 与 dirty already-controlled enemy-zone target guard 均有直接测试。

验证：focused 18/18、control / battlefield / move / target / stack / priority / payment adjacent regression 1718/1718、backend full 3754/3754、frontend build passed、Chrome smoke rerun passed。该阶段只关闭 narrow Forced Conscription representative control-to-base evidence；optional 5 experience branch、complete owner/controller model、complete control-zone movement matrix、PaymentEngine optional costs、complete FEPR target matrix、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-78 Moon Rise Power Minus 2 审计

阶段 4C-78 审计入口：`docs/CURRENT_STAGE4C_BATCH78_MOON_RISE_POWER_MINUS_2_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH78_MOON_RISE_POWER_MINUS_2_EVIDENCE.md`。本批记录 Moon Rise / 月之降临 `UNL-198/219` / cardId `34751` / `FU-4329e00e20` / `MOON_RISE_ENEMY_BATTLEFIELD_MINUS_2_NO_MOVE` 的 enemy battlefield -2 until-end-of-turn / skipped optional movement / negative-power boundary representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-198/219` 为 direct card behavior，费用 3、0 目标、`PowerModifierAmount: -2`、`ModifiesAllEnemyBattlefieldUnits: true`；既有 fixture 覆盖普通手牌打出、支付 3、0 目标 stack、双方让过后敌方战场单位 -2，且明确当前单战场区域模型下跳过可选敌方移动；负战力边界保留为 1 战力敌方战场单位变为 -1。

验证：focused 5/5、power modifier / negative-power / cleanup / combat damage / stack adjacent regression 196/196、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Moon Rise representative enemy battlefield -2 evidence；multi-battlefield selection、optional enemy movement、complete control-zone movement matrix、complete cleanup replacement / duration-effect matrix、battle / spell-duel lifecycle、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-77 Syndra Spell Duel Echo Static 审计

阶段 4C-77 审计入口：`docs/CURRENT_STAGE4C_BATCH77_SYNDRA_SPELL_DUEL_ECHO_STATIC_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH77_SYNDRA_SPELL_DUEL_ECHO_STATIC_EVIDENCE.md`。本批记录 Syndra / 辛德拉 `UNL-146/219` / cardId `34691` / `FU-bf350b5796` / `SYNDRA_SPELL_DUEL_ECHO_STATIC` 的 ordinary play-to-base unit / shared target rejection representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-146/219` 为 direct card behavior，费用 6、0 目标、源牌入基地成为 6 战力 `CARD_TYPE:UNIT` 单位对象；既有 fixture 覆盖普通手牌打出到基地单位对象且明确 defer spell-duel detection / Echo grant / repeated spell effects；共享 source-unit target rejection 测试覆盖带目标打出 no-mutation guard。

验证：focused 361/361、SpellDuel / Echo / Payment / Stack / Priority adjacent regression 553/553、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Syndra representative ordinary unit-entry evidence；actual spell-duel detection、Echo 2 purple grant、granted Echo payment/repeat、complete spell-duel lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-76 Long Sword Equipment 审计

阶段 4C-76 审计入口：`docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH76_LONG_SWORD_EQUIPMENT_EVIDENCE.md`。本批记录 Long Sword / 长剑 `SFD·022/221` / cardId `33095` / `FU-5accdd09f9` / `LONG_SWORD_AGILE_PLAY_EQUIPMENT` 的 equipment play / explicit-target rejection / minimal assemble identity representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·022/221` 为 direct card behavior，费用 2、0 目标、`PlaysSourceToBaseAsEquipment: true`、`SourceEquipmentTags: 武装|灵便`；既有 fixture 覆盖普通手牌打出到基地装备对象、显式目标拒绝 no-mutation、最小 `ASSEMBLE_RED` 贴附和 owner/controller 身份保持；随动 / 脱离 fixtures 仅作为邻接回归，不扩展为完整装备生命周期声明。

验证：focused 11/11、equipment / attach / move adjacent regression 336/336、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Long Sword representative equipment evidence；Agile reaction attach、complete `ASSEMBLE_EQUIPMENT` lifecycle、equipment movement / detach destination matrix、LayerEngine equipment modifiers、PaymentEngine beyond represented routes、complete FEPR target matrix、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-75 Gentleman Duel Damage 审计

阶段 4C-75 审计入口：`docs/CURRENT_STAGE4C_BATCH75_GENTLEMAN_DUEL_DAMAGE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH75_GENTLEMAN_DUEL_DAMAGE_EVIDENCE.md`。本批记录 Gentleman Duel / 绅士决斗 `OGS·008/024` / cardId `31587` / `FU-265c03a141` / `GENTLEMAN_DUEL_POWER_PLUS_3_THEN_MUTUAL_POWER_DAMAGE` 的 power +3 then mutual current-power damage representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGS·008/024` 为 direct card behavior，费用 6、`TargetScope: FriendlyThenEnemyUnits`、`PowerModifierAmount: 3`、`DealsMutualTargetPowerDamage: true`；既有 fixture 覆盖 +3 power modifier 在互伤计算前生效、两条 damage events、敌方目标致命伤害摧毁与 owner graveyard placement；Duel sibling tests 覆盖共享 target-order guard。

验证：focused 6/6、mutual damage / damage / cleanup adjacent regression 203/203、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Gentleman Duel representative evidence；Swift / spell-duel timing、complete FEPR target matrix、LayerEngine / duration cleanup、damage prevention / replacement、battle damage assignment lifecycle、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-74 Sivir Haste 审计

阶段 4C-74 审计入口：`docs/CURRENT_STAGE4C_BATCH74_SIVIR_HASTE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH74_SIVIR_HASTE_EVIDENCE.md`。本批记录 Sivir / 希维尔 `SFD·143/221`、`SFD·143a/221` / cardIds `33234`、`33235` / `FU-5bcc4063c2` / `SIVIR_PLAY_UNIT_NO_OPTIONAL_HASTE` 与 `SIVIR_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE` 的 no-optional Haste play-unit 与 `HASTE_READY` optional-cost representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记两张 Sivir 为 direct card behavior，费用 4、0 目标、源牌入基地成为 4 战力 `急速` 单位，并记录代表 `HASTE_READY` 额外 1 mana + 1 purple power；既有 fixtures 覆盖 no-optional route 与 HASTE_READY active-entry route；既有测试覆盖 unexpected target rejection 与 wrong-trait power no-mutation rejection。

验证：focused 78/78、haste / payment / resource adjacent regression 103/103、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow Sivir Haste representative evidence；完整 PaymentEngine、wild-rune count、+2 power / Roam branch、LayerEngine / continuous effects、cleanup queue / replacement effects、control-zone movement、full FEPR timing / targeting、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-73 Plunder Alley Battlefield 审计

阶段 4C-73 审计入口：`docs/CURRENT_STAGE4C_BATCH73_PLUNDER_ALLEY_BATTLEFIELD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH73_PLUNDER_ALLEY_BATTLEFIELD_EVIDENCE.md`。本批记录 Plunder Alley / 劫掠船巷 `OGN·285/298` / cardId `31530` / `FU-90673ef9fd` / `BATTLEFIELD_RULE_DOMAIN` 的 battlefield defend move-to-base representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`BehaviorSpecCatalog` 已把 `OGN·285/298` 纳入 implemented battlefield rule card；`CoreRuleEngine` 已识别它为 `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` source；`MatchSession` development seed `battlefield-defend-move-to-base` 使用 `P2-BATTLEFIELD-PLUNDER-ALLEY`，并在 prompt 中暴露战场 destination 与防守单位 target choice；既有 core / Hub tests 覆盖合法触发、非法多目标中文拒绝、合法 snapshot 区域变化与脏 attacker-controlled battlefield source rejection。

验证：focused 3/3、battlefield adjacent regression 137/137、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow battlefield-domain representative evidence；完整 `BATTLEFIELD_RULE_DOMAIN`、完整 `JFAQ-251023 p5-p6` 战场生命周期 / 清理裁定、battle / spell-duel / assign-combat-damage lifecycle、control freeze/release 与 zone movement matrix、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-72 Hextech Ray Damage 审计

阶段 4C-72 审计入口：`docs/CURRENT_STAGE4C_BATCH72_HEXTECH_RAY_DAMAGE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH72_HEXTECH_RAY_DAMAGE_EVIDENCE.md`。本批记录 Hextech Ray / 海克斯射线 `OGN·009/298` / cardId `31215` / `FU-441cb9fb7f` / `HEXTECH_RAY_DAMAGE_3` 的 ordinary hand play / pay 1 / battlefield-unit target / stack pass-pass / 3 damage / end-turn damage cleanup / Swift spell-duel focus representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·009/298` 为 direct card behavior，包含 `Cost: 1`、`TargetCount: 1`、`DamageAmount: 3`、`CanPlayDuringSpellDuel: true`；既有 fixtures 覆盖 ordinary damage stack、`END_TURN` damage cleanup 与 Swift spell-duel focus route；`CoreRuleEngineRejectsBattlefieldOnlySpellWhenTargetIsBaseUnit` 已记录基地单位目标拒绝 no-mutation guard。

验证：focused 4/4、damage / Swift / cleanup regression 202/202、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；完整 FAQ 裁定、complete FEPR target / stack lifecycle、全部 timing windows、PaymentEngine、damage prevention / replacement / lethal cleanup / trigger matrix、hidden-info / redaction matrix、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-71 Diana Spell Duel Static 审计

阶段 4C-71 审计入口：`docs/CURRENT_STAGE4C_BATCH71_DIANA_SPELL_DUEL_STATIC_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH71_DIANA_SPELL_DUEL_STATIC_EVIDENCE.md`。本批记录 Diana / 黛安娜 `UNL-079/219` / cardId `34612` / `FU-4215291160` / `DIANA_SPELL_DUEL_INSIGHT_STATIC` 的 ordinary hand play / pay 3 / zero-target stack / source-to-base 3-power `CARD_TYPE:UNIT|巨神峰` representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-079/219` 为 direct card behavior，包含 `Cost: 3`、`TargetCount: 0`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`、`SourceUnitTags: 巨神峰`；`p2-preflight-play-diana-spell-duel-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期；既有 parameterized tests 覆盖合法 source-to-base unit route 与 unexpected-target no-mutation rejection。`UNL-079a/219` / `FU-085a7d6c4b` 本批只作为相邻回归，不贴 `stage4C71` overlay。

验证：keyword-source / rejection regression 459/459、spell duel / insight regression 45/45、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；actual spell-duel Insight trigger / payment / reveal / draw、alt A FU 覆盖、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、LayerEngine、hidden-info / top-card redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-70 Skullcrack Battlefield Stun 审计

阶段 4C-70 审计入口：`docs/CURRENT_STAGE4C_BATCH70_SKULLCRACK_BATTLEFIELD_STUN_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH70_SKULLCRACK_BATTLEFIELD_STUN_EVIDENCE.md`。本批记录 Skullcrack / 强手裂颅 `OGN·220/298` / cardId `31458` / `FU-ee886701e4` / `SKULLCRACK_STUN_FRIENDLY_AND_ENEMY_BATTLEFIELD_UNITS` 的 ordinary hand play / pay 2 / friendly battlefield unit then enemy battlefield unit target order / stack pass-pass / apply `STUNNED` to both targets representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·220/298` 为 direct card behavior，包含 `Cost: 2`、`TargetCount: 2`、`StatusEffectId: STUNNED`、`TargetScope: FriendlyBattlefieldThenEnemyBattlefieldUnits`；既有 fixture 与 targeted tests 覆盖合法 two-target stun route、错误目标顺序、友方基地目标、敌方基地目标与同阵营第二目标 no-mutation rejection。

验证：focused 2/2、stun / battlefield status regression 64/64、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；same-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR target / stack lifecycle、PaymentEngine、status duration / cleanup / replacement / prevention、LayerEngine、hidden-info / redaction matrix、完整 `SOUL-JFAQ-260114 p23` 裁定、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-69 Faithful Craftsman Create Minion 审计

阶段 4C-69 审计入口：`docs/CURRENT_STAGE4C_BATCH69_FAITHFUL_CRAFTSMAN_CREATE_MINION_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH69_FAITHFUL_CRAFTSMAN_CREATE_MINION_EVIDENCE.md`。本批记录 Faithful Craftsman / 忠实的工坊主 `OGN·211/298` / cardId `31447` / `FU-2e2a00f575` / `FAITHFUL_CRAFTSMAN_PLAY_UNIT_CREATE_MINION` 的 ordinary hand play / pay 3 / zero-target stack / source-to-base unit / one ready Minion unit token representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGN·211/298` 为 direct card behavior，包含 `CreatedBaseUnitTokenCount: 1`、`CreatedBaseUnitTokenPower: 1`、`CreatedBaseUnitTokenName: 随从`、`CreatedBaseUnitTokenTags: CARD_TYPE:UNIT`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 2`；`CoreRuleEngine` 已在 created unit token route 对 `随从` token 追加 `TOKEN_FAMILY:MINION`；既有 fixture 与 targeted tests 覆盖合法 create-Minion route 与 unexpected-target no-mutation rejection。

验证：focused 2/2、Minion-token regression 18/18、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；all Minion-token creation cards / alternate token counts、destination selection、full token subtype/family taxonomy、PaymentEngine、token replacement / prevention / cleanup、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-68 Treasure Golem Create Four Gold 审计

阶段 4C-68 审计入口：`docs/CURRENT_STAGE4C_BATCH68_TREASURE_GOLEM_CREATE_FOUR_GOLD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH68_TREASURE_GOLEM_CREATE_FOUR_GOLD_EVIDENCE.md`。本批记录 Treasure Golem / 宝藏魔像 `SFD·174/221` / cardId `33270` / `FU-7472703e56` / `TREASURE_GOLEM_PLAY_UNIT_CREATE_FOUR_GOLD` 的 ordinary hand play / pay 8 / zero-target stack / source-to-base unit / four exhausted Gold equipment tokens representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·174/221` 为 direct card behavior，包含 `CreatedBaseEquipmentTokenCount: 4`、`CreatedBaseEquipmentTokenName: 金币`、`CreatedBaseEquipmentTokenTags: CARD_TYPE:EQUIPMENT`、`CreatedBaseEquipmentTokenIsExhausted: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 9`；既有 fixture 与 targeted tests 覆盖合法 create-four-Gold route 与 unexpected-target no-mutation rejection。

验证：focused 3/3、Gold-token regression 30/30、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；all Gold-token creation cards / alternate token counts、destination selection、Gold equipment spend / activation / extra-mana interactions、PaymentEngine、equipment cleanup / replacement、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-67 Bubblebot Ready Friendly Mechanical 审计

阶段 4C-67 审计入口：`docs/CURRENT_STAGE4C_BATCH67_BUBBLEBOT_READY_FRIENDLY_MECHANICAL_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH67_BUBBLEBOT_READY_FRIENDLY_MECHANICAL_EVIDENCE.md`。本批记录 Bubblebot / 泡泡机 `SFD·062/221` / cardId `33142` / `FU-3f5a9ef0e0` / `BUBBLEBOT_PLAY_UNIT_READY_FRIENDLY_MECHANICAL` 的 ordinary hand play / pay 3 / friendly public Mechanical target / source-to-base unit / ready target representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `SFD·062/221` 为 direct card behavior，包含 `TargetScope: FriendlyUnit`、`TargetRequiredTag: CARD_TYPE:UNIT|机械`、`ReadiesTarget: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`；既有 fixture 与 targeted tests 覆盖合法机械目标 ready 与非机械目标 no-mutation rejection。

验证：focused 2/2、mechanical-ready regression 32/32、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；all friendly Mechanical ready cards / modes、FriendlyUnit target-scope matrix、multi-battlefield precision、PaymentEngine、readiness replacement/prevention、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-66 Tibbers All Battlefield Damage 审计

阶段 4C-66 审计入口：`docs/CURRENT_STAGE4C_BATCH66_TIBBERS_ALL_BATTLEFIELD_DAMAGE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH66_TIBBERS_ALL_BATTLEFIELD_DAMAGE_EVIDENCE.md`。本批记录 Tibbers / 提伯斯 `OGS·018/024` / cardId `31597` / `FU-c168bd394c` / `TIBBERS_PLAY_UNIT_DAMAGE_ALL_BATTLEFIELD_UNITS_3` 的 ordinary hand play / pay 8 / zero-target stack / source-to-base unit / damage all public battlefield units 3 representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `OGS·018/024` 为 direct card behavior，包含 `PlaysSourceToBaseAsUnit`、`SourceUnitPower: 7`、`DamagesAllBattlefieldUnits: true`、`DamageAmount: 3`；既有 fixture 与 targeted tests 覆盖 ordinary route、非单位战场对象跳过与 unexpected target rejection。

验证：focused 3/3、battlefield damage regression 63/63、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；all battlefield-wide damage cards / amounts、damage prevention / replacement / cleanup、lethal triggers、multi-battlefield precision、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-65 Demacia Envoy Experience Static 审计

阶段 4C-65 审计入口：`docs/CURRENT_STAGE4C_BATCH65_DEMACIA_ENVOY_EXPERIENCE_STATIC_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH65_DEMACIA_ENVOY_EXPERIENCE_STATIC_EVIDENCE.md`。本批记录 Demacia Envoy / 德玛西亚使节 `UNL-092/219` / cardId `34630` / `FU-d68c203b01` / `DEMACIA_ENVOY_PLAY_UNIT_GAIN_EXPERIENCE_STATIC` 的 ordinary hand play / pay 2 / zero-target stack / source-to-base unit / gain 1 experience representative evidence。项目仍 **NOT READY**，`fullOfficial=false`。

证据点：本批不修改功能代码；`CardBehaviorRegistry` 已登记 `UNL-092/219` 为 direct card behavior，`p2-preflight-play-demacia-envoy-experience-static.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期，既有 `ConformanceFixtureRunnerTests` 与 P4 fixed experience test 覆盖该路线。

验证：focused 4/4、experience regression 37/37、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative evidence；all experience cards / amounts、experience payment / optional costs、level-up LayerEngine、battlefield Hunt experience、ability experience、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-64 EnemyBattlefieldUnit Target Scope Guard 审计

阶段 4C-64 审计入口：`docs/CURRENT_STAGE4C_BATCH64_ENEMY_BATTLEFIELD_UNIT_TARGET_SCOPE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH64_ENEMY_BATTLEFIELD_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md`。本批记录 Megashark Cannon / 怒海大鲨炮 `OGN·092/298` / cardId `31310` / `FU-6d67456a80` / `MEGASHARK_CANNON_PLAY_UNIT_DAMAGE_6_ENEMY_BATTLEFIELD` 的 ordinary hand play / pay 6 / enemy public battlefield unit target-scope damage guard。项目仍 **NOT READY**，`fullOfficial=false`。

修复点：`CardTargetScopes.EnemyBattlefieldUnit` 现在只接受敌方公开战场单位，排除 equipment / spell / rune、face-down / standby、dirty controller、friendly battlefield、enemy base、hand / stale targets。新增 `EnemyBattlefieldUnitTargetScopeGuardTests` 覆盖 valid target damage、invalid target no-mutation 与无 `TargetRequiredTag` 的 EnemyBattlefieldUnit non-unit regression。

验证：focused 18/18、target regression 82/82、backend full 3754/3754、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative guard；composite target scopes、all EnemyBattlefieldUnit card texts / modes、multi-battlefield precision、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 2026-05-13 阶段 4C-63 AnyUnit Target Scope Guard 审计

阶段 4C-63 审计入口：`docs/CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH63_ANY_UNIT_TARGET_SCOPE_GUARD_EVIDENCE.md`。本批记录 First Mate / 大副 `OGN·132/298` / cardId `31356` / `FU-abf504d74e` / `FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT` 的 ordinary hand play / pay 3 / public AnyUnit target-scope ready guard。项目仍 **NOT READY**，`fullOfficial=false`。

修复点：`CardTargetScopes.AnyUnit` 现在只接受由所在区域玩家控制的公开场上单位，排除 equipment / spell / rune、face-down / standby、dirty controller、hand / stale targets；同时保留 trait-only unit object 兼容性，避免破坏 Mirror Image 现有代表 fixture。新增 `AnyUnitTargetScopeGuardTests` 覆盖 valid target resolution、invalid target no-mutation 与无 `TargetRequiredTag` 的 AnyUnit non-unit regression。

验证：focused 15/15、target regression 16/16、backend full 3742/3742、frontend build passed、Chrome smoke passed。该阶段只关闭 narrow representative guard；all AnyUnit card texts / modes、multi-battlefield precision、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 与 formal 18-step E2E 仍为缺口。

## 总结论

结论：**NOT READY**

当前服务端已经具备产品原型可用的联机房间、服务端权威提交、按玩家视角发送 snapshot/prompt、动作幂等、开发场景、相当数量的代表性卡牌效果和 conformance fixture 覆盖。但如果按自查文档的最终门槛判断为“完整符合官方核心规则、所有官方卡牌均可页面操作且不误导为 CONFORMANCE_PASS”，目前仍存在 P0 级缺口。

最关键的结论是：当前实现更接近“代表性规则引擎 + 大量 fixture 与产品 UI smoke”，还不是完整官方规则状态机。官方 deck/opening/mulligan 与官方构筑负例矩阵、对象位置、typed 符能、窗口状态、持续效果视图、关键词覆盖报告、spectator replay redaction 和 replay 状态 hash 已有服务端路径；但完整战场控制/待命任务状态机、通用清理任务队列、法术对决/战斗完整生命周期、全路径官方费用模型、完整触发引擎、连续效果 LayerEngine 与逐关键词/逐卡牌完整执行仍需要补齐。

## 2026-05-13 阶段 4C-62 Hunt Ready Friendly Units Guard 审计

阶段 4C-62 审计入口：`docs/CURRENT_STAGE4C_BATCH62_HUNT_READY_FRIENDLY_UNITS_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH62_HUNT_READY_FRIENDLY_UNITS_GUARD_EVIDENCE.md`。本批记录 Hunt / 狩猎 `SFD·204/221` / cardId `33303` / `FU-f877e60407` / `HUNT_READY_ALL_FRIENDLY_UNITS` 的 ordinary hand play / pay 1 / zero-target friendly public field-unit ready guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 1 mana，不提交显式目标，stack / pass-pass 后 friendly public field units 变为活跃。
- Guard：Core all-friendly ready resolution 已补 public field-unit enumeration；friendly battlefield equipment / spell / rune、face-down standby、dirty controller objects 与 enemy units 不会被错误 readied。
- Command：任何显式 target 均 `INVALID_TARGET` 且 no mutation。
- Validation：focused 10/10 passed；Hunt / ready / field-unit regression 121/121 passed；backend full 3731/3731 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Hunt representative ready-all-friendly-units guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、PaymentEngine、readiness duration / replacement / prevention interactions、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-61 Overcharged Energy Field Unit Guard 审计

阶段 4C-61 审计入口：`docs/CURRENT_STAGE4C_BATCH61_OVERCHARGED_ENERGY_FIELD_UNIT_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH61_OVERCHARGED_ENERGY_FIELD_UNIT_GUARD_EVIDENCE.md`。本批记录 Overcharged Energy / 过载能量 `OGN·123/298` / cardId `31345` / `FU-b2e0e1d8da` / `OVERCHARGED_ENERGY_EXHAUST_ALL_FRIENDLY_DAMAGE_ALL_BATTLEFIELD_12` 的 ordinary hand play / pay 7 / zero-target friendly public field-unit exhaust and public battlefield-unit damage guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 7 mana，不提交显式目标，stack / pass-pass 后 friendly public field units 横置，public battlefield units 各受到 12 点伤害。
- Guard：Core all-friendly unit exhaust 与 all-battlefield unit damage resolution 已补 public unit enumeration；equipment / spell / rune、face-down standby、dirty controller objects 与 base units 不会被错误卷入对应 effect path。
- Command：任何显式 target 均 `INVALID_TARGET` 且 no mutation。
- Validation：focused 12/12 passed；Overcharged / Tibbers / BladeWhirlwind / battlefield damage regression 53/53 passed；backend full 3722/3722 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Overcharged Energy representative field-unit guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 damage prevention / replacement / cleanup、lethal-damage trigger interactions、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-60 Firestorm Enemy Battlefield Damage Guard 审计

阶段 4C-60 审计入口：`docs/CURRENT_STAGE4C_BATCH60_FIRESTORM_ENEMY_BATTLEFIELD_DAMAGE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH60_FIRESTORM_ENEMY_BATTLEFIELD_DAMAGE_GUARD_EVIDENCE.md`。本批记录 Firestorm / 烈火风暴 `OGS·002/024` / cardId `31581` / `FU-fe9dbeea3d` / `FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3` 的 ordinary hand play / pay 6 / zero-target enemy public battlefield unit damage guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 6 mana，不提交显式目标，stack / pass-pass 后 enemy public battlefield units 各受到 3 点伤害。
- Guard：Core enemy battlefield unit damage resolution 已补 unit-only enumeration；enemy battlefield equipment / spell / rune、face-down standby、dirty controller objects、friendly battlefield units 与 enemy base units 均不会被该 effect path 伤害。
- Command：任何显式 target 均 `INVALID_TARGET` 且 no mutation。
- Validation：focused 13/13 passed；Firestorm / CrescentStrike / BulletTime / enemy battlefield damage regression 36/36 passed；backend full 3711/3711 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Firestorm representative enemy battlefield damage guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 damage prevention / replacement / cleanup、lethal-damage trigger interactions、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-59 Zenith Blade Enemy Battlefield Stun Guard 审计

阶段 4C-59 审计入口：`docs/CURRENT_STAGE4C_BATCH59_ZENITH_BLADE_ENEMY_BATTLEFIELD_STUN_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH59_ZENITH_BLADE_ENEMY_BATTLEFIELD_STUN_GUARD_EVIDENCE.md`。本批记录 Zenith Blade / 天顶之刃 `OGN·262/298` / cardId `31504` / `FU-64a7f67581` / `ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE` 的 ordinary hand play / pay 3 / enemy public battlefield unit stun target guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 3 mana，选择 enemy public battlefield unit，stack / pass-pass 后目标获得 `STUNNED` until end of turn。
- Guard：Core Zenith Blade target validation 已补 enemy public battlefield unit guard；enemy battlefield equipment / spell / rune、face-down standby、stale、base、hand、friendly、dirty controller targets 均 `INVALID_TARGET` 且 no mutation。
- Prompt：`MatchSession` 既有 `EnemyBattlefieldUnit` prompt path 已过滤 visible enemy field unit；新增 prompt test 锁定 `targetChoicesByIndex` 不暴露 non-unit / hidden / dirty / friendly / out-of-zone targets。
- Validation：focused 15/15 passed；ZenithBlade / Stun / ActionPrompt / Prompt regression 154/154 passed；backend full 3701/3701 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Zenith Blade representative enemy battlefield stun target guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 optional friendly unit movement、precise multi-battlefield destination selection、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、status duration cleanup / replacement / prevention interactions、full Spellshield tax matrix、PaymentEngine、LayerEngine / effective power and duration ordering、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-58 Spirit Fire Total Power Target Guard 审计

阶段 4C-58 审计入口：`docs/CURRENT_STAGE4C_BATCH58_SPIRIT_FIRE_TOTAL_POWER_TARGET_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH58_SPIRIT_FIRE_TOTAL_POWER_TARGET_GUARD_EVIDENCE.md`。本批记录 Spirit Fire / 妖异狐火 `OGN·256/298` / cardId `31498` / `FU-a9dc3495e1` / `SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4` 的 ordinary hand play / pay 3 / public battlefield unit targets / total target power <= 4 representative guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 3 mana，选择 public battlefield unit targets，提交组合总战力不高于 4，stack / pass-pass 后目标进入 owner graveyard。
- Guard：Core Spirit Fire target validation 已补 non-unit battlefield object guard；total power > 4、battlefield equipment / spell / rune、face-down standby、stale、base、hand、dirty controller targets 均 `INVALID_TARGET` 且 no mutation。
- Prompt：`MatchSession` 已同步过滤 Spirit Fire `targetChoicesByIndex` 与 `legalTargetSelections`，目标候选排除 non-unit / hidden / dirty / out-of-zone，合法组合继续由服务端 total-power guard 约束。
- Validation：focused 48/48 passed；ActionPrompt / Prompt / SpiritFire regression 112/112 passed；backend full 3690/3690 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Spirit Fire representative total-power target guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 same-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、full destroy / cleanup / replacement / prevention / Last Breath interactions、full Spellshield tax matrix、PaymentEngine、LayerEngine / effective power、hidden-info / redaction matrix、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-57 Reflections Swap Draw Guard 审计

阶段 4C-57 审计入口：`docs/CURRENT_STAGE4C_BATCH57_REFLECTIONS_SWAP_DRAW_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH57_REFLECTIONS_SWAP_DRAW_GUARD_EVIDENCE.md`。本批记录 Reflections / 镜中幻影 `UNL-083/219` / cardId `34618` / `FU-f0eb0fb704` / `REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1` 的 test-only ordinary hand play / pay 2 / two friendly public field units in different represented positions / at least one Ephemeral / swap locations / draw 1 representative guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD`，支付 2 mana，选择两个友方 public field unit，代表位置覆盖 base + battlefield，至少一个目标带 `瞬息`，stack / pass-pass 后 `UNIT_LOCATIONS_SWAPPED` 且抽 1。
- Guard：Core `SwapsTargetLocations` command validation 已补 same-position guard；no-Ephemeral、same-position、friendly equipment / spell / rune、face-down standby、stale、enemy、dirty controller targets 均 `INVALID_TARGET` 且 no mutation。
- Prompt：`MatchSession` 已将 `AnyTargetRequiredTag` 与 `SwapsTargetLocations` 纳入 server target-selection constraint，`legalTargetSelections` 只包含 Ephemeral-qualified different-position pairs。
- Validation：focused 54/54 passed；ActionPrompt / Prompt / Sand Soldiers / Reflections regression 112/112 passed；backend full 3679/3679 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Reflections representative swap/draw guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 exact multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR targeting / stack lifecycle、full movement / control-zone lifecycle、owner/controller split across all zones、hidden-info / redaction matrix、full target prompt matrix、PaymentEngine beyond ordinary pay 2、Ephemeral lifecycle、draw replacement / deck exhaustion、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E。

## 2026-05-12 阶段 4C-56 Secret Art! Mercy Boon Guard 审计

阶段 4C-56 审计入口：`docs/CURRENT_STAGE4C_BATCH56_SECRET_ART_MERCY_BOON_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH56_SECRET_ART_MERCY_BOON_GUARD_EVIDENCE.md`。本批记录 Secret Art! Mercy / 秘奥义！慈悲度魂落 `OGN·053/298` / cardId `31265` / `FU-3461727400` / `SECRET_ART_MERCY_GRANT_BOON_NO_GLOBAL_BONUS` 的 test-only ordinary hand play / pay 3 / friendly unit grant Boon +1 / friendly Spellshield no-tax representative guard。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 选择一名友方单位，支付 3 mana，stack / pass-pass 后给予目标 `增益` 标签与 permanent +1；友方法盾目标记录 `spellshieldTaxMana = 0` / no tax。
- Guard：Core `FriendlyUnit` target scope 已收紧为 visible field unit，拒绝 friendly equipment / spell / rune / face-down standby；prompt candidate 侧同步 visible-field-unit 口径，同时允许 legacy custom-tag public field unit。
- Validation：focused 87/87 passed；prompt / Sand Soldiers / FriendlyUnit regression 133/133 passed；backend full 3668/3668 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Secret Art! Mercy representative guard verified coverage，不提升 fullOfficial。
- 不实现 / 不宣称 full-official；standby / reaction、quick / spell-duel breadth、global all-boons extra +1 this turn、LayerEngine / duration cleanup、already-has-boon / stacking semantics（除非 B 仅覆盖 narrow edge）、full target matrix、full Spellshield tax、PaymentEngine、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E 均保持 open。

## 2026-05-12 阶段 4C-55 Vex Alt Spellshield Guard 审计

阶段 4C-55 审计入口：`docs/CURRENT_STAGE4C_BATCH55_VEX_ALT_SPELLSHIELD_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH55_VEX_ALT_SPELLSHIELD_GUARD_EVIDENCE.md`。本批只记录 Vex alt A / 薇古丝 `UNL-150a/219` / cardId `34698` / `FU-4d8ee1696b` / `VEX_ALT_A_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` 的 test-only spellshield / Yordle play-unit guard representative evidence，且与 E-owned coverage / matrix 工作解耦。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tags `CARD_TYPE:UNIT` + `法盾` + `约德尔人`。
- Guard：invalid source / target / timing 均 rejected，no mutation / no leak。
- Validation：focused 59/59 passed；backend full 3656/3656 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Vex alt A ordinary hand spellshield / Yordle play-unit guard representative evidence。
- 不实现 / 不宣称 opponent-unit stun / cannot-move runtime；opponent unit-play listener、battlefield-only condition、`STUNNED` application、cannot-move-this-turn duration、movement / control effects、Spellshield full target tax、FAQ adjudication、1009/811 full-official 与 formal 18-step E2E 均保持 open。

## 2026-05-12 阶段 4C-54 Void Burrower Legend Domain Guard 审计

阶段 4C-54 审计入口：`docs/CURRENT_STAGE4C_BATCH54_VOID_BURROWER_LEGEND_DOMAIN_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH54_VOID_BURROWER_LEGEND_DOMAIN_GUARD_EVIDENCE.md`。A 裁决本批不做 direct runtime implementation，只记录 Void Burrower / 虚空遁地兽 `SFD·187/221` / cardId `33285`、`SFD·243/221` / cardId `33354` / `FU-6e7d0dba2c` / `LEGEND_ACTION_DOMAIN` 的 representative automated evidence overlay。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：记录既有 automated evidence：active Void Burrower legend 在征服战场后可自动休眠，展示主牌堆顶部两张牌，存在单位时自动打出一张并回收其余；无可打出单位时回收两张；inactive legend guard 不触发。
- Validation：focused 32/32 passed；backend full 3650/3650 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Void Burrower `FU-6e7d0dba2c` representative automated evidence gap。
- 不做 / 不宣称 direct runtime implementation；不关闭 full-official NO-GO；LegendActivePredicate、LegendOptionalTrigger、RevealChoice、shared oracle mapping、hidden / reveal redaction matrix、optional trigger prompt / decline、free-play official semantics、recycle remainder official semantics、unit destination / zone ownership details、`ORDER_TRIGGERS` / battle lifecycle full matrix、FAQ adjudication、1009/811 full-official 与 formal E2E 均保持 open。

## 2026-05-12 阶段 4C-53 Sett Legend Domain Guard 审计

阶段 4C-53 审计入口：`docs/CURRENT_STAGE4C_BATCH53_SETT_LEGEND_DOMAIN_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH53_SETT_LEGEND_DOMAIN_GUARD_EVIDENCE.md`。A 裁决本批不做 direct runtime implementation，只记录 Sett / 腕豪 `OGN·269/298` / cardId `31512` / `FU-6308c2db01` / `LEGEND_ACTION_DOMAIN` 的 representative automated evidence overlay。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：记录既有 automated evidence：Sett 代表路径可自动替代带增益友方单位摧毁、支付 1 mana、消耗增益、以休眠状态召回到基地，并在征服战场时 ready 代表性 Sett legend。
- Validation：focused 54/54 passed；backend full 3647/3647 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Sett `FU-6308c2db01` representative automated evidence gap。
- 不做 / 不宣称 direct runtime implementation；不关闭 full-official NO-GO；LegendActivePredicate、LegendOptionalTrigger、ReplacementPayment、boon consume official semantics、dormant recall cleanup、conquest ready lifecycle full matrix、shared oracle mapping、`PAY_COST` prompt / decline、cleanup queue interactions、FAQ adjudication、1009/811 full-official 与 formal E2E 均保持 open。

## 2026-05-12 阶段 4C-52 Rek'Sai Haste / Overwhelm Guard 审计

阶段 4C-52 审计入口：`docs/CURRENT_STAGE4C_BATCH52_REKSAI_HASTE_OVERWHELM_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH52_REKSAI_HASTE_OVERWHELM_GUARD_EVIDENCE.md`。本批只记录 Rek'Sai / 雷克塞 `SFD·029/221` / cardId `33104`、`SFD·029a/221` / cardId `33105` / `FU-1945f6918c` 的 no-optional haste / overwhelm keyword ordinary hand play-unit + keyword tag guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand no-optional `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tags `CARD_TYPE:UNIT` + `强攻` + `急速`，two printings covered。
- Guard：invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- Validation：focused 305/305 passed；backend full 3641/3641 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Rek'Sai `SFD·029` / `SFD·029a` ordinary hand no-optional play-unit + keyword tag guard representative evidence。
- 不实现 / 不宣称 full-official haste / overwhelm runtime；`HASTE_READY` paid branch full matrix、red resource exactness、Overwhelm / 强攻 battle modifier、`ASSIGN_COMBAT_DAMAGE` overflow behavior、non-hand friendly unit gains haste、LayerEngine、hidden-info、FAQ refs、1009/811 full-official 与 formal 18-step E2E 均保持 open。

## 2026-05-11 阶段 4C-51 Rek'Sai Attack Reveal Guard 审计

阶段 4C-51 审计入口：`docs/CURRENT_STAGE4C_BATCH51_REKSAI_ATTACK_REVEAL_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH51_REKSAI_ATTACK_REVEAL_GUARD_EVIDENCE.md`。本批只记录 Rek'Sai / 雷克塞 `SFD·170/221` / cardId `33264`、`SFD·170a/221` / cardId `33265` / `FU-422b450261` 的 attack reveal / movement text ordinary hand play-unit guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 5，tag `CARD_TYPE:UNIT`，two printings covered。
- Guard：invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- Validation：focused 25/25 passed；backend full 3633/3633 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Rek'Sai `SFD·170` / `SFD·170a` ordinary hand play-unit guard representative evidence。
- 不实现 / 不宣称 attack reveal runtime 或 movement runtime；attack reveal runtime、top-2 reveal、free play、recycle remainder、unit destination to current battlefield / "here"、hidden-info redaction / reveal matrix、`ORDER_TRIGGERS`、battle lifecycle full matrix、movement / control-zone、FAQ refs、1009/811 full-official 与 formal 18-step E2E 均保持 open。

## 2026-05-11 阶段 4C-50 Draven Keyword Unit Guard 审计

阶段 4C-50 审计入口：`docs/CURRENT_STAGE4C_BATCH50_DRAVEN_KEYWORD_UNIT_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH50_DRAVEN_KEYWORD_UNIT_GUARD_EVIDENCE.md`。本批只记录 Draven / 德莱文 `SFD·148/221` / cardId `33240`、`SFD·148a/221` / cardId `33241` / `FU-104211dbbc` 的 keyword-unit combat text ordinary hand play-unit + `法盾` tag guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 6，tags `CARD_TYPE:UNIT` + `法盾`，two printings covered。
- Guard：invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- Validation：focused 17/17 passed；backend full 3625/3625 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Draven `SFD·148` / `SFD·148a` ordinary hand play-unit + `法盾` tag guard representative evidence。
- 不实现 / 不宣称 battle / scoring runtime；battle win scoring、destroyed-in-battle opponent scoring、Spellshield target tax、battle cleanup / score once-per-turn matrix、PaymentEngine、FAQ refs、1009/811 full-official 与 formal 18-step E2E 均保持 open。

## 2026-05-11 阶段 4C-49 Ezreal Play-Unit Guard 审计

阶段 4C-49 审计入口：`docs/CURRENT_STAGE4C_BATCH49_EZREAL_PLAY_UNIT_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH49_EZREAL_PLAY_UNIT_GUARD_EVIDENCE.md`。本批只记录 Ezreal / 伊泽瑞尔 `SFD·082/221` / cardId `33162`、`SFD·082a/221` / cardId `33163`、`SFD·082b/221·P` / cardId `33164` / `FU-2dca1ad450` 的 combat-damage text play-unit guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tag `CARD_TYPE:UNIT`，three printings covered。
- Guard：invalid target、wrong zone-source、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- Validation：focused 21/21 passed；backend full 3617/3617 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Ezreal ordinary hand play-unit + guard representative evidence。
- 不实现 / 不宣称 combat-damage / move runtime；attack / defense trigger、“此处” enemy unit target selection、damage equal to Ezreal power、cannot combat damage static、blue swift move to base、swift / reaction timing、blue payment / `PAY_COST`、movement / control-zone matrix、damage prevention / replacement / cleanup、Layer / effective power、FAQ refs、1009/811 full-official 与 final 18-step E2E 均保持 open。

## 2026-05-11 阶段 4C-48 Vex Spellshield Stun Guard 审计

阶段 4C-48 审计入口：`docs/CURRENT_STAGE4C_BATCH48_VEX_SPELLSHIELD_STUN_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH48_VEX_SPELLSHIELD_STUN_GUARD_EVIDENCE.md`。本批只记录 Vex / 薇古丝 `UNL-150/219` / cardId `34697` / `FU-9f7cb73dc4` / `VEX_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` 的 test-only spellshield guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tags `CARD_TYPE:UNIT` + `法盾` + `约德尔人`。
- Guard：invalid source / target / timing 均 rejected，no mutation / no leak。
- Validation：focused 35/35 passed；backend full 3607/3607 passed；frontend build passed；Chrome smoke passed。
- 本批只关闭 Vex ordinary hand spellshield-tag play-unit + guard representative evidence。
- 不实现 / 不宣称 opponent-unit stun runtime；opponent unit-play listener、battlefield-only condition、`STUNNED` application、cannot-move-this-turn duration、movement guard / cleanup、Spellshield full target tax、FAQ adjudication、1009/811 full-official 与 final 18-step E2E 均保持 open。

## 2026-05-11 阶段 4C-47 Draven Battle Body Guard 审计

阶段 4C-47 审计入口：`docs/CURRENT_STAGE4C_BATCH47_DRAVEN_BATTLE_BODY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH47_DRAVEN_BATTLE_BODY_GUARD_EVIDENCE.md`。本批已补 Draven / 德莱文 `SFD·020/221` / cardId `33092`、`SFD·020a/221` / cardId `33093` / `FU-964b214448` 的 battle body / play-unit guard representative slice。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tag `CARD_TYPE:UNIT`。
- Guard：invalid target、wrong zone、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- B 新增 `tests/Riftbound.ConformanceTests/DravenVanillaGuardTests.cs`；Core / frontend / protocol 未改。A/B focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Draven|FullyQualifiedName~SFD020|FullyQualifiedName~VanillaPlayUnit|FullyQualifiedName~PlayUnit"` 通过 14/14；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3601/3601 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 SFD·020 / SFD·020a ordinary hand play-unit body + guard representative evidence。
- 不关闭 battle win dormant Gold、attack / defense optional red payment、+2 until EOT、full PaymentEngine、Layer / duration cleanup、FAQ refs、1009/811 full-official 或 final 18-step E2E。

## 2026-05-11 阶段 4C-46 Legend Domain / Shared Oracle Design Gate

阶段 4C-46 设计门禁入口：`docs/CURRENT_STAGE4C_BATCH46_LEGEND_DOMAIN_SHARED_ORACLE_DESIGN_GATE.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH46_LEGEND_DOMAIN_SHARED_ORACLE_EVIDENCE.md`。B/C/D/E 只读门禁一致判断 Void Burrower / 虚空遁地兽 `SFD·187/221` / `FU-6e7d0dba2c` 与 Sett / 腕豪 `OGN·269/298` / `FU-6308c2db01` **NO-GO for direct 4C-46 runtime implementation**；本批只记录 legend-domain / shared-oracle design gate。项目仍 **NOT READY**，`fullOfficial=false`。

- Void Burrower 当前服务端代表路径可自动处理征服触发、reveal top two / play one / recycle remainder / dormant legend，但官方 optional、hidden reveal choice、shared oracle mapping 尚未官方化。
- Sett 当前服务端代表路径可自动替代摧毁、支付、召回、征服 ready，但官方 optional replacement、payment、boon consume、dormant recall cleanup 尚未官方化。
- P0/P1：LegendActivePredicate、LegendOptionalTrigger、RevealChoice、ReplacementPayment、shared oracle reprint mapping、hidden redaction、`PAY_COST` / cleanup queue interactions、FAQ `SOUL-JFAQ-260114 p14` / `SOUL-OFAQ-260114 p4`、1009/811 full-official、final 18-step E2E。
- 本批不新增 runtime implementation；A 已在 checkpoint 前跑完整验证，确认设计文档 / 矩阵 overlay 未破坏基线：后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3594/3594 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 不宣称 READY / READY-CANDIDATE；不因现有代表路径外推完整 legend trigger/action、hidden reveal/choice/recycle、replacement/payment、boon、dormant recall cleanup 或 shared oracle official coverage。

## 2026-05-11 阶段 4C-45 Switcheroo Swap Guard 审计

阶段 4C-45 审计入口：`docs/CURRENT_STAGE4C_BATCH45_SWITCHEROO_SWAP_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH45_SWITCHEROO_SWAP_GUARD_EVIDENCE.md`。本批已补 Switcheroo / 换换乐 `SFD·145/221` / cardId `33237` / `FU-0b6332bbf0` / `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS` 的 ultra-narrow battlefield power-swap guard overlay。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` with two public battlefield unit targets -> stack / pass-pass -> this-turn power swap representative route。
- Guard：non-public battlefield unit target，包括 equipment / spell / rune / face-down standby / left-play target，均不得入栈或不得在结算时产生 power mutation。
- B 新增 `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`，并最小修改 `src/Riftbound.Engine/CoreRuleEngine.cs` 修复 Switcheroo target guard。A focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Switcheroo|FullyQualifiedName~PowerSwap|FullyQualifiedName~Power"` 通过 284/284；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3594/3594 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 Switcheroo representative battlefield power-swap target guard overlay。
- 不关闭 true LayerEngine、later modifier ordering、duration cleanup / EOT expiry、same-battlefield precision beyond current representative model、damage / battle math、full FAQ `SOUL-JFAQ-260114 p14`、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-44 Akshan Play Guard 审计

阶段 4C-44 审计入口：`docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH44_AKSHAN_PLAY_GUARD_EVIDENCE.md`。本批已补 Akshan / 阿克尚 `SFD·109/221` / cardId `33194` / `FU-7419ee7d9d` / `AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT` 的 ultra-narrow play-unit guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 4，tags `CARD_TYPE:UNIT` + `哨兵` + `百炼`；不选择 optional assemble，不支付 orange-orange extra cost。
- Guard：explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 rejected，no mutation / no leak。
- B 新增 `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`；focused 命令 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~PlayUnit|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Assemble"` 已由 B 和 A 均通过，A 结果 189/189；后端 full `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 3582/3582 passed；前端 build `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed；Chrome smoke `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed。
- 本批只关闭 Akshan ordinary hand no-optional / no-extra play-unit guard representative evidence。
- 不关闭 optional assemble、orange-orange extra play、enemy equipment move / control、weapon attach、control-until-leaves cleanup、LayerEngine / continuous effects、FAQ full behavior、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-43 Sfur Song Play Guard 审计

阶段 4C-43 审计入口：`docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH43_SFUR_SONG_PLAY_GUARD_EVIDENCE.md`。本批已补 Sfur Song / 斯弗尔尚歌 `SFD·059/221` / cardId `33139` / `FU-9a623b3185` / `SFUR_SONG_PLAY_EQUIPMENT` 的 play-equipment target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- Guard：explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 rejected，no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。
- B slice 为 guard test；Core unchanged，Core 无改动。A rerun focused 通过 268/268；A backend full 3576/3576 通过；A frontend build 通过；A Chrome smoke 通过；D 未运行 full tests。
- 本批只关闭 Sfur Song ordinary hand play-equipment target guard representative evidence。
- 不关闭复制宿主技能文字、持续文本 / layer、完整 assemble / equipment attach lifecycle、装备控制权 / 区域移动、FAQ full behavior、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-42 Time Gate Play Guard 审计

阶段 4C-42 审计入口：`docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH42_TIME_GATE_PLAY_GUARD_EVIDENCE.md`。本批已补 Time Gate / 预时之门 `SFD·078/221` / cardId `33158` / `FU-081d97eb3e` / `TIME_GATE_PLAY_EQUIPMENT` 的 play-equipment target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- Guard：explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 rejected，no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。
- B slice 为 guard test；Core gap none，Core 无改动。A/B focused 通过 292/292；D 未运行重测试。
- 本批只关闭 Time Gate ordinary hand play-equipment target guard representative evidence。
- 不关闭 activated / tap ability、payment `[A]`、next spell gains Echo、optional echo payment / repeat、duration cleanup、equipment exhaust / readiness lifecycle、FAQ timing、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-41 Giant Arm Kato Play Keyword Guard 审计

阶段 4C-41 审计入口：`docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH41_GIANT_ARM_KATO_PLAY_KEYWORD_GUARD_EVIDENCE.md`。本批已补 Giant Arm Kato / 巨腕加藤 `SFD·112/221` / cardId `33198` / `FU-464ec8c275` / `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT` 的 play-unit keyword-tag target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base unit，power 3，tags `CARD_TYPE:UNIT` + `法盾`。
- Guard：explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 rejected，no tick / no events / no payment / no hand movement / no stack / no unit entry / no leak。
- B slice 为 guard test；Core gap none，Core 无改动。A/B focused 通过 99/99；D 未运行重测试。
- 本批只关闭 Giant Arm Kato ordinary hand play-unit keyword-tag target guard representative evidence。
- 不关闭 Spellshield target tax、move-to-battlefield trigger、friendly-unit choice / prompt、keyword grant、+power until EOT、LayerEngine / duration cleanup、movement / control matrix、FAQ、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-40 Sea Monster Hook Play Guard 审计

阶段 4C-40 审计入口：`docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_EVIDENCE.md`。本批已补 Sea Monster Hook / 海兽钓钩 `OGN·242/298` / cardId `31482` / `FU-2653af0380` / `SEA_MONSTER_HOOK_PLAY_EQUIPMENT` 的 play-equipment target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：ordinary hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- Guard：explicit target、wrong zone / source、opponent source、face-down standby source、insufficient mana 均 rejected，no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。
- B slice 为 guard test；Core gap none，Core 无改动。A/B focused 通过 272/272；D 未运行重测试。
- 本批只关闭 Sea Monster Hook ordinary hand play-equipment target guard representative evidence。
- 不关闭 activated ability：pay 1 + yellow + exhaust、destroy friendly unit、top-five look / choice、free play、recycle remainder、hidden / zone / payment / layer / FAQ、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-39 Zhonya's Hourglass Play Guard 审计

阶段 4C-39 审计入口：`docs/CURRENT_STAGE4C_BATCH39_ZHONYAS_HOURGLASS_PLAY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH39_ZHONYAS_HOURGLASS_PLAY_GUARD_EVIDENCE.md`。本批已补 Zhonya's Hourglass / 中娅沙漏 `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` / `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT` 的 play-equipment target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：普通 hand `PLAY_CARD` 0-target -> stack / pass-pass -> base equipment。
- Guard：explicit target、source not in hand / wrong zone、opponent source、face-down standby source、insufficient mana 均 rejected，no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak。
- B slice 为 guard test；Core gap none，Core 无改动。A/B focused 通过 268/268；D 未运行重测试。
- 本批只关闭 Zhonya ordinary hand play-equipment target guard representative evidence。
- 不关闭 standby / reaction timing、destroy replacement recall、完整 equipment / layer / FAQ、hidden info、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-38 Edge of Night Assemble Guard 审计

阶段 4C-38 审计入口：`docs/CURRENT_STAGE4C_BATCH38_EDGE_OF_NIGHT_ASSEMBLE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH38_EDGE_OF_NIGHT_ASSEMBLE_GUARD_EVIDENCE.md`。本批已补 Edge of Night / 夜之锋刃 `SFD·139/221` / cardId `33229` / `FU-804412488c` / `EDGE_OF_NIGHT_PLAY_EQUIPMENT` 的 play-equipment / assemble-purple target guard representative baseline。项目仍 **NOT READY**，`fullOfficial=false`。

- Scope：普通 `PLAY_CARD` hand route 0 target -> stack / pass-pass -> base equipment；explicit target rejected no payment / no mutation；face-up controlled base Edge of Night `ASSEMBLE_PURPLE` -> friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`。
- Guard：face-down / hidden source、source in hand、opponent source、already-attached source、unknown source、unknown / opponent / face-down standby / non-unit target、missing / wrong optional cost、insufficient purple 均 no tick / no events / no payment / no stack / no attach / no leak。
- B slice 为 test-only；Core gap none，Core 无改动。A focused filter 通过 98/98；D 未运行重测试。
- 本批只关闭 narrow assemble / play guard representative evidence；Edge of Night face-down standby immediate attach remains P0 / design-gated。
- 不关闭 full official standby immediate attach、hidden redaction、equipment layer、FAQ、1009/811 full-official 或 final 18-step E2E。

## 2026-05-10 阶段 4C-37 Berserk Impulse Opponent Top Unit Guard 审计

阶段 4C-37 审计入口：`docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH37_BERSERK_IMPULSE_OPPONENT_TOP_UNIT_EVIDENCE.md`。本批已补 Berserk Impulse / 暴怒冲动 `OGN·025/298` / cardId `31231` / `FU-b05eda44ce` / `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` 的极窄 opponent top main-deck unit target guard 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-37 已关闭代表子项：

- P1 从手牌打出 Berserk Impulse，支付 4 点费用，选择 P2 已揭示 / 代表性 public top main-deck unit；双方 priority pass 后目标打出到 P1 base。
- `UNIT_PLAYED_TO_BASE` 代表事件记录 source spell、target object、`ownerPlayerId=P2`、`playedByPlayerId=P1`、`sourceZone=MAIN_DECK`、`destinationZone=BASE`。
- 结算后目标单位 damage reset to 0、until-end-of-turn effects / power modifier 清空、exhausted reset to false。
- `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- friendly top unit、opponent second main-deck unit、top spell / equipment / rune、face-down top unit、private hand / base / battlefield unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no deck movement / no stack item / no unit played / no leak。
- dirty resolution guard：stack target 结算前不再是 opponent top、target 非 unit、face-down top unit、wrong controller / ownership dirty top target 均不移动，不产生 `UNIT_PLAYED_TO_BASE`；源法术正常入墓。
- hidden-info stance：本批只覆盖代表性“已揭示 / 可选 top object”目标 guard 与 face-down / private-zone no-leak；不覆盖完整隐藏区展示、选择 prompt、未选牌回收 redaction 或多对手隐私边界。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性、隐藏信息展示或免费打出结算。
- Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-37 规则依据：

- `CATALOG` `OGN·025/298`；cardId `31231`；FU `FU-b05eda44ce`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。
- `SOUL-OFAQ-260114` p4。

4C-37 验证记录：

- Focused backend：通过 17/17。
- Tests added in `BerserkImpulseGuardTests`：`BerserkImpulsePlaysOpponentTopMainDeckUnitToControllerBaseAndResetsState`、`BerserkImpulseRejectsInvalidTargetsWithoutMutation`、`BerserkImpulseDirtyResolutionDoesNotMoveInvalidTopDeckTarget`。
- D 未运行重测试；未记录 backend full / frontend build / Chrome smoke。
- 上述 focused 不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：Berserk Impulse full hidden-zone reveal / choose / recycle 仍为 final READY 阻断；本批只关闭 narrow target guard P0 / representative evidence。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Berserk Impulse multi-opponent reveal / choose / recycle、non-unit branch、hidden-zone prompt / redaction、spell duel / reaction timing、full target prompt / invalidation、free-play branch owner/controller/payment matrix、LayerEngine interactions、private-zone replay redaction、targeting UX 与 hidden-zone UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-37 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-36 Hostile Takeover Control Ready Guard 审计

阶段 4C-36 审计入口：`docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH36_HOSTILE_TAKEOVER_CONTROL_READY_EVIDENCE.md`。本批已补 Hostile Takeover / 恶意收购 `SFD·202/221` / cardId `33301` / `FU-00ee09c2cc` / `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` 的极窄 enemy public battlefield unit gain-control + ready 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-36 已关闭代表子项：

- P1 打出 Hostile Takeover，选择 enemy public battlefield unit，双方 priority pass 后结算，P1 获得该单位控制权并 ready。
- 代表路径确认 owner 仍为 P2，controller 变为 P1，对象仍留在 battlefield，并安排 `RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2`。
- 既有 P5 end-turn return / recall fixture 可作为临时控制归还并召回 owner base 的代表证据，但不升级 full official。
- `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- friendly battlefield unit、enemy base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no control / no ready / no leak。
- hidden-info stance：face-down standby target 与 private hand unit target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性、控制权、ready 或 end-turn recall 结算。
- Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-36 规则依据：

- `CATALOG` `SFD·202/221`；cardId `33301`；FU `FU-00ee09c2cc`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p22-p26 rules 179, 187-189；p29-p35 rules 316-340；p39-p42 rules 355-356。
- `SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p22。

4C-36 验证记录：

- Focused backend：通过 265/265。
- Adjacent guard regression：通过 157/157。
- Backend full：通过 3515/3515。
- Frontend build：通过。
- Chrome smoke：通过。
- Tests added in `HostileTakeoverGuardTests`：`HostileTakeoverGainsControlReadiesEnemyBattlefieldUnitAndSchedulesReturn`、`HostileTakeoverRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Hostile Takeover full standby / reaction timing、battle-start / conquer branch、battlefield / control-zone lifecycle、owner/controller matrix、end-turn cleanup task model、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、movement / recall / replacement / cleanup 交织、event label / replay redaction、targeting UX 与 control UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-36 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-35 Vengeance Destroy Target Guard 审计

阶段 4C-35 审计入口：`docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH35_VENGEANCE_DESTROY_TARGET_GUARD_EVIDENCE.md`。本批已补 Vengeance / 复仇 `OGN·229/298` / cardId `31467` / `FU-07104fa58a` / `VENGEANCE_DESTROY_UNIT` 的极窄 public unit destroy target 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-35 已关闭代表子项：

- P1 打出 Vengeance，选择合法 public unit target，双方 priority pass 后结算，目标进入 owner graveyard，并从 base / battlefield 与 public object state 移除。
- 合法目标覆盖 friendly / enemy public unit targets in base / battlefield；Vengeance 不按 controller 阵营限制目标。
- `VENGEANCE_DESTROY_UNIT` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- stale unit、face-down standby object、battlefield / base equipment、battlefield spell object、battlefield rune object、hand / private unit 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy / no leak。
- hidden-info stance：face-down standby target 与 private hand unit target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 destroy / cleanup 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-35 规则依据：

- `CATALOG` `OGN·229/298`；cardId `31467`；FU `FU-07104fa58a`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p62-p63 rule 428。

4C-35 验证记录：

- Focused backend：通过 107/107。
- Adjacent guard regression：通过 23/23。
- `git diff --check` 通过。
- Backend full：通过 3506/3506。
- Frontend build：通过。
- Chrome smoke：通过。
- Tests added in `VengeanceDestroyGuardTests`：`VengeanceDestroysPublicUnitTargets`、`VengeanceRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Vengeance full destroy / cleanup / replacement / prevention / Last Breath interaction、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、attached-equipment detach / replacement breadth、destroyed-this-turn memory、event label / replay redaction、targeting UX 与 destroy UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-35 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-34 Isolate Move Guard 审计

阶段 4C-34 审计入口：`docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH34_ISOLATE_MOVE_GUARD_EVIDENCE.md`。本批已补 Isolate / 隔绝 `UNL-124/219` / cardId `34667` / `FU-175d573ae4` / `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW` 的极窄 enemy public battlefield unit move to owner base no-draw 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-34 已关闭代表子项：

- P1 打出 Isolate，选择合法 enemy public battlefield unit target，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power / exhausted / object identity。
- 代表路径确认不产生 `CARD_DRAWN`；落单敌方单位抽牌分支仍未关闭。
- `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no draw / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性、movement 结算或 draw 分支。
- Vengeance 保留为低耦合后续候选；Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-34 规则依据：

- `CATALOG` `UNL-124/219`；cardId `34667`；FU `FU-175d573ae4`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。

4C-34 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Isolate|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 46/46。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~IsolateMoveToBaseGuardTests|FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 48/48。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3495/3495。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Tests added in `IsolateMoveToBaseGuardTests`：`IsolateMovesPublicEnemyBattlefieldUnitToOwnerBaseWithoutDrawing`、`IsolateRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Vengeance destroy target route、Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Isolate 落单敌方单位抽牌分支、完整目的地/孤立判定、多位置 battlefield model、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、event label / replay redaction、targeting UX 与 movement UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-34 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-33 Charm Move Guard 审计

阶段 4C-33 审计入口：`docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH33_CHARM_MOVE_GUARD_EVIDENCE.md`。本批已补 Charm / 魅惑妖术 `OGN·043/298` / cardId `31255` / `FU-1586b6cdd9` / `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE` 的极窄 enemy public battlefield unit move to owner base 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-33 已关闭代表子项：

- P1 打出 Charm，选择合法 enemy public battlefield unit target，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power / exhausted / object identity。
- `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no move / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 movement 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-33 规则依据：

- `CATALOG` `OGN·043/298`；cardId `31255`；FU `FU-1586b6cdd9`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。

4C-33 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Charm|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 35/35。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 40/40。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3487/3487。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Tests added in `CharmMoveToBaseGuardTests`：`CharmMovesPublicEnemyBattlefieldUnitToOwnerBase`、`CharmRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Charm 完整目的地选择、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、event label / replay redaction、targeting UX 与 movement UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-33 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-32 Ride the Wind Move Guard 审计

阶段 4C-32 审计入口：`docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_EVIDENCE.md`。本批已补 Ride the Wind / 驭风而行 `OGN·173/298` / cardId `31403` / `FU-6f84196631` / `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 的极窄 friendly public battlefield unit ready + move to owner base 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-32 已关闭代表子项：

- P1 打出 Ride the Wind，选择合法 friendly public battlefield unit target，双方 priority pass 后结算，目标 ready 并移动到 owner base。
- `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no ready / no move / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 movement / ready 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-32 规则依据：

- `CATALOG` `OGN·173/298`；cardId `31403`；FU `FU-6f84196631`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。
- `JFAQ-251023` p4 作为 Ride the Wind swift / spell timing regression 入口；本批不关闭完整反应时机或 spell-duel breadth。

4C-32 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWind|FullyQualifiedName~Ride|FullyQualifiedName~MoveGuard"` 通过 11/11。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 32/32。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3479/3479。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Tests added in `RideTheWindMoveGuardTests`：`RideTheWindReadiesAndMovesPublicFriendlyBattlefieldUnitToOwnerBase`、`RideTheWindRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Ride the Wind / swift 相关 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、event label / replay redaction、targeting UX 与 movement / ready UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-32 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-31 Reprimand Return To Hand Guard 审计

阶段 4C-31 审计入口：`docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH31_REPRIMAND_RETURN_TO_HAND_GUARD_EVIDENCE.md`。本批已补 Reprimand / 责退 `OGN·172/298` / `FU-d0383ed260` / `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` 的极窄公共战场单位回手与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-31 已关闭代表子项：

- P1 打出 Reprimand，选择正面公共战场单位目标，双方 priority pass 后结算，目标返回 owner hand。
- `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 return-to-hand 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-31 规则依据：

- `CATALOG` `OGN·172/298`；FU `FU-d0383ed260`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。
- `JFAQ-251023` p4 作为 Reprimand swift / spell timing regression 入口；本批不关闭完整反应时机或 spell-duel breadth。

4C-31 验证记录：

- Focused backend：A 记录 focused 通过 58/58。
- Adjacent guard：A 记录 adjacent guard 通过 24/24。
- Backend full：A 记录 backend full 通过 3471/3471。
- Frontend build：A 记录 frontend build passed。
- Chrome smoke：A 记录 Chrome smoke passed。
- Tests added in `ReprimandReturnToHandGuardTests`：`ReprimandReturnsPublicBattlefieldUnitToOwnerHand`、`ReprimandRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Reprimand / swift 相关 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / control-zone matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、event label / replay redaction、targeting UX 与 return-to-hand UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-31 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-30 Hunt the Weak Destroy Guard 审计

阶段 4C-30 审计入口：`docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_EVIDENCE.md`。本批已补 Hunt the Weak / 狩魂 `UNL-159/219` / `FU-282b6e3149` 的极窄公共战场单位 power <= 3 摧毁与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-30 已关闭代表子项：

- P1 打出 Hunt the Weak，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标被摧毁并进入 owner graveyard。
- `HUNT_THE_WEAK_DESTROY_BATTLEFIELD_UNIT_POWER_3_OR_LESS` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy mutation。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 仍由 viewer-specific snapshot / redaction 保护。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 destroy 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-30 规则依据：

- `CATALOG` `UNL-159/219`；FU `FU-282b6e3149`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p31-p35 rules 318-340；p39-p42 rules 355-356；p62-p63 rule 428。
- `JFAQ-251023` p4 作为 spell timing / stack regression 入口；本批不关闭完整反应时机。

4C-30 验证记录：

- Focused backend：A 记录 Hunt the Weak focused 通过 34/34。
- Adjacent regression：A 记录 adjacent 通过 19/19。
- Tests added in `HuntTheWeakDestroyGuardTests`：`HuntTheWeakDestroysPublicSmallBattlefieldUnit`、`HuntTheWeakRejectsInvalidTargetsWithoutMutation`。
- Backend full：A 记录 `dotnet test Riftbound.slnx --no-restore` 通过 3464/3464。
- Frontend build：A 记录 `npm run build` 通过。
- Chrome smoke：A 记录 `npm run smoke:chrome -- --start-api` 通过。
- 本批未运行 Stage 3 preflight；上述验证不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：完整 destroy / cleanup / Last Breath trigger interactions、state-based cleanup 与 simultaneous destruction full-official matrix。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Hunt the Weak 相关 replacement / prevention / cleanup / full targeting matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、event label / replay redaction、targeting UX 与 destroy UX 仍需后续全矩阵证据；本批不新增这些方向的 P0。

4C-30 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-29 Gust Return To Hand Guard 审计

阶段 4C-29 审计入口：`docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_EVIDENCE.md`。本批已补 Gust / 罡风 `OGN·169/298` / `FU-48662b7661` 的极窄公共战场单位 power <= 3 回手与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-29 已关闭代表子项：

- P1 打出 Gust，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标返回 owner hand。
- `GUST_RETURN_BATTLEFIELD_UNIT_POWER_3_OR_LESS_TO_HAND` 在 `PLAY_CARD` validation 中使用服务端权威 target guard，不依赖前端裁决。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 return-to-hand 结算。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

4C-29 规则依据：

- `CATALOG` `OGN·169/298`；FU `FU-48662b7661`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p31-p35 rules 318-340；p39-p42 rules 355-356。
- `JFAQ-251023` p4 作为 Gust reaction / swift timing regression 入口；本批不关闭完整反应时机。

4C-29 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gust|FullyQualifiedName~ReturnToHand|FullyQualifiedName~Return|FullyQualifiedName~Hand"` 通过 112/112。
- Small combined regression：GustReturnToHandTests + BattleOrFlight + existing Gust rejection 通过 13/13。
- Tests added in `GustReturnToHandTests`：`GustReturnsPublicSmallBattlefieldUnitToOwnerHand`、`GustRejectsInvalidTargetsWithoutMutation`。
- 本批未记录 backend full / frontend build / Chrome smoke / Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- P0：完整 return-to-hand / movement / control-zone lifecycle、owner/controller split、attached equipment、replacement / prevention / cleanup 交织。
- P0：完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Gust return-to-hand 的 event label / replay redaction、targeting UX 与 return-to-hand UX 仍需后续全矩阵证据。

4C-29 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-28 Battle or Flight Move To Base 审计

阶段 4C-28 审计入口：`docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_EVIDENCE.md`。本批已补 Battle or Flight / 战或逃 `OGN·168/298` / `FU-813144e7d4` 的极窄战场单位移动到 owner base 与目标 guard hardening 代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-28 已关闭代表子项：

- P1 打出 Battle or Flight，选择正面战场单位目标，双方 priority pass 后结算，目标移动到 owner base。
- 移动后保留 damage / power / object identity，避免误判为重新创建对象或错误重置状态。
- battlefield equipment、base unit、stale object、face-down standby object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no unit movement。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决目标合法性或 move-to-base 结算。

4C-28 规则依据：

- `CATALOG` `OGN·168/298`；FU `FU-813144e7d4`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356。
- `JFAQ-251023` p4；`SOUL-JFAQ-260114` p12、p16 作为 Battle or Flight swift / movement FAQ regression 入口。

4C-28 验证记录：

- Focused backend：A 记录 Battle or Flight focused 通过 61/61。
- Tests added in `BattleOrFlightMoveToBaseTests`：`BattleOrFlightMovesFaceUpBattlefieldUnitToOwnerBase`、`BattleOrFlightRejectsInvalidTargetsWithoutMutation`。
- 本批未记录 backend full / frontend build / Chrome smoke / Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 spell duel / battle lifecycle、swift / reaction timing、face-down standby play 与 priority window 全矩阵。
- P0：完整 movement / control-zone / roam lifecycle、owner/controller split、attached equipment、movement replacement / prevention / cleanup 交织。
- P0：完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Battle or Flight move-to-base 的 event label / replay redaction、movement UI 解释字段和 movement UX 仍需后续全矩阵证据。

4C-28 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-27 Treasure Hunter Move Gold 审计

阶段 4C-27 审计入口：`docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_EVIDENCE.md`。本批已补 Treasure Hunter / 寻宝猎人 `SFD·130/221` / `FU-6144ab0271` 的极窄移动触发创建休眠 Gold 装备指示物代表切片。项目仍 **NOT READY**，`fullOfficial=false`。

4C-27 已关闭代表子项：

- visible face-up Treasure Hunter 通过 existing authoritative move route 成功移动后，触发 `TREASURE_HUNTER_MOVE_CREATE_GOLD` 并创建一个休眠 Gold equipment token 到 controller base。
- base -> battlefield move 与 precise ROAM battlefield A -> battlefield B 两条移动来源已有代表覆盖。
- non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source、failed move、no-op move 均 no trigger / no leak / no token。
- 本批未新增 protocol / frontend shape；前端仍不本地裁决移动触发或 Gold token 创建。

4C-27 规则依据：

- `CATALOG` `SFD·130/221`；FU `FU-6144ab0271`。
- `CATALOG` Gold token identity。
- `CORE-260330` p4-p8 rules 107-129；p31-p35 rules 318-340；p39-p42 rules 355-356；p52-p55 rules 383.3.d-383.3.e；p89 rules 718-719。
- `SOUL-JFAQ-260114` p21 作为 Treasure Hunter / movement / Gold 相关 FAQ regression 入口。

4C-27 验证记录：

- Focused backend：A 记录 Treasure Hunter focused 通过 82/82。
- Small regression：A 记录 Treasure Hunter small regression 通过 121/121。
- Tests added in `TreasureHunterMoveTriggerTests`：`TreasureHunterMoveCreatesDormantGoldToken`、`TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger`、`NonTreasureHunterMoveDoesNotTrigger`、`FailedTreasureHunterMoveDoesNotCreateGold`、`TreasureHunterPreciseRoamMoveCreatesDormantGoldToken`、`TreasureHunterPreciseRoamNoOpDoesNotCreateGold`。
- 本批未记录 backend full / frontend build / Chrome smoke / Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint / index / TODO 文档；不修改服务端、前端、coverage matrix JSON、baseline / risk / freeze 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 movement / control-zone / roam lifecycle、全部移动来源、移动替代 / 取消 / 同步触发矩阵。
- P0：完整 move-trigger family、完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：equipment token 全规则、token ownership / controller / zone matrix。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、replay redaction 与显露窗口。
- P0：Karthus / `FU-ee1dfb3ed3` extra Last Breath 仍 design-gated；optional choice、multiplicity、multi-Karthus stacking、hidden / face-down / standby visibility 与 `ORDER_TRIGGERS` batch model 未裁决。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Treasure Hunter move Gold 的 event label / replay redaction、Gold token UI 解释字段和 movement UX 仍需后续全矩阵证据。

4C-27 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-26 Jax Weapon Attach Payment Draw 审计

阶段 4C-26 审计入口：`docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH26_JAX_WEAPON_ATTACH_PAYMENT_DRAW_EVIDENCE.md`。本批已补 Jax / 贾克斯 `SFD·119/221`、`SFD·119a/221` / `FU-73f3be35df` 的极窄武装贴附触发支付抽牌代表切片。项目仍 **NOT READY**。

4C-26 已关闭代表子项：

- visible face-up Jax 通过现有 equipment attach route 被贴附 weapon / armament 后打开现有 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- `PAY_COST(SPEND_MANA:1)` 后控制者抽 1，并关闭支付窗口。
- `PAY_COST(DECLINE)` 关闭支付窗口且不抽牌、不产生其它 mutation。
- non-Jax / non-armament no prompt；hidden / face-down / standby / opponent-controlled source no trigger / no leak；insufficient payment rejected without draw。

4C-26 规则依据：

- `CATALOG` `SFD·119/221`、`SFD·119a/221`；FU `FU-73f3be35df`。
- `CORE-260330` p4-p8 rules 107-129；p52-p57 rules 377, 403-405, 413；p89 rules 718-719。
- `JFAQ-251023` p2-p4 q2.5；`SOUL-JFAQ-260114` p22-p23。

4C-26 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~TriggerPayment"` 通过 37/37。
- Small regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JaxWeaponAttach|FullyQualifiedName~Icevale|FullyQualifiedName~Vayne|FullyQualifiedName~Lux|FullyQualifiedName~SunkenTemple|FullyQualifiedName~BattlefieldConquerGold|FullyQualifiedName~TriggerPayment"` 通过 46/46。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3439/3439。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup `PURE` 注释提示。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- Tests added in `TriggerPaymentTests`：`JaxWeaponAttachOpensTriggerPaymentPrompt`、`JaxWeaponAttachPaymentAcceptedDrawsOneAndClosesWindow`、`JaxWeaponAttachPaymentDeclineClosesWithoutDraw`、`JaxWeaponAttachNonJaxOrNonEquipmentDoesNotOpenPayment`、`JaxWeaponAttachHiddenStandbyOrOpponentControlledDoesNotOpenPayment`、`JaxWeaponAttachInsufficientPaymentRejectsWithoutDraw`。
- 本批未记录 Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / 索引 / TODO 文档；不修改 A checkpoint、coverage matrix JSON、baseline / risk / freeze 文档、服务端、前端或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 Forge / 百炼 / assemble lifecycle、打出时可选装配、减费、已贴附武装选择和装配合法性全矩阵。
- P0：完整 equipment / weapon / armament attachment rules、控制权、卸除、重贴附、区域归属和 attached top-card matrix。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、order triggers 与完整 effect resolution。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / wrong-player / multi-window full matrix。
- P0：draw / replacement / burn-out / hidden-zone visibility / replay redaction 全矩阵。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Jax weapon attach payment 的 UI/DTO 解释字段、event label / replay redaction 和 equipment attach UX 仍需后续全矩阵证据。

4C-26 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-25 Icevale Archer Attack Payment 审计

阶段 4C-25 审计入口：`docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH25_ICEVALE_ARCHER_ATTACK_PAYMENT_EVIDENCE.md`。本批已补 Icevale Archer / 冰谷弓箭手 `UNL-065/219` / `FU-c170628e3a` 的极窄进攻触发支付降战力代表切片。项目仍 **NOT READY**。

4C-25 已关闭代表子项：

- active start-battle task 下，visible face-up Icevale 作为攻击者后打开现有 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- 使用 `DeclareBattleCommand.BattlefieldTargetObjectIds` 预选同一 battlefield 的正面单位目标；支付成功后目标本回合 power -1。
- `PAY_COST(DECLINE)` 关闭支付窗口且不修改目标战力。
- invalid target、hidden / face-down / standby / opponent-controlled source 均 no trigger / no leak / no mutation。

4C-25 规则依据：

- `CATALOG` `UNL-065/219`；FU `FU-c170628e3a`。
- `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p35 rules 318-340；p39-p42 rules 355-356；p52-p55 rules 377, 403-405；p77-p78 rules 454-464。
- `JFAQ-251023` p2-p4 q2.2-q2.5。

4C-25 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Icevale|FullyQualifiedName~AttackPayment|FullyQualifiedName~TriggerPayment|FullyQualifiedName~DeclareBattle|FullyQualifiedName~Vayne|FullyQualifiedName~Lux"` 通过 102/102。
- JSON / diff hygiene：`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 通过；`git diff --check` 通过。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3429/3429。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- 本批未记录 Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；A 后续对齐 coverage matrix JSON、baseline / risk / freeze 文档口径；不修改前端或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 spell duel / battle lifecycle、start-battle task、battle response window、damage assignment 与支付窗口恢复时点。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、attack-trigger family 与完整 effect resolution。
- P0：完整 target selection prompt、same-battlefield target matrix、target invalidation、Spellshield target tax。
- P0：完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Icevale attack payment 的 UI/DTO 解释字段、战斗恢复 UX、event label / replay redaction 仍需后续全矩阵证据。

4C-25 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-24 Vayne Conquer Recall 审计

阶段 4C-24 审计入口：`docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH24_VAYNE_CONQUER_RECALL_EVIDENCE.md`。本批已补 Vayne / 薇恩 `OGN·035/298` / `FU-c027639a3c` 的极窄征服支付回手代表切片。项目仍 **NOT READY**。

4C-24 已关闭代表子项：

- visible face-up Vayne 征服战场后打开现有 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- `PAY_COST(SPEND_MANA:1)` 后 Vayne 返回 owner hand。
- `PAY_COST(DECLINE)` 关闭支付窗口且不回手、不变更。
- hidden / face-down / standby / opponent-controlled source 均 no trigger / no leak / no mutation。

4C-24 规则依据：

- `CATALOG` `OGN·035/298`；FU `FU-c027639a3c`。
- `CORE-260330` p4-p8 rules 107-129；p52-p55 rules 377, 403-405；p57-p59 rules 413-416；p77-p78 rules 454-464。
- `JFAQ-251023` p2-p4 q2.5；p6-p7 q5.1-q5.4。

4C-24 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Vayne|FullyQualifiedName~BattlefieldConquer|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"` 通过 52/52。
- 本批未记录 backend full / frontend build / Chrome smoke / Stage 3 preflight；不得替代最终正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；不修改服务端、前端、coverage matrix JSON、baseline/risk 文档或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 battlefield / control / conquer lifecycle、control freeze/release、held/conquer scoring order 与 battle cleanup 全矩阵。
- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、insufficient / stale / multi-window full matrix。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Vayne Assault3 combat modifier、active-entry condition、owner/controller return-hand matrix、hand visibility / replay redaction。

4C-24 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-23 Lux High-Cost Spell Power 审计

阶段 4C-23 审计入口：`docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH23_LUX_HIGH_COST_SPELL_POWER_EVIDENCE.md`。A 已决定本批收 Lux / 拉克丝 `OGS·006/024` / `FU-f18a49e06d`，而不是 Aphelios；理由是 Lux 可作为低耦合 high-cost spell temporary power representative slice，Aphelios 的武装贴附三模式和本回合 mode memory 需要单独设计批次。项目仍 **NOT READY**。

4C-23 已关闭代表子项：

- visible face-up Lux 由其 controller 打出 cost >= 5 spell 后记录 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` compatibility events。
- Lux 本回合战力 +3，`POWER_MODIFIED_UNTIL_END_OF_TURN` 与 `UntilEndOfTurnPowerModifier` 均可追踪。
- low-cost spell、opponent spell、face-down / standby / invalid source 均 no trigger / no mutation。

4C-23 规则依据：

- `CATALOG` `OGS·006/024`；FU `FU-f18a49e06d`。
- `CORE-260330` p9；p14-p15 rules 142-143；p31-p33 rules 318-324；p33-p35 rules 333-340；p39-p42 rules 355-356。

4C-23 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCostSpell|FullyQualifiedName~Ravenbloom|FullyQualifiedName~RealTriggerQueue"` 通过 67/67。
- 本批修正 `LuxOpponentHighCostSpellDoesNotTrigger` 的测试对象 id，使 P2 对手法术负例真实使用 P2 手牌对象。
- Backend full / frontend build / Chrome smoke 待本批最终验收命令刷新后回填。

仍缺 P0/P1：

- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：完整 PaymentEngine、paid-cost override、增减费 / 额外费用 / 替代费用 full matrix。
- P0：完整 LayerEngine、temporary modifier timestamp / dependency / cleanup duration matrix。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Lux high-cost spell family、其它 spell-played temporary power FUs 与 multi-trigger ordering 仍需后续全矩阵证据。

4C-23 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-22 Muddy Dredger Warhawk Baseline 审计

阶段 4C-22 审计入口：`docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_AUDIT.md`；证据入口：`docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_EVIDENCE.md`。A 已决定本批收 Muddy Dredger / 腐泥疏浚工 `UNL-153/219` / `FU-b829fb32b9`，而不是 E 建议的 Aphelios；理由是 B/D 都判断 Muddy 是低耦合服务端 representative slice，且代码、focused backend 与 backend full 已通过。项目仍 **NOT READY**。

4C-22 已关闭代表子项：

- visible face-up Muddy Dredger 经 state-based cleanup destruction 产生 `UNIT_DESTROYED` 后进入 `TriggerQueue`。
- 多触发排序沿用 `ORDER_TRIGGERS`，排序后进入 `StackItems`，priority pass 后 `TRIGGER_RESOLVED`。
- 结算后创建 Warhawk `UNL·T02` token 到 controller base，并以 token tag / identity 代表 Spellshield。
- hidden / face-down / standby / invalid source 均 no enqueue / no leak / no token。

4C-22 规则依据：

- `CATALOG` `UNL-153/219`；FU `FU-b829fb32b9`。
- `CATALOG` `UNL·T02` Warhawk / 战鹰 token。
- `CORE-260330` p14-p15 rules 142-143；p31-p35 rules 318-340；p52-p55 rules 383.3.d-383.3.e；p77 rule 460；p92-p105 keyword rules 800+。
- `JFAQ-251023` p2-p4 q2.2-q2.3；`SOUL-OFAQ-260114` p19-p20。

4C-22 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MuddyDredger|FullyQualifiedName~RealTriggerQueue"` 通过 52/52。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3407/3407。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；不修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln`。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight 本批未运行；不得替代最终正式 18-step E2E。

仍缺 P0/P1：

- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling 与完整 effect resolution。
- P0：完整 Last Breath / destroyed / friendly-destroyed family、simultaneous destruction multiplicity matrix。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：Spellshield target tax / mandatory additional cost / multi-target tax / insufficient payment regression；4C-22 只记录 Warhawk token tag。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Warhawk token “打出”语义、token source / ownership / controller event fields 与 token family taxonomy。

4C-22 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-21 Sunken Temple Trigger Payment 审计

阶段 4C-21 审计入口：`docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_AUDIT.md`。本批已补 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 的征服强力单位触发支付代表切片；focused backend 通过 13/13。项目仍 **NOT READY**。

4C-21 已关闭代表子项：

- Sunken Temple 征服此处且战场上留存强力单位时打开服务端权威 `TRIGGER_PAYMENT` / `PAY_COST` prompt。
- 旧 immediate auto pay + draw 口径已 superseded；支付和抽牌不再由测试/前端视为自动发生。
- `PAY_COST(SPEND_MANA:1)` 支付成功后 `CARD_DRAWN` 1；`PAY_COST(DECLINE)` 拒付关闭窗口且不抽牌。
- invalid / stale / insufficient 等 no-mutation 语义纳入 focused 覆盖；仍不外推为完整 PaymentEngine。

4C-21 规则依据：

- `CATALOG` `SFD·218/221`；FU `FU-05ce012700`。
- `SOUL-OFAQ-260114` p15：沉没神庙 powerful / conquest timing 证据入口。
- `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5：触发费用可支付 / 可拒付与合法性入口。

4C-21 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P79BattlefieldConquerPowerfulUnitPaysOneToDraw|FullyQualifiedName~P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws"` 通过 13/13。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3404/3404。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过。
- B changed code/tests：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`、`tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`。
- A 因子 agent 超时接管小范围实现和审计收口；未修改前端功能代码，未触碰 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、更多非出牌支付窗口。
- P0：完整 battlefield / conquer lifecycle、战场控制冻结、battle cleanup、征服 / 据守得分全规则矩阵。
- P0：完整 trigger engine、完整 effect resolution、FAQ regression、1009 / 811 full-official、最终正式 18-step E2E。
- P1：Sunken Temple timing matrix，包括 effective power / LayerEngine、temporary modifier、征服后变强力、战场上多单位同时离场等组合。

4C-21 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-20B Undercover Agent Triggered Hand-Choice 审计

阶段 4C-20B 审计入口：`docs/CURRENT_STAGE4C_BATCH20B_UNDERCOVER_HAND_CHOICE_AUDIT.md`。本批已补 Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` 的服务端 `HAND_CHOICE` / `CHOOSE_HAND_CARDS` 微切片；A focused backend `UndercoverAgentTriggerTests` 已通过 6/6。项目仍 **NOT READY**。

4C-20B 已关闭代表子项：

- Undercover Agent 绝念触发结算中的服务端权威 hand-choice prompt。
- viewer-specific `handChoices` redaction：只有选择玩家看到候选手牌，对手看到脱敏等待信息。
- wrong player、stale prompt、invalid choice、malformed / illegal payload 拒绝且 no mutation。
- `CORE-260330` p62 / rule `422.4` 的 1 / 0 手牌 shortfall：弃尽可弃数量后仍抽两张。

4C-20B 验证记录：

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UndercoverAgentTriggerTests"` 通过 6/6。
- 前端专用接线：C 已完成 `HAND_CHOICE` 展示与 `CHOOSE_HAND_CARDS` 提交接线；前端仍不得本地裁决弃牌合法性、弃牌结果或抽牌结果。
- D 未修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln`。

仍缺 P0/P1：

- P0：Karthus 额外绝念 optional / multiplicity / multi-Karthus / visibility 裁决仍未实现。
- P0：非 Undercover 的通用 discard / hand-choice engine、其它 hand-choice FUs、完整 trigger engine、完整 effect resolution、完整 APNAP / trigger batch。
- P1：public/private discard event redaction 全矩阵、replay / spectator hand-choice redaction、更多 hand-size / replacement / prevention 组合。
- P0：FAQ regression、1009 / 811 full-official、最终正式 18-step E2E。

4C-20B 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-19 Kogmaw Last-Breath AoE Baseline 审计

阶段 4C-19 审计入口：`docs/CURRENT_STAGE4C_BATCH19_KOGMAW_LAST_BREATH_AOE_AUDIT.md`。本批已补 Kogmaw / 克格莫 `OGN·190/298` / `FU-af8b05c294` visible face-up field source 绝念 AoE damage 代表切片；A 已验证 focused/backend full/frontend build/Chrome smoke/diff/矩阵断言。项目仍 **NOT READY**。

4C-19 已关闭代表子项：

- visible、face-up、field source Kogmaw 被摧毁后触发 last-breath AoE damage。
- 路径：`UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` -> battlefield units take 4 damage -> cleanup queue stabilizes。
- AoE 使用 source pre-removal battlefield location，只伤害该 battlefield 的当前单位；其他 battlefield 单位不受伤害。
- hidden / face-down / standby Kogmaw source 不入队、不泄漏 prompt metadata、不造成 AoE damage。
- Kogmaw 缺少 battlefield location 时安全降级为 no-enqueue / no-damage。
- AoE 后伤害、后续 state-based cleanup 与 trigger queue 交织只作为代表切片验证，不外推 full official damage / cleanup matrix。

4C-19 验证记录：

- Focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests&FullyQualifiedName~Kogmaw"` 通过 4/4。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3392/3392。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- `git diff --check`、`jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 与 4C-19 matrix assertions 通过。
- 代表测试：`RealKogmawLastBreathDealsFourToDestroyedBattlefieldAndCleanupStabilizes`、`StateBasedCleanupKogmawLastBreathDealsFourToDestroyedBattlefield`、`StateBasedCleanupHiddenKogmawsDoNotEnqueueOrDealAoeDamage`、`RealKogmawDestroyedWithoutBattlefieldLocationDoesNotEnqueueOrDealDamage`。
- 协议/前端字段：本批无变更，DevUi 仍只消费 authoritative snapshot / prompt candidate。

仍缺 P0/P1：

- P0：Karthus 额外绝念、Undercover Agent discard / draw 仍未实现；Kogmaw 本批也只计划 representative baseline。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same pass / simultaneous destruction / AoE damage 后多轮 cleanup 与触发交织的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

4C-19 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-18 Mechanical + Ironclad Cleanup Trigger Baseline 审计

阶段 4C-18 审计入口：`docs/CURRENT_STAGE4C_BATCH18_MECHANICAL_IRONCLAD_CLEANUP_TRIGGER_AUDIT.md`。本批已补 Mechanical Trickster / `OGN·239/298` 与 Ironclad Vanguard / `SFD·021/221` 的 state-based cleanup last-breath trigger enqueue baseline；4C-16 / 4C-17 true stack 代表路径已关闭，4C-18 cleanup route 代表路径也已通过。项目仍 **NOT READY**。

4C-18 已关闭代表子项：

- Mechanical Trickster `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`：state-based cleanup `LETHAL_DAMAGE` -> `UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。
- Ironclad Vanguard `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`，冻结矩阵 FU `FU-6d0971786b`：同一路线结算为 `UNIT_TOKEN_CREATED` x2。
- hidden / face-down / standby source 不入队、不泄漏 prompt metadata、不创建 token。

4C-18 验证：

- B focused filter：通过 47/47。
- B backend full：通过 3388/3388。
- A backend full：通过 3388/3388。
- A frontend build：通过。
- A Chrome smoke：通过。
- `git diff --check`、矩阵 JSON/断言通过。
- 新增测试：`StateBasedCleanupMechanicalTrickstersTriggerOrderAndCreateMinionsThroughStack`、`StateBasedCleanupIroncladVanguardsTriggerOrderAndCreateRobotsThroughStack`、`StateBasedCleanupHiddenMechanicalTrickstersDoNotEnqueueTriggers`、`StateBasedCleanupHiddenIroncladVanguardsDoNotEnqueueTriggers`。

仍缺 P0/P1：

- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P1：same source same cleanup pass / same stack pass 多对象触发次数的 full official multiplicity matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

4C-18 不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-17 Ironclad Vanguard Trigger Baseline 审计

阶段 4C-17 审计入口：`docs/CURRENT_STAGE4C_BATCH17_IRONCLAD_VANGUARD_TRIGGER_AUDIT.md`。本批已把 Ironclad Vanguard / `SFD·021/221` / `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 的旧 immediate last-breath create robots -> real TriggerQueue / Stack / Priority 代表路径迁移完成；项目仍 **NOT READY**。

4C-17 已关闭代表子项：

- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x2，创建两名 3 战力 Robot / 机器人 token 到 controller base。
- face-down / standby Ironclad Vanguard 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79IroncladVanguardCreatesTwoRobotsWhenDestroyed` fixture 已更新为 queue / priority semantics。
- 矩阵使用冻结 FU `FU-6d0971786b`，`IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS` 作为 4C-17 overlay `triggerEffectKind`；不创建不存在的 `FU-a76d38727a`。

验证：

- B focused filter：通过 42/42。
- B backend full：通过 3384/3384。
- A backend full：通过 3384/3384。
- A frontend build：通过。
- A Chrome smoke：通过。
- `git diff --check`、矩阵 JSON/断言通过。

仍缺 P0/P1：

- P1：Ironclad Vanguard true stack migration 代表路径已关闭；state-based cleanup last-breath route 仍未官方化。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

4C-17 只关闭 Ironclad Vanguard true stack 代表性 migration，不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-16 Mechanical Trickster Trigger Baseline 审计

阶段 4C-16 审计入口：`docs/CURRENT_STAGE4C_BATCH16_MECHANICAL_TRICKSTER_TRIGGER_AUDIT.md`。本批由 B 完成 Mechanical Trickster / `OGN·239/298` / `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS` 的旧 immediate token create -> real TriggerQueue / Stack / Priority 迁移；D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、E matrix 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-16 可关闭的代表性子项：

- true stack `UNIT_DESTROYED` 后生成 `TRIGGER_QUEUED`。
- 单触发 auto-stack；多触发走 `ORDER_TRIGGERS` -> `StackItems`。
- priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3。
- face-down / standby Mechanical Trickster 不入队、不泄漏 prompt metadata、不创建 token。
- 旧 `P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed` fixture 已更新为 queue / priority semantics。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3382/3382。
- A frontend build / smoke：将在 D 文档后继续运行；本审计不提前记为完成。

仍缺 P0/P1：

- P1：Ironclad Vanguard 仍是旧 immediate compatibility，未迁移到 real trigger queue。
- P0：Kogmaw / Karthus / Undercover Agent 等 high-risk destroyed-family / friendly-destroyed holdbacks。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

4C-16 只关闭 Mechanical Trickster 代表性 migration，不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-15B Viktor Trigger Baseline 审计

阶段 4C-15B 审计入口：`docs/CURRENT_STAGE4C_BATCH15B_VIKTOR_TRIGGER_AUDIT.md`。前置 commit：`034f1ed checkpoint: complete stage 4C minion token family baseline`。本批由 B 完成 Viktor destroyed non-minion token trigger 最小官方化代表切片；D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、E matrix 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-15B 可关闭的代表性子项：

- 目标 FU：`FU-b5cb36a5c9`，覆盖 `ARC-006/006`、`OGN·246/298`、`OGN·246a/298`。
- visible surviving friendly Viktor source 看到另一名友方非随从单位被摧毁时触发。
- destroyed target 使用 pre-removal `CardObjectState` 判定：unit、same controller / friendly、not source、not `CardObjectTags.MinionTokenFamily`。
- source guard：Viktor still on field、face-up、non-standby、same controller、not cleanup removal set。
- 覆盖 true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED`。
- trigger path：`TriggerQueue` -> single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED`，在 controller base 创建 1-power Zaun minion `OGN·273/298`，并带 `TOKEN_FAMILY:MINION`。
- minion target 不入队、不造 token；hidden / face-down / standby / opponent source 不入队、不泄漏、不造 token；source also dying 不入队。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3380/3380。
- B `git diff --check`：pending / expected passed；A 将在文档后再次复核。

仍缺 P0/P1：

- P1：same source same stack / cleanup pass multiple non-minion friendly deaths 的 full official trigger-count matrix 仍保守 one source once。
- P0：Kogmaw / Karthus / Undercover Agent 等其他 destroyed-family / friendly-destroyed FUs。
- P0：完整 trigger engine、完整 effect resolution、trigger batch / 可选触发选择、完整 APNAP 组合。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：FAQ regression、1009 / 811 full-official 覆盖、最终正式 18-step E2E。

4C-15B 只关闭 Viktor 代表性 baseline，不宣称 full-official，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-15A Minion Token Family 前置模型审计

阶段 4C-15A 审计入口：`docs/CURRENT_STAGE4C_BATCH15A_MINION_TOKEN_FAMILY_AUDIT.md`。本批由 B 完成 token subtype / family / minion classification 最小前置模型；不实现 Viktor trigger 本体。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、E matrix 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-15A 可关闭的前置子项：

- 新增稳定 tag：`TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`。
- `P6TokenFactoryCatalog` 的官方三种“随从”token factory（`OGN·271/298`、`OGN·272/298`、`OGN·273/298`）带该 tag。
- `CoreRuleEngine.CreateBaseUnitTokens` 对 `tokenName == "随从"` 自动追加 `CARD_TYPE:UNIT` + `TOKEN_FAMILY:MINION`。
- Viktor legend 直接创建随从路径同步带 `TOKEN_FAMILY:MINION`。
- Common Cause、Future Forge、Faithful Craftsman、Vanguard Captain、Mechanical Trickster、Viktor legend、battlefield held minion 等路径可生成带 marker 的随从 token。
- 普通单位不带 marker；Gold / Sprite / Warhawk / Sand Soldier 等非“随从”token factory 不带 marker。
- hidden face-down standby 即使内部带 marker，对手 snapshot 仍不泄漏 tags / cardNo / power。

验证记录：

- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed，3375/3375。
- `git diff --check`：passed。

仍缺 P0/P1：

- P0：Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 本体。
- P0：destroy / cleanup 入队时 destroyed target pre-removal state 判定。
- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：1009 / 811 full-official 覆盖、FAQ regression、最终正式 18-step E2E。

4C-15A 只关闭 token classification 前置 blocker 的一部分；未改协议 record 字段，未改前端，不宣称 READY / READY-CANDIDATE。

## 2026-05-10 阶段 4C-15 Viktor Feasibility Blocker 审计

阶段 4C-15 审计入口：`docs/CURRENT_STAGE4C_BATCH15_VIKTOR_BLOCKER.md`。B 本轮只做只读可行性检查，未修改代码，未新增测试。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-15 候选范围：

- 候选 FU：`FU-b5cb36a5c9`。
- 候选语义：Viktor destroyed non-minion token trigger。
- 4C-14 Savage Jawfish 已 checkpoint：`2deef64 checkpoint: complete stage 4C savage jawfish trigger batch`。

4C-15 阻断原因：

- 当前 `CardObjectTags` 没有 `Minion` / `随从` / subtype 字段。
- 当前 `CardObjectState` 没有稳定 token family / subtype / `isMinion` 字段。
- 多个“随从”创建路径经 `CreateBaseUnitTokens` 只落成 `CARD_TYPE:UNIT`，不保留 `cardNo` / `tokenName` / `TokenFamilyName`。
- 摧毁时无法可靠区分“随从单位”和普通单位。
- Viktor fixtures 当前也描述 destroyed-listener / non-minion filtering / minion-token path deferred。

4C-15 审计结论：

- 不建议硬编码 Viktor 的“非随从”判定。
- 4C-15 未实现，未新增测试，不关闭 `FU-b5cb36a5c9`。
- 该项保留为 P0/P1 blocker，需要先冻结 token subtype / family 模型或由用户裁定官方解释。
- 后续建议：先做 `CardObjectState` subtype / token-family 模型和随从 token factory 统一写入，再做 Viktor；或者用户确认跳过 Viktor，改做不依赖“非随从”分类的下一个 safe FU。

仍缺 P0/P1：

- P0：Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger。
- P0：token subtype / token-family / minion classification 模型。
- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：Kogmaw / Karthus / Undercover Agent 等后续触发族。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。

## 2026-05-10 阶段 4C-14 Savage Jawfish Trigger Enqueue Baseline 审计

阶段 4C-14 审计入口：`docs/CURRENT_STAGE4C_BATCH14_SAVAGE_JAWFISH_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**，不得标记 READY / READY-CANDIDATE。

4C-14 可关闭的 P0 子项：

- Savage Jawfish / 《凶残颚鱼》（`CATALOG` UNL-129/219，`FU-bd94334cc5`）trigger enqueue baseline。
- true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均进入 `TriggerQueue`。
- 多触发走 `ORDER_TRIGGERS`；单触发 auto-stack。
- priority 双方 pass 后 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1。
- Guard：来源必须仍在场、face-up、non-standby、同 controller，不能是被摧毁对象 / cleanup removal set。
- hidden face-down / standby / opponent-controlled source 不 enqueue、不泄漏、不加经验。
- 旧 P79 Savage Jawfish fixture 已更新为 queue / stack / priority semantics。
- 同一来源同一 cleanup / stack pass 多个友方被摧毁时，当前最小切片保守 cap 为每 source 每 pass 最多一次；这不是 full official trigger-count matrix，作为 P1 / TODO 保留。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Stack destruction `UNIT_DESTROYED` 触发入队：`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Savage Jawfish experience +1：`CATALOG` UNL-129/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；experience 相关官方页码暂标 TODO，不编造。
- Source legality / visibility guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。
- Per-source per-pass cap：当前工程保守边界；精确官方触发次数证据暂标 TODO，不编造。

验证记录：

- Focused `RealTriggerQueueTests|P79SavageJawfish`：通过，33/33。
- Backend full：passed，3374/3374。
- Frontend build：passed。
- Chrome smoke：passed。
- Stage 3 preflight：passed。
- `git diff --check`：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup / stack pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：Savage Jawfish 同一来源同一 pass 多个友方被摧毁时的 full official trigger-count matrix。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-13 Stack Destroyed Trigger Migration 审计

阶段 4C-13 审计入口：`docs/CURRENT_STAGE4C_BATCH13_STACK_DESTROYED_TRIGGER_MIGRATION_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-13 不新增 FU；本批迁移 / 关闭 4C-11 / 4C-12 留下的 P1：Ghostly Centaur + Resonant Soul true stack destruction 旧 immediate compatibility -> real trigger queue。

覆盖 FUs：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）。
- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）。

4C-13 可关闭的 P1 / P0 子项：

- P1：Ghostly Centaur true stack destruction friendly-destroyed power +2 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P1：Resonant Soul true stack destruction first-friendly-destroyed draw 从旧 immediate helper 迁移到真实 `TriggerQueue` / stack / priority 语义。
- P0 子项：两个既有 FU 现在同时具备 cleanup representative 与 true stack destruction representative 的 real enqueue 证据。
- 官方化路径为：true stack destruction 非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` or single-trigger auto-stack -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1。
- cleanup path 继续通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 stack helper，避免重复入队。
- hidden / face-down / standby / opponent-controlled source 不入队；source 必须留场、正面、非 standby、同 controller。
- Resonant 继续尊重 `DestroyedUnitOwnerIdsThisTurn`。
- 旧 P79 tests 已更新为 queue / stack / priority 语义。
- 本批不覆盖 Viktor / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Stack destruction `UNIT_DESTROYED` 触发入队：`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Ghostly Centaur power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Cleanup / stack helper 防重复：`CORE-260330` p31-p33 rules 318-324；工程事件来源契约。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaur|FullyQualifiedName~P79ResonantSoul"` 通过，30/30。
- B full backend：passed，3370/3370。
- A backend full：passed，3370/3370。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 通过。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-12 Resonant Soul Cleanup Trigger Enqueue 审计

阶段 4C-12 审计入口：`docs/CURRENT_STAGE4C_BATCH12_RESONANT_SOUL_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-12 可关闭的 P0 子项：

- Resonant Soul / 《残响之魂》（`CATALOG` OGN·118/298，`FU-c146331876`）first-friendly-destroyed draw state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Resonant Soul source，owner not already in `DestroyedUnitOwnerIdsThisTurn` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `CARD_DRAWN` 1。
- hidden / face-down / standby / opponent-controlled Resonant Soul source 不入队、不显示 prompt metadata、不抽牌。
- Resonant Soul source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set；同回合已经记录 destroyed owner 时不入队。
- true stack destruction Resonant Soul 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；cleanup 事件跳过旧 immediate helper 以避免重复；这是 4C-12 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Resonant Soul first-friendly-destroyed draw：`CATALOG` OGN·118/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Per-owner first destroy guard：`CATALOG` OGN·118/298；`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂标 TODO，不编造。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂标 TODO，不编造。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn"` 通过，27/27。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3368/3368。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Ghostly 后续 / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整“每回合首次”时序、完整同时死亡触发次数、同一 cleanup pass 多对象排序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：true stack destruction Resonant Soul 从 immediate compatibility 迁移到 `TriggerQueue`。

## 2026-05-10 阶段 4C-11 Ghostly Centaur Cleanup Trigger Enqueue 审计

阶段 4C-11 审计入口：`docs/CURRENT_STAGE4C_BATCH11_GHOSTLY_CENTAUR_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-11 可关闭的 P0 子项：

- Ghostly Centaur / 《幽魂半人马》（`CATALOG` UNL-068/219，`FU-0f2c4a3ea5`）friendly-destroyed power state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` / `UNIT_DESTROYED` -> visible surviving friendly Ghostly source -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `POWER_MODIFIED_UNTIL_END_OF_TURN` +2。
- hidden / face-down / standby / opponent-controlled Ghostly source 不入队、不显示 prompt metadata、不加战力。
- Ghostly source 同时在本轮 cleanup removal set 中时保守不入队；本批不裁定完整同时死亡触发次数。
- 同一 Ghostly source 在同一个 cleanup pass 中最多入队一次；这是 4C-11 的保守边界，不代表完整规则。
- 真实 stack destruction Ghostly 旧 P79 immediate compatibility 保留，未迁移到 `TriggerQueue`；这是 4C-11 当时 P1，4C-13 后已迁移关闭。
- 本批不覆盖 Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Ghostly Centaur friendly-destroyed power +2：`CATALOG` UNL-068/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Cleanup removal-set guard：`CORE-260330` p31-p33 rules 318-324；精确同时死亡触发 FAQ 页码暂标 TODO，不编造。
- Hidden / face-down / standby / opponent source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- B focused：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RealTriggerQueueTests|FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed"` 通过，23/23。
- B diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3364/3364。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：Viktor / Resonant Soul / Kogmaw / Karthus / Undercover Agent 等 friendly-destroyed 或相关触发族。
- P0：完整同时死亡触发次数、同一 cleanup pass 多对象时序、source 同时死亡时的官方裁定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- 历史 P1（4C-13 后已关闭）：真实 stack destruction Ghostly 从 immediate compatibility 迁移到 `TriggerQueue`。

## 2026-05-10 阶段 4C-10 Unsung Hero Cleanup Trigger Enqueue 审计

阶段 4C-10 审计入口：`docs/CURRENT_STAGE4C_BATCH10_UNSUNG_HERO_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-10 可关闭的 P0 子项：

- Unsung Hero / 《无名英雄》（`CATALOG` SFD·167/221，`FU-1701d1d89a`）`UNSUNG_HERO_LAST_BREATH_DRAW_2_IF_POWERFUL` state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Unsung Hero power >= 5 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN` x2。
- power < 5 cleanup 路径不入队、不抽牌。
- hidden / face-down / standby Unsung Hero cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 现有 explicit destroy P79 Unsung immediate compatibility 保留。
- 严格边界：本批只用 `CardObjectState.Power >= 5` 代表强力；不覆盖 LayerEngine / effective power / temporary modifier；不覆盖 battlefield objectLocation 全矩阵；不迁移 explicit destroy。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Unsung Hero powerful last-breath draw：`CATALOG` SFD·167/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Power threshold guard：`CATALOG` SFD·167/221；`CORE-260330` p57 rule 413.4；相关强力证据见 `rules-evidence-index.md` strong / powerful fixtures。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3361/3361。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：effective power / LayerEngine、temporary modifier 和完整强力判定。
- P0：battlefield objectLocation matrix。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-9 Poro Cleanup Trigger Enqueue 审计

阶段 4C-9 审计入口：`docs/CURRENT_STAGE4C_BATCH9_PORO_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-9 可关闭的 P0 子项：

- Sad Poro / 《哀哀魄罗》（`CATALOG` SFD·036/221，`FU-f8bfd5c6f9`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Sad Poro / 《哀哀魄罗》（`CATALOG` UNL-221/219，`FU-938b749c23`）`SAD_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- Loyal Poro / 《忠忠魄罗》（`CATALOG` UNL-156/219，`FU-0415e3b46d`）`LOYAL_PORO_LAST_BREATH_DRAW_1` state-based cleanup real trigger enqueue representative。
- 支撑来源为 Starfall / 《星落》（`CATALOG` OGN·029/298）造成 lethal damage 后的 state-based cleanup。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible base-zone Poro condition -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- Sad 条件：base-zone、visible、非 face-down、非 standby，且同位置无其他友方正面非待命单位时触发。
- Loyal 条件：base-zone、visible、非 face-down、非 standby，且同位置有至少一个其他友方正面非待命单位，并且该友方不在本轮 cleanup removal set 中时触发。
- hidden / face-down / standby Poro cleanup 路径不入队、不显示 prompt metadata、不抽牌。
- 同位置其他友方也同时被 cleanup 摧毁的落单判定未官方化；runtime 对 Loyal 采取保守不入队，本批不宣称完整规则。
- 现有 explicit destroy P79 Sad / Loyal immediate compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Sad Poro condition draw：`CATALOG` SFD·036/221；`CATALOG` UNL-221/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Loyal Poro condition draw：`CATALOG` UNL-156/219；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：21/21 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3358/3358。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：battlefield objectLocation Poro condition matrix。
- P0：simultaneous cleanup condition timing，尤其同位置其他友方也同时被 cleanup 摧毁时的 Sad / Loyal 判定。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-8 Scouting Warhawk Cleanup Trigger Enqueue 审计

阶段 4C-8 审计入口：`docs/CURRENT_STAGE4C_BATCH8_SCOUTING_WARHAWK_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze、E evidence 文件或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-8 可关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）state-based cleanup lethal damage real trigger enqueue representative。
- 支撑测试使用 Starfall / 《星落》（`CATALOG` OGN·029/298）作为 lethal damage + state-based cleanup source；本批不是 explicit destroy source 的新增覆盖。
- 官方化路径为：Starfall lethal damage -> state-based cleanup `LETHAL_DAMAGE` `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk cleanup 路径不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- 4C-7 explicit destroy 路径和 single-trigger compatibility 保留。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3352/3352。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：Sad / Loyal Poro（4C-9 后续已补 state-based cleanup 条件抽牌代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-7 Scouting Warhawk Trigger Enqueue 审计

阶段 4C-7 审计入口：`docs/CURRENT_STAGE4C_BATCH7_SCOUTING_WARHAWK_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-7 可关闭的 P0 子项：

- Scouting Warhawk / 《侦察飞鹰》（`CATALOG` OGN·216/298，`FU-0500c77a70`）explicit destroy real trigger enqueue representative。
- 支撑测试使用 Spirit Fire / 《妖异狐火》（`CATALOG` OGN·256/298）作为 explicit destroy source；本批不是 state cleanup。
- 官方化路径为：explicit destroy `UNIT_DESTROYED` -> visible Scouting Warhawk `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `RUNES_CALLED`。
- hidden / face-down / standby Warhawk 不入队、不显示 prompt metadata、不触发 `RUNES_CALLED`。
- single-trigger compatibility 保留；既有 `P79ScoutingWarhawk` 测试继续通过。
- 本批没有协议 / 前端字段变化，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- Scouting Warhawk last-breath enqueue：`CATALOG` OGN·216/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Spirit Fire explicit destroy source：`CATALOG` OGN·256/298；`CORE-260330` p39-p42 rules 355-356；`CORE-260330` p62-p63 rule 428。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e。
- Hidden / face-down / standby source guard：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：9/9 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3350/3350。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。
- A git diff check：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：state cleanup Warhawk（4C-8 后续已补代表路径；不再作为当前独立 P0 子项）。
- P0：其他 last-breath / destroyed / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18-step E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby / face-down trigger policy 文档化。

## 2026-05-10 阶段 4C-6 Honest Broker Cleanup Trigger Enqueue 审计

阶段 4C-6 审计入口：`docs/CURRENT_STAGE4C_BATCH6_HONEST_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-6 可关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两个 Honest Broker，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED`。
- hidden / standby Honest Broker 不入队、不创建 token，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不改协议或前端，不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused RealTriggerQueueTests：6/6 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3348/3348。
- A frontend build：通过；只有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：hidden / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

## 2026-05-10 阶段 4C-5 State-Based Cleanup Trigger Enqueue 审计

阶段 4C-5 审计入口：`docs/CURRENT_STAGE4C_BATCH5_STATE_CLEANUP_TRIGGER_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵 JSON / coverage baseline / risk top20 / stage4B freeze 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-5 可关闭的 P0 子项：

- State-based cleanup `LETHAL_DAMAGE` -> visible Watchful Sentinel last-breath enqueue representative。
- 服务端只接入可见、非 face-down、非 standby 的 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）。
- Starfall / 《星落》（`CATALOG` OGN·029/298）造成致命伤害后，state-based cleanup `LETHAL_DAMAGE` 摧毁两名 Watchful，并串成 `TRIGGER_QUEUED` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- hidden / standby Watchful Sentinel 不入队，避免 trigger metadata 泄漏不可见或待命来源。
- 本批不授予 full-official，不扩完整 trigger engine。

规则证据入口：

- State-based cleanup lethal destroy：`CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20。
- Watchful Sentinel last-breath enqueue：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Starfall lethal cleanup representative：`CATALOG` OGN·029/298；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p39-p42 rules 355-356。
- Hidden / standby source redaction：`CORE-260330` p4-p8 rules 107-129；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused RealTriggerQueueTests：4/4 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3346/3346。
- A frontend build：通过；只有既有 SignalR / Rollup `PURE` 注释警告。
- A Chrome smoke：通过。
- A stage3 preflight：通过。
- A B-file diff check：`git diff --check -- src/Riftbound.Engine/CoreRuleEngine.cs tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs` 通过。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed / last-breath / friendly-destroyed functional units。
- P0：隐藏 / face-down 原始触发建模和 viewer 级 metadata 全路径。
- P0：完整 effect resolution 与 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：trigger batch 正式 DTO、触发来源解释字段、hidden / standby trigger policy 文档化。

## 2026-05-10 阶段 4C-4 触发支付 / 拒付审计

阶段 4C-4 审计入口：`docs/CURRENT_STAGE4C_BATCH4_TRIGGER_PAYMENT_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-4 可关闭的 P0 子项：

- Treasure Pile / 《珍宝堆》（`CATALOG` SFD·220/221）征服触发从自动结算改为服务端权威 `TRIGGER_PAYMENT`。
- `PAY_COST` 在该窗口支持 `SPEND_MANA:1` 与 `DECLINE` 两个服务端合法选项。
- 支付成功会扣 1 点法力、创建休眠“金币”装备指示物并关闭窗口；拒付会关闭窗口且不扣费、不创建指示物。
- wrong player、stale prompt、unknown choice、duplicate choice、pay+decline、malformed payload、insufficient mana 均已覆盖拒绝 / no mutation。
- 前端只补事件中文 label，不新增支付、触发、战场控制或胜负本地裁决。

规则证据入口：

- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5；`CATALOG` SFD·220/221。
- `PAY_COST` runtime validation：`CORE-260330` p39-p42 rules 356-357；p52-p55 rules 403-405。
- Battlefield conquer trigger：`CORE-260330` p77-p78 rules 454-461；`CATALOG` SFD·220/221。

验证记录：

- A focused trigger payment：11/11 通过。
- A trigger ordering regression：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3344/3344。
- A frontend build / Chrome smoke / stage3 preflight：通过；首次并行 preflight 因本地 API 端口竞争失败，顺序重跑通过。

仍缺 P0/P1：

- P0：完整 PaymentEngine。
- P0：`SFD·220/221` 之外的 triggered-cost functional units。
- P0：完整 trigger engine、state-based cleanup trigger enqueue、完整 effect resolution、FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TRIGGER_PAYMENT` 长期 DTO / 解释字段 / UX 契约冻结。

## 2026-05-09 阶段 4C-3 绝念真实触发入队审计

阶段 4C-3 审计入口：`docs/CURRENT_STAGE4C_BATCH3_LAST_BREATH_ENQUEUE_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-3 可关闭的 P0 子项：

- `HonestBrokerCardNo` / `HONEST_BROKER_LAST_BREATH_CREATE_GOLD` 从直接结算扩展到真实多触发路径。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）遗言金币代表路径已串成：`UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `EQUIPMENT_TOKEN_CREATED`。
- 跨控制者真实 last-breath APNAP 默认顺序可直接提交；非法跨控制者排序 rejected 且 no mutation。
- 单触发 Watchful Sentinel / Honest Broker 仍保留即时结算兼容；本批不宣称统一单触发策略完成。

4C-2 + 4C-3 合并口径：

- Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）已有真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN` 代表证据。
- Honest Broker / 《诚实掮客》（`CATALOG` SFD·155/221）已有真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED` 代表证据。
- 这只证明两个 representative last-breath family members 已接入 real enqueue，不等于完整 last-breath engine 或完整 trigger engine。

规则证据入口：

- Honest Broker last-breath enqueue：`CATALOG` SFD·155/221；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：13/13 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3339/3339。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family。
- P0：state-based cleanup 触发入队。
- P0：trigger payment / decline / payment failure。
- P0：FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段，以及单触发即时结算兼容策略文档化。

## 2026-05-09 阶段 4C-2 真实触发入队审计

阶段 4C-2 审计入口：`docs/CURRENT_STAGE4C_BATCH2_REAL_TRIGGER_ENQUEUE_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-2 可关闭的 P0 子项：

- 真实 `UNIT_DESTROYED` 路径中，多张 Watchful Sentinel / 《警觉的哨兵》（`CATALOG` OGN·096/298）遗言抽牌触发已接入 `TriggerQueue`。
- 多触发代表路径已串成：`TriggerQueue` -> `ORDER_TRIGGERS` prompt -> `StackItems` -> pass priority -> `TRIGGER_RESOLVED` / `CARD_DRAWN`。
- 单个 Watchful Sentinel 仍保留即时结算兼容；本批不宣称统一单触发策略。
- 跨控制者 APNAP 默认 `orderedTriggerIds` 可直接提交并 accepted；非法跨控制者排序 rejected 且 no state mutation。
- 本批未改协议 / 前端。

规则证据入口：

- 真实卡牌触发入队：`CATALOG` OGN·096/298；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- `ORDER_TRIGGERS` / stack / priority：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- State-based cleanup triggers：`CORE-260330` p31-p33 rules 318-324；更精确 FAQ 页码暂标 TODO，不编造。

验证记录：

- A focused：11/11 通过。
- A backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3338/3338。
- A frontend build：passed。
- A Chrome smoke：passed。
- A stage3 preflight：passed。

仍缺 P0/P1：

- P0：完整 trigger engine。
- P0：其他 destroyed-family / friendly-destroyed / attack / conquer 触发族。
- P0：state-based cleanup 触发统一入队。
- P0：trigger payment / decline / payment failure。
- P0：完整 effect resolution 与完整 FAQ regression。
- P0：1009 / 811 full-official 覆盖、最终正式 18 步 E2E。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、真实触发解释字段，以及单触发即时结算兼容策略文档化。

## 2026-05-09 阶段 4C-1 触发排序审计

阶段 4C-1 审计入口：`docs/CURRENT_STAGE4C_BATCH1_TRIGGER_ORDERING_AUDIT.md`。D 只更新规则证据 / P0-P1 审计口径；不修改服务端、前端、覆盖矩阵、A checkpoint 或 `riftbound-dotnet.sln`。项目仍 **NOT READY**。

4C-1 可关闭的 P0 子项：

- `ORDER_TRIGGERS` 从 3D 最小 runtime window 升级为保守 APNAP controller-block 子集。
- prompt metadata 约定已更新：`orderedTriggerIds` 是合法 APNAP resolution top-first 默认提交顺序，`triggerIds` 是 raw queue order。
- `legalOrderingConstraints` 已明确 APNAP policy、top-first semantics、controller block order、legal resolution block order、跨控制者不可重排、同控制者可重排。
- runtime 校验覆盖合法排序 accepted；非法跨控制者重排 rejected 且 no state mutation。
- `BuildCorePrompts` 中 `ORDER_TRIGGERS` window 优先于 `START_BATTLE` / task prompt。
- battle initial stack 代表证据已补：active battle attacker / defender 初始触发 -> `ORDER_TRIGGERS` -> stack priority。
- trigger prompt / snapshot 对不可见 face-down standby source 做 viewer 级脱敏。

规则证据入口：

- Trigger ordering / APNAP：`CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3。
- Trigger payment / decline：`CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5。
- Battle initial stack：`CORE-260330` p35-p36 rules 341-348；`CORE-260330` p77-p78 rules 454-461；`JFAQ-251023` p2-p4 q2.2-q2.4。
- Hidden information / face-down standby source：`CORE-260330` p4-p8 rules 107-129；待命 / 显露相关 evidence 继续复用 `CORE-260330` p39-p42 rules 355-356。更精确 FAQ 页码暂标 evidence TODO，不编造。

验证记录：

- A 后端 full test：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，3337/3337。

仍缺 P0/P1：

- P0：完整 trigger engine、完整 effect resolution、真实卡牌全触发生成。
- P0：trigger payment / decline / payment failure 与完整 PaymentEngine 统一。
- P0：完整 APNAP 多玩家独立排序；4C-1 只覆盖保守 controller-block 子集。
- P0：battle initial stack 全官方规则、攻防触发特殊排序、battle response window 与 FAQ 回归。
- P0：最终正式 18 步 E2E、1009 张卡 full-official 覆盖。
- P1：`TriggerInstance` / `TriggerBatchPromptView` / `legalOrderingConstraints` 正式 DTO、产品解释字段、多语言 UI 文案和证据链接。

## 2026-05-09 阶段 3D 第三阶段收口审计

阶段 3D 审计入口为 `docs/CURRENT_STAGE3_COMPLETION_AUDIT.md`。D 只修改文档；B/C/E 已完成 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项。项目仍 **NOT READY**。

第三阶段收口判断：

| 阶段 | 已关闭子项 | 仍未关闭 |
| --- | --- | --- |
| 3A | Chrome route smoke、三类复杂命令 typed mapper、`PAY_COST` 最小 runtime、前端外壳不裁决规则 | 完整 PaymentEngine、`ORDER_TRIGGERS`、battle / spell duel、最终 18 步 E2E |
| 3B | battlefield / standby snapshot、非法待命 cleanup 代表路径、control / held / conquer 代表结果、central cleanup queue 最小 task view | cleanup queue 全触发面、control freeze/release、standby 全时机、scoring order |
| 3C | spell duel close 代表链、battle view / resolution、`ASSIGN_COMBAT_DAMAGE` 最小 runtime、battle cleanup -> control update 代表链 | 完整 battle lifecycle、full-rule damage assignment、替代/预防、LayerEngine |
| 3D | `ORDER_TRIGGERS` 最小 runtime window / UI / evidence；第三阶段收口审计、阶段 4 / 最终验收边界 | 完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment |

当前证据状态：

- priority / focus：已有 `PASS_PRIORITY`、`PASS_FOCUS`、prompt stamp、spell duel focus 代表证据；完整 `SPELL_DUEL_ACTION`、全反应/迅捷/反制链和触发排序交织仍缺。
- spell duel close：3C 已有 close -> damage assignment -> cleanup/control update 代表链；所有 close -> next task、非战斗法术对决和触发排序仍缺。
- battle lifecycle：已有 `BattleState`、`BattleResolutionState`、多攻防代表路径和 3C 最小 damage assignment；完整 battle task、初始战斗结算链、响应窗口和 freeze/release 仍缺。
- damage assignment：3C 最小 prompt / validation / submit / reject / simultaneous commit 已关闭；壁垒、后排、同优先级、负战力、不可分配、替代/预防矩阵仍缺。
- battle cleanup：3C 已有 battle damage -> lethal cleanup -> battle close -> battlefield control update；cleanup queue 全触发面、LayerEngine、control freeze/release 全路径仍缺。
- battlefield control update / conquer / hold / standby visibility / cleanup queue：3B/3C 已有代表证据；正式 DTO、全时机、全触发面和 scoring order 仍缺。

`ORDER_TRIGGERS` / 多触发排序 3D 证据状态：

- 已有：`ORDER_TRIGGERS(triggerIds)` command/schema skeleton、`INVALID_PAYLOAD`、`TRIGGER_QUEUED` / `TRIGGER_RESOLVED` 代表事件。
- B 已实现最小 runtime window：prompt metadata 包含 `orderingPlayerId`、`orderedTriggerIds`、`triggerIds`、`triggers`、`triggerChoices`、`legalOrderingConstraints`、`triggeredByEventKind`。
- command 支持 `orderedTriggerIds` 并兼容 `triggerIds`；合法排序清空 `TriggerQueue`、按顺序加入 `StackItems`、设置 priority player，并广播 `TRIGGERS_ORDERED` / `TRIGGERS_MOVED_TO_STACK`。
- B 验证：`ConformanceFixtureShapeTests` 109/109 通过；full `dotnet test Riftbound.slnx --no-restore` 3333/3333 通过；`git diff --check` 通过。
- C 已实现 `ORDER_TRIGGERS` UI，上移 / 下移排序，提交 `orderedTriggerIds`，不本地结算；C 侧 build / smoke / `stage3-preflight.mjs` 通过。
- E 已补 stage3D 矩阵 overlay 和 `ORDER_TRIGGERS` 证据文档。
- 规则入口：`CORE-260330` rules 333-340、383.3.d-383.3.e；`JFAQ-251023` q2.2-q2.3、q2.5；battle initial stack 还关联 rules 454-461 与 q2.3-q2.4。
- 仍缺 P0：完整 trigger engine、完整 effect resolution、APNAP / 跨控制者复杂排序、battle initial stack 全规则、trigger cost / decline / payment。

阶段 4 建议范围：完整 trigger engine / APNAP / battle initial stack、priority/focus 与 `SPELL_DUEL_ACTION`、battle/control freeze-release、cleanup queue 全触发面、full damage assignment matrix、PaymentEngine / LayerEngine。最终验收再启动正式 18 步 E2E、1009 全量、replay/recovery/determinism 与产品 UI polish。

第三阶段 A final validation 已通过，第三阶段可判定 **DONE**；当前服务端审计确认 3D 最小 `ORDER_TRIGGERS` 子项关闭，项目仍 **NOT READY**。

## 2026-05-09 阶段 3C D 范围修正

当前执行范围已切换为阶段 3C，证据入口为 `docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`，对齐 3B checkpoint `a74beac`。3C 名称为 **Spell duel / Battle / ASSIGN_COMBAT_DAMAGE / Battle cleanup 规则证据与最小官方化切片**。

3C 只收口：

- Spell duel lifecycle：普通 / 法术对决、开环 / 闭环、焦点、让过、反应窗口、关闭后回到正确任务。
- Battle lifecycle：战斗任务、参战单位、战斗前法术对决、战斗响应、伤害分配、战斗结果、战后清理。
- `ASSIGN_COMBAT_DAMAGE` runtime：从现有 schema/shell 推进到服务端 prompt、合法分配约束、提交校验与零副作用失败。
- Battle cleanup：战斗伤害造成后，攻防身份、致命伤害、战斗结果、控制/待命/战场结果与 cleanup queue 的衔接。

3C 不进入：最终 18 步 E2E、1009 张卡全量、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine / `DECLINE_PAY_COST`、LayerEngine、全路径 replay/determinism，也不把 3B battlefield / standby / control / conquer 全量重新拉入本轮。4C-1 后 `ORDER_TRIGGERS` 已收窄出 controller-block 子项，但完整触发系统仍 P0。

3C 当前关闭候选 P0 子项：

| 候选 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- |
| 3C-CAND-001 spell duel focus/pass/close 最小链 | `SpellDuelState`、`PASS_FOCUS`、焦点恢复、swift/反应 timing context 有代表证据；3C 已补 close -> damage assignment -> cleanup/control 连续测试 | B/C/E/D | 完整 `SPELL_DUEL_ACTION`、全反应链和触发排序仍 P0 |
| 3C-CAND-002 battle view / battle resolution 最小 task | `BattleState`、`BattleResolutionState`、`BATTLE_DECLARED`、`DAMAGE_APPLIED` 与 snapshot result 有代表证据 | B/C/E/D | 把 START_BATTLE -> result/cleanup 串成可审计最小链 |
| 3C-CAND-003 `ASSIGN_COMBAT_DAMAGE` runtime 最小 prompt | 3C 已开放最小 runtime prompt、damagePool/legalTargets/threshold/requiredAssignments、合法/非法/stale 测试和 simultaneous damage commit | B/E/D | 完整壁垒/后排/同优先级/负战力/不可分配矩阵仍 P0 |
| 3C-CAND-004 battle cleanup 最小结果链 | 3C 已覆盖 battle damage、致命伤害 cleanup、battle close、battlefield control update | B/E/D | control freeze/release、替代/预防、LayerEngine 与 cleanup queue 全触发面仍 P0 |

3C 仍存在 P0/P1：

- 3C-P0-001 spell duel 完整 lifecycle 未完成：3C 已有 focus/pass/close 代表链；全反应链、复杂 `SPELL_DUEL_ACTION`、触发排序和全部 close -> next task 全路径仍缺。
- 3C-P0-002 battle 完整 lifecycle 未完成：完整 battle task、战斗响应窗口、所有多攻防组合和初始栈仍缺。
- 3C-P0-003 `ASSIGN_COMBAT_DAMAGE` full-rule runtime 未完成：3C 最小 prompt / validation / simultaneous commit 已落地；完整壁垒/后排/同优先级/负战力/不可分配矩阵仍缺。
- 3C-P0-004 battle cleanup 全路径未完成：3C 已有 battle damage -> cleanup -> control update 代表链；替代/预防、LayerEngine、control freeze/release 与 cleanup queue 全触发面仍缺。
- 3C-P0-005 3C smoke/evidence 不是最终 18 步 E2E。
- 3C-P1-001 前端 `timing.spellDuel/battle/battleResolutions` 仍是 dictionary view，正式 DTO 未冻结。

## 2026-05-09 阶段 3B 前置产物 / 防误读

阶段 3B 计划入口为 `docs/CURRENT_STAGE3B_PLAN.md`。3B 为 3C 提供 battlefield / standby / control / conquer / central cleanup queue 的前置 task/view 口径；3C 不回滚 3B 产物，也不把 3B 完整 lifecycle 误称为已 READY。

3B 只收口：

- Battlefield / standby 的最小权威 snapshot 与 task 视图。
- Control / contest / held / conquer 的代表性战后结果、事件顺序与重连展示。
- Central cleanup queue 的最小 official slice：state-based cleanup、active task、blocking guard、非法待命 / 致命伤害 / 未贴附装备等代表任务。
- 前端只读展示这些服务端字段，不裁决控制权、待命合法性、清理、得分或胜负。

3B 未进入且当前仍未关闭：最终 18 步 E2E、1009 张卡全量、完整 battle / spell duel lifecycle、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 PaymentEngine、LayerEngine、全路径 replay/determinism。4C-1 后 `ORDER_TRIGGERS` 已收窄出 controller-block 子项，但完整触发系统仍 P0。

3B 当前关闭候选 P0 子项：

| 候选 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- |
| 3B-CAND-001 battlefield/standby snapshot 只读字段 | `BattlefieldStates`、具体战场移动、occupants、standby、pending task 摘要已有代表测试 | B/C/D | D 记录字段、规则依据和仍缺完整 lifecycle |
| 3B-CAND-002 非法待命 cleanup 代表路径 | `PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask` 与 battlefield contest smoke 证明一条非法待命清理链 | B/E/D | 补 freeze 期间不提前移除、下一次 cleanup 时机和隐藏信息说明 |
| 3B-CAND-003 control / held / conquer 代表结果 | 战后 `BATTLEFIELD_CONTROL_RESOLVED`、`BATTLEFIELD_HELD`、`BATTLEFIELD_CONQUERED` 与 `BattlefieldResolutions` 已有代表路径 | B/E/D | 收一条 held、一条 conquer 的事件顺序、snapshot/reconnect 证据 |
| 3B-CAND-004 central cleanup queue 最小 task view | `PendingTaskQueue` phase/activeTaskId/isBlocking/tasks 及 spell duel/battle active task 入口已有 | B/C/D | 说明这是最小 view，不关闭全触发面 cleanup queue |

3B 仍存在 P0/P1：

- 3B-P0-001 cleanup queue 全触发面：所有 command / stack resolve / trigger resolve / move / enter / leave / damage / power change 统一 enqueue 和 repeat-until-stable 仍未完成最终审计。
- 3B-P0-002 control freeze/release：战斗/法术对决期间控制权冻结与关闭后释放仍需代表 fixture。
- 3B-P0-003 delayed illegal standby removal：失控待命在下一次 cleanup 移除的完整时机、冻结期间不提前移除、所有 standby 卡族仍需补。
- 3B-P0-004 held/conquer scoring order：全战场卡、得分替代、付费触发拒付、同时触发排序仍未关闭。
- 3B-P0-005 3B smoke 证据：已有后台 headless Chrome/CDP contest smoke，但不是最终正式 18 步 E2E。
- 3B-P1-001 前端字段仍偏 DevUi：`timing.pendingTaskQueue` / battlefield result 仍需稳定正式 DTO。

## 2026-05-09 阶段 3A 已关闭子项 / 防误读

阶段 3A 计划入口为 `docs/CURRENT_STAGE3A_PLAN.md`。A 已验收 3A 子项；旧“3A OPEN / 待验证”表述已被 3A 计划和本节替代。3A 关闭不等于 Stage 3、3B、最终 18 步 E2E 或 READY。

3A 已收口：

- Smoke 基线。
- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 强类型复杂命令解析。
- `PAY_COST` 最小 runtime 切片。
- 对战桌面外壳安全接线，前端不得裁决规则。

3A 已关闭子项：

| P0 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- |
| 3A-P0-001 Chrome smoke 基线 | 已关闭：`npm run smoke:chrome -- --start-api` 验证 API / DevUi / Chrome headless-CDP 与 7 个基础路由 | C / A / D | 3B 再扩双人连续流程与隐藏信息断言 |
| 3A-P0-002 三类复杂命令强类型映射 | 已关闭：三类 JSON command -> typed command mapper 已落地，malformed payload 稳定拒绝，后端 full test 3324/3324 通过 | B / D | 后续 runtime 逐类开放时补对应合法性测试 |
| 3A-P0-003 `PAY_COST` 最小 runtime | 已关闭最小切片：pending payment prompt、choices、合法提交、stale/invalid/零副作用测试已通过 | B / E / C / D | 完整 PaymentEngine、decline、替代/额外费用仍是后续 P0 |
| 3A-P0-004 前端外壳不裁决规则 | 已关闭 3A 外壳：只消费 snapshot/prompt、只提交服务端候选；未冻结 complex prompt safe fallback | C / D | 正式复杂交互等待服务端 runtime 冻结 |

3A 未进入且当前仍未关闭：最终正式 18 步 E2E、1009 张卡 full-official 覆盖、完整 battle runtime、完整 `ASSIGN_COMBAT_DAMAGE` runtime、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、完整 battlefield / standby / control / held / conquer lifecycle、完整 PaymentEngine / LayerEngine。3D 已关闭的是 `ORDER_TRIGGERS` 最小 runtime / UI / evidence 子项，不能外推为完整触发系统。

## 2026-05-09 阶段 3 D 对战桌面 / 核心 1v1 流程审计

阶段 3 审计入口：`docs/CURRENT_STAGE3_CORE_FLOW_AUDIT.md`；当前 3C 证据入口：`docs/CURRENT_STAGE3C_SPELL_DUEL_BATTLE_DAMAGE_EVIDENCE.md`。本节只同步服务端审计口径：宽阶段 3 未来会围绕创建 / 加入、卡组、准备、开局、起手、第一回合、召符文、打牌、移动、争夺或结算链或法术对决、结束回合、投降做 Chrome smoke 和后端验证；当前阶段 3C 只执行 spell duel / battle / `ASSIGN_COMBAT_DAMAGE` / battle cleanup 的最小官方化切片，项目仍 **NOT READY**。

阶段 3 当前 P0/P1 分类：

| 分类 | 当前阻断 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| 阻断 smoke 的 P0 | 3A 基础 Chrome route smoke 已关闭；room -> match 双浏览器连续链路、起手与隐藏信息浏览器断言、第一回合、打牌、移动、争夺/stack、结束回合、投降/结果仍未在同一阶段 3 smoke 闭环 | 后端与 UI 有分散代表测试；3B 已有后台 headless Chrome/CDP battlefield contest smoke 记录，但不是最终 18 步 E2E | C 主实现 smoke；B 维护服务端；D 审计；A 验收 | C 继续补双浏览器连续 smoke，B 保持服务端权威，D 记录规则依据、测试证据、剩余缺口 |
| 可在阶段 3 内继续的 P0 | spell duel / battle lifecycle、`ASSIGN_COMBAT_DAMAGE` runtime、battle cleanup、完整 PaymentEngine、`ORDER_TRIGGERS` 完整 trigger engine / APNAP / battle initial stack / trigger payment、battlefield / standby / control / held / conquer lifecycle、central cleanup queue | 阶段 2 已有 schema skeleton 与代表路径；3A 已补 `PAY_COST` 最小 runtime；3B 已建立 battlefield/cleanup 最小切片；3C 聚焦 spell duel/battle/damage/cleanup；3D 已补 `ORDER_TRIGGERS` 最小 runtime / UI / evidence | B 主实现；E 补 fixture；C 等正式 schema；D 审计 | 每关闭一个阻断，必须补规则依据、实现状态、测试证据、仍缺口，再由 D/A 复核 |
| 暂不阻断阶段 3 初始 smoke 但阻断 READY | 1009 张卡 full-official 覆盖矩阵、LayerEngine、全路径 replay / recovery / determinism、产品级视觉 polish | E 已建矩阵 skeleton 和风险 Top20；representative verifier / recovery smoke 有；完整覆盖未完成 | E/B/C；D 审计 | 阶段 3 smoke 可用代表卡组，但最终 READY 必须回到全量矩阵、层系统和 determinism 审计 |

阶段 2 已替代但阶段 3 仍未关闭的口径：

- `PAY_COST` / `ASSIGN_COMBAT_DAMAGE` / `ORDER_TRIGGERS` 已有 command/schema skeleton 和 `INVALID_PAYLOAD`；`PAY_COST` 已有 3A 最小 runtime，`ASSIGN_COMBAT_DAMAGE` 已有 3C 最小 runtime，`ORDER_TRIGGERS` 已有 3D 最小 runtime / UI / evidence。完整 PaymentEngine、`ASSIGN_COMBAT_DAMAGE` full-rule matrix、`ORDER_TRIGGERS` 完整 trigger engine / effect resolution / APNAP / battle initial stack / trigger payment 仍是 P0。
- 复杂 prompt 降级展示只能保证安全承接未知窗口，不能替代正式产品交互。
- 0/负战力、具体战场 objectId 大小写已进入防回归，不再列为当前 P0。
- 代表 battlefield contest / stack / spell duel / battle smoke 不能替代完整官方 lifecycle，也不能支撑 READY。

## 2026-05-09 阶段 2 D P0 证据链复审

阶段 2 D 证据链详见 `docs/CURRENT_STAGE2_P0_CONTRACT_PLAN.md`。本节只记录服务端自查的同步口径：阶段 1 协议壳、`PromptView` 最小入口、复杂 prompt 安全降级展示、`promptId/snapshotTick` 过期保护、0/负战力修复、具体战场 objectId 大小写修复、representative replay/final hash verifier 都不能误判为 READY；它们只是把旧 P0 的部分风险降为防回归或口径风险。

阶段 2 B 补同步：服务端已新增 `ErrorCodes.InvalidPayload`、`CommandTypes.PayCost/AssignCombatDamage/OrderTriggers`、`PayCostCommand`、`CombatDamageAssignmentDto`、`AssignCombatDamageCommand`、`OrderTriggersCommand`、`ActionPromptContractDto` / `ActionPromptContracts`。这只关闭“无正式 schema / 无稳定 malformed payload 拒绝语义”的 P0 子项；合法形状且进入未实现执行点的 command 仍以 `UNSUPPORTED_COMMAND` 拒绝，真实 `PAY_COST` runtime、damage assignment 状态机、`ORDER_TRIGGERS` 状态机仍未关闭。

阶段 2 剩余 P0 证据链：

| P0 | 规则依据 | 当前实现状态 | 归属 agent | 下一步 |
| --- | --- | --- | --- | --- |
| P0-S2-001 battlefield / standby / control / held / conquer lifecycle | `CORE-260330` rules 107.2-107.3, 187-189, 315.2.b.2, 319-323, 344-348, 461-464；`JFAQ-251023` q4.1-5.4；`SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p4-p5 | 已进入 3B 最小切片：`ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks`、具体战场移动、非法待命 cleanup、代表 control/held/conquer 与部分 smoke 已有；完整 control freeze/release、standby 全时机、全战场卡、battlefield trigger lifecycle 仍缺 | B 主实现；E 补证据/fixture；C 只读展示；D 维护证据链 | 3B 先关闭代表 snapshot/control/held/conquer 子项；完整 lifecycle 保持 P0 |
| P0-S2-002 cleanup queue | `CORE-260330` rules 319-324；`JFAQ-251023` q5.1-q5.2；`SOUL-OFAQ-260114` p19-p20 | 已进入 3B 最小切片：`PendingTaskQueue`、`PendingCleanupTasks`、`RunStateBasedCleanupLoop`、blocking guard、非法待命/致命伤害/未贴附装备代表任务已有；全触发面 cleanup queue 仍缺 | B 主实现；E 场景证据；C 只读展示；D 文档 | 3B 先关闭最小 task view 与代表 cleanup；所有 command/stack/trigger/move/enter/leave/damage/power change 统一 enqueue 继续 P0 |
| P0-S2-003 spell duel / battle lifecycle | `CORE-260330` rules 307-313, 333-348, 454-461；`JFAQ-251023` q2.3-q2.4, q3.1-q3.3 | `TurnWindowState`、`SpellDuelState`、`BattleState`、关联 id 和焦点恢复已有；`DECLARE_BATTLE` 仍是同步代表路径，不是官方多阶段 task | B 主实现；E 初始链/焦点/触发 fixture；C 等 typed prompt；D 文档 | 由 cleanup queue 创建并推进 `START_SPELL_DUEL` / `START_BATTLE`，拆出 focus/pass/swift/reaction/close/result |
| P0-S2-004 damage assignment | `CORE-260330` rules 142-143, 417, 460；`JFAQ-251023` q6.1-q6.4；`SOUL-OFAQ-260114` p19-p20 | 3C 已补最小 `ASSIGN_COMBAT_DAMAGE` runtime prompt、damagePool/legalTargets/lethal threshold、submit/reject、stale prompt 与 simultaneous damage commit；完整全规则矩阵仍缺 | B 主实现；E 多单位/壁垒/后排/负战力 fixture；C 仅同步类型/调试展示；D 文档 | 后续扩展壁垒/后排/同优先级/负战力/不可分配全矩阵和完整 battle task |
| P0-S2-005 `PAY_COST` / payment windows | `CORE-260330` rules 131, 135.2.e, 162-167, 356-357, 377, 403-405, 414, 416；`JFAQ-251023` q2.5；`SOUL-OFAQ-260114` p1-p4, p19-p21 | `PaymentCostRules`、typed `RunePool`、代表性 `COST_PAID` 包络和部分支付资源动作已有；`PAY_COST` command/schema skeleton 与 `INVALID_PAYLOAD` 已补；阶段 3A 已补最小 pending payment prompt/submit；阶段 4C-4 已补 `SFD·220/221` `TRIGGER_PAYMENT` 支付 / 拒付 / 支付失败 no-mutation 代表路径；完整 PaymentEngine、替代/额外费用、非出牌支付窗口、Quote/Authorize/Commit 仍缺 | B 主实现；E 支付/拒付/替代费用 fixture；C 仅同步类型/调试展示；D 文档 | 建立完整 `PaymentPlan/paymentPlanId/paymentWindow`，继续覆盖其他触发技能费用拒付和非出牌支付资源动作 |
| P0-S2-006 `ORDER_TRIGGERS` / trigger payment | `CORE-260330` rules 318-324, 333-340, 377, 383.3.d-383.3.e, 403-405；`JFAQ-251023` q2.2-q2.3, q2.5 | 3D 已补最小 runtime / UI / evidence；4C-1 已补保守 APNAP controller-block 子集、battle initial stack 代表路径和 face-down standby source 脱敏；4C-2 已补 Watchful Sentinel 多触发真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> draw 代表路径；4C-3 已补 Honest Broker 遗言金币真实 `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `EQUIPMENT_TOKEN_CREATED` 代表路径，以及非法排序 no mutation 复核；4C-4 已补 `SFD·220/221` trigger payment / decline / payment failure no-mutation 代表路径；4C-5 已补 state-based cleanup `LETHAL_DAMAGE` -> visible Watchful last-breath enqueue 代表路径；4C-6 已补 state-based cleanup `LETHAL_DAMAGE` -> visible Honest Broker last-breath enqueue 代表路径；4C-7 已补 Scouting Warhawk explicit destroy -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `RUNES_CALLED` 代表路径；4C-8 已补 Scouting Warhawk state-based cleanup `LETHAL_DAMAGE` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `RUNES_CALLED` 代表路径；4C-9 已补 Sad / Loyal Poro state-based cleanup 条件抽牌 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` 代表路径；4C-10 已补 Unsung Hero state-based cleanup powerful draw-2 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` x2 代表路径，并确认 power < 5 与 hidden / face-down / standby Unsung cleanup 不入队；4C-11 已补 Ghostly Centaur state-based cleanup friendly-destroyed power +2 -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `POWER_MODIFIED_UNTIL_END_OF_TURN` 代表路径，并确认 hidden / face-down / standby / opponent source 不入队、source 在本轮 cleanup removal set 中保守不入队、同一 source 同一 cleanup pass 最多入队一次；4C-12 已补 Resonant Soul state-based cleanup first-friendly-destroyed draw -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority -> `CARD_DRAWN` 1 代表路径，并确认 hidden / face-down / standby / opponent source 不入队、source 在本轮 cleanup removal set 中保守不入队、每 owner 每 cleanup pass 只按首次 destroyed unit 生成本批 source set、同回合已记录 destroyed owner 时不入队；4C-13 已把 Ghostly Centaur 与 Resonant Soul 的 true stack destruction 旧 immediate compatibility 迁移为 real trigger queue / stack / priority 语义，覆盖非 cleanup `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` 或 single-trigger auto-stack -> `StackItems` -> priority -> Ghostly `POWER_MODIFIED_UNTIL_END_OF_TURN` +2 / Resonant `CARD_DRAWN` 1，并保持 cleanup path 通过 `IsStateBasedCleanupDestroyedEvent` 排除旧 helper 防重复；4C-14 已补 Savage Jawfish `UNL-129/219` / `FU-bd94334cc5` trigger enqueue baseline：true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均进入 `TriggerQueue`，多触发走 `ORDER_TRIGGERS`、单触发 auto-stack，priority 双方 pass 后 `TRIGGER_RESOLVED` -> `EXPERIENCE_GAINED` +1，并确认 source 必须仍在场 / face-up / non-standby / 同 controller / 非 destroyed object / 非 cleanup removal set；4C-15B 已补 Viktor `FU-b5cb36a5c9` destroyed non-minion trigger 代表性 baseline：true stack `UNIT_DESTROYED` 与 Starfall lethal state-based cleanup `UNIT_DESTROYED` 均可让 visible surviving friendly Viktor source 在另一名友方非随从单位被摧毁时进入 `TriggerQueue`，使用 pre-removal state 过滤 minion target，single-trigger auto-stack 后 priority pass 创建 1-power Zaun minion `OGN·273/298`；4C-16 已补 Mechanical Trickster `OGN·239/298` last-breath create minions 迁移：true stack `UNIT_DESTROYED` -> `TRIGGER_QUEUED`，单触发 auto-stack、多触发 `ORDER_TRIGGERS` -> `StackItems`，priority pass 后 `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` x3，并确认 face-down / standby source 不入队、不泄漏、不造 token；完整 trigger engine、其他 destroyed / last-breath / friendly-destroyed FUs、Kogmaw / Karthus / Undercover Agent、Viktor full official trigger-count matrix、Savage Jawfish full official trigger-count matrix、Ironclad Vanguard state-based cleanup route、完整“每回合首次”时序、完整同时死亡触发次数、effective power / LayerEngine、temporary modifier、battlefield objectLocation matrix、hidden / face-down 原始触发建模、更多 trigger payment、完整 effect resolution、FAQ regression、1009/811 full-official 仍缺 | B 主实现；E 触发族 / FAQ fixture；C 只提交服务端 prompt；D 文档 | 以 Watchful Sentinel + Honest Broker 两条 last-breath real enqueue、Treasure Pile 触发支付、visible Watchful / Honest cleanup enqueue、Warhawk explicit / cleanup enqueue、Sad / Loyal Poro cleanup enqueue、Unsung Hero cleanup enqueue、Ghostly Centaur cleanup / stack enqueue、Resonant Soul cleanup / stack enqueue 、Savage Jawfish stack / cleanup enqueue 与 Mechanical Trickster stack enqueue 代表路径为基线，继续扩其他 destroyed-family / friendly-destroyed FUs、full official trigger-count matrix、“每回合首次”时序、同时死亡触发次数、experience rules、effective power / LayerEngine、temporary modifier、battlefield objectLocation 条件矩阵、hidden / face-down trigger policy、更多触发费用拒付、effect resolution 和 FAQ regression |

4C-15 / 4C-15A / 4C-15B 补充：Viktor `FU-b5cb36a5c9` destroyed non-minion token trigger 已由 B 先做只读可行性检查，4C-15 结论是不应硬编码；4C-15A 已补 `TOKEN_FAMILY:MINION` / `CardObjectTags.MinionTokenFamily`、官方三种“随从”token factory marker、`CreateBaseUnitTokens(tokenName == "随从")` 自动 marker、Viktor legend 创建随从同步 marker，以及 hidden face-down standby 不泄漏 marker 的验证；4C-15B 已在此前置模型上补 Viktor 代表性 trigger baseline，并使用 pre-removal `CardObjectState` 完成非随从 destroyed target 过滤。仍未关闭：same source same stack / cleanup pass multiple non-minion friendly deaths full official trigger-count matrix、Kogmaw / Karthus / Undercover Agent、完整 trigger engine、FAQ regression、1009/811 full-official。

4C-18 补充：B/A 已验证 Mechanical Trickster + Ironclad Vanguard state-based cleanup last-breath trigger enqueue 代表路径。Mechanical Trickster cleanup route 与 Ironclad Vanguard cleanup route 已记录为 4C-18 verified baseline；focused tests、backend full、frontend build、Chrome smoke 与 `git diff --check` 均通过。

4C-19 补充：B/A 已验证 Kogmaw / 克格莫 `OGN·190/298` / `FU-af8b05c294` last-breath AoE damage 代表切片。visible face-up field Kogmaw source 被摧毁时，经 `UNIT_DESTROYED` -> `TriggerQueue` -> auto-stack 或 `ORDER_TRIGGERS` -> `StackItems` -> priority -> `TRIGGER_RESOLVED`，按 source pre-removal battlefield location 对该 battlefield 当前单位各造成 4 点伤害，并让后续 cleanup queue 稳定收口；hidden / face-down / standby source 与缺少 battlefield location 的 source 不入队、不泄漏、不造成伤害。Kogmaw 只关闭 representative baseline；FAQ adjudication、full AoE damage matrix 与 full-official 仍未关闭。

4C-20B 补充：B/A 已验证 Undercover Agent / 卧底特工 `OGN·178/298` / `FU-6a52b04cb2` triggered hand-choice server prompt 微切片。服务端在 Undercover 绝念触发结算时打开 `HAND_CHOICE` / `CHOOSE_HAND_CARDS`，只向选择玩家暴露 `handChoices` 候选；wrong player、stale、invalid、malformed / illegal payload 均 no mutation 拒绝；`CORE-260330` p62 / rule `422.4` 已关闭 1 / 0 手牌 shortfall 裁决：弃尽可弃数量后仍抽两张。C 已完成前端展示 / 提交接线，前端只提交服务端候选，不结算弃牌或抽牌。该批不关闭非 Undercover 的通用 discard / hand-choice engine，不关闭 Karthus 额外绝念，不关闭 full-official。

阶段 2 superseded 口径：

- 0/负战力与具体战场大小写：已由阶段 1 修复和 A 验收替代，保留为防回归，不再列为未清零 P0。
- replay/final hash：历史“仍缺严格 action-log replay final-state 校验”已被当前 P1-004 状态替代；当前代表性 verifier、恢复前审计和 Postgres smoke 已有，剩余是全命令/全恢复/全随机 property 覆盖不足。
- 复杂 prompt：历史“完全没有复杂 prompt 入口”已被阶段 1 `PromptView`/降级展示替代；历史“`PAY_COST`、`ASSIGN_COMBAT_DAMAGE`、`ORDER_TRIGGERS` 完全没有 command/schema 或 malformed payload 拒绝语义”已被阶段 2 B 契约骨架替代；阶段 3A 已补 `PAY_COST` 最小 runtime，阶段 3C 已补 `ASSIGN_COMBAT_DAMAGE` 最小 runtime，阶段 3D / 4C-1 / 4C-2 / 4C-3 已把 `ORDER_TRIGGERS` 推进到最小 window、保守 APNAP controller-block 子集、Watchful Sentinel 与 Honest Broker 两条 last-breath real enqueue 代表路径；阶段 4C-4 已补 `SFD·220/221` trigger payment / decline 代表路径；阶段 4C-5 / 4C-6 已补 state-based cleanup -> visible Watchful / Honest Broker last-breath enqueue 代表路径；阶段 4C-7 已补 Scouting Warhawk explicit destroy real enqueue 代表路径；阶段 4C-8 已补 Scouting Warhawk state-based cleanup lethal damage real enqueue 代表路径；阶段 4C-9 已补 Sad / Loyal Poro state-based cleanup 条件抽牌 real enqueue 代表路径；阶段 4C-10 已补 Unsung Hero state-based cleanup powerful draw-2 real enqueue 代表路径；阶段 4C-11 已补 Ghostly Centaur state-based cleanup friendly-destroyed power +2 real enqueue 代表路径；阶段 4C-12 已补 Resonant Soul state-based cleanup first-friendly-destroyed draw real enqueue 代表路径；阶段 4C-13 已迁移 Ghostly Centaur / Resonant Soul true stack destruction 旧 immediate compatibility 到 real trigger queue / stack / priority 语义；阶段 4C-14 已补 Savage Jawfish stack / cleanup trigger enqueue baseline；阶段 4C-15 记录 Viktor non-minion token trigger blocker；阶段 4C-15A 已补 `TOKEN_FAMILY:MINION` 前置模型切片；阶段 4C-15B 已补 Viktor destroyed non-minion trigger 代表性 baseline；阶段 4C-16 已补 Mechanical Trickster stack trigger migration；阶段 4C-17 已关闭 Ironclad Vanguard true stack migration 代表路径。完整 PaymentEngine、`ASSIGN_COMBAT_DAMAGE` 全规则矩阵、完整 trigger engine / 其他 destroyed-family / friendly-destroyed FUs / Viktor full official trigger-count matrix / Savage Jawfish full official trigger-count matrix / Ironclad Vanguard state-based cleanup route / 完整“每回合首次”时序 / 完整同时死亡触发次数 / effective power 或 LayerEngine / temporary modifier / battlefield objectLocation matrix / hidden 或 face-down 原始触发建模 / effect resolution / 更多 trigger payment / FAQ regression 仍是 P0。
- 4C-18 verified 口径：Mechanical Trickster + Ironclad Vanguard state-based cleanup route 已关闭代表路径；完整 trigger engine 与 full-official 仍按 P0/P1 缺口管理。

## 2026-05-09 开发进度更新

- E 证据审计第一轮 P0 补录：已把 `SOUL-OFAQ-260114` p19-p20 的 0/负战力 FAQ 语义落到 `docs/rules-evidence-index.md`。官方口径为：单位战力可以为 0 或负数；不因 `Power <= 0` 自动被摧毁；需要至少 1 点有效伤害后才会在清理中被摧毁；负战力在战斗伤害输出/分配计算中按 0 处理，但对象实际战力保留。同步记录 `dda6385` 基线/历史服务端冲突点：`MatchSession` / `CoreRuleEngine` 中曾有 `DESTROY_ZERO_POWER_UNIT`、`ZERO_POWER` 与 `IsZeroPowerCleanupCandidate(Power <= 0)` 风险，不能再作为官方已完成证据。另记录 `dda6385` 具体战场移动大小写风险：移动 destination 规范化曾对整个 `BATTLEFIELD:<objectId>` 使用 `ToUpperInvariant()`，而官方快照存在 `OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298`。结论保持 **NOT READY**。
- B 服务端 P0 第一轮修复已通过 A 主控验收：1) `Power <= 0 && Damage == 0` 的正面场上单位不再暴露 blocking cleanup task，也不会自动入墓；2) `Power <= 0 && Damage > 0` 走 `DESTROY_LETHAL_UNIT` / `LETHAL_DAMAGE` 清理；3) 负战力单位参与战斗时输出伤害按 0，战后对象实际 `Power` 保留负值；4) `BATTLEFIELD:<objectId>` 只规范化 `BATTLEFIELD` zone，冒号后 objectId/cardNo 大小写逐字保留；5) 已覆盖小写 `a` 战场移动。A 主控验证：聚焦测试 11/11 通过，`git diff --check` 通过，后端 full test 3304/3304 通过。

- P1-002/P1-004 第三百四十五批补充：补齐基地单位移动到“服务端确认的具体战场”路径，并同步修正 ActionPrompt 条件减费合法性。`MOVE_UNIT` 现在可接受 `BASE -> BATTLEFIELD:<battlefieldObjectId>`，校验目标战场对象必须是已知官方战场牌，移动后写入精确 `ObjectLocationState.BattlefieldObjectId`，并在移动到已有敌方单位的战场时触发战场争夺与法术对决。ActionPrompt 对基地单位移动不再只给粗粒度 `BATTLEFIELD`，而是公开当前可选的具体战场 destination；同时 `PLAY_CARD` prompt 的基础减费改为复用与 Core 结算一致的条件判断，避免像《诺克萨斯新兵》（OGN·012/298）这类“本回合已打出另一张牌才减费”的卡，在条件未满足且当前法力不足时被前端展示为可打出。
- 已补验证与后台浏览器 smoke：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；新增/相关 focused filter 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 129/129；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3299/3299；`source ../../scripts/dev-env.sh && npm run build` 通过。后台 headless Chrome/CDP smoke 使用房间 `smoke-battlefield-contest-1778307710672`：P1 先通过 SignalR 入座，P2 Web UI 设置 `serverUrl=http://127.0.0.1:5093` 与 `playerId=P2` 后连接，P1 seed `battlefield-contest-stack`，P2 页面点击“让过优先权”和“让过焦点”，P1 提交 `DECLARE_BATTLE`；UI 在结算后显示“战斗结束 / 战场控制结算 / 待命清理”，authoritative snapshot 复核 `controllerId=P2`、`standbyObjectIds=[]`、`P1-STANDBY-CONTEST-001` 已离开 battlefield 并进入 P1 graveyard。reload/reconnect 后页面恢复最终 snapshot（事件日志不重播历史事件），Hub error 0，API/Vite/SignalR/headless Chrome 测试资源已清理，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未完成最终 completion audit。

- P1-002/P1-004 第三百四十四批补充：把《海克斯科技护手》（UNL-188/219）的“装配 3A，此技能的法力费用减少你所选单位的战力”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在要求 `ASSEMBLE_3_ANY_POWER`，按服务端确认的装配目标当前战力把基础 3 点法力费用降到不低于 0，再支付 1 点任意符能并贴附到合法己方单位；法力或符能不足时拒绝且零副作用。ActionPrompt 同步公开 `manaCost`、`baseManaCost`、`manaCostByTargetObjectId` 与 `targetPowerManaReductionByTargetObjectId`，并过滤掉当前资源无法支付的装配目标；DevUi 装配面板只读取这些服务端字段，显示“法力费用 / 目标战力减免”，不从卡面文本自行裁决。
- 已补验证与后台浏览器 smoke：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；新增 focused filter 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 74/74；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 129/129；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3294/3294；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser/Chrome 专用可控后端本轮仍未作为工具暴露，改用后台 headless Chrome/CDP 房间 `smoke-hextech-gauntlet-1778304888190`：P1 Web UI 连接 `http://127.0.0.1:5093`，后台 P2 join 并 seed `assemble-dynamic-mana`；P1 打开《海克斯科技护手》详情看到“法力费用 1 / 目标战力减免 2”，且战力 1、当前法力不足的候选目标未暴露，点击“确认装配”后 authoritative snapshot 确认 `P1-EQUIPMENT-HEXTECH-GAUNTLET-ASSEMBLE.attachedToObjectId = P1-UNIT-HEXTECH-GAUNTLET-TARGET`，`COST_PAID` payload 为 `mana=1/baseManaCost=3/targetPowerManaReduction=2/power=1`；reload 后点击重连恢复最终 snapshot，Hub error 0，API/Vite/SignalR/headless Chrome 测试资源已清理，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为百炼即时装配、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百四十三批补充：把《临终仪式》（SFD·150/221）的“装配 — 支付紫色，从你的废牌堆回收两张卡牌”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在要求 `ASSEMBLE_PURPLE` 与正好两个 `RECYCLE_GRAVEYARD_CARD:<objectId>` 额外费用目标同时由命令携带，先支付 1 紫色符能，再把服务端确认属于当前玩家废牌堆的两张牌回收到主牌堆底，写 `CARDS_RECYCLED.reason=ADDITIONAL_COST` 与 `COST_PAID.recycledAdditionalCostTargetObjectIds`，最后把基地中未贴附《临终仪式》贴附到己方单位；缺少、重复或非法回收目标时拒绝且零副作用。ActionPrompt 同步公开 `additionalCostChoices` 与 `requiredAdditionalCostChoiceCount=2`，DevUi 装配面板支持多选“回收废牌堆”额外费用并只提交服务端候选。
- 已补验证与后台浏览器 smoke：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；新增 focused filter 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 72/72；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 128/128；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3291/3291；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use/Chrome 可控插件后端本轮不可用，改用后台 headless Chrome/CDP 房间 `smoke-last-rites-1778303377377`：P1 Web UI 连接 `http://127.0.0.1:5093`，后台 P2 join 并 seed `assemble-recycle-graveyard`；P1 打开《临终仪式》详情看到服务端“额外费用 / 回收废牌堆”两项候选，点击“确认装配”后 authoritative snapshot 确认 P1 废牌堆为 0、主牌堆计数为 2、`P1-EQUIPMENT-LAST-RITES-ASSEMBLE.attachedToObjectId = P1-UNIT-LAST-RITES-TARGET`；reload 后点击重连恢复最终 snapshot，Hub error 0，API/Vite/SignalR/headless Chrome 测试资源已清理，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为百炼即时装配、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百四十二批补充：把《破败王者之刃》（SFD·178/221）的“装配 — 支付黄色，摧毁一名友方单位”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在要求 `ASSEMBLE_YELLOW` 与 `DESTROY_FRIENDLY_UNIT:<objectId>` 同时由命令携带，先支付 1 黄色符能，再以额外费用原因摧毁服务端确认的友方单位，最后把基地中未贴附《破败王者之刃》贴附到另一个己方单位；缺少摧毁费用、摧毁目标与装配目标相同、费用不足或目标非法时拒绝且不改变 tick、事件、符能、基地/墓地对象、贴附关系或结算链。ActionPrompt 同步公开 `additionalCostChoices` 与 `requiredAdditionalCostChoiceCount=1`，DevUi 装配面板显示“额外费用”并只提交服务端给出的摧毁候选，不从卡面文本自行裁决。
- 已补验证与 Browser smoke：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；新增 focused filter 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 70/70；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 127/127；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3288/3288；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 房间 `smoke-assemble-blade-1778301752924`：P1 Web UI 连接 `http://127.0.0.1:5093`，后台 P2 join 并 seed `assemble-destroy-friendly-unit`；P1 打开《破败王者之刃》详情看到服务端 `装配装备`、黄色费用、两个合法装配目标与“额外费用 / 摧毁友方单位”候选，点击“确认装配”后事件日志显示“支付费用 / 单位摧毁 / 装备装配”。authoritative P1 snapshot 中 `P1-UNIT-BLADE-RUINED-KING-COST` 已离开基地并进入墓地且不再作为公开对象出现，`P1-EQUIPMENT-BLADE-RUINED-KING-ASSEMBLE.attachedToObjectId = P1-UNIT-BLADE-RUINED-KING-TARGET`，符能归零；reload/reconnect 后恢复最终 snapshot，前端 console error 0，API/Vite/SignalR/Browser 测试资源已清理，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为动态费用、百炼即时装配、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百四十一批补充：把《牧人的传家宝》（UNL-158/219）的“装配 — 消耗 1 经验”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《牧人的传家宝》的来源，必须支付 `SPEND_EXPERIENCE:1`、扣除 1 经验并选择服务端已知受控单位目标；经验不足时拒绝且不改变 tick、事件、经验、基地对象、贴附关系或结算链。ActionPrompt 同步只在经验足够时公开该来源、经验费用和合法目标，DevUi 装配面板显示服务端提供的经验费用，不自行裁决装配可行性。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；新增经验装配 focused filter 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 68/68；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3285/3285；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。新增 `assemble-experience` 开发种子与 Hub 级提交测试；本批未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为动态费用、对象牺牲/回收型装配、百炼即时装配、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百四十批补充：补复杂/延迟装配费用的“不公开 + 拒绝”护栏，以《牧人的传家宝》（UNL-158/219）为代表。该牌文本中的装配费用为消耗 1 经验，不属于当前已开放的静态指定颜色符能或任意符能 `ASSEMBLE_EQUIPMENT` profile；Core 即使在 P1 拥有 1 经验且命令携带 `SPEND_EXPERIENCE:1` 时也拒绝直接装配，保持 tick、事件、经验、基地对象、贴附关系和结算链不变。ActionPrompt 同步不公开该来源，不向前端提供未支持的经验装配候选，前端继续只展示服务端已给出的合法动作。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 68/68；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3284/3284；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批为测试/文档护栏，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整经验费用模型、动态费用、对象牺牲/回收型装配、百炼即时装配、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十九批补充：把《舒瑞娅的安魂曲》（SFD·192/221）的“装配 A”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile，继续复用任意符能费用模型。该牌“打出时让你的所有单位变为活跃状态”的入场路径已由既有 preflight 覆盖；本批只补基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《舒瑞娅的安魂曲》的来源支付 `ASSEMBLE_ANY_POWER` 与 1 点任意符能后贴附到服务端已知受控单位。`唯我` 构筑限制仍不在本批伪造。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 66/66；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3282/3282；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为 `唯我` 完整构筑约束、灵便反应自动贴附、未贴附瞬息清理、待命即时贴附、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十八批补充：把《灭世者的死亡之冠》（SFD·191/221）的“装配 A”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile，继续复用任意符能费用模型。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《灭世者的死亡之冠》的来源，必须支付 `ASSEMBLE_ANY_POWER` 与 1 点任意符能并选择服务端已知受控单位目标；ActionPrompt 只公开该 profile 的任意符能、合法目标和服务端候选支付资源。`唯我` 构筑限制仍不在本批伪造。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 65/65；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3281/3281；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为 `唯我` 完整构筑约束、灵便反应自动贴附、未贴附瞬息清理、待命即时贴附、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十七批补充：把《炉火斗篷》（SFD·190/221）的“装配 A”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile，复用第三百三十六批新增的任意符能费用模型。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《炉火斗篷》的来源，必须支付 `ASSEMBLE_ANY_POWER` 与 1 点任意符能并选择服务端已知受控单位目标；需要回收符文补足任意符能时，ActionPrompt 继续只公开服务端候选支付资源。`唯我` 构筑限制仍不在本批伪造。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 64/64；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3280/3280；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为 `唯我` 完整构筑约束、灵便反应自动贴附、未贴附瞬息清理、待命即时贴附、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十六批补充：把《旋转飞斧》（SFD·186/221）的“装配 A”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile，并把装配费用模型扩展为可表达“任意符能”最小路径。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《旋转飞斧》的来源，必须支付 `ASSEMBLE_ANY_POWER` 与 1 点任意符能并选择服务端已知受控单位目标；需要回收符文补足任意符能时，ActionPrompt 只公开服务端候选支付资源。灵便反应即时贴附和未贴附瞬息开始阶段摧毁仍不在本批伪造。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 63/63；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3279/3279；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、未贴附瞬息清理、放逐打出、复制技能文字、待命即时贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十五批补充：把《Z型驱动》（SFD·090/221）的“装配蓝色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《Z型驱动》的来源，必须支付 `ASSEMBLE_BLUE` 与 1 点蓝色符能并选择服务端已知受控单位目标；放逐打出能力属于独立能力分支，仍不在本批伪造。ActionPrompt 同步公开《Z型驱动》的 `ASSEMBLE_BLUE`、蓝色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 62/62；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3278/3278；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为放逐打出、复制技能文字、待命即时贴附、灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十四批补充：把《斯弗尔尚歌》（SFD·059/221）的“装配绿色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《斯弗尔尚歌》的来源，必须支付 `ASSEMBLE_GREEN` 与 1 点绿色符能并选择服务端已知受控单位目标；贴附期间复制宿主技能文字属于持续文本/层系统分支，仍不在本批伪造。ActionPrompt 同步公开《斯弗尔尚歌》的 `ASSEMBLE_GREEN`、绿色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 61/61；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3277/3277；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为复制技能文字、待命即时贴附、灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十三批补充：把《夜之锋刃》（SFD·139/221）的“装配紫色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《夜之锋刃》的来源，必须支付 `ASSEMBLE_PURPLE` 与 1 点紫色符能并选择服务端已知受控单位目标；待命正面朝下打出与即时贴附仍是独立高阶分支，不在本批伪造。ActionPrompt 同步公开《夜之锋刃》的 `ASSEMBLE_PURPLE`、紫色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 60/60；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3276/3276；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为待命即时贴附、灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十二批补充：把 promo 编号《碎骨棒》（SFD·118a/221·P）的“装配橙色”作为独立 profile 接入服务端权威 `ASSEMBLE_EQUIPMENT`。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为 promo 编号《碎骨棒》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；普通编号与 promo 编号各自保留官方 cardNo，不互相混用。ActionPrompt 同步公开 promo 编号《碎骨棒》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 59/59；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3275/3275；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十一批补充：把普通编号《碎骨棒》（SFD·118/221）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为普通编号《碎骨棒》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；promo 编号 `SFD·118a/221·P` 已在第三百三十二批独立补齐。ActionPrompt 同步公开普通编号《碎骨棒》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 58/58；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3274/3274；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百三十批补充：把《枯萎战斧》（UNL-019/219）的“装配红色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《枯萎战斧》的来源，必须支付 `ASSEMBLE_RED` 与 1 点红色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《枯萎战斧》的 `ASSEMBLE_RED`、红色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 57/57；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3273/3273；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十九批补充：把《猎人的宽刃刀》（UNL-096/219）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《猎人的宽刃刀》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《猎人的宽刃刀》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 56/56；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3272/3272；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十八批补充：把《阿瑞昂的陨落》（SFD·030/221）的“装配红色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《阿瑞昂的陨落》的来源，必须支付 `ASSEMBLE_RED` 与 1 点红色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《阿瑞昂的陨落》的 `ASSEMBLE_RED`、红色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 55/55；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3271/3271；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十七批补充：把《云游图鉴》（SFD·086/221）的“装配蓝色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《云游图鉴》的来源，必须支付 `ASSEMBLE_BLUE` 与 1 点蓝色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《云游图鉴》的 `ASSEMBLE_BLUE`、蓝色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 54/54；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3270/3270；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十六批补充：把《暴风大剑》（SFD·161/221）的“装配黄色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《暴风大剑》的来源，必须支付 `ASSEMBLE_YELLOW` 与 1 点黄色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《暴风大剑》的 `ASSEMBLE_YELLOW`、黄色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 53/53；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3269/3269；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十五批补充：把《神圣剪刀》（SFD·172/221）的“装配黄色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《神圣剪刀》的来源，必须支付 `ASSEMBLE_YELLOW` 与 1 点黄色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《神圣剪刀》的 `ASSEMBLE_YELLOW`、黄色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 52/52；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3268/3268；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十四批补充：把《萃取》（SFD·134/221）的“装配紫色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《萃取》的来源，必须支付 `ASSEMBLE_PURPLE` 与 1 点紫色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《萃取》的 `ASSEMBLE_PURPLE`、紫色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 51/51；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3267/3267；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十三批补充：把《轻灵之靴》（SFD·133/221）的“装配紫色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《轻灵之靴》的来源，必须支付 `ASSEMBLE_PURPLE` 与 1 点紫色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《轻灵之靴》的 `ASSEMBLE_PURPLE`、紫色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 50/50；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3266/3266；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十二批补充：把《三相之力》（SFD·115/221）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《三相之力》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《三相之力》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 49/49；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3265/3265；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十一批补充：把《狂徒铠甲》（SFD·108/221）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《狂徒铠甲》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《狂徒铠甲》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 48/48；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3264/3264；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百二十批补充：把《海克斯饮魔刀》（SFD·102/221）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《海克斯饮魔刀》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《海克斯饮魔刀》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 47/47；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3263/3263；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十九批补充：把《灵魂之剑》（UNL-039/219）的“装配绿色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《灵魂之剑》的来源，必须支付 `ASSEMBLE_GREEN` 与 1 点绿色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《灵魂之剑》的 `ASSEMBLE_GREEN`、绿色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 46/46；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3262/3262；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十八批补充：把《守护天使》（SFD·051/221）的“装配绿色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《守护天使》的来源，必须支付 `ASSEMBLE_GREEN` 与 1 点绿色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《守护天使》的 `ASSEMBLE_GREEN`、绿色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 45/45；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3261/3261；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十七批补充：把《残暴之力》（SFD·042/221）的“装配绿色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《残暴之力》的来源，必须支付 `ASSEMBLE_GREEN` 与 1 点绿色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《残暴之力》的 `ASSEMBLE_GREEN`、绿色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 44/44；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3260/3260；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十六批补充：把《反曲之弓》（SFD·016/221）的“装配红色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《反曲之弓》的来源，必须支付 `ASSEMBLE_RED` 与 1 点红色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《反曲之弓》的 `ASSEMBLE_RED`、红色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 43/43；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3259/3259；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十五批补充：把《海克斯注力刚壁》（SFD·073/221）的“装配蓝色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《海克斯注力刚壁》的来源，必须支付 `ASSEMBLE_BLUE` 与 1 点蓝色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《海克斯注力刚壁》的 `ASSEMBLE_BLUE`、蓝色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 42/42；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3258/3258；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十四批补充：把《锯齿短匕》（SFD·009/221）的“装配红色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《锯齿短匕》的来源，必须支付 `ASSEMBLE_RED` 与 1 点红色符能并选择服务端已知受控单位目标；未知 cardNo、未登记 profile、错误颜色或未公开 optional cost 仍不会开放或通过。ActionPrompt 同步公开《锯齿短匕》的 `ASSEMBLE_RED`、红色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 41/41；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3257/3257；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十三批补充：把《先锋之眼》（SFD·153/221）的“装配黄色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《先锋之眼》的来源，必须支付 `ASSEMBLE_YELLOW` 与 1 点黄色符能并选择服务端已知受控单位目标；其他颜色或未公开 optional cost 仍不能替代。ActionPrompt 同步公开《先锋之眼》的 `ASSEMBLE_YELLOW`、黄色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 40/40；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3256/3256；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十二批补充：把《多兰之刃》（SFD·095/221）的“装配橙色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《多兰之刃》的来源，必须支付 `ASSEMBLE_ORANGE` 与 1 点橙色符能并选择服务端已知受控单位目标；其他颜色或未公开 optional cost 仍不能替代。ActionPrompt 同步公开《多兰之刃》的 `ASSEMBLE_ORANGE`、橙色符能要求和合法目标候选，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 39/39；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3255/3255；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十一批补充：把《多兰之戒》（SFD·124/221）的“装配紫色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 现在接受当前玩家基地中公开、受控、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 为《多兰之戒》的来源，必须支付 `ASSEMBLE_PURPLE` 与 1 点紫色符能并选择服务端已知受控单位目标；其他颜色或未公开 optional cost 仍不能替代。ActionPrompt 同步公开《多兰之戒》的 `ASSEMBLE_PURPLE`、紫色符能要求和合法目标候选，前端继续只展示并提交服务端候选。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 38/38；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3254/3254；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百一十批补充：修正 `ASSEMBLE_EQUIPMENT` 对“灵便/武装”标签的过窄前提，并补齐《多兰之盾》（SFD·033/221）“装配绿色”的服务端代表路径。装配来源现在仍必须是当前玩家控制、基地中公开、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 落在服务端已实现 profile 中的对象，但不再要求装备同时带 `武装` / `灵便` 标签；这让官方有“装配”但没有“灵便”的装备可以被服务端权威 prompt/命令覆盖，同时未知 cardNo 或未登记 profile 仍不会开放。ActionPrompt 同步公开《多兰之盾》的 `ASSEMBLE_GREEN`、绿色符能要求和合法单位目标，前端继续只按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 37/37；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3253/3253；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零九批补充：把《斯特拉克的挑战护手》（SFD·056/221）的“装配绿色”接入服务端权威 `ASSEMBLE_EQUIPMENT` profile。Core 将《斯特拉克》识别为 `ASSEMBLE_GREEN` / 绿色符能装配路径，要求来源是当前玩家控制、基地中公开、未贴附、带 `CARD_TYPE:EQUIPMENT` 且 cardNo 落在已实现 profile 中的服务端已知对象，目标必须是服务端已知的受控公开单位；蓝色/红色等错误符能不能支付绿色装配。ActionPrompt 同步公开绿色 optional cost、绿色支付资源候选和绿色符能贡献元数据，前端继续只展示和提交服务端候选。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 36/36；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3252/3252；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。本批仍无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零八批补充：把《布甲》（SFD·064/221）的“装配蓝色”接入既有服务端权威 `ASSEMBLE_EQUIPMENT` 代表路径。Core 现在按装备 cardNo 选择装配 profile：《长剑》仍要求 `ASSEMBLE_RED` 与红色符能，《布甲》要求 `ASSEMBLE_BLUE` 与蓝色符能；错误颜色、泛化符能或未公开 optional cost 不会被接受。ActionPrompt 同步只向前端公开服务端支持的《布甲》来源、目标、`ASSEMBLE_BLUE` optional cost、蓝色支付资源候选和蓝色符能贡献元数据，前端仍不根据卡面自行裁决装配颜色。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 34/34；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3250/3250；`source ../../scripts/dev-env.sh && npm run build` 通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为灵便反应自动贴附、更多装备装配 profile、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零七批补充：补齐《奥恩》（SFD·058/221 与 SFD·058a/221）“可以不选择装备”的服务端证据。既有 `MainDeckLookCount=4` / `MinTargetCount=0` 解析在 0 目标时不会写入 `CARD_DRAWN`，而是将查看到的四张全部按主牌堆底部回收流程处理并写入 `CARDS_RECYCLED.count=4`；Hub seed 也确认 `PLAY_CARD.sourceRequirements.minTargetCount=0`，前端可展示“不选择”入口但仍只提交服务端 prompt 支持的空目标命令。此批不新增前端运行时代码。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn"` 通过 11/11；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3248/3248。本批为测试/文档证据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零六批补充：补齐《奥恩》（SFD·058/221 与 SFD·058a/221）“打出时查看主牌堆顶部四张，可以展示并抽取一件装备，然后回收其余牌”的服务端代表路径。CardBehavior 现在为两版奥恩登记 `FriendlyMainDeckCard` 目标域、`MainDeckLookCount=4` 与 `MainDeckTargetRequiredTag=EquipmentCard`；Hub prompt 只公开服务端权威可见的顶部四张中装备候选，不公开非装备牌，也不让前端按卡面本地判断目标。结算时服务端先将源单位打出到基地，再把选中的装备从主牌堆移入手牌并写入 `CARD_DRAWN.count=1`，随后把未选的三张查看候选回收到主牌堆并写入 `CARDS_RECYCLED.count=3`。据守重复触发仍待更广证据覆盖。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ornn|FullyQualifiedName~p2-preflight-play-sfd-058|FullyQualifiedName~P4PlayCardRejectsSupportedUnitWithTargets"` 通过 8/8；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3245/3245；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 房间 `smoke-ornn-1778283334154` 覆盖 P1 前端连接、后台 P2 join 与 `ornn-equipment-look` seed、详情抽屉仅显示两个 `SFD·022/221` 装备目标候选、前端选择第一件装备并确认打出、P1 前端让过优先权、P2 后台让过后事件日志显示“单位进入基地 / 抽牌 / 回收卡牌”，authoritative P1 snapshot 显示 `P1-UNIT-SFD-058-ORNN` 在 P1 基地、`P1-ORNN-EQUIPMENT-001` 在 P1 手牌、`CARD_DRAWN.count=1`、`CARDS_RECYCLED.count=3` 且结算链为空；reload/reconnect 恢复最终 snapshot，Chrome runtime error 0，API/Vite/SignalR/Chrome 测试资源已清理，目标端口无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零五批补充：补齐《皇家随从》（SFD·039/221）“当你打出我时，让一名传奇变为活跃或休眠状态”的服务端代表路径。CardBehavior 现在为该牌登记 `READY_LEGEND` / `EXHAUST_LEGEND` 两个显式 mode 与 `LEGEND` 目标域；Core 允许合法传奇对象进入目标解析，结算时先由服务端权威将源单位打出到基地，再按 mode 写入 `UNIT_READIED` 或 `UNIT_EXHAUSTED` 并更新目标传奇状态。ActionPrompt 只在服务端确认存在可选传奇目标时公开这两个模式、目标槽和中文标签；前端仅展示并提交服务端候选，不根据卡面本地裁决传奇状态。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RoyalAttendant|FullyQualifiedName~p2-preflight-play-royal-attendant|FullyQualifiedName~P4PlayCardRejectsSupportedUnitWithTargets"` 通过 5/5；Royal / PoroHerder / MountainApe / Dunehorn / PlayCardPrompt 相关聚合过滤通过 14/14；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3242/3242；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 房间 `smoke-royal-attendant-1778282369491` 覆盖 P1 前端连接、后台 P2 join 与 `royal-attendant-legend-mode` seed、详情抽屉显示“活跃传奇 / 休眠传奇”和双方传奇目标、前端提交 READY_LEGEND、双方让过后皇家随从进入基地且 P1 传奇恢复活跃、reload/reconnect 恢复最终 snapshot；runtime error 0，API/Vite/SignalR/Chrome 测试资源已清理，目标端口无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零四批补充：补齐《穿沙角兽》（SFD·027/221）“当我据守一处战场时，抽两张牌”的服务端代表路径。Core 现在在 `DECLARE_BATTLE` 结算出防守方据守后，按幸存防守单位解析当前玩家控制的《穿沙角兽》，广播 `TRIGGER_RESOLVED.effectKind=DUNEHORN_BEAST_BATTLEFIELD_HELD_DRAW_2`，随后由 authoritative draw 路径移动主牌堆顶两张到手牌并写入 `CARD_DRAWN.count=2`；若《穿沙角兽》未在据守后的战场幸存或不受据守玩家控制，则不会触发。前端继续只展示服务端战场据守事件、触发事件和 snapshot，不在浏览器侧判断据守抽牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Dunehorn|FullyQualifiedName~BattlefieldHeldDraw|FullyQualifiedName~BattlefieldHeld"` 通过 37/37；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3238/3238；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端战场据守触发与事件证据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零三批补充：补齐《山猿老祖》（SFD·047/221）“当你给予我增益时，让我变为活跃状态”的服务端代表路径。Core 现在在权威 `ApplyBoon` 成功写入新 `增益` 标签与 +1 基础战力后，若受增益对象是当前玩家控制的《山猿老祖》，继续广播 `TRIGGER_RESOLVED.effectKind=MOUNTAIN_APE_ELDER_BOON_READY` 与 `UNIT_READIED.reason=MOUNTAIN_APE_ELDER_BOON_READY`，并将其 `isExhausted=false`；若对象已经拥有增益，服务端不重复授予增益，也不会错误触发活跃。前端继续只展示服务端事件与 authoritative snapshot，不在浏览器侧监听增益或自行改写休眠/活跃状态。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MountainApe|FullyQualifiedName~PoroHerder|FullyQualifiedName~BattlefieldPlayUnitBoon"` 通过 9/9；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3237/3237；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与事件证据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零二批补充：补齐《爆破队学员》（SFD·013/221）“可额外支付 1 与红色，若支付则对战场上一名单位造成 2 点伤害”的服务端代表路径。Core 复用目标效果可选额外费用模型，只有 `PLAY_CARD.optionalCosts=["SPEND_MANA:1","SPEND_POWER:red:1"]` 被服务端付费校验时才允许 1 个战场单位目标；未支付完整额外费用时仍要求 0 目标并只作为 2 战力单位入场。结算时源单位进入基地后，由服务端权威 `DAMAGE_APPLIED` 对目标战场单位造成 2 点伤害。ActionPrompt 仅在服务端确认基础 2 + 额外 1 法力、当前/可回收红色符能和合法战场单位目标均可用时公开这组可选费用，前端继续只展示并提交服务端候选。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BlastCrew|FullyQualifiedName~Frostcoat|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided"` 通过 79/79；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3235/3235；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 可选费用/目标元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百零一批补充：补齐《霜衣幼崽》（SFD·067/221）“可额外支付蓝色，若支付则让一名单位本回合战力 -2”的服务端代表路径。CardBehavior 增加目标效果可选有色符能字段，Core 只在 `PLAY_CARD.optionalCosts=["SPEND_POWER:blue:1"]` 被服务端付费校验时允许 1 个单位目标；未支付蓝色额外费用时仍要求 0 目标并只作为 3 战力、`犬形` 标签单位入场。结算时源单位先进入基地，再由服务端权威 `POWER_MODIFIED_UNTIL_END_OF_TURN` 将目标本回合战力 -2。ActionPrompt 仅在服务端确认当前/可回收蓝色符能足够且存在合法单位目标时公开“额外支付 1 蓝色符能：一名单位本回合战力 -2”，前端继续只展示并提交服务端候选。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Frostcoat|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided"` 通过 77/77；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3233/3233；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 可选费用/目标元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第三百批补充：补齐《派克》（UNL-028/219、UNL-028a/219）“可额外支付红色，若支付则自身变为活跃状态并本回合战力 +2”的服务端代表路径。CardBehavior 增加来源单位可选有色符能活跃/临时战力修正字段，Core 只在 `PLAY_CARD.optionalCosts=["SPEND_POWER:red:1"]` 被服务端付费校验并进入结算链后，于源单位入场后写入 `UNIT_READIED` 与 `POWER_MODIFIED_UNTIL_END_OF_TURN`；未支付红色额外费用时仍只是 2 战力、`待命` / `游走` 标签单位。ActionPrompt 仅在服务端确认当前/可回收红色符能足够时公开“额外支付 1 红色符能：活跃并本回合战力 +2”，前端继续只展示并提交服务端候选。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Pyke|FullyQualifiedName~TinyGuardian|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided"` 通过 81/81；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3231/3231；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 可选费用元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十九批补充：补齐《小小守护者》（OGN·044/298）“可额外支付绿色，若支付则抽一张牌”的服务端代表路径。CardBehavior 增加来源单位可选有色符能抽牌字段，Core 只在 `PLAY_CARD.optionalCosts=["SPEND_POWER:green:1"]` 被服务端付费校验并进入结算链后，于源单位入场后执行权威抽牌；未支付绿色额外费用时仍只是 2 战力 `CARD_TYPE:UNIT`。ActionPrompt 仅在服务端确认当前/可回收绿色符能足够时公开“额外支付 1 绿色符能：抽 1 张牌”，前端继续只展示并提交服务端候选，不在浏览器侧推断有色额外费用或抽牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TinyGuardian|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost|FullyQualifiedName~CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided"` 通过 77/77；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3228/3228；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 可选费用元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十八批补充：补齐《船猿》（SFD·098/221）“可额外支付 1，若支付则给予自身增益”的服务端代表路径。CardBehavior 增加 `SourceBoonAdditionalManaCost=1`，Core 只在 `PLAY_CARD.optionalCosts=["SPEND_MANA:1"]` 进入结算链后授予来源单位 `增益` 与 +1 战力；未支付额外费用时仍只是 2 战力、`海盗` 标签单位。ActionPrompt 仅在服务端确认当前法力足够支付基础 2 + 额外 1 时公开“额外支付 1 法力：给予我增益”，前端仍只展示并提交服务端候选，不在浏览器侧推断额外费用或增益。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ShipMonkey|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost"` 通过 39/39；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3226/3226；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 可选费用元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十七批补充：补齐《芭茹队长》（SFD·091/221）“抽一张牌或给予我增益”的第二个服务端模式。CardBehavior 现在为同一官方牌同时登记 `DRAW_1` 与 `SELF_BOON` 两个显式模式；`SELF_BOON` 结算后由服务端先将源单位打出到基地，再复用权威 `ApplyBoon` 写入 `OBJECT_TAG_ADDED` 与 `BOON_GRANTED`，使《芭茹队长》从 3 战力变为 4 战力并获得 `增益`。ActionPrompt 同步展示“抽 1 张 / 给予我增益”两个中文模式，前端仍只提交服务端公开的 mode，不读取卡面自行裁决。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BuhruCaptain"` 通过 4/4，覆盖抽牌模式、自身增益模式、缺失模式拒绝和 prompt 模式公开；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3224/3224；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则与 ActionPrompt 元数据补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十六批补充：补齐《自适应机器人》（OGN·056/298）“征服效果摧毁一件装备并给予自身增益”的服务端代表路径证据。Core 既有 `BATTLEFIELD_HELD_ACTIVATE_UNIT_CONQUEST_EFFECTS` 路径现在由直接引擎测试锁定：当《清算人竞技场》据守触发激活单位征服效果时，受控正面《自适应机器人》若存在场上装备，会广播 `UNIT_CONQUEST_EFFECT_ACTIVATED.effectId=UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON`，由服务端摧毁装备并将其移入拥有者废牌堆，再给予自身 `增益` 与 +1 战力；无装备时不授予增益。前端仍只展示服务端事件和 snapshot，不在浏览器侧选择或摧毁装备。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldActivateConquestEffectsAdaptiveRobot"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldActivateConquest"` 相邻聚合通过 6/6。本批为服务端测试与证据收口，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十五批补充：补齐《星蚀先锋》（OGN·059/298）“每当你眩晕一名敌方单位时，让我变为活跃状态，且本回合 +1”的服务端代表路径。Core 现在复用服务端 `STATUS_EFFECT_APPLIED.effectId=STUNNED` 事件与 authoritative 场上对象判断，只在控制者真实眩晕敌方正面单位时解析其受控正面《星蚀先锋》，广播 `TRIGGER_RESOLVED.effectKind=ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1`，随后写入 `UNIT_READIED` 与 `POWER_MODIFIED_UNTIL_END_OF_TURN`；眩晕己方单位不会触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧监听眩晕或自行修正活跃/战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79EclipseVanguard"` 通过 2/2；《星蚀先锋》/《烈阳盾卫》/蕾欧娜眩晕相邻回归 7/7 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3220/3220；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十四批补充：补齐《魄罗牧者》（OGN·061/298）“打出时若你场上拥有魄罗属性单位，则给予自身增益并抽一张牌”的服务端代表路径。Core 在源单位结算入场后按 authoritative 场上对象查找当前控制者的正面魄罗单位，满足条件时广播 `TRIGGER_RESOLVED.effectKind=PORO_HERDER_BOON_DRAW`，复用服务端 `ApplyBoon` 为《魄罗牧者》增加 `增益` 标签与 +1 战力，并由权威抽牌路径移动主牌堆顶牌到手牌。前端仍只展示服务端事件和 snapshot，不在浏览器侧判断是否控制魄罗或自行抽牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79PoroHerder"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3218/3218；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十三批补充：补齐《狂暴龙怪》（OGN·031/298）“打出时本回合内下一张法术费用减少 5”的服务端代表路径。Core 在源单位结算入场后写入 `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>` 回合持续效果；下一张真实法术 `PLAY_CARD` 由服务端计算并消耗该标记，`COST_PAID.nextSpellCostReductionMana` 记录实际减免，随后广播 `TRIGGER_RESOLVED`。ActionPrompt/Hubs 同步暴露 `PLAY_CARD.sourceRequirements.minimumManaCost=0` 与 `nextSpellCostReductionMana=2`（以 2 费《训练有素》为代表），DevUi 详情抽屉只展示服务端元数据“下一法术减费”，不读取卡面自行裁决费用。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79RagingDrake"` 通过 4/4；Raging Drake 相关 keyword-only 聚合回归通过 236/236；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3217/3217；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-raging-drake-1778275056356`：P1 页面连接 `http://127.0.0.1:5093`，后台 P2 入座并 seed `raging-drake-next-spell-cost-reduction-prompt`；P1 打开《训练有素》详情看到“费用 0 / 下一法术减费 -2”，通过前端选择 P2《均衡门徒》并确认打出，服务端事件显示支付 0、狂暴龙怪减费触发、法术入栈；P1 前端让过、P2 后台让过后 authoritative snapshot 显示减费标记已消耗、结算链为空、《训练有素》进入 P1 废牌堆、P1 抽牌、P2 单位有效战力 5；reload/reconnect 后最终 snapshot 恢复。Chrome 应用 runtime error 0；smoke 后已关闭测试标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十二批补充：补齐《粗鲁的海盗》（OGN·002/298）“打出时可额外弃置一张手牌，使费用减少 2”的服务端代表路径。CardBehavior 增加弃手牌可选费用减法力字段，Core 复用服务端 `DISCARD_HAND_CARD:<objectId>` 可选费用计划，校验目标必须是同玩家另一张手牌，支付事件记录 `optionalCostManaReduction=2`，并由权威弃牌路径广播 `CARD_DISCARDED`、写入废牌堆与 `DISCARDED_HAND_CARD_THIS_TURN:<playerId>` 回合记忆；弃置来源牌自身会被 `INVALID_TARGET` 拒绝。ActionPrompt 的 `PLAY_CARD.sourceRequirements.optionalCostChoices` 现在按具体来源牌枚举可弃置手牌，P1 只有 4 法力但有另一张手牌时仍会显示粗鲁的海盗可打出、`minimumManaCost=4`，前端继续只展示并提交服务端候选，不在浏览器侧自行构造弃牌减费。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79RudePirate"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3213/3213；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端可选费用/ActionPrompt 代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免新增前台窗口或常驻进程。整体仍 **NOT READY**，因为正式 18 步 E2E、完整费用模型、central cleanup queue、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十一批补充：补齐《驭水者》（OGN·055/298）“独自进攻或防守一处战场时战力 +2”的服务端代表路径。Core 现在在 authoritative 战斗伤害计算中按攻击方/防守方参与单位数量判断 OGN·055 是否独自进攻或防守，并把 +2 写入 `DAMAGE_APPLIED.staticPowerBonus`、`combatPower` 与最终伤害；共同进攻时不加成。前端仍只展示服务端战斗事件和 snapshot，不在浏览器侧统计战斗参与者或修正战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79Waterbender|FullyQualifiedName~P4WaterbenderTargetRejectedFixture|FullyQualifiedName~P79WiseElder|FullyQualifiedName~P79DuneDrake"` 通过 8/8；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3210/3210；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端战斗静态代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整条件式费用/静态族、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百九十批补充：补齐《睿智长者》（OGN·065/298）“拥有增益时额外 +1 战力”的服务端代表路径。Core 现在在 authoritative 战斗伤害计算中识别正面、单位、带 `增益` 标签的 OGN·065 来源，把 +1 计入 `DAMAGE_APPLIED.staticPowerBonus` 和 `combatPower`；未拥有增益时不加成。前端仍只展示服务端战斗事件和 snapshot，不根据标签在浏览器侧自行修正战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79WiseElder|FullyQualifiedName~P4WiseElderTargetRejectedFixture|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~P79BilgewaterBully"` 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3207/3207；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端战斗静态代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整条件式费用/静态族、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十九批补充：补齐《竞技场勤务小队》（OGN·091/298）“当你打出一件装备时，让我变为活跃状态”的服务端代表路径。Core 现在只在真实装备 `PLAY_CARD` 路径中解析当前玩家控制的正面 OGN·091 单位，打出《长剑》等服务端已登记装备时立即写入 `ARENA_SERVICE_CREW_EQUIPMENT_READY` 触发并把来源单位置为活跃；对手打出装备不会误触发，前端仍只展示服务端事件和 authoritative snapshot，不在浏览器侧监听装备或自行切换活跃状态。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；《竞技场勤务小队》装备打出活跃 paired fixture、P7.9 直接引擎正/反向、既有 OGN·091 带目标拒绝与《长剑》装备打出回归 5/5 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3205/3205；`source ../../scripts/dev-env.sh && npm run build` 通过。本批为服务端规则代表路径补齐，无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式费用/静态族、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十八批补充：补齐《踊跃的学徒》（OGN·084/298）“位于战场上时，你的法术的法力费用减少 1，且不能降到 1 以下”的服务端代表路径。Core 现在只在真实法术 `PLAY_CARD` 路径中按 authoritative 战场对象解析正面、受控的 OGN·084 单位，把该减费计入付费计划与 `COST_PAID.battlefieldSpellCostReductionMana`，并在已有减费后守住法术最低 1 费；对手控制、面朝下、非法术或 1 费以下路径不会误减。ActionPrompt 同步暴露 `minimumManaCost=1` 与 `battlefieldSpellCostReductionMana=1`，前端卡牌详情只展示服务端元数据“战场法术减费 -1”，不读取卡面自行裁决费用。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；《踊跃的学徒》战场法术减费、对手控制来源跳过、ActionPrompt 元数据、既有 OGN·084 拒绝与《训练有素》回归 6/6 通过；`GameHubJoinTests` 通过 121/121；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3202/3202；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-eager-apprentice-1778271465789`：P1 页面连接 `http://127.0.0.1:5093`，后台 P2 入座并 seed `battlefield-eager-apprentice-spell-cost-reduction`；P1 打开《训练有素》详情看到“费用 1 / 战场法术减费 -1”，通过前端选择 P2《均衡门徒》并确认打出，P1 页面点击“让过优先权”，后台 P2 让过后服务端结算为 P1 支付 1 点费用、法术进废牌堆、P2 单位有效战力 5、P1 抽牌；reload/reconnect 后最终 snapshot 恢复。过滤 Chrome 扩展噪声后应用 runtime error 0，smoke 后已 finalize 测试标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整条件式费用/静态族、正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十七批补充：补齐《拉文布鲁姆学生》（OGN·103/298）“每当你打出一张法术牌时，让我本回合内战力 +1”的服务端代表路径。Core 现在在真实法术 `PLAY_CARD` 付费入栈路径写入法术回合记忆后，按 authoritative 场上对象找到控制者场上的正面 OGN·103 单位，并由服务端触发 `RAVENBLOOM_STUDENT_SPELL_POWER_PLUS_1`，随后以 `POWER_MODIFIED_UNTIL_END_OF_TURN` 把自身从 2 战力临时修正到 3 战力；面朝下、非单位或非控制者对象不会作为触发来源。前端仍只展示服务端事件与 snapshot，不在浏览器侧监听法术或自行修正战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；《拉文布鲁姆学生》法术打出触发、《实战经验》基础战力修正与 OGN·103 带目标拒绝回归 3/3 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3198/3198；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十六批补充：补齐《菲奥娜》（OGN·232/298）“自身变为强力单位时获得法盾、游走和坚守”的服务端代表路径。Core 现在在 authoritative 增益/战力修正把 OGN 菲奥娜推到强力阈值 5 以上时，按服务端对象状态补齐 `法盾`、`游走` 与 `坚守` 标签；本批用《竞技场新人》的永久增益把 4 战力菲奥娜变为 5 战力，避免用临时修正提前引入回合结束关键词移除语义。前端仍只展示服务端事件与 snapshot，不在浏览器侧判断强力或授予关键词。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；《菲奥娜》强力增益授予关键词单点 1/1 通过；相关 boon / OGN Fiora 回归 6/6 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3197/3197；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十五批补充：补齐《新月禁卫》（UNL-122/219）“本回合打出过法术后可支付 1 点紫色符能，以活跃状态入场”的服务端代表路径。Core 现在在真实法术 `PLAY_CARD` 付费路径写入 `PLAYED_SPELL_THIS_TURN:<playerId>` 回合记忆；《新月禁卫》只在该 authoritative 记忆存在时接受服务端候选 `SPEND_POWER:purple:1`，按 typed purple 支付并在 `UNIT_PLAYED_TO_BASE` 事件中标记 `crescentGuardReadyOptionalCostPaid`。ActionPrompt 同步只在服务端记忆存在且当前/可回收紫色符能足够时暴露 optional cost 与 `paymentResourcePowerByChoice`，前端仍不读取卡面自行裁决。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；《新月禁卫》有/无法术记忆、紫色支付、ActionPrompt payment resource、相关法术回合记忆和 Hextech Ray end-turn 清理回归 9/9 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3196/3196；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十四批补充：补齐《均衡门徒》（UNL-097/219）“当你打出我时，如果你的其他单位总计战力不低于 5，则抽一张牌”的服务端代表路径。Core 现在在源单位结算入场后，按 authoritative 场上对象统计同控制者、排除来源自身的其他单位总战力；满足阈值时由服务端执行抽 1 张牌并更新权威牌堆/手牌。前端仍只展示服务端 `CARD_DRAWN` 事件和 authoritative snapshot，不在浏览器侧统计战力或抽牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw"` 通过 155/155；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3193/3193；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十三批补充：补齐《晋升信徒》（UNL-004/219）“若本回合曾消耗不低于 4 点费用打出法术，则我获得 +4 战力”的服务端代表路径。Core 现在在真实 `PLAY_CARD` 付费路径中，仅当来源是法术且本次实际支付 mana 费用不低于 4 时写入 `PLAYED_FOUR_PLUS_COST_SPELL_THIS_TURN:<playerId>` 回合记忆；《晋升信徒》结算为单位时只读取服务端回合记忆，条件满足才把战力从 1 修正到 5。前端仍只展示服务端 prompt、事件和 authoritative snapshot，不在浏览器侧推断法术费用记忆或战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysVanillaSourceUnit|FullyQualifiedName~CoreRuleEnginePlaysGrandStrategyAllFriendlyPowerBoost"` 通过 155/155；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3192/3192；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十二批补充：补齐《狡猾的蝾螈》（UNL-108/219）“如果你本回合内已经获得过经验，则我获得 +1 战力和游走”的服务端代表路径。Core 现在把 authoritative `EXPERIENCE_GAINED` 事件统一转写为 `GAINED_EXPERIENCE_THIS_TURN:<playerId>` 回合记忆，覆盖栈结算获得经验、战场授予经验技能和最小战斗狩猎征服/据守获得经验入口；《狡猾的蝾螈》结算为单位时只读取服务端回合记忆，条件满足才把战力从 4 修正到 5 并写入 `游走` 标签。前端仍只展示服务端 prompt、事件和 authoritative snapshot，不在浏览器侧推断经验记忆、战力或关键词。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~P4DeclareBattleHuntConquestExperience|FullyQualifiedName~CoreRuleEnginePlaysSourceUnitWithPlayExperience|FullyQualifiedName~DemaciaEnvoy"` 通过 232/232；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3191/3191；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十一批补充：补齐《肆虐狂魂》（OGN·019/298）“如果你本回合内已经弃置过手牌，则我获得强攻和游走”的服务端代表路径。Core 现在用 authoritative `UntilEndOfTurnEffects` 记录 `DISCARDED_HAND_CARD_THIS_TURN:<playerId>`，覆盖额外费用弃牌、手牌目标弃牌、任意玩家手牌弃牌、全手牌弃置、战场征服弃牌抽牌和乐芙兰战场结果弃牌费用等服务端真实弃牌入口；《肆虐狂魂》结算为单位时只读取该服务端回合记忆，条件满足才把 `强攻` 与 `游走` 写入单位对象标签。前端仍只展示服务端 prompt、事件和 authoritative snapshot，不在浏览器侧推断弃牌记忆或关键词。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysKeywordOnlySourceUnit|FullyQualifiedName~P79JinxDiscardTriggerReadiesAndGainsPowerOnceForDiscardBatch"` 通过 234/234；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Discard|FullyQualifiedName~RampagingSoul|FullyQualifiedName~BilgewaterBullyWithBoonCanUseRoam|FullyQualifiedName~PreciseRoam"` 通过 41/41；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3190/3190；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，避免占用前台或常驻资源。整体仍 **NOT READY**，因为完整条件式战力/关键词族、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百八十批补充：补齐《控潮者》（OGN·199/298）“打出时可以选择受你控制的一名单位，然后把我移动到其所在位置，再将其移动到我原来的位置”的服务端代表路径。`PLAY_CARD` 现在允许该源牌携带 0/1 个己方单位目标；带目标时服务端先将《控潮者》作为 2 战力、带 `待命` 标签的单位打出到基地，再通过 authoritative zone swap 将自身与目标单位交换位置，并同步 `ObjectLocations`。非己方单位目标仍由服务端拒绝。前端仍只展示服务端 prompt、`UNIT_PLAYED_TO_BASE` / `UNIT_LOCATIONS_SWAPPED` 事件和 authoritative snapshot，不在浏览器侧推断可交换目标或位置变更。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTidecallerOptionalLocationSwap|FullyQualifiedName~P4TideCallerTargetRejectedFixture|FullyQualifiedName~P2PreflightOfficialUnitStaticFixtures|FullyQualifiedName~Reflections|FullyQualifiedName~BaitMoveEnemyUnit"` 通过 5/5；修正 keyword-only 拒绝聚合后，`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysTidecallerOptionalLocationSwap|FullyQualifiedName~P4TideCallerTargetRejectedFixture|FullyQualifiedName~CoreRuleEngineRejectsKeywordOnlySourceUnitWhenTargetsAreProvided"` 通过 229/229；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3189/3189；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为完整待命/反应显露、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十九批补充：补齐《幽灵主母》（OGN·226/298）“打出时可以从己方废牌堆打出费用不高于 3 且不高于 A 的单位”的服务端代表路径。`PLAY_CARD` 现在允许该源牌携带 0/1 个己方废牌堆单位目标；带目标时服务端校验目标在控制者废牌堆、属于单位、费用不高于 3 且不高于当前 A，结算后《幽灵主母》自身进入基地并广播 `UNIT_PLAYED_TO_BASE`，所选废牌堆单位也由服务端打出到基地并重置伤害、休眠和回合临时效果。非废牌堆目标、费用过高或超过当前 A 的目标仍由服务端拒绝。前端仍只展示服务端 prompt、事件和 authoritative snapshot，不在浏览器侧推断可复活目标或费用/A 限制。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysGhostMatronOptionalGraveyardUnitToBase|FullyQualifiedName~CoreRuleEngineRejectsGhostMatronGraveyardTargetAboveCostOrPowerLimit|FullyQualifiedName~CoreRuleEnginePlaysHarrowingPlayGraveyardUnitToBase|FullyQualifiedName~CoreRuleEnginePlaysSteadfastLoyaltyPlayLowCostGraveyardUnitToBase|FullyQualifiedName~CoreRuleEnginePlaysCruelRevivalDestroyFriendlyUnitPlayGraveyardUnitToBase|FullyQualifiedName~P2PreflightOfficialUnitStaticFixtures|FullyQualifiedName~P4GhostMatronTargetRejectedFixture"` 通过 7/7；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3189/3189；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为完整待命/反应位置交换、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十八批补充：补齐《金克丝》（OGN·202/298、OGN·202a/298、ARC-005/006）“每当你弃置任意数量的手牌时，让我变为活跃状态，且本回合内战力 +1”的服务端代表路径。所有服务端实际弃牌批次现在都会按 authoritative field state 找到同控制者场上正面、非待命《金克丝》，每个弃牌批次只广播一次 `TRIGGER_RESOLVED`，随后广播 `UNIT_READIED` 与 `POWER_MODIFIED_UNTIL_END_OF_TURN`；面朝下、待命态或对手控制的金克丝不会误触发。覆盖入口包括普通结算弃牌、额外费用弃牌、任意手牌目标弃牌、全手牌弃置、乐芙兰战场结果弃牌费用和战场征服弃牌抽牌。前端仍只展示服务端事件和 snapshot，不在浏览器侧推断弃牌触发或战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79JinxDiscardTriggerReadiesAndGainsPowerOnceForDiscardBatch|FullyQualifiedName~CoreRuleEnginePlaysJinxDiscardTwoHand|FullyQualifiedName~CoreRuleEnginePlaysChempunkToughDiscardHand|FullyQualifiedName~CoreRuleEnginePlaysRewindTimelineDiscardHandsDrawFour|FullyQualifiedName~CoreRuleEnginePlaysBattlefieldConquerDiscardDraw|FullyQualifiedName~P79BattlefieldConquerDiscardsThenDraws|FullyQualifiedName~P79LegendTriggerLeblanc"` 通过 13/13；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3186/3186；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke，目标端口保持清理。整体仍 **NOT READY**，因为完整待命/反应位置交换、废牌堆打出、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十七批补充：补齐《神射海盗》（OGN·130/298）“进攻时对同一处战场的一个敌方单位造成 1 点伤害”的服务端代表路径。最小 `DECLARE_BATTLE` 接受后、战斗伤害分配前，Core 现在按 authoritative attacker/defender 列表找到正面、非待命且由进攻玩家控制的《神射海盗》，广播 `TRIGGER_RESOLVED`，并对本次同战场首个防守敌方单位广播 `DAMAGE_APPLIED` / `effectKind=SHARPSHOOTER_PIRATE_ATTACK_DAMAGE_1`。防守方神射海盗不会误触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧选择目标或裁决伤害。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79SharpshooterPirateDamagesEnemyUnitWhenAttackingBattlefield|FullyQualifiedName~P79SharpshooterPirateSkipsAttackDamageWhenDefending|FullyQualifiedName~P79DuneDrakeGainsPowerWhenAttackingReadyEnemyUnit|FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3183/3183；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整战斗触发目标选择、完整待命显露/目标伤害、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十六批补充：补齐《余火修士》（OGN·167/298）“当你打出一张牌面朝下的待命牌时，我本回合内获得 +2”的服务端代表路径。`HIDE_CARD` 成功暗置待命牌并广播 `CARD_HIDDEN` 后，Core 现在按 authoritative field state 找到同控制者场上正面、非待命《余火修士》，广播 `TRIGGER_RESOLVED`，并用 `POWER_MODIFIED_UNTIL_END_OF_TURN` 将来源自身本回合战力 +2；面朝下、待命态或对手控制的余火修士不会误触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧推断待命触发或战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden|FullyQualifiedName~P4HideCardCommandUsesGuerrillaWarfareFreeStandbyPermission|FullyQualifiedName~P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides|FullyQualifiedName~ActionPromptFiltersHideCardSourcesByPayableStandbyCosts"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3181/3181；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整待命显露/目标伤害、完整战斗时机/触发排序、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十五批补充：补齐《沙丘亚龙》（OGN·131/298）“进攻有准备状态敌方单位的战场时，本回合战力 +2”的服务端代表路径。最小战斗结算现在在计算进攻单位战斗力时记录本次战斗是否存在准备状态防守单位；《沙丘亚龙》作为进攻单位满足条件时通过 `staticPowerBonus=2` 写入权威 `DAMAGE_APPLIED` 事件与战斗伤害计算，作为防守单位时不会误触发。前端仍只展示服务端伤害事件和 snapshot，不在浏览器侧裁决战斗战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79DuneDrakeGainsPowerWhenAttackingReadyEnemyUnit|FullyQualifiedName~P79DuneDrakeSkipsPowerWhenDefending|FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3180/3180；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整战斗时机/触发排序、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十四批补充：补齐《比尔吉沃特恶霸》（OGN·125/298）“如果我拥有增益，则我获得游走”的服务端代表路径。Core 与 ActionPrompt 现在都按 authoritative `CardObjectState` 动态判断：来源是当前玩家控制的正面单位且带 `增益` 时，即使对象标签中没有永久 `游走`，也开放 `ROAM` 精确战场移动；未拥有增益时同一命令被服务端拒绝，prompt 也不暴露 `ROAM` source requirement。前端仍只展示和提交服务端 prompt 支持的 MOVE_UNIT，不在浏览器侧推断关键词。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BilgewaterBullyWithBoonCanUseRoam|FullyQualifiedName~P79BilgewaterBullyWithoutBoonDoesNotUseRoam|FullyQualifiedName~P79BattlefieldStaticRoamAllowsPreciseBattlefieldMovement|FullyQualifiedName~P79BattlefieldStaticRoamPromptSkipsOpponentControlledSource"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3178/3178；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无 DevUi 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整游走移动/战斗时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十三批补充：补齐《残响之魂》（OGN·118/298）“每回合首次当你的友方单位被摧毁时抽一张牌”的服务端代表路径。普通栈结算生成 `UNIT_DESTROYED` 后，Core 现在按服务端 authoritative field state 找到同控制者场上正面、非待命《残响之魂》，并用 `DestroyedUnitOwnerIdsThisTurn` 作为本回合首次守卫；第一次触发时广播 `TRIGGER_RESOLVED` 并由权威抽牌路径抽 1 张，同回合后续友方单位摧毁不再触发。面朝下、待命、对手控制的残响之魂不会误触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧推断摧毁触发或抽牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79ResonantSoulDrawsOnlyForFirstFriendlyUnitDestroyedEachTurn|FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed|FullyQualifiedName~P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3176/3176；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十二批补充：补齐《幽魂半人马》（UNL-068/219）“每当另一名友方单位被摧毁时，本回合战力 +2”的服务端代表路径。普通栈结算生成 `UNIT_DESTROYED` 后，Core 现在按服务端 authoritative field state 找到同控制者场上正面、非待命《幽魂半人马》，并复用 `POWER_MODIFIED_UNTIL_END_OF_TURN` 权威修正路径把来源自身本回合战力 +2；面朝下、待命、对手控制的半人马不会误触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧推断摧毁触发或战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79GhostlyCentaurGainsTemporaryPowerWhenAnotherFriendlyUnitDestroyed|FullyQualifiedName~P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed|FullyQualifiedName~P79UnsungHeroDrawsTwoWhenDestroyedWhilePowerful|FullyQualifiedName~P79UnsungHeroSkipsDrawWhenDestroyedBelowPowerful"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3175/3175；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十一批补充：补齐《无名英雄》（SFD·167/221）“绝念时若我是强力单位则抽两张牌”的服务端代表路径。普通栈摧毁路径在确认目标作为正面、非待命单位进入所属者废牌堆，且被摧毁前战力达到强力阈值 5 以上后，服务端排入并解析 `UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2`，随后由权威抽牌路径抽 2 张；低于强力阈值时不会排队或抽牌。前端仍只展示服务端触发、抽牌事件和 snapshot，不在浏览器侧判断强力或绝念。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79UnsungHeroDrawsTwoWhenDestroyedWhilePowerful|FullyQualifiedName~P79UnsungHeroSkipsDrawWhenDestroyedBelowPowerful|FullyQualifiedName~P79HonestBrokerCreatesSleepingGoldWhenDestroyed|FullyQualifiedName~P79IroncladVanguardCreatesTwoRobotsWhenDestroyed|FullyQualifiedName~P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed|FullyQualifiedName~P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed"` 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3174/3174；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百七十批补充：补齐《诚实掮客》（SFD·155/221）“绝念时打出一个休眠的金币装备指示物”的服务端代表路径。普通栈摧毁路径在确认目标作为正面、非待命单位进入所属者废牌堆后，服务端排入并解析 `HONEST_BROKER_LAST_BREATH_CREATE_GOLD`，随后复用权威 `EQUIPMENT_TOKEN_CREATED` 指示物生成路径，在控制者基地创建 1 个休眠/横置、带 `CARD_TYPE:EQUIPMENT` 标签的“金币”装备指示物。前端仍只展示服务端触发、创建指示物事件和 snapshot，不在浏览器侧判断绝念或创建结果。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79HonestBrokerCreatesSleepingGoldWhenDestroyed|FullyQualifiedName~P79IroncladVanguardCreatesTwoRobotsWhenDestroyed|FullyQualifiedName~P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed|FullyQualifiedName~P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3172/3172；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十九批补充：补齐《铁甲先锋》（SFD·021/221）“绝念时打出两名 3 战力机器人到你的基地”的服务端代表路径。普通栈摧毁路径在确认目标作为正面、非待命单位进入所属者废牌堆后，服务端排入并解析 `IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS`，随后复用权威 `UNIT_TOKEN_CREATED` 指示物生成路径，在控制者基地创建两名 3 战力、带 `CARD_TYPE:UNIT` / `机械` 标签的机器人。前端仍只展示服务端触发、创建指示物事件和 snapshot，不在浏览器侧判断绝念或创建结果。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79IroncladVanguardCreatesTwoRobotsWhenDestroyed|FullyQualifiedName~P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed|FullyQualifiedName~P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3171/3171；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十八批补充：补齐《机械戏法师》（OGN·239/298）“绝念时打出三名 1 战力随从到你的基地”的服务端代表路径。普通栈摧毁路径在确认目标作为正面、非待命单位进入所属者废牌堆后，服务端排入并解析 `MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS`，随后复用权威 `UNIT_TOKEN_CREATED` 指示物生成路径，在控制者基地创建三名 1 战力 `CARD_TYPE:UNIT` 随从。前端仍只展示服务端触发、创建指示物事件和 snapshot，不在浏览器侧判断绝念或创建结果。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79MechanicalTricksterCreatesThreeMinionsWhenDestroyed|FullyQualifiedName~P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed|FullyQualifiedName~P79SadPoroDrawsWhenDestroyedWhileIsolated|FullyQualifiedName~P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase"` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3170/3170；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十七批补充：补齐《侦察飞鹰》（OGN·216/298）“绝念时召出一枚休眠符文”的服务端代表路径。普通栈摧毁路径在确认目标作为正面、非待命单位进入所属者废牌堆后，服务端排入并解析 `SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1`，随后调用权威符文牌堆召出 1 枚符文并保持休眠/横置；`RUNES_CALLED` 事件记录来源对象、召出数量、符文对象列表与触发原因。前端仍只展示服务端事件和 snapshot，不在浏览器侧判断绝念或符文状态。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79ScoutingWarhawkCallsSleepingRuneWhenDestroyed|FullyQualifiedName~P79SadPoroDrawsWhenDestroyedWhileIsolated|FullyQualifiedName~P79SadPoroSkipsDrawWhenDestroyedWithAnotherFriendlyUnitAtSameBase|FullyQualifiedName~P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase|FullyQualifiedName~P79LoyalPoroSkipsDrawWhenDestroyedWhileIsolated"` 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3169/3169；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、创建随从等绝念分支、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十六批补充：补齐《哀哀魄罗》（SFD·036/221、UNL-221/219）“绝念，落单时抽一张牌”的服务端代表路径。普通栈摧毁路径会在移除前复用 authoritative field/object location 的同位置检测：若没有其他同控制者正面、非待命单位，则排入并解析 `SAD_PORO_LAST_BREATH_DRAW_1` 触发队列并抽 1 张牌；同基地存在友方单位时不触发。前端仍只展示服务端触发队列、抽牌事件和 snapshot，不自行判断落单。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79SadPoroDrawsWhenDestroyedWhileIsolated|FullyQualifiedName~P79SadPoroSkipsDrawWhenDestroyedWithAnotherFriendlyUnitAtSameBase|FullyQualifiedName~P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase|FullyQualifiedName~P79LoyalPoroSkipsDrawWhenDestroyedWhileIsolated"` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3168/3168；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、非栈摧毁触发时机、召符文/创建随从等绝念分支、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十五批补充：补齐《忠忠魄罗》（UNL-156/219）“绝念，如果我被摧毁时未处于落单状态，则抽一张牌”的服务端代表路径。`VENGEANCE_DESTROY_UNIT` 等普通栈摧毁路径现在会在移除前按 authoritative field/object location 判断同位置是否存在其他同控制者正面、非待命单位；满足条件时排入并解析 `LOYAL_PORO_LAST_BREATH_DRAW_1` 触发队列，随后由服务端抽 1 张牌。孤立状态下不会排队或抽牌，前端仍只展示服务端 `TRIGGER_QUEUED` / `TRIGGER_RESOLVED` / `CARD_DRAWN` 与 snapshot。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LoyalPoroDrawsWhenDestroyedWithAnotherFriendlyUnitAtSameBase|FullyQualifiedName~P79LoyalPoroSkipsDrawWhenDestroyedWhileIsolated|FullyQualifiedName~P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed|FullyQualifiedName~P79SoulShepherdAddsPowerToControlledTokenUnits|FullyQualifiedName~P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower|FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit"` 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3165/3165；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整绝念引擎、战斗/状态清理触发时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十四批补充：补齐《凶残颚鱼》（UNL-129/219）“当另一名友方单位被摧毁时，获得 1 经验”的服务端代表路径。普通栈结算生成 `UNIT_DESTROYED` 后，Core 现在按服务端 authoritative field state 找到同控制者场上正面、非待命《凶残颚鱼》，并由 `EXPERIENCE_GAINED` 事件记录 `sourceObjectId=颚鱼`、`cardNo=UNL-129/219`、`amount=1`；面朝下、待命、对手控制的颚鱼不会误触发。前端仍只展示服务端事件和 snapshot，不在浏览器侧推断摧毁触发。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79SavageJawfishGainsExperienceWhenAnotherFriendlyUnitDestroyed|FullyQualifiedName~P79SoulShepherdAddsPowerToControlledTokenUnits|FullyQualifiedName~P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower|FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3163/3163；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。Chrome 插件通道已确认可用；本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为战斗/状态清理等非栈摧毁时机、完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十三批补充：补齐《牧魂人》（UNL-077/219）“你的指示物单位获得战力 +1”的代表性服务端战斗静态规则。`ResolveBattleCombatPower` 现在通过官方 `P6TokenFactoryCatalog` 识别指示物单位 `cardNo`，只在同控制者场上存在正面、非待命《牧魂人》时把 +1 计入该 token 单位的 `staticPowerBonus`；面朝下/待命《牧魂人》、对手《牧魂人》和普通非 token 单位不会被计入。前端仍只展示服务端 `DAMAGE_APPLIED` 事件与 authoritative snapshot，不在浏览器侧判断 token 静态战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79SoulShepherdAddsPowerToControlledTokenUnits|FullyQualifiedName~P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower|FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3162/3162；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。Chrome 插件通道已轻量确认可用；本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome 业务 smoke。整体仍 **NOT READY**，因为完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十二批补充：补齐《猩红飞鸽》（UNL-154/219）“如果我和另一名单位一起进攻一处战场，则我获得战力 +2”的代表性服务端战斗静态规则。`ResolveBattleCombatPower` 现在接收本次战斗的进攻单位数量，在《猩红飞鸽》作为进攻单位且共同进攻时把 +2 计入 `staticPowerBonus`；该修正同样参与防守方回击时的攻击者致命伤害阈值计算。前端仍只展示服务端战斗伤害事件与 snapshot，不在浏览器侧判断共同进攻。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79ScarletPigeonGainsPowerWhenAttackingWithAnotherUnit|FullyQualifiedName~P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath|FullyQualifiedName~P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3161/3161；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十一批补充：补齐《花瓣仙子》（UNL-076/219）“我所处的战场你每有一名拥有瞬息的单位，我便获得战力 +1”的代表性服务端战斗静态规则。`ResolveBattleCombatPower` 现在通过权威 `ObjectLocations` 找到《花瓣仙子》所在战场，只统计同战场、同控制者、正面、非待命的瞬息单位作为 `staticPowerBonus`；其他战场瞬息、对手瞬息和待命瞬息不会被计入。前端仍只展示服务端战斗伤害事件与 snapshot，不在浏览器侧计算战力。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79PetalPixieCountsFriendlyEphemeralUnitsAtSameBattlefieldForBattlePower|FullyQualifiedName~P79BattlefieldStaticPowerAddsOneToBattleParticipants|FullyQualifiedName~P79BattlefieldEphemeralDefenderGainsSteadfast|FullyQualifiedName~CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3160/3160；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。新增回归断言《花瓣仙子》基础 2 战力在同战场 1 名合法己方瞬息单位存在时以 `staticPowerBonus=1` 打出 3 点战斗伤害，并同时排除待命、对手和其他战场瞬息对象。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-002/P1-004 第二百六十批补充：补齐乐芙兰（UNL-090/219、UNL-090a/219）“你在我所处战场的瞬息效果不会触发”的代表性服务端静态规则。开始阶段瞬息清理现在会按权威 `ObjectLocations` 判断同一战场：若该战场上存在当前回合玩家控制的正面乐芙兰单位，则同战场己方瞬息对象不会触发开始阶段摧毁；基地瞬息、其他战场瞬息和其他玩家瞬息仍按原规则摧毁。新增 development-only `lifecycle-ephemeral-leblanc-static` seed 与 Hub 回归，前端仍只展示服务端事件/snapshot，不在浏览器侧判断瞬息是否触发。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineSuppressesEphemeralTurnStartAtLeblancBattlefield|FullyQualifiedName~P4EphemeralKeywordDestroysControlledObjectsAtTurnStart|FullyQualifiedName~P6LifecycleEphemeralSeedBroadcastsTurnStartCleanupInDevelopment|FullyQualifiedName~P6LifecycleEphemeralLeblancStaticSeedKeepsSameBattlefieldEphemeralInDevelopment|FullyQualifiedName~P79LegendTriggerLeblanc"` 通过 9/9；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3159/3159；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签与玩家可见 fallback 门禁均通过。本批无前端 UI 运行时代码变更，未启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整正式 18 步 E2E、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十九批补充：前端连接超时 fallback 与 API 行为规格 404 文案继续去英文内部说明，并把关键玩家可见英文 fallback 纳入 DevUi build gate。此前 `MatchSocket` 在 `JoinRoom/Reconnect` 等待 `Joined` 超时时会抛出英文 `Timed out waiting for Joined.`，`/catalog/behavior-specs?cardNo=...` 未命中时返回 `BehaviorSpec not found.`。现在两处均返回中文；新增 `scripts/check-user-facing-text.mjs` 并接入 `npm run build`，防止已收口的房间、重连、seed、行为规格和连接超时英文 fallback 回流。前端仍不新增任何规则裁决，只改善服务端/API/SignalR fallback 的中文可见性。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，且实际执行 `check:user-facing-text` 并输出 `User-facing fallback text check passed.`；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3157/3157。无业务交互流程变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十八批补充：房间、重连和开发测试状态入口的错误文案继续去英文内部说明。此前房间满员、重连令牌缺失/无效、玩家不在房间、空 `playerId` / `clientIntentId` / `scenarioId`、单人房间载入 dev scenario、以及非开发环境调用 `SeedScenario` 都会正确拒绝，但玩家可见错误仍含 `room already has two players`、`invalid reconnect token`、`player is not in room`、`clientIntentId is required`、`scenarioId is required` 或 `SeedScenario is only available in Development.`。现在这些入口统一返回中文房间/重连/开发状态文案；结构化 error code、connection、room/player id 与 scenario id 仍保留协议字段供前端处理。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~JoinRoomRejectsThirdPlayerWithError|FullyQualifiedName~ReconnectWithInvalidTokenReturnsStableErrorCode|FullyQualifiedName~RequestSnapshotForUnknownPlayerReturnsStableErrorCode|FullyQualifiedName~SubmitIntentForUnknownPlayerReturnsStableErrorCode|FullyQualifiedName~SubmitIntentWithoutClientIntentIdReturnsStableErrorCode|FullyQualifiedName~SeedScenarioIsRejectedOutsideDevelopment|FullyQualifiedName~SubmitRequiresPlayerToJoinRoomFirst|FullyQualifiedName~EnsurePlayerRequiresPlayerId|FullyQualifiedName~SubmitRequiresClientIntentId|FullyQualifiedName~ReadyRequiresClientIntentId|FullyQualifiedName~SeedScenarioRequiresScenarioId|FullyQualifiedName~SeedScenarioRequiresTwoJoinedPlayers|FullyQualifiedName~JoinRejectsThirdPlayer|FullyQualifiedName~RecoveredExistingPlayerMustUseReconnectInsteadOfJoin"` 通过 14/14；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3157/3157；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-001/P1-004 第二百五十七批补充：正式卡组提交、房间准备和基础会话错误文案继续去英文内部说明。此前 `OfficialDeckValidator`、`SubmitDeckAsync`、`ReadyAsync`、未开局/已结束提交、未知命令和重复 `clientIntentId` 路径会正确拒绝并保持 authoritative state，但玩家可见错误中仍含 `mainDeck must...`、`invalid deck`、`match already finished`、`Unsupported command: FLIP_TABLE` 或 `clientIntentId...` 等英文/诊断文本。现在这些入口统一返回中文卡组、房间和命令错误；结构化 error code、action kind、deck payload 与 intent id 仍保留协议字段供前端按服务端契约处理。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialOpeningTests|FullyQualifiedName~SubmitIntentBeforeReadyReturnsStableErrorCode|FullyQualifiedName~SubmitIntentUnsupportedCommandReturnsStableErrorCode|FullyQualifiedName~SubmitIntentDuplicateConflictReturnsStableErrorCode|FullyQualifiedName~P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins|FullyQualifiedName~SubmitRequiresMatchToStart|FullyQualifiedName~OfficialOnlyRoomsRejectReadyBeforeDeckSubmission"` 通过 16/16；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3154/3154；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十六批补充：`PLAY_CARD` 代表性传奇触发目标拒绝文案继续去英文内部说明。此前雷恩加尔和蕾欧娜的触发目标通过服务端校验拒绝非法对象，但错误消息包含英文 legend trigger 说明。现在服务端分别返回中文雷恩加尔/蕾欧娜传奇触发目标说明；结构化 target object ids 和 trigger effect id 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendTriggerRengarRejectsNonUnitTriggerTarget|FullyQualifiedName~P79LegendTriggerRengarRejectsOpponentControlledFieldTriggerTarget|FullyQualifiedName~P79LegendTriggerLeonaRejectsNonFriendlyUnitBoonTarget"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3153/3153；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十五批补充：`DECLARE_BATTLE` 的战场效果目标选择拒绝文案继续去英文内部说明。此前普通战场误带 `battlefieldTargetObjectIds`、防守方坚守战场选择非防守单位、或防守方召回战场选择超过一个目标时会被服务端拒绝并保持状态不变，但错误消息包含英文效果说明和具体英文战场名。现在服务端返回中文战场效果目标说明；结构化 battlefield target object ids、战场 id 和 command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldEphemeralSteadfastSeed|FullyQualifiedName~P79BattlefieldDefenderSteadfastSeedOffersBattlefieldDestinationAndChoice|FullyQualifiedName~P79BattlefieldDefendMoveToBaseSeedOffersBattlefieldDestinationAndChoice"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3153/3153；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十四批补充：`ACTIVATE_ABILITY` 已开放代表路径的拒绝文案继续去内部协议名和英文 ability/debug 说明。此前 Vi、Xerath 与战场授予经验技能在错误时点、非法目标、非法来源、来源身份未知、来源已横置或资源不足时会正确拒绝并保持状态不变，但多条错误消息包含 raw `ACTIVATE_ABILITY`、英文技能名或 Mutation Garden 内部说明。现在服务端统一返回中文启动技能/战场授予经验/泽拉斯技能说明；结构化 prompt action、ability id、target choices 与 command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbilityCommandRejects|FullyQualifiedName~BattlefieldUnitExperienceAbility"` 通过 58/58；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3153/3153；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十三批补充：`DECLARE_BATTLE` 未开放路径拒绝文案继续去内部协议名。此前声明战斗命令未进入当前代表性最小战斗计划时会正确拒绝并保持状态不变，但错误消息包含 raw `DECLARE_BATTLE` 和 P4 阶段英文说明。现在服务端返回中文“当前声明战斗路径尚未由服务端开放。”，既有声明战斗负例回归同步断言中文错误；结构化 command kind、战场、攻击者、防守者和费用 token 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DeclareBattleCommand"` 通过 56/56；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3147/3147；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十二批补充：`LEGEND_ACT` 未开放 ability 与非法时点拒绝文案继续去内部协议名。此前未知传奇行动 ability id、或当前 timing window 不允许该传奇行动时会被服务端拒绝并保持状态不变，但错误消息包含 raw `LEGEND_ACT` 和英文内部说明。现在服务端分别返回中文“当前传奇行动尚未由服务端开放。”与“当前时点不能使用该传奇行动。”，相关回归同步断言错误消息不含 raw action kind；结构化 command kind/ability id 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActRejectsUnknownAbilityWithChineseError|FullyQualifiedName~P79LegendActDianaRejectsOutsideSpellDuelFocus"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3147/3147；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十一批补充：战场静态效果禁止 `PLAY_CARD` 目的地的拒绝文案继续去内部协议名。此前服务端在战场静态效果禁止单位打出到该战场时会正确拒绝并保持状态不变，但错误消息包含 raw `PLAY_CARD` 和英文内部说明。现在服务端返回中文“战场效果禁止将单位打出到该战场。”，Core 与 Hub 回归同步断言错误消息不含 raw action kind；结构化 command kind/destination 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticPreventsUnitPlayToBattlefield"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百五十批补充：`ACTIVATE_ABILITY` 未开放路径拒绝文案继续去内部协议名。此前 deferred legend/battlefield/token activated surfaces 未进入 `P4ActivatedAbilityCatalog` 时会正确拒绝并保持状态不变，但错误消息包含 raw `ACTIVATE_ABILITY` 和 P4 阶段英文说明。现在服务端返回中文“当前启动技能路径尚未由服务端开放。”，相关 deferred surface 回归同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6ActivateAbilityCommandRejects|FullyQualifiedName~P6BattlefieldEffectCommandRejectsDeferredActivatedSurfaces"` 通过 8/8；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十九批补充：`TURN_START` 非回合玩家推进拒绝文案继续去内部协议名。此前回合开始阶段若非当前回合玩家提交命令触发推进，会被服务端拒绝并保持状态不变，但错误消息包含 raw `TURN_START` 和英文内部说明。现在服务端返回中文“回合开始只能由当前回合玩家推进。”，核心回归测试同步断言错误消息不含 raw phase/action token；结构化 phase 仍保留协议枚举供前端按服务端 snapshot 展示。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsTurnStartAdvanceFromNonTurnPlayer"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十八批补充：`PLAY_CARD` 伏击模式未开放路径拒绝文案继续去内部协议名。此前 `PLAY_CARD` 以 `AMBUSH` 模式进入未开放/非法伏击出牌路径时会正确拒绝并保持状态不变，但错误消息包含 raw `PLAY_CARD mode AMBUSH` 和 P4 阶段英文说明。现在服务端返回中文“当前伏击出牌路径尚未由服务端开放。”，既有伏击出牌负例回归同步断言中文错误；结构化 command mode 与 prompt action 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4AmbushPlayCardMode"` 通过 21/21；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十七批补充：`ASSEMBLE_EQUIPMENT` 未开放路径拒绝文案继续去内部协议名。此前装配在未进入当前代表性最小计划、优先权窗口、来源/目标异常或费用异常等负例路径会正确拒绝并保持状态不变，但错误消息包含 raw `ASSEMBLE_EQUIPMENT` 和 P4 阶段英文说明。现在服务端返回中文“当前装备装配路径尚未由服务端开放。”，既有装配负例回归同步断言中文错误；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4AssembleEquipmentCommand"` 通过 28/28；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十六批补充：`LEGEND_ACT` 来源拒绝文案继续去内部协议名。此前传奇行动来源不在自己传奇区、来源不由当前玩家控制、来源缺少服务端确认牌号、来源不具备请求的已开放传奇行动或来源已横置时，会被服务端拒绝并保持状态不变，但错误消息包含 raw `LEGEND_ACT` 或英文内部说明。现在这些来源拒绝路径返回中文玩家文案，核心回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79LegendActRejectsSourceOutsideLegendZoneWithChineseError|FullyQualifiedName~P79LegendActRejectsOpponentControlledSourceWithChineseError|FullyQualifiedName~P79LegendActRejectsSourceWithoutCardNoWithChineseError|FullyQualifiedName~P79LegendActRejectsSourceWithoutRequestedAbilityWithChineseError|FullyQualifiedName~P79LegendActRejectsExhaustedSourceWithChineseError"` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3146/3146；`source ../../scripts/dev-env.sh && npm run build` 通过。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十五批补充：`ACTIVATE_ABILITY` 来源控制权拒绝文案继续去内部协议名。此前 Vi / Xerath 代表性启动技能在来源位于玩家场区但实际不由当前玩家控制时，会被服务端拒绝并保持状态不变，但错误消息包含 raw `ACTIVATE_ABILITY` 和英文内部说明。现在服务端返回中文“启动技能只能选择当前玩家控制的来源。”，核心回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4ActivateAbilityCommandRejectsViOpponentControlledSourceInPlayerZone|FullyQualifiedName~P4ActivateAbilityCommandRejectsXerathOpponentControlledSourceInPlayerZone"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3141/3141。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十四批补充：全局 finished-match 拒绝文案继续去英文内部说明。此前对局已结束后提交任意命令会被 `CoreRuleEngine.ResolveAsync` 拒绝并保持状态不变，但错误消息为英文 “Match is not in progress.”。现在服务端统一返回中文“对局已经结束，不能继续提交行动。”，回归测试覆盖结束后 `END_TURN` 与 `MULLIGAN` 并断言错误消息不含 raw action kind。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsGameCommandAfterMatchFinished|FullyQualifiedName~CoreRuleEngineRejectsMulliganAfterMatchFinished"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3141/3141。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十三批补充：`PLAY_CARD` 出牌来源拒绝文案继续去内部协议名。此前出牌来源不在手牌、来源缺少服务端确认牌号、提交牌号与来源身份不匹配、来源不由当前玩家控制时会被服务端拒绝并保持状态不变，但错误消息仍含 raw `PLAY_CARD` 或英文内部说明。现在这些出牌来源拒绝路径返回中文玩家文案，核心回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4PlayCardCommandRejectsSourceWithoutCardNo|FullyQualifiedName~P4PlayCardCommandRejectsSourceCardNoMismatch|FullyQualifiedName~P4PlayCardCommandRejectsOpponentControlledSourceInPlayerHand"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3141/3141。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十二批补充：`SURRENDER` 运行时拒绝文案继续去内部协议名。此前异常单人房间或缺少对手时提交投降会被服务端拒绝并保持状态不变，但错误消息包含 raw `SURRENDER` 和英文说明。现在服务端返回中文“投降需要存在对手。”，新增回归测试断言错误消息不含 raw action kind，并保留正常双人投降胜负裁决路径。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsSurrenderWithoutOpponent|FullyQualifiedName~CoreRuleEngineSurrenderFinishesMatchAndOpponentWins"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3141/3141。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十一批补充：`MOVE_UNIT` 运行时拒绝文案继续去内部协议名。此前移动单位在错误窗口、非法区域、暂未开放额外费用、目的地相同、来源位置/控制权/正面单位/已知牌号/战斗状态不合法、战场静态禁止回基地、贴附装备移动或精确游走代表路径不合法时会被服务端拒绝并保持状态不变，但错误消息仍含 raw `MOVE_UNIT`、英文内部说明或 `P4` 阶段口径。现在这些拒绝路径返回中文玩家文案，核心、official opening 与 Hub seed 回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4MoveUnitCommandRejects|FullyQualifiedName~P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit|FullyQualifiedName~PreciseRoamMoveRejectsOriginThatDoesNotMatchAuthoritativeLocation|FullyQualifiedName~P79BattlefieldStaticPreventMoveBaseSeedRejectsMoveToBase"` 通过 22/22；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百四十批补充：`REVEAL_CARD` 运行时拒绝文案继续去内部协议名。此前待命翻开/反应在来源不在基地、非法模式、窗口不合法、无优先权、来源非面朝下、身份未知/不匹配、来源不受控或来源无待命关键词等路径会被服务端拒绝并保持状态不变，但错误消息仍含 raw `REVEAL_CARD` 或英文内部说明。现在这些拒绝路径返回中文玩家文案，常规翻开与反应回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4RevealCardCommandRejects"` 通过 20/20；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十九批补充：`HIDE_CARD` 运行时拒绝文案继续去内部协议名。此前待命埋伏在错误窗口、来源不在手牌、非法去向/费用、身份未知/不匹配、来源不受控、来源无待命关键词等路径会被服务端拒绝并保持状态不变，但错误消息仍含 raw `HIDE_CARD` 或英文内部说明。现在这些待命埋伏拒绝路径返回中文玩家文案，关键回归测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4HideCardCommandRejectsSourceOutsideHand|FullyQualifiedName~P4HideCardCommandRejectsOpponentHandSource|FullyQualifiedName~P4HideCardCommandRejectsKnownNonStandbyCard|FullyQualifiedName~P4HideCardCommandRejectsUnsupportedDestination|FullyQualifiedName~P4HideCardCommandRejectsUnsupportedOptionalCost|FullyQualifiedName~P4HideCardCommandRejectsMissingStandbyCost|FullyQualifiedName~P4HideCardCommandRejectsOutsideActivePlayerOpenMainWindow|FullyQualifiedName~P4HideCardCommandRejectsPendingStackWindow|FullyQualifiedName~P4HideCardCommandRejectsSourceCardNoMismatch|FullyQualifiedName~P4HideCardCommandRejectsSourceWithoutCardNo|FullyQualifiedName~P4HideCardCommandRejectsOpponentControlledSourceInPlayerHand"` 通过 11/11；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十八批补充：`MULLIGAN` 运行时拒绝文案继续去内部协议名。此前正式 opening path 中错误玩家、超过数量、重复选择、非手牌选择或替换抽牌控制权异常会被服务端拒绝并保持状态不变，但错误消息仍含 raw `MULLIGAN` 或英文规则说明。现在起手调整各拒绝路径均返回中文玩家文案，正式 opening 与核心控制权测试同步断言错误消息不含 raw action kind；结构化 prompt action/command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OfficialMulliganRejectsInvalidSelectionsAndWrongPlayer|FullyQualifiedName~CoreRuleEngineRejectsMulliganWhenSelectedHandCardIsNotControlledByPlayer|FullyQualifiedName~CoreRuleEngineRejectsMulliganWhenReplacementDrawIsNotControlledByPlayer"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十七批补充：`END_TURN` 运行时拒绝文案继续去内部协议名。此前非当前玩家、非开放主阶段或结算链未清空时提交结束回合，服务端会正确拒绝并保持状态不变，但错误消息包含 raw `END_TURN` 和英文窗口说明。现在服务端返回中文“结束回合只能由当前玩家在开放主阶段提交。”，非当前玩家与关闭窗口测试同步断言错误消息不含 raw action kind；结构化 prompt action 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsEndTurnFromNonActivePlayer|FullyQualifiedName~CoreRuleEngineRejectsEndTurnDuringClosedPriorityWindow"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十六批补充：`TAP_RUNE` / `RECYCLE_RUNE` 运行时拒绝文案继续去内部协议名。此前手写客户端在错误窗口、错误来源、已横置符文、缺少已知符文牌号或非特性符文等路径提交符文资源动作时，服务端会正确拒绝并保持状态不变，但错误消息仍含 raw action kind 和英文规则说明。现在横置/回收符文的运行时错误均使用中文玩家文案，未知牌号回归测试同步断言错误消息不含 `TAP_RUNE` / `RECYCLE_RUNE`；结构化 prompt action 与 command kind 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsTapRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十五批补充：`PASS_PRIORITY` / `PASS_FOCUS` 非法窗口错误文案去内部协议名。此前玩家或手写客户端在没有优先权窗口/法术对决焦点窗口时提交让过命令，服务端会正确拒绝并保持状态不变，但错误消息包含 raw `PASS_PRIORITY` / `PASS_FOCUS` 和英文窗口名。现在服务端分别返回“让过优先权只能在优先行动窗口中提交。”与“让过焦点只能在法术对决焦点窗口中提交。”，测试同步断言错误消息不含 raw action kind；结构化 prompt action 仍保留协议枚举供前端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsPassPriorityOutsidePriorityWindow|FullyQualifiedName~CoreRuleEngineRejectsPassFocusOutsideSpellDuel"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十四批补充：`DECLARE_BATTLE` 匹配 active START_BATTLE 任务失败时的运行时错误文案去内部协议名。此前当前行动玩家绕过 UI、把声明战斗命令提交到错误战场时，服务端会拒绝且保持状态不变，但错误消息为英文并包含 raw `DECLARE_BATTLE` / `START_BATTLE`。现在服务端返回中文“声明战斗必须匹配当前争夺战场的开始战斗任务。”，测试同步断言错误消息不含 raw action/task kind；结构化 pending task / prompt action 仍保留协议枚举供客户端按服务端候选提交。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十三批补充：START_BATTLE 阻塞原因的测试契约与当前玩家可见安全口径对齐。非当前行动玩家绕过 UI 手写 `DECLARE_BATTLE` 时，服务端仍以 `PhaseNotAllowed` 拒绝并保持状态不变，但 `ActionPrompt.reason`/error message 使用中文“开始战斗”，不再把 raw `START_BATTLE` 任务名作为玩家可见文本。该批只更新 conformance 断言，锁定“显示中文原因且不泄漏内部 task kind”的当前契约。
- 已补验证：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 通过 3140/3140。无前端运行时代码变更，本批未启动 API/Vite/Chrome smoke；目标端口保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百三十二批补充：战场得分与战斗声明前端 smoke 覆盖新增代表路径。Chrome 插件房间 `smoke-battlefield-held-score-1778247059745` 中，P1 页面从服务端 `DECLARE_BATTLE` candidate/sourceRequirements 打开《大力仙灵》详情，选择战场 `SFD·214/221` 与防守单位 `SFD·125/221` 并提交声明战斗；服务端结算战斗伤害、单位摧毁、据守战场、能量枢纽据守支付、`SCORE_GAINED` 和战斗/战场控制清理，P2 分数变为 `1/8`。该批无服务端规则代码变更，只补前端真实操作证据。
- 已补验证：Chrome 插件 smoke 通过，应用 runtime error 0；仅记录 Chrome 扩展 autoplay `NotAllowedError` 噪声。验证后已清理 Chrome 标签、API/Vite 进程，目标端口无监听。整体仍 **NOT READY**，因为该证据仍是 development seed 代表路径，不替代同一连续正式 E2E，也不代表完整 battle task/central cleanup/PaymentEngine/LayerEngine/全卡证据清零。

- P1-004 第二百三十一批补充：前端投降确认从浏览器原生确认弹窗改为站内确认区，避免 Chrome/Browser smoke 卡在 `window.confirm`，也让产品交互保持在 Web UI 内。该批没有修改服务端规则；前端仍只在服务端 prompt 暴露 enabled `SURRENDER` 候选时显示投降入口，确认后提交同一个 `SURRENDER` 命令，由服务端裁决胜者。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件房间 `smoke-surrender-confirm-1778246799998` 中，P1 页面连接，后台 P2 入座并 seed `basic-play`；点击“投降”显示“确认投降 / 取消”，取消可收起，再次确认后结果页显示 `胜者：P2`。验证后已清理 Chrome 标签、API/Vite 进程，目标端口无监听。整体仍 **NOT READY**，因为同一正式 E2E 还没有覆盖战场争夺/战斗、战场得分，服务端 P0/P1 规则缺口也仍未清零。

- P1-004 第二百三十批补充：正式房间主流程 E2E 探针继续验证服务端 authoritative 房间/对局链路。Chrome 插件房间 `room-vnpnxy` 覆盖页面创建/加入、P1/P2 入座、官方测试卡组提交、准备、正式开局、双方起手调整、第 1 回合主阶段、召符文、抽牌、符文横置、P1 打出《炼金太保》进结算链、P1/P2 让过优先权、单位结算进基地、移动到战场、P1 结束回合、P2 第 2 回合开始，以及 reload/reconnect 后恢复 authoritative snapshot。该批没有修改服务端规则代码；前端仍只提交服务端 prompt/candidate 支持的 TAP_RUNE、PLAY_CARD、PASS_PRIORITY、MOVE_UNIT、END_TURN 等命令。
- 已补验证：Chrome 插件在“投降”按钮的 `window.confirm` 处卡住，未把该步记录为前端按钮通过；随后同一房间通过后台 SignalR 提交 P2 `SURRENDER`，结果页 headless 验证 `胜者：P1` 且 snapshot `winnerPlayerId=P1`。验证后已清理 Chrome 标签、API/Vite/headless 进程，目标端口无监听。整体仍 **NOT READY**，因为同一正式 E2E 还没有覆盖战场争夺/战斗、战场得分，完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据也仍未清零。

- P1-004 第二百二十九批补充：入口文档继续降低历史 READY 口径误导。`README.md` 和 `docs/START_HERE.md` 现在把 P7/P7.9 明确标为历史阶段记录，并把当前验收入口指向 `CURRENT_FRONTEND_REBUILD_PLAN`、`CURRENT_SERVER_RULE_AUDIT`、`CURRENT_COMPLETION_AUDIT` 与 `docs/任务补充.md`；Browser/Chrome smoke 原则也更新为优先插件、必要时后台 headless，不使用 Computer Use 抢前台。该批无服务端代码变更。
- 已补验证：文档-only 变更，`git diff --check` 通过；未启动 API/Vite/Chrome smoke，目标端口无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十八批补充：前端卡牌详情不可组合警告继续去内部 reason 直显。`CardDetailDrawer` 的所有 composer `unsupportedReason` 显示现在统一走 `promptReasonLabel`，中文服务端原因保留，内部英文说明、协议 token、对象 ID 或未来裸 metadata 会降级为中文兜底；该批只影响前端展示，不改变服务端候选、`composable=false` 语义或命令提交。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件本批可用，房间 `smoke-unsupported-check`，P1/P2 通过真实 Chrome 页面入座，后台 development seed `battlefield-legend-attach-armament`；P1 打开《圣锤之毅》详情后显示服务端 LEGEND_ACT 元数据与中文不可组合警告，`确认传奇行动` disabled，正文/title 不含 raw ability id、对象 id、`unsupportedReason`、`composable`、`targetChoicesByIndex` 或 `sourceRequirements`，应用 error 0。smoke 后已 finalize Chrome 标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十七批补充：前端规则队列 `StackPanel` 的未知 phase/result fallback 继续去 raw enum。通用 `labelFor` 现在会把未识别的协议 token 降级为“服务端阶段 / 服务端战斗结果 / 服务端战场结果”等中文占位，而不是直接显示 `IDLE`、`BATTLE_TASKS` 或未来新增的服务端枚举；自然文本仍经过内部 ID 脱敏后展示。该批只影响 snapshot 展示，不改变服务端 pending task、battle result 或命令提交。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件本批两次打开本地 Vite 标签被 Chrome 报 `ERR_BLOCKED_BY_CLIENT`，未使用 Computer Use；随后使用后台 bundled Playwright + 系统 Chrome headless smoke。房间 `smoke-stack-label-headless-1778243871233`，P1 页面连接，后台 P2 入座并 seed `basic-play`；规则队列显示“阶段：空闲”，正文不含 `IDLE`、`BATTLE_TASKS`、`BATTLEFIELD_TASKS`、`SPELL_DUEL_TASKS`、`STATE_BASED_CLEANUP`、`CONTROL_RESOLVED`、`NO_RESULT` 或 `CLOSED`，应用 error 0。smoke 后已清理后台 SignalR、headless Chrome、API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十六批补充：前端 `ActionPrompt.reason` / `ActionPromptCandidate.reason` 展示继续过滤内部说明。`formatters.ts` 新增 `promptReasonLabel` / `promptReasonTitle`，`ActionPanel`、`RoomPage` 和 `CardDetailDrawer` 的 reason 正文与 tooltip 不再直接渲染服务端内部英文说明、协议 token 或对象 ID；中文服务端原因仍按原文显示，不能安全展示的原因降级为“服务端候选 / 服务端行动提示”。该批只改展示层，前端仍只消费服务端 prompt/candidate 并提交服务端候选支持的命令。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-reason-filter-1778243194494`，P1 页面真实设置 `serverUrl=http://127.0.0.1:5093`、`playerId=P1` 后连接对战页，后台 SignalR 让 P2 入座并 seed `basic-play`；P1 打开《魔法小仙灵》卡牌详情后，页面显示“当前玩家普通开环行动 / 服务端可提交操作”，正文和 title 均不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`opening hand mulligan candidate`、`P1-UNIT-MIGHTY-FAERIE`、`P1-MAIN-001` 或 `BATTLEFIELD:P1-MAIN`，过滤非应用扩展噪声后应用 error 0。smoke 后已 finalize Chrome 标签并清理后台 SignalR、API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十五批补充：卡牌详情操作选择 tooltip 继续过滤内部英文/协议 reason。`CardDetailDrawer` 现在统一通过 `choiceTitle` 生成选择按钮 title：中文服务端原因保留，明显内部英文说明或协议 token 降级为“服务端候选”，避免 `implemented payable PLAY_CARD source`、`required for precise battlefield movement` 这类历史 reason 进入玩家 hover 文案；提交参数仍使用服务端候选 id，不新增前端规则裁决。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过，静态扫描确认卡牌详情中不再有 `title={choice.reason ?? undefined}` 或已知内部 reason 文案直连。Chrome 插件 smoke 使用房间 `smoke-choice-title-1778242626275`，P1 页面连接、后台 P2 入座并 seed `basic-play`，打开卡牌详情后页面和 title 不含 `implemented payable PLAY_CARD source`、`implemented coarse battlefield destination`、`required for precise battlefield movement`、`P1-HAND` 或 `P1-MAIN`，过滤非应用扩展噪声后应用 error 0。smoke 后已 finalize Chrome 标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十四批补充：事件/错误日志标题 tooltip 继续去 raw enum/code。`EventLog` 与 `RoomPage` 房间日志不再把 `event.kind` 或 `error.code` 放入 `<strong title=...>`，玩家 hover 也只能看到中文标题与中文描述；日志正文仍来自服务端事件/错误并通过既有中文 formatter 展示，不改变事件协议或规则裁决。
- 已补验证：静态扫描确认 `title={event.kind}` / `title={error.code}` 已清零；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-log-title-1778242330553`，P1 页面连接、后台 P2 入座并 seed `basic-play`，事件日志显示“载入测试状态”，`.event-log strong[title]` / `.room-log-list strong[title]` 数量为 0，正文不含 `DEV_SCENARIO_SEEDED`、`MATCH_STARTED`、`ROOM_FULL` 或 `UNSUPPORTED_COMMAND`，过滤非应用扩展噪声后应用 error 0。smoke 后已 finalize Chrome 标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十三批补充：官方起手调整候选 tooltip 继续去内部英文 reason / 对象 ID fallback。服务端 `MULLIGAN` source choice reason 从 `opening hand mulligan candidate` 改为“起手调整候选”，前端起手候选按钮在缺少 reason 时也只显示“服务端起手候选”，不再用 `choice.id` 作为浏览器 tooltip。前端仍按服务端候选 source id 提交 `MULLIGAN`，不在浏览器侧裁决可调度手牌。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub"` 1/1 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-mulligan-title-1778242041908`，P1 页面连接、后台 P2 入座并推动双方正式提交卡组/准备到起手调整；最终 P2 视角显示 4 个起手候选，候选 tooltip 全为“起手调整候选”，页面正文和 title 均不含 `opening hand mulligan candidate`、`P1-MAIN`、`P1-HAND`、`P2-MAIN` 或 `P2-HAND`，过滤非应用扩展噪声后应用 error 0。smoke 后已 finalize Chrome 标签并清理 API/Vite，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十二批补充：前端共享 formatter 的未知协议 fallback 继续中文化。`promptActionLabel`、阶段、窗口、房间状态、服务端状态、就绪状态与证据状态等展示函数现在对未识别的全大写/下划线/冒号协议 token 或连字符枚举键降级为“服务端操作 / 服务端阶段 / 服务端窗口 / 服务端状态 / 服务端证据”等中文占位；普通中文或自然文本仍按原文显示。该批只改展示 fallback，前端仍只读取服务端 snapshot/prompt/event，不新增任何规则裁决。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过，包含 event label 覆盖检查、TypeScript build 与 Vite production build。本批没有新增服务端规则代码或可触发未知 action 的页面场景，未启动 API/Vite/Chrome smoke；5092/5093/5094/5175/5176/9223/9224 保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十一批补充：服务端禁用行动候选原因继续去 raw action token。`ActionPromptBuilder.DisabledReasonFor` 现在复用服务端中文行动名，缺少合法来源/目标/费用候选时显示“横置符文 当前没有服务端可执行候选”“回收符文 当前没有服务端可执行候选”，不再把 `TAP_RUNE`、`RECYCLE_RUNE` 或其他全大写协议 action 拼入玩家可见 reason；未知协议 action 降级为“服务端操作”。前端仍只展示服务端下发的候选和 reason，不在浏览器侧自行裁决可执行性。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~ActionPromptHidesRuneSourcesWhenRuneHasNoCardNo"` 1/1 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 75/75 通过。本批为服务端 prompt 文案契约与测试收口，没有前端 UI 代码变更，未启动新的 API/Vite/Chrome smoke；5092/5093/5094/5175/5176/9223/9224 保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百二十批补充：前端结算链摘要继续去 destination 协议值。`StackPanel` 现在把 stack item 的 `destination` 显示为“战场 / 基地 / 结算链 / 待命 / 废牌堆 / 放逐区 / 服务端区域”，不再把 `BATTLEFIELD:P1-MAIN`、`STACK` 等 raw destination token 放进玩家正文；该批只改 UI 展示，服务端 stack item 与命令契约不变。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-zone-label-field-1778240000004`，P1 页面连接后由后台 SignalR 让 P2 入座、seed `basic-play`，并由 P1 提交服务端候选支持的 `PLAY_CARD` 到 `BATTLEFIELD:P1-MAIN`；结算链摘要显示“去向：战场”，页面正文不含 `BATTLEFIELD:P1-MAIN` 或 raw `BATTLEFIELD`。Chrome 只记录扩展脚本 autoplay `NotAllowedError` 噪声，过滤非应用 extension 日志后应用 runtime error 0；smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十九批补充：前端资源条继续去协议键展示。`runePoolText` 现在把 `powerByTrait` 中的 `red/green/blue/yellow/purple` 渲染为“红色符能 / 绿色符能 / 蓝色符能 / 黄色符能 / 紫色符能”，未知 trait 降级为“服务端符能”，不再把 `red:2` 这类协议键作为玩家正文；该批只改 UI formatter，服务端权威 rune pool/snapshot 不变。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-rune-trait-label-1778240000001`，P1 页面连接后由后台 SignalR 让 P2 入座并 seed `typed-power-payment`；P1 资源条显示“法力 1 / 符能 2 / 红色符能 2”，页面正文不含 `red:2`、`red : 2` 或 `red 2`。Chrome 只记录扩展脚本 autoplay `NotAllowedError` 噪声，过滤非应用 extension 日志后应用 runtime error 0；smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十八批补充：前端规则队列继续去内部 cleanup task kind。`StackPanel` 现在把 authoritative snapshot 中的 `RECALL_UNATTACHED_EQUIPMENT` 显示为“装备清理”，未知全大写任务名统一降级为“服务端任务”，不再把 raw task kind 展示给玩家；`MatchSession` 新增 development-only `battlefield-unattached-equipment-cleanup` seed，用真实服务端 pending task 复现未贴附装备清理阻塞，前端仍只读 snapshot/prompt，不在浏览器侧裁决清理。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~SeedScenarioBroadcastsUnattachedEquipmentCleanupTask|FullyQualifiedName~SeedScenarioBroadcastsIllegalStandbyCleanupTask|FullyQualifiedName~PendingTaskQueueExposesUnattachedBattlefieldEquipmentCleanupAsStateBasedTask"` 3/3 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~GameHubJoinTests"` 120/120 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-unattached-equipment-1778239965401`，P2 页面连接后由后台 SignalR 让 P1/P2 seed `battlefield-unattached-equipment-cleanup`，规则队列显示“阶段：状态清理 / 活动任务：处理中 / 装备清理：装备脱离清理”，行动提示原因为“等待服务端处理任务队列：装备清理”；页面正文不含 `RECALL_UNATTACHED_EQUIPMENT`、`UNATTACHED_EQUIPMENT_CLEANUP`、`cleanup:unattached-equipment` 或未贴附装备/战场对象 ID。Chrome 只记录扩展脚本 autoplay `NotAllowedError` 噪声，过滤非应用 extension 日志后应用 runtime error 0；smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十七批补充：pending cleanup task 的阻塞 prompt/error 继续去内部任务名。服务端 `BlockingPendingTaskQueueReason` 现在覆盖 `RECALL_UNATTACHED_EQUIPMENT`，玩家可见文本显示“装备清理”；致命伤害、0 战力、待命和未贴附装备清理的 prompt/error 回归均改为断言中文原因，并禁止 `DESTROY_LETHAL_UNIT`、`DESTROY_ZERO_POWER_UNIT`、`REMOVE_ILLEGAL_STANDBY`、`RECALL_UNATTACHED_EQUIPMENT` 出现在玩家可见阻塞文案中。authoritative snapshot 仍保留 task kind/id 作为结构化状态来源，前端只读展示。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore --no-incremental` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 75/75 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~OfficialOpeningTests"` 9/9 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-build --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过。本批没有前端 UI 交互变更，没有启动新的 API/Vite/Chrome smoke 进程；5092/5093/5094/5175/5176/9223/9224 保持无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十六批补充：前端开发场景事件日志继续去 fixture/debug 文本。`DEV_SCENARIO_SEEDED` 的玩家可见标题改为“载入测试状态”，描述固定为“测试状态已载入”，不再把 `basic-play`、`battlefield-*` 等 development-only seed 名称显示给玩家；事件本体仍来自服务端，不改变 seed、snapshot 或规则裁决。
- 已补验证：本批无服务端规则代码变更；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-scenario-redaction-1778238780476`，P1 连接页面、P2 后台 SignalR seed `basic-play` 后，事件日志显示“载入测试状态 / 测试状态已载入”，页面正文不含 `basic-play`、`DEV_SCENARIO_SEEDED`、`开发测试场景已载入`、`SeedScenario` 或 `scenarioId`，应用自身 runtime error 0。随后只读确认 Codex Chrome Extension 可通信，并已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十五批补充：前端卡牌详情操作 composer 继续去内部 fallback。缺失服务端展示名或能力/费用 label 时，前端不再把 objectId、abilityId 或 raw cost id 当作玩家正文，而是显示通用中文占位；真实提交参数仍只取服务端候选 id。
- 已补验证：本批无服务端规则代码变更；静态扫描确认 `CardDetailDrawer` 不再有 `|| sourceObjectId`、`|| abilityId`、`|| origin`、`|| mode` 或 `?? cost` fallback；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-detail-fallback-1778238527789`，P1/P2 后台 seed `basic-play` 后页面与卡牌详情 smoke 正文不含内部对象、结算链或 raw 操作/费用 token，应用自身 runtime error 0。smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十四批补充：前端服务端错误展示继续去 raw protocol code。房间页和对战日志使用中文错误标题/说明展示服务端 ErrorDto，raw code 仅保留在 title；预期 Join/Reconnect 拒绝不再冒泡成浏览器未处理 Promise。该批只改产品展示和错误处理，不改变服务端裁决或 ErrorDto 协议。
- 已补验证：本批无服务端规则代码变更；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-error-redaction-1778238256298`，P1/P2 后台 SignalR 占满房间后，P3 在房间页连接得到中文“房间已满 / 该房间已经有两名玩家。”，正文不含 `ROOM_FULL`、`room already has two players`、`UNSUPPORTED_COMMAND` 或 `PHASE_NOT_ALLOWED`，应用自身 runtime error 0。smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十三批补充：任务队列提示原因继续去内部标识。服务端 `BlockingPendingTaskQueueReason` 改为中文任务名，不再把 raw task kind/taskId 下发到 `ActionPrompt.reason` 的玩家可见文本；前端规则队列也不再展示 `activeTaskId` 或 raw task reason，而是显示“处理中”和中文任务原因。authoritative snapshot 内仍保留任务 ID 作为客户端状态来源，前端不新增规则裁决。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`SeedScenarioBroadcastsIllegalStandbyCleanupTask|P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass` 精确回归 2/2 通过；`GameHubJoinTests` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-task-redaction-1778237723284`，P1 页面与 P2 后台 SignalR seed `battlefield-illegal-standby` 后，规则队列只显示中文“状态清理 / 活动任务：处理中 / 待命清理：战场控制清理”，行动提示原因为“等待服务端处理任务队列：待命清理”；页面正文不含 raw cleanup/task/object ID 或 raw task reason，应用自身 runtime error 0。smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十二批补充：服务端行动提示候选标签继续去内部标识。`MatchSession` 构造 object/stack choice label 时不再向玩家可见文本拼接对象 ID 或结算链 ID；公开对象显示服务端卡号，缺失卡号时显示通用中文 fallback。候选 `id` 仍作为服务端提交参数保留在协议内，前端继续只提交当前 enabled candidate 支持的对象，不在浏览器侧裁决规则。
- 已补验证：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 精确回归通过；`GameHubJoinTests` 119/119 通过；`ConformanceFixtureShapeTests` 75/75 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-official-redaction-1778237277600`，P1 页面与 P2 后台 SignalR 完成提交卡组、准备、起手调整，并推进到第 1 回合主阶段；P1 UI 不含 `P1-MAIN...`、`P2-MAIN...` 或 `opening hand mulligan candidate`，应用自身 runtime error 0。smoke 后已清理临时连接、测试标签和 API/Vite 进程，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十一批补充：首页/设置页玩家可见接口路径继续中文化。`HomePage` 不再显示 `/catalog/behavior-specs`，`SettingsPage` 不再显示 `/health`；页面仍读取相同服务端接口和 authoritative evidence，只把产品文案收口到中文口径。
- 已补验证：本批无服务端规则代码变更；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 依次打开 `/settings` 和 `/`，设置 `serverUrl=http://127.0.0.1:5093`、`playerId=P1` 后，设置页显示“正在读取服务端健康状态 / 服务端健康”，首页显示“图鉴状态来自服务端卡牌证据”，页面正文不含 `/health`、`/catalog/behavior-specs`、`SignalR`、`JoinRoom`、`ActionPrompt`、`SUBMIT_DECK`、`REST`、`BehaviorSpec` 或 `Deferred family`，应用自身 runtime error 0。smoke 后已清理 API/Vite/Chrome 测试标签，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百一十批补充：大厅页玩家可见协议词继续中文化。`LobbyPage` 不再把 `SignalR` / `JoinRoom` 暴露给玩家，改为“服务端实时连接”口径；房间创建、入座、卡组提交和准备逻辑不变，前端仍只展示并提交服务端 prompt/candidate 支持的操作。
- 已补验证：本批无服务端规则代码变更；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 打开 `http://127.0.0.1:5175/lobby`，页面显示“房间由服务端实时连接创建”，正文不含 `SignalR`、`JoinRoom`、`ActionPrompt`、`SUBMIT_DECK`、`REST`、`NOT READY`、`BehaviorSpec` 或 `Deferred family`，应用自身 runtime error 0。smoke 后已清理 API/Vite/Chrome 测试标签，5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- 第二百零九批补充：新增当前 completion audit 文档。`docs/CURRENT_COMPLETION_AUDIT.md` 按补充任务要求列出修改/新增文件、关键架构、服务端规则补齐项、前端页面完成项、接口契约、隐藏信息检查、测试与 Browser smoke/E2E 门禁，并明确当前仍存在 P0/P1 阻断，active goal 不可标记 complete。
- 已补验证：本批仅修改文档，无源码或规则实现变更；`git diff --check` 已执行通过。Chrome 插件连通性已只读确认可用，未启动 API/Vite/业务 browser smoke 进程。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- 第二百零八批补充：项目入口文档口径与当前复审状态对齐。`README.md` 与 `docs/START_HERE.md` 现在把当前入口指向 `CURRENT_FRONTEND_REBUILD_PLAN`、`CURRENT_SERVER_RULE_AUDIT` 和 `任务补充.md`，明确 P7/P7.9 文档只是历史阶段基线；同时把玩家可见协议词改为“服务端行动提示 / 服务端快照 / 事件”等中文口径，避免新窗口把历史 `CONFORMANCE_PASS` 当作最终 READY。
- 已补验证：本批仅修改文档，无源码或规则实现变更；`git diff --check` 已在提交前执行通过。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零七批补充：卡面与卡牌详情继续收口内部对象标识 fallback。`CardFace` 与 `CardDetailDrawer` 在公开卡缺少 `cardNo/spec` 的异常恢复状态下不再把 `objectId` 当作标题或编号展示，而是显示“未知卡牌 / 无编号”；隐藏卡仍显示“未公开卡牌 / 隐藏信息”。该批只改展示 fallback，不改变 authoritative snapshot、prompt 候选或命令提交。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-object-redaction-1778235759932`，P1 连接对战页、P2 后台连接并 seed `basic-play`，页面和隐藏卡详情均显示“未公开卡牌 / 隐藏信息”，正文不含 `P1-...`、`P2-...`、`STACK-...`、`hidden-...`、`task-...` 或 `cleanup-...` 内部标识，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零六批补充：对战桌面无 authoritative snapshot 时的顶栏空状态继续产品化。`MatchTopBar` 不再显示“第 0 回合｜｜”这种由缺失 snapshot 拼出的空白阶段/窗口，而是显示“等待服务端快照”；已有 snapshot 时仍只渲染服务端回合、phase 与 timingState 的中文映射，不新增前端规则裁决。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 打开 `/settings` 设置 `serverUrl=http://127.0.0.1:5093` 与 `playerId=P2` 后进入 `/matches/copy-smoke`，页面显示“等待服务端快照 / 提示状态”，不含“第 0 回合｜｜”或 raw `ActionPrompt`、`snapshot`、`prompt`、`REST`、`SUBMIT_DECK`、`房间/Match`、`API 健康`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零五批补充：玩家可见协议文案继续中文化。首页/导航/卡组/房间/对战/结算/行动面板/卡牌详情中的 `ActionPrompt`、`snapshot`、`prompt`、`REST`、`SUBMIT_DECK`、`房间/Match`、`API 健康` 等协议词改为“服务端行动提示 / 快照 / 接口 / 提交卡组 / 房间/对局 / 服务端健康”等中文文案；行动面板不再正文显示 raw prompt id，只显示提示状态。该批只改展示文本，不改变服务端 prompt/candidate 读取或命令提交。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 串行重跑通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 依次打开首页、`/decks`、`/settings`、`/matches/copy-smoke`，正文显示“服务端行动提示 / 提示状态 / 卡组保存接口 / 房间/对局 / 重新同步快照”，不含 raw `ActionPrompt`、`snapshot`、`prompt`、`REST`、`SUBMIT_DECK`、`房间/Match` 或 `API 健康`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零四批补充：首页、图鉴和设置诊断继续中文化。`HomePage` 不再裸显 `NOT READY`，图鉴页不再显示 `BehaviorSpec` / `Deferred family` / `deferred/representative`，设置页将 `/health` 的真实 `status/service/role` 与关键词覆盖 `statusCounts` 枚举映射为中文（如“正常 / 符文战场服务端 / 规则迁移模式 / 已识别待补”），本地身份胶囊也从 `localStorage` 改为“本地存储”。前端仍只展示服务端证据状态，不隐藏 NOT READY 结论。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 单独重跑通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 依次打开首页、`/cards`、`/settings`，设置 `serverUrl=http://127.0.0.1:5093` 后可见“尚未就绪 / 待补关键词族 / 服务端行为规格 / 正常 / 符文战场服务端 / 规则迁移模式 / 本地存储”，正文不含 raw `NOT READY`、`Deferred family`、`BehaviorSpec`、`localStorage`、`riftbound-dotnet`、`migration-skeleton`、`implemented-representative`、`recognized-deferred` 或 `recognized-delegated`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零三批补充：前端官方规则文本去模板语法。`CardFace` 与 `CardDetailDrawer` 现在通过共享 `rulesText` formatter 展示 `officialText`，把官方数据中的 `{{S}}`、`{{A}}`、`{{红色}}`、`{{横置}}`、`{{反应>}}` 等模板 token 转为玩家可读中文文本，避免卡面/详情裸显 `{{...}}`。该 formatter 只影响 UI 文本，不改变服务端卡牌数据、规则裁决、prompt 或命令提交。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 打开 `/cards` 并进入首张卡详情，图鉴正文与详情正文均不含 `{{` 或 `}}`，规则文本显示“游走（我可以向其他战场进行移动。）”“战力+1”等可读中文 token，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零二批补充：卡牌详情抽屉继续去内部调试字段。公开卡牌详情不再显示“对象 ID”、raw `CARD_TYPE:*` 标签、raw location zone、贴附目标对象 ID、英文 conformance reason 或 raw effect/template 标识；位置改为中文区域（如 `P1 / 手牌`），服务端证据只保留中文完成度说明。该变更只影响展示层，不改变 prompt/candidate 组合或命令提交。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-card-detail-redaction-1778234066612`，P1 连接对战页，P2 外部连接并 seed `basic-play`，打开公开手牌详情后可见中文位置和中文服务端证据，正文不含“对象 ID”、`CARD_TYPE:`、raw `BATTLEFIELD/BASE/HAND` zone、`P1-...-001` 对象 ID 模式、raw effect id 或英文 representative reason，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百零一批补充：对战事件日志详细模式继续去调试化。`EventLog` 的 detailed density 不再额外渲染 `event.kind` raw 枚举，只保留中文事件标题和经过 `eventDescriptionLabel` 脱敏后的服务端描述；raw kind 仍仅作为标题 tooltip 供开发排查，不作为产品正文展示。前端仍只读服务端 events/snapshot/prompt，不从日志文本自行裁决规则。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-detailed-log-1778233665607`，P1 连接对战页并切到详细日志后，由 P2 外部连接并 seed `basic-play`，页面显示“载入开发场景”等中文日志，正文不含 raw `DEV_SCENARIO_SEEDED` 或 `MATCH_STARTED`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第二百批补充：对战顶栏 phase/timingState 继续中文化。`MatchTopBar` 现在通过共享 `matchPhaseLabel` / `timingStateLabel` 显示服务端阶段与窗口，主 HUD 会显示“房间阶段 / 起手调整 / 回合开始 / 主阶段 / 回合结束”和“普通开环 / 普通闭环 / 法术对决开环 / 法术对决闭环”等中文文案，不再裸显 `MAIN`、`NEUTRAL_OPEN` 等服务端枚举；前端仍只读 authoritative snapshot。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-topbar-label-1778233450965`，外部连接 P1/P2 并 seed `basic-play` 后，P1 对战桌面顶栏显示“第 690 回合｜主阶段｜普通开环”，顶栏不含 raw `MAIN` 或 `NEUTRAL_OPEN`，应用 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百九十九批补充：结算页房间状态枚举中文化。`ResultPage` 现在通过共享 `roomStatusLabel` / `roomStatusTone` 显示 `EMPTY / SEATING / IN_PROGRESS / FINISHED`，页面主状态与详情卡片显示“空房间 / 等待入座 / 对局进行中 / 对局已结束”，不再把 `FINISHED` 等服务端 raw roomStatus 作为玩家可见主文案；“winningScore” 文案同步改为“胜利分数”。前端仍只读取 authoritative snapshot，不自行推断胜负。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~Surrender"` 121/121 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-result-status-1778233263409`，外部连接 P1/P2、seed `basic-play` 后 P1 `SURRENDER`，打开 `/matches/{roomId}/result` 可见“胜者：P2 / 对局已结束 / 胜利分数”，页面正文不含 raw `FINISHED`，应用 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百九十八批补充：房间页连接状态继续中文化并与对战桌面统一。`connectionStatusLabel` / `connectionStatusTone` 已抽到共享 formatter，`RoomPage`、`MatchTopBar` 和 `ActionPanel` 共用同一套连接状态文案与色调；房间页不再把 `connected` 等 raw 状态枚举作为玩家可见主文案。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-status-label-1778233017487`，房间页连接前显示“未连接”，点击“连接/重连并入座”后显示“已连接”，页面正文不含 raw `connected`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-005/P1-004 第一百九十七批补充：前端服务端日志描述增加展示级内部 ID 脱敏。`EventLog` 与 `RoomPage` 现在共用 `eventDescriptionLabel`，会把日志描述中的 `P1-...` / `P2-...` 对象 ID、`STACK-...` 结算链 ID、`hidden-...` 占位和 `task:`/`cleanup:` 服务端任务 ID 显示为“对象 / 结算链项目 / 隐藏对象 / 服务端任务”。这只影响 UI 文案，不修改 authoritative snapshot、payload、prompt 或命令提交；前端仍不从日志描述裁决规则。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-event-redaction-1778232667598`，P1 连接房间页后外部连接 P2、seed `legend-act` 并提交 `LEGEND_ACT`，房间日志显示“传奇横置 / 对象 横置”，页面正文不含 `P1-LEGEND-POPPY` 或 `STACK-...`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第一百九十六批补充：房间页服务端日志继续产品化收口。`RoomPage` 现在复用对战桌面的事件 kind 中文标签，房间日志主文案显示“载入开发场景”等服务端事件中文名，不再把 `DEV_SCENARIO_SEEDED` 这类 raw event kind 直接作为玩家可见标题。前端仍只展示服务端 events/snapshot/prompt，不新增客户端规则裁决；raw kind 仅保留在标题 tooltip 供开发排查。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 119/119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-room-event-label-1778232358005`，P1 连接房间页后由 P2 外部连接并 `SeedScenario("basic-play")`，房间日志显示“载入开发场景”，页面正文不含 `DEV_SCENARIO_SEEDED`，应用自身 runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-005 第一百九十五批补充：前端结算链面板不再裸显 raw stack JSON。`StackPanel` 将服务端 stack item 渲染为中文摘要，只显示控制者、公开卡号/服务端技能、中文化效果类型、目标数量和公开去向；不再把 `sourceObjectId`、`targetObjectIds`、`stackItemId` 等内部字段或 raw JSON 直接暴露在产品 UI 中。当前已为 `HEXTECH_RAY_DAMAGE_3` 补中文标签，未知 effectKind 只显示通用“服务端效果”。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-stack-summary-1778231855451`，P1 连接 spell-duel 场景后，规则队列显示“项目 1 / 控制者：P1 / 来源：OGN·009/298 / 类型：海克斯射线伤害 / 目标：1 个”，不显示 raw JSON、`sourceObjectId`、`targetObjectIds` 或 `HEXTECH_RAY_DAMAGE_3`，app runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-005 第一百九十四批补充：前端隐藏信息牌面继续收紧。`CardFace` 对未公开对象不再把 `objectId` 渲染到卡背上，只显示“未公开 / 卡背 / 隐藏信息”；隐藏卡牌详情抽屉的编号状态也改为通用“未公开”，战场对象缺失占位不再回落显示原始对象 ID。服务端 snapshot 仍可携带权威对象标识供内部状态和命令使用，但产品 UI 不把这些调试标识展示给无权玩家。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-hidden-object-labels-1778231623362`，P1 设置 `serverUrl=http://127.0.0.1:5093` 后连接对战页，页面可见文本不含 `P2-LEGEND-001`、`P2-CHAMPION-001` 或 `hidden-0` 这类隐藏对象标识，仍显示通用“隐藏信息”；点击对手未公开传奇后，详情抽屉显示“未公开卡牌 / 隐藏信息 / 未公开”，不显示对象 ID，app runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第一百九十三批补充：房间页按钮区继续与“只展示可提交且当前页面能提交的操作”对齐。`RoomPromptButtons` 现在只渲染 enabled 的 `SUBMIT_DECK` / `READY` 房间级候选；如果服务端当前 prompt 已进入对战动作（例如 `MOVE_UNIT`、`END_TURN`、`SURRENDER`），房间页只显示“其他可提交行动请进入对战桌面”，不再把这些动作渲染成 disabled ghost 按钮。房间页底部仍可只读汇总服务端 enabled candidate，帮助玩家理解状态但不代替对战桌面提交。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过。后台 headless Chrome/CDP smoke 使用房间 `smoke-room-buttons-1778231212969`，P1 设置 `serverUrl=http://127.0.0.1:5093` 后连接房间页，房间按钮列表只有“连接/重连并入座”和“进入对战桌面”，提示“其他可提交行动请进入对战桌面”，未出现“移动单位/结束回合/投降”按钮，也未出现 disabled 的“打出卡牌（需选择）”或“声明战斗（需选择）”；“当前可提交行动”摘要仍显示“移动单位、结束回合、投降”，browser runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第一百九十二批补充：继续收紧前端“可提交操作”展示面。`ActionPanel` 现在只渲染服务端 `candidates[].enabled == true` 的候选，避免把 disabled 的 `PLAY_CARD`、`DECLARE_BATTLE` 等宽泛候选显示成“需选择”的操作按钮；卡牌详情抽屉也改为“服务端可提交操作”，并只列出 enabled source candidate。前端仍不从卡面、`prompt.actions` 或 disabled candidate 自行裁决可玩入口。
- 已补验证：本批无服务端规则代码变更；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-card-detail-actions-1778230564156`，P1 设置 `serverUrl=http://127.0.0.1:5093` 后连接对战页，主操作面板只显示“移动单位、结束回合、投降”，不显示 disabled 的“打出卡牌（需选择）”或“声明战斗（需选择）”；点击 P1 战场单位后，详情抽屉显示“服务端可提交操作”和“移动单位”，不再显示旧标题“可执行操作”，app runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第一百九十一批补充：收紧 JoinRoom/RequestSnapshot 的普通开环 prompt 口径，移除旧占位 `PASS`。此前实时 Core prompt 已只公开产品支持的 `MOVE_UNIT`、`END_TURN`、`SURRENDER` 等动作，但重连/房间页从 `ResolutionResult.BuildPrompts` 取 prompt 时仍会把旧 `PASS` 作为 enabled candidate 暴露；现在服务端重连 prompt 与实时 prompt 对齐，避免前端在恢复/刷新后出现“让过”这种非当前产品路径的可提交入口。
- 已补验证：同步更新 legacy Java/placeholder fixture 的 `expected.promptActions`，保留历史 oracle 原文但让当前 expected 契约不再要求普通开环 `PASS`；`RequestSnapshot|GameHubJoinTests|CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant` 相关回归 120/120 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3139/3139 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-room-candidate-label-1778230235113`，P1 重连房间页显示“当前可提交行动：移动单位、结束回合、投降”，不再出现“让过”或“声明战斗”，app runtime error 0。整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第一百九十批补充：补齐 `START_BATTLE` active task 的服务端命令护栏测试。当前战场战斗任务激活时，服务端只允许 `ActivePlayerId` 按该 active task 的战场与参战对象提交 `DECLARE_BATTLE`；非当前玩家即使手写同名命令也只能得到 task queue 阻塞错误，当前玩家若把命令切到另一个战场也会被拒绝为“必须匹配 active START_BATTLE task”。这保证前端之外的客户端不能绕过 UI 只读候选去推进错误战斗任务。
- 已补测试与验证：新增 `CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask` 与 `CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask`，覆盖非 active 玩家、错误战场、prompt 保持 P1 可声明/P2 等待、tick/events 不变和 pending queue 仍停留在 `BATTLE_TASKS`。验证结果：active battle task 精确回归 3/3 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3139/3139 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签覆盖 110 个后端 event kind。本批没有前端 UI 代码变更，没有启动业务 Chrome smoke；整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第一百八十九批补充：为法术对决关闭后 cleanup 取消后续战斗的真实前端路径补 development seed 与 Hub 回归。新增 `battlefield-contest-spell-duel-cleanup` 场景，直接进入 P2 焦点、P1 已让过且队列仍含 `START_BATTLE` 的争夺战场法术对决；P2 让过焦点后服务端广播 `FOCUS_PASSED`、`SPELL_DUEL_CLOSED`、`UNIT_DESTROYED`，把 P2 致命单位移入墓地，并把战场收敛为 P1 控制、非争夺、无待命，后续 `START_BATTLE`/可提交 `DECLARE_BATTLE` 不再暴露。
- 已补测试与验证：新增 `P6BattlefieldContestSpellDuelCleanupSeedSkipsBattleAfterFocusPass`，覆盖 development seed、Hub 实时 prompt、`RequestSnapshot` 重连候选、墓地/objectLocations 与 battlefield controller/occupant 收敛；`DECLARE_BATTLE` 不允许成为 enabled candidate 或 UI 可提交动作。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；精确回归 2/2 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3137/3137 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件 smoke 使用房间 `smoke-spell-duel-cleanup-1778228927718`，P2 前端设置 `serverUrl=http://127.0.0.1:5093`、`playerId=P2` 后连接，P1 通过 SignalR JoinRoom 并 seed；P2 点击“让过焦点”后 UI 显示“法术对决关闭 / 单位摧毁”，未显示“声明战斗”，reload/reconnect 后仍恢复 P2 单位在墓地、战场 P1 控制、队列 `IDLE`、app runtime error 0。本批结束后仍已清理 API/Vite/Chrome smoke 进程；整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第一百八十八批补充：法术对决关闭后的状态性清理会重新决定战场是否仍待发生战斗。`PASS_FOCUS` 现在从当前 pending tasks 中识别战场 `START_SPELL_DUEL`，不再依赖它恰好是 active task；因此当同一时刻状态性 cleanup 先成为 active task 时，服务端仍会记录该战场法术对决已完成。若 cleanup 随后摧毁并移走一方参战单位，authoritative `BattlefieldStates` 会变为非争夺，`PendingTaskQueue` 不再公开 `START_BATTLE`，P1 prompt 只保留服务端合法的 `MOVE_UNIT`、`END_TURN`、`SURRENDER`，不会继续暴露 `DECLARE_BATTLE`。
- 已补测试与验证：新增 `CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant`，覆盖法术对决关闭事件、致命清理事件、墓地/objectLocations 同步、battlefield occupant/controller 收敛、`START_BATTLE`/`DECLARE_BATTLE` 消失和合法 prompt 收敛。验证结果：关闭/争夺/清理相关精确回归 3/3 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3136/3136 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签覆盖 110 个后端 event kind。本批没有启动 API/Vite/业务 Browser/Chrome smoke，因为没有前端 UI 交互变更；整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第一百八十七批补充：法术对决焦点窗口关闭后补执行状态性清理。`PASS_FOCUS` 在双方均已让过并广播 `SPELL_DUEL_CLOSED` 后，会进入同一 `RunStateBasedCleanupLoop`，把法术对决期间累积的致命伤害、0 战力、非法待命、未贴附装备等状态性清理落到服务端 authoritative zones/objectLocations/events；前端仍只展示服务端事件和最终 prompt，不自行判断法术对决结束后的清理。
- 已补测试与验证：新增 `CoreRuleEngineRunsStateBasedCleanupAfterSpellDuelCloses`，覆盖法术对决中已有致命伤害单位时，焦点让过关闭窗口后事件顺序为 `FOCUS_PASSED`、`SPELL_DUEL_CLOSED`、`UNIT_DESTROYED`，单位入墓、`ObjectLocations` 同步、`DestroyedUnitOwnerIdsThisTurn` 记录且 prompt 回到 `END_TURN`。验证结果：关闭/争夺相关精确回归 3/3 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3135/3135 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签覆盖 110 个后端 event kind。本批没有启动 API/Vite/业务 Browser/Chrome smoke；整体仍 **NOT READY**，因为完整 battle task 自动化、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003 第一百八十六批补充：未贴附装备状态性清理继续覆盖 legacy/fixture 恢复态。现代 authoritative state 仍要求 `ObjectLocations` 明确战场对象后才公开 blocking `RECALL_UNATTACHED_EQUIPMENT` pending task，避免旧测试状态在行动前被意外抢占；但实际 cleanup loop 现在会从 `PlayerZones.Battlefields` 回落发现缺索引的未贴附装备，在宿主离场、圣裁之刻保留战场装备等栈后清理点把它们召回 effective controller 基地并广播 `EQUIPMENT_RECALLED_TO_BASE`。
- 已补测试与验证：更新 `p5-equipment-detaches-when-host-destroyed`，宿主被摧毁后装备先脱离，再由 cleanup 召回基地；更新 `p2-preflight-play-judgment-day-recycle-unkept`，保留在战场的未贴附装备在回收结算后的 cleanup 中召回基地。验证结果：精确/相邻回归 15/15 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3134/3134 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签覆盖 110 个后端 event kind。本批没有启动 API/Vite/业务 Browser/Chrome smoke；整体仍 **NOT READY**，因为 central cleanup queue、完整官方 battle/spell-duel 生命周期、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003 第一百八十五批补充：状态性清理补齐“未贴附装备出现在战场时，下次清理召回控制者基地”的官方规则。服务端现在会公开 `RECALL_UNATTACHED_EQUIPMENT` / `UNATTACHED_EQUIPMENT_CLEANUP` pending task，栈后 cleanup 将未贴附、非待命、正面装备从战场移回其 effective controller 基地，并广播 `EQUIPMENT_RECALLED_TO_BASE`；前端只新增事件日志中文标签“装备召回基地”，继续只读服务端 event/snapshot，不在浏览器侧判断装备是否合法。
- 已补测试与验证：新增 `PendingTaskQueueExposesUnattachedBattlefieldEquipmentCleanupAsStateBasedTask` 与 `P7PostStackCleanupRecallsUnattachedBattlefieldEquipmentToControllerBase`，覆盖 pending task/snapshot 阻塞、手写 `END_TURN` 被拒、栈后自动召回到控制者基地、owner/controller 保持、`ObjectLocations` 同步和事件 payload。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`UnattachedBattlefieldEquipment|UnattachedEquipment` 精确回归 2/2 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3134/3134 通过；`source ../../scripts/dev-env.sh && npm run build` 通过，事件标签覆盖 110 个后端 event kind。本批没有启动 API/Vite/业务 Browser/Chrome smoke；整体仍 **NOT READY**，因为 central cleanup queue、完整官方 battle/spell-duel 生命周期、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-004 第一百八十四批补充：战斗清理召回的所有者基地边界补齐测试证据。临时受控的进攻单位在战斗无结果且防守方仍在战场时，会被 `UNIT_RECALLED_TO_BASE` 召回其 owner 的基地，并保留当前 controller；这与“召回至所属基地且召回本身不改变控制权/状态”的规则口径对齐。
- 已补测试与验证：新增 `P4BattleCleanupRecallsTemporarilyControlledAttackerToOwnerBase`，覆盖 P2 拥有、P1 控制的眩晕进攻单位被战斗清理召回 P2 基地，同时 `ControllerId` 仍为 P1、`ObjectLocations` 同步为 P2/BASE。验证结果：目标回归 1/1 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3132/3132 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 文件变更，没有启动 API/Vite/业务 Browser smoke；整体仍 **NOT READY**，因为完整官方 battle task、spell duel 生命周期、PaymentEngine、LayerEngine、central cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百八十三批补充：战斗清理 `DAMAGE_REMOVED` 事件继续补齐回放/日志可解释性。战斗特殊清理现在会在 payload 中写入 `previousDamageByObject` 和 `totalDamageRemoved`，让前端日志、战报和 replay 可以只读服务端事件展示“从多少伤害清到 0”，不需要浏览器侧根据旧 snapshot 反推。
- 已补测试与验证：扩展战斗代表路径、眩晕进攻者、狩猎征服/据守、壁垒/后排、同优先级壁垒和 Hub seed 断言，并同步 fixture 的 `DAMAGE_REMOVED` payload。验证结果：`DeclareBattleCommand|GameHubJoinTests` 相邻回归 174/174 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 文件变更，没有启动 API/Vite/业务 Browser smoke；整体仍 **NOT READY**，因为完整官方 battle task、spell duel 生命周期、PaymentEngine、LayerEngine、central cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百八十二批补充：最近战斗结果 snapshot 继续补齐战斗清理证据。`BattleResolutions.relatedEventKinds` 现在会记录 `DAMAGE_REMOVED`，并在双方仍有单位的无结果代表路径中记录 `UNIT_RECALLED_TO_BASE`；前端规则队列、战报与 reload/reconnect 后的 snapshot 不需要从即时事件流或本地状态反推战斗清理是否发生。
- 已补测试与验证：扩展 `P4DeclareBattleCommandStunnedAttackerContributesZeroCombatPower` 和 `P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath`，断言权威 `BattleResolutions` 与 snapshot `timing.battleResolutions[].relatedEventKinds` 均保留战斗清理事件。验证结果：目标回归 2/2、`DeclareBattleCommand|BattleResolution` 相邻回归 56/56 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 文件变更，没有启动 API/Vite/业务 Browser smoke；整体仍 **NOT READY**，因为完整官方 battle task、spell duel 生命周期、PaymentEngine、LayerEngine、central cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百八十一批补充：`DECLARE_BATTLE` 代表路径补齐战斗结算中的战斗特殊清理。服务端在战斗伤害与致命清理后，会对仍存活的参战单位广播 `DAMAGE_REMOVED` 并把伤害清为 0；若防守方仍有单位留在该战场，则把仍位于该战场的进攻单位召回其基地并广播 `UNIT_RECALLED_TO_BASE`。前端继续只展示服务端事件和 authoritative snapshot，不在浏览器侧自行判断战斗清理、召回或最终伤害状态。
- 已补测试与验证：扩展单攻单防、狩猎征服/据守、壁垒/后排、多攻击者、多攻防、眩晕进攻者、静态战斗修正、战场修正和 Hub seed 断言，fixture 期望同步加入 `DAMAGE_REMOVED` 事件与最终 0 伤害。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`DeclareBattleCommand|BattlefieldControl` 相邻回归 61/61、用户指定待命/控制目标回归 3/3、用户指定战场 seed 回归 2/2、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Chrome 插件只读连通性检查成功并已清理 agent 标签页；本批没有前端 UI 文件变更，没有启动 API/Vite/业务 Browser smoke。整体仍 **NOT READY**，因为完整官方 battle task、spell duel 生命周期、PaymentEngine、LayerEngine、central cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百八十批补充：据守狩猎的事件 payload 继续补齐回放/日志证据。`BATTLEFIELD_HELD` 在由幸存防守狩猎单位触发经验时，现在会携带 `huntAmount`、`huntSourceObjectIds` 和 `huntAmountsBySource`，与征服路径的狩猎来源证据对齐；`EXPERIENCE_GAINED` 仍以实际幸存防守狩猎单位作为来源。既有非狩猎据守事件保持原 payload，不引入新的前端裁决。
- 已补测试与验证：扩展 `P4DeclareBattleCommandGrantsHuntExperienceWhenDefenderHoldsBattlefield`，断言 `BATTLEFIELD_HELD` payload 中的狩猎来源和数值。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；狩猎征服/据守精确回归 3/3、`DeclareBattleCommand|Hunt|BattlefieldHeld` 相邻回归 104/104、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P1-002/P1-003 第一百七十九批补充：狩猎关键词的证据口径与上一批服务端能力对齐。`CardResourceKeywordRules` 的 resource keyword profile reason 不再只写 “Hunt conquest experience”，而是明确为 “Hunt conquest/held battle experience”；`p2-preflight-play-spring-messenger-experience-static` 与 `p4-declare-battle-hunt-conquest-experience` 的 fixture 证据说明同步改为征服/据守代表路径已由 `DECLARE_BATTLE` 命令级回归覆盖，完整得分、任务状态机、多攻防选择和其他狩猎触发仍暂缓。`docs/conformance-fixture-format.md` 与 `docs/rules-evidence-index.md` 也同步修正，避免图鉴/审计继续把已覆盖的狩猎据守经验误列为暂缓。
- 已补测试与验证：扩展 `P4ResourceKeywordProfilesMapOfficialTextToRegistryTags` 断言 profile reason 包含 `Hunt conquest/held battle experience`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；resource keyword/profile 精确回归 2/2、经验/关键词 fixture 相关回归 45/45、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百七十八批补充：狩猎关键词的据守路径补齐到服务端权威战斗结算。`DECLARE_BATTLE` 中防守方赢得战斗并据守时，现在会枚举仍在场上的幸存防守单位，合计其中 `狩猎` / `狩猎N` 的经验，并用实际幸存狩猎防守单位作为 `EXPERIENCE_GAINED` 来源；这与上一批征服路径一致，避免防守狩猎单位据守战场却漏给经验。本批未引入新的前端裁决，前端仍只展示服务端 `BATTLEFIELD_HELD`、`EXPERIENCE_GAINED` 与最终 snapshot。
- 已补测试与验证：新增 `P4DeclareBattleCommandGrantsHuntExperienceWhenDefenderHoldsBattlefield`，覆盖攻击者战死、防守狩猎单位幸存并据守时，服务端给 P2 2 点经验且事件来源为幸存防守猎手。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；狩猎征服/据守精确回归 3/3、`DeclareBattleCommand|Hunt|BattlefieldHeld` 相邻回归 104/104、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3131/3131 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百七十七批补充：多攻击者代表路径中的狩猎征服经验不再只读取第一个攻击者。`DECLARE_BATTLE` 战斗结算后会枚举所有仍在场上的幸存攻击单位，合计其中带 `狩猎` / `狩猎N` 的来源经验；`BATTLEFIELD_CONQUERED` 与 `EXPERIENCE_GAINED` 的来源会指向首个幸存狩猎单位，并额外记录 `huntSourceObjectIds` 与 `huntAmountsBySource`，避免第二个幸存攻击者带狩猎时服务端漏给经验。完整多参与者征服触发排序仍未官方化，本批只收口狩猎经验代表路径。
- 已补测试与验证：新增 `P4DeclareBattleCommandGrantsHuntExperienceFromSurvivingSecondAttacker`，覆盖第一个普通攻击者战死、第二个狩猎攻击者幸存并征服时，服务端给 P1 2 点经验且事件来源为幸存猎手。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标/代表路径精确回归 3/3、`DeclareBattleCommand|DeclareBattle|Hunt|Conquer` 相邻回归 122/122、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3130/3130 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百七十六批补充：`DECLARE_BATTLE` 的服务端候选与命令校验现在真正要求攻防单位处于 ready/未横置状态。`CoreRuleEngine.IsReadyFaceUpUnitForMinimalBattle` 与 `ActionPromptBuilder.IsReadyFaceUpBattlefieldUnitForBattle` 均加入 `!IsExhausted`，prompt metadata 的 `candidateFiltering` 同步更新为 `battlefield-zone-controlled-ready-face-up-units-not-already-in-combat`；绕过前端提交横置攻击者或横置防守者会被服务端拒绝。
- 已补测试与验证：新增 `ActionPromptDeclareBattleFiltersExhaustedAttackersAndDefenders` 与 `P4DeclareBattleCommandRejectsExhaustedAttackersAndDefenders`，并更新 Hub prompt 契约测试与一个据守激活征服 fixture，使横置 Lucian 保持为触发收益对象、由独立 ready 防守单位承接战斗。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/代表路径精确回归 3/3、`DeclareBattleCommand|DeclareBattle|BattlefieldTask` 相邻回归 64/64、据守激活征服相关回归 5/5、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3129/3129 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P0-004 第一百七十五批补充：战斗伤害中的眩晕状态已纳入服务端权威战力计算。`ResolveBattleCombatPower` 现在会在单位带有本回合 `STUNNED` 状态或旧展示标签“眩晕”时返回 0 战斗战力，并清空强攻/坚守及静态战斗修正贡献；眩晕单位仍会承受伤害，达到致命伤害时继续由状态性清理移入墓地。
- 已补测试与验证：新增 `P4DeclareBattleCommandStunnedAttackerContributesZeroCombatPower` 与 `P4DeclareBattleCommandStunnedDefenderContributesZeroCombatPowerButCanBeDestroyed`，覆盖眩晕进攻者不造成攻击伤害、眩晕防守者不反击但被致命伤害摧毁。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/代表路径精确回归 3/3、`DeclareBattleCommand|Stun` 相邻回归 88/88、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3127/3127 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；Chrome 插件只做了只读连通性确认并已清理 agent 标签页。整体仍 **NOT READY**，因为完整官方战斗/法术对决状态机、PaymentEngine、LayerEngine、cleanup queue 与全官方卡牌证据仍未清零。

- P1-004 第一百七十四批补充：防守战场触发继续收口战场来源控制权。`BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE`、`BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO` 与 `BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE` 现在要求触发的战场牌仍由防守玩家控制/legacy-owned；如果攻击方已控制该战场牌，防守方不会获得 reveal 抽牌或移动回基地等收益，带过期 battlefield target 的手写命令会被服务端拒绝。
- 已补测试与验证：新增 `P79BattlefieldDefendMoveToBaseRejectsAttackerControlledBattlefield` 与 `P79BattlefieldDefendRevealSpellSkipsAttackerControlledBattlefield`，覆盖攻击方控制战场牌时防守移动目标被拒绝、展示抽牌不触发且主牌堆/手牌保持不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；本批精确回归 5/5、`BattlefieldDefend|BattlefieldDefender|BattlefieldEphemeral|DeclareBattle` 相邻回归 71/71、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3125/3125 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Browser/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百七十三批补充：基础据守触发族继续统一战场来源控制权。`TryResolveBattlefieldHeldDrawTrigger`、造随从、双方召符文、持有者召符文、赐福、移动到基地、支付能量得分、七单位胜利、单位费用增加、下个法术获得 Echo、激活己方单位征服效果等 held 分支，都会要求战场牌仍由据守玩家控制/legacy-owned；owner=P2/controller 空的旧恢复梦树仍能抽牌，owner 漂移到 P1 的脏梦树不再给 P2 抽牌。
- 已补测试与验证：`P79BattlefieldHeldDrawsCardFromBattlefieldObject` 改为 owner=P2/controller 空的合法旧恢复梦树；新增 `P79BattlefieldHeldDrawSkipsOpponentOwnedBattlefield` 覆盖 owner=P1/controller 空的脏梦树不会触发 `BATTLEFIELD_HELD_DRAW_ONE` 或 `CARD_DRAWN`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；基础 held 相关回归 18/18、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3123/3123 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百七十二批补充：据守陵墓类 `BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD` 触发继续收口战场来源控制权。`TryResolveBattlefieldHeldReturnHeroTrigger` 现在要求陵墓战场牌仍由据守玩家控制/legacy-owned；owner=P2/controller 空的旧恢复陵墓仍能把 P2 英雄从墓地返回英雄区，owner 漂移到 P1 的脏陵墓不再触发返回。
- 已补测试与验证：`P79BattlefieldHeldReturnsHeroFromGraveyardToChampionZone` 改为 owner=P2/controller 空的合法旧恢复陵墓；新增 `P79BattlefieldHeldReturnHeroSkipsOpponentOwnedTomb` 覆盖 owner=P1/controller 空的脏陵墓不触发 `BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD`，P2 英雄仍留在墓地。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldHeldReturnHero|BattlefieldHeldReturnsHero|BATTLEFIELD_HELD_RETURN_HERO` 相关回归 3/3、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3122/3122 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百七十一批补充：鲜血祭坛类“战斗中被摧毁时支付 3 并召回”的战场来源继续收口控制权语义。`TryApplyBattlefieldDestroyedInBattleRecallReplacement` 现在要求触发的战场牌仍由其所在场区玩家控制/legacy-owned；owner=P2/controller 空的旧恢复鲜血祭坛仍可触发，owner 漂移到 P1 的脏战场牌不再替 P2 扣 3 法力并召回防守单位。
- 已补测试与验证：`P79BattlefieldBattleDestroyedUnitPaysThreeAndRecalls` 改为 owner=P2/controller 空的合法旧恢复战场牌；新增 `P79BattlefieldBattleDestroyedUnitSkipsOpponentOwnedAltar` 覆盖 owner=P1/controller 空的脏鲜血祭坛不会触发替代召回，单位按普通摧毁进入墓地。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldBattleDestroyedUnit|BattlefieldBattleDestroyed|BattlefieldDestroyedInBattle` 相关回归 4/4、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3121/3121 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百七十批补充：通用状态性非法待命 cleanup 执行侧继续与 pending task/snapshot 的 effective controller 语义对齐。`ApplyIllegalStandbyCleanup` 不再直接比较待命牌与战场牌的裸 `ControllerId`，而是使用 `controllerId -> ownerId -> 所在场区玩家`；事件里的 `controllerId` 和 `removedCards.previousControllerId` 也输出同一 effective controller，避免旧恢复对象出现“任务可见但清理漏执行”。
- 已补测试与验证：`P7PostStackCleanupRemovesIllegalStandbyFromBattlefield` 改为 owner-only 的 P2 战场牌与 owner-only 的 P1 待命牌，锁定栈后 cleanup 仍移除非法待命、写入墓地、广播 `BATTLEFIELD_STANDBY_REMOVED`，并记录 `previousControllerId = P1`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P7PostStackCleanupRemovesIllegalStandbyFromBattlefield|IllegalStandby|Standby` 相关回归 44/44、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3120/3120 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十九批补充：蜕变花园授予的 `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE` 命令侧来源继续与 prompt/战场来源控制语义对齐。`ResolveBattlefieldUnitGainExperienceAbility` 的来源单位检查不再直接要求 `ControllerId == playerId`，而是复用 source-control/legacy-owned 判断；owner=P1/controller 空的旧恢复战场单位仍可作为 P1 控制来源横置并获得经验。
- 已补测试与验证：`P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience` 的来源单位改为 owner=P1/controller 空，锁定 legacy-owned 来源仍能广播 `ABILITY_ACTIVATED` / `BATTLEFIELD_TRIGGER_RESOLVED` / `EXPERIENCE_GAINED` 并同步对象位置。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldUnitExperienceAbility|BattlefieldUnitExperience|ActivateAbility` 相关回归 67/67、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3120/3120 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十八批补充：战场防守触发“将友方防守单位移回基地”的最终结算检查继续与 source-control/legacy-owned 语义对齐。`TryResolveBattlefieldDefenderMoveToBaseTrigger` 不再直接按 `ControllerId` 判断，而是复用 `SourceObjectControlledByPlayerOrLegacyOwned`；owner=P2/controller 空的旧恢复防守单位仍可在 P2 防守战场触发后被移回基地，前端只消费服务端事件和 authoritative snapshot。
- 已补测试与验证：`P79BattlefieldDefendMovesChosenSurvivingDefenderToBase` 的防守单位改为 owner=P2/controller 空，锁定 legacy-owned 防守单位仍广播 `BATTLEFIELD_TRIGGER_RESOLVED` / `UNIT_MOVED_TO_BASE` 并从战场移动到基地。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldDefendMovesChosenSurvivingDefenderToBase|BattlefieldDefend|DeclareBattle` 相关回归 67/67、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3120/3120 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十七批补充：battle 后非法待命清理继续与服务端 effective controller 语义对齐。`RemoveIllegalStandbyAfterBattlefieldControl` 现在使用 `controllerId -> ownerId -> 所在场区玩家` 判断待命对象控制者，并在事件 `removedCards.previousControllerId` 中输出同一 effective controller；owner=P2/controller 空的合法待命在 P2 控制战场时会保留，owner=P1/controller 空的非法待命在 P2 控制战场后会被清到 P1 墓地。
- 已补测试与验证：扩展 `CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges` 覆盖 owner=P1/controller 空的非法待命被清，并新增 `CoreRuleEngineKeepsLegacyOwnedStandbyAfterBattlefieldControlConfirmed` 覆盖 owner=P2/controller 空的合法待命保留。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 4/4、`Standby|BattlefieldControl|DeclareBattle` 相关回归 104/104、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3120/3120 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十六批补充：battle 后战场控制结算的占据者枚举继续与 snapshot/prompt 的 source-control/legacy-owned 语义对齐。`ResolveBattlefieldControlAfterBattle` 现在以对象所在场区玩家作为候选控制者，并要求对象按 source-control/legacy-owned 归属该场区；owner=P1/controller 空的旧恢复幸存占据单位会正确让战场控制权结算给 P1，不会被误当成无人占据导致控制者清空。
- 已补测试与验证：扩展 `CoreRuleEngineChangesBattlefieldControllerAfterBattle`，将幸存攻击者改为 owner=P1/controller 空，锁定 battle 后 `BATTLEFIELD_CONTROL_RESOLVED`、`BattlefieldResolutions` 与 snapshot controller 仍为 P1。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 3/3、`BattlefieldController|BattlefieldControl|DeclareBattle` 相关回归 63/63、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3119/3119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十五批补充：0 战力状态性清理继续收口 legacy-owned 与 standby 边界。`PendingCleanupTasks` 和 Core 栈后 `ApplyLethalDamageCleanup` 的 0 战力候选现在只要求 owner 或 controller 任一身份存在即可覆盖旧恢复对象；同时显式排除 face-down/standby，避免待命牌因默认 0 战力被误走 `DESTROY_ZERO_POWER_UNIT`，待命仍只通过 `REMOVE_ILLEGAL_STANDBY` 处理非法控制权。
- 已补测试与验证：`PendingTaskQueueExposesZeroPowerFromPowerModifierAsStateBasedTask`、`P7PostStackCleanupDestroysZeroPowerFieldUnit` 已切换为 owner 存在但 controller 空的 legacy-owned 输入；`MatchStateBattlefieldControllerAndStandbyCleanupUseLegacyOwnershipFallback` 同步锁定 standby 不被 0 战力任务误清理。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻目标回归 4/4、`ZeroPower|Cleanup` 相关回归 16/16、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3119/3119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十四批补充：battlefield 自身控制者与非法待命清理继续使用同一套服务端 fallback。`BattlefieldState.ControllerId` 现在在 `controllerId` 为空但 `ownerId` 明确时会回落为 owner；`REMOVE_ILLEGAL_STANDBY` 判断也改为比较待命对象的有效控制者，避免旧恢复状态中 owner=战场控制者、controller 空的合法待命被误清理，同时仍会清理 owner 属于对手的非法待命。
- 已补测试与验证：新增 `MatchStateBattlefieldControllerAndStandbyCleanupUseLegacyOwnershipFallback`，覆盖 legacy-owned 战场牌显示为 `CONTROLLED`、合法待命不产生清理任务、对手 owner 待命仍产生 `REMOVE_ILLEGAL_STANDBY`，并锁定 snapshot `controllerId/status/pendingTaskKinds`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻目标回归 4/4、`Standby|BattlefieldState|PendingTaskQueue` 相关回归 46/46、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3119/3119 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十三批补充：battle/battlefield 权威 snapshot 与 battlefield task view 的参与者控制权继续收口。`BattleState.ParticipantControllerIds`、`BattlefieldState.OccupantControllerIds` 和 `BattlefieldTaskState.ParticipantControllerIds` 现在统一使用 `controllerId -> ownerId -> 所在场区玩家` 的服务端 fallback；旧恢复状态中 `controllerId` 为空但 owner 明确的攻防单位/战场占据单位，不会再让前端看到缺失参与玩家的争夺、法术对决或开战任务。
- 已补测试与验证：新增 `MatchStateBattlefieldTasksUseLegacyOwnedOccupantControllers`、`MatchStateBattleStateUsesLegacyOwnedParticipantControllers`，覆盖 legacy-owned 战场占据单位、攻防参与者和 snapshot 字段。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻目标回归 4/4、`BattlefieldTask|BattleState|BattlefieldState` 相关回归 2/2、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3118/3118 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；用户要求确认的 Chrome 插件只读连接检查已通过，后续显著前端 smoke 可优先使用 Browser/Chrome 通道。整体仍 **NOT READY**，因为完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十二批补充：`MOVE_UNIT` prompt 来源继续与 Core 命令侧对齐。`IsControlledObjectWithTag` 现在按 source-control/legacy-owned 且要求对象仍在该玩家场上区域；owner=P1/controller 空的旧恢复单位继续显示为可移动来源，owner=P2/controller 空的脏单位不会进入 `MOVE_UNIT.sources` 或 `sourceRequirements`。
- 已补测试与验证：扩展 `ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits`，覆盖 legacy-owned 单位被展示、对手 owner 脏单位被过滤，并保持面朝下/战斗中/对手控制单位不可移动。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`MoveUnit` 相关回归 51/51、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3116/3116 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是 prompt 层移动来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十一批补充：`ACTIVATE_ABILITY` prompt 层来源继续与 Core 命令侧对齐。`ActivateAbilityRequirementsForSource`、friendly unit/equipment prompt target 判断现在按 source-control/legacy-owned 且要求对象仍在该玩家场上区域；owner=P1/controller 空的旧恢复 Vi/Xerath 会继续暴露为 P1 的激活能力来源，owner=P2/controller 空的脏对象不会进入前端候选或 `sourceRequirements`。
- 已补测试与验证：新增 `ActionPromptActivateAbilityMetadataUsesLegacyOwnedSources`，覆盖 legacy-owned Vi/Xerath 被展示、对手 owner 脏对象被过滤。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`ActivateAbility` 相关回归 63/63、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3116/3116 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是 prompt 层激活能力来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百六十批补充：基础符文与支付资源回收符文的 prompt/command 来源控制语义继续收口。前端 `ActionPrompt` 的 `TAP_RUNE` / `RECYCLE_RUNE` 来源、出牌/装配支付资源候选，以及 Core 的 `TAP_RUNE`、`RECYCLE_RUNE`、`RECYCLE_RUNE:<objectId>` 支付资源校验，现在统一使用 source-control/legacy-owned 判断；owner=P1/controller 空的恢复旧对象会继续作为 P1 合法符文展示和提交，owner=P2/controller 空的脏符文不会被暴露或支付。
- 已补测试与验证：新增 `CoreRuleEnginePromptsAndTapsLegacyOwnedBasicRune`、`CoreRuleEngineRecyclesLegacyOwnedBasicRune`、`P7PlayCardRecyclesLegacyOwnedRuneAsPaymentResourceAction`，覆盖 prompt 候选、基础横置、基础回收与支付回收路径；对手 owner 的脏符文不会进入候选。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`TapRune|RecycleRune|PaymentResource` 相关回归 15/15、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3115/3115 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是基础符文/支付资源来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十九批补充：战场征服触发的自动目标/资源选择继续收口控制权语义。Irelia/通用 exhausted legend 查找、征服消耗增益目标、征服返回/单位征服增益目标、回合结束重置符文候选、重置装备候选、按其他战场抽牌的其他战场枚举，现在统一使用 source-control/legacy-owned 判断；恢复脏状态中 `controllerId` 为空但 `ownerId` 已明确属于对手的对象，不再被自动消耗增益、重置传奇/符文/装备、或计入抽牌数量。
- 已补测试与验证：新增 `P79BattlefieldConquerConsumesControlledBoonWhenDirtyBoonIsOpponentOwned`、`P79BattlefieldConquerReadyLegendSkipsOpponentOwnedLegend`、`P79BattlefieldConquerReadyRunesAtEndSkipsOpponentOwnedRune`、`P79BattlefieldConquerDrawsForOtherBattlefieldsSkipsOpponentOwnedBattlefield`、`P79BattlefieldConquerReadyEquipmentSkipsOpponentOwnedEquipment`，覆盖脏对象被跳过且合法对象仍按原路径结算。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`BattlefieldConquer` 相关回归 34/34、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3112/3112 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性战场征服目标/资源选择护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十八批补充：传奇触发来源继续收口控制权语义。`ControllerHasAzirLegend`、`ControllerHasRengarLegend`、`ControllerHasLeonaLegend`、`ControllerHasJhinLegend` 以及 Rengar/Leona/Sivir 的具体传奇查找现在统一使用 source-control/legacy-owned 判断；恢复脏状态中 `controllerId` 为空但 `ownerId` 已明确属于对手的传奇，不再能作为当前玩家传奇触发来源继续给单位加战力、创建休眠金币或完成 Jhin 高费法术计数。
- 已补测试与验证：新增 `P79LegendTriggerRengarSkipsPowerWhenLegendBecomesOpponentOwned`、`P79LegendTriggerSivirSkipsGoldWhenLegendBecomesOpponentOwned`、`P79LegendTriggerJhinSkipsHighCostSpellWhenLegendBecomesOpponentOwned`，覆盖脏传奇来源不再广播对应 `LEGEND_TRIGGER_RESOLVED`、不产生额外资源/抽牌/召符文，合法相邻路径仍通过。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`LegendTriggerRengar|LegendTriggerSivir|LegendTriggerJhin` 相关回归 11/11、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3107/3107 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性传奇触发来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十七批补充：清算人竞技场据守后激活己方战场单位征服效果继续收口单位控制权枚举。`TryResolveBattlefieldHeldActivateConquestEffects` 现在按统一 source-control/legacy-owned 判断枚举可激活单位；恢复脏状态中 `controllerId` 为空但 `ownerId` 属于对手的 Kai'Sa，不再作为 P2 的己方战场单位触发抽牌。合法 Bad Poro 征服效果仍会创建休眠金币，避免把过滤写成全量跳过。
- 已补测试与验证：新增 `P79BattlefieldHeldActivateConquestEffectsSkipsOpponentOwnedUnits`，覆盖触发事件的 `activatedUnitObjectIds` 只包含合法 Bad Poro，不再包含脏 Kai'Sa；不会为 P2 抽牌，主牌堆保留原卡。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`BattlefieldHeldActivateConquest` 回归 4/4、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3104/3104 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性战场据守触发单位枚举护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十六批补充：单位返回手牌后支付 1 召符文的 Ghost Bay 触发继续收口战场来源控制权。`TryResolveBattlefieldUnitReturnedCallRuneTrigger` 现在使用统一 source-control/legacy-owned 判断；恢复脏状态中 `controllerId` 为空但 `ownerId` 属于对手的鬼影湾，不再能在单位返回手牌后扣 P1 法力、额外召符文或广播该战场触发。原卡牌自身的基础召符文效果仍保留，避免把触发护栏写成过度拦截。
- 已补测试与验证：新增 `P79BattlefieldReturnedUnitDoesNotPayWhenGhostBayIsOpponentOwned`，覆盖基础返回/召符文仍结算，但 Ghost Bay 不触发、不扣 1 法力、不召第二个符文、不发 `BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE` 事件。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`BattlefieldReturnedUnit|BattlefieldReturnCallRune` 回归 4/4、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3103/3103 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性战场触发来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十五批补充：战场牌授予传奇行动的 required battlefield source 继续收口控制权。`PlayerControlsBattlefieldCard` / `TryGetControlledBattlefieldCardObject` 现在使用统一 source-control/legacy-owned 判断；Poro Forge 这类要求玩家控制指定战场牌才开放的 `LEGEND_ACT`，不会再因战场牌 `controllerId` 为空但 `ownerId` 属于对手而被 prompt 暴露或被命令侧接受。
- 已补测试与验证：新增 `ActionPromptHidesLegendActionSourceWhenRequiredBattlefieldIsOpponentOwned` 与 `P79BattlefieldForgeLegendAttachRejectsOpponentOwnedForge`，覆盖 prompt 不暴露候选、命令侧拒绝且不横置传奇/不贴附武装/不发事件。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；Poro Forge 精确回归 5/5、`LegendAct` 相关回归 38/38、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3102/3102 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性战场授予传奇行动来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十四批补充：蜕变花园授予战场单位激活能力继续收口战场来源控制权。`ActionPromptBuilder` 的 `BattlefieldGrantUnitExperienceObjectId` 与 Core 的对应结算查找现在都使用统一 source-control/legacy-owned 判断；恢复脏状态中 `controllerId` 为空但 `ownerId` 属于对手的蜕变花园，不再授予 P1 战场单位 `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE` 候选，也不能被手写 `ACTIVATE_ABILITY` 命令绕过触发。
- 已补测试与验证：新增 `ActionPromptHidesActivateAbilitySourceWhenGrantingBattlefieldIsOpponentOwned`，并新增 `P79BattlefieldUnitExperienceAbilityRejectsOpponentOwnedMutationGarden`，分别覆盖 prompt 不暴露候选、命令侧拒绝且不横置单位/不增加经验/不发事件。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 7/7、`ActivateAbility` 相关回归 62/62、`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3100/3100 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是代表性战场授予能力来源护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十三批补充：服务端 `HIDE_CARD` 命令侧同步收口 Bandle Tree 额外待命目的地控制权。`TryGetBattlefieldExtraStandbyObject` 现在与 prompt 层一致使用统一 source-control/legacy-owned 判断；恢复/旧日志中 `controllerId` 为空但 `ownerId` 已明确属于对手的战场牌，不再能被手写命令绕过前端用作 `BATTLEFIELD:<objectId>` 目的地。
- 已补测试与验证：`P79BattlefieldBandleTreeRejectsExtraStandbyWithoutControlledTree` 扩展为覆盖两类脏状态：`owner=P1/controller=P2` 与 `owner=P2/controller 空`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldBandleTree|HideCard` 相关回归 33/33 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3098/3098 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是待命额外目的地命令侧护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十二批补充：服务端 `HIDE_CARD` 的额外待命目的地继续收口来源控制权。Bandle Tree 这类“战场牌允许额外待命到该战场”的 prompt 目的地现在使用统一的 source-control/legacy-owned 判断；恢复脏状态中如果该战场牌已经明确由对手控制，即使仍残留在当前玩家 `Battlefields` 列表中，也不会再作为 `BATTLEFIELD:<objectId>` 目的地暴露给前端。前端仍只展示服务端 `ActionPrompt.sourceRequirements` / `destinationChoices`，不自行判断待命目的地合法性。
- 已补测试与验证：扩展 `ActionPromptBuildsHideCardSourceRequirements`，覆盖 `P1-BATTLEFIELD-BANDLE-TREE` 控制权漂移到 P2 后，P1 的 `HIDE_CARD` 仍可暗置到普通 `STANDBY`，但候选和 `sourceRequirements.destinationChoices` 都不再包含 `BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`ActionPromptBuildsHideCardSourceRequirements|P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides|HideCard|Standby` 相关回归 58/58 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3097/3097 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Chrome smoke；整体仍 **NOT READY**，因为这是待命目的地 prompt 来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十一批补充：服务端 ActionPrompt 构造层同步收口战场来源控制权。移动单位 prompt 的游走许可/禁止移回基地、出牌 prompt 的 Echo 减费/武装减费/禁止单位打到战场，现在与 Core 结算侧一样要求战场来源仍由所在场区玩家控制/legacy-owned；前端只展示服务端候选时，不会再看到恢复脏战场牌产生的错误游走、错误折扣或错误阻止入口。
- 已补测试与验证：新增 `P79BattlefieldStaticRoamPromptSkipsOpponentControlledSource`、`P79BattlefieldStaticPreventMoveToBasePromptSkipsOpponentControlledSource`、`P79BattlefieldStaticEchoCostReductionPromptSkipsOpponentControlledSource`、`P79BattlefieldStaticEquipmentCostReductionPromptSkipsOpponentControlledSource`，并回归对应 Core 命令侧脏来源测试。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；prompt/命令精确回归 9/9 通过；`BattlefieldStatic|Prompt|sourceRequirements` 相关回归 73/73 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3097/3097 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke；整体仍 **NOT READY**，因为这是 prompt 层战场来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百五十批补充：计分/回合开始战场来源继续收口控制权语义。提高胜利分数阈值、第一回合额外召符文、第一回合战场得分、得分延迟、回合开始全战场单位伤害、回合开始摧毁单位并抽牌，现在在 Core 结算侧按“战场来源仍由所在场区玩家控制/legacy-owned”过滤；回合开始伤害/摧毁目标也同步要求仍由所在场区玩家控制/legacy-owned。`ResolutionResult` 视角 snapshot 的 `timing.winningScore` 同步使用同一控制权过滤，避免权威胜负事件和前端 snapshot 阈值分叉。
- 已补测试与验证：新增 `P79BattlefieldStaticWinningScoreIncreaseSkipsOpponentControlledSource`、`P79BattlefieldStaticFirstTurnRuneSkipsOpponentControlledSource`、`P79BattlefieldStaticFirstTurnScoreSkipsOpponentControlledSource`、`P79BattlefieldScoreDelaySkipsOpponentControlledSource`、`P79BattlefieldTurnStartDamageSkipsOpponentControlledSource`、`P79BattlefieldTurnStartDestroyDrawSkipsOpponentControlledSource`，覆盖脏战场来源不再改变胜利阈值、召符文数量、第一回合得分/延迟、回合开始伤害或摧毁抽牌。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻精确回归 17/17 通过；计分/回合开始相关回归 18/18 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3093/3093 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke；整体仍 **NOT READY**，因为这是代表性计分/回合开始战场来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十九批补充：移动/伤害相关战场静态继续收口来源控制权语义。风丘授予游走、卑尔居恩巢穴阻止移回基地、落石禁单位打到战场、后巷酒吧单位离开战场 +1、虚空之门目标法术/技能伤害 +1，现在都要求战场来源仍由对应场区玩家控制/legacy-owned；虚空之门加伤还同步要求被加伤目标仍由其所在场区玩家控制/legacy-owned，避免恢复/旧日志里的错位对象继续吃到战场静态。
- 已补测试与验证：新增 `P79BattlefieldStaticRoamSkipsOpponentControlledSource`、`P79BattlefieldStaticPreventMoveToBaseSkipsOpponentControlledSource`、`P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`、`P79BattlefieldStaticPreventUnitPlaySkipsOpponentControlledSource`、`P79BattlefieldTargetDamageBonusSkipsOpponentControlledSource`，覆盖脏战场来源不再授予游走、不再阻止移动/打出、不再给移动单位 +1，也不再给目标伤害 +1。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻精确回归 12/12 通过；`BattlefieldStatic|BattlefieldTargetDamageBonus|MoveUnit` 相关回归 75/75 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3087/3087 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke；整体仍 **NOT READY**，因为这是代表性移动/伤害战场静态来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十八批补充：战场静态/触发来源继续收口控制权语义。玛莱尖塔 Echo 减费、奥恩锻炉首个武装减费、梦树友方战场单位被法术目标选择后抽牌、失落书库高费法术洞察回收，现在都要求对应战场来源仍由该玩家控制/legacy-owned；恢复/旧日志中对手控制的战场牌即使仍残留在当前玩家 `Battlefields` 列表中，也不会继续提供减费、抽牌或洞察触发。
- 已补测试与验证：新增 `P79BattlefieldStaticEchoCostReductionSkipsOpponentControlledSource`、`P79BattlefieldStaticEquipmentCostReductionSkipsOpponentControlledSource`、`P79BattlefieldFriendlySpellTargetSkipsOpponentControlledSource`、`P79BattlefieldHighCostSpellInsightSkipsOpponentControlledSource`，覆盖脏战场来源不再贡献 Echo/武装减费，也不触发梦树抽牌或失落书库回收。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻精确回归 8/8 通过；战场静态/触发相关回归 15/15 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3082/3082 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke；Chrome 插件连通性已确认，后续显著前端批次继续优先用 Chrome 插件并在结束时清理标签页和本地进程。整体仍 **NOT READY**，因为这是代表性战场静态/触发来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十七批补充：代表性战场触发继续收口来源/目标控制权语义。废弃大厅法术 +1、偶像谷打出单位支付 1 赐福、流星疗泉本回合首个单位移动另一单位三个相邻战场触发，现在都会要求触发战场对象由所在场区玩家控制/legacy-owned；触发目标/被移动对象也继续使用同一场区玩家控制口径，避免恢复/旧日志中对手控制的战场牌或单位留在当前玩家战场列表时仍发动或吃到触发。
- 已补测试与验证：新增 `P79BattlefieldSpellPowerBonusSkipsOpponentControlledSource`、`P79BattlefieldPlayUnitBoonSkipsOpponentControlledBattlefieldSource`、`P79BattlefieldFirstUnitPlayedMoveOtherSkipsOpponentControlledBattlefieldSource`，覆盖脏战场来源不再触发 +1、支付赐福或移动另一单位。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标战场触发回归 10/10 通过；`BattlefieldSpellPowerBonus|BattlefieldPlayUnitBoon|BattlefieldFirstUnitPlayed|BattlefieldHeldUnitCostIncrease|BattlefieldHighCostSpellInsight|BattlefieldFriendlySpellDraw` 相关回归 17/17 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3078/3078 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke。整体仍 **NOT READY**，因为这是部分代表性战场触发控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十六批补充：Rengar 单位打出传奇触发继续收口延迟目标控制权语义。`IsValidRengarUnitPlayedTriggerTarget` 现在不仅要求额外触发目标是场上正面单位，也要求该对象仍由所在场区玩家控制/legacy-owned；`ResolveRengarUnitPlayedPowerTrigger` 结算侧同步复核同一条件，避免目标在入栈后控制权漂移或恢复脏状态中错位在对手场区时仍被授予本回合 +1 战力。
- 已补测试与验证：新增 `P79LegendTriggerRengarRejectsOpponentControlledFieldTriggerTarget` 与 `P79LegendTriggerRengarSkipsPowerWhenStoredTargetControlChanges`，覆盖命令侧拒绝 P2 场区中 P1 控制脏单位作为 Rengar 触发目标，以及结算前已记录目标失去 P1 控制时不产生 `LEGEND_TRIGGER_RESOLVED` / `POWER_MODIFIED_UNTIL_END_OF_TURN`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79LegendTriggerRengar|P79LegendTriggerLeona` 精确回归 7/7 通过；`Rengar|Leona|UnitPlayed|LegendTrigger` 相关回归 46/46 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3075/3075 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke。整体仍 **NOT READY**，因为这是单个传奇触发延迟目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十五批补充：通用场上目标 scope 继续收口控制权语义。`IsBattlefieldObject`、`IsBaseObject`、`IsFieldUnitObject` 与 `IsEquipmentObject` 现在在供 `BattlefieldUnit`、`AnyUnit`、`AnyUnitThenFriendlyMainDeckCard`、`FriendlyHandCardThenBattlefieldUnit`、`AttackingUnit`、`Equipment` 等不区分 friendly/enemy 的目标范围使用时，也要求对象仍由所在场区玩家控制/legacy-owned；恢复/旧日志中错位在对手场区的脏单位/装备不再仅因出现在 `Base/Battlefields` 列表中就成为合法 target。
- 已补测试与验证：新增 `CoreRuleEnginePlayCardRejectsGenericFieldTargetsWithMismatchedZoneControl`，覆盖默认战场单位目标、`AnyUnit` 基地目标和 `Equipment` 装备目标三类通用 scope 面对 P2 场区中 P1 控制脏对象时，绕过前端提交也稳定返回 `INVALID_TARGET` 且不新增 stack item。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻精确回归 6/6 通过；`Incinerate|LotusTrap|DancingGrenade|RunePrison|SigilBurst|TakeUp|EquipmentAttachment|AttackingUnit|BattlefieldUnit` 相关回归 64/64 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3073/3073 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke。整体仍 **NOT READY**，因为这是通用目标范围控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十四批补充：场上身份 helper 与双目标武装提交校验继续收口控制权语义。`IsControlledFieldUnit` / `IsEnemyFieldUnit` 现在不再只看对象处于友方/敌方场区和单位标签，还要求对象由其所在场区玩家控制/legacy-owned；《取放自如》这类“单位 + 其控制者武装”双目标牌的提交前 `AreAttachDetachTargetsAllowed` 也补齐与结算路径一致的 `FieldIdentityMatchesZone` 检查，避免恢复/旧日志中的脏武装或脏单位进入服务端 stack。
- 已补测试与验证：新增 `P4EquipmentAttachmentRejectsOpponentControlledWeaponInControllersZone`，覆盖 P2 场区中的 P1 控制武装不会成为《取放自如》合法第二目标，绕过前端提交也稳定返回 `INVALID_TARGET`；新增 `P79LegendTriggerLeonaSkipsBoonWhenStoredTargetControlChanges`，覆盖 Leona 眩晕触发在结算前目标控制权漂移后不会给已非 P1 控制的旧目标授予 Boon。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相邻精确回归 6/6 通过；`TakeUp|Leona|EquipmentAttachment` 相关回归 10/10 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3072/3072 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，没有启动 API/Vite/Web smoke；已用 Chrome 插件做只读连通性检查并清理 agent 标签页。整体仍 **NOT READY**，因为这是场上身份与双目标提交/延迟触发控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十三批补充：区域批量选择结算继续收口控制权语义。全战场对象、全场对象、全场单位、全场装备、友方/敌方场上单位、友方/敌方战场单位和敌方战斗单位等批量枚举 helper 现在都会过滤为“对象仍由其所在场区玩家控制/legacy-owned”，再供全体摧毁、全体返回、全体状态、全战场伤害、敌方战斗/战场伤害、友方/敌方战力修正、全体 Boon/重置/休眠等路径使用，避免恢复/旧日志里的脏区域对象被批量效果当成有效友方/敌方对象。
- 已补测试与验证：新增 `CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject`，覆盖《提伯斯》全战场伤害恢复后面对 P2 战场中 P1 控制脏单位时，只伤害 P2 合法战场单位，不对脏对象产生 `DAMAGE_APPLIED`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相关精确回归 2/2 通过；`BladeWhirlwind|Tibbers|CannonBarrage|Skullcrack|ThousandTailedWatcher|Ruination|Undertow|DecisiveStrike|GrandStrategy|OverchargedEnergy|HuntAndReadiesAllFriendlyUnits` 相关回归 16/16 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3070/3070 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是区域批量选择结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十二批补充：专用场上目标互动结算继续收口控制权语义。互斗、源/目标战力互伤、按第一目标战力伤害第二目标、重置第一目标后按其战力伤害第二目标、匹配/交换目标战力、按第一目标战力伤害全体敌方战场单位等通用循环外的专用分支，现在同样要求涉及的场上目标由其所在场区玩家控制/legacy-owned 后才改写伤害、战力或状态；“摧毁第一个目标再按其战力增益第二目标”也要求第一个目标确实被摧毁且第二目标仍是合法场区受控对象。
- 已补测试与验证：新增 `CoreRuleEngineCarnivorousVinetendrilResolutionSkipsOpponentControlledEnemyZoneTarget`，覆盖《食肉蛇藤》恢复后的 stack target 指向 P2 战场中 P1 控制脏单位时，源单位和脏目标都不受到互伤伤害，且不产生 `DAMAGE_APPLIED`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增/相关精确回归 3/3 通过；`Duel|MarchingOrders|ClashOfGiants|CarnivorousVinetendril|Knockdown|AlphaStrike|LastBreath|ConvergentMutation|Switcheroo|SoulStrangle|Bait|DragonsRage|BulletTime` 相关回归 61/61 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3069/3069 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是专用目标互动结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十一批补充：通用场上目标结算继续收口控制权语义。`RequiredTargetCount` / `UsesFriendlyBattlefieldUnitCountAsMaxTargetCount` 共用的目标循环现在先通过 `IsFieldObjectControlledByZonePlayer`，要求目标仍在场上且由其所在场区玩家控制/legacy-owned，再允许伤害、状态、标签、Boon、战力修正、重置和后续目标型移动/返回等通用变更；恢复/旧日志里的脏 target 不再仅因仍处于某玩家场区就被通用效果改写。
- 已补测试与验证：新增 `CoreRuleEngineIncinerateResolutionSkipsOpponentControlledEnemyZoneTarget`，覆盖《焚烧》恢复后的 stack target 指向 P2 战场中 P1 控制脏单位时，源法术正常入墓，但不产生 `DAMAGE_APPLIED`，脏对象伤害仍为 0 且留在 P2 battlefield。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增精确回归 1/1 通过；`Incinerate|DancingGrenade|Starfall|LotusTrap|NoxianGuillotine|LastStand|AnimalFriends|StandDefiant|HeroicCharge|Skullcrack|PowerPunch|Parry` 相关回归 20/20 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3068/3068 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是通用目标变更结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百四十批补充：目标型摧毁结算继续收口控制权语义。新增 `TryDestroyControlledFieldTarget` 作为 `DestroysTarget`、`DestroysTargetIfAlreadyHasStatusEffect` 与“摧毁第一个目标并按其战力增益第二目标”这类明确目标摧毁路径的结算 guard；它要求目标由其所在场区玩家控制/legacy-owned 后才调用底层 `TryDestroyTarget`。全场摧毁、状态性 lethal/zero-power cleanup、回合开始清理等非目标型路径仍继续使用底层清理语义，避免误收紧全局规则。
- 已补测试与验证：新增 `CoreRuleEngineVengeanceResolutionSkipsOpponentControlledEnemyZoneTarget`，覆盖《复仇》恢复后的 stack target 指向 P2 基地中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_DESTROYED`，脏对象仍留在 P2 base 且不进 P2 graveyard。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Vengeance|Destroy` 相关回归 55/55 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3067/3067 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是目标型摧毁结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十九批补充：交换目标位置结算继续收口控制权语义。`SwapsTargetLocations` 共用的 `TrySwapTargetLocations` 现在接收 `cardObjects`，要求两个交换对象都由各自所在场区玩家控制/legacy-owned；恢复/旧日志里的脏 target 不会把对手控制对象交换到当前玩家场区或把合法对象带到错误位置。
- 已补测试与验证：新增 `CoreRuleEngineReflectionsResolutionSkipsOpponentControlledFriendlyZoneTarget`，覆盖《镜中幻影》恢复后的 stack target 指向 P1 基地中 P2 控制脏单位时，源法术正常入墓，但不产生 `UNIT_LOCATIONS_SWAPPED`，脏对象仍留在 P1 base，合法单位仍留在 P1 battlefield。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Reflections|Swap` 精确回归 4/4 通过；`Move|Swap|Reflections` 相关回归 95/95 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3066/3066 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是交换目标位置结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十八批补充：移动目标到另一单位所在位置的结算继续收口控制权语义。`MovesFirstTargetToSecondTargetLocation` 共用的 `TryMoveFirstTargetToSecondTargetLocation` 现在接收 `cardObjects`，要求被移动对象和目的地对象都由各自所在场区玩家控制/legacy-owned；《猛龙摆尾》这类“移动后互斗伤害”组合路径会先通过该 guard，再执行移动和互斗伤害，避免恢复/旧日志里的脏 target 继续移动或造成伤害。
- 已补测试与验证：新增 `CoreRuleEngineBaitResolutionSkipsAlreadyControlledEnemyZoneTarget` 与 `CoreRuleEngineDragonsRageResolutionSkipsAlreadyControlledEnemyZoneTarget`，覆盖《诱饵》和《猛龙摆尾》恢复后的 stack target 指向 P2 场区中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_MOVED_TO_UNIT_LOCATION`；《猛龙摆尾》同时不产生 `DAMAGE_APPLIED`，双方单位伤害保持 0。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增精确回归 2/2 通过；`Bait|DragonsRage|Move` 相关回归 95/95 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3065/3065 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是移动到另一单位位置结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十七批补充：移动目标到战场结算继续收口控制权语义。`MovesTargetsToOwnerBattlefields` 共用的 `TryMoveTargetToOwnerBattlefield` 现在接收 `cardObjects` 并要求目标由其所在基地玩家控制/legacy-owned；`MovesTargetToBattlefield` 共用的 controller battlefield helper 也要求目标由行动玩家控制/legacy-owned。像《天声震落》《叹为观止》这类移动前还有伤害/增益副作用的组合效果，会先检查该结算 guard，避免恢复/旧日志里的脏 target 仍吃到副作用或被移动到错误战场。
- 已补测试与验证：新增 `CoreRuleEngineThunderingDropResolutionSkipsOpponentControlledFriendlyBaseTarget`、`CoreRuleEngineBattleCommandResolutionSkipsOpponentControlledFriendlyBaseTarget`、`CoreRuleEngineStunningDisplayResolutionSkipsOpponentControlledFriendlyBaseTarget`，覆盖脏 stack target 指向 P1 基地中 P2 控制单位时，《天声震落》不造成敌方战场伤害且不移动、《战斗号令》只移动仍合法的 P2 基地目标、《叹为观止》不授予 Boon 且不移动。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增精确回归 3/3 通过；`ThunderingDrop|StunningDisplay|BattleCommand|VoidAssault` 相关回归 64/64 通过；`Move|Battlefield` 相关回归 295/295 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3063/3063 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是移动目标到战场结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十六批补充：移动目标回所属基地结算继续收口控制权语义。`MovesTargetToBase` 与部分战场触发共用的 `TryMoveTargetToOwnerBase` 现在接收 `cardObjects` 并要求目标由其所在战场区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象从当前玩家战场列表移动到当前玩家基地。
- 已补测试与验证：新增 `CoreRuleEngineFlashResolutionSkipsOpponentControlledFriendlyBattlefieldTarget`，覆盖《闪现》恢复后的 stack target 指向 P1 战场中 P2 控制脏单位时，源法术正常入墓，但不产生 `UNIT_MOVED_TO_BASE`，脏对象仍留在 P1 battlefield，伤害状态不被刷新。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Flash|ShieldWall|RuthlessPursuit|PlayfulTentacles` 精确回归 11/11 通过；`Move|Battlefield` 相关回归 295/295 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3060/3060 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是移动目标回所属基地结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十五批补充：获得控制权结算继续收口控制权语义。`GainsControlOfTargetToBase` 与 `GainsControlOfTargetToBattlefield` 现在要求目标由其所在敌方战场区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把当前玩家已控制但错位在对手战场列表的对象移动到当前玩家基地/战场。
- 已补测试与验证：新增 `CoreRuleEngineForcedConscriptionResolutionSkipsAlreadyControlledEnemyZoneTarget` 与 `CoreRuleEngineHostileTakeoverResolutionSkipsAlreadyControlledEnemyZoneTarget`，分别覆盖《强制征召》和《恶意收购》恢复后的 stack target 指向 P2 战场中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_CONTROL_GAINED`，脏对象仍留在 P2 battlefield，控制者不被重写。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`ForcedConscription|TakenForARide|HostileTakeover` 精确回归 9/9 通过；`Control|Battlefield` 相关回归 306/306 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3059/3059 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是获得控制权结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十四批补充：放逐后打回场上结算继续收口控制权语义。`BanishesTargetThenPlaysToBase` / `BanishesTargetThenPlaysToBattlefield` 共用的 `TryBanishTargetThenPlayToOwnerField` 现在要求目标由其所在基地/战场区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象从当前玩家场区放逐并刷新到当前玩家基地/战场。
- 已补测试与验证：新增 `CoreRuleEnginePortalpalRescueResolutionSkipsOpponentControlledFriendlyZoneTarget`，覆盖《传送门大营救》恢复后的 stack target 指向 P1 战场中 P2 控制脏单位时，源法术正常入墓，但不产生 `UNIT_BANISHED` 或 `UNIT_PLAYED_TO_BASE`，脏对象仍留在 P1 battlefield，伤害状态不被刷新。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`PortalpalRescue|ArcaneShift|HuntingRhythm` 精确回归 8/8 通过；`Banish|PlayCard` 相关回归 53/53 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3057/3057 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是放逐后打回场上结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十三批补充：公共场上目标返回手牌结算继续收口控制权语义。`ReturnsTargetToHand` / 返回额外费用等共用的 `TryReturnTargetToHand` 现在要求目标由其所在基地/战场区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把当前玩家控制对象从对手场区移入对手手牌。
- 已补测试与验证：新增 `CoreRuleEngineHurricaneSweepResolutionSkipsOpponentControlledBattlefieldTarget`，覆盖《飓风席卷》恢复后的 stack target 指向 P2 战场中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_RETURNED_TO_HAND`，脏对象仍留在 P2 battlefield，P2 hand 不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Happenstance|HurricaneSweep|Gust|Reconsider` 精确回归 11/11 通过；`Return|Hand` 相关回归 92/92 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3056/3056 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是公共返回手牌结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十二批补充：目标移回所属者主牌堆结算继续收口控制权语义。`TargetOwnerMainDeckDestination` 代表路径现在在 `TryMoveTargetToOwnerMainDeck` 中要求目标由其所在基地/战场区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把当前玩家控制对象从对手场区移入对手主牌堆。
- 已补测试与验证：新增 `CoreRuleEngineCustodianJudgmentResolutionSkipsOpponentControlledBattlefieldTarget`，覆盖《持卫的裁决》恢复后的 stack target 指向 P2 战场中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_RETURNED_TO_DECK`，脏对象仍留在 P2 battlefield，P2 main deck 不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`CustodianJudgment` 精确回归 5/5 通过；`Deck|Return` 相关回归 56/56 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3055/3055 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是目标移回所属者主牌堆结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十一批补充：对手主牌堆顶单位打出结算继续收口控制权语义。`PlaysOpponentTopMainDeckUnitToBase` 代表路径现在在 `TryPlayOpponentTopMainDeckUnitToBase` 中要求目标由其所在主牌堆区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把当前玩家控制对象从对手主牌堆顶打出到当前玩家基地。
- 已补测试与验证：新增 `CoreRuleEngineBerserkImpulseResolutionSkipsOpponentControlledTopDeckTarget`，覆盖《暴怒冲动》恢复后的 stack target 指向 P2 主牌堆顶中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_PLAYED_TO_BASE`，脏对象仍留在 P2 main deck 顶部。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`BerserkImpulse` 精确回归 3/3 通过；`MainDeck|PlayCard` 相关回归 46/46 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3054/3054 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是对手主牌堆顶打出结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百三十批补充：手牌目标打出到基地结算继续收口控制权语义。`PlaysHandTargetToBase` 代表路径现在在 `TryPlayHandCardToBase` 中要求目标由其所在手牌区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象从某玩家手牌打出到该玩家基地。
- 已补测试与验证：新增 `CoreRuleEngineBoneSkewerResolutionSkipsOpponentControlledHandZoneTarget`，覆盖《透骨尖钉》恢复后的 stack target 指向 P2 手牌中 P1 控制脏单位时，源法术正常入墓，但不产生 `UNIT_PLAYED_TO_BASE` 或 `STATUS_EFFECT_APPLIED`，脏对象仍留在 P2 hand。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`BoneSkewer|HelpArrives` 精确回归 7/7 通过；`Hand|PlayCard` 相关回归 89/89 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3053/3053 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是手牌目标打出到基地结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十九批补充：墓地打出到基地结算继续收口控制权语义。`PlaysGraveyardTargetToBase` 代表路径现在在 `TryPlayGraveyardCardToBase` 中要求目标由该墓地区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象从当前玩家墓地打出到当前玩家基地。
- 已补测试与验证：新增 `CoreRuleEngineHarrowingResolutionSkipsOpponentControlledOwnGraveyardTarget`，覆盖《蚀魂夜》恢复后的 stack target 指向 P1 墓地中 P2 控制脏单位时，源法术正常入墓，但不产生 `UNIT_PLAYED_TO_BASE`，脏对象仍留在 P1 graveyard。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Harrowing|SteadfastLoyalty|CruelRevival` 精确回归 9/9 通过；`Graveyard|Return` 相关回归 54/54 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3052/3052 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是墓地打出到基地结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十八批补充：墓地返回手牌结算继续收口控制权语义。`ReturnsGraveyardTargetToHand` 代表路径现在在 `TryReturnGraveyardCardToHand` 中要求目标由该墓地区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象从当前玩家墓地移入当前玩家手牌。
- 已补测试与验证：新增 `CoreRuleEngineReviveResolutionSkipsOpponentControlledOwnGraveyardTarget`，覆盖《复苏》恢复后的 stack target 指向 P1 墓地中 P2 控制脏单位时，源法术正常入墓，但不产生 `CARD_RETURNED_TO_HAND`，脏对象仍留在 P1 graveyard。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`Revive` 精确回归 3/3 通过；`Graveyard|Return` 相关回归 53/53 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3051/3051 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是墓地返回手牌结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十七批补充：己方手牌目标弃置结算继续收口控制权语义。`DiscardsTargetFromHand` 和弃置手牌额外费用共用的 `TryDiscardCardFromHand` 现在也要求目标由该手牌玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象移动到当前玩家废牌堆。既有 LeBlanc 与战场弃牌抽牌触发在选择阶段已过滤，结算 helper 兜底后语义保持一致。
- 已补测试与验证：新增 `CoreRuleEngineDarkenedLurkerResolutionSkipsOpponentControlledHandTarget`，覆盖《永黯潜伏者》恢复后的 stack target 指向 P1 手牌中 P2 控制脏对象时，源单位仍正常入场、抽牌仍执行，但不产生 `CARD_DISCARDED`，脏对象与合法手牌仍留在 P1 hand。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`DarkenedLurker` 精确回归 3/3 通过；`Discard` 相关回归 34/34 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3050/3050 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是己方手牌目标弃置的结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十六批补充：任意玩家手牌目标弃置结算继续收口控制权语义。`DiscardsTargetFromOwnerHand` 代表路径现在在结算 helper `TryDiscardCardFromAnyHand` 里也要求目标由其所在手牌区域玩家控制/legacy-owned；即使恢复/旧日志里已有脏 target 进入 stack，结算时也不会把对手控制对象移动到错误玩家废牌堆。
- 已补测试与验证：新增 `CoreRuleEngineCharmingSpiritResolutionSkipsOpponentControlledHandTarget`，覆盖《魅惑之灵》合法入栈后把 stack target 改为 P2 手牌中 P1 控制脏对象的恢复/旧日志场景，结算仍让源单位入场，但不产生 `CARD_DISCARDED`，P2 手牌和废牌堆保持正确。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`CharmingSpirit` 精确回归 5/5 通过；`Discard` 相关回归 34/34 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3049/3049 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是任意手牌目标弃置的结算侧私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十五批补充：弃置所有手牌结算继续收口控制权语义。`DISCARDS_ALL_PLAYERS_HANDS_THEN_DRAWS` 代表路径现在每名玩家只弃置自己手牌区中由自己控制/legacy-owned 的对象；脏状态把对手对象塞进该玩家手牌时，不会被 `CARDS_DISCARDED` 批量弃置事件移动到错误玩家废牌堆。
- 已补测试与验证：新增 `CoreRuleEngineRewindTimelineDiscardsOnlyControlledHandCards`，从《反转时间线》官方 fixture 派生 P1/P2 手牌各混入对手控制脏对象的场景，覆盖双方只弃置各自合法手牌、脏对象留在原手牌并在抽 4 后仍保留在 hand snapshot。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`RewindTimeline` 精确回归 2/2 通过；`Discard` 相关回归 34/34 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3048/3048 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是全手牌批量弃置结算的私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十四批补充：战场征服后弃牌抽牌触发继续收口控制权语义。`BATTLEFIELD_CONQUERED_DISCARD_DRAW` 现在自动弃置手牌时只会选择征服玩家控制/legacy-owned 的手牌对象；脏状态把对手对象塞进当前玩家手牌第一位时，不会被当作该玩家弃牌费用。
- 已补测试与验证：新增 `P79BattlefieldConquerDiscardDrawSkipsOpponentControlledHandCard`，覆盖 P1 手牌第一张为 P2 控制脏对象、第二张为 P1 合法手牌时，触发只弃置 P1 合法手牌，脏对象仍留在 P1 hand，后续抽牌仍进入手牌。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79BattlefieldConquerDiscardDraw` 精确回归 2/2 通过；`P79Battlefield` 相关回归 135/135 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3047/3047 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是战场自动触发的手牌控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十三批补充：废牌堆单位放逐结算继续收口控制权语义。《受诅咒的石棺》代表路径 `CURSED_SARCOPHAGUS_PLAY_EQUIPMENT_BANISH_GRAVEYARD_UNITS` 现在只会放逐控制者废牌堆中由该玩家控制/legacy-owned 的单位牌；脏状态把对手单位对象塞进当前玩家废牌堆时，不会被当作“你的废牌堆单位”放逐。
- 已补测试与验证：新增 `CoreRuleEngineCursedSarcophagusBanishesOnlyControlledGraveyardUnits`，从原官方 fixture 派生 P1 废牌堆第一张为 P2 控制单位的场景，覆盖结算后脏单位仍留在 P1 graveyard，`CARDS_BANISHED.cardIds` 只包含 P1 合法单位。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`CursedSarcophagus` 精确回归 4/4 通过；`Banish` 相关回归 9/9 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3046/3046 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是废牌堆自动放逐结算的私有区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十二批补充：诡术妖姬战场结果自动触发继续收口控制权语义。`BATTLEFIELD_CONQUERED_CREATE_IMAGE` / `BATTLEFIELD_HELD_CREATE_IMAGE` 现在要求传奇来源由触发玩家控制/legacy-owned；自动弃置手牌时也只会选择该玩家控制/legacy-owned 的手牌对象，脏状态把对手对象塞进当前玩家手牌或传奇区时不会被当成本玩家触发来源/费用。
- 已补测试与验证：新增 `P79LegendTriggerLeblancSkipsOpponentControlledDiscardInHand` 与 `P79LegendTriggerLeblancRequiresControlledLegendSource`，覆盖 P1 手牌第一张为 P2 控制脏对象时只弃置 P1 合法手牌，以及 P1 传奇区里的 LeBlanc 若由 P2 控制则不触发、不弃牌、不创建映像。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`P79LegendTriggerLeblanc` 精确回归 5/5 通过；`P79LegendTrigger` 相关回归 39/39 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3045/3045 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是自动传奇触发的私有区/来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十一批补充：战场征服回收符文触发继续收口控制权语义。`BATTLEFIELD_CONQUERED_RECYCLE_RUNE` 现在从征服玩家基地选择符文时要求该符文由该玩家控制/legacy-owned；脏状态把对手符文放进当前玩家基地时，不会被自动回收到当前玩家主牌堆。
- 已补测试与验证：新增 `P79BattlefieldConquerRecycleRuneSkipsOpponentControlledBaseRune`，覆盖 P1 基地第一枚符文为 P2 legacy-owned、第二枚为 P1 受控符文时，战场触发跳过脏符文，只回收 P1 受控符文，脏符文仍留在 P1 基地。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 3/3 通过；`P79Battlefield` 相关回归 134/134 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3043/3043 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是战场触发回收符文控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百二十批补充：目标牌回收结算继续收口控制权语义。`RecycleTargetCards` 现在在结算侧也要求待回收目标位于对手手牌/废牌堆且由该区域玩家控制/legacy-owned；即使结算链里已有包含脏对象的目标列表，也不会把明确属于其他玩家的对象回收到该区域玩家主牌堆。
- 已补测试与验证：新增 `CoreRuleEngineRecycleTargetsSkipsOpponentZoneCardNotControlledByThatPlayer`，覆盖《处置命令》回收结算链目标同时包含 P2 受控废牌和 P1 legacy-owned 脏废牌时，只回收 P2 受控牌，P1 脏对象仍留在 P2 废牌堆。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 4/4 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3042/3042 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是目标牌回收结算侧控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十九批补充：自动展示/弃置/回收主牌堆顶触发继续收口控制权语义。战场征服弃置 top-2、战场防守展示顶牌、战场征服展示回收 top-2、失落书库洞察回收顶牌，以及雷克塞征服展示 top-2 现在都只读取当前玩家主牌堆中连续的 controlled/legacy-owned 顶牌前缀；如果顶牌明确属于其他玩家，触发不会越过它揭示后续牌，也不会把脏对象写入展示、弃置或回收事件。
- 已补测试与验证：新增 `P79BattlefieldDefendRevealSpellSkipsOpponentControlledTopCard`，覆盖 P2 防守战场触发遇到 P1 legacy-owned 的主牌堆顶牌时，不产生展示/抽牌/回收事件，P2 手牌与主牌堆保持不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标战场/传奇展示回归 7/7 通过；`P79Battlefield|P79LegendTriggerReksai` 相关回归 136/136 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3041/3041 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是自动主牌堆展示触发的隐藏区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十八批补充：燃尽回收废牌堆继续收口控制权语义。`DrawOne` 在主牌堆为空触发燃尽并回收废牌堆时，现在只会把抽牌玩家控制/legacy-owned 的废牌堆对象洗回主牌堆；明确属于其他玩家的脏对象会留在废牌堆，避免随后被抽进当前玩家手牌或进入当前玩家主牌堆随机序列。
- 已补测试与验证：新增 `CoreRuleEngineBurnoutRecyclesOnlyControlledGraveyardCards`，覆盖 P2 主牌堆为空、废牌堆同时有 P2 受控牌和 P1 legacy-owned 脏对象时，燃尽只回收并抽到 P2 受控牌，P1 脏对象仍停留在 P2 废牌堆。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 4/4 通过；`Draw|Burnout|TurnStart` 相关回归 96/96 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3040/3040 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是废牌堆回收隐藏区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十七批补充：通用抽牌路径继续收口主牌堆控制权语义。`DrawOne` 现在在主牌堆顶对象明确不由抽牌玩家控制/legacy-owned 时停止本次抽牌，不会把对手对象移入当前玩家手牌，也不会越过脏顶牌继续暴露后续牌序。
- 已补测试与验证：新增 `CoreRuleEngineStopsTurnStartDrawAtOpponentControlledMainDeckObject`，覆盖 P2 回合开始主牌堆顶为 P1 legacy-owned 时，服务端仍推进回合开始并召出合法符文，但抽牌数量为 0，脏顶牌和后续牌仍留在 P2 main deck，比分不触发燃尽变化。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 4/4 通过；`Draw|TurnStart` 相关回归 92/92 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3039/3039 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是主牌堆隐藏区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十六批补充：召出符文路径继续收口控制权语义。正式回合开始的 rune call 与战场效果共用的 `CallRunes` 现在只会从当前玩家 rune deck 顶部连续召出由该玩家控制/legacy-owned 的符文；如果 top-N 中出现明确属于其他玩家的脏对象，召出会在该对象前停止，避免把对手符文移动到当前玩家基地并横置。
- 已补测试与验证：新增 `CoreRuleEngineStopsTurnStartRuneCallAtOpponentControlledRuneDeckObject`，覆盖 P2 回合开始 rune deck 第二张为 P1 legacy-owned 时，服务端只召出第一张 P2 受控符文，脏对象与后续符文仍留在 P2 rune deck，手牌抽牌继续按权威流程执行。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 3/3 通过；`TurnStart|CallRune` 相关回归 17/17 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3038/3038 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是 rune deck 隐藏区控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十五批补充：`PLAY_CARD` 主牌堆查看窗口继续收口控制权语义。对 `MainDeckLookCount` 代表路径，Core 现在不仅要求提交的主牌堆目标位于当前玩家 top-N 且由当前玩家控制/legacy-owned，还要求整个 top-N 查看窗口中的对象都属于该牌堆玩家；避免《预判攻势》这类“选 1 张抽、其余回收”的效果在未选中的窗口牌被脏状态跨玩家污染时，把对手对象回收到当前玩家主牌堆。
- 已补测试与验证：新增 `CoreRuleEngineRejectsPredictiveOffensiveWhenLookWindowCardIsNotControlledByDeckPlayer`，覆盖 P1 top-2 中第二张为 P2 legacy-owned 时，即使选择第一张 P1 受控牌也会被拒绝，手牌、主牌堆、结算链保持不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；《预判攻势》目标回归 3/3 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3037/3037 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是主牌堆查看窗口控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十四批补充：正式起手调度继续收口补牌阶段主牌堆控制权语义。`MULLIGAN` 在从当前玩家主牌堆顶抽取替换牌前，会确认这些待抽对象由当前玩家控制/legacy-owned；避免脏状态把 P2 legacy-owned 对象放进 P1 主牌堆顶后，被 P1 调度流程抽进手牌并写入 P1 的 object location。
- 已补测试与验证：新增 `CoreRuleEngineRejectsMulliganWhenReplacementDrawIsNotControlledByPlayer`，覆盖 P1 调度一张手牌时若待抽牌堆顶对象属于 P2，命令会被拒绝，手牌、主牌堆和调度完成列表保持不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；起手调度目标回归 3/3 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3036/3036 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是起手调度补牌来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十三批补充：正式起手调度继续收口手牌来源控制权语义。`MULLIGAN` prompt 现在不会把明确由其他玩家控制/legacy-owned 的脏手牌对象暴露为 `sources`；命令侧也要求所选对象既在当前玩家手牌中，又由当前玩家控制/legacy-owned，避免客户端绕过 prompt 把对手对象调度回当前玩家主牌堆底。
- 已补测试与验证：新增 `CoreRuleEngineRejectsMulliganWhenSelectedHandCardIsNotControlledByPlayer`，覆盖 P1 手牌中混入 P2 legacy-owned 对象时，prompt 只暴露 P1 受控手牌，直接提交脏对象会被拒绝且手牌、牌堆、调度完成列表不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；起手调度目标回归 3/3 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3035/3035 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是起手调度来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十二批补充：`PLAY_CARD` 的《圣裁之刻》保留/回收目标继续收口控制权语义。Core 的 `SACRED_JUDGMENT_KEEP_CARD` 现在在候选判断、每玩家每类别保留目标分组，以及最终回收未保留牌时，都要求对象由其所在手牌/基地/战场区域玩家控制/legacy-owned；避免脏状态把 P1 legacy-owned 对象塞进 P2 手牌或场上后，被当成 P2 的合法保留牌，或在结算时回收到 P2 主牌堆。
- 已补测试与验证：新增 `CoreRuleEngineRejectsJudgmentDayWhenKeepTargetIsNotControlledByZonePlayer`，覆盖《圣裁之刻》不会接受位于 P2 手牌但由 P1 legacy-owned 的保留目标，拒绝后 P2 手牌与结算链不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；《圣裁之刻》目标回归 3/3 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3034/3034 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌特殊保留目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十一批补充：`PLAY_CARD` 任意主牌堆顶五张目标继续收口控制权语义。Core 的 `ANY_MAIN_DECK_TOP_FIVE_CARD` 现在要求目标位于某名玩家主牌堆顶指定张数内，且由该牌堆区域玩家控制/legacy-owned；《光明未来》这类“每名玩家从主牌堆顶五张选择单位”的代表路径，在命令校验和结算映射中都复用相同区域玩家控制口径，避免脏状态把 P1 legacy-owned 单位塞进 P2 主牌堆顶后被当成 P2 的合法选择。
- 已补测试与验证：新增 `CoreRuleEngineRejectsBrightFutureWhenTopFiveTargetIsNotControlledByDeckPlayer`，覆盖《光明未来》不会接受位于 P2 主牌堆顶但由 P1 legacy-owned 的单位目标。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；《光明未来》目标回归 3/3 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3033/3033 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌主牌堆顶选择目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百一十批补充：`PLAY_CARD` 任意手牌目标继续收口控制权语义。Core 的 `ANY_HAND_CARD` 现在要求目标位于某名玩家手牌且由该手牌区域玩家控制/legacy-owned，避免脏状态把 P1 legacy-owned 对象塞进 P2 手牌后，被《魅惑之灵》这类“选择任意玩家手牌”的效果绕过使用。legacy 旧 fixture 缺少 `CardObjectState` 的手牌对象仍按既有兼容路径处理。
- 已补测试与验证：新增 `CoreRuleEngineRejectsCharmingSpiritWhenAnyHandTargetIsNotControlledByHandPlayer`，覆盖《魅惑之灵》不会接受位于 P2 手牌但由 P1 legacy-owned 的任意手牌目标。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 1/1 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3032/3032 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌任意手牌目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零九批补充：`PLAY_CARD` 对手私有区域目标继续收口控制权语义。Core 现在对 `OPPONENT_HAND_CARD`、`OPPONENT_GRAVEYARD_CARD`、`OPPONENT_MAIN_DECK_TOP_CARD` 使用“位于对手私有区域且由该区域玩家控制/legacy-owned”的判断，避免脏状态把 P1 legacy-owned 对象放进 P2 手牌、废牌堆或牌堆顶后，被 P1 绕过 prompt 当作对手私有区目标。prompt 侧对手废牌堆目标也同步过滤到相同控制权口径；对手手牌/牌堆顶仍不主动暴露候选。
- 已补测试与验证：新增 `CoreRuleEngineRejectsBoneSkewerWhenOpponentHandTargetIsNotControlledByOpponent`，覆盖《透骨尖钉》不会接受 P1 legacy-owned、但位于 P2 手牌的对手手牌单位目标，拒绝后状态不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 1/1 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3031/3031 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌对手私有区域目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零八批补充：`PLAY_CARD` 敌方目标继续收口控制权语义。Core 的 `ENEMY_UNIT`、`ENEMY_BATTLEFIELD_UNIT` 和相关敌方装备/战场目标现在要求目标位于对手区域且由该区域玩家控制/legacy-owned，而不是只看对象被放在哪个对手区域；`ActionPromptBuilder` 同步用区域玩家控制语义过滤 enemy field/equipment target choices，避免 `ownerId = P1`、controller 为空的脏对象放进 P2 区域后被 P1 当成敌方目标。
- 已补测试与验证：新增 `CoreRuleEngineRejectsHighwayRobberyWhenEnemyTargetIsNotControlledByOpponentZonePlayer`，扩展 `ActionPromptPlayCardMetadataFiltersEnemyTargetsByZoneController`，覆盖《巧取豪夺》不会暴露或接受由行动玩家 legacy-owned、但位于对手区域的敌方目标。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 2/2 通过；`PlayCard` 相关回归 44/44 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3030/3030 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌敌方目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零七批补充：`PLAY_CARD` 私有区域目标继续收口控制权语义。Core 现在对 `FRIENDLY_HAND_CARD`、`FRIENDLY_MAIN_DECK_CARD`、`FRIENDLY_GRAVEYARD_CARD` 以及相关手牌可选费用使用“位于当前玩家私有区域且由当前玩家控制/legacy-owned”的判断，避免脏状态把 `controllerId = P2` 的对象放进 P1 手牌、牌堆或废牌堆后，被当成 P1 的友方私有区域目标。`ActionPromptBuilder` 同步过滤友方手牌/废牌堆目标，前端仍只展示服务端 target choices，不推断私有区规则。
- 已补测试与验证：新增 `CoreRuleEngineRejectsHelpArrivesWhenFriendlyHandTargetIsOpponentControlled`，扩展 `ActionPromptPlayCardMetadataFiltersFriendlyHandTargetsByController`，覆盖《前来相助》不会暴露或接受对手控制但位于当前玩家手牌的友方手牌单位目标。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 2/2 通过；`PlayCard` 相关回归 43/43 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3028/3028 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌私有区域目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零六批补充：`PLAY_CARD` 友方目标继续收口控制权语义。Core 命令侧现在对 `FRIENDLY_UNIT`、友方/敌方组合目标、友方战场单位、友方基地单位和友方装备目标使用“在己方区域且由行动玩家控制/legacy-owned”的判断，避免脏状态把 `controllerId = P2` 的单位放进 P1 基地或战场后，被绕过 prompt 当成 P1 友方目标、友方费用目标或相关栈目标条件。prompt 侧既有 controller 过滤继续由 shape 测试锁定，前端仍只消费服务端 `sourceRequirements` / target choices。
- 已补测试与验证：新增 `CoreRuleEngineRejectsArenaRookieWhenFriendlyTargetIsOpponentControlled`，扩展 `ActionPromptPlayCardMetadataFiltersFriendlyTargetsByController`，覆盖对手控制但位于当前玩家区域的友方目标不会进入服务端候选，且直接提交会被命令侧拒绝并保持状态不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 2/2 通过；`PlayCard` 相关回归 42/42 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3026/3026 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌目标控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零五批补充：传奇行动来源、目标和费用减免继续收口控制权语义。`LEGEND_ACT` source 现在使用同一 control/legacy-owned helper，避免 controller 为空但 owner 指向对手的传奇对象被误当成当前玩家来源；友方单位目标、pending friendly 目标、武装第二目标和 Lillia 类友方 ephemeral 减费也会要求对象由行动玩家控制，而不是仅位于当前玩家区域。prompt 与 command 口径同步，前端仍只消费服务端 `sourceRequirements`。
- 已补测试与验证：新增 `P79LegendActYasuoRejectsOpponentControlledTargetInPlayerZone`，扩展 `ActionPromptLegendActMetadataFiltersSourcesAbilitiesTargetsAndCosts`，覆盖脏状态来源/目标不会进入可提交路径，且命令侧拒绝对手控制目标并保持状态不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 2/2 通过；`LegendAct` 相关回归 37/37 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3024/3024 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是传奇行动控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零四批补充：装配装备继续收口控制权语义。`ASSEMBLE_EQUIPMENT` prompt 不再暴露明确由其他玩家控制的长剑来源；命令侧代表性长剑装配从“owner 与 controller 都必须匹配区域玩家”收紧/修正为“必须由区域玩家控制，legacy 空 owner/controller 兼容”。这避免对手控制装备出现在前端候选，也允许规则上由 P1 控制但所属者仍为 P2 的单位作为 P1 的受控装配目标。前端仍只展示和提交服务端 `sourceRequirements` 候选，不自行判断控制权。
- 已补测试与验证：扩展 `ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower`，继续锁定对手控制长剑不进入 prompt；新增 `P5EquipmentStateAssembleLongSwordAllowsControlledOpponentOwnedTarget`，并回归既有 `P5EquipmentStateAssembleLongSwordRejectsControllerMismatchWithoutSideEffects`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 3/3 通过；`AssembleEquipment` 相关回归 32/32 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3023/3023 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是装配控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零三批补充：出牌来源继续做 command-side control hardening。`PLAY_CARD` 现在在服务端命令侧拒绝明确由其他玩家控制的手牌来源；即使脏状态把 `controllerId = P2` 的卡牌对象放进 P1 手牌，客户端也不能绕过 prompt 打出该对象。legacy 旧 fixture 中手牌来源没有 `CardObjectState` 或 owner/controller 为空的路径仍保留兼容，不把历史数据误判为对手对象。`ActionPromptBuilder` 同步过滤这些脏手牌来源，前端仍只展示服务端候选。
- 已补测试与验证：新增 `P4PlayCardCommandRejectsOpponentControlledSourceInPlayerHand`，扩展 `ActionPromptFiltersPlayCardSourcesByImplementedTimingAndBaseCost`，覆盖 prompt 不暴露对手控制手牌来源、绕过 prompt 直接提交也会稳定拒绝且状态不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 2/2 通过；`PlayCard` 相关回归 41/41 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3022/3022 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是出牌来源控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零二批补充：声明战斗参与者继续做 command-side control hardening。`DECLARE_BATTLE` 的攻击者必须由行动玩家控制，防守者必须由其所在对手战场区域玩家控制；即使脏状态把 `controllerId = P2` 的单位放进 P1 战场，或把 `controllerId = P1` 的单位放进 P2 战场，客户端也不能绕过 prompt 把它们作为攻防参与者提交。`ActionPromptBuilder` 同步把 `DECLARE_BATTLE` 候选过滤口径更新为 “battlefield-zone-controlled-face-up-units-not-already-in-combat”，前端仍只展示服务端候选，不新增本地裁决。
- 已补测试与验证：新增 `P4DeclareBattleCommandRejectsOpponentControlledAttackerInPlayerBattlefield` 与 `P4DeclareBattleCommandRejectsActingPlayerControlledDefenderInOpponentBattlefield`，扩展 `ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts` 并更新 `P79CombatPromptFiltersDeclareBattleCandidatesToLegalBattlefieldUnits` 的 metadata 契约。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 3/3 通过；`DeclareBattle` 相关回归 59/59 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3021/3021 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是声明战斗参与者控制权护栏，完整 cleanup queue、官方级战斗/法术对决任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百零一批补充：激活能力来源继续做 command-side control hardening。Vi 双倍战力与 Xerath 伤害两个代表性 `ACTIVATE_ABILITY` resolver 现在都会在服务端命令侧校验来源对象必须由行动玩家控制；即使脏状态把 `controllerId = P2` 的 Vi/Xerath 放进 P1 的基地或战场区域，客户端也不能绕过 prompt 激活能力。prompt 既有 controller 过滤继续由 shape 测试锁定，前端不新增本地裁决。
- 已补测试与验证：新增 `P4ActivateAbilityCommandRejectsViOpponentControlledSourceInPlayerZone` 与 `P4ActivateAbilityCommandRejectsXerathOpponentControlledSourceInPlayerZone`，扩展 `ActionPromptActivateAbilityMetadataFiltersSourcesTargetsAndSpellshieldTax` 证明这些脏对象不会成为服务端 `sourceRequirements` 来源。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 3/3 通过；`ActivateAbility` 相关回归 64/64 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3019/3019 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是激活能力来源控制权护栏，完整 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第一百批补充：移动单位来源继续做 command-side control hardening。`MOVE_UNIT` 粗粒度移动与精确 `ROAM` 战场移动现在都会在服务端命令侧校验来源对象必须由行动玩家控制；即使持久化或测试脏状态把 `controllerId = P2` 的单位放进 P1 的 `Base/Battlefields` 区域，客户端也不能绕过 prompt 移动该对象。`ActionPromptBuilder` 既有 controller 过滤已通过扩展 shape 测试锁定，前端仍只展示服务端候选，不新增本地规则判断。
- 已补测试与验证：新增 `P4MoveUnitCommandRejectsOpponentControlledSourceInPlayerZone` 与 `P4MoveUnitCommandRejectsOpponentControlledPreciseRoamSourceInPlayerZone`，扩展 `ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；新增目标回归 3/3 通过；`MOVE_UNIT` 相关回归 43/43 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3017/3017 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是移动来源控制权护栏，完整 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第九十九批补充：待命暗置/翻开来源继续做 command-side hidden-info hardening。`HIDE_CARD` 与 `REVEAL_CARD` 现在不仅要求来源对象位于当前玩家手牌/基地并有已知 `cardNo`，还会在服务端命令侧拒绝明确由其他玩家控制的来源；`ActionPromptBuilder` 同步过滤这些脏状态来源，避免前端看到可提交候选。legacy 旧 fixture 中 owner/controller 为空的对象仍按既有兼容路径处理，不把历史数据误判为对手对象。
- 已补测试与验证：新增 `P4HideCardCommandRejectsOpponentControlledSourceInPlayerHand`、`P4RevealCardCommandRejectsOpponentControlledSourceInPlayerBase`，并扩展 `ActionPromptFiltersHideCardSourcesByPayableStandbyCosts` 与 `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby`，覆盖 prompt 不暴露对手控制来源、绕过 prompt 直接提交也会稳定拒绝且状态不变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；本批目标回归 4/4 通过；`HIDE_CARD`/`REVEAL_CARD` 全相关回归 66/66 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 后端 full test 3015/3015 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是待命来源控制权护栏，完整 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0 胜负/投降收口补充：服务端新增权威 `SURRENDER` 命令与 prompt 候选，`CoreRuleEngine` 在比赛进行中接受任一已入座玩家投降，直接以对手为 `WinnerPlayerId`、状态置为 `FINISHED` 并广播 `MATCH_WON`，payload 记录 `winnerPlayerId`、`surrenderedPlayerId`、`reason = SURRENDER`。`MatchSession` 的命令结算后 prompt builder 与 `ResolutionResult.BuildPrompts` 均会在进行中 prompt 追加全局让步动作；conformance fixture 对比层忽略该元操作，避免历史规则 fixture 把“可投降”误判为规则动作变化。前端只展示服务端候选，点击投降先弹确认框，服务端确认 finished snapshot 后跳转只读结果页。
- 已补测试与 smoke：新增/扩展 `CoreRuleEngineSurrenderFinishesMatchAndOpponentWins`、prompt shape 与 `GameHubJoinTests` 断言，验证等待、优先权、焦点、战斗任务等窗口仍保留原服务端动作并额外允许 `SURRENDER`；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3013/3013；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 已优先尝试但本地 IAB backend 未发现；未使用 Computer Use 抢前台。后台 headless Chrome/CDP smoke 房间 `smoke-surrender-debug-1778196461` 通过：P2 Web UI 看到服务端 `WAIT,SURRENDER` prompt，点击“投降”触发确认框；SignalR 观察到 `MATCH_WON`，authoritative snapshot 为 `winnerPlayerId = P1`、`roomStatus = FINISHED`；页面自动进入结果页并显示 `胜者：P1 / FINISHED`，reload/reconnect 后仍恢复同一结果，浏览器错误为 0；smoke 后确认 5092/5093/5094/5175/5176/9223/9224 无监听。整体仍 **NOT READY**，因为这是投降胜负路径收口，完整官方胜负/燃尽矩阵、统一 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-003 第九十八批补充：前端卡牌详情继续降低 BehaviorSpec 口径误导风险。详情抽屉现在在“服务端证据”中展示 `implementedEffectKind`、`implementedByCardNo`、`templateIds` 和前 4 条 `effects` 的状态/原因，并对这些数组字段做空数组兜底；这只是把 API 已有代表性证据更清楚地展示给测试者，不把代表性通过提升为完整官方通过，也不让前端自行判断卡牌规则完备性。
- 已补验证：`source ../../scripts/dev-env.sh && npm run build` 通过，静态事件标签守护仍覆盖 109 个后端事件。已优先尝试 Browser Use，但本地 IAB backend 未发现；为避免抢用户前台，没有使用 Computer Use 操控前台 Chrome。后台 headless Chrome/CDP smoke 房间 `smoke-card-detail-evidence-mow2agx2` 通过：P1 Web UI 连接房间，后台 P2 SignalR 入座并 seed `typed-power-payment`，点击《弹幕时间》后详情显示 `BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT`、`OGN·268/298`、`damage` 和 effect 明细；reload/reconnect 后恢复同一证据且无浏览器 error。整体仍 **NOT READY**，因为这是 P1-003 展示口径收口，完整官方 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-005/P1-004 第九十七批补充：补齐正式起手调度的服务端候选与前端选择闭环。此前 `MULLIGAN` prompt 只暴露动作本身，前端全局行动面板只能提交 `handObjectIds: []`，实际等同于“全部保留”，无法让玩家从当前起手牌中选择 0-2 张调度。现在 `ActionPromptBuilder` 会把当前玩家手牌作为 `MULLIGAN.sources` 公开，并在 metadata 中给出 `sourcePolicy = opening-hand-cards`、`minSelectionCount = 0`、`maxSelectionCount = 2`；前端 `ActionPanel` 只渲染这些服务端来源和上限，提交的 `handObjectIds` 只来自当前 prompt 候选，不读取卡面或本地规则自行裁决。
- 已补测试与验证：扩展 `OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub`，断言 `MULLIGAN.sources` 与 active player 当前手牌一致且 metadata `maxSelectionCount = 2`；修复前该断言失败为候选 sources 为空，修复后通过。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；起手调度目标回归 3/3 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3011/3011；`source ../../scripts/dev-env.sh && npm run build` 通过。
- 后台 headless Chrome/CDP smoke：已优先尝试 Browser Use，但本地 IAB backend 未发现；为避免抢用户前台，没有使用 Computer Use 操控前台 Chrome。API `http://127.0.0.1:5093` 与 Vite `http://127.0.0.1:5175`，房间 `smoke-mulligan-mow0wz4v`。后台 SignalR 准备正式 P1/P2 房间并推进到 P1 起手调度，P1 Web UI 显示 4 个服务端 `MULLIGAN.sources` 候选和“已选 0 / 2”；点击第一张后显示“已选 1 / 2 / 将调度”，点击“确认起手调整”后事件日志显示“P1 完成起手调度 / 双方完成起手调度，开始第一个回合”。额外 SignalR 校验 authoritative snapshot 中 P1 `mulliganCompleted = true`、被选对象 `P1-MAIN-OGN-008-298-26` 不再在手牌中、手牌仍为 4 张、prompt 收敛为 `WAIT`；reload/reconnect 后 UI 恢复 `Prompt：smoke-mulligan-mow0wz4v:5:P1:WAIT` 且不再出现调度按钮。整体仍 **NOT READY**，因为这是正式开局 UI/Prompt 的产品级收口，完整官方 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第九十六批补充：补齐 recovery spectator replay frame 与 authoritative state 的 validator guard。此前 Postgres recovery store 会基于最终 authoritative state 生成公开 spectator replay frame，但 `MatchRecoveryValidator` 只校验 command/event/player view/authoritative state 本体，没有校验 spectator frame 的 room、tick、event sequence、authoritative hash 是否和同一恢复帧对齐。现在 validator 会检查 spectator frame 必须绑定同一 room、同一 last event sequence、同一 tick/currentTick、同一 `MatchStateHasher` hash，并继续拒绝 timing 中泄漏 `seed` / `rngCursor`。
- 已补测试与验证：新增 `RecoveryValidatorAcceptsMatchingSpectatorReplayFrame` 和 `RecoveryValidatorRejectsSpectatorReplayFrameMismatch`，覆盖匹配 frame 通过，以及 event sequence、tick、hash 分叉时返回明确错误。`MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests` 目标回归 24/24 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3011/3011；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是恢复/回放一致性护栏，完整官方 cleanup queue、战场/法术对决/战斗状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十五批补充：补齐 Vi 与 Xerath 代表性 `ACTIVATE_ABILITY` 成功后的 `ObjectLocations` 同步。此前 Vi 技能会正确支付费用并加入结算链，Xerath 技能会正确支付费用、横置来源并加入结算链，但两个成功路径都不会补齐来源/目标对象位置索引；如果初始状态没有 `ObjectLocations`，重连/回放只能依赖 snapshot fallback，authoritative state 自身索引仍不完整。现在这两个激活成功路径都会按当前 `PlayerZones` reconcile `ObjectLocations`。
- 已补测试与验证：扩展 `P4ActivateAbilityCommandAddsViDoublePowerSkillToStack` 和 `P4ActivateAbilityCommandAddsXerathDamageSkillWithSpellshieldTaxToStack`，断言 Vi 来源为 `BASE`，Xerath 来源与目标单位为 `BATTLEFIELD`。修复前两条断言分别失败为 `KeyNotFoundException: P1-UNIT-VI` 与 `KeyNotFoundException: P1-UNIT-XERATH`，修复后通过。`ActivateAbility` 相关回归 59/59 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3009/3009；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性激活能力对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十四批补充：补齐蜕变花园授予的 `ACTIVATE_ABILITY` 成功后的 `ObjectLocations` 同步。此前 `BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE` 会正确横置战场单位并增加经验，但 resolver 返回的新 authoritative state 不会补齐当前战场对象位置索引；如果初始状态没有 `ObjectLocations`，重连/回放只能靠 snapshot fallback，状态自身索引仍不完整。现在该能力成功路径会按当前 `PlayerZones` reconcile `ObjectLocations`。
- 已补测试与验证：扩展 `P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience`，断言蜕变花园战场对象和获得经验的单位在成功后都带 `BATTLEFIELD` 对象位置。该断言修复前失败为 `KeyNotFoundException: P1-BATTLEFIELD-MUTATION-GARDEN`，修复后通过。`ActivateAbility` / `BattlefieldUnitExperienceAbility` 相关回归 62/62 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3009/3009；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性战场授予能力对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十三批补充：补齐 standalone `RECYCLE_RUNE` 成功后的 `ObjectLocations` 同步。此前基础符文回收会把来源符文从基地移到符文牌堆底并直接标记来源为 `RUNE_DECK`；但如果初始状态没有完整对象索引，同一符文牌堆中已有的底部符文仍不会进入 authoritative `ObjectLocations`。现在 `RECYCLE_RUNE` 成功路径会按最终 `PlayerZones` reconcile 对象位置，再显式保留来源符文为 `RUNE_DECK`。
- 已补测试与验证：新增 `CoreRuleEngineRecyclesBasicRuneAndReconcilesObjectLocations`，构造无初始 `ObjectLocations` 的红符文回收场景，断言来源符文与原符文牌堆底部符文最终都标记为 `RUNE_DECK`。该断言修复前失败为 `KeyNotFoundException: P1-RUNE-BOTTOM-RECYCLE-LOCATION`，修复后通过。Tap/Recycle/PaymentResource 相关回归 11/11 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3009/3009；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是基础资源动作对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十二批补充：补齐 `TAP_RUNE` 成功后的 `ObjectLocations` 同步。此前基础符文横置会正确横置来源并增加 1 法力，但若权威状态中尚未有该 rune 的对象位置索引，resolver 返回后 `PlayerZones.Base` 有该符文、`CardObjects` 已横置，`ObjectLocations` 仍缺失该对象。现在 `TAP_RUNE` 成功路径会按当前 `PlayerZones` reconcile 对象位置，保证基础资源动作后的 authoritative index 完整。
- 已补测试与验证：新增 `CoreRuleEngineTapsBasicRuneAndReconcilesObjectLocation`，构造无初始 `ObjectLocations` 的基础红符文横置场景，断言横置成功、P1 法力为 1、符文横置且对象位置为 `BASE`。该断言修复前失败为 `KeyNotFoundException: P1-RUNE-RED-TAP`，修复后通过。Tap/Recycle/Jinx 相关回归 6/6 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3008/3008；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是基础资源动作对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十一批补充：补齐 Jinx 回合开始触发抽牌后的 `ObjectLocations` 同步。此前 `ResolveTurnStart` 会在普通召符文/抽牌后先 reconcile 对象位置，随后 Jinx 传奇“手牌少于两张时额外抽牌”又通过 `ApplyDrawToPlayer` 改动 `PlayerZones`；这会导致第二张触发抽到的牌已在 hand 中，但 authoritative `ObjectLocations` 仍可能保留 `MAIN_DECK`，影响重连、回放和前端按对象位置恢复。
- 已补测试与验证：扩展 `P79LegendTriggerJinxDrawsAtTurnStartWhenHandBelowTwo`，为 Jinx 回合开始场景补初始 `ObjectLocations`，并断言普通抽牌与 Jinx 触发抽牌两张对象最终都为 `HAND`。该断言修复前失败为 `Expected: HAND / Actual: MAIN_DECK`，修复后通过。目标窄测 1/1 通过；TurnStart/Jinx/LegendAct 相关回归 47/47 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3007/3007；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性回合开始触发抽牌对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第九十批补充：补齐 `REVEAL_CARD` 待命反应进栈后的 `ObjectLocations` 同步。此前待命牌以 `STANDBY_REACTION` 翻开作为反应加入结算链时，服务端会从基地移除来源牌并创建 stack item，但 authoritative `ObjectLocations` 仍可能保留旧 `BASE` 位置。现在 `REVEAL_CARD` 在反应进栈时会 reconcile 最终区域并显式把来源对象标记为 `STACK`。
- 已补测试与验证：扩展 `P4RevealCardCommandPlaysStandbyReactionToStack`，为面朝下提莫待命牌补初始 `ObjectLocations`，并断言翻开反应后来源对象位置为 `STACK`。该断言修复前失败为 `Expected: STACK / Actual: BASE`，修复后通过。`FullyQualifiedName~RevealCard` 42/42 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3007/3007；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性待命反应对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十九批补充：补齐 `LEGEND_ACT` 后的 `ObjectLocations` 同步。此前传奇行动可直接抽牌、移动单位、创建衍生物或回手单位，并正确更新 `PlayerZones` / `CardObjects`；但 resolver 返回时没有用最终区域重建对象位置索引，导致 Poppy 抽到手里的牌仍可能在 authoritative `ObjectLocations` 中标记为 `MAIN_DECK`，Yasuo/Pyke/Viktor 等传奇行动也存在同类重连/回放索引风险。现在 `LEGEND_ACT` 返回 state 时统一 `ReconcileObjectLocations(state.ObjectLocations, playerZones)`。
- 已补测试与验证：扩展 `P79LegendActSpendsExperienceExhaustsLegendAndDraws`，为 Poppy 抽牌场景补初始 `ObjectLocations` 并断言 `P1-LEGEND-DRAW-001` 最终索引为 `HAND`。该断言修复前失败为 `Expected: HAND / Actual: MAIN_DECK`，修复后通过。`FullyQualifiedName~LegendAct` 36/36 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3007/3007；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性传奇行动对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十八批补充：补齐 `PLAY_CARD` 额外费用移动后的 `ObjectLocations` 同步。此前出牌 resolver 会先为来源牌建立 stack 位置索引，再执行“摧毁友方单位”“返回友方装备”“弃置手牌”等额外费用移动；这些移动会更新 `PlayerZones`，但最终 `ObjectLocations` 仍可能保留旧位置。现在出牌 resolver 在所有额外费用、触发抽牌和事件构建后，用最终 `playerZones` 重新 reconcile 对象位置，并保留来源牌的 `STACK` 位置。
- 已补测试与验证：新增 `CoreRuleEngineReconcilesObjectLocationsForDestroyedAdditionalCost`，构造《牺牲》摧毁友方强力单位作为额外费用，断言费用单位最终在 P1 墓地且 `ObjectLocations.Zone = GRAVEYARD`，来源法术仍为 `STACK`。该断言修复前失败为 `Expected: GRAVEYARD / Actual: BATTLEFIELD`，修复后通过。额外费用/出牌相关回归 15/15 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3007/3007；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性出牌额外费用对象索引收口，完整官方 cleanup queue、全路径 PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十七批补充：补齐回合开始战场伤害 cleanup 后的 `ObjectLocations` 同步。此前冰霜要塞等战场在回合开始对全部战场单位造成伤害时，状态性 cleanup 能把致命单位从战场移入墓地，但 `ResolveTurnStart` 随后从旧 `state.ObjectLocations` 直接复制对象索引，可能让 authoritative `PlayerZones` 显示对象已入墓、而 `ObjectLocations` 仍显示该对象在战场，影响重连、回放和前端按对象位置恢复。
- 已补测试与验证：扩展 `P79BattlefieldTurnStartDamageAllBattlefieldUnitsBeforeScoring`，为冰霜要塞场景补齐初始 `ObjectLocations`，并断言被致命伤害清理的 `P1-BATTLEFIELD-FROST-FALLING` 最终索引同步为 `GRAVEYARD`。该断言修复前失败为 `Expected: GRAVEYARD / Actual: BATTLEFIELD`，修复后通过。turn-start/cleanup 相关回归 12/12 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3006/3006；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性回合开始 cleanup 的对象索引收口，完整官方 cleanup queue、战场任务生命周期、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十六批补充：补齐 Xerath 技能伤害结算后状态性 cleanup 的符能池回传。此前 `ResolveXerathDamageAbilityStackItem` 会在技能造成致命伤害后调用 `RunStateBasedCleanupLoop`，Sett 这类摧毁替代效果可以正确召回单位、消耗增益并广播 `COST_PAID`；但 resolver 返回的 `StackResolutionResult` 仍携带旧 `state.RunePools`，导致替代效果支付的 1 法力没有落入最终 authoritative state。现在 Xerath 技能 resolver 会把 cleanup 返回的 `RunePools` 一并带回栈结算结果。
- 已补测试与验证：新增 `P79LegendTriggerSettReplacementDebitsManaAfterXerathSkillCleanup`，构造 P2 Xerath 技能栈项目对 P1 带增益单位造成致命伤害，并断言 Sett 替代路径后 P1 法力从 1 扣到 0、单位休眠召回基地、增益被消耗、墓地不出现该单位，且 `DAMAGE_APPLIED` / `COST_PAID` 事件存在。该测试修复前失败为 `Expected: 0 / Actual: 1`，修复后通过。目标回归 6/6 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3006/3006；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是代表性技能伤害 cleanup 的资源回传收口，完整官方 cleanup queue、替代效果全路径、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第八十五批补充：补齐已结束比赛后 `SUBMIT_DECK` 的错误语义 guard。此前 `SubmitDeckAsync` 把 `IN_PROGRESS` 与 `FINISHED` 合并为 “deck cannot be changed after the match starts.” / `PhaseNotAllowed`；比赛结束后前端或旧 intent 再提交卡组时，服务端没有清晰表达“比赛已结束”。本批拆分 finished 分支，`SUBMIT_DECK` 在 `MatchStatuses.Finished` 下稳定返回 `MATCH_FINISHED / match already finished`，进行中比赛仍保留“开局后不能改卡组”的 `PhaseNotAllowed`。
- 已补测试与验证：继续扩展 `P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins`，在 `MATCH_WON` 与 finished snapshot 后额外提交 `SUBMIT_DECK`，断言 Hub 返回 `MATCH_FINISHED` 且不广播事件。目标窄测 1/1 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3005/3005；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是胜负后卡组提交错误语义收口，完整官方胜负/燃尽矩阵、战场/法术对决/战斗任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P1-004 第八十四批补充：补齐已结束比赛后 Hub/session 普通 intent 的错误语义 guard。此前 `MatchSession.SubmitAsync` 在 `Status != IN_PROGRESS` 时统一抛 `MatchNotStarted`；比赛已经 `FINISHED` 后再次提交普通游戏命令虽然不会落状态，但前端会收到“match has not started”，容易把结束状态误解为未开局或重连失败。本批将 `FINISHED` 明确映射为 `MatchFinished / match already finished`，未开局房间仍保留原 `MatchNotStarted` 语义。
- 已补测试与验证：扩展 `P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins`，先通过战场据守七单位路径产生 `MATCH_WON` 与 `MatchStatuses.Finished`，再提交 `PASS_PRIORITY`，断言 Hub 只返回 `MATCH_FINISHED` 错误且不广播事件；同步回归 `SubmitIntentBeforeReadyReturnsStableErrorCode`，确认未开局仍返回 `MATCH_NOT_STARTED`。目标窄测 2/2 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3005/3005；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是胜负后错误语义与前端状态恢复边界收口，完整官方胜负/燃尽矩阵、战场/法术对决/战斗任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十三批补充：补齐已结束比赛下 `MULLIGAN` 的特殊分发 guard。此前 Core 的 `MULLIGAN` resolver 早于全局 `Status != IN_PROGRESS` 检查；虽然真实 `MatchSession.SubmitAsync` 已能挡住大多数已结束比赛提交，但手写 Core 状态若同时为 `FINISHED` 与 `MULLIGAN` phase，仍可能绕过普通游戏命令 guard 进入起手调度 resolver。本批把比赛状态检查前置到所有 Core 游戏命令之前，`MULLIGAN` 也统一在已结束比赛返回 `PhaseNotAllowed`，并保持胜者、tick、phase、起手完成列表和区域不变。
- 已补测试与验证：新增 `CoreRuleEngineRejectsMulliganAfterMatchFinished`，覆盖 `MatchStatuses.Finished`、`WinnerPlayerId` 已设置且 phase 被构造成 `MULLIGAN` 时提交 `MULLIGAN` 不会推进起手调度、不改变区域、不清空胜者；同时回归 `CoreRuleEngineRejectsGameCommandAfterMatchFinished` 与 `OfficialMulligan`。本批窄测 4/4 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3005/3005；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是胜负后特殊命令分发 guard，完整官方胜负/燃尽矩阵、战场/法术对决/战斗任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P1-004 第八十二批补充：补齐已结束比赛的全局命令侧 guard。此前部分具体 resolver 自身有窗口 guard，但 Core 没有统一拒绝 `Status != IN_PROGRESS` 的游戏命令；如果上层或恶意客户端在胜负结算后继续提交 `END_TURN` / 出牌等命令，仍可能进入某些 resolver。现在 Core 在普通游戏命令分发前统一检查比赛状态，已结束比赛返回 `PhaseNotAllowed` 且状态、胜者、区域和 tick 均不变。
- 已补测试与验证：新增 `CoreRuleEngineRejectsGameCommandAfterMatchFinished`，覆盖 `MatchStatuses.Finished` 且 `WinnerPlayerId` 已设置后提交 `END_TURN` 不会推进回合、不清空胜者、不改变区域。finished guard 窄测 3/3 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3004/3004；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是胜负后 command guard，完整官方胜负/燃尽矩阵、战场/法术对决/战斗任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第八十一批补充：补齐 `TURN_START` 自动推进的命令侧玩家身份 guard。既有 fixture 通过占位命令触发服务端自动执行回合开始流程，本批保留该服务端自动流程，但要求只有 `TurnPlayerId` 本人能触发；非当前回合玩家提交任何占位命令都以 `PhaseNotAllowed` 拒绝且不召符文、不抽牌、不推进 tick。
- 已补测试与验证：新增 `CoreRuleEngineRejectsTurnStartAdvanceFromNonTurnPlayer`，覆盖 P2 回合开始状态下 P1 尝试用 `PASS_PRIORITY` 触发回合开始会被拒绝、P2 rune/main deck/base/hand 均保持不变；既有 `CoreRuleEngineResolvesP2TurnStartPreflightFixture` 等 turn-start 回归仍通过。turn-start guard 窄测 4/4 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3003/3003；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是回合开始自动流程的 command guard，完整战场/法术对决/战斗任务状态机、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003 第八十批补充：回合结束特殊清理后接入状态性 cleanup loop。此前 `ApplyTurnEndCleanup` 会清除伤害、直到回合结束效果和临时战力修正，但如果某个单位依靠临时正向战力修正维持在 1 战力，修正过期后变为 0 战力，服务端会直接进入下一名玩家回合，没有立即执行 `ZERO_POWER` 状态清理。本批在 turn-end cleanup 后、turn-start 前运行 `RunStateBasedCleanupLoop`，并同步 `ObjectLocations`，使临时修正过期造成的 0 战力场上单位立即入墓，事件顺序保持在回合开始前。
- 已补测试与验证：新增 `CoreRuleEngineRunsStateBasedCleanupAfterTurnEndPowerExpires`，覆盖 `POWER_MODIFIER_EXPIRED` 后同一回合结束流程内广播 `UNIT_DESTROYED` / `ZERO_POWER`、对象从基地移到墓地、`ObjectLocations` 同步为 `GRAVEYARD`，并锁定事件顺序为修正失效 -> 状态性摧毁 -> `TURN_PLAYER_ADVANCED` -> `TURN_START_BEGAN`。turn-end/cleanup 窄测 3/3 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3002/3002；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这只补上 turn-end cleanup 的代表路径，替代效果、全部进出战场路径、完整 central task queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-003/P0-004 第七十九批补充：补齐 `END_TURN` 命令侧窗口与玩家身份 guard。此前服务端 prompt 通常只会把结束回合暴露给当前行动玩家，但 Core 分发层只检查 `Phase == MAIN`；手写命令可在非行动玩家视角或闭环/栈窗口直接提交 `END_TURN`，绕过前端 prompt 推进回合。本批要求 `END_TURN` 必须同时满足当前阶段为 `MAIN`、timing 为 `NEUTRAL_OPEN`、提交者同时是 `ActivePlayerId` 与 `TurnPlayerId`、且结算链为空，否则以 `PhaseNotAllowed` 拒绝且状态不变。
- 已补测试与验证：新增 `CoreRuleEngineRejectsEndTurnFromNonActivePlayer` 与 `CoreRuleEngineRejectsEndTurnDuringClosedPriorityWindow`，覆盖非当前行动玩家和闭环栈窗口两条绕过路径；合法 `END_TURN` 与 turn-end cleanup 代表回归 5/5 通过。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 3001/3001；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，也没有启动 browser/API/Vite/Chrome smoke 进程。整体仍 **NOT READY**，因为这是命令侧窗口 guard，完整 battlefield/control task 状态机、central cleanup queue、PaymentEngine、LayerEngine 与全官方卡牌证据仍未清零。

- P0-005 第七十八批补充：继续收紧 `PLAY_CARD` 命令侧的无权威身份边界。此前 prompt/Hub 已不会把 `cardNo = null` 的出牌来源或目标暴露给前端，但 Core 的手写 `PLAY_CARD` 路径仍可能接受客户端自行提交的卡号、来源对象或公开区域目标。本批要求：若来源对象存在于 `CardObjects`，其 `CardNo` 必须非空且必须与命令提交的 `CardNo` 完全一致；若目标对象是带 owner/controller 的公开卡牌对象，则也必须有非空 `CardNo`，否则以 `InvalidTarget` 拒绝且状态不变。兼容旧 fixture 的匿名占位对象仍不被当作公开卡牌身份处理。
- 已补测试与 fixture 数据：新增 `P4PlayCardCommandRejectsSourceWithoutCardNo`、`P4PlayCardCommandRejectsSourceCardNoMismatch`、`P4PlayCardCommandRejectsTargetWithoutCardNo`，覆盖未知来源、来源卡号不匹配、未知公开目标三条手写命令绕过路径。同步给旧 conformance fixture 中本来代表公开合法单位/武装/符文/目标的对象补齐 `cardNo`，避免把合法公开对象误建模为隐藏身份对象。
- 验证结果：未知来源/目标防回归目标测试 7/7 通过；上一轮全量中暴露的 12 个合法 fixture 回归通过 12/12；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2999/2999；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 118/118；`source ../../scripts/dev-env.sh && npm run build` 通过。本批没有前端 UI 代码变更，未启动新的 browser/API/Vite/Chrome smoke 进程；仍沿用前序 `unknown-play-source-prompt` 与 `unknown-play-target-prompt` 的后台 headless Chrome/CDP UI smoke 证据。整体仍 **NOT READY**，因为这是 `PLAY_CARD` command guard hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十七批补充：继续收紧 `MOVE_UNIT` 精确战场/游走目的地的隐藏信息边界。此前移动来源已要求非空 `cardNo`，但游走目的地和 Core 精确战场移动仍可能只凭战场牌 tag 接受一个缺少服务端权威 `cardNo` 的战场对象。本批要求 prompt 侧的精确战场 origin/destination 只使用非空 `CardNo` 的战场牌对象；默认抽象 `BATTLEFIELD:{playerId}-MAIN` 仍保留。Core 手写 `MOVE_UNIT` 对已有战场牌对象位置也要求该对象有已知 `CardNo`，但继续兼容旧 fixture 使用的抽象精确战场位置。
- 新增 Development-only `unknown-move-unit-battlefield-prompt` seed，构造 P1 已知战场牌 `P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN`、有游走权限的已知单位 `P1-UNIT-ROAM-MOVE-DESTINATION-FILTER`，以及 `cardNo = null` 的未知战场牌对象 `P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION`。该场景用于确认“未知战场牌对象可存在于 snapshot/公共战场视图”与“可作为 `MOVE_UNIT` 精确战场目的地提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesMoveUnitBattlefieldDestinationWhenCardHasNoCardNo`、`P4MoveUnitCommandRejectsPreciseBattlefieldDestinationWithoutCardNo`、`P79UnknownMoveUnitBattlefieldSeedHidesDestinationWithoutCardNoThroughHub`，覆盖 prompt 目的地过滤、Core 拒绝手写未知战场目的地命令、Hub seed 后 snapshot 保留未知战场对象且 `cardNo = null`。验证结果：目标窄测 6/6 通过；`FullyQualifiedName~MoveUnit` 宽回归 49/49 通过；`GameHubJoinTests` 118/118 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2996/2996；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮仍无可连接 IAB backend；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-move-battlefield-movwrgls`：P1 Web UI 连接，后台 SignalR 让 P1/P2 入座并 seed；authoritative prompt 中 `MOVE_UNIT.enabled=true`，`destinations` 为 `BASE` 与 `BATTLEFIELD:P1-MAIN`，`ROAM` 的 `destinationChoices` 仅包含 `BATTLEFIELD:P1-MAIN`，未知战场对象 `cardNo = null` 且不进候选。页面行动面板显示“移动单位（需选择）”；打开已知游走单位详情并切到“游走”，目的地只显示“己方主战场”，不包含未知战场对象；reload/reconnect 后仍恢复同一 prompt。smoke 结束后已清理后台 Chrome、API、Vite 和临时 profile，确认 5093/5175/9224 无监听。整体仍 **NOT READY**，因为这是 MOVE_UNIT 精确战场目的地 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十六批补充：继续收紧 `DECLARE_BATTLE` 战场目的地候选的隐藏信息边界。此前攻防单位已要求非空 `cardNo`，但公开战场牌目的地和 active `START_BATTLE` 任务仍可能只凭 `BattlefieldCardTag` 暴露一个缺少服务端权威 `cardNo` 的战场对象；前端会把该对象当成可选战场，Core 手写命令也可能把它当成受支持战场牌目的地。本批要求 `DECLARE_BATTLE.destinations`、`metadata.sourceRequirements[].battlefieldChoices` 和 Core `IsSupportedDeclareBattlefieldId` 都只接受非空 `CardNo` 的战场牌对象；默认 `BATTLEFIELD:{playerId}-MAIN` 仍可用，未知战场对象只保留在 snapshot 中展示，不能作为可提交战斗目的地。
- 新增 Development-only `unknown-declare-battle-battlefield-prompt` seed，构造 P1 战场区域同时存在一个 `cardNo = null` 的战场牌对象 `P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION`、一个已知攻击者和 P2 已知防守者的场景。该场景用于确认“未知战场牌对象仍可按 snapshot 存在”与“可作为 `DECLARE_BATTLE` 战场目的地提交”彻底分离；候选仍保持 enabled，但仅提供默认 `BATTLEFIELD:P1-MAIN`。
- 已补测试与 smoke：新增 `ActionPromptHidesDeclareBattleBattlefieldDestinationWhenCardHasNoCardNo`、`P4DeclareBattleCommandRejectsBattlefieldDestinationWithoutCardNo`、`P79UnknownDeclareBattleBattlefieldSeedHidesDestinationWithoutCardNoThroughHub`，覆盖 prompt 目的地过滤、Core 拒绝手写未知战场目的地命令、Hub seed 后 snapshot 保留未知战场对象且 `cardNo = null`。验证结果：目标窄测 7/7 通过；`FullyQualifiedName~DeclareBattle` 宽回归 57/57 通过；`GameHubJoinTests` 117/117 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2993/2993；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮仍无可连接 IAB backend；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-battlefield-movw7om1`：P1 Web UI 连接，后台 SignalR 让 P2 入座并 seed；authoritative prompt 中 `DECLARE_BATTLE.enabled=true`，但 `destinations` 与 `sourceRequirements[0].battlefieldChoices` 仅包含 `BATTLEFIELD:P1-MAIN`，未知战场对象 `cardNo = null` 且不进候选。页面行动面板显示“声明战斗（需选择）”；打开已知攻击者详情，战场选择只显示“己方主战场”，不包含未知战场对象；reload/reconnect 后仍恢复同一 prompt。smoke 结束后已清理后台 Chrome、API、Vite 和临时 profile，确认 5093/5175/9224 无监听。整体仍 **NOT READY**，因为这是声明战斗战场目的地 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十五批补充：继续收紧 `ACTIVATE_ABILITY` 目标候选的隐藏信息边界。此前 prompt 侧 `IsPromptFieldUnitObject` 已要求目标对象有非空 `cardNo`，但 Core 手写 `ACTIVATE_ABILITY` 命令的泽拉斯目标校验仍只看场上区域和 `UnitCard` tag；客户端若自行提交缺少服务端权威 `cardNo` 的单位对象，仍可能绕过 prompt 进入结算链。本批要求泽拉斯 `PAY_RED_EXHAUST_DAMAGE_3` 目标必须是场上、单位、且 `CardNo` 非空的公开对象；未知目标仍可按视角留在 snapshot 中，但不会进入 `ACTIVATE_ABILITY.targets` 或 `metadata.sourceRequirements.targetChoicesByIndex`，Core 手写命令同样以 `InvalidTarget` 拒绝且状态不变。
- 新增 Development-only `unknown-activate-ability-target-prompt` seed，构造 P1 战场有已知《泽拉斯》、P1 有 1 红色符能，P2 战场有 `P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET` 且该目标对象无 `cardNo` 的场景。由于泽拉斯自身也是合法“任一单位”目标，该场景不会强行要求 `ACTIVATE_ABILITY` disabled；验收重点是 candidate 仅保留 `P1-UNIT-XERATH-TARGET-FILTER` 作为来源和目标槽候选，未知敌方目标只在 snapshot 中作为隐藏/未知对象展示，不可作为可提交目标。
- 已补测试与 smoke：新增 `ActionPromptHidesActivateAbilityTargetWhenUnitHasNoCardNo`、`P4ActivateAbilityCommandRejectsXerathDamageSkillTargetWithoutCardNo`、`P79UnknownActivateAbilityTargetSeedHidesUnitWithoutCardNoThroughHub`，覆盖 prompt targetChoices 过滤、Core 拒绝手写未知目标命令、Hub seed 后 snapshot 保留未知目标且 `cardNo = null`。同步给合法泽拉斯 Spellshield/Stand Firm fixture 中本来公开的目标单位补齐 `cardNo`，避免旧测试数据误模拟未知对象。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标窄测 8/8 通过；`FullyQualifiedName~ActivateAbility` 宽回归 59/59 通过；`GameHubJoinTests` 116/116 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2990/2990；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 in-app browser 控制入口；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-activate-target-movvkesi`：P1 Web UI 连接，后台 SignalR 校验 P1/P2 authoritative snapshot/prompt；`ACTIVATE_ABILITY.enabled=true`，但 `sources`、`targets`、`targetChoicesByIndex[0]` 均只有 `P1-UNIT-XERATH-TARGET-FILTER`，未知目标 `cardNo = null` 且不进候选。页面行动面板显示“激活能力（需选择）”；打开泽拉斯详情显示“确认激活”且不包含未知目标；打开未知目标详情只显示“隐藏信息”，不出现“确认激活”；reload/reconnect 后仍恢复同一 prompt。smoke 结束后已清理后台 Chrome、API、Vite，确认 5093/5175/9224 无监听。整体仍 **NOT READY**，因为这是激活能力目标 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十四批补充：继续收紧 `LEGEND_ACT` 目标候选的隐藏信息边界。此前传奇行动来源缺少 `cardNo` 已不会进入 prompt/Core，但友方单位目标和武装第二目标仍可能只凭区域、控制者与 tag 被视为合法；若目标对象缺少服务端权威 `cardNo`，前端详情会按隐藏信息处理，却仍可能收到可提交的传奇行动组合。本批要求传奇行动的友方单位目标、pending 友方单位目标和武装目标都必须有非空 `CardNo`；未知目标仍保留在 snapshot 中，但不会进入 `LEGEND_ACT.targets` 或 `metadata.sourceRequirements`，Core 手写命令同样以 `InvalidTarget` 拒绝且状态不变。
- 新增 Development-only `unknown-legend-action-target-prompt` seed，构造 P1 已知亚索传奇、2 法力，以及战场上 `P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET` 且该单位对象无 `cardNo` 的场景。该场景用于确认“未知传奇行动目标可见/隐藏信息展示”与“可作为传奇行动目标提交”彻底分离；同步给既有合法传奇行动 fixture 中本来公开的单位/武装目标补齐 `cardNo`，避免旧测试数据误模拟未知对象。
- 已补测试与 smoke：新增 `ActionPromptHidesLegendActionTargetWhenUnitHasNoCardNo`、`P79LegendActYasuoRejectsTargetWithoutCardNo`、`P79UnknownLegendActionTargetSeedHidesUnitWithoutCardNoThroughHub`，覆盖 prompt disabled、sources/targets/sourceRequirements 为空、Core 拒绝手写命令、Hub seed 后 snapshot 仍保留已知传奇和未知目标且目标 `cardNo = null`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标窄测 3/3 通过；`FullyQualifiedName~LegendAct` 宽回归 36/36 通过；`GameHubJoinTests` 115/115 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2987/2987；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮已知没有可连接的 IAB backend；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-legend-target-zmziu3fw`：P1 Web UI 连接，P2 和 P1 dev 连接后台 SignalR 入座并 seed `unknown-legend-action-target-prompt`；authoritative prompt 中 `LEGEND_ACT.enabled=false`、sources/targets/sourceRequirements 均为空，P1 snapshot 中 `P1-LEGEND-YASUO-TARGET-FILTER.cardNo = FND-259/298`、`P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET.cardNo = null`。页面行动面板“传奇行动（需选择）”disabled，打开未知目标详情只显示隐藏信息保护，不出现“确认传奇行动”；reload/reconnect 后仍恢复同一禁用 prompt。smoke 结束后已清理后台 Chrome、API、Vite，确认 5093/5175/9224 无监听。整体仍 **NOT READY**，因为这是传奇行动目标 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十三批补充：继续收紧 `ASSEMBLE_EQUIPMENT` 目标候选的隐藏信息边界。此前代表性长剑装配来源已经要求装备本身为服务端已知 `SFD·022/221`，但目标单位 helper 仍只按受控、正面与单位标签过滤；若基地单位对象缺少服务端权威 `cardNo`，prompt/Core 仍可能把该未知身份单位当成可装配目标。本批要求装配目标也必须有非空 `CardNo`；未知目标仍保留在按视角过滤后的 snapshot 中，但不会进入 `ASSEMBLE_EQUIPMENT.targets` 或 `metadata.sourceRequirements`，Core 手写命令同样以 unsupported guard 拒绝且状态不变。
- 新增 Development-only `unknown-assemble-target-prompt` seed，构造 P1 基地有已知《长剑》、有红色符能，同时存在 `P1-UNIT-UNKNOWN-ASSEMBLE-TARGET` 且该单位对象无 `cardNo` 的场景。该场景用于确认“未知目标对象可见/隐藏信息展示”与“可作为装配目标提交”彻底分离；同步给既有合法装配 fixture 中本来公开的目标单位补齐 `cardNo`，避免旧测试数据误模拟未知对象。
- 已补测试与 smoke：新增 `ActionPromptHidesAssembleEquipmentTargetWhenUnitHasNoCardNo`、`P4AssembleEquipmentCommandRejectsTargetWithoutCardNo`、`P79UnknownAssembleTargetSeedHidesTargetWithoutCardNoThroughHub`，覆盖 prompt disabled、sources/targets/sourceRequirements 为空、Core 拒绝手写命令、Hub seed 后 snapshot 仍保留已知长剑与未知目标且目标 `cardNo = null`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 5/5 通过；`FullyQualifiedName~AssembleEquipment` 宽回归 32/32 通过；`GameHubJoinTests` 114/114 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2984/2984；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮优先尝试，但没有可连接的 IAB backend；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-assemble-target-bm6vvl9p`：P1 Web UI 连接，P2 和 P1 dev 连接后台 SignalR 入座并 seed `unknown-assemble-target-prompt`；authoritative prompt 中 `ASSEMBLE_EQUIPMENT.enabled=false`、sources/targets/sourceRequirements 均为空，P1 snapshot 中 `P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER.cardNo = SFD·022/221`、`P1-UNIT-UNKNOWN-ASSEMBLE-TARGET.cardNo = null`。页面行动面板“装配装备（需选择）”disabled，打开未知目标详情只显示隐藏信息保护，不出现“确认装配”；reload/reconnect 后仍恢复同一禁用 prompt。smoke 结束后已清理后台 Chrome/CDP，9224 无监听。整体仍 **NOT READY**，因为这是装配目标 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十二批补充：继续收紧 `PLAY_CARD` 目标候选的隐藏信息边界。此前 `PLAY_CARD` 来源缺少 `cardNo` 已不会出现在 prompt；但如果目标对象位于公开区域却缺少服务端权威 `cardNo`，prompt 仍可能把它作为合法目标候选暴露给前端，造成前端能提交一个由隐藏/未知身份对象参与的出牌组合。本批把 prompt 侧的手牌、战场、基地与墓地目标 helper 统一改为只返回 `CardNo` 非空的对象；未知对象仍保留在按视角过滤后的 snapshot 中，但不会进入 `PLAY_CARD.targets` 或 `metadata.sourceRequirements` 的目标槽候选。
- 新增 Development-only `unknown-play-target-prompt` seed，构造 P1 手牌《海克斯射线》、P2 战场存在 `P2-UNIT-UNKNOWN-PLAY-TARGET` 且该目标对象无 `cardNo` 的场景。该场景用于确认“未知目标对象可见/隐藏信息保护”与“可作为出牌目标提交”彻底分离。同步给既有合法 Development seed 与 prompt shape 测试中本来公开的单位/法术补齐 `cardNo`，避免旧测试数据误模拟未知对象。
- 已补测试与 smoke：新增 `P79UnknownPlayTargetSeedHidesTargetWithoutCardNoThroughHub`，覆盖 Hub seed 后 `PLAY_CARD.enabled=false`、sources/targets/sourceRequirements 为空，P1 手牌仍可见且 P2 自视角未知目标 `cardNo = null`；扩展合法 prompt shape / spell-duel focus 数据，确认公开目标补齐身份后仍可出现在服务端候选。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P79UnknownPlaySourceSeedHidesHandObjectWithoutCardNoThroughHub|P79UnknownPlayTargetSeedHidesTargetWithoutCardNoThroughHub` 通过 2/2；`GameHubJoinTests` 通过 113/113；`ActionPromptActivateAbilityMetadataFiltersSourcesTargetsAndSpellshieldTax` 与 `CoreRuleEngineRejectedSpellDuelFocusPromptIncludesPlayableSwiftCard` 回归通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2980/2980；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮优先尝试，但没有可连接的 IAB backend；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。专门 unknown target UI smoke 使用 `unknown-play-target-prompt`，P1 Web UI 连接、P2 后台 SignalR 入座并 seed；页面显示 `P1-SPELL-UNKNOWN-PLAY-TARGET` 与 `P2-UNIT-UNKNOWN-PLAY-TARGET`，全局“打出卡牌（需选择）”按钮保持 disabled，点击《海克斯射线》详情只显示“服务端未提供可执行候选”，不出现“确认打出”；reload/reconnect 后仍恢复同一 disabled prompt，脚本退出码 0。另用房间 `smoke-standby-1778178260963` 复核待命清理链：P1 先以 SignalR 入座以保持 dev seed 座位映射，P2 Web UI 设置 `serverUrl = http://127.0.0.1:5093`、`playerId = P2` 后连接；P1 `SeedScenario(battlefield-contest-stack)`，P2 通过前端点击“让过优先权”和“让过焦点”，P1 SignalR 提交 `DECLARE_BATTLE`。页面显示“战斗结束”“战场控制结算”“待命清理”；authoritative snapshot 确认 `P1-STANDBY-CONTEST-001` 已从 battlefield 移到 P1 graveyard，battlefield `standbyObjectIds = []`，`controllerId = P2`；reload/reconnect 后 UI 恢复 `控制：P2` 与 `0 张面朝下`。smoke 结束后已清理 Chrome、API、Vite，确认 5093/5175/9224 无监听。整体仍 **NOT READY**，因为这是 PLAY_CARD target prompt hygiene 与战场待命清理代表路径证据，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十一批补充：继续收紧隐藏信息边界上的 `HIDE_CARD` / `REVEAL_CARD`。此前 prompt 层已经要求待命布置/翻开来源有服务端已知 `cardNo`，不会向前端暴露未知身份来源；但 Core 手写命令层仍可能接受客户端自行上报的 `cardNo`，把 `cardNo = null` 的手牌/面朝下待命对象当成对应卡牌执行。本批要求 Core 在执行 `HIDE_CARD` 与 `REVEAL_CARD` 前必须确认来源对象存在且 `CardNo` 非空，并与命令 `cardNo` 完全一致；缺少身份的来源以 `InvalidTarget` 拒绝，状态、区域和资源保持不变。同步把旧合法待命 fixture/单测补成“服务端权威状态知道卡号”的建模，避免合法路径误用未知对象。
- 新增 Development-only `unknown-hide-card-source-prompt` 与 `unknown-reveal-card-source-prompt` seed，分别构造 P1 手牌待命对象 `P1-HAND-UNKNOWN-HIDE-SOURCE`、P1 基地面朝下待命对象 `P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE` 且 `cardNo = null` 的场景。两者用于确认“未知待命对象仍可被 snapshot 展示/隐藏信息保护”与“布置/翻开待命可提交”彻底分离。
- 已补测试与 smoke：扩展 `ActionPromptFiltersHideCardSourcesByPayableStandbyCosts` / `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby`，确认未知待命对象不会进入 sources/sourceRequirements；新增 `P4HideCardCommandRejectsSourceWithoutCardNo` 与 `P4RevealCardCommandRejectsSourceWithoutCardNo` 覆盖 Core 手写命令拒绝；新增 `P79UnknownHideCardSourceSeedHidesStandbyWithoutCardNoThroughHub` 与 `P79UnknownRevealCardSourceSeedHidesFaceDownStandbyWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。目标回归通过 10/10；`GameHubJoinTests` 通过 112/112；旧合法待命代表项回归通过 56/56；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2979/2979；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮按要求优先尝试，但 IAB backend 未发现；未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP，并在结束后确认 5093/5175/9224 无监听。房间 `smoke-unknown-standby-6rjmr6j8`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并分别 seed `unknown-hide-card-source-prompt`、`unknown-reveal-card-source-prompt`；authoritative prompt 中 `HIDE_CARD.enabled=false` 与 `REVEAL_CARD.enabled=false`，sources/sourceRequirements 均为空；P1 snapshot 中两个未知待命对象均 `cardNo = null`。页面行动面板“布置待命（需选择）”和“翻开待命（需选择）”均 disabled，title 分别为“HIDE_CARD 当前没有服务端可执行候选”和“REVEAL_CARD 当前没有服务端可执行候选”；打开详情只显示隐藏信息保护，不出现“确认布置待命”或“确认翻开待命”；reload/reconnect 后仍恢复 reveal 最终 disabled prompt。整体仍 **NOT READY**，因为这是待命隐藏信息 prompt/Core hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

- P0-005 第七十批补充：继续收紧 `DECLARE_BATTLE` 攻防参与者过滤。此前战斗声明来源/目标主要按正面、受控、未参战、单位标签和区域判定；若战场单位对象缺少 `cardNo`，服务端 prompt/Core 仍可能把该未知单位当成可声明战斗的攻击者或防守者，前端详情却会按隐藏信息处理该对象。本批要求 `DECLARE_BATTLE` 攻击者与防守者都必须公开已知单位 `cardNo`；缺少 `cardNo` 的单位仍保留在 snapshot 中，但 `DECLARE_BATTLE.sources`、`targets` 与 `sourceRequirements` 不再暴露该对象，手写命令也以现有 unsupported battle guard 拒绝且状态不变。
- 新增 Development-only `unknown-declare-battle-source-prompt` seed，构造 P1 战场存在 `P1-BATTLE-UNKNOWN-ATTACKER`、P2 战场存在 `P2-BATTLE-UNKNOWN-DEFENDER`，二者均为单位但 `cardNo = null` 的场景。该场景用于确认“未知战场单位可见/隐藏信息展示”与“声明战斗可提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesDeclareBattleCombatantsWhenUnitHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `DECLARE_BATTLE` disabled、sources/targets/sourceRequirements 为空；新增 `P4DeclareBattleCommandRejectsAttackerWithoutCardNo` 与 `P4DeclareBattleCommandRejectsDefenderWithoutCardNo` 覆盖 Core 手写命令拒绝且攻防状态不变；新增 `P79UnknownDeclareBattleSourceSeedHidesCombatantsWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。同步给既有合法战斗、战场任务和移动测试数据补齐公开单位 `cardNo`，避免旧测试状态误模拟未知对象。目标回归通过 7/7；失败收口回归通过 8/8；`GameHubJoinTests` 通过 110/110；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2975/2975；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-declare-movrd1ki`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `unknown-declare-battle-source-prompt`；authoritative prompt 中 `DECLARE_BATTLE.enabled=false`、sources/targets/sourceRequirements 均为空，P1 snapshot 中 `P1-BATTLE-UNKNOWN-ATTACKER.cardNo = null` 且未泄漏 P2 隐藏防守者对象。页面行动面板“声明战斗（需选择）”disabled，title 为“DECLARE_BATTLE 当前没有服务端可执行候选”；打开未知攻击者详情只显示隐藏信息保护，不出现“确认声明战斗”；reload/reconnect 后仍恢复同一禁用 prompt。整体仍 **NOT READY**，因为这是未知战斗参与者的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。

## 2026-05-07 开发进度更新

- P0-005 第六十九批补充：继续收紧 `TAP_RUNE` / `RECYCLE_RUNE` 来源过滤。此前基础符文横置/回收只按 `RuneCard`、控制者、区域、横置/颜色 tag 判定；若基地符文对象缺少 `cardNo`，服务端 prompt/Core 仍可能把该未知符文当成可横置或可回收来源，前端却会按隐藏信息展示该对象。本批要求基础符文资源动作来源必须公开已知符文 `cardNo`；缺少 `cardNo` 的符文仍保留在 snapshot 中，但 `TAP_RUNE.sources` 与 `RECYCLE_RUNE.sources` 不再暴露该对象，手写命令也分别以 `UnsupportedCardBehavior` 拒绝。
- 新增 Development-only `unknown-rune-source-prompt` seed，构造 P1 基地存在 `P1-RUNE-UNKNOWN-SOURCE`、对象带 `RuneCard` / `COLOR:red` tag 但 `cardNo = null` 的场景。该场景用于确认“未知符文对象可见/隐藏信息展示”与“横置/回收资源动作可提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesRuneSourcesWhenRuneHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `TAP_RUNE` / `RECYCLE_RUNE` disabled 且 sources 为空；新增 `CoreRuleEngineRejectsTapRuneSourceWithoutCardNo` 与 `CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo` 覆盖 Core 手写命令拒绝且资源池/区域不变；新增 `P79UnknownRuneSourceSeedHidesRuneWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。目标回归 `ActionPromptHidesRuneSourcesWhenRuneHasNoCardNo|CoreRuleEngineRejectsTapRuneSourceWithoutCardNo|CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo|P79UnknownRuneSourceSeedHidesRuneWithoutCardNoThroughHub|CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 通过 6/6；补齐旧装配支付资源 shape 测试中公开红色符文的 `cardNo` 后，失败点回归 3/3；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2971/2971；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-rune-oegg6dgn`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-rune-source-prompt`；authoritative prompt 中 `TAP_RUNE.enabled=false`、`RECYCLE_RUNE.enabled=false`、两个候选 sources 均为空；页面显示 `P1-RUNE-UNKNOWN-SOURCE` 的隐藏信息，行动面板“横置符文（需选择）”和“回收符文（需选择）”均 disabled，详情抽屉不出现横置/回收确认入口；reload/reconnect 后仍恢复同一 disabled prompt。整体仍 **NOT READY**，因为这是未知基础符文来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十八批补充：继续收紧 `MOVE_UNIT` 来源过滤。此前 `MOVE_UNIT` 只要求来源是受控、正面、非战斗中的单位；若基地/战场单位对象缺少 `cardNo`，服务端 prompt/Core 仍可能把该未知单位当成可移动来源，前端卡牌详情却会按隐藏信息处理该对象。本批要求 `MOVE_UNIT` 来源必须公开已知单位 `cardNo`；缺少 `cardNo` 的单位仍保留在 snapshot 中，但 `MOVE_UNIT.sources` 与 `sourceRequirements` 不再暴露该对象，手写命令也以 `UnsupportedCardBehavior` 拒绝。同步给旧移动/装配测试 fixture 中本来公开的代表单位补齐 `cardNo`，避免历史测试数据误模拟未知对象。
- 新增 Development-only `unknown-move-unit-source-prompt` seed，构造 P1 基地存在 `P1-UNIT-UNKNOWN-MOVE-SOURCE`、对象为单位但 `cardNo = null` 的场景。该场景用于确认“未知场上单位可见/隐藏信息展示”与“移动单位可提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesMoveUnitSourceWhenUnitHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `MOVE_UNIT` disabled、sources/sourceRequirements 为空；新增 `P4MoveUnitCommandRejectsSourceWithoutCardNo` 覆盖 Core 手写命令拒绝、来源仍留在基地；新增 `P79UnknownMoveUnitSourceSeedHidesUnitWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。目标回归 `ActionPromptHidesMoveUnitSourceWhenUnitHasNoCardNo|P4MoveUnitCommandRejectsSourceWithoutCardNo|P79UnknownMoveUnitSourceSeedHidesUnitWithoutCardNoThroughHub|P4MoveUnitCommandMovesFriendlyBaseUnitToBattlefieldInCoarseModel|ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits` 通过 5/5；移动相关宽回归 `P4MoveUnitCommand|P5MoveUnitCommand|P79BattlefieldStaticRoamAllowsPreciseBattlefieldMovement|P79BattlefieldStaticPreventMoveToBaseRejectsMoveUnit|P79BattlefieldMovedUnitGainsTemporaryPower` 通过 43/43；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2967/2967；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-move-weqq9zpl`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-move-unit-source-prompt`；authoritative prompt 中 `MOVE_UNIT.enabled=false`、sources 数量 0、sourceRequirements 为空；页面显示 `P1-UNIT-UNKNOWN-MOVE-SOURCE` 的隐藏信息，行动面板“移动单位（需选择）” disabled 且 title 为“MOVE_UNIT 当前没有服务端可执行候选”，详情抽屉不出现“确认移动”；reload/reconnect 后仍恢复同一 disabled prompt。整体仍 **NOT READY**，因为这是未知移动来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十七批补充：继续收紧 `ACTIVATE_ABILITY` 来源过滤。此前《蜕变花园》授予单位“横置：获得 1 经验”的代表路径只要求来源是受控战场单位；若该单位对象缺少 `cardNo`，服务端 prompt/Core 仍可能把该未知单位当成可激活来源。本批要求该战场授予能力来源也必须公开已知单位 `cardNo`；缺少 `cardNo` 的战场单位仍保留在 snapshot 中，但 `ACTIVATE_ABILITY.sources` 与 `sourceRequirements` 不再暴露该对象，手写命令也以 `UnsupportedCardBehavior` 拒绝。
- 新增 Development-only `unknown-activate-ability-source-prompt` seed，构造 P1 控制《蜕变花园》战场、同战场有 `P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE` 但该单位对象无 `cardNo` 的场景。该场景用于确认“未知战场单位可见/隐藏信息展示”与“激活能力可提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesActivateAbilitySourceWhenGrantedUnitHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `ACTIVATE_ABILITY` disabled、sources/sourceRequirements 为空；新增 `P79BattlefieldUnitExperienceAbilityRejectsSourceWithoutCardNo` 覆盖 Core 手写命令拒绝、来源不横置且经验不增加；新增 `P79UnknownActivateAbilitySourceSeedHidesGrantedUnitWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。目标回归 `ActionPromptHidesActivateAbilitySourceWhenGrantedUnitHasNoCardNo|P79BattlefieldUnitExperienceAbilityRejectsSourceWithoutCardNo|P79UnknownActivateAbilitySourceSeedHidesGrantedUnitWithoutCardNoThroughHub|P79BattlefieldUnitExperienceAbilityExhaustsSourceAndGainsExperience|P79BattlefieldUnitExperienceAbilitySeedOffersActivateAbilityAndGainsExperience` 通过 5/5；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2964/2964；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-activate-eb9aeq92`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-activate-ability-source-prompt`；authoritative prompt 中 `ACTIVATE_ABILITY.enabled=false`、sources 数量 0、sourceRequirements 为空；页面显示 `P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE` 的隐藏信息，行动面板“激活能力（需选择）” disabled 且 title 为“ACTIVATE_ABILITY 当前没有服务端可执行候选”，详情抽屉不出现“确认激活能力”；reload/reconnect 后仍恢复同一 disabled prompt。整体仍 **NOT READY**，因为这是未知战场授予能力来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十六批补充：继续收紧 `LEGEND_ACT` 来源过滤。此前战场授予的代表性传奇贴附能力只要求玩家控制《魄罗熔炉》战场，若 legend zone 中对象缺少 `cardNo`，服务端 prompt/Core 仍可能把该未知传奇对象当成可提交传奇行动来源。本批要求 `LEGEND_ACT` 来源必须公开已知传奇 `cardNo`；缺少 `cardNo` 的 legend zone 对象仍保留在 snapshot 中，但 `LEGEND_ACT.sources` 与 `sourceRequirements` 不再暴露该对象，手写命令也以 `UnsupportedCardBehavior` 拒绝。
- 新增 Development-only `unknown-legend-action-source-prompt` seed，构造 P1 控制《魄罗熔炉》战场、基地有合法单位/武装目标、legend zone 中存在 `P1-LEGEND-UNKNOWN-ACTION-SOURCE` 但对象无 `cardNo` 的场景。该场景用于确认“未知传奇对象可见/隐藏信息展示”与“传奇行动可提交”彻底分离。
- 已补测试与 smoke：新增 `ActionPromptHidesLegendActionSourceWhenLegendHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `LEGEND_ACT` disabled、sources/sourceRequirements 为空；新增 `P79BattlefieldForgeLegendAttachRejectsLegendSourceWithoutCardNo` 覆盖 Core 手写命令拒绝、来源不横置且武装不贴附；新增 `P79UnknownLegendActionSourceSeedHidesLegendWithoutCardNoThroughHub` 覆盖 Hub seed、snapshot 和 prompt。目标回归 `ActionPromptHidesLegendActionSourceWhenLegendHasNoCardNo|P79BattlefieldForgeLegendAttachRejectsLegendSourceWithoutCardNo|P79UnknownLegendActionSourceSeedHidesLegendWithoutCardNoThroughHub|P79BattlefieldForgeGrantsLegendArmamentAttach|P79BattlefieldLegendAttachArmamentSeedOffersLegendActionAndAttaches` 通过 5/5；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2961/2961；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 本轮无可调用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-legend-q4azhapy`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `unknown-legend-action-source-prompt`；authoritative prompt 中 `LEGEND_ACT.enabled=false`、sources 数量 0、sourceRequirements 为空；页面显示 `P1-LEGEND-UNKNOWN-ACTION-SOURCE` 的隐藏信息，行动面板“传奇行动（需选择）” disabled 且 title 为“LEGEND_ACT 当前没有服务端可执行候选”，详情抽屉不出现“确认传奇行动”；reload/reconnect 后仍恢复同一 disabled prompt。整体仍 **NOT READY**，因为这是未知传奇来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十五批补充：把代表性 `ASSEMBLE_EQUIPMENT` 支付窗口接入服务端候选的回收符文支付资源动作。此前长剑装配只接受当前 `powerByTrait.red >= 1`，若玩家当前 0 红色符能但基地有可回收红色基础符文，前端只能先单独 `RECYCLE_RUNE` 再装配，不能表达官方支付步骤中的资源动作。本批允许 `ASSEMBLE_EQUIPMENT.optionalCosts` 同时携带 `ASSEMBLE_RED` 与服务端候选 `RECYCLE_RUNE:<objectId>`；Core 会先把该符文移入符文牌堆、记录 `RUNE_RECYCLED` / `POWER_GAINED` 且 `paymentWindow = ASSEMBLE_EQUIPMENT`，再扣红色装配费用并贴附长剑。
- Prompt/UI 补充：`ASSEMBLE_EQUIPMENT.sourceRequirements` 新增 `paymentResourceChoices`、`paymentResourcePowerByChoice`、`availablePowerByTrait` 与 `availablePowerByTraitWithPaymentResources`。当前红色符能不足但红色基础符文可回收时，服务端仍公开长剑装配来源，并只把红色回收符文作为支付资源候选；前端卡牌详情抽屉新增“支付资源”组，未选择该服务端候选时“确认装配”保持 disabled，选择后才提交 `ASSEMBLE_RED + RECYCLE_RUNE:<objectId>`，不从符文 tag 或资源池自行构造资源动作。
- 已补测试与 smoke：新增 `P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource` 覆盖 Core 命令先回收红符文、再支付装配红色符能、符文移入符文牌堆且长剑贴附；扩展 `ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower` 覆盖 0 红符能 + 红符文时 prompt 暴露 `paymentResourceChoices`；新增 `P79AssemblePaymentRecycleSeedOffersResourceAndAttachesThroughHub` 覆盖 Development seed、Hub prompt 和提交后 snapshot。目标回归 `P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource|ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower|P79AssemblePaymentRecycleSeedOffersResourceAndAttachesThroughHub|P4AssembleEquipmentCommandRejectsGenericPowerForRedAssembleCost` 通过 4/4；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`、`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过，后端 full test 2958/2958；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-assemble-payment-c6a6643h`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `assemble-payment-recycle`；P1 打开《长剑》详情抽屉，页面显示“支付资源 / 回收符文支付”，确认按钮在未选择资源时 disabled、选择后 enabled；提交后事件日志显示“回收符文 / 支付费用 / 装配长剑”，authoritative snapshot 中 `P1-RUNE-RED-ASSEMBLE-PAYMENT-001` 已离开基地进入符文牌堆、`runeDeckCount = 2`、长剑 `attachedToObjectId = P1-UNIT-ASSEMBLE-PAYMENT-TARGET`、red power 已扣空；reload/reconnect 后仍恢复最终 snapshot。整体仍 **NOT READY**，因为这是装配支付窗口的代表性资源动作，完整 PaymentEngine、所有非出牌支付窗口、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十四批补充：继续收紧 `ASSEMBLE_EQUIPMENT` 来源过滤。此前代表性装配路径只实现《长剑》，但 `IsImplementedAssembleEquipmentSource` 会把基地中正面、受控、未贴附且带 `EquipmentCard` / `武装` / `灵便` 标签、却缺少 `cardNo` 的装备对象公开为装配来源，导致 ActionPrompt 可能出现 `ASSEMBLE_EQUIPMENT.sources` 有对象、但服务端无法证明该对象就是已实现的长剑行为。本批改为装配来源必须精确匹配 `SFD·022/221`，缺少 `cardNo` 或未知装备不再公开装配候选；前端仍按 snapshot 展示该对象，但不能把它伪装成可提交的装配操作。
- 新增 Development-only `unknown-assemble-source-prompt` seed，构造 P1 基地中存在 `P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE`、对象无 `cardNo`、同时有合法单位目标和 1 红色符能的场景。该场景用于确认“装备对象可见”与“装配命令可提交”彻底分离：snapshot 保留对象，prompt 不给 `ASSEMBLE_EQUIPMENT.sources` 或 source requirements。
- 已补测试与 smoke：新增 `ActionPromptHidesAssembleEquipmentSourceWhenEquipmentHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `ASSEMBLE_EQUIPMENT` disabled、sources/sourceRequirements 为空；新增 `P79UnknownAssembleSourceSeedHidesEquipmentWithoutCardNoThroughHub` 覆盖 Hub seed 后 snapshot 基地仍包含该对象但 `cardNo = null`、prompt 不公开装配来源。目标回归 `ActionPromptHidesAssembleEquipmentSourceWhenEquipmentHasNoCardNo|P79UnknownAssembleSourceSeedHidesEquipmentWithoutCardNoThroughHub|ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower|P4AssembleEquipmentCommandRejectsGenericPowerForRedAssembleCost` 通过 4/4。已优先尝试 Browser Use，但本地 IAB backend 仍未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-assemble-mr8gvnej`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed；authoritative prompt 中 `ASSEMBLE_EQUIPMENT.enabled=false`、`sources=[]`、`sourceRequirements=[]`，snapshot 中 P1 基地仍包含 `P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE` 且对象 `cardNo = null`。页面能看到该未知装备对象，行动面板“装配装备（需选择）”为 disabled，title 为“ASSEMBLE_EQUIPMENT 当前没有服务端可执行候选”；打开详情只显示隐藏信息保护文案，不出现“确认装配”；reload/reconnect 后仍恢复同一禁用 prompt。整体仍 **NOT READY**，因为这是无行为定义装备来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十三批补充：继续收紧 `PLAY_CARD` 来源过滤。此前兼容旧 fixture 的 `IsImplementedPlayableHandSource` 会把“存在于手牌但缺少 `cardNo` / 服务端行为定义”的对象当成可出牌来源，导致 ActionPrompt 可能出现 `PLAY_CARD.sources` 有对象、但 `metadata.sourceRequirements` 为空且前端无法构造权威命令的误导入口。本批改为缺少 `cardNo` 时不公开 `PLAY_CARD` 来源；前端仍会按 snapshot 展示该对象，但不会把它伪装成可提交的游戏操作。
- 新增 Development-only `unknown-play-source-prompt` seed，构造 P1 手牌中存在 `P1-HAND-UNKNOWN-PLAY-SOURCE`、对象无 `cardNo`、P1 有 3 法力的场景。该场景用于确认“对象可见/可隐藏展示”与“游戏操作候选可提交”彻底分离：snapshot 保留对象，prompt 不给 `PLAY_CARD.sources` 或 source requirements。
- 已补测试与 smoke：新增 `ActionPromptHidesPlayCardSourceWhenHandObjectHasNoCardNo` 覆盖直接 `BuildPrompts` 时 `PLAY_CARD` disabled、sources/sourceRequirements 为空；新增 `P79UnknownPlaySourceSeedHidesHandObjectWithoutCardNoThroughHub` 覆盖 Hub seed 后 snapshot 手牌仍包含该对象但 `cardNo = null`、prompt 不公开出牌来源。目标回归 `ActionPromptHidesPlayCardSourceWhenHandObjectHasNoCardNo|P79UnknownPlaySourceSeedHidesHandObjectWithoutCardNoThroughHub|P79SpellshieldTaxInsufficientSeedHidesUnpayablePlaySourceThroughHub` 通过 3/3。已优先尝试 Browser Use，但本地 IAB backend 仍未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-unknown-source-3a8idnup`：P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed；页面能看到未知手牌对象 `P1-HAND-UNKNOWN-PLAY-SOURCE`，行动面板“打出卡牌（需选择）”为 disabled，title 为“PLAY_CARD 当前没有服务端可执行候选”；打开详情只显示隐藏信息保护文案，不出现“确认打出”；reload/reconnect 后仍恢复同一禁用 prompt。整体仍 **NOT READY**，因为这是无行为定义来源的 prompt hygiene，完整 PaymentEngine、战场任务状态机、LayerEngine 和全官方卡牌证据仍未清零。
- P0-005 第六十二批补充：继续收紧 `PLAY_CARD` 的服务端候选口径。此前 `PromptHasRequiredTargetChoices` 只验证每个必需目标槽存在候选，若唯一目标带 Spellshield 且玩家只有基础法力、无法支付目标税，前端仍可能看到 `PLAY_CARD` 来源但详情抽屉没有可提交组合。本批把“目标组合合法性依赖 Spellshield 目标税或总目标战力上限”的牌源过滤改为要求至少存在一个服务端 `legalTargetSelections` 组合；没有合法组合时 `PLAY_CARD.sources` 与 `metadata.sourceRequirements` 均为空，前端只显示禁用候选。
- 新增 Development-only `spellshield-tax-insufficient-prompt` seed，构造 P1 只有 2 法力、手牌《焚烧》，P2 战场只有带 `法盾` 的《呸呸魄罗》的场景。该场景保留卡牌与目标在 snapshot 中可见，但服务端 prompt 不公开可提交的《焚烧》来源，避免前端把“Core 会拒绝”的 Spellshield 加税不足路径伪装成可玩操作。
- 已补测试与 smoke：新增 `ActionPromptHidesPlayCardSourceWhenSpellshieldTaxLeavesNoLegalTargetSelection` 覆盖 2 法力时隐藏《焚烧》来源、3 法力时恢复来源且 `legalTargetSelections` 包含目标；新增 `P79SpellshieldTaxInsufficientSeedHidesUnpayablePlaySourceThroughHub` 覆盖 Hub seed 后 `PLAY_CARD` disabled、sources/sourceRequirements 为空、手牌仍保留《焚烧》。目标回归 `ActionPromptHidesPlayCardSourceWhenSpellshieldTaxLeavesNoLegalTargetSelection|P79SpellshieldTaxInsufficientSeedHidesUnpayablePlaySourceThroughHub|P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub|CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient` 通过 4/4；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2952/2952；`source ../../scripts/dev-env.sh && npm run build` 通过。已优先尝试 Browser Use，但本地 IAB backend 仍未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-spellshield-insufficient-sbqmk5ci`：P1 Web UI 连接后能看到《焚烧》和 P2 法盾单位《呸呸魄罗》，行动面板中的“打出卡牌（需选择）”为 disabled，title 为“PLAY_CARD 当前没有服务端可执行候选”；打开《焚烧》详情抽屉不出现“确认打出”；reload/reconnect 后仍恢复同一禁用 prompt。整体仍 **NOT READY**，因为这是 Spellshield 目标税 prompt 泄露的代表路径，完整 PaymentEngine、全支付窗口加税和所有目标费用模型仍未清零。
- P0-005 第六十一批补充：补齐普通结算链优先权窗口中反应牌 `PLAY_CARD` 的 prompt/UI 候选口径。`ActionPromptBuilder.StackPriorityActions` 现在会在持有优先权且 `PLAY_CARD` 有服务端来源时公开 `PLAY_CARD`，并继续复用既有 `PlayCardSourceRequirements` 的来源、目标槽、模式、费用和可组合性过滤；普通非反应手牌不会因栈上有项目而暴露给前端。本批新增 Development-only `priority-reaction-counter` seed，用《强买强卖》对栈上《焚烧》提供可点击反应牌场景。
- 前端仍使用同一服务端候选口径：卡牌详情抽屉的既有 `PLAY_CARD` 组合器只读取 `metadata.sourceRequirements` 渲染栈目标 `OGS·003/024 / STACK-1-P1-SPELL-INCINERATE`、模式 `TARGET_DECLINES_PAY_2_NO_ECHO` 和确认按钮；提交命令只包含服务端候选的 `sourceObjectId`、`cardNo`、`mode` 与 `targetObjectIds`。事件日志新增 `STACK_ITEM_COUNTERED` 中文“无效化法术”。
- 已补测试与 smoke：新增 `ActionPromptExposesPlayableReactionCardsDuringStackPriority`，覆盖 P2 持有优先权时 prompt 动作包含 `PLAY_CARD` / `PASS_PRIORITY`，只公开《强买强卖》来源、栈上法术目标和服务端模式，对手只看到 `WAIT`。目标回归 `ActionPromptExposesPlayableReactionCardsDuringStackPriority|P6ReactionKeywordAllowsProphetsOmenInPriorityWindow|P4RevealCardCommand` 通过 40/40；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2950/2950；`source ../../scripts/dev-env.sh && npm run build` 通过。已优先尝试 Browser Use，但本地 IAB backend 仍未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-priority-reaction-wotz00vd`：P2 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `priority-reaction-counter`；P2 从手牌详情抽屉选择栈上 `OGS·003/024` 目标并确认打出《强买强卖》，事件日志显示“打出卡牌 / 支付费用 / 加入结算链”；随后 P2 通过 UI 让过优先权、后台 P1 让过，服务端广播 `STACK_ITEM_RESOLVED` / `STACK_ITEM_COUNTERED`，页面显示“无效化法术”和“当前无结算链项目”，authoritative snapshot 结算链为空，reload/reconnect 后仍恢复最终 snapshot。整体仍 **NOT READY**，因为这是普通优先权反应牌 prompt/UI 的代表路径，完整响应窗口、所有反应/反制目标条件、费用目标和统一 PaymentEngine 仍未清零。
- P0-005 第六十批补充：补齐 `REVEAL_CARD` 待命翻开/反应打出的 prompt/UI 候选口径。`ActionPromptBuilder` 现在只在服务端合法窗口公开 `REVEAL_CARD`：主动玩家普通开环可从自己基地中面朝下待命对象以 `STANDBY_REVEAL` 翻回基地；结算链待处理且该玩家持有优先权时，可从同类来源以 `STANDBY_REACTION` / `STACK` / `STANDBY_REVEAL_0` 作为反应牌打出。非面朝下对象、非待命对象、无栈优先权窗口或对手视角不会得到可提交来源。
- 前端接入同一服务端候选口径：`CardDetailDrawer` 新增 `REVEAL_CARD` 组合器，只读取 `metadata.sourceRequirements` 渲染模式、目的地和 0 费翻开费用；提交命令只包含服务端候选的 `sourceObjectId`、`cardNo`、`mode`、`destination`、`optionalCosts` 和空目标列表。事件日志新增 `CARD_REVEALED` 中文“翻开待命”。
- 已补测试与 smoke：新增 `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby`，覆盖普通开环只公开面朝下待命来源、非面朝下/非待命不公开、无栈闭环不公开、持有优先权的结算链窗口公开 `STANDBY_REACTION` / `STACK`，以及对手只看到 `WAIT`。目标回归 `ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby|P4RevealCardCommand` 通过 39/39；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2949/2949；`source ../../scripts/dev-env.sh && npm run build` 通过。已优先尝试 Browser Use，但本地 IAB backend 仍未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-reveal-card-3eexrdzw`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `standby-reaction`；P1 在详情抽屉看到“翻开费用”和“确认作为反应打出”，点击后事件日志显示“翻开待命 / 打出卡牌 / 支付费用 / 加入结算链”；authoritative snapshot 中 `P1-FACEDOWN-OGN-TEEMO-PURPLE` 已离开 P1 基地，结算链包含 `OGN·197/298` 待命反应项目，reload/reconnect 后仍恢复该栈状态。整体仍 **NOT READY**，因为这是待命翻开/反应代表路径，完整待命目标分支、所有待命卡牌反应效果、PaymentEngine 和官方级响应窗口仍未清零。
- P0-005 第五十九批补充：修复 `HIDE_CARD` 仍是泛化候选、前端难以区分待命布置费用/目的地且可能暴露不可支付入口的问题。`ActionPromptBuilder` 现在对 `HIDE_CARD` 使用每来源 `sourceRequirements`：只有已实现的待命手牌、存在可支付待命费用时才公开来源；费用候选由服务端给出 `STANDBY_A`、`STANDBY_TEEMO_MANA` 或 `STANDBY_FREE`；目的地候选只包含普通待命区与当前控制的《班德尔树》战场待命目的地；无费用、无授权或无合法目的地时 prompt 不暴露可提交来源。`HIDE_CARD` 中文标签同步为“布置待命”。
- 前端接入同一服务端候选口径：`CardDetailDrawer` 新增 `HIDE_CARD` 待命布置组合器，只读取 `metadata.sourceRequirements` 渲染目的地和待命费用，不从卡面或本地资源自行判断；提交命令只包含服务端候选的 `sourceObjectId`、`cardNo`、`destination` 和单个 `optionalCosts` token。
- 已补测试与 smoke：新增 `ActionPromptFiltersHideCardSourcesByPayableStandbyCosts`，覆盖 0 法力不公开待命来源、1 法力 + Teemo 传奇公开普通/Teemo 待命费用、控制《班德尔树》时公开战场待命目的地，以及免费待命效果只公开 `STANDBY_FREE`。目标回归 `ActionPromptFiltersHideCardSourcesByPayableStandbyCosts|P4HideCardCommand|P79LegendTeemoStandbyReplacement|P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides` 通过 28/28；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2948/2948。已优先尝试 Browser Use，但本地 IAB backend 未发现；按用户要求未使用 Computer Use 抢前台，改用后台 headless Chrome/CDP。房间 `smoke-standby-hide-yy6gku5x`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-extra-standby`；P1 打开 Teemo 待命手牌详情，页面显示“待命费用”“支付 1 法力布置待命”和《班德尔树》目的地，点击“确认布置待命”后事件日志显示“班德尔树额外布置待命牌”；authoritative snapshot 中 `P1-STANDBY-BANDLE-TEEMO` 已从手牌移到《班德尔树》战场、`isFaceDown = true`、P1 法力为 0，reload/reconnect 后仍恢复同一最终 snapshot。整体仍 **NOT READY**，因为这是待命布置 prompt/UI 的代表路径，完整官方 PaymentEngine、所有待命/揭示/替代费用与完整 battlefield/control task lifecycle 仍未清零。
- P0-004/P0-005 第五十八批补充：修复战场静态效果“单位不能被打出到该战场”只在 Core 提交侧拒绝、但 prompt 仍可能暴露 `PLAY_CARD`/战场目的地的合法操作泄露。`ActionPromptBuilder` 现在按服务端同源目的地规则生成 `PLAY_CARD.destinationChoices`：伏击模式只暴露战场目的地；若该战场受 `SFD·216/221` 静态效果禁止单位进场，则伏击来源不再作为可出牌来源暴露，普通单位仍可保留基地目的地。
- 已补测试与 smoke：扩展 `P79BattlefieldStaticPreventPlayUnitsSeedRejectsAmbushToBattlefield`，在 `battlefield-static-prevent-play-units` seed 后先断言 P1 prompt 不含 `PLAY_CARD`，再保留直接提交被 Core 拒绝的兜底断言。后台 headless Chrome/CDP smoke 使用房间 `smoke-prevent-unit-play-rnfcoybs`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed；页面行动面板只显示“让过优先权”，没有全局“打出卡牌”；打开《阴森药剂师》详情抽屉也不渲染“确认打出”组合器。整体仍 **NOT READY**，因为这只是代表性静态禁止进场 prompt 泄露修复，完整战场任务状态机、战斗/法术对决 lifecycle、PaymentEngine 和 LayerEngine 仍未清零。
- P0-005 第五十七批补充：修复代表性“Core 已支持战场据守后非衍生物单位加费，但 `PLAY_CARD.sourceRequirements.minimumManaCost` 仍显示基础费用”的 prompt/UI 缺口。`PromptMinimumManaCost` 现在会在基础费用与减费后叠加 `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:<playerId>`，并在每来源 metadata 中公开 `battlefieldHeldUnitCostIncreaseMana`；`battlefield-held-unit-cost-increase` seed 中 P1 只有 4 法力、手牌《忠实的工坊主》基础费用 3 且受到战场据守加费时，prompt 显示 `minimumManaCost = 4`，前端详情抽屉显示“费用 4 / 战场加费 +1”。
- 已补测试与 smoke：扩展 `P79BattlefieldHeldUnitCostIncreaseSeedAddsOneToUnitPlayCost`，在提交前断言 Hub prompt 的 `manaCost = 3`、`minimumManaCost = 4`、`battlefieldHeldUnitCostIncreaseMana = 1`，再提交同一命令验证 `COST_PAID.mana = 4` / `baseMana = 3` / `battlefieldHeldUnitCostIncreaseMana = 1`。后台 headless Chrome/CDP smoke 使用房间 `smoke-held-unit-cost-fhth3xdd`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-held-unit-cost-increase`；P1 打开《忠实的工坊主》详情抽屉看到“费用 4 / 战场加费 +1”，点击“确认打出”，事件日志显示“支付费用”，P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 stack 为空、`P1-UNIT-CRAFTSMAN` 与生成的 `P1-UNIT-CRAFTSMAN-TOKEN-001` 位于 P1 基地且 P1 法力为 0；reload/reconnect 后仍恢复最终 snapshot。整体仍 **NOT READY**，因为这只是代表性加费 prompt/UI 闭环，完整 PaymentEngine 的费用来源、替代/额外/减费/加费、费用目标和所有支付窗口仍未统一。
- P0-005 第五十六批补充：修复代表性“Core 已支持战场装备减费，但 `PLAY_CARD.sourceRequirements` 仍用未减免费用过滤手牌”的 prompt/UI 缺口。`PromptMinimumManaCost` 现在会按服务端同源逻辑计入战场装备减费，并在每来源 metadata 中公开 `battlefieldEquipmentCostReductionMana`；`battlefield-static-equipment-cost-reduction` seed 中 P1 只有 1 法力、手牌《长剑》基础费用 2 且控制《奥恩的锻炉》时，prompt 会公开《长剑》，`minimumManaCost = 1`，前端详情抽屉显示“费用 1 / 战场减费 -1”。
- 已补测试与 smoke：扩展 `P79BattlefieldStaticEquipmentCostReductionSeedPaysReducedEquipmentCost`，在提交前断言 Hub prompt 的 `manaCost = 2`、`minimumManaCost = 1`、`battlefieldEquipmentCostReductionMana = 1`，再提交同一命令验证 `COST_PAID.mana = 1` 与 `battlefieldEquipmentCostReductionMana = 1`。后台 headless Chrome/CDP smoke 使用房间 `smoke-equipment-reduction-qelxo0mo`：P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battlefield-static-equipment-cost-reduction`；P1 打开《长剑》详情抽屉看到“费用 1 / 战场减费 -1”，点击“确认打出”，事件日志显示“支付费用”，P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 stack 为空、`P1-EQUIPMENT-LONG-SWORD` 位于 P1 基地且 P1 法力为 0；reload/reconnect 后仍恢复最终 snapshot。整体仍 **NOT READY**，因为这只是代表性装备减费 prompt/UI 闭环，完整 PaymentEngine 的替代/额外/减费/加费、费用目标和所有支付窗口仍未统一。
- P0-005 第五十五批补充：继续补齐 Echo prompt 与 Core 费用计划的一致性。`PLAY_CARD.sourceRequirements.optionalCostChoices` 现在会识别 `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:<playerId>` 回合内效果；当战场据守授予“下一个法术获得回响”时，即使该法术本身没有原生 Echo，也会公开 `ECHO` 候选，标签使用该法术基础费用作为额外费用，并通过 reason 标出“战场效果授予此法术回响”。
- 新增 Development-only `battlefield-held-next-spell-echo-prompt` seed，直接构造 P2 已获得下一个法术 Echo、手牌《台前作秀》、4 法力且轮到 P2 的 prompt 场景。新增 `P79BattlefieldHeldNextSpellEchoPromptOffersGrantedEchoAndRepeatsThroughHub`，覆盖 Hub prompt 中授予 Echo 候选、提交后 `COST_PAID.mana = 4` / `baseMana = 2`、`BATTLEFIELD_TRIGGER_RESOLVED.trigger = BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`、`STACK_ITEM_ADDED.effectRepeatCount = 2`、双方让过优先权后抽 2、法术入墓且法力扣空。
- Chrome/CDP smoke：Browser Use IAB 仍不可用，继续使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-held-echo-prompt-3ak2nocn`；P2 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `battlefield-held-next-spell-echo-prompt`。P2 打开《台前作秀》详情抽屉，页面显示“回响：额外支付 2 法力”，选择后确认启用并提交。事件日志显示“支付费用 / 加入结算链”，后台事件包含 `BATTLEFIELD_TRIGGER_RESOLVED`；P2 通过 UI 让过优先权、后台 P1 让过后 authoritative snapshot 中 stack 为空，P2 抽到两张牌，`P2-SPELL-CENTER-STAGE` 入墓，P2 法力为 0；reload/reconnect 后页面仍恢复废牌堆 1 与空结算链。整体仍 **NOT READY**，因为这只是授予 mana-only Echo 的代表路径，复杂 Echo 费用和全支付窗口 PaymentEngine 仍未完成。
- P0-005 第五十四批补充：修复代表性“Core 已支持战场 Echo 减免，但 `PLAY_CARD.sourceRequirements.optionalCostChoices` 仍按未减免费用判断”的前端候选缺口。`PlayCardOptionalCostChoicesForBehavior` 现在会按服务端同源的战场效果计算有效 Echo 额外法力；`battlefield-static-echo-cost-reduction` seed 中 P1 只有 3 法力、基础费用 2、Echo 原费用 2 且《玛莱尖塔》减免 1 时，prompt 会公开 `ECHO`，标签为“回响：额外支付 1 法力”，并通过 reason 标出“战场效果已减免 1 法力”。
- 已补测试与 smoke：扩展 `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost`，在提交前断言 Hub prompt 的 `optionalCostChoices` 已公开减免后的 `ECHO` 候选，再提交同一命令验证 `COST_PAID.mana = 3`、`battlefieldEchoCostReductionMana = 1` 和 `STACK_ITEM_ADDED.effectRepeatCount = 2`。后台 headless Chrome/CDP smoke 使用房间 `smoke-echo-reduction-ldgywesc`：P1 Web UI 连接后打开《台前作秀》详情抽屉，看到并选择“回响：额外支付 1 法力”，确认打出后事件日志显示“支付费用 / 加入结算链”，P1 通过 UI 让过优先权、后台 P2 让过后最终 snapshot 中 stack 为空、P1 抽到两张牌、`P1-SPELL-CENTER-STAGE` 入墓、P1 法力为 0；reload/reconnect 后仍恢复废牌堆 1 与空结算链。
- 验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 已顺序重跑通过，0 warning/0 error；目标回归 `P79BattlefieldStaticEchoCostReductionSeedPaysReducedEchoCost|P79BattlefieldStaticReducesEchoCost` 通过 2/2。本批已优先尝试 Browser Use，但 IAB backend 仍未发现，故按用户要求使用后台 headless Chrome/CDP，未使用 Computer Use 抢前台 Chrome。整体仍 **NOT READY**，因为这只补齐 mana-only Echo 减免的 prompt/UI 代表路径，复杂 Echo 费用、替代/加税/费用目标和非出牌支付窗口仍未统一进完整 PaymentEngine。
- P0-005 第五十三批补充：把代表性 Spellshield 多目标加税从 Core/fixture 证据补到 Hub 与真实前端。`PLAY_CARD.sourceRequirements` 新增 `legalTargetSelections`，当目标组合合法性依赖总目标战力上限或目标上的 Spellshield 税时，服务端枚举可提交的目标对象组合；前端详情抽屉只按该服务端列表启用确认按钮，当前目标组合不在列表中时显示“当前目标组合不在服务端合法组合中。”，不从卡面文本或本地规则自行裁决。
- 新增 Development-only `spellshield-multiple-tax` seed，构造 P1 用《妖异狐火》同时指定 P2 的 Spellshield / Spellshield2 单位且排除 5 战力非合法目标的支付场景。新增 `P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub`，覆盖 Hub seed、`legalTargetSelections` 包含两个法盾目标、不包含高战力目标、`COST_PAID` 记录 `spellshieldTaxMana = 3` 与两个加税目标、双方让过优先权后两个法盾单位入墓、非合法目标留场、P1 法力扣空。
- 验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `CoreRuleEngineRejectsMultipleSpellshieldTaxWhenManaIsInsufficient|P4SpellshieldTaxAggregatesMultipleEnemySpellTargets|P4MultipleSpellshieldTaxInsufficientFixture|P79SpellshieldMultipleTaxSeedEnumeratesLegalTargetsAndPaysThroughHub` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 100/100；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2946/2946。
- Chrome/CDP smoke：本批已优先尝试 Browser Use，但本地 IAB backend 未发现；按用户要求未使用 Computer Use 抢前台 Chrome，改用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-spellshield-tax-wxp9cgbm`；P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `spellshield-multiple-tax`。P1 打开《妖异狐火》详情抽屉，先选择 5 战力目标时页面提示“当前目标组合不在服务端合法组合中。”且确认禁用；改选 Spellshield 与 Spellshield2 两个服务端合法目标后确认启用并提交。事件日志显示“支付费用”“加入结算链”；P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中 stack 为空，P2 战场只剩 `P2-SPIRIT-FIRE-KEEPER-001`，两个法盾单位进入 P2 墓地，P1《妖异狐火》进入 P1 墓地且 P1 法力为 0；reload/reconnect 后页面仍恢复同一最终 snapshot。整体仍 **NOT READY**，因为这是 `PLAY_CARD` 目标组合和加税的代表性产品闭环，不是 `[A]`、所有 `[C]` 资源技能、替代/减费/加税、费用目标和非出牌支付窗口的完整 PaymentEngine。
- P0-005 第五十二批补充：补齐代表性“支付失败不改变权威状态”的 hash 级证据。`CoreRuleEngineRejectsEchoWhenManaIsInsufficient`、`P7PlayCardRejectsOverRecycledPaymentResourceActions`、`P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower`、`CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient` 和 `CoreRuleEngineRejectsMultipleSpellshieldTaxWhenManaIsInsufficient` 现在都会在提交前记录 `MatchStateHasher.Hash(state)`，拒绝后要求 `MatchStateHasher.Hash(result.State)` 完全一致，覆盖 Echo 额外费用、支付资源过量、Haste 错色、单目标 Spellshield 税和多目标 Spellshield 税。
- 验证结果：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRejectsEchoWhenManaIsInsufficient|FullyQualifiedName~P7PlayCardRejectsOverRecycledPaymentResourceActions|FullyQualifiedName~P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower|FullyQualifiedName~CoreRuleEngineRejectsSpellshieldTaxWhenManaIsInsufficient|FullyQualifiedName~CoreRuleEngineRejectsMultipleSpellshieldTaxWhenManaIsInsufficient"` 通过 5/5；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2945/2945。本批无前端源码改动，未新增 browser smoke。整体仍 **NOT READY**，因为这只是代表性 hash 护栏，还不是全命令、全支付窗口的统一 PaymentEngine 原子性证明。
- P0-005 第五十一批补充：把已实现 `HASTE_READY` 的 34 张官方 Haste 卡从“只有 Sivir 代表路径带颜色”推进到 registry/profile/fixture 全面记录官方颜色。`CardBehaviorDefinition.HasteReadyPowerTrait` 现在覆盖红/绿/蓝/黄/橙/紫各色 Haste 卡，`CardPermissionKeywordProfile` 同步公开 `HasteReadyPowerTrait`，服务端 prompt 与命令侧沿用上批机制按 typed power 校验；前端无需新增裁决逻辑，仍只消费服务端 `sourceRequirements.hasteReadyPowerTrait`。
- 已补测试：新增 `P4HasteReadyProfilesCarryOfficialColoredPowerTrait` 覆盖 34 张 Haste 卡的 profile 与 registry trait；所有成功类 `p4-play-*-haste-ready.fixture.json` 初始资源从泛化 `power = 1` 改为对应官方 `powerByTrait.<trait> = 1`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P4HasteOptionalReadyBranch|P4HasteReadyProfilesCarryOfficialColoredPowerTrait|P4PermissionKeywordProfilesMapOfficialTextToRegistryFlags|P4PermissionKeywordProfileIncludesJinx` 通过 74/74；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 99/99；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2945/2945。
- Browser smoke 说明：本批未改前端源码或新增 UI 控件，`HASTE_READY` 的颜色展示/支付资源禁用逻辑已由上一批 `haste-payment-colored-recycle` 真实 UI smoke 覆盖；本批仅扩展服务端 registry/profile/fixture 数据证据，未重复启动浏览器 smoke。整体仍 **NOT READY**，剩余阻断集中在 `[A]`、所有支付步骤中的 `[C]` 资源技能、单阵营/多阵营费用、Spellshield 全支付窗口加税、Echo 复杂费用、替代/减费/加税/费用目标和非出牌支付窗口统一 PaymentEngine。
- P0-005 第五十批补充：把《希维尔》`HASTE_READY` 从代表性任意 1 符能推进到官方文本要求的紫色符能代表路径。`CardBehaviorDefinition` 新增 `HasteReadyPowerTrait`，`SFD·143/221` / `SFD·143a/221` 配置为 `purple`；服务端 `PLAY_CARD.sourceRequirements` 新增 `hasteReadyPowerTrait`，prompt 只有在当前紫色符能或服务端支付资源可补足时才公开 `HASTE_READY`，命令侧把该费用纳入 typed power 校验，蓝色回收符文不能伪装支付紫色急速费用。
- 已补测试与 smoke：更新两份 Sivir `HASTE_READY` fixture 为 `powerByTrait.purple = 1`，新增 `P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower` 覆盖只有 blue power 时拒绝且状态不变；新增 Development-only `haste-payment-colored-recycle` seed 与 `P79HasteColoredPaymentRecycleSeedRequiresMatchingTraitThroughHub`，覆盖 prompt `hasteReadyPowerTrait = purple`、blue/purple 支付资源贡献元数据、blue + `HASTE_READY` 被 `InsufficientCost` 拒绝、purple + `HASTE_READY` 成功。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P4HasteOptionalReadyBranchPaysManaAndPowerForSivir|P4HasteOptionalReadyBranchPaysManaAndPowerForSivirAltA|P4HasteOptionalReadyBranchRejectsSivirWrongTraitPower|P79HasteColoredPaymentRecycleSeedRequiresMatchingTraitThroughHub|P79HastePaymentRecycleSeedPaysReadyBranchThroughHub` 通过 5/5；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2911/2911。
- Chrome/CDP smoke：本批工具上下文未提供可调用 Browser Use，且前序 Computer Use 获取独立 Chrome 窗口失败；为避免抢用户前台，继续使用后台 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-haste-colored-nflnfbtp`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `haste-payment-colored-recycle`。P1 打开《希维尔》详情抽屉，选择“急速活跃：额外支付 1 法力 / 1 紫色符能”后，blue `翠意符文` 支付资源按钮禁用且点击不会选中，purple `摧破符文` 可选；选择 purple 后“确认打出”启用并提交。事件日志显示“回收符文 / 获得符能 / 支付费用 / 加入结算链”；P1 通过 UI 让过优先权、后台 P2 让过后 authoritative snapshot 中《希维尔》位于 P1 基地且 `isExhausted = false`，blue 符文仍在基地，purple 符文离开基地进入符文牌堆，`runeDeckCount = 2`，stack 为空；reload/reconnect 后页面仍恢复该最终 snapshot。
- 复审结论补充：本批关闭的是“急速支付资源 UI/Hub 仍可能接受错色符能”的代表性产品风险。整体仍 **NOT READY**，因为当前只把 Sivir 代表路径接入颜色精确 Haste；其它 Haste 卡、多特性/无特性 `[A]`、Echo、Spellshield 全支付窗口加税、替代/减费/费用目标和所有非出牌支付窗口仍未统一进完整 PaymentEngine。
- P0-005 第四十九批补充：把代表性 `HASTE_READY` 急速活跃支付资源路径从 Core 级证据补到 Hub 与真实前端。新增 Development-only `haste-payment-recycle` seed，构造 P1 有 5 法力、0 符能、手牌《希维尔》、基地有一张可回收紫色符文的场景；服务端 prompt 同时公开 `HASTE_READY` 与 `RECYCLE_RUNE:P1-RUNE-PURPLE-HASTE-PAYMENT-001`，并通过 `hasteReadyPowerCost = 1` 与 `paymentResourceChoices` 告诉前端必须选择服务端支付资源才能提交。
- 已补测试与 smoke：新增 `P79HastePaymentRecycleSeedPaysReadyBranchThroughHub`，覆盖 Hub seed、prompt 元数据、`RECYCLE_RUNE:* + HASTE_READY` 提交、`COST_PAID` 的 mana/power/optional/paymentResourceActions 记录、双方让过优先权后 `UNIT_PLAYED_TO_BASE.hasteReadyOptionalCostPaid = true`，以及《希维尔》进入基地且未横置。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P79HastePaymentRecycleSeedPaysReadyBranchThroughHub|P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction` 通过 2/2；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2909/2909。
- Chrome/CDP smoke：本轮已优先尝试 Browser Use IAB 与 Computer Use，二者均不可用或无法取得 Chrome 窗口，因此继续使用独立 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-haste-payment-recycle-ui-k9ghd440`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `haste-payment-recycle`。P1 打开《希维尔》详情抽屉，选择“急速活跃：额外支付 1 法力 / 1 符能”后确认禁用；选择紫色“回收符文支付”后确认启用并提交，事件日志显示“回收符文 / 支付费用 / 加入结算链”。P1 通过 UI 点击“让过优先权”，后台 P2 让过后 authoritative snapshot 中《希维尔》位于 P1 基地且 `isExhausted = false`，紫色符文离开基地、`runeDeckCount = 2`、purple typed power 已花掉；reload/reconnect 后页面仍恢复《希维尔》与符文牌堆计数 2 的最终 snapshot。
- 复审结论补充：本批关闭的是“急速代表性支付资源路径只有 Core 证据，前端可能无法按服务端候选组合提交”的产品风险。整体仍 **NOT READY**，因为当前 Haste 仍是代表性 `1 mana + 1 power` 模型，还不是完整官方颜色精确费用、替代/加税和所有支付窗口统一 PaymentEngine。
- P0-005 第四十八批补充：补齐通用 `SPEND_POWER:<amount>` 在混合 trait 支付资源下的代表性边界。新增 Development-only `typed-power-payment-generic-mixed-recycle` seed 入口，复用 P1 当前有 1 点 red power、基地有 red/blue 两张可回收符文、手牌为《弹幕时间》的场景；区别在于本批验证的是泛化 `SPEND_POWER:2`，而不是 typed `SPEND_POWER:red:2`。前端在该泛化费用下应允许 red 或 blue 任一服务端支付资源补足 1 点缺口，但选择其中一张后必须禁用另一张，避免过量回收。
- 已补测试与 smoke：新增 `P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution`，覆盖 prompt 暴露 `SPEND_POWER:2`、red/blue 支付资源贡献元数据、blue + `SPEND_POWER:2` 被服务端接受、blue 符文进符文牌堆、red 符文留在基地、stack `damageAmount = 2` 且 red/blue typed power 都被泛化费用花掉；新增 `P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub`，覆盖 Hub seed 与同一提交路径。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub|P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub` 通过 4/4；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2908/2908。
- Chrome/CDP smoke：已按用户要求优先尝试 Browser Use IAB，但本轮仍无可发现后端；随后尝试 Computer Use 控制独立 Chrome，返回 `cgWindowNotFound`，因此使用该独立 Chrome 的 CDP 端口完成断言。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-generic-mixed-recycle-ui-l9qe3xqc`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `typed-power-payment-generic-mixed-recycle`。P1 打开《弹幕时间》详情抽屉并选择 `支付 2 符能` 后，red/blue 两张支付资源均可选且“确认打出”禁用；选择 blue 支付资源后，blue 变为已选、red 禁用、确认启用。提交后事件日志显示“回收符文 / 支付费用 / 加入结算链”；authoritative snapshot 中 blue 符文离开基地、red 符文仍在基地、`runeDeckCount = 2`、stack item `damageAmount = 2`、red/blue typed power 均为空；reload/reconnect 后页面仍恢复 `damageAmount = 2` 与符文牌堆计数 2 的最终 stack snapshot。
- 复审结论补充：本批关闭的是“泛化 X 符能费用可能被 typed 支付资源筛选逻辑误伤，或允许一次选入多张资源”的代表性产品风险。整体仍 **NOT READY**，因为完整 PaymentEngine 仍未覆盖所有官方费用、替代/加税、费用目标和非出牌支付窗口。
- P0-005 第四十七批补充：补齐 typed 支付下错 trait 支付资源的产品级边界。新增 Development-only `typed-power-payment-mixed-recycle` seed，构造 P1 当前有 1 点 red power、基地同时有一张 red 与一张 blue 可回收符文、手牌为《弹幕时间》的场景。服务端 prompt 会同时暴露两张 `RECYCLE_RUNE:*` 支付资源，并通过 `paymentResourcePowerByChoice` 标明 red/blue trait；前端在选择 `SPEND_POWER:red:2` 时只能把 red 资源当作可补足资源，blue 资源保持禁用。
- 已补测试与 smoke：新增 `P7PlayCardPaymentResourceContributionMetadataSeparatesTraits`，覆盖 red/blue 支付资源贡献元数据、blue + `SPEND_POWER:red:2` 被服务端 `InsufficientCost` 拒绝且状态不变、red + `SPEND_POWER:red:2` 接受并只回收 red；新增 `P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub`，覆盖 Hub seed、错 trait 拒绝和匹配 red 提交。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub` 通过 2/2；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2906/2906。
- Chrome/CDP smoke：Browser Use IAB 本轮仍不可用，继续使用独立可见 Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-mixed-recycle-ui-nieihg25`；P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-mixed-recycle`。P1 选择 `支付 2 红色符能` 后，支付资源组中 blue 资源禁用且点击不会选中，red 资源可选；选中 red 后确认启用并提交。authoritative snapshot 中 red 符文离开基地、blue 符文仍在基地、`runeDeckCount = 2`、stack item `damageAmount = 2`；reload/reconnect 后页面仍恢复该 stack snapshot。
- 复审结论补充：本批关闭的是“typed 费用 UI 可能把错 trait 支付资源当作可补足资源”的代表性产品风险。整体仍 **NOT READY**，因为完整 PaymentEngine 仍未覆盖所有官方费用、替代/加税和非出牌支付窗口。
- P0-005 第四十六批补充：在防过量回收之后，补齐“真实需要多张支付资源”代表性证据。新增 Development-only `typed-power-payment-double-recycle` seed，构造 P1 当前没有红色符能、基地有两张可回收红色符文、手牌为《弹幕时间》的场景；`SPEND_POWER:red:2` 必须同时选择两张服务端 `RECYCLE_RUNE:*` 支付资源才能合法。该批验证前端的精确资源选择不是简单“最多只能选一张”，而是按 `paymentResourcePowerByChoice` 求和到刚好满足缺口。
- 已补测试与 smoke：新增 `P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions`，覆盖 CoreRuleEngine 在两张回收符文均为必要时接受命令、广播两次 `RUNE_RECYCLED`、`COST_PAID.paymentResourceActions` 记录两项、两张符文都进入符文牌堆且 stack `damageAmount = 2`；新增 `P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub`，覆盖 Hub seed prompt 与提交。验证结果：初次并行 build/test 命中 `obj` cache 文件锁后已顺序重跑；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|P7PlayCardRejectsOverRecycledPaymentResourceActions|P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub` 通过 3/3；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2904/2904。
- Chrome/CDP smoke：按要求优先尝试 Browser Use IAB，但本轮仍无可发现后端；Computer Use 读取直启测试 Chrome 时返回 `cgWindowNotFound`，因此继续使用独立可见 Chrome 的 CDP 端口完成自动化断言。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-double-recycle-ui-w3n10ikh`；P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-double-recycle`。P1 选择 `支付 2 红色符能` 后确认禁用；选择第一张支付资源后确认仍禁用、第二张仍可选；两张都选择后确认启用并提交。事件日志显示“回收符文 / 支付费用 / 加入结算链”；authoritative snapshot 中两张回收符文均离开基地、`runeDeckCount = 3`、stack item `damageAmount = 2`；reload/reconnect 后页面仍恢复该 stack snapshot。
- 复审结论补充：本批关闭的是“防过量回收 UI 可能误伤合法多资源支付”的代表性回归风险。整体仍 **NOT READY**，因为完整 PaymentEngine 仍需覆盖官方多阵营/万能符能分配、费用目标、替代费用、加税和非出牌支付窗口。
- P0-005 第四十五批补充：`PLAY_CARD.sourceRequirements` 新增 `paymentResourcePowerByChoice`，按每个服务端支付资源 token 暴露本次可贡献的 trait 与 power。前端详情抽屉不再只知道“有几个 `RECYCLE_RUNE:*` 可以作为支付资源”，而是能在选择 `SPEND_POWER:<trait>:<amount>` 或代表性 `HASTE_READY` 后，依据服务端提供的当前可用符能和逐资源贡献，要求用户选择刚好补足缺口的资源。资源不足时“确认打出”禁用；已刚好满足时，额外支付资源按钮禁用；trait 不匹配或服务端未给出贡献元数据的 token 不会被计入合法选择。
- 已补测试与 smoke：扩展 `P7PlayCardRejectsOverRecycledPaymentResourceActions`，断言两个红色回收符文支付 token 都带有 `paymentResourcePowerByChoice[choice].trait = red` 和 `power = 1`；新增 Development-only `typed-power-payment-over-recycle` seed，构造“当前 1 红 + 两张可回收红符文 + 只需 `SPEND_POWER:red:2`”的 UI 场景。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardRejectsOverRecycledPaymentResourceActions|P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub` 通过 3/3；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2902/2902。
- 后台 Chrome/CDP smoke：Browser Use IAB 后端本轮仍未暴露可连接实例；按后台 smoke 路径使用 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-over-recycle-ui-x3dj3adn`；P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-over-recycle`。P1 打开《弹幕时间》详情抽屉并选择 `支付 2 红色符能` 后，“确认打出”保持禁用；支付资源组显示两张服务端候选红色符文，选择第一张后确认启用，第二张立即禁用且点击不会加入命令。提交后事件日志显示“回收符文 / 支付费用 / 加入结算链”；authoritative snapshot 中 stack item `damageAmount = 2`、只回收 `RECYCLE_RUNE:P1-RUNE-RED-EXTRA-PAYMENT-001`、另一张 `P1-RUNE-RED-PARTIAL-PAYMENT-001` 留在基地、`runeDeckCount = 2`、`runePool.powerByTrait.red` 已扣空；reload/reconnect 后页面仍恢复包含 `P1-SPELL-BULLET-TIME` 与 `damageAmount` 的 stack snapshot。
- 复审结论补充：本批把 P0-005 从“服务端能拒绝过量回收”推进到“前端可依据服务端逐资源贡献元数据阻止用户组合出过量回收命令”的代表性产品闭环。整体仍 **NOT READY**，因为这仍不是完整 PaymentEngine：多阵营/万能符能分配、费用目标、替代/加税、非出牌支付窗口和所有官方费用类型还未统一。
- P0-005 第四十四批补充：`CoreRuleEngine` 现在会拒绝 `PLAY_CARD` 中过量选择的 `RECYCLE_RUNE:*` 支付资源动作。服务端在构建支付计划时不只检查“当前符能是否已经足够”，还会逐个移除已选择的回收符文重新计算支付池；只要少回收其中任意一张仍可支付本次 `SPEND_POWER:*` / typed power cost，就判定该资源动作不是本次费用所必需并拒绝命令。这样可以阻止“现有 1 红、两张可回收红符文、只支付 `SPEND_POWER:red:2` 却回收两张”的过量资源污染，仍允许确实需要两张资源动作的更高金额支付。
- 已补测试：新增 `P7PlayCardRejectsOverRecycledPaymentResourceActions`，覆盖过量回收被 `InvalidTarget` 拒绝、事件为空、基地/符文牌堆/typed red 符能和结算链均不改变。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardRejectsOverRecycledPaymentResourceActions|P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|P7PlayCardRecyclesRuneAsPaymentResourceAction|P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction|P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub` 通过 5/5。
- 复审结论补充：本批把 P0-005 的支付资源组合从“可补足”推进到“不能过量污染资源池”的代表性服务端兜底。整体仍 **NOT READY**，因为这仍不是完整 PaymentEngine：前端还没有逐资源结构化需求量、服务端还未覆盖所有支付窗口与所有官方费用类型，也没有完成多阵营费用、费用目标选择和替代/加税支付矩阵。
- P0-005 第四十三批补充：`PLAY_CARD.sourceRequirements` 现在把当前已可用符能与“回收符文支付资源”拆成结构化元数据：`availablePower`、`availablePowerByTrait`、`availablePowerWithPaymentResources`、`availablePowerByTraitWithPaymentResources`、`paymentResourceChoices` 和 `hasteReadyPowerCost`。对《弹幕时间》这类 X 符能法术，若 P1 当前只有 `powerByTrait.red = 1`，但基地有一张可回收红色符文，服务端会公开 `SPEND_POWER:red:2` 与 `RECYCLE_RUNE:<objectId>`，表示该 2 点红色符能金额只能在同一服务端候选组合中通过回收符文补足。前端详情抽屉同步把 `paymentResourceChoices` 从普通可选费用中分离，用户选择超出现有符能的 `SPEND_POWER:*` 或需要额外符能的代表性 `HASTE_READY` 时，必须再选择服务端给出的支付资源，确认按钮才会启用；如果当前已足够支付，则不会把多余的 `RECYCLE_RUNE:*` 夹带进命令。
- 已补测试与 smoke：新增 `P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount` 和 `P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub`，并新增 Development-only `typed-power-payment-recycle` seed，覆盖“现有 1 红 + 回收 1 红 -> `SPEND_POWER:red:2`”的 prompt、Hub seed、事件与最终 snapshot。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|P7PlayCardPromptOffersSpendPowerAmountsByAvailableTraitPower|P7PlayCardRecyclesRuneAsPaymentResourceAction|P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction|P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub|P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub` 通过 6/6；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 94/94；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2901/2901。
- 后台 Chrome/CDP smoke：Browser Use IAB 后端未暴露可连接实例，本批按不抢前台原则继续使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-typed-power-recycle-1uj64kjn`；P1 Web UI 连接后，后台 SignalR 让 P1/P2 入座并 seed `typed-power-payment-recycle`。P1 打开《弹幕时间》详情抽屉后，页面同时显示 `支付 2 红色符能` 与 `回收符文支付`；只选择 `支付 2 红色符能` 时“确认打出”保持禁用，选择 `回收符文支付：P1-RUNE-RED-PARTIAL-PAYMENT-001` 后才启用并提交。事件日志出现“回收符文 / 支付费用 / 加入结算链”；额外 SignalR 校验 authoritative snapshot 中 stack item `damageAmount = 2`、P1 基地为空、`runeDeckCount = 2`、`runePool.powerByTrait` 已无 red，reload/reconnect 后页面仍恢复包含 `P1-SPELL-BULLET-TIME` 与 `damageAmount` 的 stack snapshot。
- 复审结论补充：本批关闭的是“X 符能金额虽然已枚举，但前端无法区分该金额是否需要服务端支付资源动作补足”的代表性产品缺口。整体仍 **NOT READY**，因为这仍不是统一 PaymentEngine：多支付资源最小化、费用目标选择、所有非出牌支付窗口、同阵营/多阵营费用、Spellshield/Echo/Haste 的官方完整费用模型仍未全部收敛。
- P0-005 第四十二批补充：`PLAY_CARD.sourceRequirements[].optionalCostChoices` 现在会按当前服务端可支付符能枚举 X 符能金额候选，而不再只公开 1 点代表项。以《弹幕时间》这类 `DamageAmountFromOptionalPowerCost` 法术为例，若 P1 当前有 `powerByTrait.red = 2`，prompt 会同时公开 `SPEND_POWER:1`、`SPEND_POWER:2`、`SPEND_POWER:red:1`、`SPEND_POWER:red:2`，并且不会公开超过可支付上限的 `SPEND_POWER:3` / `SPEND_POWER:red:3`。前端详情抽屉同步把所有 `SPEND_POWER:*` 可选费用视为同一组单选，用户先点 1 点再点 2 点时，最终命令只提交服务端候选中的一个 spend-power token，避免组合出服务端会拒绝的多金额费用列表。
- 已补测试与 smoke：新增 `P7PlayCardPromptOffersSpendPowerAmountsByAvailableTraitPower` 和 `P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub`，并新增 Development-only `typed-power-payment` seed，覆盖 prompt 金额候选、Hub seed、`SPEND_POWER:red:2` 入栈 damage = 2、typed red 符能扣空。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P7PlayCardPromptOffersSpendPowerAmountsByAvailableTraitPower|P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait|P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing|P7PlayCardRecyclesRuneAsPaymentResourceAction|P79TypedPowerPaymentSeedOffersAmountChoicesAndPlaysThroughHub` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 93/93；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2899/2899。
- 后台 Chrome/CDP smoke：本轮工具上下文仍未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-typed-power-jvf0djpz`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `typed-power-payment`。P1 点击《弹幕时间》打开 `PLAY_CARD` 组合器，页面显示 `支付 1 符能` 与 `支付 2 红色符能`；先点击 1 点再点击 2 点红色并确认后，事件日志出现“打出卡牌 / 支付费用 / 加入结算链”。额外 SignalR 校验 authoritative snapshot 中 stack item `damageAmount = 2`，P1 `runePool.powerByTrait` 已无 red，reload/reconnect 后页面仍恢复包含 `P1-SPELL-BULLET-TIME` 与 `damageAmount:2` 的最终 snapshot。
- 复审结论补充：本批关闭的是“X 符能法术 prompt 只公开 1 点，前端无法从服务端候选选择任意可支付数量”的代表性产品缺口。整体仍 **NOT READY**，因为 PaymentEngine 尚未统一：多资源动作组合策略、费用目标选择、所有非出牌支付窗口、同阵营/多阵营费用、Spellshield/Echo/Haste 等完整费用模型仍未全部收敛。
- P0-004 第四十一批补充：`MatchState` 新增最近战斗结果权威状态 `BattleResolutions`，snapshot `timing.battleResolutions` 会保留最近 `BATTLE_CLOSED` / `BATTLE_NO_RESULT` 的结构化结果。该视图记录 battle id、攻防玩家、胜者、攻防对象、幸存攻防对象、被摧毁对象和相关事件种类，解决 reload/reconnect 后只能从一次性事件日志或墓地状态反推刚刚发生过什么战斗的问题。前端 `StackPanel` 只读展示该 snapshot 视图，例如“战斗结束：P1”或“战斗无结果：无胜者”，不自行计算战斗胜负。
- 已补测试与 smoke：扩展 `P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath`、`P4DeclareBattleCommandAssignsDamageForMultiAttackerMultiDefenderRepresentativePath`、`P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed` 和 `P79CombatMultiParticipantSeedOffersSecondAttackerAndSecondDefender`，覆盖 `BattleResolutions`、snapshot `timing.battleResolutions`、胜者/无胜者、幸存对象和摧毁对象。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；目标回归 `P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath|P4DeclareBattleCommandAssignsDamageForMultiAttackerMultiDefenderRepresentativePath|P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed|P79CombatMultiParticipantSeedOffersSecondAttackerAndSecondDefender` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 92/92；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2897/2897。
- 后台 Chrome/CDP smoke：本轮工具上下文仍未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-battle-resolution-7u0qmwvz`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-multi-participant`。P1 通过前端提交 2 攻 + 2 防战斗后，右侧规则队列显示 `战斗结束：P1`；额外 SignalR 校验 `timing.battleResolutions[0]` 为 `kind = CLOSED`、`winnerPlayerId = P1`、幸存攻击者 `P1-BATTLE-MULTI-PARTICIPANT-YI`、摧毁对象为盖伦与 P2 两个防守者。reload/reconnect 后前端仍从 authoritative snapshot 显示 `战斗结束：P1`。
- 复审结论补充：本批关闭的是“最近 battle 结果只在瞬时事件流中，重连后无法作为 snapshot 事实展示”的代表性产品缺口。整体仍 **NOT READY**，因为这仍不是完整官方 battle task：独立战斗伤害分配 prompt、战斗响应窗口、完整 held/conquer/control task 生命周期、PaymentEngine、LayerEngine 和全官方卡牌证据仍未完成。
- P0-004 第四十批补充：`DECLARE_BATTLE` 代表路径从“最多 2 攻击者或最多 2 防守者”推进到“最多 2 攻击者 + 最多 2 防守者”的组合代表路径。`TryBuildMinimalDeclareBattle` 仍限制攻防各最多 2 个、去重、攻防对象不重叠、均为服务端判定的正面未参战战场单位；多防守者仍要求防守列表至少包含 `壁垒` / `后排` 这类伤害分配关键词，避免前端组合出两个普通防守者并伪装成完整官方分配系统。`DECLARE_BATTLE` metadata 新增 `multiParticipantBattlePolicy = up-to-two-attackers-and-defenders-without-independent-assignment-prompt`，`sourceRequirements` 现在可以同时暴露“攻击单位 2（可选）”与“防守单位 2（可选）”。当正好两个防守者且只有一个带分配关键词时，第二防守槽同时列出两个服务端候选，使前端既可先选关键词单位再选普通单位，也可先选普通单位再选关键词单位；重复选择仍由前端禁用并由服务端兜底拒绝。
- 已补测试与 smoke：新增 `P4DeclareBattleCommandAssignsDamageForMultiAttackerMultiDefenderRepresentativePath`、`P79CombatMultiParticipantSeedOffersSecondAttackerAndSecondDefender`，并新增 Development-only `battle-multi-participant` seed；同步更新 `ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts` 的第二防守槽候选口径。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DeclareBattleCommandAssignsDamageForMultiAttackerMultiDefenderRepresentativePath|FullyQualifiedName~P79CombatMultiParticipantSeedOffersSecondAttackerAndSecondDefender|FullyQualifiedName~P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath|FullyQualifiedName~P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage|FullyQualifiedName~P4DeclareBattleCommandAssignsDamageToBulwarkBeforeBackRowForRepresentativePath|FullyQualifiedName~P79CombatMultiDefenderSeedAssignsBulwarkBeforeBackRow|FullyQualifiedName~P4DeclareBattleCommandPreservesSubmittedOrderForSamePriorityBulwarkDefenders|FullyQualifiedName~P79CombatSamePriorityBulwarkSeedPreservesSubmittedDefenderOrder|FullyQualifiedName~P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed|FullyQualifiedName~P79CombatNoResultSeedEmitsNoResultAndMovesBothParticipantsToGraveyard"` 通过 10/10；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 92/92；`source ../../scripts/dev-env.sh && npm run build` 通过；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2897/2897。
- 后台 Chrome/CDP smoke：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-multi-participant-lk8x90zo`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-multi-participant`。P1 点击《盖伦》打开 `DECLARE_BATTLE` 抽屉，UI 同时显示“攻击单位 2（可选）”“防守单位 1”“防守单位 2（可选）”；按服务端候选选择 `P1-BATTLE-MULTI-PARTICIPANT-YI`、`P2-BATTLE-MULTI-PARTICIPANT-BULWARK`、`P2-BATTLE-MULTI-PARTICIPANT-DEFENDER` 后确认。页面事件日志显示“造成伤害”“战斗结束”；额外 SignalR 校验六条战斗伤害：盖伦对壁垒 4 点、盖伦对普通防守者 2 点、易对普通防守者 3 点、壁垒对盖伦 4 点、普通防守者对盖伦 2 点、普通防守者对易 1 点。最终 authoritative snapshot 中 P1 战场只剩 `P1-BATTLE-MULTI-PARTICIPANT-YI` 且 1 伤害，P1 盖伦进入 graveyard，P2 战场为空，P2 两个防守者进入 graveyard；reload/reconnect 后页面恢复 `UNL-059/219`、`1 伤害`、`废牌堆 1` / `废牌堆 2` 的最终 snapshot。
- 复审结论补充：本批关闭的是“服务端完全拒绝多攻击者 + 多防守者组合”的代表性产品阻断。整体仍 **NOT READY**，因为这还不是完整官方 battle task：仍缺独立战斗伤害分配 prompt、战斗响应窗口、完整 held/conquer/control task 生命周期、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-004 第三十九批补充：补齐“战斗没有结果时的状态”代表性可见性。`DECLARE_BATTLE` 结算后若服务端无法解析出战斗胜者，会广播 `BATTLE_NO_RESULT`，payload 包含攻防对象、幸存攻防对象、攻防玩家与 `reason`；双方同归于尽时 reason 为 `ALL_PARTICIPANTS_DESTROYED`。前端事件日志新增中文标签“战斗无结果”，只展示服务端事件，不自行判断胜负。
- 已补测试与 smoke：新增 `P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed` 和 `P79CombatNoResultSeedEmitsNoResultAndMovesBothParticipantsToGraveyard`，并新增 Development-only `battle-no-result` seed。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed|FullyQualifiedName~P79CombatNoResultSeedEmitsNoResultAndMovesBothParticipantsToGraveyard|FullyQualifiedName~P79CombatSamePriorityBulwarkSeedPreservesSubmittedDefenderOrder|FullyQualifiedName~P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 91/91；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2895/2895；`source ../../scripts/dev-env.sh && npm run build` 通过。
- 后台 Chrome/CDP smoke：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-battle-no-result-0m20w1k8`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-no-result`。P1 点击《盖伦》打开 `DECLARE_BATTLE` 抽屉，按服务端候选选择 `P2-BATTLE-NO-RESULT-DEFENDER` 后确认。页面事件日志显示“造成伤害”“战斗无结果”；额外 SignalR 校验两条 4 点互相伤害、`BATTLE_NO_RESULT.reason = ALL_PARTICIPANTS_DESTROYED`、幸存列表为空、P1/P2 战场均为空、双方单位均在 graveyard 且 `timing.battle.isActive = false`。reload/reconnect 后页面恢复最终 authoritative snapshot。
- 复审结论补充：本批关闭的是 mutual destruction 下“无胜者战斗状态不可见”的代表性缺口。整体仍 **NOT READY**，因为这还不是完整官方 battle task：多攻击者 + 多防守者组合、独立战斗伤害分配 prompt、战斗响应窗口、完整 held/conquer/control task 生命周期、PaymentEngine 和 LayerEngine 仍未完成。
- P0-004 第三十八批补充：补齐“多个相同优先级伤害分配要求，分配玩家可任意排序”的代表性证据。`DECLARE_BATTLE` metadata 新增 `samePriorityAssignmentPolicy = preserve-player-submitted-object-order-within-same-priority`；服务端现有 `BuildBattleDamageAssignmentOrder` 在不同优先级仍强制壁垒优先/后排最后，但在同优先级内保留玩家提交的对象顺序。新增 Development-only `battle-same-priority-bulwark` seed，构造一个攻击者面对两个同为 `壁垒` 的防守者，前端只按服务端 `sourceRequirements.targetChoicesByIndex` 选择顺序并提交。
- 已补测试与 smoke：新增 `P4DeclareBattleCommandPreservesSubmittedOrderForSamePriorityBulwarkDefenders` 和 `P79CombatSamePriorityBulwarkSeedPreservesSubmittedDefenderOrder`。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DeclareBattleCommandPreservesSubmittedOrderForSamePriorityBulwarkDefenders|FullyQualifiedName~P79CombatSamePriorityBulwarkSeedPreservesSubmittedDefenderOrder|FullyQualifiedName~P79CombatMultiDefenderSeedAssignsBulwarkBeforeBackRow|FullyQualifiedName~P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage"` 通过 4/4；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 90/90；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2893/2893；`source ../../scripts/dev-env.sh && npm run build` 通过。
- 后台 Chrome/CDP smoke：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-same-priority-bulwark-orikd055`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-same-priority-bulwark`。P1 点击《沃利贝尔》打开 `DECLARE_BATTLE` 抽屉，UI 显示两个防守槽；按服务端候选先选择 `P2-BATTLE-SAME-BULWARK-B`、再选择 `P2-BATTLE-SAME-BULWARK-A` 后确认。页面事件日志显示“造成伤害”“战斗结束”；额外 SignalR 校验攻击者伤害事件顺序为 B `assignmentIndex = 1 / BULWARK_FIRST / damage = 4`，A `assignmentIndex = 2 / BULWARK_FIRST / damage = 6`。最终 authoritative snapshot 中 P1 战场只剩 `P1-BATTLE-SAME-VOLIBEAR` 且 8 伤害，P2 两个壁垒防守者均进入 graveyard；reload/reconnect 后页面恢复 `废牌堆 2` 与同一最终 snapshot。
- 复审结论补充：本批关闭的是同优先级壁垒分配的代表性“玩家可选择顺序”证据；整体仍 **NOT READY**，因为这还不是完整官方 battle task：多攻击者 + 多防守者组合、独立战斗伤害分配 prompt、战斗响应窗口、完整 held/conquer/control task 生命周期、PaymentEngine 和 LayerEngine 仍未完成。
- P0-004 第三十七批补充：`DECLARE_BATTLE` 代表路径从“单攻击者 + 1-2 防守者”扩展到“1-2 攻击者 + 单防守者”。服务端现在会校验攻击者列表去重、攻防对象不重叠、所有攻击者均为当前玩家战场正面未参战单位；当攻击者超过 1 个时，继续拒绝多防守者，避免前端误开放尚未完整实现的“多攻击者 + 多防守者”官方分配矩阵。战斗结算会按服务端命令顺序让每个攻击者对单防守者造成自身战斗伤害，并让防守者按攻击者顺序分配返伤；触发/征服/据守等既有代表性来源仍以第一攻击者作为来源对象。
- ActionPrompt/前端补充：`DECLARE_BATTLE.sourceRequirements` 新增 `minAttackerCount`、`maxAttackerCount`、`attackerCountLabel` 与 `attackerChoicesByIndex`；只有当服务端给出第二攻击者槽位时，卡牌详情抽屉才显示“攻击单位 2（可选）”。前端提交的 `attackerObjectIds` 只来自这些服务端候选，不从场面或卡面文本自行组合。
- 已补测试与 smoke：新增 `P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath` 和 `P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage`，并新增 Development-only `battle-multi-attacker` seed。验证结果：`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4DeclareBattleCommandAssignsDamageFromMultipleAttackersForRepresentativePath|FullyQualifiedName~P4DeclareBattleCommandAssignsDamageToBulwarkBeforeBackRowForRepresentativePath|FullyQualifiedName~P79CombatMultiAttackerSeedOffersSecondAttackerAndAssignsDamage|FullyQualifiedName~P79CombatMultiDefenderSeedAssignsBulwarkBeforeBackRow|FullyQualifiedName~P79CombatPromptFiltersDeclareBattleCandidatesToLegalBattlefieldUnits"` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 89/89；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2891/2891；`source ../../scripts/dev-env.sh && npm run build` 通过。后台 Chrome/CDP smoke：Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-multi-attacker-56xmafv0`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed `battle-multi-attacker`。P1 点击《盖伦》打开 `DECLARE_BATTLE` 抽屉，UI 显示“攻击单位 2（可选）”和“防守单位 1”；按服务端候选选择 `P1-BATTLE-MULTI-YI` 与 `P2-BATTLE-MULTI-DEFENDER` 后确认，事件日志出现“造成伤害”“战斗结束”。额外 SignalR 校验四条伤害事件分别为盖伦 5、易 2、防守者对盖伦 5、防守者对易 1；最终 snapshot 中 P1 只剩 `P1-BATTLE-MULTI-YI` 且 1 伤害，`P1-BATTLE-MULTI-GAREN` 与 `P2-BATTLE-MULTI-DEFENDER` 进入墓地。reload/reconnect 后页面恢复同一最终 snapshot。
- 复审结论补充：本批关闭的是“官方战斗规则 460.2 要求所有进攻单位总战力参与伤害，但当前代表路径完全不支持多攻击者”的第一段代表性缺口。整体仍 **NOT READY**，因为这不是完整官方 battle task：多攻击者 + 多防守者、独立战斗伤害分配 prompt、战斗响应窗口、完整 held/conquer/control task 生命周期、PaymentEngine 和 LayerEngine 仍未完成。
- 测试门禁第三十五批补充：`ConformanceFixtureRunner` 的旧 `promptActions` 与未标记精确的 `expected.prompts.actions` 现在按“必需动作子序列”比较，解决旧 fixture 只声明 `END_TURN` 但当前服务端合法公开更多 ActionPrompt 候选时的历史误报；`WAIT` 仍保持精确匹配，避免把非行动玩家误判为可行动。需要精确动作列表的新 fixture 可设置 `exactActions: true`，并已补 helper 级测试覆盖缺失动作仍失败、`WAIT` 精确、默认 required-actions 和 opt-in exact 四类语义。
- 同步校正旧测试断言：战斗代表路径现在把 `BATTLE_CLOSED` 作为结算事件并在战后清理攻防标记，相关直接断言已与 fixture 中的权威期望对齐；Xerath 拒绝路径改为断言 typed 红色符能池不被拒绝命令消耗。验证结果：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` 通过 2680/2680；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2889/2889。
- 复审结论补充：本批关闭的是“完整 ConformanceFixtureRunnerTests 被旧 prompt 精确清单口径阻塞”的测试门禁问题，让当前后端 full test 恢复全绿。整体仍 **NOT READY**，因为该批没有实现新的官方规则面，剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、完整法术对决/战斗生命周期、统一 PaymentEngine、LayerEngine 和全官方卡牌证据；本批无前端源码改动，未新增 browser smoke。
- 前端 smoke 第三十六批补充：本轮工具上下文未提供可调用 Browser Use，按不抢前台原则使用后台 headless Chrome/CDP。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-multi-defender-mova8kc6`；P1 Web UI 连接后，后台 SignalR 让 P2 入座并 seed Development-only `battle-multi-defender`。P1 点击《沃利贝尔》打开 `DECLARE_BATTLE` 抽屉，UI 显示“防守单位 1”“防守单位 2”和第二槽“不选择”；按服务端候选选择 `P2-BATTLE-MULTI-LEBLANC` 与 `P2-BATTLE-MULTI-KITTEN` 后确认，浏览器事件日志出现“造成伤害”“战斗结束”。额外 SignalR 校验 `DAMAGE_APPLIED.assignmentRole` 包含 `BULWARK_FIRST` 与 `BACK_ROW_LAST`，P2 snapshot 中两个防守者进入 graveyard。重新打开同房间并点击“连接/重连”后，页面恢复 `废牌堆 2` 与 P1《沃利贝尔》的最终 snapshot。
- 复审结论补充：本批 smoke 关闭了“1-2 防守者 prompt 已有测试但前端双防守槽未做真实浏览器验证”的产品级验证缺口。整体仍 **NOT READY**，因为这仍是代表性单攻击者/最多两个防守者路径，不包含多攻击者、完整战斗响应窗口、官方级 battle task 生命周期或完整 PaymentEngine。
- P0-005 第三十四批补充：`PLAY_CARD.sourceRequirements[].optionalCostChoices` 现在会公开 1 点 typed 符能支付候选，例如 `SPEND_POWER:red:1`。候选来源来自当前 `runePool.powerByTrait` 以及同一出牌支付步骤中服务端公开的 `RECYCLE_RUNE:<objectId>` 支付资源动作，避免前端为了提交 typed 支付而猜测服务端 token。泛化 `SPEND_POWER:1` 仍保留，用于当前代表性 X 符能法术 UI。
- 已补测试：扩展 `P7PlayCardRecyclesRuneAsPaymentResourceAction`，断言 prompt 在只有可回收红色符文、当前无符能的状态下同时公开 `SPEND_POWER:1`、`SPEND_POWER:red:1` 和 `RECYCLE_RUNE:<objectId>`；同一命令提交 `RECYCLE_RUNE:<objectId>` + `SPEND_POWER:red:1` 后继续验证 typed power 先获得再扣空。`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait|FullyQualifiedName~P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing|FullyQualifiedName~ActionPromptPlayCardMetadataFiltersTargetsBySourceRequirement"` 通过 4/4。
- 复审结论补充：本批关闭的是“提交侧支持 typed `SPEND_POWER:<trait>:n`，但前端候选只公开泛化 `SPEND_POWER:1`”的服务端候选缺口。当时 UI 候选仍只覆盖 1 点代表性符能支付；后续第四十二批已把 X 符能金额候选扩展到当前可支付上限。整体仍 **NOT READY**，因为资源动作组合策略和所有支付窗口仍需要统一 PaymentEngine。
- P0-005 第三十三批补充：补齐 `PLAY_CARD` 支付步骤资源动作在 `HASTE_READY` 急速额外费用窗口中的代表性回归证据。此前 `RECYCLE_RUNE:<objectId>` 已覆盖按符能数造成伤害的法术支付，本批新增测试锁住急速单位在当前符能不足、但服务端候选给出可回收基础符文时，可以在同一 `PLAY_CARD` 命令中提交 `RECYCLE_RUNE:<objectId>` + `HASTE_READY`，先获得 typed power，再支付急速额外 1 符能，并在结算后以活跃状态进入基地。
- 已补测试：新增 `P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction`，覆盖 prompt 的 `optionalCostChoices` 同时公开 `HASTE_READY` 与 `RECYCLE_RUNE:<objectId>`、`paymentResourceChoices` 公开同一资源动作、命令结算后符文入 rune deck、位置同步、typed power 被扣空、`COST_PAID` 记录 `paymentResourceActions` / `recycledRuneObjectIds`，并在双方让过优先权后断言 `UNIT_PLAYED_TO_BASE.hasteReadyOptionalCostPaid = true` 且单位未横置。`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4HasteOptionalReadyBranchPaysManaAndPowerForSivir|FullyQualifiedName~P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction"` 通过 4/4；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。额外尝试跑更宽的 `P4PermissionKeywordsKeepExistingP2FixturesGreen` theory 时仍触发既有旧 fixture `promptActions` 口径差异，本批未把该历史大面纳入提交验收。
- 复审结论补充：本批把“支付步骤资源动作只被测试覆盖在 X 符能法术上”的证据扩展到急速额外费用窗口。整体仍 **NOT READY**，因为当前急速仍是代表性 `1 mana + 1 power` 模型，不等同于官方完整颜色精确费用、万能符能计数、减费/加费、替代费用和所有非出牌支付窗口统一 PaymentEngine。
- P0-004 第三十二批补充：`DECLARE_BATTLE.sourceRequirements` 现在与 `TryBuildMinimalDeclareBattle` 的代表路径能力对齐。服务端在每个攻击者 requirement 中继续要求 1 个防守者；当存在至少两个合法防守者且其中包含 `壁垒` / `后排` 这类伤害分配关键词防守者时，额外公开第二个可选防守槽。第二槽只列出带伤害分配关键词的防守者，避免前端组合出“两名普通防守者”这类服务端会拒绝的非法命令；重复选择仍由前端禁用并由服务端兜底拒绝。
- 已补测试：扩展 `ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts`，覆盖 `DECLARE_BATTLE` candidate targets、每来源 `maxDefenderCount = 2`、slot 0 的完整防守者候选和 slot 1 的壁垒/后排限定候选；同步校正 `P4DeclareBattleCommandAssignsDamageToBulwarkBeforeBackRowForRepresentativePath`，断言多防守者代表结算后的 `BATTLE_CLOSED` 与战斗状态清理。`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts|FullyQualifiedName~P4DeclareBattleCommandAssignsDamageToBulwarkBeforeBackRowForRepresentativePath"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~GameHubJoinTests"` 通过 136/136；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批关闭的是“服务端已能代表性结算 1-2 防守者，但 ActionPrompt 每来源候选仍只公开 1 个防守槽”的产品级候选缺口。整体仍 **NOT READY**，因为这仍不是完整官方 battle task：多攻击者、战斗响应窗口、完整 control/held/conquer task 生命周期和官方级法术对决/战斗状态机仍未完成。本批没有前端源码改动；现有前端战斗组合器继续只消费服务端 `sourceRequirements`，双防守槽真实浏览器 smoke 已在后续第三十六批补上。
- P0-005 第三十一批补充：`PLAY_CARD` 支付步骤新增代表性资源动作 token `RECYCLE_RUNE:<objectId>`。服务端会先从 `optionalCosts` 中拆出这些支付资源动作，验证目标是当前玩家基地中正面、受控、带 `COLOR:*` 的基础符文，且当前符能本来不足以支付本次 power cost，再在扣本次出牌费用前把符文回收到符文牌堆底部、获得同特性 typed power，并广播带 `paymentWindow = PLAY_CARD` 的 `RUNE_RECYCLED` / `POWER_GAINED`；随后 `PayRuneCosts` 继续按原有 `SPEND_POWER:*` 扣费。`ActionPrompt` 的 `PLAY_CARD.sourceRequirements` 也新增 `paymentResourceChoices`，并把同一 token 放进 `optionalCostChoices`，让前端只能提交服务端显式给出的资源动作。
- 已补测试：新增 `P7PlayCardRecyclesRuneAsPaymentResourceAction`，覆盖 prompt 暴露 `RECYCLE_RUNE:<objectId>`、出牌命令同时携带回收符文资源动作和 `SPEND_POWER:red:1`、符文入 rune deck、位置同步、typed power 先获得再被扣空、stack damage 按支付符能为 1、`COST_PAID` 记录 `paymentResourceActions` / `recycledRuneObjectIds`。`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait|FullyQualifiedName~P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|FullyQualifiedName~ActionPromptFiltersPlayCardSourcesByImplementedTimingAndBaseCost|FullyQualifiedName~ActionPromptPlayCardMetadataFiltersTargetsBySourceRequirement"` 通过 6/6；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把基础符文回收从“只能作为普通开环 `RECYCLE_RUNE` 命令”推进到“代表性 `PLAY_CARD` 支付步骤可通过服务端候选 token 使用 `[C]` 资源动作”。整体仍 **NOT READY**，因为这还不是统一 PaymentEngine：多资源动作组合策略、所有非出牌支付窗口、同阵营/多阵营费用、Haste/Echo/Spellshield/Encourage 的全路径费用模型仍未全部收敛。
- P0-003 第三十批补充：`RunStateBasedCleanupLoop` 纳入非法待命状态性清理。栈项目结算后会携带 mutable `ObjectLocations` 进入 cleanup loop，`ApplyIllegalStandbyCleanup` 会把不再由当前战场控制者控制的面朝下/待命对象翻面、移入所属者墓地、同步 `ObjectLocations`，并广播 `BATTLEFIELD_STANDBY_REMOVED`。`MOVE_UNIT` / 精确游走路径也开始把对象位置索引传入同一 cleanup loop，为后续更多进出战场路径复用同一状态性清理入口。
- 已补测试：新增 `P7PostStackCleanupRemovesIllegalStandbyFromBattlefield`，覆盖已有 `REMOVE_ILLEGAL_STANDBY` pending task 的状态在栈结算后自动清理、事件广播、墓地移动、翻面、位置同步和 pending task 清空。`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PostStackCleanupRemovesIllegalStandbyFromBattlefield|FullyQualifiedName~CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges|FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass|FullyQualifiedName~PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask|FullyQualifiedName~SeedScenarioBroadcastsIllegalStandbyCleanupTask"` 通过 5/5；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把 P0-003 中“非法待命清理只作为 blocking task 暴露”的代表性死锁风险推进到“栈结算后的状态性 cleanup loop 可自动清理非法待命”。整体仍 **NOT READY**，因为替代效果、所有进场/离场路径、完整 control/held/conquer task 顺序和官方级 central task queue 仍未全部统一。
- P0-003 第二十九批补充：新增 `P7PostStackCleanupDestroysUnitReducedToZeroPower`，覆盖真实栈项目 `PERFECT_FINALE_BATTLEFIELD_POWER_MINUS_4` 结算后把战场单位从 4 战力修正到 0，随后服务端在同一栈结算后的 `RunStateBasedCleanupLoop` 中以 `ZERO_POWER` 摧毁该单位、移入所属者墓地、同步 `ObjectLocations`，并记录 `POWER_MODIFIED_UNTIL_END_OF_TURN` / `UNIT_DESTROYED`。
- 已补测试：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P7PostStackCleanupDestroysUnitReducedToZeroPower|FullyQualifiedName~PendingTaskQueueExposesZeroPowerFromPowerModifierAsStateBasedTask"` 通过 2/2；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。扩展验证中，完整 `ConformanceFixtureRunnerTests` 当前仍有历史 fixture 断言失败（2127/2672 通过、545 失败），主要集中在旧 fixture 的 `promptActions` 期望仍为 `END_TURN`，与当前服务端更宽的权威 prompt 候选不一致；该问题未作为本批修复范围。
- 复审结论补充：本批把 P0-003 从“战力修正到 0 的脏状态会暴露 blocking cleanup task”推进到“代表性法术栈结算实际产生 0 战力后会立即进入状态性清理并入墓”的可执行证据。整体仍 **NOT READY**，因为替代效果、所有进场/离场路径、控制权变化和完整战场任务仍未全部收敛到同一个官方级持久 cleanup task queue。
- P0-003 第二十八批补充：新增 `PendingTaskQueueExposesZeroPowerFromPowerModifierAsStateBasedTask`，覆盖单位因临时战力修正降到 0 后，服务端 `PendingCleanupTasks` 立即暴露 `DESTROY_ZERO_POWER_UNIT` / `ZERO_POWER`，`PendingTaskQueue` 进入 `STATE_BASED_CLEANUP` blocking，snapshot 公开同一任务，prompt 只返回 `WAIT`，普通 `END_TURN` 被 `PhaseNotAllowed` 拒绝。
- 已补测试：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingTaskQueueExposesZeroPowerFromPowerModifierAsStateBasedTask"` 通过 1/1；`ConformanceFixtureShapeTests` 目标回归 48/48 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把 P0-003 中“战力变化产生的 0 战力对象应立即触发持久 task queue”的代表性证据补上。整体仍 **NOT READY**，因为所有进场/离场/替代效果/控制权变化仍未全部收敛到同一个持久 cleanup task queue。
- P1-004 第二十七批补充：新增 `SpectatorReplayFramesRedactPrivateInformationAcrossGeneratedStates`，用 16 组生成式 `MatchState` 覆盖不同手牌数量、面朝下基地对象、公开战场对象、seed/rngCursor 和 active player 组合；每组都通过 `MatchReplayRedactor.BuildSpectatorFrame` 生成 spectator frame，并断言 authoritative hash 匹配、timing 不含 `seed` / `rngCursor`、手牌 object id 不进入公开 objects、面朝下对象不暴露 `cardNo` / `tags` / `power`。
- 已补测试：`MatchRecoveryTests` 目标回归 21/21 通过；`PostgresMatchRecoveryStoreSmokeTests` 目标 smoke 1/1 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批补的是随机/隐藏信息 redaction 的代表性 property coverage，降低后续改 snapshot/replay 时把私有手牌、面朝下详情或随机状态重新泄露到 spectator frame 的风险。整体仍 **NOT READY**，因为这还不是全命令、全恢复场景、全随机路径的 determinism/property 证明，且 P0 战场/Payment/LayerEngine 等核心规则阻断仍未清零。
- P1-004 第二十六批补充：`MatchRecoveryFrame` 新增 `SpectatorReplayFrame`，`MatchReplayRedactor.BuildSpectatorFrame` 新增可从 `roomId`、tick、event sequence、公开事件列表和 authoritative state 直接生成 spectator frame 的 overload；`PostgresMatchRecoveryStore` 在读取 authoritative state snapshot 时同步生成已裁剪的 recovery spectator replay frame。
- 已补测试：扩展 `PostgresRecoveryStoreLoadsReplayInitialStateAndPassesRegistryReplayAudit`，覆盖 recovery frame 自带 `SpectatorReplayFrame`、event sequence 与最终 recovery sequence 对齐、authoritative state hash 等于最终 state hash，且 spectator snapshot timing 不包含 `seed` / `rngCursor`。`MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests` 目标回归 21/21 通过；`GameHubJoinTests` 目标回归 88/88 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批补齐了 recovery 读取路径的公开 spectator replay frame 输出，避免恢复/回放调用方只能拿最终 authoritative state 再自行拼装公开帧。整体仍 **NOT READY**，因为随机/隐藏信息与 replay determinism 还缺更广泛 property tests，且 P0 战场/任务/Payment/LayerEngine 等核心规则阻断仍未清零。
- P1-004 第二十五批补充：新增真实 `PostgresMatchRecoveryStoreSmokeTests`，在 `ConnectionStrings__Riftbound` 可用时对本地 PostgreSQL 执行迁移 SQL、写入 `match_players`、通过 `PostgresMatchJournal` 记录 `READY -> READY -> PASS` 命令日志和 authoritative state snapshot，再由 `PostgresMatchRecoveryStore` 读取 recovery frame，验证 `ReplayInitialState` 存在、replay final hash audit 通过，并经 registry 恢复最终 snapshot。
- 持久化缺口修复：Postgres smoke 暴露出 `MatchState` 的 `[JsonConstructor]` 中 `seed` / `rngCursor` 为 `long?`、而属性为 `long` 导致 `state_snapshots.payload` 无法反序列化的问题；现已把构造参数改为非空 `long` 默认值，并将 fixture 初始状态的可空 seed 在构造前归零，确保 authoritative state snapshot 可从数据库恢复。
- 已补测试：`PostgresRecoveryStoreLoadsReplayInitialStateAndPassesRegistryReplayAudit` 真实 DB smoke 1/1 通过；`MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests` 目标回归 21/21 通过；`GameHubJoinTests` 目标回归 88/88 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把 P1-004 的“Postgres migration/store 集成 smoke”补上，并修复了 authoritative state snapshot 无法反序列化的真实恢复阻断。整体仍 **NOT READY**，因为恢复时还没有自动输出 spectator replay frame，随机/隐藏信息与 replay determinism 也还缺更广泛 property tests。
- P1-004 第二十四批补充：恢复帧新增 `ReplayInitialState`，并新增 `MatchReplayInitialStateBuilder.FromSeats` 从持久化 `match_players` 座位构造命令日志重放基线。`PostgresMatchRecoveryStore` 在读取到 command log 与最终 authoritative state snapshot 时，会把该基线随恢复帧返回；`InMemoryMatchSessionRegistry` 在真正 `Restore` 前调用 `MatchActionLogReplayer.ValidateRecoveryFrameAsync`，使用当前 `IRuleEngine` 重放 recovered commands，并拒绝 final canonical state hash 不一致的恢复帧。
- 已补测试：新增 `RegistryRunsActionLogReplayAuditBeforeRecoveryRestore` 覆盖 registry 在恢复前执行 action-log replay audit 并可恢复匹配的 final state；新增 `RegistryRejectsRecoveryFrameWhenActionLogReplayHashMismatches` 覆盖 final state 被篡改时返回 `RECOVERY_INCONSISTENT`。`MatchRecoveryTests` 目标回归 20/20 通过；`GameHubJoinTests` 目标回归 88/88 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把 P1-004 从“有可测 verifier”推进到“Postgres recovery frame 会携带座位基线，生产 registry 恢复前会强制 replay final hash audit”的服务端路径；整体仍 **NOT READY**，因为当时还缺真实 Postgres migration/store 集成 smoke、恢复时的 spectator replay frame 输出以及更广泛的随机/隐藏信息 property tests。
- P1-004 第二十三批补充：新增 `MatchActionLogReplayer.VerifyFinalStateAsync`，可以从给定初始权威 `MatchState`、`RecoveredCommand` 列表、目标最终 `MatchState` 和指定 `IRuleEngine` 重放命令日志，并校验每条命令的 accepted、completed tick、事件数量和最终 canonical state hash。该 helper 支持 `READY`、`SUBMIT_DECK`、普通 `GameCommand` 以及 Development seed 命令，作为后续持久化 replay audit 的服务端基础构件。
- 已补测试：新增 `ActionLogReplayerReplaysRecoveredCommandsToFinalStateHash` 覆盖从初始 in-progress 状态重放 `PASS` / `END_TURN` recovered commands 后 final state hash 与实时 journal authoritative state 一致；新增 `ActionLogReplayerReportsFinalStateHashMismatch` 覆盖 expected final state 被篡改时报告 hash mismatch。`MatchRecoveryTests` 目标回归 18/18 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批把 P1-004 从“只有 replay frame hash”推进到“可从明确初始状态重放 action log 并比对 final hash”的可测构件；整体仍 **NOT READY**，因为尚未把生产恢复帧自动绑定到完整房间初始边界并在 recovery store 中强制执行全量 replay audit。
- P1-004 第二十二批补充：恢复一致性校验新增 authoritative state tick guard。`PostgresMatchRecoveryStore` 现在把 `matches.current_tick` 传入 `MatchRecoveryValidator`；当持久化的 `state_snapshots.payload.Tick` 与 match metadata current tick 不一致时，恢复帧会被标记不一致，避免 `MatchSession.RestoreState` 静默覆盖 authoritative state tick 并掩盖持久化/重放边界错误。
- 已补测试：新增 `RecoveryValidatorRejectsAuthoritativeStateTickMismatch` 覆盖 authoritative state tick 与 recovery current tick 不一致时返回明确错误；`MatchRecoveryTests` 目标回归 16/16 通过；`source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error。
- 复审结论补充：本批是 replay/recovery 的一致性护栏，不能等同于完成严格 action-log replay final-state hash。P1-004 仍保留，后续还需要从 command log 重放到最终 state hash，并与实时 authoritative hash 比对。
- P0-005 第二十一批补充：基础符文现在补齐代表性 `RECYCLE_RUNE` 服务端命令。普通开环中，服务端只会把基地中正面、受控、带 `COLOR:*` 特性的符文作为回收来源公开；提交后符文回到所属者符文牌堆底部，来源重置为未横置/正面，玩家获得 1 点与被回收符文特性相同的 typed power，并广播 `RUNE_RECYCLED` / `POWER_GAINED`。
- 前端/集成补充：协议类型、行动面板、卡牌详情抽屉和事件日志已接入 `RECYCLE_RUNE`。前端只在服务端 `ActionPrompt.candidates` 暴露合法来源时启用“回收符文”，多来源时仍要求用户点具体卡牌详情提交，不从卡面文本或客户端资源池自行裁决。
- 已补测试：新增 `CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower` 覆盖横置基础符文可被回收、移入 rune deck、typed power 增加、对象位置同步和事件广播；扩展 `OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 覆盖正式 Hub 开局中横置后回收符文的权威路径；同步更新 legacy/java fixture 期望中的 open-main action 列表。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|FullyQualifiedName~OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub|FullyQualifiedName~GameCommandMapperParsesOfficialDeckAndMulliganPayloads"` 通过 3/3；fixture runner/shape 相关目标回归通过 52/52；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 88/88；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use IAB backend 本次不可用，按不抢前台原则使用后台 Chrome/CDP smoke：Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-recycle-rune-browser-mov6ieuw-1`；P2 前端连接后点击基地符文详情中的“回收符文”，页面显示“回收符文”“获得符能”，额外 SignalR 观察者确认 authoritative snapshot 中 `P2-RUNE-SFD-R03-10` 从基地移到符文牌堆、`runeDeckCount 10 -> 11`、`runePool.powerByTrait.blue = 1`，reload/reconnect 后仍恢复最终 snapshot。
- 复审结论补充：本批关闭的是“基础符文回收获得同特性符能完全缺失”的代表性 open-main 路径；整体结论仍为 **NOT READY**。官方文本要求该资源技能可作为 `[反应]` 在支付费用步骤产生资源，当前还不是完整 PaymentEngine / reaction payment-window 集成。
- P0-002/P0-004 第十八批已落地：战场待命对象不再计入 battlefield occupant。`SnapshotDto.Lanes.battlefields[].occupantObjectIds` 现在只包含正面、非待命单位；`standbyObjectIds` 单独表达待命对象，避免战场争夺/控制结算把面朝下待命牌当成占据单位。
- P0-002/P0-004 第十八批补充：Development `battlefield-contest-stack` seed 新增 `P1-STANDBY-CONTEST-001`，用于覆盖“原控制者失去战场控制后，其非法待命牌必须离开待命区”的代表路径。`DECLARE_BATTLE` 后的战场控制结算会清理不再由当前战场控制者控制的待命对象，把它们翻面并移入所属者墓地，广播 `BATTLEFIELD_STANDBY_REMOVED`，并同步 `ObjectLocations` 到 `GRAVEYARD`。
- 前端/集成补充：事件日志新增 `BATTLEFIELD_STANDBY_REMOVED` 中文标签“待命清理”，并将任务队列 phase/kind 中文化展示。前端仍只展示服务端 snapshot / prompt / events，不自行判断待命是否合法或是否应移入墓地。
- 已补测试：新增 `CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges` 覆盖战场控制从 P1 变更为 P2 后，P1 面朝下待命从战场移入 P1 墓地、翻面、位置同步为 `GRAVEYARD` 并发出 `BATTLEFIELD_STANDBY_REMOVED`；扩展 `P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass` 覆盖 Hub seed 战后 `BATTLEFIELD_STANDBY_REMOVED`、P1 待命移入墓地、中央战场 `standbyObjectIds` 为空且 controllerId 为 P2。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges|FullyQualifiedName~CoreRuleEngineChangesBattlefieldControllerAfterBattle|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask"` 通过 3/3；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass|FullyQualifiedName~P79BattlefieldExtraStandbySeedOffersBandleDestinationAndHides"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 87/87；`source ../../scripts/dev-env.sh && npm run build` 通过。
- Browser Use smoke：IAB backend 可用，本批优先使用 Browser Use。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093` 以无持久化配置启动，房间 `smoke-standby-cleanup-3`。P1 先入座，P2 在前端设置 `serverUrl = http://127.0.0.1:5093`、`playerId = P2` 并连接房间；后台 SignalR 让 P1 `SeedScenario(battlefield-contest-stack)`。P2 通过前端点击“让过优先权”；后台 P1 提交 `PASS_FOCUS`；P2 再通过前端点击“让过焦点”；后台 P1 提交服务端 `DECLARE_BATTLE`。P2 页面随后显示中文“战斗结束”“战场控制结算”“待命清理”，中央战场显示 `控制：P2`、待命 `0 张面朝下`、pending queue `IDLE`。额外 SignalR 校验确认 authoritative snapshot 中 `P1-STANDBY-CONTEST-001` 已从 battlefield 移到 P1 graveyard，`isFaceDown = false`，`ObjectLocations` zone 为 `GRAVEYARD`，battlefield `standbyObjectIds = []`，`controllerId = P2`。刷新页面后 P2 点击“连接/重连”，仍恢复同一最终 snapshot。
- P0-003/P0-004 第十九批补充：`MatchState.PendingCleanupTasks` 现在会把战场上的非法待命对象显式列为 `REMOVE_ILLEGAL_STANDBY` 状态性清理任务；`PendingTaskQueue` 会把它视为 `STATE_BASED_CLEANUP`、以该任务作为 active task，并在 `BattlefieldState.pendingTaskKinds` 中同步公开，供前端只读展示和阻塞提示使用。Development `battlefield-illegal-standby` seed 新增，用于直接 smoke 这个队列可见性与阻塞语义。
- 前端/集成补充：事件日志与队列标签继续显示“待命清理”；前端仍不裁决待命是否合法，只读展示服务端 `pendingTaskQueue` / `battlefieldTasks` / `battlefields[].pendingTaskKinds`。
- 已补测试：新增 `PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask`，覆盖脏状态里非法待命任务会以 `REMOVE_ILLEGAL_STANDBY`、`STATE_BASED_CLEANUP`、blocking prompt 与战场 pendingTaskKinds 一起暴露；新增 `SeedScenarioBroadcastsIllegalStandbyCleanupTask`，覆盖 Hub 级 Development seed 通过 snapshot/prompt 广播同一队列状态。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask|FullyQualifiedName~SeedScenarioBroadcastsIllegalStandbyCleanupTask|FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses|FullyQualifiedName~CoreRuleEngineRemovesIllegalStandbyAfterBattlefieldControlChanges"` 通过；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use smoke：IAB backend 可用，本批优先使用 Browser Use。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093` 以无持久化配置启动，房间 `local`；P2 在前端设置 `serverUrl = http://127.0.0.1:5093`、`playerId = P2` 并连接房间，后台 SignalR 让 P1 加入并 seed `battlefield-illegal-standby`。P2 页面规则队列随后显示中文“状态清理”“待命清理”，prompt 原因显示 `REMOVE_ILLEGAL_STANDBY`，服务端 snapshot 的战场 pendingTaskKinds 同步包含 `REMOVE_ILLEGAL_STANDBY`，规则队列阶段为 `STATE_BASED_CLEANUP`、活动任务为非法待命清理任务，prompt 为 `WAIT`；刷新页面后 P2 点击“连接/重连”，仍恢复同一权威 snapshot。
- 复审结论补充：本批关闭的是“战场控制改变后旧控制方非法待命仍残留在战场/待命区，且待命可能被 occupant 计入控制结算”的 P0 代表路径缺口；整体结论仍为 **NOT READY**。剩余阻断继续集中在完整每战场 held/conquer/control task 生命周期、多参与者战斗与战斗响应窗口、central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-002/P0-004 第二十批补充：`MatchState.BattlefieldResolutions` 新增最近战场结果权威状态，把已经由服务端裁决并广播的 `BATTLEFIELD_HELD`、`BATTLEFIELD_CONQUERED` 与 `BATTLEFIELD_CONTROL_RESOLVED` 持久进 snapshot `timing.battlefieldResolutions`。该状态记录结果 kind、reason、战场 object id、相关玩家、控制权变化和参与对象，解决重连后只能依赖一次性事件日志理解最近战场结果的问题。
- 前端/集成补充：规则队列面板新增最近战场结果只读展示，中文显示“据守 / 征服 / 控制结算”。前端仍只消费服务端 `timing.battlefieldResolutions`，不从卡面、事件文本或当前战场对象本地推断战斗结果。
- 已补测试：扩展 `CoreRuleEngineChangesBattlefieldControllerAfterBattle`、`P79BattlefieldHeldDrawsCardFromBattlefieldObject`、`P79BattlefieldConquerMillsTopTwoFromBattlefieldObject`，覆盖 control/held/conquer 结果进入 `MatchState.BattlefieldResolutions` 与 snapshot；扩展 Hub seed 测试 `P79BattlefieldHeldDrawSeedOffersBattlefieldDestinationAndDraws`、`P79BattlefieldConquerBoonDrawSeedOffersBattlefieldDestinationAndConsumesBoon`，覆盖 GameHub 广播 snapshot 中的战场结果视图。同步修正 `SnapshotsExposeBattlefieldControlOccupantsAndStandbyState`，明确待命不再计入 occupant。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P79BattlefieldHeldDrawsCardFromBattlefieldObject|FullyQualifiedName~P79BattlefieldConquerMillsTopTwoFromBattlefieldObject|FullyQualifiedName~CoreRuleEngineChangesBattlefieldControllerAfterBattle|FullyQualifiedName~P79BattlefieldHeldDrawSeedOffersBattlefieldDestinationAndDraws|FullyQualifiedName~P79BattlefieldConquerBoonDrawSeedOffersBattlefieldDestinationAndConsumesBoon"` 通过 5/5；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests|FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 135/135；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use IAB backend 本次不可用；按“不抢前台”原则使用后台 Chrome/CDP smoke。Vite `http://127.0.0.1:5175`，API `http://127.0.0.1:5093`，房间 `smoke-battlefield-resolutions-4`；P1 由 Web UI 连接，P2 后台 SignalR 入座，后台开发连接 seed `battlefield-held-draw` 并提交 P1 `DECLARE_BATTLE`。页面显示事件“据守战场”“战场控制结算”，规则队列显示 `据守：P2` 与 `控制结算：无控制者`；reload 后 P1 点击“连接/重连”恢复同一权威 `battlefieldResolutions` snapshot。
- 复审结论补充：本批关闭的是“已裁决 held/conquer/control 结果只存在于瞬时事件流，重连后前端无法只读展示最近战场结果”的代表路径缺口；整体结论仍为 **NOT READY**。剩余阻断继续集中在完整每战场 held/conquer/control task 生命周期、多参与者战斗与战斗响应窗口、central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-002/P0-004 第十七批已落地：`DECLARE_BATTLE` 代表路径结算后现在会清除参战单位 `IsAttacking` / `IsDefending` 标记、关闭 `BattleState`，并广播 `BATTLE_CLOSED`。前端 snapshot 不再在战斗完成后把幸存单位误显示为仍在攻击/防守中。
- P0-002/P0-004 第十七批补充：当战斗发生在真实 battlefield object id 上时，服务端会基于战后仍在该战场、正面、非待命的占据单位控制者结算战场控制方；控制权改变或确认都会广播 `BATTLEFIELD_CONTROL_RESOLVED`，并把 `CardObjectState.ControllerId` 写回权威状态。该路径覆盖当前 direct/minimal 代表战斗后的控制方结算，但仍不等同于完整官方 held/conquer/control task 生命周期。
- 前端/集成补充：事件日志新增中文事件标签，`BATTLE_CLOSED` 显示为“战斗结束”，`BATTLEFIELD_CONTROL_RESOLVED` 显示为“战场控制结算”，未知事件仍以“服务端事件”兜底并保留原始 kind title。API Development CORS 策略补齐 loopback Vite 端口段 `5173-5179`，解决本批新窗口 `5175` smoke 时 SignalR negotiate 被旧白名单拒绝的问题；生产环境仍不启用该 fallback。
- 已补测试：扩展 `CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask` 覆盖 battle close、战斗状态关闭、攻击标记清理和战场控制确认；新增 `CoreRuleEngineChangesBattlefieldControllerAfterBattle` 覆盖战后控制方从 P2 改为 P1；扩展 `P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass` 覆盖 Hub seed 战后广播 `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED`、battle inactive、中央战场 controllerId 写回；新增 `ApiDevUiCorsPolicyTests` 覆盖 Development 允许 `127.0.0.1:5175`、Production 不放行 fallback、非 loopback 来源拒绝。同步更新 P4 declare battle fixture 期望，反映战斗关闭事件和战后攻防状态。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineChangesBattlefieldControllerAfterBattle"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P4CombatKeywordProfilesKeepExistingKeywordUnitFixturesGreen"` 通过 30/30；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 46/46；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 87/87；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ApiDevUiCorsPolicyTests"` 通过 3/3；`source ../../scripts/dev-env.sh && npm run build` 通过。
- Computer Use smoke：按用户要求继续使用新的 Chrome 窗口，API `http://127.0.0.1:5092` 与 Vite `http://127.0.0.1:5175` 打开房间 `smoke-battlefield-control-1`。P2 浏览器视角连接后，Node/SignalR 加入 P1 并 seed `battlefield-contest-stack`，P1 过优先权触发 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`，P2 在 UI 点击服务端 `PASS_FOCUS`，P1 再 `PASS_FOCUS` 后进入 `BATTLE_TASKS`。P2 点击己方《大力仙灵》打开卡牌详情，按服务端给出的战场和唯一防守者确认 `DECLARE_BATTLE`；事件日志显示中文“声明战斗”“造成伤害”“单位摧毁”“战斗结束”“战场控制结算”，最终 snapshot 显示中央战场 `控制：P1`、pending queue `IDLE`、prompt 回到普通开环。刷新页面后重新点击“连接/重连”，P2 成功恢复同一权威 snapshot。
- 复审结论补充：本批关闭的是“战斗代表路径结算后仍残留战斗状态/攻防标记，且真实战场对象控制方不随战后占据单位结算”的 P0 代表路径缺口；整体结论仍为 **NOT READY**。剩余阻断继续集中在完整每战场 held/conquer/control task、待命移除、多参与者战斗与战斗响应窗口、central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-003/P0-004 第十六批已落地：`START_BATTLE` active task 现在能由服务端权威 prompt 推进到代表性 `DECLARE_BATTLE`。当 pending queue active task 为 `START_BATTLE` 时，服务端只给当前行动玩家暴露 `DECLARE_BATTLE`，并把来源、防守者和战场候选收紧到该 active battlefield task；非行动玩家仍只能 `WAIT`。
- P0-003/P0-004 第十六批补充：blocking queue 期间只允许匹配 active `START_BATTLE` 的 `DECLARE_BATTLE` 穿过命令 guard；提交侧会校验 `battlefieldId`、攻击者和防守者都位于当前 active task 的战场，否则以 `PHASE_NOT_ALLOWED` 拒绝。该能力仍复用现有 direct/minimal 战斗结算路径，不代表完整官方 battle task lifecycle 已完成。
- 已补测试：`PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses` 改为断言 `START_BATTLE` 阶段 prompt 暴露 `DECLARE_BATTLE` 及当前战场限定来源/目标/目的地/战斗分配费用；新增 `CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask` 覆盖 blocking `START_BATTLE` 期间允许匹配的 `DECLARE_BATTLE` 结算并清空任务队列；扩展 `P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass` 覆盖 GameHub seed 从 `START_BATTLE` prompt 提交 `DECLARE_BATTLE` 后广播 `BATTLE_DECLARED` / `UNIT_DESTROYED` 并回到 `IDLE`。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 46/46；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 87/87；`source ../../scripts/dev-env.sh && npm run build` 通过。Computer Use smoke：在新的 Chrome 窗口用房间 `smoke-battlefield-contest-3` 覆盖 `battlefield-contest-stack`；P2 浏览器视角从 `BATTLE_TASKS` 看到服务端 `DECLARE_BATTLE`，点击卡牌详情中服务端给出的战场 `P1-BATTLEFIELD-CONTEST-001` 与防守者 `P2-UNIT-CONTEST-001` 后确认，事件日志出现 `BATTLE_DECLARED`、两条 `DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终 snapshot 显示 pending queue `IDLE` 且 prompt 回到 `END_TURN`。
- 复审结论补充：本批关闭的是“`START_BATTLE` active task 只能阻塞、无法由服务端 prompt 推进”的 P0 代表路径缺口；整体结论仍为 **NOT READY**。剩余阻断继续集中在完整 control/held/conquer task 生命周期、battle task 多参与者与响应窗口、central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-003/P0-004 第十五批已落地：争夺战场法术对决在双方都 `PASS_FOCUS` 后不再退回泛化 `BATTLEFIELD_CONTESTED` 阻塞任务。`ResolvePassFocus` 会把当前 active `START_SPELL_DUEL` 所属战场写入服务端权威完成标记，`BattlefieldTasks` 中该战场 `START_SPELL_DUEL` 变为 `COMPLETED`，`PendingTaskQueue` 的 active task 切到 `START_BATTLE`，phase 暴露为 `BATTLE_TASKS`。
- P0-003/P0-004 第十五批补充：状态变化后的争夺战场自动推进入口会跳过已经完成过本轮法术对决的战场，避免后续状态检查把同一个 contested battlefield 重新启动法术对决。
- 已补测试：新增 `PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses` 覆盖关闭法术对决后的 snapshot / `BattlefieldTasks` / `PendingTaskQueue` / blocking prompt；新增 `CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus` 覆盖双方让过焦点后事件、完成标记和 `BATTLE_TASKS` active task；扩展 `P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass`，覆盖 GameHub seed 从优先权让过、进入法术对决、双方 `PASS_FOCUS` 到最终 `BATTLE_TASKS` 的广播 snapshot。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 46/46；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 87/87；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口做 Computer Use smoke：房间 `smoke-battlefield-contest-2` 覆盖 Development-only `battlefield-contest-stack` seed；P2 浏览器视角显示 `SPELL_DUEL_OPEN` 和服务端 `PASS_FOCUS`，浏览器提交 `PASS_FOCUS` 后切到等待，Node/SignalR 让 P1 提交 `PASS_FOCUS` 后，页面事件日志出现 `SPELL_DUEL_CLOSED`，最终 snapshot 为 `NEUTRAL_OPEN`、规则队列 `BATTLE_TASKS`、active task `task:start-battle:P1-BATTLEFIELD-CONTEST-001`。
- 复审结论补充：本批关闭的是“争夺战场法术对决结束后又回到 contested 阻塞”的 P0 状态机缺口；整体结论仍为 **NOT READY**。下一阻断点变为 `START_BATTLE` / control task 的实际服务端推进与完整战斗/控制权生命周期，另有 central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据仍未完成。
- P0-003/P0-004 第十四批已落地：服务端新增状态变化后的争夺战场任务推进入口。`MOVE_UNIT`、精确游走和栈项目结算后，如果权威状态留下争夺战场、且不存在致命/0 战力状态性清理优先项，`CoreRuleEngine` 会自动进入 `SPELL_DUEL_OPEN`，把焦点交给造成争夺的玩家，并广播 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`；前端不需要也不能提供自定义“启动法术对决”裁决按钮。
- P0-003/P0-004 第十四批补充：新增 Development-only `battlefield-contest-stack` seed，用于本地构造“优先权栈项目结算后留下争夺战场”的 smoke 场景；该 seed 只用于验证任务队列推进，不代表生产匹配能力。
- 已补测试：新增 `CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield` 覆盖栈结算后争夺战场自动进入法术对决、`BattlefieldTasks` 从 `PENDING` 变为 `ACTIVE/WAITING_FOR_SPELL_DUEL`、prompt 收敛到焦点玩家 `PASS_FOCUS`；新增 `P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass` 覆盖 GameHub seed 后 P2 让过优先权会广播 `PRIORITY_PASSED`、`STACK_ITEM_RESOLVED`、`BATTLEFIELD_CONTESTED`、`SPELL_DUEL_STARTED` 并把 snapshot/prompt 推到 `SPELL_DUEL_OPEN`。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 45/45；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 87/87；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineStartsBattlefieldSpellDuelAfterStackResolutionLeavesContestedBattlefield"` 通过 1/1；`source ../../scripts/dev-env.sh && npm run build` 通过。另跑 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"` 时，当前工作副本出现大量旧 fixture 的 promptActions 期望差异（例如旧期望 `END_TURN`，当前 prompt 会暴露真实可用源驱动动作），本批未把该历史大面纳入提交验收，后续需要单独清理旧 fixture 期望或调整比较口径。Browser Use 当前无可用 IAB backend，按用户授权继续使用新的 Chrome 窗口做 Computer Use smoke：房间 `smoke-battlefield-contest-1` 覆盖 Development-only `battlefield-contest-stack` seed；页面显示 `BATTLEFIELD_TASKS` 与争夺战场，Node/SignalR 让 P1 过优先权后，浏览器事件日志出现 `PRIORITY_PASSED`、`STACK_ITEM_RESOLVED`、`BATTLEFIELD_CONTESTED`、`SPELL_DUEL_STARTED`，snapshot 切到 `SPELL_DUEL_OPEN`，P2 prompt 只显示服务端给出的 `PASS_FOCUS`。继续两边 `PASS_FOCUS` 后会回到 `BATTLEFIELD_CONTESTED` blocking queue，说明“法术对决结束后启动 battle/control task”仍是下一批 P0 缺口。
- 复审结论补充：本批关闭的是“争夺战场任务视图存在但无法由服务端权威推进到法术对决入口”的任务状态机缺口；整体结论仍为 **NOT READY**。剩余阻断继续集中在 spell duel 结束后的 battle/control task、完整 battlefield/standby/control task 状态机、central cleanup task queue、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-004/P0-005 第十三批已落地：`SPELL_DUEL_OPEN` 焦点窗口的 `PLAY_CARD` 暴露已收紧到同一套服务端 `sourceRequirements`。服务端现在只有在存在可支付、合法时点、目标槽可组合的真实来源时才给焦点玩家 `PLAY_CARD`，否则只给 `PASS_FOCUS` 或其它真实可用候选；前端不会再看到空的法术对决出牌入口。
- P0-004/P0-005 第十三批补充：Development `spell-duel` seed 补齐《海克斯射线》和目标单位的公开 cardNo、owner/controller 与标签；新增 `spell-duel-focus` seed，直接构造 P1 迅捷带目标法术、P2 合法战场单位、窗口为 `SPELL_DUEL_OPEN` 且焦点在 P1 的前端 smoke 场景。该 seed 只用于 Development，本批不把它记为生产匹配能力。
- 已补测试：新增 `ActionPromptSpellDuelFocusOnlyExposesPlayCardWhenSourceIsComposable`，覆盖空焦点窗口不会暴露 `PLAY_CARD`、存在《海克斯射线》与合法战场目标时才暴露 `PLAY_CARD,PASS_FOCUS`；新增 `P6SpellDuelFocusSeedExposesPlayableSwiftCardPrompt`，覆盖 GameHub seed 后 P1 snapshot/prompt 能拿到公开手牌、目标单位、`sourceRequirements`、目标槽和提交后 `PASS_PRIORITY`。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6SpellDuel"` 通过 2/2；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPromptSpellDuelFocusOnlyExposesPlayCardWhenSourceIsComposable"` 通过 1/1；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 86/86；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 45/45；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6SwiftKeywordAllowsHextechRayInSpellDuelFocusWindow"` 通过 1/1；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `smoke-spell-focus-1` 覆盖 Development-only `spell-duel-focus` seed、P1 点击手牌《海克斯射线》打开详情抽屉、选择服务端目标槽 `P2-UNIT-HEXTECH-RAY-001` 并确认，事件日志出现 `CARD_PLAYED`、`COST_PAID`、`STACK_ITEM_ADDED`，后续 prompt 为 `PASS_PRIORITY`；额外 SignalR smoke 让 P2 过优先权，服务端广播 `STACK_ITEM_RESOLVED`、`DAMAGE_APPLIED`、`UNIT_DESTROYED`，最终回到 `SPELL_DUEL_OPEN` 且 P2 prompt 为 `PASS_FOCUS`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852。
- 复审结论补充：本批关闭的是“法术对决焦点窗口中带目标迅捷法术不能由前端从服务端候选安全组合提交”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、完整 spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-004/P0-005 第十二批已落地：`DECLARE_BATTLE` prompt 从泛化来源/目标/战场升级为每攻击者 `sourceRequirements` 元数据。服务端现在按当前时点、攻击者是否正面/受控/未参战、防守者是否合法、战场候选和必需 `COMBAT_ASSIGNMENT` 费用过滤候选；前端详情抽屉只读取这些服务端候选渲染战斗声明组合器，不从卡面文本、关键词、客户端战场状态或 UI 位置自行裁决。
- P0-004/P0-005 第十二批补充收紧：`DECLARE_BATTLE.sources`、`targets`、`destinations`、`optionalCosts` 都由同一组服务端 source requirements 汇总生成；当前只开放服务端已支持的单攻击者/单防守者 direct/minimal 代表路径。多防守者、战场任务驱动的完整战斗生命周期和战斗中响应窗口仍明确留在 P0-004 后续，不把本批误记为官方完整 battle task。
- 已补测试：新增 `ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts`，覆盖基地/面朝下/已参战/装备对象不会作为战斗来源或防守者暴露，并断言战场候选、必需 `COMBAT_ASSIGNMENT` 和 per-source `targetChoicesByIndex`。Development `battle-declare` seed 补齐攻防单位 cardNo、owner/controller 和单位标签，避免 prompt 来源可见但玩家 snapshot 仍给卡背。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 44/44；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `smoke-battle-3` 覆盖 Development-only `battle-declare` seed、P1 点击己方战场《大力仙灵》打开卡牌详情 `DECLARE_BATTLE` 组合器、选择服务端防守者并确认，事件日志出现 `BATTLE_DECLARED`、两条 `DAMAGE_APPLIED`、`UNIT_DESTROYED`；最终 snapshot 显示防守者进入废牌堆，后续 prompt 为 `END_TURN`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852。
- 复审结论补充：本批关闭的是“前端可声明代表性战斗但不能自行判断攻击者、防守者、战场和战斗分配费用”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-005 第十一批已落地：`LEGEND_ACT` prompt 从泛化模式/费用升级为每来源 `sourceRequirements` 元数据。服务端现在按具体传奇或授予来源公开能力、目标槽、必需费用、时点、横置来源、立即结算和 `composable` 状态；前端详情抽屉只读取这些服务端候选渲染传奇行动组合器，不从卡面文本、关键词、资源或经验自行裁决。
- P0-005 第十一批补充收紧：`LEGEND_ACT` 来源会按当前时点、来源是否横置、资源/经验是否可支付、卡牌前置条件、目标槽候选和代表路径支持状态过滤候选。Poppy、Yasuo 等简单目标结构可组合提交；依赖第一目标再决定第二目标的武装类传奇行动会以 `composable=false` 和服务端原因明确禁用前端提交。
- 已补测试：新增 `ActionPromptLegendActMetadataFiltersSourcesAbilitiesTargetsAndCosts`，覆盖无资源/经验时不暴露传奇行动来源，有资源/经验时暴露 Poppy/Yasuo/Jax 来源、模式、目标槽、必需费用和不可组合双目标依赖原因。前端 smoke 覆盖了 Poppy 传奇行动的零目标经验支付立即结算路径。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 43/43；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `smoke-legend-1` 覆盖 Development-only `legend-act` seed、P1 点击《圣锤之毅》打开卡牌详情 `LEGEND_ACT` 组合器并确认，事件日志出现 `LEGEND_ABILITY_ACTIVATED`、`EXPERIENCE_SPENT`、`LEGEND_EXHAUSTED`、`CARD_DRAWN`；最终 snapshot 显示 P1 `experience = 0`、Poppy 横置、手牌 +1、后续 prompt 为 `END_TURN`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852。
- 复审结论补充：本批关闭的是“前端可执行代表性传奇行动但不能自行判断来源、能力、目标、时点、经验/资源支付和横置”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-005 第十批已落地：`ACTIVATE_ABILITY` prompt 从泛化来源/目标升级为每来源 `sourceRequirements` 元数据。服务端现在只对已实现代表路径的 Vi、Xerath 和蜕变花园授予单位能力公开来源、能力、目标槽、费用、横置来源、立即结算和 `composable` 状态；前端详情抽屉只读取这些服务端候选渲染激活能力组合器，不从卡面文本、关键词或客户端位置自行裁决。
- P0-005 第十批补充收紧：`ACTIVATE_ABILITY` 来源会按资源、来源是否横置、是否需要战场来源、目标槽是否存在合法目标，以及敌方 Spellshield 加税是否可支付过滤候选。Xerath 的目标槽由服务端逐槽公开，敌方 Spellshield 单位在当前法力不能支付加税时不会作为可选目标出现；蜕变花园授予能力只对己方场上、未横置、可被友方蜕变花园覆盖的单位公开。
- 已补测试：新增 `ActionPromptActivateAbilityMetadataFiltersSourcesTargetsAndSpellshieldTax`，覆盖无资源时不暴露可激活来源、有资源时暴露 Vi/Xerath 来源和目标槽、以及法力不足时过滤敌方 Spellshield 加税目标。前端 smoke 也覆盖了蜕变花园授予能力的零目标立即结算路径。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 42/42；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `smoke-activate-1` 覆盖 Development-only `battlefield-unit-experience-ability` seed、P1 点击场上单位打开卡牌详情 `ACTIVATE_ABILITY` 组合器并确认，事件日志出现 `ABILITY_ACTIVATED`、`UNIT_EXHAUSTED`、`BATTLEFIELD_TRIGGER_RESOLVED`、`EXPERIENCE_GAINED`；额外 SignalR 校验确认最终 snapshot 中 `experience = 1`、来源 `exhausted = true`、后续 prompt 为 `MOVE_UNIT,END_TURN`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852。
- 复审结论补充：本批关闭的是“前端可激活代表性能力但不能自行判断来源、目标、横置和 Spellshield 加税”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-005 第九批已落地：`ASSEMBLE_EQUIPMENT` prompt 从泛化来源/目标升级为每来源 `sourceRequirements` 元数据。服务端现在只对已实现代表路径的未贴附《长剑》公开来源、单位目标候选、必需 `ASSEMBLE_RED` 费用、红色符能费用和 `composable` 状态；前端详情抽屉只读取这些服务端候选渲染装配组合器，不从卡面文本、关键词或客户端位置自行裁决。
- P0-005 第九批补充收紧：长剑装配费用从“任意 1 符能”收紧为 `powerByTrait.red >= 1`。泛化符能不再使 prompt 暴露 `ASSEMBLE_EQUIPMENT` 来源，提交侧也会以 `INSUFFICIENT_COST` 拒绝；成功 fixture 和手写测试已迁到 `powerByTrait.red`，避免 UI 把“装配红色符能”展示成可用但服务端支付语义不一致。
- 已补测试：`ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower` 已扩展断言 `ASSEMBLE_EQUIPMENT.sourceRequirements`、目标候选、必需费用和红色符能过滤；新增 `P4AssembleEquipmentCommandRejectsGenericPowerForRedAssembleCost` 覆盖泛化符能拒绝；装配成功 fixture 迁到红色符能池，并更新装配后 prompt 仍可暴露 `MOVE_UNIT` 的真实候选。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 41/41；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AssembleEquipment"` 通过 29/29；`source ../../scripts/dev-env.sh && npm run build` 通过；`git diff --check` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `smoke-assemble-2` 覆盖 Development-only `equipment` seed、P1 从详情抽屉打出《长剑》、P1/P2 让过优先权结算到基地，再由卡牌详情 `ASSEMBLE_EQUIPMENT` 组合器选择服务端目标并装配，事件日志出现 `EQUIPMENT_PLAYED_TO_BASE`、`COST_PAID`、`EQUIPMENT_ATTACHED`，最终 snapshot 显示长剑 `attachedToObjectId = P1-UNIT-ASSEMBLE-TARGET`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852。
- 复审结论补充：本批关闭的是“前端可装配装备但不能自行判断合法目标和红色费用”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。
- P0-005 第八批已落地：`MOVE_UNIT` prompt 从泛化来源/目的地升级为每来源 `sourceRequirements` 元数据。服务端现在按具体单位公开来源、起点区域、移动模式、目的地候选、必需/可选额外费用和 `composable` 状态；基地单位公开“基地 -> 战场”，战场单位在未被静态效果禁止时公开“战场 -> 基地”，有游走权限且能从权威位置索引精确定位时才公开游走目的地与必需 `ROAM` 费用。前端详情抽屉只读取这些服务端候选渲染移动组合器，不从卡面文本、关键词或客户端位置自行裁决。
- 已补测试：`ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits` 已扩展断言 `MOVE_UNIT.sourceRequirements`，覆盖基地正面受控非战斗单位只暴露 `origin=BASE`、`mode=BASE_TO_BATTLEFIELD`、`destinationChoices=BATTLEFIELD` 且不暴露可选费用。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 41/41；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source ../../scripts/dev-env.sh && npm run build` 通过。Browser Use 当前无可用 IAB backend，按用户授权降级使用 Computer Use smoke：房间 `room-9z3bds` 覆盖 P1/P2 入座、提交 deck、ready、双方 mulligan、P1 横置符文、打出《军团后卫》、P1/P2 让过优先权结算到基地，再由卡牌详情 `MOVE_UNIT` 组合器移动到战场，事件日志出现 `UNIT_MOVED_TO_BATTLEFIELD`。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852；本批最终提交前仍需执行 `git diff --check`。
- 复审结论补充：本批关闭的是“前端可移动单位但不能自行判断合法目的地”的产品级服务端候选缺口；整体结论仍为 **NOT READY**。剩余阻断仍集中在完整 battlefield/standby/control task 状态机、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine、LayerEngine 和全官方卡牌证据。

## 2026-05-06 开发进度更新

- 复审基线 `45bb446`：本轮自查整改后重新按 `docs/符文战场_服务端核心规则自查文档.md` 复核。结论仍为 **NOT READY**，但 P0 风险已进一步收窄：official opening/mulligan 边界已有测试矩阵，battlefield task 已有权威视图，replay frame 已有 authoritative state hash，battlefield trigger 支付已覆盖 typed power。剩余 NOT READY 根因集中在完整 battlefield/standby/control task 状态机、统一 cleanup task queue、由 task queue 驱动的 spell duel/battle lifecycle、全路径官方 PaymentEngine、完整 LayerEngine 与逐关键词/逐卡牌 full-official-rule-pass 证据。
- P0-001 第一批已落地：新增 `SUBMIT_DECK`、`MULLIGAN` 协议命令，新增官方卡组校验器，新增正式 deck submit 入口，双方提交合法 deck 并 ready 后会进入正式 1v1 开局、随机回合顺序、双方传奇/英雄区域、每人 3 选 1 战场、主牌堆/符文牌堆洗牌、起手 4 张、按回合顺序调度，并在双方调度后进入第一个回合。
- P0-002 第一批已落地：新增 `ObjectLocationState` 权威位置索引，snapshot 对公开对象输出 `location`，正式开局/调度/召符文/打出到结算链/结算后入场或入废牌堆/移动都会同步对象位置；精确战场游走会校验来源位置是否匹配服务端权威位置，并把目的战场写回状态。
- P0-003 第一批已落地：`MOVE_UNIT` 和精确游走完成后会执行一次致命伤害清理，并将清理后的区域重新同步回 `ObjectLocations`；若单位在行动前已经处于待清理致命状态，blocking pending task queue 会先拒绝移动，避免已摧毁单位继续行动。
- P0-004 第一批已落地：`StackItemState` 记录入栈时机上下文；迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，会回到 `SPELL_DUEL_OPEN` 并把焦点交给回合顺序下一名玩家，而不是错误关闭到普通开环；法术对决 prompt 也会在有可用来源时暴露 `PLAY_CARD`。
- P0-004 第二批已落地：`MatchState` 归一化/恢复栈项目时保留 `TimingContext`，反应/反制牌入栈会继承现有栈顶的法术对决上下文；最后一个法术对决栈项目被反制后，结算仍会回到 `SPELL_DUEL_OPEN` 并把焦点交还给下一名玩家，避免由状态恢复或反应链造成的错误窗口关闭。
- P1-004 第一批已落地：普通玩家 `SnapshotDto.Timing` 不再暴露 `seed` 和 `rngCursor`；服务端权威 `MatchState` 仍保留随机状态用于内部结算、恢复和日志，避免客户端通过 snapshot 推断牌库/随机顺序。
- P0-001 第二批已落地：新增 `MatchSessionOptions.AllowLegacyReadyWithoutDeck`；API 在非 Development 环境创建房间时关闭 legacy no-deck ready，正式/生产房间必须先 `SUBMIT_DECK` 才能 `READY`。Development 与既有测试默认保留 legacy ready，用于开发 seed 和旧 fixtures。
- P0-002 第二批已落地：`SnapshotDto.Lanes` 新增 `battlefields` 状态视图，从服务端权威 `ObjectLocations` 和 `CardObjects` 推导战场牌、控制者、占据单位、待命占位、面朝下待命数量与争夺状态，给 UI 和后续 cleanup/battle task 提供稳定服务端投影。
- P0-001 第三批已落地：新增 GameHub 级正式开局 smoke，覆盖 `SUBMIT_DECK -> READY -> OFFICIAL_OPENING_STARTED -> MULLIGAN -> MULLIGAN_PHASE_COMPLETED -> RUNES_CALLED -> MAIN`，确保 WebSocket/房间层不会绕过或吞掉服务端官方 deck/opening/mulligan 流程。
- P0-003 第二批已落地：结算链项目结算后新增统一状态性致命清理兜底。即使某个栈项目本身是无行为/未映射或单卡 resolver 漏接局部 cleanup，只要结算后场上存在伤害大于等于战力的单位，服务端会将其清理入对应非场地区域、同步 `ObjectLocations` 并记录 `UNIT_DESTROYED`。
- P0-005 第一批已落地：`RunePool` 新增 `PowerByTrait` typed 符能池和 `TotalPower` 视图；`PLAY_CARD` 支付计划可区分任意符能与指定特性符能，`SPEND_POWER:red:2` / `SPEND_POWER:红色:2` 等 token 会校验并只扣对应特性，旧的泛化 `power` fixture 仍兼容。snapshot 的 `runePool` 继续提供总 `power`，同时新增 `untypedPower` 与 `powerByTrait` 给 UI 展示支付来源。
- P1-003 第一批已落地：`BehaviorSpec` 新增 `ConformanceTier` / `ConformanceReason`，将当前 `implemented` 明确降义为 `representative-rule-pass`，并在 API summary、图鉴详情和基线测试中断言 `full-official-rule-pass = 0`。产品 UI 不再把 `implemented` 文案展示为“官方完整一致性通过”。
- P0-002 第三批已落地：新增 `BattlefieldState` 权威派生状态视图与 `MatchState.BattlefieldStates`，从服务端 `PlayerZones`、`ObjectLocations`、`CardObjects` 统一表达战场牌、控制者、占据单位、占据方控制者、待命对象、面朝下待命数量和争夺状态；snapshot 的 lanes.battlefields 改为复用该服务端状态视图。
- P0-003 第三批已落地：新增 `CleanupTaskState` / `MatchState.PendingCleanupTasks`，能显式列出致命伤害单位清理与战场争夺检查任务；移动和结算链结算后的致命伤害清理由单次 helper 升级为 `RunStateBasedCleanupLoop`，重复执行直到当前状态性致命伤害任务稳定。
- P0-004 第三批已落地：新增 `TurnWindowState`、`SpellDuelState`、`BattleState` 与对应 `MatchState` 派生视图；普通开环/闭环、法术对决开环/闭环现在有统一窗口状态，snapshot timing 会暴露 `turnWindow`、`spellDuel`、`battle`，用于 UI 和后续 task machine，而不是让前端自行推断。
- P0-005 第二批已落地：装备装配、Vi 双倍战力技能、Xerath 伤害技能等非 `PLAY_CARD` 支付路径的资源校验已改为 typed-power aware；泛化符能费用现在既可由普通 `Power` 支付，也可由 `PowerByTrait` 中任意可用特性符能支付并正确扣除，避免只有出牌路径支持彩色符能。
- P1-001 第一批已落地：新增 `ContinuousEffectState` 服务端派生视图，按 `RULE_TEXT` / `POWER_MODIFIER` 层公开全局与对象级直到回合结束效果；snapshot 中每个公开对象新增 `basePower` / `effectivePower`，timing 新增 `continuousEffects`，UI 不再需要从临时战力聚合字段自行反推基础战力。
- P1-002 第一批已落地：新增 `KeywordCoverageReporter`，将权限/战斗/资源/装备/生命周期/交互六类关键词的 implemented、representative、delegated、recognized-deferred 状态汇总为服务端报告；API `/catalog/summary`、`/catalog/p3-status` 和新增 `/catalog/keyword-coverage` 均会暴露 keyword coverage，产品侧可直接禁用或提示 deferred 关键词能力。
- P1-004 第二批已落地：新增 `ResolutionResult.BuildSpectatorSnapshot` 与 `MatchReplayRedactor.BuildSpectatorFrame`，从权威 journal entry 生成观战/回放 frame 时统一使用 spectator redaction；手牌、面朝下对象详情和随机 seed/rngCursor 不会进入观战 replay snapshot。
- P0-003 第四批已落地：争夺战场的 `PendingCleanupTasks` 现在除 `BATTLEFIELD_CONTESTED` 外，还显式列出 `START_SPELL_DUEL` 与 `START_BATTLE` 后续任务；战斗伤害、常规栈项目局部清理、Xerath 技能伤害和回合开始战场群体伤害都改为走 `RunStateBasedCleanupLoop`，并在首轮保留伤害触发摧毁目标集合，后续重复清理直到稳定。
- P0-004 第四批已落地：新增 `BattlefieldTaskState` 与 `MatchState.BattlefieldTasks`，争夺战场会生成带 `PENDING`/`ACTIVE`/`WAITING_FOR_SPELL_DUEL` 状态、参与控制者、参与单位、焦点玩家和 spell-duel stack 关联的权威任务视图；snapshot timing 新增 `battlefieldTasks`，UI 和后续 task queue 可直接消费服务端任务，而不是只从 `pendingTaskKinds` 猜测。
- P1-004 第三批已落地：新增 `MatchStateHasher`，对权威 `MatchState` 生成 canonical SHA-256 hash；`MatchReplayFrame` 现在携带 `AuthoritativeStateHash`，观战/回放帧可在不泄露手牌、面朝下对象和随机状态明文的前提下，与实时权威状态进行最终状态一致性校验。
- P0-005 第三批已落地：战场据守触发 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 现在也走 `CanPayRuneCosts` / `PayRuneCosts`，泛化 4 符能费用可以由 `PowerByTrait` 支付并正确扣除；这把 battlefield trigger payment 纳入 typed-power-aware 路径，不再只支持普通 `Power`。
- P0-003 第五批已落地：状态性 cleanup loop 新增 0 战力现代权威单位清理；`PendingCleanupTasks` 会显式列出 `DESTROY_ZERO_POWER_UNIT`，栈结算后的统一清理会把有 owner/controller 的 0 战力场上单位移入废牌堆并记录 `ZERO_POWER`，同时保留旧 fixture 中无所有权信息的 0 power 占位对象兼容行为。
- P0-003 第六批已落地：新增 `PendingTaskQueueState` 与 `MatchState.PendingTaskQueue`，把 `PendingCleanupTasks` 按官方清理优先级排序并公开 `hasTasks`、`isBlocking`、`phase`、`activeTaskId` 和 task 列表；snapshot timing 新增 `pendingTaskQueue`，后续 UI/状态机不再需要从多个数组自行拼装清理队列。
- P0-003 第七批已落地：`ResolutionResult.BuildPrompts` 与 `CoreRuleEngine.ResolveAsync` 接入 blocking pending task queue；当当前权威状态存在待处理清理/战场任务且没有栈优先权或法术对决焦点窗口时，服务端 prompt 只返回 `WAIT`，并拒绝普通玩家命令，避免前端或客户端在 cleanup/task queue 阻塞期间继续出牌、移动或结束回合。
- P0-005 第四批已落地：`ActionPromptBuilder` 将 `PLAY_CARD`、`MOVE_UNIT`、`ASSEMBLE_EQUIPMENT` 纳入“必须有服务端候选源才可启用”的 prompt 规则；`PLAY_CARD` 来源现在会经过 BehaviorRegistry、时点权限和基础费用的服务端保守过滤，没有足够基础费用的手牌不会作为可执行来源暴露给 UI。
- P0-005 第五批已落地：移动/装配 prompt 继续收紧。`MOVE_UNIT` 只暴露正面、受控、非战斗中的单位；`ASSEMBLE_EQUIPMENT` 只暴露当前代表实现支持的未贴附长剑/武装装备，并要求有可支付符能和合法单位目标，减少 UI 上“按钮可点但必然被服务端拒绝”的误导。
- P0-005 第六批已落地：前端重建 smoke 发现普通主阶段 prompt 暴露 `TAP_RUNE`，但协议和核心规则引擎没有对应命令解析。已按自查文档 8.2 基础符文规则补齐 `TapRuneCommand`、JSON mapper、服务端候选来源过滤与 `CoreRuleEngine.ResolveTapRune`；只有基地中正面、受控、未横置的符文会作为 `TAP_RUNE.sources`，提交后符文横置、玩家获得 1 法力，并广播 `RUNE_TAPPED` / `MANA_GAINED`。回收符文获得同特性符能仍未补齐，后续应作为独立资源技能/支付模型批次进入统一 PaymentEngine。
- P0-005 第七批已落地：`PLAY_CARD` prompt 从泛化 targets/modes/options 升级为每来源 `sourceRequirements` 元数据。服务端现在按具体手牌公开最小/最大目标数、目标范围中文标签、逐目标槽候选、目的地候选、可支付可选费用、模式与 `composable` 状态；需要目标的牌必须有服务端过滤后的必需目标槽候选才作为可执行来源暴露。复杂额外费用牺牲/返回目标暂以 `composable=false` 禁用前端组合提交，后续应接入统一 PaymentEngine 的费用目标选择模型。Dev `basic-play` seed 也补齐了手牌对象 cardNo/power/tags，避免 prompt 只能暴露来源却无法生成来源约束。
- P0-004 第五批已落地：`CoreRuleEngine` 的拒绝/结算后 core prompt 在法术对决焦点窗口复用 `ActionPromptBuilder.SpellDuelFocusActions`；如果焦点玩家有可支付且时点合法的迅捷牌，服务端 prompt 会同时暴露 `PLAY_CARD` 与 `PASS_FOCUS`，避免被局部 `BuildCorePrompts` 降级成只能让过焦点。
- P0-002/P0-003 第八批已落地：`DECLARE_BATTLE` 战斗结算后会用服务端 `PlayerZones` 重新同步 `ObjectLocations`，避免战斗摧毁、战场触发移动或后续 cleanup 改变区域后，权威对象位置索引仍停留在旧战场并污染 snapshot、战场任务和后续合法性检查。
- P0-003/P0-004 第九批已落地：`PendingTaskQueue` 的 active task 会优先反映正在进行的 `START_SPELL_DUEL` / `START_BATTLE` 任务；争夺战场已进入法术对决时，队列 phase 暴露为 `SPELL_DUEL_TASKS`，active task 指向 `START_SPELL_DUEL`，`START_BATTLE` 在 `BattlefieldTasks` 中保持 `WAITING_FOR_SPELL_DUEL`。
- 复审结论补充：截至 `4be8b41`，本轮已把 P0-001 官方开局/调度、P0-002 对象位置索引、P0-003 pending task 阻塞、P0-004 法术对决任务视图、P0-005 prompt 候选与代表性 typed 支付继续收紧；重新对照自查文档后，结论仍为 **NOT READY**，剩余阻断项不再是局部测试缺口，而是需要完整 board task model、central cleanup task queue、spell duel/battle lifecycle、PaymentEngine 和 LayerEngine 的架构级后续批次。
- P0-001 第四批已落地：`OfficialDeckValidatorRejectsOfficialNegativeMatrix` 补齐基础官方构筑负例矩阵，覆盖英雄不在主牌、主牌非法类别、未知卡号、符文牌堆非符文、战场牌堆非战场、主牌/符文越出传奇特性等拒绝路径。
- P0-001 第五批已落地：`OfficialMulliganRejectsInvalidSelectionsAndWrongPlayer` 与 `OfficialMulliganWithShortMainDeckDrawsAvailableCardsAndReturnsSetAside` 补齐起手调度边界，覆盖非当前调度玩家拒绝、最多 2 张、重复选择、非手牌选择、主牌堆不足时只抽可用牌并把搁置牌回收到主牌堆且不触发燃尽。
- P0-001 第六批已落地：前端正式房间 smoke 发现房间阶段 prompt 仍直接暴露 `READY`，但官方 deck path 要求先 `SUBMIT_DECK`。服务端 `ResolutionResult.BuildPrompts` 已改为未提交合法卡组时只返回 `SUBMIT_DECK`，提交后才返回 `READY`，已准备后返回 `WAIT`；`GameHubJoinTests` 已覆盖 join prompt 与 submit deck 后 prompt 转换。新前端主流程只渲染这些服务端候选，不再常驻 ready/deck 游戏命令按钮。
- 已补测试：`OfficialOpeningTests` 覆盖协议解析、卡组构筑拒绝条件、官方构筑负例矩阵、正式开局、起手调度、调度非法选择/抽牌不足边界、精确战场位置写回/来源不匹配拒绝、待清理致命单位移动被 blocking queue 拒绝。
- 已补测试：`P7SpellDuelReactionInheritsStackTimingContextWhenItCountersLastSpell` 覆盖法术对决反应/反制链继承 timing context；`CoreRuleEngineRejectedSpellDuelFocusPromptIncludesPlayableSwiftCard` 覆盖 core prompt 在法术对决焦点窗口暴露可打出的迅捷牌；`SnapshotsDoNotExposeRandomSeedOrCursor` 覆盖普通玩家 snapshot 隐藏随机种子和游标；`SpectatorReplayFrameRedactsPrivateZonesFaceDownObjectsAndRngState` 覆盖观战回放 redaction 与 `AuthoritativeStateHash`；`MatchStateHashIsStableAcrossDictionaryInsertionOrder` 覆盖权威状态 hash 的字典顺序稳定性；`OfficialOnlyRoomsRejectReadyBeforeDeckSubmission` 覆盖正式房间拒绝绕过 deck submit；`SnapshotsExposeBattlefieldControlOccupantsAndStandbyState` 覆盖战场状态 snapshot 投影；`MatchStateExposesAuthoritativeBattlefieldAndCleanupTaskViews` 覆盖服务端 `BattlefieldStates`、`START_SPELL_DUEL`/`START_BATTLE`、`PendingCleanupTasks`、`BattlefieldTasks`、`PendingTaskQueue`、`timing.battlefieldTasks`、`timing.pendingTaskQueue`、blocking prompt 与 command guard；`PendingTaskQueueUsesSpellDuelTaskAsActiveWhileContestDuelIsOpen` 覆盖争夺战场进入法术对决后 active task/phase 不再停留在泛化 contested 任务；`ActionPromptFiltersPlayCardSourcesByImplementedTimingAndBaseCost`、`ActionPromptPlayCardMetadataFiltersTargetsBySourceRequirement`、`ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits`、`ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower` 覆盖服务端 prompt 候选过滤和 `PLAY_CARD.sourceRequirements`；`MatchStateExposesTurnWindowSpellDuelAndBattleViews` 覆盖服务端四类窗口、法术对决和战斗状态视图；`MatchStateExposesContinuousEffectPowerLayerViews` 覆盖基础/有效战力与持续效果层 snapshot；`KeywordCoverageReportExposesDeferredKeywordFamilies` 覆盖关键词 deferred 报告；`OfficialDeckSubmitReadyAndMulliganFlowWorksThroughHub` 覆盖 Hub 级正式开局、`TAP_RUNE` 候选来源、基础符文横置得 1 法力、符文横置状态和来源移除；`P7PostStackCleanupDestroysPreExistingLethalFieldUnit` / `P7PostStackCleanupDestroysZeroPowerFieldUnit` / `P7BattleCleanupReconcilesAuthoritativeObjectLocations` 覆盖栈结算和战斗结算后的统一状态/位置清理兜底；`P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait` / `P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing` 覆盖彩色符能成功支付与失败回滚；`P7TypedPowerPaymentActivatesViSkillWithTraitPool` / `P7TypedPowerPaymentActivatesXerathSkillWithTraitPool` / `P7TypedPowerPaymentAssemblesLongSwordWithTraitPool` / `P79BattlefieldHeldPaysTypedPowerToGainScore` 覆盖非出牌与战场触发路径消耗 typed 符能；`P79ProductCatalogExposesRepresentativesWithoutClaimingFullOfficialRulePass` 覆盖图鉴状态口径拆分；最近完整回归记录仍为 `dotnet test 2852/2852`，当前焦点回归为 `ConformanceFixtureShapeTests 41/41`、`GameHubJoinTests 85/85`。
- 复审验证记录：本批 `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore` 通过，0 warning/0 error；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureShapeTests"` 通过 41/41；`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"` 通过 85/85；`source ../../scripts/dev-env.sh && npm run build` 通过；Browser Use 双人正式房间 smoke 覆盖 P1/P2 创建/加入、提交 deck、ready、双方 mulligan、P2 横置两张符文、卡牌详情选择器打出《军团后卫》、P2/P1 让过优先权后结算到基地、P2 `END_TURN` 到 P1 主阶段。Browser dev logs 中仍有历史本地 API 重启导致的旧 SignalR 断线/协商失败记录；本批重启后功能路径正常完成。最近完整回归记录仍为 `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 2852/2852；`git diff --check` 待本批最终提交前执行；工作区预期仅剩未跟踪 `riftbound-dotnet.sln`。
- 兼容性边界：为避免打碎既有开发 seed 和旧测试，当前无 decklist 的普通 `READY` 仍保留 legacy 入口；产品 UI 和后续正式规则路径必须强制先走 `SUBMIT_DECK`。因此 P0-001 从“缺失”降为“正式路径已存在，仍需收紧 legacy 入口/前端入口和调度边界”。

## 已确认做得比较扎实的部分

- 服务端权威与串行化：`src/Riftbound.Engine/MatchSession.cs:2375` 通过 `serialGate` 串行处理命令；`src/Riftbound.Engine/MatchSession.cs:2421` 只在 `result.Accepted` 时更新权威状态；`src/Riftbound.Api/Hubs/GameHub.cs:216` 只接收命令并广播服务端结果。
- 房间/重连/按玩家发送视图：`src/Riftbound.Api/Hubs/GameHub.cs:24` 支持加入房间，`src/Riftbound.Api/Hubs/GameHub.cs:53` 支持重连，`src/Riftbound.Api/Hubs/GameHub.cs:270` 按玩家组发送 snapshot/prompt。
- 基础隐藏信息：`src/Riftbound.Engine/MatchSession.cs:743` 对非己方手牌只给数量，`src/Riftbound.Engine/MatchSession.cs:811` 对非己方面朝下对象做字段裁剪；相关测试覆盖见 `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs:459`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs:29922`。
- 开发场景安全边界：`src/Riftbound.Api/Hubs/GameHub.cs:154` 的 `SeedScenario` 被限制在 Development 环境。
- 行为目录和 fixture 体系已经很大：`tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs:68`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 说明当前项目已有系统性测试基础。

## P0 问题

### P0-001 官方构筑、开局、调度流程缺失

当前状态：**PARTIALLY RESOLVED / 正式入口、Hub 级 deck submit smoke、前端正式 prompt 入口、基础负例矩阵与起手调度边界已收紧**

当前 active blocking 口径：P0-001 保留为历史和边界加固章节；formal 18-step 已覆盖官方 deck/opening/mulligan 主流程，当前 completion audit 的 P0 阻断集中在 P0-002 / P0-003 / P0-004 / P0-005。

规则依据：自查文档 3.1/3.2；核心规则关于 1v1 构筑、开局准备、随机战场、起手、调度、P2 额外符文的要求。

代码位置：
- `src/Riftbound.Contracts/Protocol.cs` 新增 `SubmitDeckCommand` 和 `MulliganCommand`。
- `src/Riftbound.Engine/OfficialDeckRules.cs` 新增官方 decklist 模型与校验器，覆盖主牌 40+、符文 12、战场 3、传奇/英雄匹配、同名 3、唯我 1、专属卡限制、颜色/符文特性约束。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `SubmitDeckAsync`、正式开局构建、按玩家 snapshot 的 `deckSubmitted`/`mulliganCompleted` 标记。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `MULLIGAN` 解析和调度结算，并修正后手额外符文从固定 seat P2 扩展为 `OpeningSecondActionPlayerId`。

现象：正式 deck path 已可测，并且 Hub 级 smoke 已覆盖双方通过 WebSocket/房间层提交 deck、ready、进入 mulligan、完成起手调度并进入首回合。起手调度拒绝错误玩家、超过 2 张、重复选择与非手牌选择；极端抽牌不足时只抽取可用牌并把搁置牌回收到主牌堆，不触发燃尽。为了兼容既有开发 seed 和旧测试，Development 与默认测试会话仍允许 no-deck legacy `READY`；API 在非 Development 环境创建的正式房间会关闭 legacy ready，未提交 deck 的玩家提交 `READY` 会被拒绝且状态不变。产品 UI 主流程现在按服务端 prompt 显式执行 `SUBMIT_DECK -> READY -> MULLIGAN`，不再在未提交 deck 时展示 ready 游戏命令。

最小复现场景：创建房间，P1/P2 先提交合法 `SUBMIT_DECK`，再双方 `READY`，服务端进入 `MULLIGAN`，双方按顺序 `MULLIGAN` 后进入首回合 `MAIN`。如果不提交 deck 而直接 `READY`，当前仍为 legacy 兼容路径。

建议修复：
- 已完成：将服务端 prompt 改为未提交 deck 只暴露 `SUBMIT_DECK`，提交后才暴露 `READY`；产品 UI 主流程只渲染服务端候选。
- 已完成：增加会话配置开关，非 Development API 房间禁止 legacy no-deck ready。
- 已完成：补基础负例矩阵，覆盖英雄不在主牌、主牌非法类别、未知卡号、符文/战场牌堆类别错误、越出传奇特性等拒绝路径。
- 已完成：补起手调度边界，覆盖非法选择、错误调度玩家和主牌堆不足时的服务端行为。
- 后续可继续补专属卡超过 3、唯我同名与多特性组合的更细 fixture，但 P0-001 当前主要剩余风险已转向前端正式入口。

建议测试：
- 已新增：`tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs`。
- 已新增：正式-only 会话拒绝 no-deck `READY`。
- 已新增：GameHub 级 `SUBMIT_DECK -> READY -> MULLIGAN` smoke。
- 已新增：官方 deck validator 负例矩阵。
- 已新增：起手调度非法选择与抽牌不足边界。
- 已新增：前端正式入口 Browser Use smoke，覆盖 P1/P2 入座、提交 deck、ready、双方 mulligan 与进入 `MAIN`。

### P0-002 战场、待命区、控制权和单位位置模型不足

当前状态：**PARTIALLY RESOLVED / 对象位置索引、权威派生战场状态、权威 battlefield task 视图、具体战场移动、4D-01 destination-scoped contest task 与 reconnect redaction foundation 已落地；完整 held/conquer/control lifecycle 仍待建模**

规则依据：自查文档 4、10；核心规则关于基地、战场、待命区、战场控制权、占领/争夺、单位移动与区域归属的要求。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 新增 `ObjectLocationState` 与 `MatchState.ObjectLocations`，snapshot 会输出对象 `location`，`SnapshotDto.Lanes.battlefields` 会投影战场牌、控制者、占据单位、待命占位、面朝下待命数量与争夺状态。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldState` 与 `MatchState.BattlefieldStates`，snapshot 复用该服务端状态视图，而不是自行重算 UI 专用结构。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldTaskState` 与 `MatchState.BattlefieldTasks`，争夺战场会公开 `START_SPELL_DUEL`、`START_BATTLE` 的任务状态、参与者和关联 stack/focus 信息。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的打出、结算、移动、调度、召符文路径开始同步 `ObjectLocations`。
- `dda6385` 基线中，`src/Riftbound.Engine/CoreRuleEngine.cs` 的移动 origin/destination 规范化曾使用全串 `ToUpperInvariant()`；这会把 `BATTLEFIELD:<objectId>` 冒号后的 objectId 一并改写。2026-05-09 第一轮 B 修复后，当前移动 location 解析只规范化冒号前 zone，保留 objectId 原始大小写，并已补小写 `a` 官方战场测试。
- 4D-01 已新增 `BoardTaskQueueFoundationTests`，覆盖 precise roam mixed-case destination、destination-only contest queue、base-to-battlefield / battlefield-to-base 代表路径和 reconnect redaction。
- 仍缺：每个战场的 held/conquered/占领结果、待命容量、control freeze/release 和完整 control/contest 变更由统一 task queue 推进。

现象：系统现在可以在权威状态中表达对象所在粗粒度区域和精确战场 object id，并拒绝来源位置与权威状态不一致的精确游走；`MatchState.BattlefieldStates` 能从服务端状态统一暴露已知战场的占据/争夺/待命视图；`MatchState.BattlefieldTasks` 能把争夺战场后续 spell duel/battle 任务公开给 UI 和日志。`dda6385` 具体战场移动提交曾存在大小写风险；第一轮已确认官方 card catalog 中小写 `a` 战场 `OGN·276a/298` 能在 `BATTLEFIELD:<objectId>` 目的地中逐字保留，并写回 `ObjectLocations.BattlefieldObjectId`。4D-01 进一步固化：战场间 precise roam 会保留 mixed-case 目的地 object id，且只为目的地战场排 `START_SPELL_DUEL` / `START_BATTLE`。战场本身仍没有完整控制权变更、占据、征服、战斗/法术对决任务推进生命周期，因此 P0-002 仍只能降级为部分解决。

最小复现场景：在两个友方战场之间提交精确 `MOVE_UNIT` 游走。当前结果会写回 `ObjectLocations[source].BattlefieldObjectId`；如果客户端提交的 origin 与权威位置不一致，服务端会拒绝。第一轮回归场景已覆盖：目的地为 `BATTLEFIELD:<小写 a 战场 objectId>` 时，规范化只影响 zone，不会把 objectId 改成大写 `A`。

建议修复：
- 在当前 snapshot 视图基础上继续把 `BattlefieldState`/`BattlefieldTaskState` 接入真正的 `PendingTaskQueue`，覆盖 held/conquered、控制权改变和占领结果。
- 将 `PlayerZones.Battlefields` 从“对象列表”升级为“玩家战场槽位 + 位置引用”，并让 `CardObjectState` 或 location index 记录对象所在具体位置。
- 保持移动/战斗/待命 destination 规范化约束：只允许 `BASE` / `BATTLEFIELD` zone 大小写规范化，冒号后的 objectId 必须逐字保留。

建议测试：
- 单位从基地到战场、战场到基地、战场间游走。
- 以 `OGN·276a/298`、`OGN·278a/298`、`OGN·293a/298` 对应战场对象覆盖 prompt destination、submit destination、`ObjectLocations.BattlefieldObjectId`、`BattlefieldStates` snapshot 和 reconnect/recovery 后的大小写保持。
- 已补 4D-01：移入空战场、移入敌方占据战场、战场间 roam 到敌方占据目的地、栈结算移回基地、reconnect 后 pending task redaction。
- 待补：held/conquer scoring、control freeze/release、完整战斗/法术对决 pending 状态机和所有战场控制变化路径。
- 待命区容量、面朝下信息、revealed 后转移。

### P0-003 通用清理检查与任务队列缺失

当前状态：**PARTIALLY RESOLVED / 状态性 cleanup task、battlefield task 视图、致命伤害 cleanup loop、非法待命 / 未贴附装备清理、cleanup repeat、blocking guard 与 4D-01 reconnect redaction foundation 已接入；完整统一任务队列仍缺失**

规则依据：自查文档 5；核心规则关于“任意状态变化后进行清理检查、重复直到稳定、触发待处理任务、清理期间不能响应”的要求。`SOUL-OFAQ-260114` p19-p20 进一步澄清：0/负战力单位不会自动被摧毁，必须受到至少 1 点有效伤害后才会死亡；负战力战斗伤害输出按 0，但实际战力值保留。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:18896` 有局部 `ApplyLethalDamageCleanup`。
- `src/Riftbound.Engine/CoreRuleEngine.cs:19362` 有回合结束清理。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10641` 的 `ResolveEndTurn` 只在回合结束路径调用 `ApplyTurnEndCleanup`，然后直接 `ResolveTurnStart`。
- `dda6385` 基线中，`src/Riftbound.Engine/MatchSession.cs` 的 `PendingCleanupTasks` 可显式暴露致命伤害、`DESTROY_ZERO_POWER_UNIT` 和战场争夺检查任务；其中 `DESTROY_ZERO_POWER_UNIT` 是 P0 冲突点，不能按官方 FAQ 视为已完成。2026-05-09 第一轮 B 修复后，当前引擎不再生成仅因 `Power <= 0` 的 cleanup task。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `PendingTaskQueue`，按状态性清理、战场争夺、法术对决启动、战斗启动的顺序公开当前 active task 和 blocking phase。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 新增 `RunStateBasedCleanupLoop`，移动和结算链结算后会重复执行致命伤害/0 战力/非法待命/未贴附装备清理直到稳定；`ResolveAsync` 会在 blocking pending task queue 期间拒绝普通玩家命令。
- 4D-01 已新增 focused tests，覆盖 cleanup-first ordering、普通命令 no-mutation、cleanup repeat until stable、非法待命 / 未贴附装备 prompt redaction、reconnect 后 pending task phase / active task / hidden-info redaction。
- `dda6385` 基线中，`MatchSession` / `CoreRuleEngine` 的 `IsZeroPowerCleanupCandidate` 以 `Power <= 0` 作为 zero power cleanup candidate 的核心条件，未要求 `Damage >= 1`。2026-05-09 第一轮 B 修复已改为 0/负战力且有非零伤害才进入致命伤害清理。

现象：当前清理仍没有完全升级为官方意义上的“所有状态变化后统一检查并重复”的持久任务队列。移动、栈项目结算、战斗伤害、Xerath 技能伤害和回合开始战场群体伤害会运行状态性致命伤害 cleanup loop 并同步位置；栈结算后若发现非法待命，也会通过同一 cleanup loop 移入墓地、翻面、同步位置并清空该 pending task；未贴附装备可通过同一 cleanup loop 召回基地；`PendingCleanupTasks` 已能列出待处理的致命伤害、非法待命、未贴附装备、战场争夺、`START_SPELL_DUEL` 与 `START_BATTLE` 任务；`PendingTaskQueue` 能按清理优先级公开 active task 和 blocking phase；`BattlefieldTasks` 能给这些战场任务附带状态和参与者。服务端 prompt/command guard 已阻止普通行动在 blocking queue 期间继续执行，reconnect 后仍保留 pending phase / active task 并维持对手视角隐藏信息。2026-05-09 第一轮已修正 0/负战力自动死亡冲突：`Power <= 0 && Damage == 0` 不清理，`Power <= 0 && Damage > 0` 走致命伤害清理。由战场控制权变化、替代效果、所有进出战场路径等触发的 pending duel/battle/控制权变化仍无法通过一个中央状态机保证。

最小复现场景：尝试移动一个已经带致命伤害的单位，当前会因 `DESTROY_LETHAL_UNIT` blocking task 被拒绝；结算一个无行为栈项目时，如果场上已有致命伤害单位或非法待命，会在栈结算后被状态性清理兜底处理。第一轮新增验收覆盖：正面场上单位被修正到 0 或负数但没有伤害时继续留场；同一类单位受到至少 1 点伤害后会在清理中被摧毁；负战力战斗输出为 0 但对象实际战力保留。如果移动导致战场控制权/争夺状态变化，仍没有统一 cleanup loop 能保证后续待处理任务被稳定排入并按官方顺序解决。

建议修复：
- 保持第一轮修复后的 `DESTROY_LETHAL_UNIT` / `LETHAL_DAMAGE` 语义：`Power <= 0` 不是自动清理条件；只有 `Damage >= 1` 且满足清理时才应进入摧毁流程。
- 引入 `PendingTaskQueue` 与 `RunCleanupLoop`，统一处理致命伤害、离场、战场控制变化、法术对决/战斗启动、胜负检查。
- 所有命令、栈结算、触发结算、移动、进场/离场之后必须进入同一 cleanup loop。

建议测试：
- 已新增：移动、栈结算、战斗伤害、Xerath 技能伤害、回合开始战场伤害进入 cleanup loop；非法待命会暴露 `REMOVE_ILLEGAL_STANDBY` blocking task，且代表性栈结算后会自动翻面入墓并清空该 pending task；争夺战场暴露 START_SPELL_DUEL/START_BATTLE 任务及 `BattlefieldTasks` 权威任务视图；待清理 blocking queue 会关闭普通 prompt 并拒绝普通命令。
- 已补：`Power <= 0 && Damage == 0` 不暴露 blocking cleanup task、不自动入墓；`Power <= 0 && Damage > 0` 在清理中入墓；负战力单位战斗输出按 0，但对象实际战力值保留。
- 已补 4D-01：cleanup loop 重复直到稳定、普通命令 no-mutation、非法待命/未贴附装备 redaction、reconnect 后 pending task redaction。
- 待补：替代效果、所有进出战场路径、完整控制权变化和完整 battle cleanup 都触发同一持久 cleanup task queue。
- 已新增：cleanup/task queue 阻塞期间 prompt 只给 `WAIT`，普通命令被 `PhaseNotAllowed` 拒绝。

### P0-004 法术对决与战斗不是完整官方状态机

当前状态：**PARTIALLY RESOLVED / 4D-02 focused slice 已收窄多争夺战场、wrong-focus、task binding 与 reconnect redaction；完整官方任务状态机仍缺失**

规则依据：自查文档 11、12；核心规则关于 FEPR、法术对决焦点、初始栈、双方行动、战斗 pending、攻击/防守单位声明、战斗清理、无战斗结果的要求。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:232` 的命令分发直接按 `PlayCard`、`MoveUnit`、`DeclareBattle` 等命令进入各自 resolver。
- `src/Riftbound.Engine/MatchSession.cs` 的 `StackItemState.TimingContext` 现在记录入栈前的 timing window。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `TurnWindowState`、`SpellDuelState`、`BattleState`，snapshot timing 暴露四类窗口、法术对决和战斗参与者视图。
- `src/Riftbound.Engine/MatchSession.cs` 新增 `BattlefieldTaskState`，snapshot timing 暴露 `battlefieldTasks`，争夺战场可以把后续 spell duel/battle 任务状态化。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolvePassPriority` 现在能在法术对决栈清空时恢复 `SPELL_DUEL_OPEN` 并转移焦点。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolvePassFocus` 现在能关闭争夺战场法术对决、标记 matching `START_SPELL_DUEL` completed，并把 queue 推进到 matching `START_BATTLE`。
- `src/Riftbound.Engine/CoreRuleEngine.cs:4174` 的 `ResolveDeclareBattle` 直接执行战斗。
- `src/Riftbound.Engine/CoreRuleEngine.cs:5185` 的 `TryBuildMinimalDeclareBattle` 只支持 1 个攻击者、1 到 2 个防守者，且条件被命名为 minimal。
- `src/Riftbound.Engine/MatchSession.cs` 的 `DeclareBattleSourceRequirements` 现在按同一代表路径公开 1 个必选防守槽；只有在存在壁垒/后排伤害分配关键词防守者时才公开第二个可选防守槽。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolveDeclareBattle` 直接计算并应用伤害；代表路径已在伤害与致命清理后执行 `DAMAGE_REMOVED` 战斗清理，并在防守方仍在该战场时通过 `UNIT_RECALLED_TO_BASE` 召回战场上的进攻单位。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `ResolveAssignCombatDamageRuntime` / `CommitCombatDamageAssignments` 已有代表性 battle damage assignment window，并用 battle id / battlefield id / participant validation 约束提交。
- `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md` 已建立 4D-02 写锁、验收过滤器和实现前绿线。
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs` 已覆盖 4D-02 focused slice：多争夺战场 one-active ordering、wrong-focus no-mutation、spell-duel stack task binding、cleanup 后推进下一 task、`SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect metadata + redaction。

现象：当前战斗仍有大量路径依赖显式 `DECLARE_BATTLE` 命令驱动的“立即结算战斗片段”，不是完整由清理任务在争夺战场时启动、推进和关闭的 battle task。法术对决已修复几个关键窗口问题：迅捷牌结算后不会提前关闭法术对决；反应/反制链会继承并保留法术对决 timing context；core prompt 在法术对决焦点窗口也会暴露可支付、合法时点的迅捷出牌来源。现在服务端也能显式表达四类窗口、当前法术对决、战斗参与者、争夺战场任务视图、1-2 防守者 direct/minimal 代表路径的服务端候选、代表性伤害分配窗口，以及代表路径的战斗清伤害/进攻方召回；但仍缺少围绕某个 battle/trigger/card 的完整 pending/focus/initial-stack 生命周期。

最小复现场景：迅捷牌在 `SPELL_DUEL_OPEN` 焦点窗口打出并结算后，当前会回到 `SPELL_DUEL_OPEN` 且焦点移交下一名玩家。单位移动到敌方控制战场时，按官方规则应进入争夺并触发法术对决/战斗流程；这一部分仍没有完整 battle task。

建议修复：
- 建立 `SpellDuelState` 和 `BattleState`，由 cleanup/task queue 创建、推进和关闭。
- 声明攻击/防守、初始栈、双方 focus/pass、swift/reaction 许可、战斗伤害、战斗结果和清理全部挂在同一状态机。

建议测试：
- 已补 4D-02 baseline：focused 29/29、adjacent 121/121 通过，覆盖既有 spell duel / start battle / declare battle / assign combat damage 代表路径。
- 已补 4D-02 focused slice：focused new 6/6、focused handoff 35/35、adjacent 127/127、backend full 3786/3786 通过，覆盖多个争夺战场 one-active-task ordering、非焦点 / 错时机 `PASS_FOCUS` no-mutation、swift/reaction task binding、`SPELL_DUEL_TASKS` / `BATTLE_TASKS` reconnect redaction 和 cleanup 后推进下一 task。
- 已补 4D-02E focused slice：`docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02E_BATTLE_TASK_ADVANCEMENT_EVIDENCE.md`；focused 427/427、adjacent 608/608、backend full 4195/4195、`git diff --check` 通过，覆盖 natural `ASSIGN_COMBAT_DAMAGE` 关闭当前 battle 后推进下一个 contested battlefield `START_SPELL_DUEL`。
- 已补 4D-02F focused slice：`docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_02F_BATTLE_ASSIGNMENT_NO_RESULT_EVIDENCE.md`；focused 428/428、adjacent 608/608、backend full 4196/4196、`git diff --check` 通过，覆盖 natural `ASSIGN_COMBAT_DAMAGE` 分支的 all-participants-destroyed no-result representative。
- 待补：完整 battle response window、initial stack、swift/reaction 链矩阵、battle id / participant lifecycle 全路径、no-result 全矩阵剩余分支、replacement / prevention / cleanup 全组合。
- 由移动/占领触发的法术对决与战斗。
- focus 轮转、pass、swift/reaction、初始栈顺序。
- 多攻击者/多防守者、伤害分配顺序、战斗没有结果时的状态。

### P0-005 彩色符能、普通费用、符能费用与资源技能模型不足

当前状态：**PARTIALLY RESOLVED / typed pool 已覆盖 PLAY_CARD、代表性非出牌支付、一个 battlefield trigger 支付路径、基础符文横置得法力、代表性回收得同特性符能、PLAY_CARD 支付步骤回收符文资源动作、Vi / Xerath ACTIVATE_ABILITY 支付资源动作、HIDE_CARD 待命暗置 shared payment plan、ordinary pending PAY_COST 支付资源动作、battlefield held score 支付资源动作、SFD Fiora trigger payment resource action、Renata colored activated draw typed-blue cost、Renata colored activated score typed-blue exhaust cost、Renata draw / score typed temporary resource payment parity、Crimson Rose experience-cost target-bearing ready-unit、Shadow swift combat-response stun representative、activated ability Spellshield tax catalog-bound coverage verifier、resource skill catalog-bound coverage verifier、SFD/OGN Sigil typed payment-only resource family representative、resource conversion equipment representative 与 Gold token resource ability representative，MOVE_UNIT、ASSEMBLE_EQUIPMENT、ACTIVATE_ABILITY 与 LEGEND_ACT 已有每来源服务端候选，急速额外费用已有资源动作回归证据；4D-03 PaymentEngine handoff / baseline 与 focused foundation 已验收，4D-03B / 4D-03C / 4D-03D / 4D-03E / 4D-03F / 4D-03G / 4D-03H focused slices 已验收，4D-03I Malzahar resource skill、4D-03J Malzahar lifecycle、4D-03K temporary inline、4D-03L Dragon Soul Sage reaction resource skill、4D-03M Renata colored activated draw、4D-03N Renata colored activated score、4D-03O Crimson Rose ready-unit、4D-03P Fluft Poro Warhawk token、4D-03Q Shadow swift stun、4D-03R Rage Sigil typed resource、4D-03S SFD Sigil typed resource family、4D-03T OGN Sigil typed resource family、4D-03U resource conversion equipment、4D-03V Gold token resource skill、4D-03W Renata Gold bonus、4D-03AC battlefield held temporary resource、4D-03AD trigger temporary resource、4D-03AE pending temporary prompt aggregate、4D-03AG PLAY_CARD typed resource prompt parity、4D-03AJ Renata typed temporary resource focused slice、4D-03AK Spellshield tax coverage verifier、4D-03AL resource skill coverage verifier 与 4D-03AF remaining-scope audit / 4D-03AH action-window coverage verifier 已验收或建立，完整 PaymentEngine 与 reaction payment window 仍待统一**

4D-03R Rage Sigil typed resource handoff / baseline 已建立并被 focused slice 验收 supersede：focused baseline 173/173、adjacent baseline 421/421 通过；该基线仍保留为回归护栏，不升级当前 P0-005 状态。
4D-03R Rage Sigil typed resource focused slice 已验收：focused 191/191、adjacent 439/439、backend full 4021/4021 通过；该切片只证明 typed red payment-only temporary resource representative，不关闭完整 P0-005。
4D-03S SFD Sigil typed resource family focused slice 已验收：focused 213/213、adjacent 461/461、backend full 4043/4043 通过；该切片只证明剩余五张 SFD Sigil typed temporary payment resource，不升级当前 P0-005 状态。
4D-03T OGN Sigil typed resource family focused slice 已验收：focused 238/238、adjacent 486/486、backend full 4068/4068 通过；该切片只证明 OGN 六张 Sigil typed temporary payment resource，不升级当前 P0-005 状态。
4D-03U resource conversion equipment focused slice 已验收：focused 230/230、adjacent 485/485、backend full 4089/4089 通过；该切片只证明能量通道、远古簇碑、海克斯异常体三张 conversion equipment resource skills，不升级当前 P0-005 状态。

规则依据：自查文档 8、15；核心规则关于 `A/C`、阵营符能、费用支付、符文技能、可选费用、Spellshield/Encourage/Echo/Haste 等费用分支。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 的 `RunePool` 已升级为 `Mana` + 泛化 `Power` + `PowerByTrait`，并通过 `RuneTrait.Normalize` 支持中英文颜色别名。
- `src/Riftbound.Engine/CoreRuleEngine.cs:10141` 的出牌计划从 `CardBehaviorRegistry` 获取行为并做局部费用计算。
- `src/Riftbound.Engine/MatchSession.cs` 的 `ActionPromptBuilder` 会要求 `PLAY_CARD` / `MOVE_UNIT` / `ASSEMBLE_EQUIPMENT` / `ACTIVATE_ABILITY` / `LEGEND_ACT` 存在服务端候选源才启用；出牌来源会经过 BehaviorRegistry、时点权限和基础费用过滤。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 的 `PLAY_CARD` 支付计划已可记录任意符能与指定特性符能，并通过 `PayRuneCosts` / `CanPayRuneCosts` / `CanPayPowerCost` 校验与扣费。
- `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03_PAYMENT_ENGINE_BASELINE_EVIDENCE.md` 已建立下一服务端切片的写锁、PaymentPlan / Quote / Authorize / Commit 验收口径和实现前绿线。
- `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_BASELINE_EVIDENCE.md` 已建立 non-play payment window 迁移写锁、focused / adjacent 验收过滤器和实现前绿线。
- `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03B_PAYMENT_ENGINE_NON_PLAY_EVIDENCE.md` 已记录 non-play payment focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_EVIDENCE.md` 已记录 Vi / Xerath `ACTIVATE_ABILITY` payment resource focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md` 已建立 `HIDE_CARD` standby payment plan 迁移交接与实现前绿线。
- `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_EVIDENCE.md` 已记录 `HIDE_CARD` standby payment focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md` 已建立普通 pending `PAY_COST` resource action 迁移交接与实现前绿线。
- `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_EVIDENCE.md` 已记录普通 pending `PAY_COST` payment resource focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_BASELINE_EVIDENCE.md` 已建立 battlefield held score resource action 迁移交接与实现前绿线。
- `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03G_PAYMENT_ENGINE_BATTLEFIELD_HELD_RESOURCE_EVIDENCE.md` 已记录 battlefield held score payment resource focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_BASELINE_EVIDENCE.md` 已建立 SFD Fiora trigger payment resource action 迁移交接与实现前绿线。
- `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03H_PAYMENT_ENGINE_TRIGGER_RESOURCE_EVIDENCE.md` 已记录 SFD Fiora trigger payment resource focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03I_PAYMENT_ENGINE_RESOURCE_SKILL_EVIDENCE.md` 已记录 Malzahar open-main resource skill focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03J_PAYMENT_ENGINE_RESOURCE_SKILL_LIFECYCLE_EVIDENCE.md` 已记录 Malzahar lifecycle focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03K_PAYMENT_ENGINE_TEMPORARY_RESOURCE_INLINE_EVIDENCE.md` 已记录 temporary payment-only resource inline focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_BASELINE_EVIDENCE.md` 已建立 Dragon Soul Sage reaction resource skill 迁移交接与实现前绿线。
- `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03L_PAYMENT_ENGINE_REACTION_RESOURCE_SKILL_EVIDENCE.md` 已记录 Dragon Soul Sage reaction resource skill focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_BASELINE_EVIDENCE.md` 已建立 Renata Glasc colored activated draw skill 迁移交接与实现前绿线；该基线已被 focused slice 验收 supersede。
- `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03M_PAYMENT_ENGINE_COLORED_ACTIVATED_DRAW_EVIDENCE.md` 已记录 Renata Glasc colored activated draw focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_BASELINE_EVIDENCE.md` 已建立 Renata Glasc colored activated score skill 迁移交接与实现前绿线；该基线已被 focused slice 验收 supersede。
- `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03N_PAYMENT_ENGINE_COLORED_ACTIVATED_SCORE_EVIDENCE.md` 已记录 Renata Glasc colored activated score focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_BASELINE_EVIDENCE.md` 已建立 Crimson Rose ready-unit skill 迁移交接与实现前绿线；该基线已被 focused slice 验收 supersede。
- `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03O_PAYMENT_ENGINE_CRIMSON_ROSE_READY_UNIT_EVIDENCE.md` 已记录 Crimson Rose ready-unit focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_BASELINE_EVIDENCE.md` 已建立 Fluft Poro battlefield-only Warhawk token skill 迁移交接与实现前绿线；该基线已被 focused slice 验收 supersede。`docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md` 已记录 Fluft Poro Warhawk token focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_BASELINE_EVIDENCE.md` 已建立 Shadow swift stun 迁移交接与实现前绿线；focused baseline 198/198、adjacent baseline 738/738 通过。该基线已被 focused slice 验收 supersede，仍保留为回归护栏。
- `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md` 已记录 Shadow swift stun focused slice 验收证据。
- `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_HANDOFF.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_BASELINE_EVIDENCE.md` 已建立 resource conversion equipment 迁移交接与实现前绿线；该基线已被 focused slice 验收 supersede，仍保留为回归护栏。
- `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_AUDIT.md` 与 `docs/CURRENT_STAGE4D_03U_PAYMENT_ENGINE_RESOURCE_CONVERSION_EQUIPMENT_EVIDENCE.md` 已记录 Energy Channel / Ancient Stele / Hextech Anomaly resource conversion equipment focused slice 验收证据。
- `CoreRuleEngine` 中 Vi / Xerath `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 代表窗口已接入 shared `PaymentPlan` / `TryCommitPayment`。
- `ASSEMBLE_EQUIPMENT`、Vi 双倍战力技能、Xerath 伤害技能、能量枢纽据守支付 4 符能得分等代表性非出牌/战场触发支付路径已改为 typed-power aware；泛化符能费用会从普通 `Power` 优先扣除，再按特性名稳定扣除 `PowerByTrait`。
- `TAP_RUNE` 现在实现基础符文横置获得 1 法力，并在 prompt 中只暴露可执行符文来源。
- `MOVE_UNIT` prompt 现在公开每来源 `sourceRequirements`，包括起点、移动模式、目的地候选、必需/可选费用和可组合状态；前端已按该元数据提交移动命令。
- `ASSEMBLE_EQUIPMENT` prompt 现在公开每来源 `sourceRequirements`，包括代表性长剑装配目标、红色符能费用、必需费用 token 和可组合状态；装配来源和目标都必须有服务端权威 `cardNo`，前端已按该元数据提交装配命令。
- `ACTIVATE_ABILITY` prompt 现在公开每来源 `sourceRequirements`，包括代表性 Vi/Xerath/Renata/Crimson Rose/蜕变花园授予能力来源、目标槽、资源费用、typed blue cost、experience cost、Spellshield 加税过滤、横置来源、普通 stack item 和立即结算状态；前端已按该元数据提交激活能力命令。
- `ACTIVATE_ABILITY.sourceRequirements` 现在也能在 Vi / Xerath 当前 power 不足但基地有可回收符文时公开 `RECYCLE_RUNE:<objectId>` payment resource choice、per-choice power contribution 与 available-power metadata。
- `ACTIVATE_ABILITY.sourceRequirements` 现在也能在 Renata Glasc 当前 blue typed power 不足但基地有可回收蓝色符文时公开 `RECYCLE_RUNE:<objectId>` payment resource choice、draw skill 的 `powerCostByTrait.blue=1`、score skill 的 `powerCostByTrait.blue=4`、per-choice trait contribution 与 available-power metadata；score skill 会公开 `exhaustsSource=true` 并在 source 已横置时隐藏，Malzahar temporary payment-only resources 不会被公开为 Renata typed-blue 费用支付候选。
- `ACTIVATE_ABILITY.sourceRequirements` 现在也能在 Crimson Rose controlled base equipment source 上公开 `experienceCost=3`、target choices、enemy Spellshield target tax policy、`exhaustsSource=true`、`stackPolicy=ordinary-stack-item-before-ready`；命令侧会支付 3 experience 与必要 target-tax mana，横置 source，创建普通 stack item，pass-pass 后才 ready target。
- `ACTIVATE_ABILITY.sourceRequirements` 现在也能在 Energy Channel / Ancient Stele / Hextech Anomaly controlled base equipment source 上公开 reaction resource skill metadata、conversion choices、resource lifecycle 与 no-stack policy；命令侧能处理 gain-mana、mana-to-generic-temporary-power 与 ordinary-generic-power-to-mana 三条 representative path。
- `HIDE_CARD` prompt 已能公开待命暗置来源、目的地与 `STANDBY_A` / `STANDBY_TEEMO_MANA` / `STANDBY_FREE` 费用候选；Core 侧标准待命、Teemo 替代待命与免费待命实际支付已迁移到 shared `PaymentPlan` / `TryCommitPayment`。
- 普通 pending `PAY_COST` snapshot / prompt metadata 已能公开 `RECYCLE_RUNE:<objectId>` payment resource action、per-choice power contribution 和包含支付资源后的可用符能视图；Core 侧会在同一支付事务中先应用合法回收，再通过 shared `PaymentPlan` / `TryCommitPayment` 提交实际扣费。
- `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE` 现在也可在当前 power 不足但基地存在可回收基础符文时，随 `DECLARE_BATTLE` 的 `COMBAT_ASSIGNMENT` 一起提交必要 `RECYCLE_RUNE:<objectId>` payment resource action；Core 侧在同一 `paymentId` / `BATTLEFIELD_HELD` window 下先回收符文，再通过 shared `PaymentPlan` / `TryCommitPayment` 支付 4 power。
- `LEGEND_ACT` prompt 现在公开每来源 `sourceRequirements`，包括代表性传奇行动来源、能力、目标槽、经验/资源费用、前置条件、横置来源和立即结算状态；传奇行动来源和可提交目标都必须有服务端权威 `cardNo`，前端已按该元数据提交 Poppy 传奇行动命令。依赖第一目标再决定第二目标的武装类传奇行动暂以 `composable=false` 禁用前端提交。
- `RECYCLE_RUNE` 现在实现基础符文代表性 open-main 回收路径，来源回到底部符文牌堆，并按 `COLOR:*` 标签向 `PowerByTrait` 增加 1 点同特性符能。
- `PLAY_CARD` 现在支持代表性支付资源动作 token `RECYCLE_RUNE:<objectId>`；当当前符能不足以支付本次 power cost 时，服务端 prompt 会在 `sourceRequirements.paymentResourceChoices` / `optionalCostChoices` 暴露可回收符文，命令侧先结算该资源动作再扣 `SPEND_POWER:*` 或代表性 `HASTE_READY` 急速额外费用。对 X 符能法术，prompt 会同步公开当前可由 `PowerByTrait` 或支付资源动作支持的 `SPEND_POWER:<amount>` 与 typed `SPEND_POWER:<trait>:<amount>` 候选，金额上限来自服务端当前可支付符能。
- `PLAY_CARD.sourceRequirements.legalTargetSelections` 已能在总目标战力上限或 Spellshield 目标税影响组合合法性时枚举服务端可提交的目标对象组合；前端只按该列表启用确认按钮，多目标 Spellshield 出牌代表路径已有 Hub 与真实 UI smoke。
- `PLAY_CARD.sourceRequirements.optionalCostChoices` 已能把代表性战场 Echo 减免与战场授予下一个法术 Echo 计入 prompt 可用性与展示，避免 Core 可执行的 Echo 费用路径被前端隐藏。
- `PLAY_CARD.sourceRequirements.minimumManaCost` 已能把代表性战场装备减费计入来源过滤与展示；P1 只有 1 法力但控制《奥恩的锻炉》时，手牌《长剑》不会被 prompt 隐藏，并通过 `battlefieldEquipmentCostReductionMana` 告知前端展示减费。
- `PLAY_CARD.sourceRequirements.minimumManaCost` 已能把代表性战场据守非衍生物单位加费计入来源过滤与展示；P1 只有 4 法力且受到据守加费时，基础费用 3 的《忠实的工坊主》会显示为费用 4，并通过 `battlefieldHeldUnitCostIncreaseMana` 告知前端展示加费。
- 仍缺：把所有资源动作、费用来源、替代/额外费用和支付失败回滚纳入统一 PaymentEngine；完整 `[A]` / `[C]` resource skill family、target-bearing colored-cost activated abilities、`LEGEND_ACT` resource action、战场技能、Haste/Echo/Spellshield 等所有支付窗口和依赖型费用目标选择仍待进入同一个官方费用模型。

现象：服务端现在可以在 `PLAY_CARD` 的可选符能支付中表达并校验指定特性，例如 `SPEND_POWER:red:2` 会要求红色符能并只扣红色；旧 fixtures 的泛化 `power` 仍按任意符能兼容。装备装配、两个代表性主动技能和一个战场据守支付触发也可以用 `PowerByTrait` 支付泛化符能费用。普通开环 prompt 不再把无服务端来源、基础费用不足的出牌、非正面/战斗中移动源、无可支付装配源、装配来源或目标缺少权威 `cardNo`、无可支付/可选目标激活能力源、无可支付/时点不合法传奇行动源、传奇行动目标缺少权威 `cardNo`、不可横置符文、不可回收符文或被战场静态效果禁止的伏击进场来源展示为 enabled。基础符文横置会横置来源并向 runePool 增加 1 法力；基础符文回收会把来源回收到符文牌堆底部，并向 runePool 增加 1 点同特性符能；出牌支付步骤可用服务端候选 `RECYCLE_RUNE:<objectId>` 先获得同特性符能，再由同一 `PLAY_CARD` 命令用 `SPEND_POWER:*`、typed `SPEND_POWER:<trait>:<amount>` 或代表性 `HASTE_READY` 急速额外费用支付。Vi / Xerath 代表性 `ACTIVATE_ABILITY` 支付窗口也可用服务端候选 `RECYCLE_RUNE:<objectId>` 补足 power，并在同一个 `ACTIVATE_ABILITY` `paymentId` 下审计 `RUNE_RECYCLED` / `POWER_GAINED` / `COST_PAID`；Xerath 的 Spellshield tax mana 仍必须由当前 mana 支付。Renata Glasc 代表性 `ACTIVATE_ABILITY` 支付窗口现在分别支持 `powerCostByTrait.blue=1` 的抽牌技能与 `powerCostByTrait.blue=4` 且横置来源作为费用的得分技能，可用必要蓝色符文回收补足 typed-blue shortfall；抽牌技能成功后先创建普通 stack item，双方让过后才抽牌，得分技能成功后横置来源、创建普通 stack item，双方让过后才获得 1 分；wrong trait、temporary resource、重复/无效/不必要回收均 rejected no-mutation。Crimson Rose 代表性 `ACTIVATE_ABILITY` 支付窗口现在支持基地装备 source、3 experience cost、敌方法盾目标 mana tax、横置来源作为费用、普通 stack item before ready 与 pass-pass 后 ready target；friendly Spellshield target 不缴税，unsupported optional costs、`RECYCLE_RUNE:*` 与 `TEMP_PAYMENT_RESOURCE:*` 均 rejected no-mutation。能量枢纽据守支付 4 符能得分也可在当前 power 不足时通过必要 `RECYCLE_RUNE:<objectId>` 补足 power，并在同一 `BATTLEFIELD_HELD` payment window 下审计回收和扣费。`PLAY_CARD` 也能在代表性多目标 Spellshield 场景中公开服务端合法目标组合，前端不会允许选择超过总目标战力或当前法力无法支付加税的组合；代表性战场 Echo 减免、战场授予 Echo、战场装备减费和战场单位加费都会反映在服务端来源候选中。`MOVE_UNIT` 已能按具体来源暴露基地到战场、战场回基地和可定位游走候选，`ASSEMBLE_EQUIPMENT` 已能按具体来源暴露红色装配目标且不会暴露未知身份装配来源/目标，`ACTIVATE_ABILITY` 已能按具体来源暴露代表性能力、目标槽与支付资源选择，`LEGEND_ACT` 已能按具体传奇来源暴露代表性传奇行动与经验/目标要求且不会暴露未知身份传奇目标，前端只按这些候选提交命令。但同阵营符能、多符能组合、所有非出牌支付窗口中的 `[C]` 资源技能，以及由 legend/battlefield/skill 产生的复杂支付来源选择仍未统一。

最小复现场景：P1 拥有 `powerByTrait.red = 2` 时打出《弹幕时间》并提交 `SPEND_POWER:red:2` 会成功入栈且只消耗红色；如果只有 `powerByTrait.blue = 3`，同一命令会以 `INSUFFICIENT_COST` 拒绝且手牌、资源和结算链不变。P1 只有可回收的红色基础符文、但当前没有符能时，可以在同一个 `PLAY_CARD` 命令中提交 `RECYCLE_RUNE:<runeObjectId>` 与 `SPEND_POWER:red:1`，服务端先回收符文获得红色符能，再支付出牌符能并把来源加入栈。P1 只有 2 法力、0 power 和可回收红色基础符文时，可以在同一个 Vi `ACTIVATE_ABILITY` 命令中提交 `RECYCLE_RUNE:<runeObjectId>`，服务端先回收符文获得 red power，再支付 2 mana / 1 generic power 并把技能加入栈；若当前已经有足够 power，夹带同一回收动作会被拒绝且 no-mutation。P1 只有 1 mana、0 power、可回收红色基础符文且 Xerath 目标带 Spellshield 时，可以回收符文补足 power 并支付 Spellshield tax mana；若 mana 为 0，即使可回收符文存在也会以 `INSUFFICIENT_COST` 拒绝且 no-mutation。P1 只有可回收的紫色基础符文、当前没有符能但有足够法力时，也可以在同一个 `PLAY_CARD` 命令中提交 `RECYCLE_RUNE:<runeObjectId>` 与 `HASTE_READY`，服务端先回收符文获得 typed power，再支付急速额外费用并让单位以活跃状态进入基地。P1 只有 6 点法力时，`spellshield-multiple-tax` seed 中《妖异狐火》可以同时指定 Spellshield 与 Spellshield2 两个单位并支付 3 点基础法力 + 3 点 Spellshield 加税，但 5 战力单位不会出现在服务端合法目标组合中。P1 只有 3 点法力且控制《玛莱尖塔》时，`battlefield-static-echo-cost-reduction` seed 会把《台前作秀》的 Echo 候选公开为“额外支付 1 法力”，提交后仍由 Core 记录 Echo 减免并重复抽牌。P2 已获得 `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P2` 时，`battlefield-held-next-spell-echo-prompt` seed 会把本身没有额外费用目标的《台前作秀》公开为可支付 2 法力 Echo 的法术，提交后触发授予 Echo 事件并重复抽牌。P1 只有 1 法力且控制《奥恩的锻炉》时，`battlefield-static-equipment-cost-reduction` seed 会把基础费用 2 的《长剑》公开为 `minimumManaCost = 1`，前端只按服务端候选提交，Core 记录装备减费并让长剑结算到基地。P1 只有 4 法力且受到 `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1` 时，`battlefield-held-unit-cost-increase` seed 会把基础费用 3 的《忠实的工坊主》公开为 `minimumManaCost = 4`，前端只按服务端候选提交，Core 记录据守单位加费并结算单位与衍生物到基地。P1 只有 `powerByTrait.red = 1` 且普通 `Power = 0` 时，也可以装配长剑、启动 Vi/Xerath 的代表性泛化符能技能，并在支付后清空对应 typed 符能。P2 只有 `powerByTrait.red = 4` 且普通 `Power = 0` 时，能量枢纽据守支付 4 符能得分也会成功并扣空 red；P2 只有 3 generic power 和一个可回收红色基础符文时，也可以随 `DECLARE_BATTLE` 提交 `RECYCLE_RUNE:<runeObjectId>`，服务端先回收补足第 4 点 power，再支付据守得分费用。正式开局后，P2 横置基础蓝色符文获得 1 法力，再回收同一符文会把它移入符文牌堆底部并获得 `powerByTrait.blue = 1`。需要 `[A]`、所有支付步骤中的 `[C]` 资源技能、同阵营 Haste 支付、Spellshield 全支付窗口加税或 Echo/装备/单位加费复杂费用的更广路径仍未完整。

建议修复：
- 继续把所有支付都通过统一 `PaymentEngine` 校验，支持普通费用、符能费用、额外/可选费用、替代费用、减费/加费、符文技能。
- 为 `CardBehaviorDefinition` 或 BehaviorSpec 费用模型补充官方颜色需求，而不是只靠客户端传入 `SPEND_POWER:<trait>:<amount>`。

建议测试：
- 已新增：指定红色符能支付成功扣对应 trait；指定红色但只有蓝色时拒绝且状态不变；装备装配、Vi 技能、Xerath 技能和能量枢纽据守得分触发可以用 typed 符能支付泛化符能费用；Renata Glasc 技能可支付 1 mana + 1 blue typed power、必要时回收蓝色符文补足 typed shortfall，并在 stack pass-pass 后抽 1；Crimson Rose 技能可从 controller base equipment source 支付 3 experience、敌方法盾目标 tax、横置来源并在 stack pass-pass 后 ready target；出牌/移动/装配/激活能力/传奇行动/TAP_RUNE/RECYCLE_RUNE prompt 对基础不合法来源禁用；基础符文横置得 1 法力；基础符文回收得 1 点同特性符能并进入符文牌堆；`PLAY_CARD.sourceRequirements` 暴露可作为支付资源动作的 `RECYCLE_RUNE:<objectId>`，且同一出牌命令可先回收符文再支付 typed power；`ACTIVATE_ABILITY.sourceRequirements` 暴露 Vi / Xerath / Renata 可作为支付资源动作的 `RECYCLE_RUNE:<objectId>`，且同一激活技能命令可先回收符文再支付 power / typed blue；`PLAY_CARD.sourceRequirements.legalTargetSelections` 覆盖代表性多目标 Spellshield 加税组合；`PLAY_CARD.sourceRequirements.optionalCostChoices` 覆盖代表性战场 Echo 减免候选和战场授予下一个法术 Echo 候选；`PLAY_CARD.sourceRequirements.minimumManaCost` 覆盖代表性战场装备减费和战场据守单位加费候选；`PLAY_CARD.destinationChoices` 覆盖代表性战场静态禁止单位进场过滤；`MOVE_UNIT.sourceRequirements` 覆盖基地单位来源、起点、模式、目的地候选和可选费用边界；`ACTIVATE_ABILITY.sourceRequirements` 覆盖 Vi/Xerath/Renata/Crimson Rose 来源、目标槽、typed/experience cost、Spellshield 加税过滤和 payment metadata；`PaymentEngineCoverageAuditTests` 锁定当前所有 `AppliesSpellshieldTargetTax=true` activated abilities 均有 prompt / command / `COST_PAID` / rollback anchors，也锁定当前所有 `IsResourceSkill=true` ability ids 均有 family-level prompt / command / `ABILITY_ACTIVATED` / rollback anchors；`LEGEND_ACT.sourceRequirements` 覆盖 Poppy/Yasuo/Jax 来源、能力、目标槽、费用和不可组合依赖型双目标原因；4D-03AV 已把 PaymentEngine residual official blocker families 固定为 executable manifest，4D-03AX 已把 `LEGEND_BATTLEFIELD_TRIGGER_RESOURCE_ACTIONS` 拆成 `LEGEND_ACT` / `BATTLEFIELD_HELD_SCORE_PAYMENT` / `TRIGGER_PAYMENT` window-level manifest；这些只证明 blocker / representative 可回归性，不关闭 P0-005。
- 已补 4D-03 实现前基线：focused payment baseline 51/51、adjacent payment / ActionPrompt / GameHub regression 240/240 通过；该基线只证明既有代表支付路径绿色，不关闭 P0-005。
- 已补 4D-03B 实现前基线：focused non-play payment baseline 18/18、adjacent ActivateAbility / LegendAct / BattlefieldHeld / ActionPrompt / GameHub regression 318/318 通过；该基线只证明既有非出牌代表路径绿色，不关闭 P0-005。
- 已补 4D-03B focused slice 验收：focused 18/18、adjacent 318/318、backend full 3791/3791 通过；该切片只证明代表性 non-play payment windows 已接入 shared plan / commit，不关闭 P0-005。
- 已补 4D-03C 实现前基线：focused play optional / extra baseline 31/31、adjacent Haste / Echo / Spellshield / Experience / PaymentResource / ActionPrompt / GameHub regression 363/363 通过；该基线只证明既有 `PLAY_CARD` optional / extra / payment-resource 代表路径绿色，不关闭 P0-005。
- 已补 4D-03C focused slice 验收：focused 31/31、adjacent 363/363、backend full 3791/3791 通过；该切片只证明代表性 `PLAY_CARD` optional / extra / payment-resource windows 已进一步接入 shared plan audit / authorize 口径，不关闭 P0-005。
- 已补 4D-03D 实现前基线：focused activate payment resource baseline 79/79、adjacent ActivateAbility / PaymentResource / ActionPrompt / GameHub regression 252/252 通过；该基线只证明既有 `ACTIVATE_ABILITY`、基础符文资源技能和 payment-resource 代表路径绿色，不关闭 P0-005。
- 已补 4D-03D focused slice 验收：focused 84/84、adjacent 257/257、backend full 3796/3796 通过；该切片只证明代表性 Vi / Xerath `ACTIVATE_ABILITY` payment resource window 已接入 prompt quote / command commit / audit 口径，不关闭 P0-005。
- 已补 4D-03E 实现前基线：focused hide-card payment baseline 84/84、adjacent HideCard / Standby / RevealCard / ActionPrompt / GameHub regression 286/286 通过；该基线只证明既有待命暗置与相邻 prompt / Hub 路径绿色，不关闭 P0-005。
- 已补 4D-03E focused slice 验收：focused 88/88、adjacent 290/290、backend full 3800/3800 通过；该切片只证明代表性 `HIDE_CARD` standby payment window 已接入 shared plan / commit / audit 口径，不关闭 P0-005。
- 已补 4D-03F 实现前基线：focused pending PAY_COST resource baseline 51/51、adjacent PaymentEngine / TriggerPayment / PAY_COST / ActionPrompt / GameHub regression 229/229 通过；该基线只证明既有 ordinary pending `PAY_COST` 与相邻 prompt / Hub 路径绿色，不关闭 P0-005。
- 已补 4D-03F focused slice 验收：focused 55/55、adjacent 233/233、backend full 3804/3804 通过；该切片只证明代表性 ordinary pending `PAY_COST` payment resource window 已接入 prompt quote / command commit / audit 口径，不关闭 P0-005。
- 已补 4D-03G 实现前基线：focused battlefield held resource baseline 21/21、adjacent BattlefieldHeld / PaymentEngine / ActionPrompt / GameHub regression 219/219 通过；该基线只证明当前据守得分支付与相邻路径绿色，不关闭 P0-005。
- 已补 4D-03G focused slice 验收：focused 22/22、adjacent 224/224、backend full 3809/3809 通过；该切片只证明代表性 battlefield held score payment resource window 已接入 command commit / audit 口径，不关闭 P0-005。
- 已补 4D-03H 实现前基线：focused trigger resource baseline 55/55、adjacent TriggerPayment / PAY_COST / PaymentEngine / ActionPrompt / GameHub regression 233/233 通过；该基线只证明当前 trigger / PAY_COST 相邻路径绿色，不关闭 P0-005。
- 已补 4D-03H focused slice 验收：focused 69/69、adjacent 242/242、backend full 3818/3818 通过；该切片只证明代表性 SFD Fiora trigger payment resource action 已接入 `TRIGGER_PAYMENT` / `PAY_COST` / audit 口径，不关闭 P0-005。
- 已补 4D-03I 实现前基线：focused Malzahar / ActivateAbility / PaymentEngine / ResourceSkill baseline 83/83、adjacent ActivateAbility / PaymentEngine / ActionPrompt / GameHub / PaymentResource / SpendPower / RunePool / SpellDuel / Priority regression 312/312 通过；该基线只证明当前 Malzahar ordinary play 与相邻路径绿色，不关闭 P0-005。
- 已补 4D-03I focused slice 验收：focused 105/105、adjacent 317/317、backend full 3840/3840 通过；该切片只证明代表性 Malzahar open-main `[A A]` resource skill 已接入 prompt / command / audit 口径，不关闭 P0-005。
- 已补 4D-03J focused slice 验收：focused 116/116、adjacent 340/340、backend full 3847/3847 通过；该切片只证明代表性 Malzahar spell-duel focus、immediate no-stack resource skill 与 temporary payment-only ledger 已接入服务端口径，不关闭 P0-005。
- 已补 4D-03K focused slice 验收：focused 344/344、adjacent 539/539、backend full 3860/3860 通过；该切片只证明 temporary payment-only resource 已接入 `PLAY_CARD` / `ACTIVATE_ABILITY` / `ASSEMBLE_EQUIPMENT` inline representatives，不关闭 P0-005。
- 已补 4D-03L 实现前基线：focused Dragon Soul Sage / Malzahar / ActivateAbility / PaymentEngine baseline 126/126、adjacent DragonSoulSage / ActivateAbility / Reaction / SpellDuel / PaymentResource / ActionPrompt / GameHub baseline 374/374 通过；该基线只锁定 Dragon Soul Sage reaction resource skill 下一实现切片，不关闭 P0-005。
- 已补 4D-03L focused slice 验收：focused 140/140、adjacent 388/388、backend full 3874/3874 通过；该切片只证明 `UNL-093/219` Dragon Soul Sage reaction resource skill representative 已接入服务端 prompt / command / audit 口径，不关闭 P0-005。
- 已补 4D-03M 实现前基线：focused Renata / ActivateAbility / ResourceSkill / PaymentEngine baseline 144/144、adjacent Renata / ActivateAbility / ResourceSkill / PaymentResource / SpendPower / RunePool / ActionPrompt / GameHub baseline 316/316 通过；该基线只锁定 Renata Glasc colored activated draw 下一实现切片，不关闭 P0-005。
- 已补 4D-03M focused slice 验收：focused 164/164、adjacent 335/335、backend full 3893/3893、`git diff --check` 通过；该切片只证明 `SFD·088/221` / `SFD·088a/221` Renata Glasc colored activated draw representative 已接入 prompt / command / stack resolution / audit 口径，不关闭 Renata score、target-bearing activated abilities 或完整 P0-005。
- 已补 4D-03N 实现前基线：focused Renata / ActivateAbility / ResourceSkill / PaymentEngine baseline 163/163、adjacent Renata / ActivateAbility / ResourceSkill / PaymentResource / SpendPower / RunePool / ActionPrompt / GameHub baseline 335/335 通过；该基线已被 focused slice 验收 supersede，仍保留为回归护栏。
- 已补 4D-03N focused slice 验收：focused 185/185、adjacent 369/369、backend full 3914/3914、`git diff --check` 通过；该切片只证明 `SFD·088/221` / `SFD·088a/221` Renata Glasc colored activated score representative 已接入 prompt / command / stack resolution / audit 口径，不关闭 target-bearing activated abilities、完整 resource skill family 或 P0-005。
- 已补 4D-03O 实现前基线：focused Crimson / Scarlet / ActivateAbility / ResourceSkill / PaymentEngine baseline 143/143、adjacent Crimson / Scarlet / ActivateAbility / ResourceSkill / PaymentResource / SpendPower / RunePool / ActionPrompt / GameHub / Experience / Spellshield baseline 370/370 通过；该基线已被 focused slice 验收 supersede，仍保留为回归护栏。
- 已补 4D-03O focused slice 验收：focused 169/169、adjacent 396/396、backend full 3940/3940、`git diff --check` 通过；该切片只证明 `UNL-109/219` Crimson Rose ready-unit representative 已接入 prompt / command / stack resolution / audit 口径，不关闭 Crimson Rose 第一行触发、Fluft 或 P0-005。
- 已补 4D-03P focused slice 验收：focused 189/189、adjacent 685/685、backend full 3962/3962、`git diff --check` 通过；该切片只证明 `UNL-160/219` Fluft Poro Warhawk token representative 已接入 prompt / command / stack resolution / audit 口径，不关闭完整 resource skill family、token-play breadth 或 P0-005。
- 已补 4D-03Q 实现前 handoff / baseline：focused 198/198、adjacent 738/738 通过；该基线只锁定 `UNL-194/219` Shadow swift stun 下一实现切片，不代表功能完成，不关闭 P0-004/P0-005。
- 已补 4D-03Q focused slice 验收：focused 239/239、adjacent 779/779、backend full 4003/4003、`git diff --check` 通过；该切片只证明 `UNL-194/219` Shadow swift stun representative 已接入 prompt / command / stack resolution / audit 口径，不关闭完整 battle lifecycle、完整 activated ability family 或 P0-004/P0-005。
- 已补 4D-03R 实现前 handoff / baseline：focused 173/173、adjacent 421/421 通过；该基线已被 focused slice 验收 supersede，仍保留为回归护栏。
- 已补 4D-03R focused slice 验收：focused 191/191、adjacent 439/439、backend full 4021/4021、`git diff --check` 通过；该切片只证明 `SFD·222/221` Rage Sigil typed red payment-only resource representative 已接入 prompt / command / temporary ledger / audit 口径，不关闭完整 Sigil family、完整 resource skill family 或 P0-005。
- 已补 4D-03S focused slice 验收：focused 213/213、adjacent 461/461、backend full 4043/4043、`git diff --check` 通过；该切片只证明剩余五张 SFD Sigil typed resource skills，并明确排除 OGN Sigil resource skills，不关闭 P0-005。
- 已补 4D-03T 实现前 handoff / baseline：focused 213/213、adjacent 461/461 通过；下一切片锁定 OGN 六张 Sigil typed resource skills，不关闭 P0-005。
- 已补 4D-03T focused slice 验收：focused 238/238、adjacent 486/486、backend full 4068/4068、`git diff --check` 通过；该切片只证明 OGN 六张 Sigil typed resource skills，并将 SFD + OGN Sigil family representative 收口，不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005。
- 已补 4D-03U focused slice 验收：focused 230/230、adjacent 485/485、backend full 4089/4089、`git diff --check` 通过；该切片只证明能量通道、远古簇碑、海克斯异常体三张 resource conversion equipment skills，不关闭完整 `[A]` / `[C]` resource skill family 或 P0-005。
- 已补 4D-03V focused slice 验收：focused 288/288、adjacent 782/782、backend full 4113/4113、`git diff --check` 通过；该切片只证明 `UNL·T05` / `SFD·T03` Gold token resource ability representative，当时不实现 Renata Gold extra mana bonus；该 bonus 已由 4D-03W supersede。
- 已补 4D-03W focused slice 验收：focused 320/320、adjacent 965/965、backend full 4120/4120、`git diff --check` 通过；该切片只证明 `RENATA_GOLD_EXTRA_1_MANA` marker Gold activation +1 mana representative，不关闭 equipment-token full rules、完整 `[A]` / `[C]` resource skill family 或 P0-005。
- 待补：完整 `[A]` / `[C]` 资源技能、target-bearing colored-cost activated abilities、单阵营/多阵营费用、Haste 的特殊/替代/加减费分支、Spellshield 加税的全支付窗口推广、Echo 费用、完整 trigger payment resource family。

## P1 问题

### P1-001 连续效果、层、时间戳和依赖模型不足

当前状态：**PARTIALLY RESOLVED / 服务端持续效果层视图已落地，完整 LayerEngine 仍待实现**

规则依据：自查文档 14；核心规则关于连续效果、层、时间戳、依赖和装备/静态效果的重算。

代码位置：
- `src/Riftbound.Engine/CoreRuleEngine.cs:36304` 的 `ApplyPowerModifier` 仍直接修改 `CardObjectState.Power`，并用 `UntilEndOfTurnPowerModifier` 聚合记录；4D-04L-B 在该路径追加 `PowerModifierLedgerEntry` source/effect metadata，4D-04M-B 又补上 requested/applied/minimum/resulting metadata。
- `src/Riftbound.Engine/CoreRuleEngine.cs:36355` 的 `ApplyDirectUntilEndPowerModifier` 为 selected direct until-end power mutation paths 追加同一 ledger metadata，同时保持现有 current-power arithmetic。
- `src/Riftbound.Engine/CoreRuleEngine.cs:1819` `ResolveIcevaleArcherAttackTriggerPayment`、`src/Riftbound.Engine/CoreRuleEngine.cs:13081` `ResolveEmberMonkStandbyHiddenPowerTrigger`、`src/Riftbound.Engine/CoreRuleEngine.cs:25307` `ResolveRengarUnitPlayedPowerTrigger` 与 `src/Riftbound.Engine/CoreRuleEngine.cs:34028` `ResolveViDoublePowerAbilityStackItem` 已接入 direct power ledger helper。
- `src/Riftbound.Engine/CoreRuleEngine.cs:20261` `UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN`、`src/Riftbound.Engine/CoreRuleEngine.cs:26428` `ApplyBattlefieldMovedUnitPowerPlusOne` 与 `src/Riftbound.Engine/CoreRuleEngine.cs:29494` `TryResolveSourceUnitOptionalReadyPower` 也已接入 selected direct power ledger helper。
- `src/Riftbound.Engine/CoreRuleEngine.cs:39637` 在回合结束时从 `Power` 中扣回聚合值，并同步清空 `UntilEndOfTurnPowerModifiers`。
- `src/Riftbound.Engine/MatchSession.cs:324` / `src/Riftbound.Engine/MatchSession.cs:340` 新增 `ContinuousEffectState` metadata 字段与 `PowerModifierLedgerEntry`。
- `src/Riftbound.Engine/MatchSession.cs:343` / `src/Riftbound.Engine/MatchSession.cs:408` 现在让 `ContinuousEffectState` 与 `PowerModifierLedgerEntry` 暴露 nullable `AppliedOrder`；`src/Riftbound.Engine/MatchSession.cs:516` `NormalizePowerModifierLedger` 在 entries 有 order 时按 `AppliedOrder` 排序，避免 ordered view 被 `EffectId` 字典序改写。
- `src/Riftbound.Engine/MatchSession.cs:1681` 会把 ledger-backed `UntilEndOfTurnPowerModifiers` 投影为对象级 `POWER_MODIFIER` continuous effects，并为 legacy untracked modifier 保留 remainder fallback。
- `src/Riftbound.Engine/MatchSession.cs:1798` 最终 continuous effect view 在同一 scope / target / layer 下优先按 `AppliedOrder` 排序，再按 `EffectId` fallback。
- `src/Riftbound.Engine/MatchSession.cs:2820` 的 snapshot view 会暴露 `effectKind`、`sourceCardNo`、`sourcePath`、`layerEngineStatus`、`appliedOrder` 与 `deferredLayerEngineResiduals`。
- 4D-04P-B 已补同目标 sequence representative：Smoke Bomb 先把同一目标 floor 到 1，Extortion 在 floor 上 applied delta 为 0，Power Bind 后续对同目标 +1；state ledger、continuous effect view 与 snapshot view 均断言 requested/applied/minimum/resulting/base/effective/order metadata 一致。
- 4D-04Q-B 已新增 `STATIC_AURA` foundation view；`ContinuousEffectState` 与 snapshot view 可暴露 `condition`、`lifecycle`、`participantObjectIds`，并派生 Ornn friendly-equipment static aura 与 battlefield all-units +1 static aura source lifecycle metadata。

现象：临时战力修正仍通过修改对象当前数值实现，但服务端现在能派生出对象级 `POWER_MODIFIER` 层、`STATIC_AURA` foundation 层、全局/对象级 `RULE_TEXT` 层以及 `basePower` / `effectivePower` 视图，供 UI、日志和后续 LayerEngine 使用。4D-04H 已补 Ornn 入场时按己方公开场上装备数量增加战力的 representative；4D-04I-B 进一步补上 Ornn 已在公开 field 后，友方公开 field equipment count 变化时从 registered base power + 当前装备数做窄重算；4D-04L-B 为 `ApplyPowerModifier` representative path 增加 source/effect-aware ledger 与 `FOUNDATION_ONLY` snapshot metadata；4D-04M-B 补上 minimum-power requested/applied/minimum/resulting metadata；4D-04N-B 又让 Icevale Archer、Ember Monk、Rengar、Vi、conquest +8、battlefield moved +1、optional ready power 等 selected direct mutation paths 追加 ledger metadata。4D-04O-B 进一步补上 explicit application order metadata，后续 LayerEngine 不需要通过解析 id 或 projection order 推断同目标同层 ledger-backed power modifier 的 append sequence。4D-04P-B 已用 test-only representative 证明同目标 minimum floor + zero-applied floor + later visible modifier 的 metadata/order 组合语义。4D-04Q-B 已为 Ornn dynamic static recompute 与 battlefield all-units +1 representative 增加 source / target / participant / condition / lifecycle metadata，并证明 source/condition 失效后不留 stale view。但当前仍不是完整层系统；完整的“基础值 + 连续效果层 + 时间戳 + 来源 + 依赖”重算模型仍未替换所有战力/关键词/装备静态效果路径；多个装备静态光环、其他装备静态修正、复杂控制权变化、失去/获得关键词时仍可能与官方层规则不一致。

建议修复：引入 `ContinuousEffect`、`LayerEngine`、`Timestamp`、`SourceObjectId`，计算视图时重算派生属性，避免永久修改基础属性。

建议测试：已新增 `MatchStateExposesContinuousEffectPowerLayerViews` 覆盖基础/有效战力、对象级 power modifier、对象/全局 until-end 效果 snapshot；4D-04L-B 已扩展 `SwitcherooSwapsTwoPublicBattlefieldUnitPowersUntilEndOfTurn`，验证 state 和 snapshot 中的 source/effect metadata 与 deferred residuals；4D-04M-B 已覆盖 minimum-power requested/applied/minimum/resulting metadata；4D-04N-B 已扩展 Icevale Archer 与 Rengar representatives，证明 direct power mutation 的 state / snapshot metadata，并验证 Rengar end-turn ledger cleanup；4D-04O-B 已新增 Power Bind Echo 同目标多 modifier order、reversed `EffectId` shape、legacy untracked no-order、Rengar/Icevale/Switcheroo/minimum-power order metadata assertions。4D-04P-B 已新增 `PowerModifierMinimumPowerAppliedOrderSkipsZeroFloorSequence`，并补强 `CoreRuleEngineExpiresSmokeBombPowerFloorAtEndTurn` cleanup assertions。4D-04Q-B 已扩展 Ornn static tests 与 `P79BattlefieldStaticPowerAddsOneToBattleParticipants`，覆盖 static aura source/target/participants/condition/lifecycle metadata、snapshot projection 与 source/condition stale cleanup；A 侧 focused static-aura / LayerEngine-view guard 11/11、adjacent static / continuous-effect / equipment regression 49/49、backend full 4451/4451 通过。待补：多个装备/光环叠加、控制权变化、失去来源、同层时间戳、全部 direct/static/replacement breadth、回合结束、最小战力完整排序 beyond current representatives。

### P1-002 关键词覆盖仍存在“识别但 deferred”的内部事实

当前状态：**PARTIALLY RESOLVED / deferred 关键词覆盖已变成服务端/API 显式报告，完整执行仍待逐族补齐**

规则依据：自查文档 15；关键词必须不仅被识别，还要能按官方时点、费用、目标、区域和替代/触发规则执行。

代码位置：
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 明确装备关键词可同时存在 representative boundary 与 `RecognizedDeferred` closure。
- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs` 现在登记 assemble / Take Up / printed Agile direct-play / Tempered optional attach / Jax trigger integration / Akshan steal / Armed Assaulter combination / Ornn friendly-equipment static power 等 representative boundaries；4D-04J / 4D-04K 又把已有 P5 owner/controller / attached-equipment follows host / host destroyed detach-recall 代表路径列为下一批 profile / verifier alignment 输入，但 full official breadth 仍 deferred。
- `src/Riftbound.Engine/CardInteractionKeywordRules.cs:72` 到 `src/Riftbound.Engine/CardInteractionKeywordRules.cs:75` 说明 Standby/Ambush/Echo 仍有宽泛 deferred 分支。
- `src/Riftbound.Engine/CardCombatKeywordRules.cs:55` 到 `src/Riftbound.Engine/CardCombatKeywordRules.cs:60` 说明 combat damage、assignment order、roam movement execution remain deferred。
- `src/Riftbound.Engine/CardResourceKeywordRules.cs:76` 到 `src/Riftbound.Engine/CardResourceKeywordRules.cs:80` 说明狩猎征服/据守战斗经验已有代表路径，但 broader experience/level/encourage/spellshield branches 仍 remain deferred。
- `src/Riftbound.Engine/KeywordCoverageReporter.cs` 会把各关键词族 profile 汇总成 `KeywordCoverageReport`，API `/catalog/keyword-coverage`、`/catalog/summary`、`/catalog/p3-status` 会公开这些 deferred 状态。

现象：这些 profile 与“所有卡牌功能完整实现”存在冲突。即便某些代表性路径已有实现，关键词族整体不能判定完整。4D-04H 把 Ornn friendly-equipment static power 的入场代表路径从泛化 static modifier residual 中剥离出来；4D-04I-B 又补上 Ornn dynamic friendly-equipment static recompute representative。但 full `百炼` breadth、其他装备静态修正、owner/controller breadth 与 attach lifecycle 仍不能判定完整。现在 deferred 状态不再只是测试/内部 profile 里的事实，而是正式服务端报告和 API 输出；前端、图鉴和本地测试入口可以按 family/status 禁用或明确提示。

建议修复：继续把关键词 profile 状态与真实规则执行路径重新对齐；没有完整执行的卡牌/功能单元不能暴露为完全 `CONFORMANCE_PASS`。下一步要按 family 优先级补真实执行路径，而不是只扩展报告。

建议测试：已新增 `KeywordCoverageReportExposesDeferredKeywordFamilies`，覆盖关键词 family 报告、deferred rows 和 implemented/deferred 并存口径。待补：按关键词族建立完整矩阵测试，每个关键词覆盖时点、费用、目标、隐藏信息、替代/触发、失败回滚。

### P1-003 BehaviorSpec/CONFORMANCE_PASS 口径疑似过度乐观

当前状态：**PARTIALLY RESOLVED / 已拆分代表性通过与官方完整通过，后续仍需逐条提升证据**

规则依据：自查文档 16、19、21；BehaviorSpec 需要真实映射到可执行规则，不能用域级占位掩盖未实现细节。

代码位置：
- `src/Riftbound.Contracts/BehaviorSpecs.cs` 新增 `BehaviorConformanceTiers` 和 `BehaviorSpec.ConformanceTier/ConformanceReason`。
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 继续保留 `Status=implemented` 作为已有代表路径/fixture 口径，但 `ConformanceTier` 只标为 `representative-rule-pass`，没有标为 `full-official-rule-pass`。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 不再断言 “CONFORMANCE_PASS = 官方完整”，而是断言 `1009/1009 representative-rule-pass` 与 `0 full-official-rule-pass`。
- 但 `docs/CURRENT_P6_STATUS.md:894` 到 `docs/CURRENT_P6_STATUS.md:901` 仍记录 P6 final 为 `713/811` implemented、`98/811` manual deferred；`src/Riftbound.Engine/P6LegendAbilityCatalog.cs:13`、`src/Riftbound.Engine/P6BattlefieldEffectCatalog.cs:20` 也保留 deferred surfaces。

现象：P7.9 文档与旧测试曾把目录推进到全 implemented，但底层规则模型仍存在 P0/P1 缺口。现在 API/UI 可以展示“代表性通过”，并明确当前不是官方完整规则闭环；后续仍需要逐卡把代表性证据提升为完整规则证据。

建议修复：
- 继续将非 PLAY_CARD 域逐条映射到真实命令、状态机和测试；只有对应 P0/P1 规则域完整后才允许提升到 `full-official-rule-pass`。
- 逐步让 UI playable filter 从旧 `status=implemented` 迁移到“服务端 prompt candidate + conformance tier + deferred surface”三重门槛。

建议测试：
- 已新增：每个 BehaviorSpec 必须有 `ConformanceTier/ConformanceReason`；当前 `full-official-rule-pass` 必须为 0。
- 待补：每个 BehaviorSpec 的 `ImplementedEffectKind` 必须能追溯到具体 resolver、prompt candidate、fixture 和官方文本锚点。
- 对 legend/battlefield/token/rune 域建立负例，防止占位 domain 把未实现能力标为 pass。

### P1-004 隐藏信息与 replay 边界仍需加固

当前状态：**PARTIALLY RESOLVED / 普通 snapshot、spectator replay redaction、权威状态 hash、recovery tick 一致性 guard、给定初始状态的 action-log final hash verifier、registry 恢复前审计、Postgres 集成 smoke、recovery spectator replay frame 与代表性 redaction property tests 已修，全路径 determinism/property 仍待补**

规则依据：自查文档 2、18；客户端不得得到能预测未来随机信息的私密状态；replay/观战要区分公开信息与玩家私有视角。

代码位置：
- `src/Riftbound.Engine/MatchSession.cs` 的普通玩家 snapshot 已移除 `seed` 和 `rngCursor`，并新增 `BuildSpectatorSnapshot` 统一观战视角裁剪。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchReplayRedactor.BuildSpectatorFrame`，从权威 journal entry 生成观战/回放 frame 时不复用任一玩家私有视角。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchStateHasher`，`MatchReplayFrame.AuthoritativeStateHash` 会携带 canonical SHA-256 状态 hash，供 replay frame 与实时权威状态对账。
- `src/Riftbound.Engine/MatchRecovery.cs` 的 `MatchRecoveryValidator` 会在调用方提供 recovery current tick 时校验 authoritative state tick；`src/Riftbound.Persistence/PostgresMatchRecoveryStore.cs` 会传入 `matches.current_tick`，避免恢复时静默掩盖 tick 不一致。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchActionLogReplayer.VerifyFinalStateAsync`，可从调用方提供的初始权威状态重放 recovered commands，并比对 final state hash。
- `src/Riftbound.Engine/MatchRecovery.cs` 新增 `MatchReplayInitialStateBuilder.FromSeats` 与 `MatchActionLogReplayer.ValidateRecoveryFrameAsync`；`src/Riftbound.Persistence/PostgresMatchRecoveryStore.cs` 会从 `match_players` 构造 `ReplayInitialState`，`src/Riftbound.Engine/MatchSession.cs` 的 registry 在 `Restore` 前执行 replay final hash audit。
- `src/Riftbound.Engine/MatchRecovery.cs` 的 `MatchRecoveryFrame.SpectatorReplayFrame` 会携带 recovery 读取路径生成的公开回放帧；`src/Riftbound.Persistence/PostgresMatchRecoveryStore.cs` 会基于最终 authoritative state 与 recovered events 生成该帧。
- `src/Riftbound.Engine/MatchSession.cs` 的 `MatchState` JSON 构造参数已与 `Seed` / `RngCursor` 属性类型对齐，避免 `state_snapshots.payload` 无法反序列化；`tests/Riftbound.ConformanceTests/PostgresMatchRecoveryStoreSmokeTests.cs` 已覆盖真实 PostgreSQL journal/recovery/registry 恢复路径。
- `src/Riftbound.Engine/MatchSession.cs` 的 `RestoreState` 仍优先恢复 authoritative state；当前已经有恢复前 hash audit 钩子、Postgres smoke 和恢复路径 spectator replay frame。

现象：目前 opponent hand/face-down redaction 做得不错，普通玩家 snapshot 也已不再包含 `seed`/`rngCursor`；观战/回放 frame 现在也会从 authoritative state 重新生成 spectator snapshot，而不是直接拿玩家 snapshot，并携带稳定的权威状态 hash 用于最终状态对账。恢复路径也会拒绝 authoritative state tick 与 match metadata current tick 不一致的持久化帧。服务端现在具备“给定初始状态 + recovered commands -> final state hash”的可测 verifier，并且生产 registry 在恢复 Postgres recovery frame 前会执行该审计。真实 Postgres smoke 已证明 command log / state snapshot / match_players 可恢复并通过 final hash audit，且 recovery frame 会带公开 spectator replay frame。代表性 property tests 已覆盖多组手牌/面朝下/随机状态裁剪；剩余风险是全命令、全恢复场景、全随机路径的 determinism/property 证明仍不足。

建议修复：
- 已完成：从普通玩家 snapshot 中移除 seed/rngCursor；如后续需要调试随机状态，应单独走 Development/debug stream，不能复用普通玩家 snapshot。
- 已完成：从 journal entry 构建 spectator replay frame 时强制使用 spectator redaction。
- 已完成：replay frame 携带 canonical authoritative state hash，用于最终状态一致性校验。
- 已完成：recovery store 传入 current tick 后，validator 会拒绝 authoritative state tick 不一致的恢复帧。
- 已完成：建立给定初始权威状态的 action-log verifier，重放命令后校验 final authoritative state hash。
- 已完成：Postgres recovery frame 从 `match_players` 构造 replay 初始状态，registry 在恢复前强制执行 action-log final hash audit。
- 已完成：补真实 Postgres store 集成 smoke，并修复 `MatchState` authoritative snapshot 反序列化构造参数类型不匹配。
- 已完成：恢复读取路径输出 spectator replay frame，且该 frame 使用 spectator redaction 与 authoritative state hash。
- 已完成：补代表性随机/隐藏信息 redaction property tests。
- 待补：扩大到全命令、全恢复场景、全随机路径的 determinism/property tests。

建议测试：
- 已新增：玩家 snapshot 不含 seed/rngCursor。
- 已新增：spectator replay 不泄露手牌、面朝下内容和未来随机 seed/rngCursor，并携带 64 位 hex authoritative state hash。
- 已新增：authoritative state hash 对字典插入顺序稳定。
- 已新增：authoritative state tick 与 recovery current tick 不一致时 recovery validator 返回明确错误。
- 已新增：给定初始状态时，recovered command log 重放后的 final state hash 等于实时 journal authoritative state hash；篡改 expected final state 时会报告 hash mismatch。
- 已新增：registry 在恢复前执行 action-log replay audit；hash mismatch 会阻止恢复并返回 `RECOVERY_INCONSISTENT`。
- 已新增：真实 Postgres store 集成 smoke 覆盖从持久化 command log / state snapshot / match_players 自动恢复并通过 final state hash audit。
- 已新增：真实 Postgres store smoke 覆盖 recovery spectator replay frame 输出、state hash 和 seed/rngCursor 裁剪。
- 已新增：生成式 spectator replay redaction property test 覆盖多组隐藏手牌、面朝下对象和随机状态组合。
- 待补：全命令、全恢复场景、全随机路径 determinism/property tests。

## P2 问题

### P2-001 测试通过不等于官方规则完整

当前 fixture 和 baseline 测试数量很大，但自查文档最终门槛要求的是官方规则闭环。现有测试更多证明“当前代码声明支持的路径能稳定运行”，不足以证明“官方规则所有路径都已覆盖”。

建议补充：
- 官方开局/构筑专项测试。
- cleanup/task queue 集成测试。
- FEPR/法术对决/战斗状态机集成测试。
- 关键字矩阵测试。
- FAQ/勘误回归测试。
- 随机/隐藏信息/replay property tests。

### P2-002 文档状态需要重新分层

当前状态：**RESOLVED FOR THIS AUDIT / 当前报告已按产品、fixture、官方完整规则三层拆分口径**

`docs/CURRENT_P7_9_STATUS.md:76` 到 `docs/CURRENT_P7_9_STATUS.md:78` 记录 legend/battlefield 已 0 remaining，`docs/CURRENT_P7_9_STATUS.md:153` 到 `docs/CURRENT_P7_9_STATUS.md:158` 记录 P7.9 全完成；但本次核心规则自查显示这些结论只适合描述当前产品/fixture 口径，不能直接等同“官方完整核心规则 READY”。

当前口径说明：
- `Product UI playable smoke`: 当前 P7 页面可用性。
- `Conformance fixture pass`: 当前 fixture 全绿。
- `Official full rules ready`: 本报告结论为 NOT READY。

## 模块矩阵

| 模块 | 结论 | 说明 |
| --- | --- | --- |
| 服务端权威/幂等/非法命令不落状态 | PASS | `MatchSession.SubmitAsync` 串行且仅 accepted 更新状态。 |
| 双人房间/重连/snapshot/prompt | PASS | `GameHub` 具备 join/reconnect/request snapshot/submit flow。 |
| 隐藏手牌/面朝下对象 | PASS | 手牌、face-down、随机 seed/rngCursor 与 spectator replay frame 均已裁剪。 |
| 官方 deck/opening/mulligan | RISKY | 已有 `SUBMIT_DECK -> READY -> MULLIGAN` 正式路径、deck validator、负例矩阵、起手调度边界和 Hub smoke；剩余风险主要是产品前端仍需强制正式入口。 |
| 区域/对象/控制权/战场位置 | RISKY | 已有 `ObjectLocations`、`BattlefieldStates`、`BattlefieldTasks` 与 4D-01 destination-scoped contest task foundation；仍缺完整 battlefield/standby/control/held/conquer task 状态机。 |
| FEPR/优先权/焦点 | RISKY | 有 `TurnWindowState`、`SpellDuelState`、`BattlefieldTasks`、focus/prompt；仍缺完整 pending task/state machine。 |
| 栈/出牌/费用/目标 | RISKY | 大量代表路径实现；PLAY_CARD 与代表性非出牌路径已 typed-power aware，但通用支付来源/目标语法仍不足。 |
| 通用清理检查 | RISKY | 已有 `PendingCleanupTasks`、`PendingTaskQueue`、cleanup-first blocking、cleanup repeat 与 reconnect redaction foundation；仍缺覆盖全部状态变化/替代效果/战斗清理的统一任务队列。 |
| 移动/战场控制 | RISKY | 精确移动可写回 object location，战场间 roam 保留 mixed-case destination 并只为目的地排 contest tasks；完整控制权改变/征服/据守仍待状态机化。 |
| 法术对决 | RISKY | 已有显式 `SpellDuelState`、`BattlefieldTasks` 与焦点恢复；仍缺完整 spell duel lifecycle。 |
| 战斗 | RISKY | 已有显式 `BattleState` 参与者视图、`START_BATTLE` 任务视图和 `DECLARE_BATTLE.sourceRequirements`；4D-02B 到 4D-02H 已覆盖最小 response、assignment、task advancement、no-result、Icevale Archer declaration context 与 Brush replacement context representative，但完整官方 battle task 未关闭。 |
| 计分/胜负 | RISKY | 有部分得分/胜负实现；依赖战场控制与 cleanup 的完整性不足。 |
| 连续效果层 | RISKY | 已有 `ContinuousEffectState`、`basePower`、`effectivePower` 视图；仍缺完整 LayerEngine/timestamp/dependency 重算。 |
| 关键词 | RISKY | 已有 `KeywordCoverageReporter` 暴露 implemented/delegated/deferred 边界；多个关键词族仍需真实执行矩阵。 |
| 全卡牌效果 | RISKY | BehaviorSpec 已降义为 representative-rule-pass 且 full-official-rule-pass=0；仍需逐卡提升证据。 |
| 日志/replay/观战 | RISKY | 有 journal/recovery、普通 snapshot 随机裁剪、spectator replay redaction、authoritative state hash、给定初始状态的 action-log final hash verifier、恢复前审计与 Postgres smoke；历史“仍缺严格 action-log replay final-state 校验”口径已 superseded，剩余风险是全命令、全恢复、全随机路径 determinism/property 覆盖不足。 |

## 建议下一步开发顺序

1. 已完成：冻结并重命名当前 `CONFORMANCE_PASS` 口径，避免把 fixture/domain pass 当成 full official rules pass。
2. 已完成第一批：官方 deck/opening/mulligan、Hub smoke、legacy ready 生产边界。
3. 已完成第一批：board object location、battlefield state、battlefield task、pending cleanup/turn window/spell duel/battle/continuous effect/keyword coverage/spectator replay 的服务端显式视图。
4. 已完成 4D-01 foundation：board task queue focused checklist、cleanup-first blocking、destination-scoped contest tasks、reconnect redaction。
5. 下一步：补 complete standby/control/conquer/hold full-official lifecycle 与所有状态变化统一 cleanup queue 的残余边界。
6. 已完成 4D-03 focused foundation：shared payment plan / authorize / commit helper、PLAY_CARD / PAY_COST / ASSEMBLE_EQUIPMENT 代表接入、payment resource rollback 与 plan audit payload。
7. 已完成 4D-03B focused slice：Vi / Xerath `ACTIVATE_ABILITY`、`LEGEND_ACT` 与 battlefield held score 等 non-play payment windows 接入 shared plan / commit。
8. 已完成 4D-03C focused slice：`PLAY_CARD` Haste / Echo / Spellshield / experience / payment-resource 代表路径接入 shared plan audit / authorize 口径。
9. 已完成 4D-03D focused slice：Vi / Xerath `ACTIVATE_ABILITY` payment resource action 接入 prompt quote / command commit / audit 口径。
10. 已完成 4D-03E focused slice：`HIDE_CARD` standby payment window 接入 shared `PaymentPlan` / authorize / commit / audit 口径。
11. 已完成 4D-03F focused slice：ordinary pending `PAY_COST` payment resource action 接入 shared plan / prompt quote / command commit / audit 口径。
12. 已完成 4D-03G focused slice：battlefield held score payment resource action 接入 shared plan / command commit / audit 口径。
13. 已完成 4D-03H focused slice：SFD Fiora trigger payment resource action 接入 shared plan / command commit / audit 口径。
14. 已验收 4D-03I focused slice：`OGN·113/298` Malzahar open-main `[A A]` resource skill focused 105/105、adjacent 317/317、backend full 3840/3840 通过。
15. 已验收 4D-03J focused slice：Malzahar spell-duel focus lifecycle 与 temporary payment-only ledger focused 116/116、adjacent 340/340、backend full 3847/3847 通过。
16. 已验收 4D-03K focused slice：temporary payment-only resource inline representatives focused 344/344、adjacent 539/539、backend full 3860/3860 通过。
17. 已验收 4D-03L focused slice：Dragon Soul Sage reaction resource skill focused 140/140、adjacent 388/388、backend full 3874/3874 通过。
18. 已验收 4D-03M focused slice：Renata Glasc colored activated draw focused 164/164、adjacent 335/335、backend full 3893/3893 通过。
19. 已验收 4D-03N focused slice：Renata Glasc colored activated score focused 185/185、adjacent 369/369、backend full 3914/3914 通过。
20. 已验收 4D-03O focused slice：Crimson Rose ready-unit focused 169/169、adjacent 396/396、backend full 3940/3940 通过。
21. 已验收 4D-03P focused slice：Fluft Poro Warhawk token focused 189/189、adjacent 685/685、backend full 3962/3962 通过。
22. 已验收 4D-03Q focused slice：Shadow swift stun focused 239/239、adjacent 779/779、backend full 4003/4003 通过。
23. 已建立 4D-03R handoff / baseline：Rage Sigil typed resource focused baseline 173/173、adjacent baseline 421/421 通过，已被 focused slice 验收 supersede。
24. 已验收 4D-03R focused slice：Rage Sigil typed resource focused 191/191、adjacent 439/439、backend full 4021/4021 通过。
25. 已验收 4D-03S focused slice：SFD Sigil typed resource family focused 213/213、adjacent 461/461、backend full 4043/4043 通过。
26. 已建立 4D-03T handoff / baseline：OGN Sigil typed resource family focused baseline 213/213、adjacent baseline 461/461 通过，已被 focused slice 验收 supersede。
27. 已验收 4D-03T focused slice：OGN Sigil typed resource family focused 238/238、adjacent 486/486、backend full 4068/4068 通过。
28. 已验收 4D-03U focused slice：resource conversion equipment focused 230/230、adjacent 485/485、backend full 4089/4089 通过。
29. 已验收 4D-03V focused slice：Gold token resource skill focused 288/288、adjacent 782/782、backend full 4113/4113 通过。
30. 已验收 4D-03W focused slice：Renata Gold bonus focused 320/320、adjacent 965/965、backend full 4120/4120 通过。
31. 已验收 4D-03AC focused slice：battlefield held temporary payment resource parity focused 221/221、backend full 4158/4158 通过。
32. 已验收 4D-03AD focused slice：trigger temporary payment resource parity focused 149/149、backend full 4170/4170 通过。
33. 已验收 4D-03AE focused slice：pending temporary resource aggregate prompt parity focused 170/170、backend full 4173/4173 通过。
34. 已建立 4D-03AF A-side remaining-scope audit：focused baseline 587/587 通过，确认 P0-005 仍不能关闭。
35. 已验收 4D-03AG focused slice：`PLAY_CARD` typed resource prompt parity focused 454/454、backend full 4177/4177 通过。
36. 已验收 4D-03AH focused audit：PaymentEngine action-window coverage verifier focused 717/717、backend full 4182/4182 通过。
37. 已验收 4D-03AJ focused slice：Renata typed temporary resource prompt / command / audit parity focused 85/85、adjacent 687/687、backend full 4239/4239 通过。
38. 已验收 4D-03AK focused audit：Spellshield tax coverage verifier focused 8/8、adjacent 382/382、backend full 4242/4242 通过。
39. 已验收 4D-03AL focused audit：resource skill coverage verifier focused 11/11、adjacent 452/452、backend full 4245/4245 通过。
40. 已建立 4D-03AU A-side PaymentEngine residual official scope handoff / baseline：focused PaymentEngine coverage guard 14/14、adjacent payment / prompt / hub / keyword regression 572/572、backend full 4451/4451 通过；本批只路由下一建议 4D-03AV residual blocker manifest / quote-parity verifier，不改 runtime/tests/frontend/matrix，不派发 B，不关闭 P0-005。
41. 已验收 4D-03AV / 4D-03AW / 4D-03AX focused verifiers：PaymentEngine residual blocker manifest focused 18/18、adjacent payment / prompt / hub / keyword regression 576/576、backend full 4455/4455 通过；target / colored activated ability manifest focused 22/22、adjacent target / payment / prompt / hub regression 530/530、backend full 4459/4459、`git diff --check` 通过；legend / battlefield / trigger resource action manifest focused 27/27、adjacent LegendAct / BattlefieldHeld / TriggerPayment / PaymentEngine / prompt / hub regression 408/408、backend full 4464/4464 通过；这些 test-only verifiers 固定 residual families、target / colored activated representatives、legend / battlefield / trigger resource-action representatives 与 NOT READY closure，不改 runtime/frontend/matrix，不关闭 P0-005。
42. 已验收 4D-03AY / 4D-03AZ / 4D-03BA / 4D-03BC / 4D-03BD focused verifiers：keyword payment branch manifest focused 32/32、backend full 4469/4469 通过；resource skill residual manifest focused 34/34、backend full 4471/4471 通过；official matrix residual manifest focused 39/39、backend full 4476/4476 通过；official matrix row schema focused 45/45、adjacent PaymentEngine / resource skill / prompt / hub regression 603/603、backend full 4482/4482 通过；coverage doc anchor integrity focused 46/46、adjacent PaymentEngine / resource skill / prompt / hub regression 604/604、backend full 4483/4483 通过。这些 test-only verifiers 只固定 residual / row routing / evidence traceability，不改 runtime/frontend/matrix，不关闭 P0-005。
43. 已验收 4D-03BE / 4D-03BF / 4D-03BG / 4D-03BH / 4D-03BJ / 4D-03BK / 4D-03BL-B / 4D-03BM / 4D-03BN / 4D-03BO-B / 4D-03BP-B / 4D-03BQ-B / 4D-03BR-B focused verifiers，并已建立 4D-03BL rollback failure official matrix handoff / baseline、4D-03BO official matrix downstream aggregate handoff / baseline、4D-03BP keyword branch all-window matrix handoff / baseline、4D-03BQ resource skill all-window matrix handoff / baseline 与 4D-03BR target / tax activated ability matrix handoff / baseline：rollback failure row manifest focused 51/51、cross-window generation / consumption row manifest focused 56/56、card matrix alignment row manifest focused 61/61、missing row downstream coverage focused 64/64、representative seed upstream coverage focused 67/67、policy-deferred MOVE_UNIT boundary focused 70/70、rollback failure all-window matrix focused 75/75、cross-window generation / consumption all-window matrix focused 80/80、card matrix alignment all-window matrix focused 85/85、official matrix downstream aggregate focused 92/92、keyword branch all-window matrix focused 97/97、resource skill all-window matrix focused 102/102、target / tax activated ability matrix focused 107/107；latest 4D-03BR-B adjacent PaymentEngine / resource skill / prompt / hub regression 665/665、backend full 4544/4544、`git diff --check` passed。这些 verifiers / handoff 只固定 official matrix missing-row / representative-seed / policy-deferred routing completeness、rollback failure all-window audit contract、cross-window generated-resource all-window audit contract、card matrix alignment all-window audit contract、downstream aggregate verifier contract、keyword branch all-window matrix contract、resource skill all-window matrix contract 与 target/tax activated ability matrix boundary，不改 runtime/frontend/matrix，不关闭完整 PaymentEngine official matrix、P0-005 或 READY。
44. 已建立 4D-03BS PaymentEngine remaining official scope handoff / baseline：focused PaymentEngine coverage guard 107/107、adjacent PaymentEngine / resource skill / prompt / hub regression 665/665、backend full 4544/4544 通过；本批只把 4D-03BR-B 之后的 P0-005 剩余范围路由为 future B-side PaymentEngine official breadth verifier / implementation slice、future E-side card matrix readiness slice 或 future D-side P0 audit slice，不改 runtime/tests/frontend/matrix，不派发 worker，不关闭 P0-005。
45. 已验收 4D-03BT PaymentEngine remaining official closure gate verifier：focused PaymentEngine closure gate / coverage guard 110/110、adjacent PaymentEngine / resource skill / prompt / hub regression 668/668、backend full 4547/4547 通过；本批只把 4D-03BS 后续 B/E/D fresh-dispatch gates 转成 executable guard，并读取 card matrix skeleton 确认 1009 / 811、0 full-official、freeze ready=false，不改 runtime/frontend/matrix，不关闭 P0-005。
46. 已建立 4D-03BU PaymentEngine resource skill official breadth handoff / baseline：focused PaymentEngine coverage guard 110/110、adjacent PaymentEngine / resource skill / prompt / hub regression 668/668、backend full 4547/4547、`git diff --check` 通过；本批只把 4D-03BT 后的下一枚 concrete future B-side scope 收窄到完整 `[A]` / `[C]` resource skill official breadth verifier / implementation slice，不改 runtime/tests/frontend/matrix，不派发 worker，不关闭 P0-005。
47. 已验收 4D-03BU PaymentEngine resource skill official breadth verifier：focused PaymentEngine coverage guard 115/115、adjacent PaymentEngine / resource skill / prompt / hub regression 673/673、backend full 4552/4552、`git diff --check` 通过；本批把 fixed official catalog 中带 resource-skill reminder text 的 32 个 snapshot entries 锁为 executable reconciliation manifest，区分 19 个 implemented source card nos 与 13 个 deferred official candidates，不改 runtime/frontend/matrix，不关闭 P0-005。
48. 已验收 4D-02G focused slice：Battle response declaration-context focused 429/429、adjacent 608/608、backend full 4197/4197、`git diff --check` 通过；natural `DECLARE_BATTLE` 携带 Icevale Archer battlefield target context 且存在合法 Shadow battle response 时会先开启 response window，双方 pass 后保留原 declaration context 打开 trigger payment，internal carrier 不进入公开 snapshot / prompt。
49. 已验收 4D-02H test-only slice：Battle response Brush replacement context focused 430/430、adjacent 608/608、backend full 4198/4198、`git diff --check` 通过；`BRUSH_USE_REPLACED_BATTLEFIELD:*` optional cost 在 response pass 后进入 replacement-aware held-score path，internal carrier 继续不进入公开 snapshot / prompt。
50. 下一步：4D-03BR-B 已把 target-bearing / typed / experience / Spellshield-tax activated ability representatives 扩成 executable target/tax matrix；4D-03BS / 4D-03BT 已固定下一步 fresh-dispatch boundary 与 executable closure gate；4D-03BU 已把 resource skill official breadth 的 32 / 19 / 13 split 转成 executable verifier。后续仍需继续扩展 typed payment engine 到剩余 target-bearing activated abilities、rune/legend/battlefield/keyword 全路径，支持完整 `[A]` / `[C]` resource skills、替代费用、减费/加费、额外/可选费用。
51. 下一步：引入完整 continuous effect LayerEngine，并逐关键词、逐卡牌把 `Representative/FixturePass` 提升到 `FullOfficialRulePass`。

Stage 4D 主控计划已将以上顺序拆为可执行写锁与验收门槛，见 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`。4D-01 board task queue foundation 已验收；4D-02 battle/spell-duel focused slice、4D-02B battle-response lifecycle、4D-02C natural assignment lifecycle、4D-02D response-assignment integration guard、4D-02E battle task advancement、4D-02F battle assignment no-result、4D-02G battle response declaration-context 与 4D-02H battle response Brush replacement context 已验收但不关闭 full official P0-004；4D-02W focused slice 已验收，证明 Shadow response stack item 上方的 standby reaction 可按 LIFO 解析并返回 Shadow / battle response priority；4D-02X focused slice 已验收，证明同一 battle response window 内两个 ready Shadow response sources 可同时公开并逐个消耗；4D-02Y focused slice 已验收，修复 active battle response 中 Shadow stale target no-effect 后 response close 使用保存 declaration context 的边界，但不关闭完整 battle-response breadth。4D-03 PaymentEngine focused foundation、4D-03B non-play focused slice、4D-03C play optional / extra focused slice、4D-03D activate ability payment resource focused slice、4D-03E hide-card payment focused slice、4D-03F pending PAY_COST resource focused slice、4D-03G battlefield held score resource focused slice、4D-03H trigger payment resource focused slice、4D-03I Malzahar resource skill focused slice、4D-03J Malzahar lifecycle focused slice、4D-03K temporary inline focused slice、4D-03L reaction resource skill focused slice、4D-03M Renata colored activated draw focused slice、4D-03N Renata colored activated score focused slice 与 4D-03O Crimson Rose ready-unit focused slice 已验收但不关闭 full official P0-005；4D-03P Fluft Poro Warhawk token focused slice、4D-03Q Shadow swift stun focused slice、4D-03R Rage Sigil typed resource focused slice、4D-03S SFD Sigil typed resource family focused slice、4D-03T OGN Sigil typed resource family focused slice、4D-03U resource conversion equipment、4D-03V Gold token resource skill、4D-03W Renata Gold bonus、4D-03AC battlefield held temporary resource、4D-03AD trigger temporary resource、4D-03AE pending temporary prompt aggregate、4D-03AG PLAY_CARD typed resource prompt parity、4D-03AJ Renata typed temporary resource focused slice、4D-03AK Spellshield tax coverage verifier、4D-03AL resource skill coverage verifier、4D-03AF remaining-scope audit / 4D-03AH action-window coverage verifier、4D-03AU residual scope handoff / baseline、4D-03AV residual blocker manifest verifier、4D-03AW target / colored activated ability manifest verifier 与 4D-03AX legend / battlefield / trigger resource action manifest verifier 已验收或建立。下一实现应继续完整 `[A]` / `[C]` resource skill family、完整 target-bearing / typed activated ability official breadth、剩余 payment windows 与更多 payment window quote parity。

## 最终验收口径

在完成上述 P0/P1 之前，不建议把服务端声明为“完整符合五个官方规则文件”。推荐当前对外口径为：

> 当前服务端可支撑本地双人对战原型、服务端权威结算、正式 deck/opening/mulligan 入口、代表性规则与大量 conformance fixture；但完整官方核心规则仍处于 NOT READY，自查剩余风险集中在完整战场/待命/控制权任务状态机、通用清理任务队列、法术对决/战斗状态机、全路径官方费用模型、连续效果 LayerEngine 与逐关键词/逐卡牌完整执行。
