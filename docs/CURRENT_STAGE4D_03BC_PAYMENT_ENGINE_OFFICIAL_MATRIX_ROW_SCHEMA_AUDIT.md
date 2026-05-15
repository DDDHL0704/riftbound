# Stage 4D-03BC PaymentEngine Official Matrix Row Schema Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批把 4D-03BB handoff 中的下一枚 4D-03BC row schema / seed verifier 落到 `PaymentEngineCoverageAuditTests.cs`。它只在 conformance test 层为 4D-03BA 的 12 个 official matrix residual axes 建立 row-level schema 与 seed/missing/deferred 分类，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，也不关闭 P0-005 或 READY。

写入范围：

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md`
- A 主控 checkpoint / completion / server audit / P0-P1 closure plan / dispatch writelock 文档

## 2. Row Schema

`OfficialPaymentEngineMatrixSeedRowManifest` 新增 13 个 row entries。每行必须保留：

- `RowId`
- residual `Axis`
- `RowStatus`
- action window
- payment or policy profile
- representative scope
- prompt anchor
- command anchor
- audit anchor
- rollback expectation
- remaining official breadth
- NOT READY closure status
- docs anchors

Row status 仅允许：

- `representative-seed`
- `missing-official-row`
- `policy-deferred-row`

## 3. Row Coverage

本批建立的 row-level entries：

| Row | Axis | Status | Window |
| --- | --- | --- | --- |
| `ROW_ACTION_WINDOWS_PLAY_CARD_TYPED_RESOURCE_SEED` | `ACTION_WINDOWS` | `representative-seed` | `PLAY_CARD` |
| `ROW_PAYMENT_SOURCES_PAY_COST_TEMPORARY_SEED` | `PAYMENT_SOURCES` | `representative-seed` | `PAY_COST` |
| `ROW_RESOURCE_SKILLS_MALZAHAR_TARGET_AS_COST_SEED` | `RESOURCE_SKILLS` | `representative-seed` | `ACTIVATE_ABILITY` |
| `ROW_TARGET_TAXES_XERATH_SPELLSHIELD_SEED` | `TARGET_TAXES` | `representative-seed` | `ACTIVATE_ABILITY` |
| `ROW_KEYWORD_BRANCHES_HASTE_READY_SEED` | `KEYWORD_BRANCHES` | `representative-seed` | `PLAY_CARD` |
| `ROW_COST_MODIFIERS_BATTLEFIELD_EQUIPMENT_SEED` | `COST_MODIFIERS` | `representative-seed` | `PLAY_CARD` |
| `ROW_OPTIONAL_EXTRA_ALTERNATIVE_AZIR_REATTACH_SEED` | `OPTIONAL_EXTRA_ALTERNATIVE_COSTS` | `representative-seed` | `ACTIVATE_ABILITY` |
| `ROW_REPLACEMENT_PREVENTION_BATTLEFIELD_HELD_SEED` | `REPLACEMENT_PREVENTION` | `representative-seed` | `BATTLEFIELD_HELD_SCORE_PAYMENT` |
| `ROW_RESOURCE_ACTIONS_TRIGGER_PAYMENT_SEED` | `RESOURCE_ACTIONS` | `representative-seed` | `TRIGGER_PAYMENT` |
| `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` | `ROLLBACK_FAILURE_BRANCHES` | `missing-official-row` | `ALL_PAYMENT_WINDOWS` |
| `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING` | `CROSS_WINDOW_GENERATION_CONSUMPTION` | `missing-official-row` | `ALL_GENERATION_AND_CONSUMPTION_WINDOWS` |
| `ROW_CARD_MATRIX_ALIGNMENT_MISSING` | `CARD_MATRIX_ALIGNMENT` | `missing-official-row` | `ALL_CARD_PAYMENT_ROWS` |
| `ROW_MOVE_UNIT_POLICY_DEFERRED` | `ACTION_WINDOWS` | `policy-deferred-row` | `MOVE_UNIT` |

This intentionally keeps 9 representative seeds, 3 missing official rows and 1 policy-deferred row separate. The seeded rows point at current executable representative evidence; the missing rows say what still has no full official row matrix; the MOVE_UNIT row remains deferred because current movement permission is not a mana/power/experience/temporary-resource payment row.

## 4. Guards Added

新增 focused verifier 覆盖以下约束：

- required row ids exactly once
- schema fields and closure anchors are present
- every 4D-03BA residual axis appears in row schema coverage
- representative seed / missing official / policy deferred rows stay separate
- concrete row breadth remains visible across action windows, payment sources, resource skills, target taxes, cost modifiers, optional/alternative/replacement/resource-action rows, rollback, cross-window resource generation and card matrix alignment
- no row claims `FullOfficialRulePass`, `fullOfficial=true`, P0-005 closure or READY

## 5. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 45/45 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 603/603 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4482/4482 passed

## 6. Remaining Risk

4D-03BC improves official matrix row routing, but it is still not full official PaymentEngine completion. Remaining blockers include full official row enumeration, all payment-source combinations, full rollback failure matrix, cross-window generated resource lifetime / consumption breadth, card matrix alignment, frontend final validation and the final completion audit.

项目仍 **NOT READY**；P0-005 remains open.
