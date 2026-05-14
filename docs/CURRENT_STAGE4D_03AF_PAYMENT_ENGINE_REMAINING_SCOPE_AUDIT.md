# Stage 4D-03AF PaymentEngine Remaining Scope Audit

日期：2026-05-14
结论：**AUDIT COMPLETE / PROJECT NOT READY**

本文件是 A 主控对 4D-03AE 之后 P0-005 PaymentEngine 状态的剩余范围审计。它不实现新功能、不关闭 P0-005，也不把 representative evidence 升级为 full official。

## Inputs Inspected

- `docs/A_MASTER_AGENT_GOAL.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- Payment / trigger / move / activated ability / keyword adjacent conformance tests

## Prompt-To-Artifact Checklist

| Requirement | Current Evidence | Status |
| --- | --- | --- |
| Shared authorize / commit / audit primitive | `PaymentCostRules.PaymentPlan`, `AuthorizePayment`, `TryCommitPayment`, `BuildCostPaidPayload`; 4D-03 foundation audit / evidence | Representative foundation exists; not full official |
| `PLAY_CARD` ordinary / optional / extra / keyword costs | 4D-03C plus Haste / Echo / Spellshield / experience representative tests and `COST_PAID` audit payloads | Representative coverage exists; full keyword and all-card matrix remains open |
| `ASSEMBLE_EQUIPMENT` payment windows | 4D-03 foundation, 4D-03K temporary inline, equipment optional-cost fixture coverage | Representative coverage exists; complete equipment keyword lifecycle remains open |
| pending `PAY_COST` / `TRIGGER_PAYMENT` | 4D-03F / 03H / 03AD / 03AE audits; prompt resource choices and temporary aggregate parity now covered | Representative coverage exists; all future trigger payment windows remain open |
| `ACTIVATE_ABILITY` resource / colored / target-bearing windows | 4D-03D, 03I through 03W; `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` is empty | Broad representative coverage exists; complete activated ability family remains open |
| `LEGEND_ACT` | 4D-03B and 4D-03X prove representative legend action path and retired deferred catalog | Representative coverage exists; not all legend / FAQ interactions are full official |
| battlefield trigger / held score payment | 4D-03G / 03AC plus Brush replacement 03AB | Representative coverage exists; full battlefield and replacement ordering remain open |
| `MOVE_UNIT` | Core supports coarse movement, precise `ROAM`, Baron Nest no-ROAM movement; prompt exposes source-specific `sourceRequirements` / `requiredOptionalCosts` | Movement representative exists, but `MOVE_UNIT` is not yet proven as part of a complete PaymentEngine coverage matrix |
| prompt / command parity | Many focused tests compare server prompt choices with command-side acceptance and no-mutation guards | Representative parity exists; no generated exhaustive action-window verifier exists |
| rollback / no-mutation | Shared payment tests plus many focused no-mutation tests across windows | Representative coverage exists; full cross-window failure matrix remains open |
| final readiness gates | Backend tests pass in latest focused slices, formal 18-step already has evidence | P0/P1, frontend smoke, card matrix and final READY audit remain open |

## Current Code Facts

- `PaymentCostRules.PaymentPlan` can express mana, generic power, typed power, experience, optional costs, extra costs, payment resource actions, legal choices and audit metadata.
- `CoreRuleEngine.ResolveMoveUnit` is still a direct movement resolver. It validates timing, source, destination and `ROAM` / Baron Nest movement permissions, then writes movement events; it does not use `PaymentPlan` because current movement representatives do not spend rune resources or experience.
- `MatchSession.MoveUnitMetadataFor` exposes source-specific movement requirements, destination choices, optional cost choices and required optional costs.
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` is empty and `GetAll()` contains current executable representative definitions through Sigil, conversion equipment and Gold token resource skills.

## Remaining P0-005 Gaps

- Full official PaymentEngine is not proven because the project still lacks a single generated coverage matrix that enumerates every action window, payment kind, optional / extra / replacement cost, payment resource action and command failure branch.
- `MOVE_UNIT` has representative `ROAM` / Baron Nest movement support, but it needs an explicit PaymentEngine policy decision: either remain a non-resource movement optional-cost window with dedicated audit guards, or be represented in a coverage verifier as a zero-resource / movement-permission cost window.
- Haste, Echo, Spellshield, replacement, alternative, experience, resource conversion, target tax and payment-only temporary resources are covered through representative slices, not a full all-card official matrix.
- Full `[A]` / `[C]` resource skill family, target-bearing reaction / counter breadth, replacement / prevention interactions and LayerEngine dependencies remain outside P0-005 closure.
- P0-002 / P0-003 / P0-004 board / cleanup / spell-duel / battle lifecycle residuals still limit any final PaymentEngine READY claim.

## Recommended Next Slice

Recommended next handoff: **4D-03AG PaymentEngine Action-Window Coverage Verifier**.

Goal:

- Add a narrow server-side test / audit surface that enumerates current PaymentEngine action windows and marks each as `representative-covered`, `policy-non-resource`, or `remaining-gap`.
- Include `PLAY_CARD`, `PAY_COST`, `ASSEMBLE_EQUIPMENT`, `ACTIVATE_ABILITY`, `LEGEND_ACT`, battlefield held / trigger payment, `HIDE_CARD`, and `MOVE_UNIT`.
- Treat `MOVE_UNIT` explicitly as a movement-permission optional-cost window until a future rule requires rune / experience payment.
- Keep this as audit / verifier work unless it discovers a real prompt / command mismatch.

Suggested write scope:

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` or adjacent conformance test file
- `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_HANDOFF.md`
- `docs/CURRENT_STAGE4D_03AG_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_BASELINE_EVIDENCE.md`

No-go:

- Do not change runtime behavior unless the verifier exposes a concrete mismatch.
- Do not close P0-005.
- Do not update `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- Do not start LayerEngine or frontend work.

## Verdict

4D-03AF establishes the current P0-005 evidence boundary after 4D-03AE. PaymentEngine has strong representative breadth, but full official closure remains unproven. The project remains **NOT READY**.
