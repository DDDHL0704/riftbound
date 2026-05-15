# Stage 4D-03BB PaymentEngine Official Matrix Implementation Handoff

日期：2026-05-16
结论：**A-SIDE HANDOFF ACCEPTED / NO WRITELOCK OPEN / PROJECT NOT READY**

本文件把 4D-03BA 的 12 个 `OFFICIAL_PAYMENT_ENGINE_MATRIX` residual axes 转成下一枚可执行实现交接。它只建立 future B slice 的写入边界、验收口径和实现前基线；本批不改 runtime、tests、frontend 或 card matrix，不派发 B，不打开写锁，也不关闭 P0-005、P1 或 READY。

## Inputs Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BA_PAYMENT_ENGINE_OFFICIAL_MATRIX_RESIDUAL_MANIFEST_EVIDENCE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`

## Current Evidence Boundary

| Surface | Current evidence | Boundary |
| --- | --- | --- |
| Official matrix axes | 4D-03BA locks 12 remaining-official-gap axes: action windows, payment sources, resource skills, target taxes, keyword branches, cost modifiers, optional / extra / alternative costs, replacement / prevention, resource actions, rollback failures, cross-window generation / consumption and card matrix alignment. | Axis-level residuals only; no row schema enumerates concrete official combinations yet. |
| Existing representative manifests | 4D-03AV through 4D-03BA link blocker families and representative anchors to prompt / command / audit / rollback expectations. | Representative evidence is not a complete generated official matrix and cannot prove full official closure. |
| Validation | Focused PaymentEngine coverage, adjacent payment / prompt / hub and backend full are green. | Green tests are implementation-before evidence only; they do not close P0-005 or READY. |
| Card matrix | 4D-03AT recorded Azir representative evidence overlay, and 4D-03BA keeps card matrix alignment as an official matrix axis. | Card matrix remains `fullOfficial=false` for latest representatives and cannot be upgraded by PaymentEngine verifier evidence alone. |

## Recommended Next Slice

Recommended future dispatch: **4D-03BC PaymentEngine Official Matrix Row Schema / Seed Verifier**.

Goal:

- Convert the 4D-03BA axis manifest into an executable row schema so future PaymentEngine work can track concrete matrix rows instead of prose-only gaps.
- Seed the matrix with current representative rows from existing manifests, while marking every row as `representative-seed`, `policy-deferred`, or `missing-official-row`.
- Require each row to identify action window, payment source, modifier / tax / replacement context, prompt anchor, command anchor, audit anchor, rollback expectation, doc anchor and closure status.
- Preserve honesty: the row schema may pass while P0-005 remains open; it must not create `FullOfficialRulePass`, `fullOfficial=true`, READY, or card matrix upgrade language.

Suggested future write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- optional new conformance-test-only helper under `tests/Riftbound.ConformanceTests/`
- `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs after A validation

No-go:

- Do not modify `src/Riftbound.Engine` runtime unless the row schema exposes a concrete prompt / command mismatch and A opens a separate implementation lock.
- Do not change frontend runtime, `ActionPrompt` contracts, browser smoke scripts, card matrix JSON, fullOfficial flags or READY status.
- Do not touch `riftbound-dotnet.sln`.
- Do not claim full official PaymentEngine matrix coverage merely because seeded representative rows are green.

Expected future acceptance:

- Focused `PaymentEngineCoverageAuditTests`.
- Adjacent payment / prompt / hub regression covering `PaymentEngineUnificationTests`, `ResourceSkill`, `SpellshieldTax`, `HasteReady`, `TriggerPayment`, `LegendAct`, `ActionPrompt` and `GameHub`.
- Backend full `dotnet test Riftbound.slnx --no-restore`.
- `git diff --check`.
- Audit / evidence docs updated and closure language still **NOT READY**.

## Pause Point

4D-03BB records the official matrix implementation handoff and baseline only. No B worker is dispatched, no code write lock is open, and the project remains **NOT READY**.
