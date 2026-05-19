# 4D-03HR-E Switcheroo Power-Swap Blocker Closure Candidate

Status: candidate-only; project **NOT READY**; `fullOfficial=false`; `ready=false`.

Scope: `FU-0b6332bbf0` / `SFD·145/221` / `换换乐` / `SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS`.

- Input manifest: `Post03HqCardMatrixReadinessPaymentCostBloodRushEchoOverwhelmTargetingStackBlockerClosureCandidateManifest`.
- Selected partition: `bd-engine-support-payment-cost`.
- Selected matrix row query: `payment-cost`; secondary row query: `payment-and-targeting-stack-timing`.
- Evidence: Stage 4C-45 Switcheroo guard audit/evidence, P2 preflight fixture, P4 duplicate/base target reject fixtures, direct guard tests, CardBehaviorRegistry entry, and rules evidence index anchors.
- Row transition: `IMPLEMENTED_TESTED+NEEDS_ENGINE_SUPPORT+NEEDS_FAQ_REVIEW` -> `IMPLEMENTED_TESTED+NEEDS_FAQ_REVIEW` for one functional unit and one snapshot entry.
- Count impact: `NEEDS_ENGINE_SUPPORT 300 -> 299`, `payment-or-targeting-stack-timing 488 -> 487`, `payment-and-targeting-stack-timing 198 -> 197`, while automated evidence remains 328, FAQ remains 92, `fullOfficialTrue=0`, `ready=false`.

Non-closure: Switcheroo FAQ adjudication, same-battlefield two-target legality breadth, power-swap duration and cleanup / replacement, LayerEngine continuous-effect power modifier breadth, battle/spell-duel lifecycle, FEPR target ordering and legality, full PaymentEngine / PAY_COST, card matrix, and READY all remain open.

Validation: jq empty passed; PaymentEngineCoverageAuditTests 420/420 passed; dotnet test Riftbound.slnx --no-restore 4991/4991 passed; git diff --check passed.
