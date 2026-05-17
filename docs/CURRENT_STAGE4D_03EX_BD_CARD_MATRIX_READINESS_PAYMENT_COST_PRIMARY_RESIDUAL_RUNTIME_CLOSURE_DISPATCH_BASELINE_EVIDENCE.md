# 4D-03EX-BD Payment-Cost Primary Residual Runtime Closure Dispatch Baseline Evidence

4D-03EX-BD is a fresh A dispatch for B/D only. It uses 4D-03EW-E matrix gate-hold evidence and 4D-03ET-BD primary residual verifier evidence as input. A does not implement runtime in this dispatch batch.

## Inputs

- `Post03EwCardMatrixReadinessPaymentCostMatrixReadinessGateHoldEvidenceManifest`
- `Post03EtCardMatrixReadinessEngineSupportPaymentCostPrimaryResidualVerifierEvidenceManifest`
- `Post03EpCardMatrixReadinessEngineSupportPaymentCostImplementationDispatchManifest`
- `Post03ErCardMatrixReadinessEngineSupportPaymentCostClosureReadinessAuditManifest`
- `Post03EgCardMatrixReadinessJsonWriteAuthorizationBlockerDispositionVerifierManifest`

## Dispatch Contract

```txt
classification=post-03ew-bd-card-matrix-readiness-payment-cost-primary-residual-runtime-closure-dispatch
gate=B_D_ENGINE_SUPPORT_POST_03EW_PAYMENT_COST_PRIMARY_RESIDUAL_RUNTIME_CLOSURE_DISPATCH
owner=B/D_ENGINE_SUPPORT
dispatch lane=lane-1-bd-primary-engine-support-residual
selected partition=bd-engine-support-payment-cost
selected matrix row query=payment-cost
selected blocker=NEEDS_ENGINE_SUPPORT
dispatch mode=fresh B/D runtime + verifier closure dispatch
```

## Baseline Counts

```txt
payment-cost functionalUnits=360
NEEDS_ENGINE_SUPPORT=360
NEEDS_AUTOMATED_TEST_EVIDENCE=328
NEEDS_FAQ_REVIEW=92
IMPLEMENTED_TESTED=31
SHARED_ORACLE_IMPLEMENTATION=52
primary NEEDS_ENGINE_SUPPORT residual=216
primary NEEDS_FAQ_REVIEW residual=61
snapshot entries=1009
functional units=811
fullOfficialTrue=0
ready=false
```

## Allowed Future B/D Scope

The future B/D implementation or verifier may touch only the local PaymentCost primary residual surfaces:

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs` around `ResolvePendingPayCost`, `BuildPendingPaymentPlan`, and `PaymentPlan` commit
- `src/Riftbound.Engine/MatchSession.cs` around `PAY_COST` prompt, pending payment snapshot, and payment metadata
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- optional new `tests/Riftbound.ConformanceTests/PaymentCostPrimaryResidualClosureTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` for A acceptance guard only

No matrix JSON write window is open. `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains locked and must stay at `fullOfficialTrue=0` / `ready=false`.

## Verification Required Later

Future B/D acceptance must provide focused runtime/verifier test evidence, focused `PaymentEngineCoverageAuditTests`, backend full test, payment-cost row-query trace, current `fullOfficial=false` continuity, no matrix JSON write proof, and A acceptance audit.

Current A-side dispatch validation: focused `PaymentEngineCoverageAuditTests` 272/272, backend full 4841/4841, `git diff --check` passed.

## Non-Closure

4D-03EX-BD does not close payment-cost blocker closure, B/D_ENGINE_SUPPORT, P0-005, P0-004 adjacency audit-sensitive, P1, full official PaymentEngine matrix closure, E_CARD_MATRIX_READINESS, card matrix or READY. Project remains **NOT READY**.
