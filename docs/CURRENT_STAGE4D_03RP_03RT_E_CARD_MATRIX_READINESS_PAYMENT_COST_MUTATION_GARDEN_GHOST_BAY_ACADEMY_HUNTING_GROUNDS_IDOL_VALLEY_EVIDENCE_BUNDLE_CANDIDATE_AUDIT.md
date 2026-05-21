# 4D-03RP..03RT Candidate Audit

Status: audit validated on `DOC_MATRIX_CURRENT`; project remains **NOT READY**.

## Authorization And Write Scope

The latest shared-board actionable entry keeps `DOC_MATRIX_CURRENT` under `APPROVED_ACTIVE_NO_IDLE`. This 03RP-03RT batch stays inside the matrix-number-reduction lane.

## Evidence Audit

| Row | Existing implementation | Evidence note |
|---|---|---|
| `FU-be69df2cec` / `UNL-213/219` / 蜕变花园 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; mutation garden layer/non-play-domain evidence remains representative-only. Row can move to automated-evidence residual only. |
| `FU-d466d0f382` / `UNL-214/219` / 鬼影湾 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Ghost Bay hidden/control/targeting evidence remains representative-only. Row can move to automated-evidence residual only. |
| `FU-3e080d1b63` / `UNL-216/219` / 皮城学院 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Piltover Academy battle/cleanup/layer/targeting evidence remains representative-only. Row can move to automated-evidence residual only. |
| `FU-15ce2642d6` / `UNL-217/219` / 捕猎场 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Hunting Grounds battle/non-play evidence remains representative-only. Row can move to automated-evidence residual only. |
| `FU-41a09f5386` / `UNL-218/219` / 偶像谷 | representative `BATTLEFIELD_RULE_DOMAIN` | Existing battlefield-rule-domain representative implementation; Idol Valley layer/targeting evidence remains representative-only. Row can move to automated-evidence residual only. |

## Count Audit

- all functional units `NEEDS_ENGINE_SUPPORT`: 441 -> 436
- payment-cost `NEEDS_ENGINE_SUPPORT`: 43 -> 38
- primary payment-cost residual: 8 -> 3
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 212 -> 209
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 170 -> 169
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 145 -> 144
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 230 -> 225
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 25 -> 22
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: remains 328
- `NEEDS_FAQ_REVIEW`: remains 92
- primary FAQ residual: remains 61
- `fullOfficialTrue`: remains 0
- `ready`: remains false

## Closure Judgment

This is acceptable only as row-level `NEEDS_ENGINE_SUPPORT` evidence synchronization. Automated evidence, full official breadth, frontend/browser/formal E2E and READY remain open.

## Validation Results

- jq matrix JSON valid
- git diff --check passed
- conflict-marker scan clean
- PaymentEngineCoverageAuditTests 697/697 passed
- ConformanceFixtureRunnerTests 3019/3019 passed
- backend full test 5344/5344 passed
