# 4D-03LD-E payment-cost Miss Fortune legend-action-domain Roam shared-oracle blocker closure candidate

## Scope
- Gate: E_CARD_MATRIX_READINESS_POST_03LC_PAYMENT_COST_MISS_FORTUNE_LEGEND_ACTION_DOMAIN_ROAM_SHARED_ORACLE_BLOCKER_CLOSURE_CANDIDATE
- Classification: post-03lc-e-card-matrix-readiness-payment-cost-miss-fortune-legend-action-domain-roam-shared-oracle-blocker-closure-candidate
- Previous manifest: Post03LcCardMatrixReadinessPaymentCostRumbleMechanicalYordleSharedOracleLayerBattlefieldBlockerClosureCandidateManifest
- Selected functional unit: FU-1c65e004e7
- Selected cards: OGN·267/298 + OGN·309*/298 + OGN·309/298 赏金猎人
- Selected effects: LEGEND_ACTION_DOMAIN

## Matrix Impact
- NEEDS_ENGINE_SUPPORT: 211 -> 210
- Primary residual: 146 -> 146
- Payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 399 -> 398
- Payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT: 131 -> 131
- NEEDS_AUTOMATED_TEST_EVIDENCE residual: 328 -> 328
- NEEDS_FAQ_REVIEW residual: 92 -> 92
- Primary FAQ residual: 61 -> 61
- fullOfficialTrue: 0 -> 0
- ready: false -> false

## Evidence Used
- Existing server-authored `LEGEND_ACT` ability definition for OGN·267/298, OGN·309*/298, and OGN·309/298 in src/Riftbound.Engine/CoreRuleEngine.cs.
- Existing server-authored prompt / candidate wiring for the same legend cards in src/Riftbound.Engine/MatchSession.cs.
- Existing behavior catalog mapping for the three official card numbers in src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs.
- Existing ConformanceFixtureRunnerTests coverage for `P79LegendActGrantsRoamWithMissFortune`.
- Official card text remains from data/official/card-catalog.zh-CN.json; no live website data was fetched.

## Non Closure
- Miss Fortune legend automated evidence disposition remains open.
- Miss Fortune legend full legend-action official breadth remains open.
- Miss Fortune legend Roam/control-zone movement breadth remains open.
- Miss Fortune legend cleanup/replacement-duration breadth remains open.
- Miss Fortune legend LayerEngine/continuous-effect breadth remains open.
- Complete non-play-domain representative matrix remains open.
- Complete PaymentEngine / PAY_COST matrix remains open.
- READY remains open.

## Validation
- validation passed: matrix JSON valid (jq empty); 03LD matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 582/582; Miss Fortune legend focused regression 67/67; adjacent prompt/payment/legend/roam regression 2108/2108; backend full 5153/5153; git diff --check passed.
