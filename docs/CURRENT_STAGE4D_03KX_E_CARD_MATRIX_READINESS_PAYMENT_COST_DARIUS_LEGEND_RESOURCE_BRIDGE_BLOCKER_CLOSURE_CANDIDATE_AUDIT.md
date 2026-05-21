4D-03KX-E audit: payment-cost Darius legend-resource bridge blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本审计只确认 03KX 的单行矩阵 blocker-count reduction 候选边界；不关闭全量卡牌覆盖、FAQ evidence、formal 18-step E2E 或 completion audit。

Candidate identity:
- classification: `post-03kw-e-card-matrix-readiness-payment-cost-darius-legend-resource-bridge-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03KW_PAYMENT_COST_DARIUS_LEGEND_RESOURCE_BRIDGE_BLOCKER_CLOSURE_CANDIDATE`
- manifest: `Post03KxCardMatrixReadinessPaymentCostDariusLegendResourceBridgeBlockerClosureCandidateManifest`
- matrix object: `stage4D03KxPaymentCostDariusLegendResourceBridgeBlockerClosureCandidate`
- previous manifest: `Post03KwCardMatrixReadinessPaymentCostFioraNotPowerfulFaqLayerBattleBlockerClosureCandidateManifest`

Selected row:
- functionalUnit: `FU-246150ecd7`
- card: `OGN·253/298 + OGN·302*/298 + OGN·302/298` / `诺克萨斯之手`
- effect: `LEGEND_ACTION_DOMAIN`
- row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

Count continuity:
- snapshot entries remain 1009.
- functional units remain 811.
- payment-cost functional units remain 360.
- payment-cost snapshot entries remain 446.
- NEEDS_ENGINE_SUPPORT 217 -> 216.
- primary residual 148 -> 148.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 405 -> 404.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 135 -> 134.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328.
- NEEDS_FAQ_REVIEW residual remains 92.
- primary FAQ residual remains 61.
- fullOfficialTrue remains 0.
- ready remains false.

Evidence reviewed:
- Darius origin and premium source-card rows share the same server-authored legend resource bridge ability id.
- CoreRuleEngine and MatchSession bind source-card group parity for `OGN·253/298`, `OGN·302/298` and `OGN·302*/298`.
- LegendResourceBridgeVerifierTests covers prompt, command, generated mana payment, cleanup and rollback behavior for the Darius bridge family.
- ConformanceFixtureRunnerTests keeps prior-card success and no-prior-card rejection coverage for the Darius legend action.
- PaymentEngineCoverageAuditTests records runtime card-row evidence for the three fixed snapshot entries.

Audit boundary:
- This is not an automated evidence disposition closure.
- This is not full legend-resource bridge official breadth closure.
- This is not Inspire / previous-card timing breadth closure.
- This is not generated mana resource lifetime / cleanup breadth closure.
- This is not complete priority / stack timing closure.
- This is not complete FEPR target / stack lifecycle closure.
- This is not complete PaymentEngine / PAY_COST closure.
- This does not change frontend behavior, Chrome smoke scripts, runtime code, protocol fields or official catalog.
- This does not change fullOfficial or readiness.

Validation passed: matrix JSON valid (jq empty); 03KX active-goal guard 1/1; PaymentEngineCoverageAuditTests 580/580; Darius legend resource bridge focused 84/84; adjacent prompt/payment/legend-resource bridge regression 811/811; backend full 5151/5151; git diff --check passed.
