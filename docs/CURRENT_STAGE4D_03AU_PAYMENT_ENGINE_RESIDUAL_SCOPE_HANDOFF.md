# Stage 4D-03AU PaymentEngine Residual Official Scope Handoff

日期：2026-05-16
结论：**A-SIDE HANDOFF ACCEPTED / NO WRITELOCK OPEN / PROJECT NOT READY**

本文件是 A 主控在 4D-03AL / 4D-03AQ / 4D-03AT 与 4D-04Q-B 之后，对 P0-005 PaymentEngine 剩余官方范围的重新路由。它只建立下一批实现前边界和验收口径，不修改 runtime、tests、frontend 或 card matrix，不派发 B，不打开写锁，也不关闭 P0-005、P1 或 READY。

## Inputs Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_STAGE4D_NEXT_DISPATCH_AND_WRITELOCKS.md`
- `docs/CURRENT_STAGE4D_03AF_PAYMENT_ENGINE_REMAINING_SCOPE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AL_PAYMENT_ENGINE_RESOURCE_SKILL_COVERAGE_AUDIT.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`

## Current Evidence Boundary

| Surface | Current evidence | Boundary |
| --- | --- | --- |
| Action windows | 4D-03AH classifies `PLAY_CARD`, `PAY_COST`, `TRIGGER_PAYMENT`, `ASSEMBLE_EQUIPMENT`, `ACTIVATE_ABILITY`, `LEGEND_ACT`, battlefield held score payment, `HIDE_CARD`, and `MOVE_UNIT`. | Representative / policy classification only; not exhaustive official payment matrix. |
| Resource skills | 4D-03AL binds all current `P4ActivatedAbilityCatalog.GetAll().Where(IsResourceSkill)` representatives to family-level anchors. | Current executable catalog only; not full official `[A]` / `[C]` resource skill family. |
| Spellshield target tax | 4D-03AK binds current activated abilities with `AppliesSpellshieldTargetTax=true`. | Activated-ability representatives only; not all payment windows. |
| HASTE_READY | 4D-03AQ binds current implemented HASTE_READY registry profiles and 4D-03AP locks Rek'Sai red exactness. | Representative Haste ready branch only; strong / overflow / non-hand granting / all Haste FAQ breadth remain open. |
| Target-bearing colored-cost abilities | 4D-03AM / 03AN / 03AO / 03AS and related slices cover selected Azir / Maduli / Ezreal / Renata / Crimson Rose / Shadow representatives. | Family breadth, dependency target choice, alternative / extra / replacement costs and all failure branches remain open. |
| Final readiness | Backend full is green at current state. | Backend full does not replace P0/P1 closure, frontend final smoke, formal final run, card matrix full-official, or completion audit READY. |

## Remaining Official P0-005 Blockers

- No generated official PaymentEngine matrix enumerates every action window, payment source, cost modifier, optional / extra / alternative cost, target tax, replacement interaction, payment resource action and no-mutation failure branch.
- Complete `[A]` / `[C]` resource skill breadth is not proven beyond the current 19 representative catalog ability ids.
- Target-bearing colored-cost activated abilities remain representative; official family breadth and dependency target choice are not closed.
- `LEGEND_ACT` resource-action breadth, battlefield skills, full trigger payment resource family and cross-window resource skills remain representative.
- Haste, Echo, Spellshield, experience, battlefield replacement, cost reduction / cost increase, extra / optional payment and temporary-resource quote-command-audit parity are not proven across all official windows.
- P0-002 / P0-003 / P0-004 and P1 LayerEngine / keyword / card-matrix blockers still prevent any PaymentEngine READY claim.

## Recommended Next Slice

Recommended next dispatch: **4D-03AV PaymentEngine Residual Blocker Manifest / Quote-Parity Verifier**.

Goal:

- Extend the server-side audit surface so the remaining P0-005 official blocker families are explicit and test-guarded, not hidden in prose.
- Add a test-only manifest that classifies residual PaymentEngine families as `catalog-bound-representative`, `covered-representative`, `remaining-official-gap`, or `policy-deferred`.
- Require each entry to name current evidence anchors, missing official breadth, no-mutation / rollback expectations and closure status.
- Keep the manifest honest: it may pass while P0-005 remains open, and every closure string must say project **NOT READY**.

Suggested write scope for a future B dispatch:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AV_PAYMENT_ENGINE_RESIDUAL_BLOCKER_MANIFEST_EVIDENCE.md`
- A-side checkpoint / completion / server-rule / closure-plan docs after A validation

No-go:

- Do not modify `src/Riftbound.Engine` runtime unless the verifier exposes a concrete prompt / command mismatch and A opens a separate implementation lock.
- Do not change frontend runtime, `ActionPrompt` contracts, browser smoke scripts, card matrix JSON, fullOfficial flags or READY status.
- Do not touch `riftbound-dotnet.sln`.
- Do not dispatch B from this 4D-03AU handoff; the next implementation needs a fresh explicit write-lock record.

Expected acceptance for a future 4D-03AV implementation:

- Focused `PaymentEngineCoverageAuditTests`.
- Adjacent payment / prompt / hub / keyword regression covering `PaymentEngineUnificationTests`, `ResourceSkill`, `SpellshieldTax`, `HasteReady`, `TriggerPayment`, `LegendAct`, `ActionPrompt`, and `GameHub`.
- Backend full `dotnet test Riftbound.slnx --no-restore`.
- `git diff --check`.
- Audit / evidence docs updated and closure language still **NOT READY**.

## Pause Point

4D-03AU records the PaymentEngine residual scope handoff and baseline only. No B worker is dispatched, no code write lock is open, and the project remains **NOT READY**.
