# Stage 4D-03BD PaymentEngine Coverage Doc Anchor Integrity Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BC official matrix row schema 后补一层 A 侧审计护栏：PaymentEngine coverage manifests 中的 `DocAnchors` 不能只满足 `docs/*.md` 字符串格式，还必须解析到当前仓库内真实存在的审计 / handoff 文档。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 fullOfficial，不关闭 P0-005 或 READY。

## 2. Test Guard

新增 focused verifier：

- `PaymentEngineCoverageManifestDocAnchorsResolveToCurrentAuditDocs`

该 verifier 聚合以下 manifest 的所有 `DocAnchors`，从测试运行目录向上定位 `Riftbound.slnx` 所在仓库根目录，并要求每个 anchor 对应文件存在：

- action-window coverage manifest
- Spellshield tax activated ability manifest
- target / colored activated ability manifest
- resource skill manifest
- residual blocker manifest
- official matrix residual manifest
- official matrix seed row manifest
- legend / battlefield / trigger resource-action manifest
- keyword payment branch manifest

## 3. Anchor Repairs

本批修复的漂移锚点：

| Old Anchor | Current Anchor |
| --- | --- |
| `docs/CURRENT_STAGE4D_04G_TEMPERED_HASTE_EQUIPMENT_AUDIT.md` | `docs/CURRENT_STAGE4D_04G_ARMED_ASSAULTER_HASTE_TEMPERED_AUDIT.md` |
| `docs/CURRENT_STAGE4D_03AI_PAYMENT_ENGINE_BATTLEFIELD_BRUSH_CONTEXT_AUDIT.md` | `docs/CURRENT_STAGE4D_02H_BATTLE_RESPONSE_BRUSH_REPLACEMENT_CONTEXT_AUDIT.md` |
| `docs/CURRENT_STAGE4D_04P_STATIC_AURA_COST_MODIFIER_LAYERING_AUDIT.md` | `docs/CURRENT_STAGE4D_04P_LAYERENGINE_MINIMUM_POWER_ORDERING_AUDIT.md` |
| `docs/CURRENT_STAGE4D_04D_EQUIPMENT_OPTIONAL_TARGET_BRANCH_AUDIT.md` | `docs/CURRENT_STAGE4D_04D_TEMPERED_OPTIONAL_ATTACH_AUDIT.md` |
| `docs/CURRENT_STAGE4D_04E_EQUIPMENT_EXTRA_PAY_BRANCH_AUDIT.md` | `docs/CURRENT_STAGE4D_04E_JAX_TEMPERED_OPTIONAL_ATTACH_TRIGGER_AUDIT.md` and `docs/CURRENT_STAGE4D_04F_AKSHAN_ORANGE_EXTRA_EQUIPMENT_STEAL_AUDIT.md` |

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 46/46 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 604/604 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4483/4483 passed

## 5. Remaining Risk

4D-03BD proves the audit manifest doc anchors resolve to current docs. It does not prove full official PaymentEngine behavior, full official row enumeration, all rollback branches, cross-window generated resource breadth, card matrix alignment, frontend final validation or final completion audit.

项目仍 **NOT READY**；P0-005 remains open.
