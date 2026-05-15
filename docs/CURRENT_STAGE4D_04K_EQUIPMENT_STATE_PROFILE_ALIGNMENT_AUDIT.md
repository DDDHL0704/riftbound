# Stage 4D-04K Equipment State Profile Alignment Audit

日期：2026-05-15
结论：**IMPLEMENTED AND A-VALIDATED / PROJECT NOT READY**

本文件记录 4D-04K-B 的 A 侧验收。该切片只做 equipment keyword profile / verifier alignment，不实现新 runtime，不修改 frontend，不更新 card matrix JSON，不升级 `fullOfficial`，不关闭 P1-001、P1-002 或 READY。

## 1. Scope

B-Implementation / Meitner `019e2c13-5b3b-7750-9971-08cf68b074f2` 在 4D-04K 写锁内完成：

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

未触碰：

- runtime semantics / `CoreRuleEngine.cs`
- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- fullOfficial / READY status
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

A 接受该切片，因为当前 diff 满足 handoff：

1. `CardEquipmentKeywordRules` 新增 Long Sword equipment-state representative manifest。
2. Manifest 明确记录 P5 state representative boundaries：
   - owner/controller/attachment invariant。
   - controller mismatch no-mutation rejection。
   - controlled opponent-owned target attach。
   - attached equipment follows host base-to-battlefield movement。
   - attached equipment follows host battlefield-to-base movement。
   - host destroyed detach / recall to owner base。
3. `CardEquipmentKeywordProfile` 暴露 `HasImplementedRepresentativeEquipmentStateBoundary` 与 `EquipmentStateRepresentativeVerifierTests`。
4. `CardCatalogBaselineTests` 新增 profile verifier，通过反射确认 manifest 绑定的 `ConformanceFixtureRunnerTests` P5 anchors 仍真实存在。
5. Long Sword 仍为 `recognized-deferred`，但 reason 明确承认 `P5 equipment state representatives`。
6. Deferred wording 继续保留 full owner/controller breadth、full attach lifecycle breadth、Agile reaction timing、Jax-granted Agile、full Tempered official breadth、static modifiers、copy-text effects、LayerEngine 与 full equipment official coverage。
7. Assemble-only representatives 未降级；Agile / Tempered / Weapon deferred rows 仍可见。

## 3. Verification

A 侧验收命令与结果记录在 `docs/CURRENT_STAGE4D_04K_EQUIPMENT_STATE_PROFILE_ALIGNMENT_EVIDENCE.md`：

- Focused state / profile guard：12/12 passed。
- Adjacent equipment regression：195/195 passed。
- `git diff --check` passed。

## 4. Residuals

仍未关闭：

- P1-001。
- P1-002。
- full owner/controller breadth。
- full attach lifecycle breadth。
- Agile reaction timing。
- Jax-granted Agile。
- full Tempered official breadth。
- copy-text effects。
- other equipment static modifiers。
- full LayerEngine。
- card matrix fullOfficial。
- frontend final validation。
- READY / active goal completion。

## 5. Verdict

4D-04K-B is accepted and its profile-verifier write lock is closed. This improves audit/profile truthfulness by binding existing P5 equipment-state evidence, but it is not a runtime capability expansion and cannot be used as a completion proxy. Project remains **NOT READY**.
