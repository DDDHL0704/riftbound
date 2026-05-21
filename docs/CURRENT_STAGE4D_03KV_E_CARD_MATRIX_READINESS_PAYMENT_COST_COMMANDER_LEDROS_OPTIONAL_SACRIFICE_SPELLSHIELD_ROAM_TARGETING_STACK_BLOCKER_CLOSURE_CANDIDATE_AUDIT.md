4D-03KV-E audit: payment-cost Commander Ledros optional-sacrifice Spellshield/Roam targeting-stack blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本审计只确认 03KV 的单行矩阵 blocker-count reduction 候选边界；不关闭全量卡牌覆盖、FAQ evidence、formal 18-step E2E 或 completion audit。

Candidate identity:
- classification: `post-03ku-e-card-matrix-readiness-payment-cost-commander-ledros-optional-sacrifice-spellshield-roam-targeting-stack-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03KU_PAYMENT_COST_COMMANDER_LEDROS_OPTIONAL_SACRIFICE_SPELLSHIELD_ROAM_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- manifest: `Post03KvCardMatrixReadinessPaymentCostCommanderLedrosOptionalSacrificeSpellshieldRoamTargetingStackBlockerClosureCandidateManifest`
- matrix object: `stage4D03KvPaymentCostCommanderLedrosOptionalSacrificeSpellshieldRoamTargetingStackBlockerClosureCandidate`
- previous manifest: `Post03KuCardMatrixReadinessPaymentCostAlbusFerrosNoBoonCallRuneLayerTargetingStackBlockerClosureCandidateManifest`

Selected row:
- functionalUnit: `FU-8d2d30613a`
- card: `OGN·231/298` / `莱卓斯指挥官`
- effect: `OGN_COMMANDER_LETROS_OPTIONAL_SACRIFICE_PLAY_UNIT`
- row query: `payment-cost`
- secondary row query: `payment-and-targeting-stack-timing`

Count continuity:
- snapshot entries remain 1009.
- functional units remain 811.
- payment-cost functional units remain 360.
- payment-cost snapshot entries remain 446.
- NEEDS_ENGINE_SUPPORT 219 -> 218.
- primary residual 149 -> 148.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 407 -> 406.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 136 -> 135.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328.
- NEEDS_FAQ_REVIEW residual remains 92.
- primary FAQ residual remains 61.
- fullOfficialTrue remains 0.
- ready remains false.

Evidence reviewed:
- Runtime behavior registry has the Commander Ledros direct card behavior.
- P2 static play fixture covers no-target hand play, 6-cost payment and 8-power unit base entry with `法盾` / `游走` / `灵体` tags.
- Conformance fixture runner target-rejection inline data covers explicit-target rejection.
- `rules-evidence-index.md` and `p2-rules-preflight.md` carry the current Commander Ledros evidence anchors.

Audit boundary:
- This is not an optional sacrifice / cost-reduction branch closure.
- This is not an automated evidence disposition closure.
- This is not Spellshield target-tax breadth closure.
- This is not Roam / control-zone movement breadth closure.
- This is not cleanup / replacement-duration closure.
- This is not complete FEPR target / stack lifecycle closure.
- This is not complete PaymentEngine / PAY_COST closure.
- This does not change frontend behavior, Chrome smoke scripts, runtime code, protocol fields or official catalog.
- This does not change fullOfficial or readiness.

Validation passed: matrix JSON valid (jq empty); PaymentEngineCoverageAuditTests 580/580; Commander Ledros focused 3021/3021; adjacent prompt/payment/Spellshield/Roam/control/cleanup 2080/2080; backend full 5151/5151; git diff --check passed.
