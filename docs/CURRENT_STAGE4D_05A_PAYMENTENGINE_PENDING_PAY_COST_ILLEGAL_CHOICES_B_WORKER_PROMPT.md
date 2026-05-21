# 4D-05A B_SERVER Prompt: Pending PAY_COST Illegal Choice Guard

Date: 2026-05-21

Owner: `B_SERVER`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

## Objective

Add focused server-side coverage for active ordinary non-trigger `PendingPayment` / `PAY_COST` windows where the submitting player and payment window are correct, but the submitted payment choices are illegal.

4D-04Y proved stale replay after an ordinary pending `PAY_COST` window closes. 4D-04Z proved active-window wrong player, wrong `paymentId`, and wrong `paymentWindow` rejection. 4D-05A must prove the complementary active-window payment-choice guard: while an ordinary non-trigger pending payment window is still open, illegal payment choice payloads must be rejected without mutation, without events, without closing the payment window, without spending resources, and without losing the authoritative `PAY_COST` prompt.

This is P0-005 rollback / revalidation narrowing evidence only. It does not close complete PaymentEngine, card matrix, P0/P1, frontend gates, Chrome smoke, formal E2E or READY.

## Allowed Write Scope

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Optional helper extraction inside the same file.
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused tests expose an actual runtime bug.
- Optional 05A audit/evidence docs if needed.

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

Add one focused theory or equivalent tests in `PaymentEngineUnificationTests` that starts from ordinary pending payment states and attempts invalid active-window `PayCostCommand` submissions with the correct `paymentId`, correct `paymentWindow`, and correct player.

Cover mana, generic power and typed power rows if the implementation remains small. Include at least these illegal-choice shapes:

- unknown / unsupported spend choice only, for example `SPEND_MANA:2` or `SPEND_POWER:blue:1`
- legal spend choice plus an additional illegal spend choice
- duplicate legal spend choice
- blank / whitespace payment choice entry

For each rejected submission, assert:

- result rejected
- no events emitted
- exact `MatchStateHasher` hash preserved
- `PendingPayment` still exists with the original `paymentId`, `paymentWindow`, `PlayerId`, and legal choices
- rune pool / typed pool remains unspent
- stack remains unchanged / empty for the fixture
- authoritative `PAY_COST` prompt remains present for the owning player

## Validation

Run at least:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"
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
