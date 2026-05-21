4D-03KW-E audit: payment-cost Fiora not-powerful FAQ/layer/battle blocker closure candidate

结论：**NOT READY / GOAL NOT COMPLETE**。本审计只确认 03KW 的单行矩阵 blocker-count reduction 候选边界；不关闭全量卡牌覆盖、FAQ evidence、formal 18-step E2E 或 completion audit。

Candidate identity:
- classification: `post-03kv-e-card-matrix-readiness-payment-cost-fiora-not-powerful-faq-layer-battle-blocker-closure-candidate`
- gate: `E_CARD_MATRIX_READINESS_POST_03KV_PAYMENT_COST_FIORA_NOT_POWERFUL_FAQ_LAYER_BATTLE_BLOCKER_CLOSURE_CANDIDATE`
- manifest: `Post03KwCardMatrixReadinessPaymentCostFioraNotPowerfulFaqLayerBattleBlockerClosureCandidateManifest`
- matrix object: `stage4D03KwPaymentCostFioraNotPowerfulFaqLayerBattleBlockerClosureCandidate`
- previous manifest: `Post03KvCardMatrixReadinessPaymentCostCommanderLedrosOptionalSacrificeSpellshieldRoamTargetingStackBlockerClosureCandidateManifest`

Selected row:
- functionalUnit: `FU-ccf4fc420e`
- card: `OGN·232/298` / `菲奥娜`
- effect: `OGN_FIORA_NOT_POWERFUL_VANILLA_PLAY_UNIT`
- row query: `payment-cost`
- secondary row query: `payment-or-targeting-stack-timing`

Count continuity:
- snapshot entries remain 1009.
- functional units remain 811.
- payment-cost functional units remain 360.
- payment-cost snapshot entries remain 446.
- NEEDS_ENGINE_SUPPORT 218 -> 217.
- primary residual 148 -> 148.
- payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT 406 -> 405.
- payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT 135 -> 135.
- NEEDS_AUTOMATED_TEST_EVIDENCE residual remains 328.
- NEEDS_FAQ_REVIEW residual remains 92.
- primary FAQ residual remains 61.
- fullOfficialTrue remains 0.
- ready remains false.

Evidence reviewed:
- Runtime behavior registry has the OGN Fiora direct card behavior.
- P2 static play fixture covers no-target hand play, 4-cost payment and 4-power unit base entry.
- Paired powerful-boon fixture covers server-authoritative powerful threshold and keyword grant for `法盾` / `游走` / `坚守`.
- Conformance fixture runner target-rejection inline data covers explicit-target rejection.
- `rules-evidence-index.md`, `p2-rules-preflight.md` and `CURRENT_P2_STATUS.md` carry the current Fiora evidence anchors.

Audit boundary:
- This is not FAQ adjudication closure for `SOUL-JFAQ-260114 p15` or `SOUL-JFAQ-260114 p8`.
- This is not an automated evidence disposition closure.
- This is not full powerful keyword / battle / layer official breadth closure.
- This is not one-on-one battle or combat-damage lifecycle closure.
- This is not complete battle / spell-duel lifecycle closure.
- This is not complete LayerEngine / continuous-effect closure.
- This is not complete PaymentEngine / PAY_COST closure.
- This does not change frontend behavior, Chrome smoke scripts, runtime code, protocol fields or official catalog.
- This does not change fullOfficial or readiness.

Validation passed: matrix JSON valid (jq empty); 03KW/03KV active-goal guard 6/6; PaymentEngineCoverageAuditTests 580/580; Fiora focused 3042/3042; adjacent prompt/payment/FAQ/layer/battle/damage/spell-duel/target/stack/boon/power 2702/2702; backend full 5151/5151; git diff --check passed.
