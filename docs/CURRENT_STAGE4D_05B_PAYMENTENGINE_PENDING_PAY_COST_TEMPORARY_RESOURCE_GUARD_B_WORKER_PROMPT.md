# 4D-05B B_SERVER Prompt: Pending PAY_COST Temporary Resource Guard

Date: 2026-05-21

Owner: `B_SERVER`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

## Objective

Add focused server-side coverage for active ordinary non-trigger `PendingPayment` / `PAY_COST` windows that include temporary / generated payment-resource actions.

4D-04Y proved stale ordinary pending `PAY_COST` replay rejection. 4D-04Z proved active-window identity guard rejection. 4D-05A proved active-window illegal ordinary payment-choice rejection. 4D-05B must prove the complementary active pending-window temporary-resource guard: forged, missing, wrong-owner, exhausted, wrong-kind, duplicate or unnecessary temporary payment-resource actions must reject without mutation, without events, without closing the payment window, without spending resources, without consuming / clearing temporary resources, and without losing the authoritative `PAY_COST` prompt.

This is P0-005 rollback / revalidation narrowing evidence only. It does not close complete PaymentEngine, card matrix, P0/P1, frontend gates, Chrome smoke, formal E2E or READY.

## Allowed Write Scope

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Optional helper extraction inside the same file.
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused tests expose an actual runtime bug.
- Optional 05B audit/evidence docs if needed.

## Locked Scope

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- Frontend files
- Official catalog / source snapshot files
- API/protocol core fields
- Browser / Chrome / formal E2E scripts
- `fullOfficial`
- READY / READY-CANDIDATE status
- `riftbound-dotnet.sln`

## Required Coverage

Add one focused theory or equivalent tests in `PaymentEngineUnificationTests` that starts from active ordinary non-trigger pending `PAY_COST` temporary-resource states and submits `PayCostCommand` with the correct player, correct `paymentId`, and correct `paymentWindow`.

Prefer using / extending `PendingGenericPayCostTemporaryResourceState` and `PendingTypedPayCostTemporaryResourceState`. Cover a compact but meaningful set of invalid temporary-resource shapes, such as:

- missing / forged temporary resource action id
- wrong owner temporary resource
- remaining power is zero
- wrong allowed payment kind
- duplicate temporary resource action id
- unnecessary temporary resource action when the rune pool already can pay
- if small, typed-resource wrong trait or generic resource used against typed-only payment

For each rejected submission, assert:

- result rejected
- no events emitted
- exact `MatchStateHasher` hash preserved
- `PendingPayment` still exists with the original `paymentId`, `paymentWindow`, `PlayerId`, and legal choices
- `TemporaryPaymentResources` are preserved, not consumed or cleared
- rune pool / typed pool remains unspent
- stack remains unchanged / empty for the fixture
- authoritative `PAY_COST` prompt remains present for the owning player

## Validation

Run at least:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~TemporaryPaymentResource"
git diff --check
```

If runtime code changes, also run full backend:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## Stop Conditions

Stop and report to A_MAIN if:

- Any backend test fails after the intended change.
- The fix would require protocol core field changes.
- Hidden information leakage is observed.
- The implementation requires matrix JSON or `PaymentEngineCoverageAuditTests.cs` edits.
- The behavior conflicts with the service-side authority principle.
