# Stage 4D-03BL PaymentEngine Rollback Failure Matrix Handoff

日期：2026-05-16
结论：**A-SIDE HANDOFF ACCEPTED / NO WRITELOCK OPEN / PROJECT NOT READY**

本文件把 4D-03BE 的 `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` 从 representative failure-family manifest 收敛为下一枚 B-side implementation / verifier 交接。它只建立 future 4D-03BL-B 的写入范围、验收口径和实现前基线；本批不改 runtime、tests、frontend 或 card matrix，不派发 B，不打开写锁，也不关闭 P0-005、P1、full-card matrix 或 READY。

## Inputs Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_ACTIVE_GOAL_PROMPT_ARTIFACT_CHECKLIST.md`
- `docs/CURRENT_STAGE4D_03BC_PAYMENT_ENGINE_OFFICIAL_MATRIX_ROW_SCHEMA_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BE_PAYMENT_ENGINE_ROLLBACK_FAILURE_ROW_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BH_PAYMENT_ENGINE_MISSING_ROW_DOWNSTREAM_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BK_PAYMENT_ENGINE_POLICY_DEFERRED_MOVE_UNIT_BOUNDARY_AUDIT.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`

## Current Evidence Boundary

| Surface | Current evidence | Boundary |
| --- | --- | --- |
| Official row schema | 4D-03BC fixes 13 PaymentEngine official-matrix rows and marks `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` as `missing-official-row`. | The row is visible and executable as a gap, but not full official rollback coverage. |
| Downstream routing | 4D-03BH proves all missing official rows have downstream representative manifests; 4D-03BE owns rollback failures. | Routing completeness does not prove all rollback combinations are covered. |
| Rollback family manifest | 4D-03BE splits rollback failures into 7 families: stale prompt / pending payment, invalid source / trait, insufficient cost / target tax, stale source / target / option, optional / extra / alternative branch, replacement / prevention / no-effect, generated resource lifetime / duplicate spend. | Families are representative and prose-bounded; they are not yet an all-window generated rollback matrix. |
| Policy-deferred boundary | 4D-03BK keeps `ROW_MOVE_UNIT_POLICY_DEFERRED` outside payment manifests. | Future rollback work must not pull movement-permission policy rows into PaymentEngine payment failure coverage. |
| Latest baseline | 03BL baseline passed focused 70/70, adjacent 628/628 and backend full 4507/4507. | Green baseline is pre-implementation evidence only; it does not close P0-005 or READY. |

## Recommended Future Slice

Recommended future dispatch: **4D-03BL-B PaymentEngine rollback failure official matrix expansion**.

Goal:

- Convert the 7 rollback failure families into an executable all-window matrix for current PaymentEngine payment surfaces.
- Require each row to bind action window, failure dimension, payment source kind, prompt quote, command rejection, no-mutation state assertion, audit expectation and doc anchor.
- Cover at least these windows explicitly: `PLAY_CARD`, `PAY_COST`, `ACTIVATE_ABILITY`, `ASSEMBLE_EQUIPMENT`, `TRIGGER_PAYMENT` and `BATTLEFIELD_HELD_SCORE_PAYMENT`.
- Keep representative seed rows, missing official rows and policy-deferred movement rows separate; do not treat `MOVE_UNIT` as a PaymentEngine payment rollback row.
- Preserve closure honesty: the slice may reduce rollback failure uncertainty, but it must not create `FullOfficialRulePass`, `fullOfficial=true`, P0-005 closure, READY, or card matrix upgrades.

Suggested future write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- focused PaymentEngine rollback / failure / no-mutation tests under `tests/Riftbound.ConformanceTests/`
- minimal `src/Riftbound.Engine/CoreRuleEngine.cs` or `src/Riftbound.Engine/MatchSession.cs` changes only if a new rollback verifier exposes a real runtime mismatch and A opens the 4D-03BL-B runtime lock
- `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_AUDIT.md`
- `docs/CURRENT_STAGE4D_03BL_PAYMENT_ENGINE_ROLLBACK_FAILURE_MATRIX_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan / dispatch docs after A validation

No-go:

- Do not rewrite broad PaymentEngine payment planning, stack resolution, battle lifecycle or cleanup queues.
- Do not modify frontend runtime, browser smoke scripts, card matrix JSON, `fullOfficial` flags, READY status or `riftbound-dotnet.sln`.
- Do not use green representative failures to claim complete rollback coverage across all official payment rows.
- Do not create a B-side write lock without a fresh dispatch note that names the owner, files and validation gates.

Expected future acceptance:

- Focused PaymentEngine coverage / rollback matrix tests.
- Adjacent payment / prompt / hub regression covering `PaymentEngineUnificationTests`, `ResourceSkill`, `SpellshieldTax`, `HasteReady`, `TriggerPayment`, `LegendAct`, `ActionPrompt` and `GameHub`.
- Backend full `dotnet test Riftbound.slnx --no-restore`.
- `git diff --check`.
- Audit / evidence docs updated and closure language still **NOT READY** unless the full objective is actually proven.

## Pause Point

4D-03BL records the next PaymentEngine rollback failure official-matrix handoff and current baseline only. No B worker is dispatched, no code write lock is open, and the project remains **NOT READY**.
