# Stage 4D-03BE PaymentEngine Rollback Failure Row Manifest Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BC official matrix row schema 和 4D-03BD doc-anchor integrity guard 之后，补一层 A 侧 rollback failure row manifest。它把 `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` 拆成可执行、可审计的 representative failure families，避免后续关闭 P0-005 时只用一句 "rollback failure branches" 带过。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Failure Families

`RollbackFailureRowManifest` 固定 7 个 representative failure families：

| Family | Scope |
| --- | --- |
| `STALE_PROMPT_PENDING_PAYMENT` | stale prompt / pending payment windows |
| `INVALID_PAYMENT_SOURCE_OR_TRAIT` | invalid resource id, wrong trait, duplicate or unnecessary source |
| `INSUFFICIENT_COST_OR_TARGET_TAX` | mana / power / experience / Spellshield target tax shortfall |
| `STALE_SOURCE_TARGET_OR_OPTION` | target-bearing source, target and target-scoped option stale guards |
| `OPTIONAL_EXTRA_ALTERNATIVE_BRANCH` | optional / extra / alternative / declined / malformed branch guards |
| `REPLACEMENT_PREVENTION_NO_EFFECT` | replacement, prevention and no-effect payment-adjacent guards |
| `GENERATED_RESOURCE_LIFETIME_REUSE` | generated payment-only resource lifetime, wrong-window and duplicate-spend guards |

每个 family 必须保留：

- `OfficialMatrixRowId = ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING`
- payment window scope
- representative surface
- prompt anchor
- command anchor
- audit anchor
- no-mutation anchor
- remaining official breadth
- NOT READY / P0-005 remains open closure status
- resolving docs anchors

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- required rollback failure families exactly once
- each row links back to `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING`
- prompt / command / audit / no-mutation anchors are present
- failure dimensions remain explicit: stale, invalid, insufficient, wrong trait, target, optional, extra, alternative, replacement, generated, duplicate, cross-window
- no row claims `FullOfficialRulePass`, `fullOfficial=true`, P0-005 closure or READY

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 51/51 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 609/609 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4488/4488 passed

## 5. Remaining Risk

4D-03BE proves rollback failure row routing is visible and executable as a representative manifest. It does not prove full official rollback coverage, all payment-source combinations, cross-window generated resource breadth, card matrix alignment, frontend final validation or final completion audit.

项目仍 **NOT READY**；P0-005 remains open.
