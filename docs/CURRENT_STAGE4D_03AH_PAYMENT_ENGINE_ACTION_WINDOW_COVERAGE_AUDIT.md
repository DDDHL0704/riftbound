# Stage 4D-03AH PaymentEngine Action-Window Coverage Audit

日期：2026-05-14
结论：**FOCUSED AUDIT ACCEPTED / PROJECT NOT READY**

本切片新增 server-side conformance audit surface：`tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`。它不修改 runtime，不新增卡，不修改前端或 coverage matrix；只把当前 PaymentEngine 相关 action windows 明确列入分类 manifest，防止后续误把 representative coverage 当作 P0-005 full official closure。

## Manifest Policy

分类只允许：

- `representative-covered`：已有 focused prompt / command / audit / rollback evidence 的代表窗口。
- `policy-non-resource`：当前不是 rune / mana / experience payment window，而是 movement-permission / optional-cost policy 的窗口。
- `remaining-gap`：必须显式命名并绑定未来工作；本次 manifest 没有静默 unclassified entry。

所有 entry 都必须包含 evidence summary、test anchors、doc anchors 与 closure status。closure status 必须明确项目仍 **NOT READY**，P0-005 仍未 full official closure。

## Classified Windows

| Action window | Classification | Evidence summary |
| --- | --- | --- |
| `PLAY_CARD` | `representative-covered` | Shared PaymentPlan / prompt / command / audit representatives cover mana, generic / typed rune power, recycle / temporary resources, optional / extra costs, and 4D-03AG typed resource prompt parity. |
| `PAY_COST` | `representative-covered` | Pending PAY_COST representatives cover recycle resources, temporary payment-only resources, aggregate prompt metadata, spent / cleared ledger events, and rollback guards. |
| `TRIGGER_PAYMENT` | `representative-covered` | SFD Fiora trigger payment representatives cover typed-yellow recycle / temporary resources, prompt aggregate parity, command commit, audit events, stale guards, and decline behavior. |
| `ASSEMBLE_EQUIPMENT` | `representative-covered` | Equipment assembly representatives cover shared plan commit, typed costs, recycle actions, temporary inline consumption, prompt metadata, and no-mutation guards. |
| `ACTIVATE_ABILITY` | `representative-covered` | Activated ability representatives cover payment resources, typed costs, experience costs, target tax, stack timing, resource skills, temporary ledgers, and rollback. |
| `LEGEND_ACT` | `representative-covered` | Legend action representatives remain on the `LEGEND_ACT` action-domain path; 4D-03X retired old deferred ACTIVATE_ABILITY legend surfaces. |
| `BATTLEFIELD_HELD_SCORE_PAYMENT` | `representative-covered` | Battlefield held score representatives cover pay-4-power, typed power, recycle resources, temporary resources, mixed prompt / command, and score-prevention rollback. |
| `HIDE_CARD` | `representative-covered` | Standby hide-card representatives cover standard A-cost, Teemo mana alternative, free standby, audit payloads, and insufficient-cost rollback. |
| `MOVE_UNIT` | `policy-non-resource` | `MOVE_UNIT` remains a server-authoritative movement-permission / optional-cost policy window. ROAM and Baron Nest choices are not rune, mana, experience, or temporary-resource payment windows. |

## Guardrails

- `PLAY_CARD` must reference 4D-03AG typed resource prompt parity evidence.
- `MOVE_UNIT` must stay `policy-non-resource` and must not be mislabeled as a resource-payment window.
- Every manifest entry must retain test/doc anchors and NOT READY / P0-005-open closure text.
- The verifier can pass while P0-005 remains open; it is a checklist gate, not a full official rules claim.

## Modified Files

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AH_PAYMENT_ENGINE_ACTION_WINDOW_COVERAGE_EVIDENCE.md`
- `docs/CURRENT_A_MASTER_CHECKPOINT.md`
- `docs/CURRENT_COMPLETION_AUDIT.md`
- `docs/CURRENT_RULE_EVIDENCE_TODO.md`
- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`

## Verdict

4D-03AH adds the action-window coverage verifier requested by 4D-03AF / 4D-03AH. The verifier strengthens P0-005 audit discipline but does **not** close P0-005, P1, READY, LayerEngine, frontend final validation, or full-card official evidence. Project remains **NOT READY**.
