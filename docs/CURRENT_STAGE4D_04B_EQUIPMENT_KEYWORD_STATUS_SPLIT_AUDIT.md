# Stage 4D-04B Equipment Keyword Status Split Audit

日期：2026-05-15
结论：**IMPLEMENTED AND A-VALIDATED / P1-002 STILL OPEN / PROJECT NOT READY**

本文件记录 4D-04B B-Implementation 对 P1-002 equipment keyword execution-boundary status split 的实现与 A 侧验收。本批只调整 equipment keyword profile/report 口径，不修改 frontend，不修改 card matrix，不升级 full-official，不关闭 READY。

## 1. Scope

本批覆盖：

- `src/Riftbound.Engine/CardEquipmentKeywordRules.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

本批不覆盖：

- frontend runtime / DevUi local rules
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad equipment runtime rewrite
- Azir / Maduli / Ezreal historical focused slices
- battle lifecycle, PaymentEngine broad refactor, LayerEngine broad implementation
- `riftbound-dotnet.sln`

## 2. Implementation Summary

4D-04B adds a narrow status split for equipment keyword coverage:

- `EquipmentKeywordProfileStatuses.ImplementedRepresentative` now exists for equipment keyword profile rows.
- `CardEquipmentKeywordProfile` exposes `HasImplementedRepresentativeAssembleBoundary`.
- `CardEquipmentKeywordRules.BuildProfile` marks registered assemble-only representatives as `implemented-representative`, while mixed equipment cards with agile / tempered / weapon / static breadth remain `recognized-deferred`.
- `ActionPromptBuilder.HasImplementedRepresentativeAssembleEquipmentProfile` provides a read-only check against existing `ImplementedAssembleEquipmentProfiles`; no assemble runtime semantics were changed.
- `CardCatalogBaselineTests` now asserts equipment coverage has both implemented representative and deferred equipment rows, and guards that agile / tempered / weapon residuals remain deferred.

## 3. Accepted Evidence

Evidence is recorded in `docs/CURRENT_STAGE4D_04B_EQUIPMENT_KEYWORD_STATUS_SPLIT_EVIDENCE.md`.

A-side verification:

- focused equipment keyword/profile guard: 4/4 passed
- adjacent equipment fixture / AssembleEquipment guard: 98/98 passed
- broader keyword family guard: 8/8 passed
- backend full: 4355/4355 passed
- `git diff --check`: passed

## 4. Residuals

P1-002 remains open. This status split does not implement or claim:

- `灵便` reaction attachment
- `百炼` optional attachment
- weapon / static equipment modifiers
- copy-text effects
- owner/controller changes
- attach lifecycle breadth
- full equipment official coverage
- LayerEngine or continuous-effect replacement
- full-card matrix coverage

## 5. Verdict

4D-04B is accepted as a narrow P1-002 equipment keyword status split. It improves the honesty and granularity of keyword coverage reporting by distinguishing implemented representative assemble boundaries from still-deferred official breadth. It does not close P1-002, LayerEngine, full-card matrix, frontend final validation or READY.
