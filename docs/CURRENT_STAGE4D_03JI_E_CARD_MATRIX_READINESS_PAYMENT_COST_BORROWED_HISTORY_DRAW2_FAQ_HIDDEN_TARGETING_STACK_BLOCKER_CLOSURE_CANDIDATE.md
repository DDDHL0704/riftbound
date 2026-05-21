# 4D-03JI-E Payment-Cost Borrowed History Draw-2 FAQ Hidden Targeting-Stack Blocker Closure Candidate

日期：2026-05-20
结论：NOT READY / GOAL NOT COMPLETE

本批由 A/E 侧只做 card matrix row-level evidence 入账，不修改 runtime、frontend、Chrome/browser script、formal 18-step script、official catalog、protocol core fields、`fullOfficial` status 或 READY 标志。

## Scope

- Gate: `E_CARD_MATRIX_READINESS_POST_03JH_PAYMENT_COST_BORROWED_HISTORY_DRAW2_FAQ_HIDDEN_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE`
- Manifest: `Post03JiCardMatrixReadinessPaymentCostBorrowedHistoryDraw2FaqHiddenTargetingStackBlockerClosureCandidateManifest`
- Classification: `post-03jh-e-card-matrix-readiness-payment-cost-borrowed-history-draw2-faq-hidden-targeting-stack-blocker-closure-candidate`
- Input previous closure candidate manifest: `Post03JhCardMatrixReadinessPaymentCostRemorseOrbPlayEquipmentBlockerClosureCandidateManifest`
- Selected functional unit: `FU-f00de407f3`
- Selected card: `OGN·083/298` 借鉴历史
- Selected effect: `BORROWED_HISTORY_DRAW_2`

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 258 -> 257
- Primary residual: 174 -> 174
- `payment-or-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 446 -> 445
- `payment-and-targeting-stack-timing NEEDS_ENGINE_SUPPORT`: 164 -> 163
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328
- `NEEDS_FAQ_REVIEW`: 92 -> 92
- Primary FAQ residual: 61 -> 61
- `fullOfficialTrue`: 0 -> 0
- `ready`: false -> false

## Evidence

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-borrowed-history-draw-stack.fixture.json`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `data/official/card-catalog.zh-CN.json`
- `docs/p2-rules-preflight.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_P4_STATUS.md`

## Non-Closure

Borrowed History automated evidence disposition remains open. Borrowed History FAQ adjudication, standby / reaction timing path, hidden-info / draw redaction matrix, complete battle / spell-duel lifecycle matrix, complete FEPR target / stack lifecycle matrix, full PaymentEngine / PAY_COST matrix, card matrix closure, formal 18-step E2E and READY remain open.

## Validation

Prevalidation passed: `BorrowedHistory|PaymentEngine|Draw` focused regression 689/689 passed; `ActionPrompt|Prompt|BorrowedHistory|PaymentResource|SpendPower|RunePool|Draw|Hidden|Redaction` adjacent regression 453/453 passed. Final validation passed: `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; `BorrowedHistory|PaymentEngine|Draw` focused regression 691/691 passed; `ActionPrompt|Prompt|BorrowedHistory|PaymentResource|SpendPower|RunePool|Draw|Hidden|Redaction` adjacent regression 456/456 passed; `PaymentEngineCoverageAuditTests` 504/504 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5075/5075 passed; `git diff --check` passed.
